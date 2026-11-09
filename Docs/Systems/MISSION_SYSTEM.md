# MISSION_SYSTEM

LJ 담당 탐험 미션의 현재 코드와 사용 계약입니다. CSV 임포터·초기 미션 데이터·맵 등록·PlayerTest Inspector 연결까지 구성했습니다. **기본형 처치 미션의 달성·최초 보상·저장 복원은 사용자 확인했으며, 다른 조건과 KMB/LHJ 실제 연결 검증은 후속입니다.**

## 확정 규칙

- 미션·도감 기반과 저장 연결을 구현했으며 저장 개편은 완료했습니다. 미션의 실제 게임·UI 연계와 추가 검증은 후속입니다. 저장 완료 범위는 [TODO](../TODO.md#단일-로컬-저장)를 참조합니다.
- 누적 처치·완료·보상은 난이도별로 분리합니다. 같은 미션도 다른 난이도에서 최초 완료하면 각각 탐험 레벨 +1과 개별 보상을 즉시 지급합니다.
- 탐험 레벨 구간 보상과 수동 수령 버튼은 없습니다. 상위 난이도의 하위 난이도 자동 완료는 구현하지 않습니다.
- 단계형 처치는 하나의 처치 집계을 공유합니다. 잠긴 단계도 누적하며 초과분을 보존하고 여러 단계 동시 완료 시 각 단계의 최초 보상을 모두 지급합니다.
- 준비 단계의 행동은 집계에서 제외합니다. 플레이어·동료·설치물·화상·맵 기믹 처치와 보스 소환 적을 인정합니다.
- 실패·포기·재시작에도 누적 처치와 확정된 완료·보상을 보존합니다. 부활·동료 사망·목표 회복으로 이미 발생한 이력을 지우지 않습니다.
- 스테이지 3은 B 기획을 기준으로 **통과 한도 도달 OR 목표 HP 0이면 실패**로 확정합니다. 별 평가와 완전 방어는 별개 판정입니다.

### 초기 미션

초기 미션 SO는 10개입니다. 보상은 조정 전 기획 초기값이며 고정 ID는 `종류_번호_설명`의 0번 시작 규칙을 사용합니다. 기존 ID는 임의로 재번호화하지 않습니다.

| 고정 ID | 표시 이름 | 영구 골드 초기값 |
| --- | --- | --- |
| Mission_0_Stage1PerfectDefense | 성채 진입로에서 적을 한 명도 통과시키지 않고 클리어 | 100 |
| Mission_1_Stage2PerfectDefense | 분기형 지하 회랑에서 방어 목표가 피해를 받지 않고 클리어 | 100 |
| Mission_2_Stage3PerfectDefense | 최후의 요새에서 적 통과와 방어 목표 피해 없이 클리어 | 100 |
| Mission_3_ClearWithoutItem | 아이템을 사용하지 않고 클리어 | 100 |
| Mission_4_ClearWithoutCompanion | 동료를 전투에 참여시키지 않고 클리어 | 150 |
| Mission_5_BasicKill30 | 기본형 적 누적 30마리 처치 | 100 |
| Mission_6_BasicKill100 | 기본형 적 누적 100마리 처치 | 200 |
| Mission_7_TankKill20 | 탱커형 적 누적 20마리 처치 | 150 |
| Mission_8_ClearWithoutDeath | 한 번도 사망하지 않고 클리어 | 100 |
| Mission_9_HardClear | 어려움 난이도로 클리어 | 200 |

완전 방어는 각각 `stage_1`·`stage_2`·`stage_3`, 나머지는 맵 공통입니다. 어려움 클리어만 Hard에 적용하고 나머지는 Easy·Normal·Hard에 적용합니다. 기본형 100마리는 30마리를 선행 조건으로 둡니다.

처치 집계는 `MissionKillCount_0_Basic` → Enemy_Basic_Data, `MissionKillCount_1_Tank` → Enemy_Tank_Data이며 둘 다 맵 공통입니다. 기본형 30·100마리는 같은 집계를 공유합니다.

## CSV 임포트

- 실사용 원본: `Assets/03. Configs/Mission/Mission.csv`·`MissionKillCount.csv`. `_Test.csv` 두 파일은 테스트용으로 유지합니다.
- `Create > Mission > Data Map`으로 저장된 맵을 준비하고 `Tools > Mission CSV Importer`에서 CSV·맵·출력 폴더를 선택합니다. 미션 CSV는 필수, 처치 집계 CSV는 선택입니다.
- 검사·변경 미리보기 후 적용하면 SO 생성/갱신과 맵 등록을 수행합니다. 같은 ID·타입은 기존 참조를 유지하며 CSV에 없는 항목은 삭제하지 않습니다.
- 미션 CSV 헤더: `MissionId,Type,DisplayName,Difficulties,PrerequisiteId,RewardAmount,MapId,Comparison,Count,KillCountId,TargetCount`.
- 집계 CSV 헤더: `KillCountId,EnemyAssetPath,MapId`. 집계 CSV를 생략하면 맵·출력 폴더의 기존 집계를 사용합니다.
- UTF-8과 인용 필드를 지원하며 ID·타입·조건·선행 참조를 검사합니다. 기존 미션의 타입 변경과 집계의 대상 적/맵 변경은 차단하므로 새 ID를 사용합니다.
- 적용 직전에 입력과 에셋을 재검사합니다. 변경되면 미리보기를 갱신하며, 부분 실패는 표시된 경로를 확인한 뒤 재시도합니다. Play Mode에서는 임포트하지 않습니다.
- 임포터는 저장 파일을 변경하거나 미션을 자동 달성시키지 않습니다. 평가는 정상 처치·승리 사건에서 수행합니다.


## 데이터 구성

공통 부모에는 ID·이름·적용 난이도·선행 미션 ID·보상만 둡니다. 조건마다 필요한 필드는 해당 데이터가 보관하며 MissionType으로 모든 조건을 한 SO에 넣지 않습니다.

| 데이터 | 설정/조건 |
| --- | --- |
| MissionData | 공통 식별·적용 난이도·선행 조건·최초 영구 재화 보상 |
| ClearMissionData | 클리어 미션의 대상 맵. 빈 값은 맵 공통 |
| ItemUseMissionData | 플레이 중 아이템 사용 성공 횟수 |
| PlayerDeathMissionData | 플레이 중 플레이어 사망 횟수 |
| CompanionParticipationMissionData | 플레이에 참여한 서로 다른 고용 번호 수 |
| MissionCountCondition | 횟수의 일치·이하·이상 비교와 기준 횟수 |
| StageClearMissionData | 적용 맵·난이도의 승리. 어려움 정복은 Hard만 설정 |
| PerfectDefenseMissionData | 대상 맵 필수, 현재 맵의 방어 규칙과 최종 방어 이력 평가 |
| KillMissionData | 처치 집계 설정 참조와 목표 수량 |
| MissionKillCountData | 처치 집계 ID·대상 EnemyData·맵 범위 |
| MissionDataMap | 등록 설정 검사·미션 ID 조회·선행 평가 순서 준비 |

아이템 사용·플레이어 사망·동료 참여 제한은 각각 횟수 조건을 '정확히 0'으로 구성합니다. 기본 비교 설정도 일치/0이며 '3회 이하 사용 후 클리어' 같은 조건은 SO 설정으로 표현합니다. 클리어형 표시 진행도는 0/1이고 조건의 기준 횟수를 진행도 분모로 쓰지 않습니다.

동료 참여는 고용 번호 기준입니다. 같은 번호의 재고용·웨이브 재진입은 추가 인원으로 세지 않고, 다른 고용 번호가 실제 플레이에 참여하면 추가합니다. 준비 중 사용·사망·고용만으로는 플레이 기록이 늘지 않습니다. 부활·동료 사망·준비 구간 전환으로 이미 기록한 횟수/참여를 지우지 않습니다.

완전 방어는 맵별 NoPass/NoDamage 조합을 사용합니다. 스테이지 1은 NoPass, 2는 NoDamage, 3은 두 항목 모두 요구합니다. 방어 기록의 유효 여부가 false이거나 규칙이 None이면 완전 방어를 완료하지 않습니다.

## 코드 구성

미션 설정/실행/전달 타입은 다음 폴더로 나누고 영구 저장 기록은 공통 Save 폴더에 둡니다. 실행 객체는 일반 C# 클래스이며 SO 외에 컴포넌트를 각각 붙이지 않습니다.

| 폴더 | 주요 코드와 책임 |
| --- | --- |
| Data | 위 조건 SO·데이터 맵·횟수 비교 설정 |
| Runtime | MissionController, MissionEvaluator, MissionProgress, MissionStageRecord, MissionKillCountProgress, MissionCompletionProgress |
| 공통 Save | MissionKillCountSaveData, MissionSaveData |
| Types | MissionEnums, MissionKey, MissionKillCountKey, MissionStageContext, MissionKillContext, MissionStageResult, MissionReward, MissionCommit, MissionState |

구조체·클래스는 타입별 파일로 분리했으며 미션 공통 enum은 MissionEnums에 모았습니다.

## 진행도와 식별 기준

| 기록 | 키 | 값 |
| --- | --- | --- |
| 처치 집계 | MissionKillCountKey: KillCountId + 난이도 | MissionKillCountProgress: 현재 처치 수·저장 처치 수 |
| 미션 완료 | MissionKey: MissionId + 난이도 | MissionCompletionProgress: 현재 완료/지급 기록·저장 완료/지급 기록 |
| 이번 도전 | 원래 도전 번호 + 맵 ID + 난이도 | 준비/플레이·사용/사망 횟수·참여 고용 번호·고정 결과 |

처치 집계 ID는 **같은 적/맵 범위의 처치 미션이 함께 참조하는 집계 식별자**입니다. 난이도는 키의 별도 항목입니다. 동일 적의 전체 맵 집계와 특정 맵 집계가 함께 적용되면 각각 한 번 증가하고, 같은 집계의 30/100마리 단계는 하나의 처치 수를 조회합니다. 목표에서 누적을 자르지 않으며 잠긴 단계도 누적합니다.

MissionProgress는 처치 집계와 완료용 딕셔너리 두 개를 관리합니다. 각 값 객체 안에 현재값과 저장값을 함께 두어 저장 실패 후에도 추가 사건을 받되, 저장된 완료·지급·레벨과 구분합니다.

원래 적 생명 번호는 처치 중복 방지용입니다. 같은 도전에서 이미 처리한 번호를 다시 전달하면 거절합니다. 적의 풀 재사용/부활은 생성 주체가 새 생명 번호를 부여해야 하며 미션 수신부가 재발급하지 않습니다. 도전 번호는 맵 ID가 아닙니다.

탐험 레벨은 난이도별 저장 완료 기록 수로 계산합니다. 현재 맵에서 사라진 지급 완료 기록과 집계를 자동 삭제하지 않습니다. 설정이 없는 미지급 완료 기록은 보상 계산이 불가능하므로 초기화에 실패합니다.

## 초기화와 도전 수명

### 진행 연결과 초기화

공통 진행 복원·모듈 연결 순서는 [저장 시스템의 초기화](PLAY_FLOW.md#진행-연결과-씬-초기화)를 따릅니다. 미션 진행 조회 객체는 최초 재화 알림 전에 준비하고, 사용·사망·참여 사건은 PlaySession이 연결합니다.

형식·중복은 SaveService, 설정과 선행 순서는 MissionDataMap, 저장 기록과 현재 설정의 호환은 MissionProgress가 검사합니다. 처리 내부에서 같은 설정 검사를 반복하지 않습니다. 읽기 실패를 신규 진행으로 덮어쓰지 않습니다.

미션/도감 데이터 맵은 PlayScene의 필수 참조입니다. 파일 준비는 SaveManager/SaveService, 모듈 복원·외부 진입점은 PlayScene, 사건 연결과 도전 조율은 PlaySession이 담당합니다. TestScene은 PlayScene을 상속한 직접 테스트 시작 경로이며 Init은 파일 재불러오기가 아닙니다.

### 새 도전과 구간

도전 개설·첫 전투·웨이브 구간 전환은 [공통 플레이 흐름](PLAY_FLOW.md#도전-시작과-전투-구간)을 따릅니다. 미션은 준비 중 사건을 제외하고 실제 플레이 구간에서 사용·사망·참여를 기록합니다. 같은 고용 번호의 재고용·웨이브 재진입은 추가 참여로 세지 않습니다.

사용 성공은 HeroItemUse.OnUseCommitted에서 효과 적용 전에, 사망은 Hero.OnDied에서 공격 취소 알림 전에 기록합니다. 동료 고용/재고용은 OnParticipationConfirmed(stageId, hireId)를 표시 알림 전에 전달합니다. 사건 수신부는 기록만 반영하며 도전 전환을 실행하지 않습니다.

## 처치·평가·저장 순서

PlayScene.RegisterMissionKill(MissionKillContext)는 원래 도전·생명 번호·EnemyData를 받습니다. 적 종류는 실제 EnemyData 참조로 비교하며 ActorType.Enemy로 기본형/탱커를 구분하지 않습니다. 처치 원인 인정은 연계부 책임이고 전달 타입에 별도 원인 필드는 없습니다.

```text
도전/플레이 구간/생명 중복 확인
→ 해당 처치 집계 현재값 증가
→ 선행 순서로 처치 미션 평가
→ 누적·완료·최초 지급·보상 잔액 후보 통합 저장
→ 진행 저장값·이번 판 최초 보상 확정
→ 런타임 영구 잔액 반영
→ 신규 보상 알림·상태 변경 알림
```

MissionEvaluator.Evaluate는 적용 범위와 시점이 정해진 목록을 한 번 순회합니다. 같은 평가에서 신규 완료된 선행 미션도 후행 조건에 반영합니다. 파일·재화·UI 변경은 하지 않습니다.

RegisterKill 결과는 Rejected / Accepted / AcceptedSavePending입니다. 저장 실패도 처치는 접수한 상태이므로 같은 번호를 다시 전달하지 않습니다. 확정 29 → 30 저장 실패 → 31 접수라면 다음 저장 후보는 31과 최초 미지급 보상입니다. TrySaveProgress는 사건을 다시 집계하지 않고 최신 후보만 저장합니다.

미션 저장 계약은 [SAVE_SYSTEM.md](SAVE_SYSTEM.md#미션-통합-저장), 지원 버전과 변환은 [저장 버전 정책](SAVE_SYSTEM.md#저장-버전-정책)을 참조합니다.

## 최종 종료와 재시도

결과 예약·마지막 사건과 점수 전달·단계별 재시도·전체 완료는 [공통 플레이 흐름](PLAY_FLOW.md#최종-종료와-완료-통지)을 따릅니다. 미션은 마지막 사건 전달 이후 기록을 고정하고 최종 평가·저장을 수행합니다. 패배·포기도 누적 처치를 저장하지만 클리어 조건은 평가하지 않습니다.

같은 고정 결과의 재시도는 최초 보상을 반복하지 않으며 상충 결과는 거절합니다. 미션 저장 실패 시 고정 결과·미저장 진행을 유지하고 전체 종료를 성공으로 알리지 않습니다. 원래 도전의 보상과 시작 진행은 결과 표시용으로 다음 도전 시작 전까지 보관합니다.

## 조회와 알림

로비는 LobbyController의 Missions·TryGetMissionState·ExplorationLevel과 미션 변경/보상 확정 이벤트를 사용합니다. 도전 사건·평가·최종 정산은 PlayScene/PlaySession 경로를 사용하며 기존 TestScene 호출은 상속으로 유지합니다. 로비의 미저장 처리와 해제 순서는 [로비 시스템](LOBBY_SYSTEM.md)을 참조합니다.

| API/상태 | 사용 |
| --- | --- |
| Missions / TryGetState(id, difficulty, out state) | 적용 난이도별 목록·현재/저장 진행·목표·잠금·완료·지급·미저장 조회 |
| ExplorationLevel | 저장 완료 기록 수 |
| OnChanged | 최초/변경 시 상태 재조회. 토스트 재생 용도가 아님 |
| OnRewardsCommitted(stageId, rewards, amount) | 저장 성공한 신규 보상 배치의 즉시 표시 |
| GetStageRewards(stageId) | 해당 도전에서 확정된 보상 목록 복사 |
| TryGetStartingProgress(stageId, missionId, out count) | 시작 저장 진행과 현재 저장 진행을 비교해 결과 증가분 계산 |
| StageContext / StageResult | 원래 도전 적용 정보와 최종 고정 결과 |
| HasPendingSave / IsFinalized / PlayScene.IsEndingStage | 미저장·최종 정산·전체 종료 상태 구분 |

현재 진행은 미저장 처치를 포함할 수 있지만 완료·지급·잠금·레벨은 저장값 기준입니다. 알림 시점에는 내부 상태를 먼저 확정합니다. 결과/보상 목록은 다음 도전 시작 전까지 보존되므로 화면은 원래 도전 번호로 조회해야 합니다.

접수 결과·종료 상태·실패 위치 조회는 [공통 결과 조회](PLAY_FLOW.md#결과와-실패-상태-조회)를 사용합니다.

## 비활성화와 연결 상태

미션 사건 구독은 PlaySession 수명을 따릅니다. 씬 연결부 비활성화와 세션 해제의 차이는 [공통 수명 계약](PLAY_FLOW.md#비활성화재초기화파괴)을 참조합니다.

## 디버그 검증

1. 도전 시작 후 `전투 시작`으로 플레이 구간에 진입합니다. 동료가 없어도 필요합니다. 조회 난이도는 실제 도전 난이도에 맞춥니다.
2. 대상 EnemyData와 `추가 처치 수`를 입력하고 `미션 처치 일괄 접수`를 누릅니다. 목표 총량이 아닌 추가량이며 실제 처치 접수·평가·저장 경로를 사용합니다.
3. 생명 번호는 기존 발급 경로를 사용하고 수동 접수했던 번호는 건너뜁니다. `미션 처치 접수 (생명 번호 유지)`는 같은 번호의 중복 거절 확인용입니다. 저장 대기 시 중단하므로 저장 재시도 후 남은 수를 입력합니다.
4. 승리는 확정 점수를 입력하고 마지막 사건 전달 완료로 종료합니다. 패배·포기는 점수 없이 완료합니다. 빠른 정산·일괄 검증 절차는 [종료 보상 검증](Test/CURRENCY_TEST.md#종료-보상)을 참조합니다.

사용자 확인: 기본형 30·100마리 순차 달성, 초과 누적·동일 생명 중복 거절·일괄 접수, 최초 보상/레벨 증가와 재지급 방지, 정상 종료·재초기화 후 진행/완료/지급/잔액/레벨 복원. 다른 조건·예외·실제 전투 연결은 [TODO.md](../TODO.md#탐험-미션--구현-상태와-후속)에서 추적합니다.

## 다음 작업과 담당별 연계

미션의 기존 완료·보상·복원 확인 이력은 [DONE.md](../DONE.md), 추가 조건과 실제 연계 검증은 [TODO.md](../TODO.md#탐험-미션--구현-상태와-후속)에서 관리합니다.

- KMB: 공용 처치·구간, Stage 1·2 결과·별·도감 대상·확정 점수와 Stage 2 방어 피해 이력이 연결됐습니다. Stage 3도 OR 실패 판정·방어 피해 이력·별·도감 대상·확정 점수 전달 기반이 있습니다. 제한시간·부활 소진 실패 코드는 Stage 1·2·3에 있으며 완전 방어·마지막 사건·종료 재시도의 통합 검증과 Stage 3 정식 적용은 후속입니다. 상세는 [연계 가이드](../LJ_INTEGRATION_GUIDE.md#미션-집계-연동-요구)를 따릅니다.
- LHJ: 최초 조회/재조회·저장 성공 보상 토스트·레벨·원래 도전 결과 증가분·미저장/종료 재시도 표시. UI가 판정·직접 지급하지 않고 전체 종료 성공 후 전환합니다.
- 상세 진입점과 담당별 남은 작업은 [LJ_INTEGRATION_GUIDE.md](../LJ_INTEGRATION_GUIDE.md#미션-집계-연동-요구)를 참조합니다.

미션은 난이도별로 집계하며 최초 완료마다 탐험 레벨이 1 증가합니다. 완전 방어는 스테이지별로 판정합니다.
