# LOBBY_SYSTEM

LJ 로비의 기능 연결·수명·스테이지 목록/CSV 계약입니다. 완료 이력은 [DONE](../DONE.md), 남은 작업은 [TODO](../TODO.md#로비-연결검증)에 둡니다.

## 구성

| 구성 | 책임 |
| --- | --- |
| LobbyController | 진행 사용권·로비 기능 연결과 요청 순서 조율 |
| StageSelectionData | 스테이지 목록 설정과 유효성 검사 |
| LobbyStageInfo | 맵 ID·이름·설명·이미지·지원 난이도 |
| StageEntrySelection / EntryPreparationResult | 확정한 맵/난이도/무기와 입장 준비 결과 |
| LobbyEnemySource / LobbyEnemyQuery | 실제 등장 구성 제공 계약 / 도감 매핑과 표시 조회 |
| SceneFlow / SceneMove | 입장·재도전·복귀 조율 / 종료 완료 후 이동 허용 |
| StageSelectionCsvImporter | CSV 검사·최종 목록 병합·설정 저장 |
| StageSelectionCsvImporterWindow | 입력 선택·미리보기·적용 요청과 창 수명 |

구매·강화·미션·도감 규칙과 저장은 각 기능이 담당합니다. 로비는 플레이 객체 생성·웨이브 진행·씬 로드를 수행하지 않습니다. 목록 설정은 플레이용 맵/웨이브 설정과 별개입니다.

## 초기화와 해제

```text
활성 로비에서 TryOpen
→ 필수 참조/설정 검사 → GameManager.StartupProgress가 Ready인지 확인
→ TryAttachScene으로 확정 진행 연결
→ 강화/미션/인벤토리/도감·해금 조회 객체 복원과 구매 연결
→ 첫 스테이지/기본 난이도 선택 → 재화 초기화
→ 내부 이벤트 구독 → Ready → OnInitialized → OnStateChanged
```

- 첫 타이틀에서 진행을 준비하며 로비는 파일을 다시 읽거나 신규 생성하지 않습니다. 준비 실패는 GameManager의 사유를 반환합니다. 로비 초기화가 실패하면 내부 구독 전에 부분 연결과 사용권을 정리하고 Closed 상태를 알립니다. [진행 준비](SAVE_SYSTEM.md#초기화)
- TryReleaseProgress는 초기화/종료 중 또는 미션 처리 중이면 거절합니다. 정상 해제는 Closing → OnReleasing → 내부 구독 해제·객체 참조 정리 → 진행 사용권 반환 → Closed·상태 알림 순서입니다.
- 비활성화는 진행 반환이나 재초기화가 아닙니다. OnDestroy는 알림·새 저장 없이 연결만 정리하므로 필수 저장은 정상 이동 절차에서 먼저 처리합니다.

## UI 조회와 요청

UI는 로비 이벤트를 구독한 뒤 현재 초기화 상태를 확인하고 최초 표시를 구성합니다. 상세 진행 조회는 CanQuery일 때 사용하며, 화면 재표시 때 현재 값을 다시 읽습니다. OnReleasing과 UI 자체 종료에서 로비/조회 객체의 구독을 해제합니다.

| 표시/요청 | 로비 API |
| --- | --- |
| 준비 상태 | State, CanQuery, CanProcessRequest, OnInitialized, OnStateChanged |
| 스테이지/난이도/기록 | Stages, SelectedStage, SelectedDifficulty, TrySelectStage, TrySelectDifficulty, TryGetBestStars, TryGetBestScore, OnSelectionChanged |
| 등장 적 | TryGetStageEnemies |
| 상품/무기 | ShopProducts, GetProductPurchaseState, TryPurchaseProduct, TrySelectWeapon, SelectedWeapon, 보유 조회와 OnEquipmentChanged |
| 강화 | Upgrades, GetUpgradeState, TryUpgrade, 대상별 미리보기와 OnUpgradeChanged |
| 미션 | Missions, TryGetMissionState, ExplorationLevel, OnMissionChanged, OnMissionRewardsCommitted |
| 도감 | Collections, TryMarkCollectionSeen, OnCollectionChanged |
| 잔액/수량 | Currency, Inventory의 현재 값과 변경 이벤트 |

요청은 Ready·활성·미션 비처리 상태에서 허용합니다. UI가 재화·수량·보상을 직접 변경하거나 화면을 열 때 초기화하지 않습니다. 보상 표시에는 저장 성공 알림을 사용하며 로비 상점에는 적 간략 정보를 표시하지 않습니다. 도감 확인 요청은 화면을 열 때 확보한 조회 객체와 신규 ID를 정상 닫기에서 전달합니다.

## 입장 준비와 이동 연결

기본 난이도는 보통이며, 보통이 설정에 없으면 첫 등록 난이도를 선택합니다. 맵을 바꿀 때 기존 난이도가 새 맵의 목록에 있으면 유지하고, 없으면 기본 난이도를 적용합니다. 실제 맵 준비 범위는 [담당별 연계](../LJ_INTEGRATION_GUIDE.md#로비-담당별-후속-연결)를 참조합니다.

TryPrepareEntry는 선택 무기 확인 → 미저장 미션 저장 → StageEntrySelection 구성 → Suspended·상태 알림 순서입니다. 결과는 Success / Unavailable / WeaponNotSelected / SaveFailed이며 실패 시 선택 확정·요청 중단 단계로 넘어가지 않습니다.

선택 준비 이후 로드·맵 준비·도전 시작과 복귀·재도전은 [공통 플레이 흐름](PLAY_FLOW.md#플레이-준비와-입장)을 따릅니다. 입장 요청 성공은 이동 접수이며 도전 시작 완료가 아닙니다.

## 최고 점수와 등장 적

TryGetBestScore의 반환값은 조회 성공 여부이고 hasScore는 점수 기록 여부입니다. 점수 미기록과 정상 0점을 구분하며 맵·난이도별 클리어 최고 점수를 조회합니다. KMB의 Stage 1·2 점수 확정·전달과 Stage 3 전달 기반이 있으며 정식 로비·선택 맵 흐름의 통합은 후속입니다. [실제 연결 상태](../LJ_INTEGRATION_GUIDE.md#점수와-종료-순서)와 점수별 보상은 구분합니다.

LobbyEnemySource는 맵·난이도별 실제 구성에서 원본 EnemyData 목록을 제공합니다. 조회 실패와 정상 빈 목록을 구분하며 로비용 목록을 수동 복제하지 않습니다. LobbyEnemyQuery가 중복 제거·도감 매핑·공개 범위를 적용하고 조회로 해금·신규 확인·보상·저장을 변경하지 않습니다.

표시 옵션은 클리어 전 로비 등장 목록에만 적용합니다. HideLocked는 이름을 ???와 빈 이미지로 표시하고 RevealLocked는 이름·이미지를 공개하며 항목 자체를 제거하지 않습니다. Unset이거나 제공자가 없으면 조회를 연결하지 않습니다. 실제 제공자·화면 연결은 남아 있습니다. 도감은 클리어 시 해당 구성의 미해금 적을 일괄 해금하며 미해금 상세·스토리 비공개 규칙은 유지합니다.

## 스테이지 CSV

UTF-8이며 한 행은 한 스테이지입니다. 고정 ID는 이름·씬 경로와 구분합니다.

| 필수 컬럼 | 값 |
| --- | --- |
| MapId | 중복 없는 맵 ID |
| DisplayName / Description | 표시 이름 / 설명, 빈 값 불가 |
| Difficulties | Easy/Normal/Hard 이름을 `|`로 구분, 중복 불가 |

1. `Create > Stage > Selection Data`로 저장한 설정을 만듭니다.
2. `Tools > Stage Selection CSV Importer`에서 CSV와 설정을 선택하고 검사·미리보기를 확인합니다.
3. 적용 시 입력·설정을 다시 검사하고 이전 미리보기와 같을 때만 Undo 기록 → 목록 반영 → 선택한 설정 저장을 수행합니다. 바뀌었으면 다시 검사해야 합니다.

CSV 순서로 반영하며 같은 MapId의 이미지를 보존합니다. CSV에서 빠진 기존 항목은 뒤에 기존 순서로 유지하며 삭제하지 않습니다. 이미지는 Inspector에서 연결합니다. 따옴표·쉼표·여러 줄 셀을 지원하고 구문/헤더/최종 목록 오류는 적용을 막습니다.

선택한 설정의 미저장 변경도 함께 저장합니다. 저장 실패 시 메모리 반영이 남을 수 있으며 자동 롤백하지 않습니다. 입력 변경·창 종료·플레이 전환 후에는 다시 검사하며 플레이 모드에서는 검사·적용하지 않습니다.

