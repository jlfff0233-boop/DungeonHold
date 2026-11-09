# COMPANION_SYSTEM

## 현재 범위와 운용 규칙

고용·재고용, 준비/전투 중 배치·재배치, 추종/방어/후퇴 AI와 3종 기본 공격을 구현했습니다. 기본 동작은 사용자 확인을 마쳤으며, 확인 범위는 [COMPANION_TEST.md](Test/COMPANION_TEST.md), 남은 연계와 작업은 [COMPANION_TODO.md](Todo/COMPANION_TODO.md)에서 관리합니다.

- 스테이지 재화로 최대 2개의 고용 기록을 보유하며 같은 타입을 중복 고용할 수 있습니다. 사망·대기 기록도 한도에 포함하고 재고용 시 번호와 순번을 유지합니다.
- 준비 중 고용은 비용과 기록을 확정합니다. 포인트에 배치하면 방어형으로 생성하고 미배치 동료는 첫 전투 시작 때 추종형으로 생성합니다. 전투 중 고용·재고용은 추종형으로 생성합니다.
- Z/X는 타입이 아닌 첫 번째/두 번째 고용 기록을 배치합니다. 준비·전투 중 재배치할 수 있으며 HP·공격 쿨타임·강화 결과를 유지합니다.
- 사망 즉시 타입 공통 대기를 시작하고, 대기 중 같은 타입이 추가 사망하면 전체 대기로 갱신합니다. 해당 타입은 여유 인원이 있어도 대기 중 신규 고용할 수 없습니다.
- 사망 객체와 포인트는 연출 종료 때 해제합니다. 연출과 타입 대기가 모두 끝난 기록에 최초 고용과 같은 비용을 지불해 재고용합니다. 같은 타입의 다른 생존 동료는 유지합니다.

## 책임과 데이터

| 모듈 | 책임 |
| --- | --- |
| CompanionHireController / CompanionHireEntry | 고용 비용·기록·타입 대기·생성/제거·전투 시작 조율 / 개체 상태와 점유 보관 |
| CompanionQuickSlotData / QuickSlotContext / CompanionHireInput | 타입별 고용 요청 / 공용 슬롯 실행 / 고용 순번 배치 입력 |
| CompanionPlacementController / CompanionPlacementPoint | 플레이어 영역에 따른 포인트 선택 / 배치 영역·생성 기준점 |
| CompanionSpawnPositionFinder | 생성·배치 위치의 NavMesh/장애물 검사와 후보 탐색 |
| Companion | 개체 초기화, 대상·활동 중심과 접근/후퇴/복귀 판단, 생명주기 조율 |
| CompanionMover / CompanionTargetDetector | 실제 이동·회전·경로 처리 / 공격 우선 대상과 가까운 위협 탐색 |
| CompanionCombatAnimationBinding | 공격 시작/종료와 애니메이션 적중/완료 신호 연결 |
| CompanionHireDataMap / CompanionHireData / CompanionData | 타입별 고용 설정 조회 / 프리팹·비용·대기 / 전투·이동·탐색 설정 |
| CombatTarget | 같은 루트 DamageReceiver의 진영·생존 상태 제공 |

HP는 Health, 피격·진영은 DamageReceiver, 공격 회차·쿨타임은 Attacker가 관리합니다. 동료 프리팹에는 내장 무기와 AnimatorHandler를 연결하고 Companion의 Source에 실제 피해 발생원을 지정합니다.

## 초기화와 고용 수명

PlayScene이 재화/보상·고용·배치를 초기화하고 PlaySession이 도전 시작을 조율합니다. TestScene은 이 기능을 상속해 직접 테스트를 시작합니다. 히어로 준비와 재화 도전 발급 후 같은 번호로 고용 수명을 연결합니다.

```text
CompanionHireController.Init(currency, hero, hireDataMap, canRequest)
→ CompanionPlacementController.Init(hireController, hero)
→ BeginStage(stageId, companionUpgrades, priorityPosition)
→ SetHireEnabled(true)
```

입장 시 확정한 강화 결과와 선택적 우선순위 위치를 고용 관리자에서 생성·재고용 동료에 전달합니다. HP·피해는 기본값 × (1 + 증가율)이며 원본 SO는 유지합니다. 강화 계약은 [UPGRADE_SYSTEM.md](UPGRADE_SYSTEM.md)를 참조합니다.

PlayScene은 고용 관리자에 사운드·VFX 서비스를 전달하고, 생성·재고용은 Companion.SetSound·SetVfx → PrepareHire 순서로 연결합니다. 동료는 실행자에도 서비스를 전달합니다. 유지 VFX는 PrepareHire만으로 시작하지 않고 ConfirmHire에서 현재 상태를 동기화합니다. 준비 실패는 기존 제거 경로로 정리합니다.

고용 VFX는 최초 배치·첫 전투 자동 생성·전투 중 고용/재고용 성공 후 요청합니다. 기록만 있는 고용·준비 실패·재배치에서는 재생하지 않습니다. 경로별 출력 검증은 [동료 TODO](Todo/COMPANION_TODO.md), 효과 수명은 [VFX 시스템](VFX_SYSTEM.md)을 참조합니다.

사망은 로컬 사운드와 캐릭터 유지 VFX를 정리한 뒤 위치 사망음·단발 VFX를 요청합니다. 위치 출력은 동료 제거와 독립적이고 비활성화·StopStageActions에서는 캐릭터 유지 효과를 정리합니다. 일반 사망은 이미 승인된 투사체 실행을 유지하며 도전 종료·비활성화는 무효화합니다. 상세는 [사운드](SOUND_SYSTEM.md)·[VFX](VFX_SYSTEM.md) 시스템을 참조합니다.

고용 상태는 `Hired → Prepared → Alive → Dying → Waiting/Ready`입니다. Hired는 비용을 낸 미생성 기록, Prepared는 초기화됐지만 행동을 보류한 객체입니다.

- 생성은 PrepareHire로 이동·공격·피격을 보류한 채 준비합니다. 첫 전투의 전체 준비가 성공하면 기록을 Alive로 확정하고 ConfirmHire로 행동을 허용합니다. 부분 실패 시 이번 자동 생성분만 Hired로 복구하고 기존 배치는 유지합니다.
- 전투 중 고용·재고용은 위치 확보·초기화 성공 → 비용 소비 → 기록 연결 → 행동 허용 순서입니다. 준비 실패에는 비용을 소비하지 않습니다.
- 재배치는 위치 확보 → 객체 이동·방어 중심 갱신 → 점유 기록 반영 → 이전 공격 취소 순서입니다. 실패하면 기존 기록·위치·점유를 유지합니다.

## 요청과 조회 API

외부 고용 요청은 PlayScene.TryHireCompanion을 사용하며 기존 TestScene 호출도 상속으로 유지합니다. 고용 번호와 CompanionHireResult를 반환하며 고용 규칙은 컨트롤러가 판정합니다.

| API | 역할 |
| --- | --- |
| TryHireOrRehire(stageId, weaponType, out hireId, out result) | 타입의 재고용 가능 기록 우선, 없으면 신규 고용 |
| TryHire(stageId, data, out hireId, out result) / TryRehire(stageId, hireId, out result) | 신규 기록 고용 / 지정 기록 재고용 |
| CanHireOrRehire(weaponType, out result) | 타입 슬롯의 요청 가능 여부·불가 사유 조회 |
| GetRehireRemainingTime(weaponType) | 타입 공통 대기의 남은 게임 시간 |
| EntryCount / TryGetEntryAt(index, out entry) | 사망·대기를 포함한 기록 수 / 0부터 시작하는 고용 순번 조회 |
| TryGetRehireEntry(data, out entry) | 같은 설정의 재고용 가능 기록 조회 |
| CompanionPlacementController.TryPlace(hireId, out result) | 현재 포인트에 지정 기록 배치 |
| BeginCombat(stageId, out result, out participantIds) | 전체 전투 준비·행동 허용, 참여 번호 반환 |
| BeginCombat(stageId, out result) | 호환 호출. 성공 후 표시 알림까지 수행 |
| NotifyEntriesChanged() / GetActiveCompanionIds() | 확정된 기록 표시 갱신 / 생존 참여 번호 목록 조회 |
| StopStageActions() / EndStage(stageId) | 행동 중단·기록 유지 / 객체·기록 정리 |

CanHireOrRehire는 생성 공간·초기화 성공까지 보장하지 않으므로 실제 요청 결과가 최종 판정입니다. 개체 상태는 OnEntryChanged, 타입 대기 만료·요청 허용 변경은 OnHireAvailabilityChanged로 재조회합니다. 화면 연결 기준은 [연계 가이드](../LJ_INTEGRATION_GUIDE.md#동료-기획-정합성과-표시-연계)에 둡니다.

## 미션 참여와 종료 연결

첫 전투는 전체 준비 성공 → 고용 상태·참여 번호 확정 → 행동 허용 → 미션 플레이/참여 반영 → 표시 알림 순서입니다. 전투 중 고용·재고용은 OnParticipationConfirmed를 OnEntryChanged보다 먼저 전달합니다. 같은 고용 번호의 재고용·웨이브 재진입은 추가 인원으로 세지 않습니다.

최종 사건 전달 후 StopStageActions로 행동을 중단하고 객체·기록을 유지합니다. 정산 성공 후 EndStage로 정리하며 정산 실패 중에는 행동을 재개하지 않습니다. 정리만 실패하면 TryCompleteStageCleanup으로 재화 정산을 반복하지 않고 재시도합니다. 고용 관리자 자체 비활성화는 전체 기록을 정리하므로 일시정지와 구분합니다. 전체 종료 계약은 [PLAY_FLOW](PLAY_FLOW.md#최종-종료와-완료-통지)를 참조합니다.

## 탐색과 이동/공격

| 설정 | 역할 |
| --- | --- |
| DetectionRadius / DetectionMargin | 공격 시작 거리 + 탐색 여유. 동료 위치 기준 3D 후보 범위 |
| ActivityRadius / DefendRadius | 플레이어 중심 추종 반경 / 배치점 중심 방어 반경 |
| AttackRange | 적까지의 수평 접근 정지·공격 시작 거리 |
| FollowDistance / ArrivalDistance | 추종 정지 거리 / 배치점 도착·경로 갱신 허용 거리 |
| SearchInterval | 우선 대상·후퇴 경로 재판단 주기 |

```text
효과·공격 쿨타임 갱신 → 넉백/행동 제한 → 활동 범위 확인
→ 공격 우선 대상·가까운 위협 탐색 → 후퇴 또는 접근 → 정지·회전·공격
```

탐색 주기마다 전달받은 목표 위치에 가까운 유효 적을 선택하며, 위치 미전달 시 동료 위치를 기준으로 합니다. 공격 모션 중에는 대상을 고정합니다. 접근 실패 대상은 다음 선택에서 한 번 제외하되 후퇴를 판단할 위협에는 포함합니다.

후퇴는 시작 거리 안에서 진입하고 확보 거리까지 유지합니다. 활동 반경 안의 유효 NavMesh 경로를 우선하므로 확보 거리를 항상 보장하지 않습니다. 경로 실패는 탐색 주기에 재판단하며, 이동할 수 없으면 정지한 채 사거리 안의 적을 공격할 수 있습니다. 대상이 없으면 추종/배치점 복귀를, 활동 범위 밖에서는 복귀를 우선합니다.

검사는 범위 공격, 궁수·마법사는 투사체 공격을 사용합니다. CompanionCombatAnimationBinding은 적중 시 현재 대상의 생존과 조준 위치를 확인한 뒤 같은 공격 회차의 타격을 승인합니다. 일반 사망은 이미 발사된 공격을 유지하고 스테이지 종료·비활성화는 남은 실행도 정리합니다.

## CSV 수치 관리

동료 한 타입을 한 행으로 관리합니다. [Companion.csv](../../Assets/03.%20Configs/Combat/Companion/Companion.csv)는 기획 초기값용, [Companion_Test.csv](../../Assets/03.%20Configs/Combat/Companion/Companion_Test.csv)는 개발 테스트용입니다. 미정 수치는 기존 개발값을 사용하며 후퇴 후보 수·NavMesh 보정 거리는 임시 설정입니다.

| 분류 | CSV 컬럼 |
| --- | --- |
| 식별 | WeaponType: Sword / Bow / Staff |
| 능력치 | MaxHp, DamageReductionRate |
| 공격 | RawDamage, AttackInterval, AttackRadius, AttackAngle |
| 이동 | MoveSpeed, RotationSpeed, FollowDistance, ArrivalDistance |
| 범위/탐색 | ActivityRadius, AttackRange, DetectionMargin, DefendRadius, SearchInterval |
| 후퇴 | CanRetreat, RetreatStartDistance, RetreatDistance, RetreatCandidateCount, RetreatSampleDistance |
| 고용 | HireCost, RehireDelay |

`Tools > Companion CSV Importer`에서 CSV와 고용 맵을 선택해 변경 내용을 확인한 뒤 적용합니다. 기존 고용/동료/능력치/공격 SO의 수치를 갱신하며 프리팹과 참조는 유지합니다. 플레이어와 공유하는 투사체 설정은 별도 관리합니다.

입력 단위는 거리 m, 시간 초, 비율 0~1입니다. AttackRange는 공격 시작 거리, AttackRadius는 타격 반경입니다. 적용값은 다음 동료 초기화부터 사용합니다.

## 피격과 넉백 표현

일반 피격은 이동·공격을 취소하지 않고 피격 표현을 요청하며 치명 피해는 사망 표현을 우선합니다. 넉백은 표현 상태 반영 → 행동 제한 → 진행 공격 취소 → 넉백 피격 재생 순서입니다. 종료 후 생존·기절 상태에 맞춰 행동을 재계산합니다. 공통 효과 계약은 [COMBAT_EFFECT_SYSTEM.md](COMBAT_EFFECT_SYSTEM.md)를 참조합니다.
