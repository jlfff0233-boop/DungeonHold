# 현재 구현 기반 UI 연결 계획 — 2026-10-17

> 2026-10-20 사용자 확정: HUD 통합은 UI 연결 중 가장 마지막에 진행한다. 아래 HUD 작업은 기존 테스트 씬에서의 개별 연결·검증 기준으로 읽으며, 실제 플레이 씬으로의 통합과 전체 흐름 검증은 나머지 UI 연결 이후 진행한다. 최신 작업 순서는 [남은 UI 연결 계획](UI_REMAINING_CONNECTION_PLAN_2026-10-19.md)을 따른다. 아래 7일 배분은 전체 UI 연결 및 최종 HUD 통합의 완료 일정이 아니다.

## 범위와 확인 기준

사용자가 보고한 플레이어·동료·적 전투, 사망/부활, 상태이상, 재화/콤보, 스킬/궁극기/활, 설치 시스템의 진행을 기준으로 UI 연결 순서를 정리한다. 이번 요청은 연결 가능 범위 확인과 계획이며 코드·씬·프리팹 연결을 실행한 기록이 아니다.

- 실제 게임 데이터/버튼 연결을 우선한다. 튜토리얼/도움말의 PNG·Arrow 최종 정렬과 최종 미술 검수는 보류한다. 기존 UI를 재제작하지 않는다.
- 일정은 기존 전체 6주 안에 배치할 다음 연결 작업 7일·56시간의 예상 예산이다. 전체 프로젝트가 7일에 끝난다는 뜻이나 별도 7일 연장이 아니다.
- 현재 작업 폴더의 코드·프리팹·씬·문서만 정적으로 확인했다. Unity 실행/Play/빌드, 최신 팀 작업본의 동작 검증은 수행하지 않았다.
- ‘바로 착수’는 제공 데이터/API가 확인되어 연결 코드를 만들 수 있다는 의미다. 게임과 UI의 연결 완료를 뜻하지 않는다.

## 사용자 보고와 로컬 파일의 차이

현재 로컬에는 Health/Energy/기본 SkillCaster/부활/Currency/상태이상/Wave 공개 API가 있다. 반면 사용자 보고의 콤보·처치 보상, 궁극기·활·원거리 공통 기반·Animator Handler·Hero Editor, EnemyData/TrapData, 8m/장애물/준비 시간 설치 확장 일부는 현재 파일에서 확인하지 못했다. COMBO_SYSTEM 문서도 아직 설계 상태다.

보고된 기능을 미구현으로 판정하지 않는다. 해당 기능을 연결하기 전에 최신 팀 작업본과 실제 공개 조회/이벤트/실행 결과를 확인한다. 이 확인을 기다리는 동안 HP/에너지/재화/웨이브 표시부터 진행할 수 있다. UI 담당이 다른 담당자의 공통 API를 임의로 확장하지 않는다.

## 현재 UI 연결 상태

- `Presentation/UI/PlayerHudView.cs`, `StageHudView.cs`, `WorldHealthView.cs`는 존재한다.
- 이 세 스크립트의 GUID는 현재 저장된 씬/프리팹에서 참조되지 않으며, 게임 상태를 구독하여 View를 호출하는 코드도 확인되지 않았다.
- 따라서 기존 UI 오브젝트에 View를 붙이고, UI 필드를 지정하며, 게임 데이터와 View를 이어 주는 연결 컴포넌트가 필요하다. C# 이벤트는 Button OnClick에 오브젝트를 드래그하는 작업만으로 연결되지 않는다.

## 바로 연결 작업을 시작할 수 있는 UI

| UI | 현재 근거 | 필요한 연결 작업 |
| --- | --- | --- |
| 플레이어 HP | Health.CurrentHp/MaxHp/OnHpChanged → PlayerHudView.SetHealth | 초기 HP 조회, 피해/회복/부활 변경 반영 |
| 플레이어 에너지 | Energy.CurrentEnergy/MaxEnergy/OnEnergyChanged → SetEnergy | 초기 표시, 획득/소비 반영 |
| 기본 스킬 슬롯 | SkillCaster.CanUse/IsCasting/RemainingCooldown/EnergyCost/시작·종료 이벤트 | 기존 SkillSlot의 쿨타임·사용 가능 표시용 UI 코드 추가 |
| 사망·자동 부활 안내 | Hero.IsWaitingRevive/RemainingReviveTime/UsedReviveCount, Health.OnRevived | 사망 안내/실제 부활 대기 카운트다운/복구. 필요 최소 안내 UI 추가 |
| 적·동료 HP | 각 대상 Health + WorldHealthView | 대상 바인딩, 동료 행 배정, 적 체력바 위치 추적/카메라 방향/제거 정리 |
| 적 상태이상 표시 | StatusEffectController.OnEffectChanged/HasEffect/개별 상태 조회 | 화상·감속·기절 아이콘 표시용 View 추가. 실제 콘텐츠 적용 대상은 적/보스 기준 |
| 스테이지 재화 | Currency.GetBalance(Stage)/OnBalanceChanged → StageHudView.SetCurrency | StageStatus/Currency/CurrencyText에 최초 잔액과 Stage 변경만 반영 |
| 영구 재화 잔액 | Currency.GetBalance(Permanent)/OnBalanceChanged | SpellbookScreen·UpgradeDetailScreen 등의 기존 CurrencyText에 표시 |
| 준비 시간·현재 웨이브 | WaveController.IsPreparing/RemainingPreparationTime/CurrentWaveNumber, 준비·시작 이벤트 | WaveText/Timer/준비 안내 표시 전환과 시간 갱신 |
| 현재 출현 적·통과 적 수 | WaveController.ActiveEnemyCount/PassedEnemyCount | 현재 조회값 표시. 이름·목표값의 의미를 먼저 맞춤 |
| 웨이브 시작 요청 | WaveController.TryStartWave()의 bool 결과 | 기존 입력 담당 경로에서 1회 요청, 성공한 뒤 준비 안내 전환 |
| 설치 가능/불가의 기본 표시 | BuildPreviewController.HasCell/IsAreaAvailable/RotationDegrees | PlacementGuideRoot의 기본 상태·금지 표시. 상세 실패 사유는 최신 API 확인 필요 |

## 최신 API 또는 추가 원본 데이터 확인 후 연결할 UI

| UI | 추가 확인 사항 |
| --- | --- |
| 궁극기 슬롯/활 전투 표시 | 실제 궁극기 사용 가능/소비/쿨타임/시전 상태, 선택 무기 데이터. 로컬 기본 SkillCaster를 궁극기라고 가정하지 않음 |
| 콤보·보상 팝업 | 현재 콤보/잔여 유지시간/단계 도달/실제 지급 성공 알림. 이벤트 이름은 최신 코드 확인 후 확정 |
| 설치 거리·차단 사유 | 8m 초과, 영역 밖, 점유, 장애물/캐릭터 겹침, 준비 종료 등의 판정 결과/우선순위 |
| 설치물 슬롯/비용 부족/철거 환불 | 선택 설치물 데이터와 아이콘/비용, 선택 변경, 실제 소비·환불 결과. 프리뷰 성공과 설치 성공 구분 |
| 점수·결과·통계 | 점수 원본, 마지막 보상 처리 완료, 성공/실패 판정, 확정 결과 스냅샷 |
| 미니맵 | 지도 이미지/좌표 변환/플레이어·적·목표 등록과 해제. NavMesh 이동 구현만으로 지도 UI가 완성되지는 않음 |
| 인벤토리·강화·해금·고용 버튼 | 해당 기능의 요청/성공/거절·보유 정보 API. 재화 모듈만으로 기능 완료 처리하지 않음 |
| 동료 에너지 바 | 로컬 Companion에 Energy 제공 근거 없음. 실제 사용하는 값/필요성 확인 |
| Pause/메뉴·재시도·복귀 | 게임 입력 차단/커서/시간/설치 취소·진행 수명 계약과 실제 스테이지 진입·정리 경로 |

## 연결 시 지킬 데이터 의미

1. `ActiveEnemyCount`는 현재 생성되어 살아 있는 등록 적 수다. 스폰 예정 적을 포함한 ‘전체 남은 적’으로 표시하지 않는다.
2. `PassedEnemyCount`는 통과 누적이다. 균열 잔여점수/통과 허용량/실패 한계값은 실제 스테이지 설정에서 받아야 한다. 샘플 20을 판정값으로 사용하지 않는다.
3. WaveController의 총 웨이브 값은 현재 private다. `SetWave(current,total)`의 total은 실제 설정/담당 공개 API가 확보된 뒤 표시한다. 미확보 상태에는 현재 번호만 표시하는 대안을 사용한다.
4. `OnAllWavesCompleted`는 전체 웨이브 종료다. 이를 곧바로 스테이지 성공으로 판정하지 않는다.
5. 에너지와 스테이지/영구 재화, 게임 점수는 각각 별개다. 재화 잔액 증감만으로 처치 보상·콤보 보상·획득 누적량을 추정하지 않는다.
6. UI는 보상 지급·피해·소비를 별도로 재실행하지 않는다. 요청의 실제 성공 결과를 표시한다. 재화 화면을 열 때 Currency.Init/BeginStage를 호출하지 않는다.
7. 부활 카운트다운은 HP가 0이 된 순간이 아닌 Hero의 실제 대기 상태를 따른다. 마지막 사망/부활 소진 후 실패 전환은 상위 판정과 연결한다.
8. 스킬 쿨타임/준비 시간/부활 시간은 현재 값 갱신이 필요하다. 시작·종료 이벤트만 구독하고 숫자가 멈춘 상태로 두지 않는다.
9. 상태이상 OnEffectChanged는 매 프레임 시간 알림이 아니다. 감속은 SO별 상태 조회 계약을 확인하고 대표 잔여시간을 임의로 합산하지 않는다.
10. 최종 보상 처리 → 필요한 결과 스냅샷 → EndStage 순서로 맞춘다. EndStage 후 Stage 잔액은 초기화되므로 종료 잔액/누적 획득량을 나중에 조회하여 복원하지 않는다.

## 첫 연결의 Inspector 대상

기존 Canvas_HUD 씬 인스턴스에 PlayerHudView를 붙인 뒤 UI 참조를 지정한다. 게임 객체 참조는 실제 실행 씬의 연결 컴포넌트에서 지정하며, Project의 공용 프리팹에 씬 객체를 저장하려 하지 않는다.

| PlayerHudView 필드 | 기존 UI |
| --- | --- |
| Character Name Text | PlayerStatus/NameText |
| Portrait Image | PlayerStatus/PortraitFrame/Portrait |
| Health Fill | PlayerStatus/HealthFrame/Health |
| Energy Fill | PlayerStatus/EnergyFrame/Energy |

Health Text/Energy Text에 대응할 숫자 TMP가 없으면 None으로 두고 게이지부터 연결한다. HP/에너지 Image는 이미 Filled/Horizontal 설정이 있으므로 새 바를 만들지 않는다.

StageHudView는 다음 기존 참조를 사용한다.

| 필드 | 기존 UI |
| --- | --- |
| Currency Text | StageStatus/Currency/CurrencyText |
| Score Text | PlayerStatus/ScoreText — 점수 원본 확보 후 사용 |
| Time Text | StageStatus/Wave/Timer |
| Wave Text | StageStatus/Wave/WaveText |
| Passed Enemies Text | StageStatus/Objective/PassedEnemy |
| Passed Enemies Root | StageStatus/Objective |

실제 게임 초기화 성공 확인 → 바인딩/현재 값 반영 → 이벤트로 갱신의 흐름을 사용한다. 비활성화/대상 교체/씬 종료 시 이전 대상 구독을 해제하고, 다시 열 때 현재 값을 조회한다. HUD를 숨기는 일 때문에 전투 모듈을 비활성화하지 않는다.

## 다음 연결 작업 7일·56시간 예상 배분

| 일차 | 작업·8시간 배분 | 완료 기준 |
| --- | --- | --- |
| 1일 | 최신 코드/API·실행 씬 확인 2h → PlayerHudView/참조·HP/에너지 연결 4h → 확인 2h | 피격/회복/소비/충전/재표시 때 실제 게이지와 현재 값 일치 |
| 2일 | Stage/Permanent 재화 표시 3h → 웨이브·준비 시간·통과/출현 적 수 3h → 확인 2h | 두 재화 분리, 준비/전투 전환·카운트다운·적 사망/통과 반영 |
| 3일 | 기본 스킬 표시 3h → 사망/부활 안내 3h → 확인 2h | 사용 불가/시전/쿨타임, 실제 부활 대기와 HP 복구 일치 |
| 4일 | 동료 행·적 HP/생성·제거 수명 4h → 화상/감속/기절 표시 2h → 확인 2h | 대상별 HP 정확, 사망/제거 뒤 UI 잔존 없음, 실제 적용/해제 반영 |
| 5일 | 최신 설치 API·입력 대조 2h → 선택/설치 가능·사유/철거 안내 4h → 확인 2h | 준비 종료·8m·겹침·회전·설치 직전 거절의 실제 판정과 UI 일치 |
| 6일 | 최신 콤보/처치 보상 표시 3h → 궁극기·무기 표시 3h → 확인 2h | 실제 콤보/지급·시전 결과 1회에 표시 1회, 취소·실패 오표시 없음 |
| 7일 | 종료 결과 스냅샷·기존 결과 UI 일부 연결 3h → 통합 회귀 확인 3h → 문서·미연결 목록 2h | 실제 제공되는 결과만 표시, 재도전/화면 재열기에서 중복 지급·구독·이전 값 없음 |

5~7일차는 최신 API 제공을 전제로 한 조건부 예산이다. 제공이 늦으면 UI 담당은 앞선 HUD의 예외/수명 처리와 가능한 항목을 마감하고, 부족 API와 보류 UI를 남긴다. 미확인 게임 기능을 새로 구현하거나 임시 성공값으로 대체하지 않는다. API 변경량·새 데이터/상태 UI 작업량에 따라 예산을 조정한다.

Pause/메뉴 전체 전환, 구매·강화·해금·고용·저장, 전체 미션/결과 판정의 완성은 위 56시간의 완료 보장 범위가 아니다. 각각의 도메인 기능 준비 뒤 별도 연결 작업으로 잡는다. FX/애니메이션은 이번 데이터 연결 일정에 포함하지 않으며 기존 UI_VFX_WORK_PLAN의 순서를 따른다.

## 예정 확인 항목

- 실제 적용 피해/회복 및 에너지 소모/획득 후 값과 게이지 일치.
- HUD OFF→ON, 대상 교체, 씬 재진입에서 최초 값 표시와 중복 구독 없음.
- 준비 자동 종료/조기 시작 요청의 성공·거절 및 연속 입력에서 웨이브 1회 시작.
- 적 스폰 도중/스폰 완료 후 사망·통과에서 출현 적 수와 전체 남은 적 의미 혼동 없음.
- 설치 실패/거리 초과/회전/준비 종료에서 판정과 표시 일치, 실패 시 소비·완료 표시 없음.
- 화상/감속/기절의 재적용·만료·사망·면역 결과와 표시 일치.
- 사망 연출→부활 대기→부활 완료→최종 소진에 맞춘 UI 전환.
- 콤보/처치/마지막 보상의 지급 성공·실패·이전 도전 알림, 결과 화면 재열기에서 중복 지급 없음.
- 최종 UI 실행 확인은 연결 구현 후 수행할 예정이며 현재 확인 완료가 아니다.

## 근거 파일

- [플레이어 HUD View](../Assets/02.%20Scripts/Presentation/UI/PlayerHudView.cs), [스테이지 HUD View](../Assets/02.%20Scripts/Presentation/UI/StageHudView.cs), [월드 HP View](../Assets/02.%20Scripts/Presentation/UI/WorldHealthView.cs)
- [HUD 프리팹](../Assets/04.%20Prefabs/Canvas_HUD.prefab), [월드 HP 프리팹](../Assets/04.%20Prefabs/Canvas_Health.prefab)
- [연계 가이드](LJ_INTEGRATION_GUIDE.md), [재화](Systems/CURRENCY_SYSTEM.md), [콤보 설계 문서](Systems/COMBO_SYSTEM.md), [효과](Systems/COMBAT_EFFECT_SYSTEM.md)
- [웨이브](../Assets/02.%20Scripts/KMB/Wave/WaveController.cs), [설치 Preview](../Assets/02.%20Scripts/KMB/Installation/BuildPreviewController.cs), [설치 실행](../Assets/02.%20Scripts/KMB/Installation/BuildPlacementController.cs)
- [기존 UI/FX 후속 순서](UI_VFX_WORK_PLAN.md), [도움말 보류 기준](UI_HELPBOOK_PLAN.md)
