# UPGRADE_SYSTEM

LJ 담당 영구 강화 기반입니다. 구매·저장, 플레이어/동료 적용과 설치물 결과 제공, CSV 임포터를 구현했습니다. LJ 기반 구현은 완료했으며 감속 강화와 설치물별 지원 계약을 포함합니다. 실제 설치물 적용은 KMB, 감속 강화 표시와 나머지 실제 UI 연결은 LHJ 연계 작업입니다. 콘텐츠 에셋 설정과 통합 확인은 구현 완료와 구분합니다.

## 구성

스크립트는 `Upgrade`의 공통 파일과 `Player/`, `Weapon/`, `Companion/`, `Installation/`에 있습니다. UpgradeSaveData는 공통 `Save` 폴더에 둡니다. 클래스는 별도 파일이며 전용 LevelData만 중첩합니다.

| 구성 | 책임 |
| --- | --- |
| UpgradeData / UpgradeLevelData | 고정 ID, 최대 단계·가격 조회와 공통 초기 설정 검사 / 단계별 가격 |
| PlayerHpUpgradeData / PlayerMoveSpeedUpgradeData | 플레이어 HP 추가량 / 이동속도 증가율, 독립 구매 |
| WeaponDamageUpgradeData / WeaponAttackSpeedUpgradeData | WeaponType별 직접 피해 / 기본 공격속도 증가율, 독립 구매 |
| CompanionUpgradeData | CompanionData별 HP·직접 피해 동시 강화. 두 증가율은 별도 설정 |
| InstallationDamageUpgradeData / InstallationIntervalUpgradeData | TrapData별 피해 / 공격 주기 단축, 독립 구매 |
| InstallationSlowUpgradeData | TrapData별 감속률 추가량, 독립 구매 |
| InstallationUpgradeData | 공통 설치물 대상, ApplyToStats로 효과 반영, ValidateCompatibility로 실제 설치물 호환성 검사 |
| IInstallationUpgradeTarget / InstallationUpgradeSupport | 설치물이 지원하는 강화 종류 제공. None / Damage / Interval / Slow 조합 |
| IInstallationSlowSource | 설치물이 실제 사용하는 기본 SlowEffectData 제공 |
| UpgradeDataMap | ID·타입 조회, 초기 설정과 ID·대상 중복 검사 |
| UpgradeProgress / UpgradePurchase | 단일 진행의 단계 복원·관리 / 구매 조건과 통합 저장·반영 조율 |
| PlayerUpgradeCalculator / WeaponUpgradeCalculator | 현재 진행도의 플레이어 공통·선택 무기 결과 계산 |
| CompanionUpgradeSnapshot | 입장 시 확정한 동료별 결과 보관 |
| InstallationUpgradeCalculator / InstallationUpgradeSnapshot | 설치물 하나의 결과 계산 / 입장 시 대상별 결과 보관 |

공통 부모에는 대상별 효과를 넣지 않습니다. 기존 UpgradeKind·범용 Value·UpgradeStatCalculator는 사용하지 않습니다. HeroUpgradeStats·CompanionUpgradeStats·InstallationUpgradeStats는 계산 결과 전달용이며 SO 원본을 수정하지 않습니다.

## 단계와 적용

- 0단계는 미강화입니다. 단계별 효과는 기본값 대비 최종 증가량이며 이전 단계 효과를 누적해서 더하지 않습니다.
- 선형 3단계는 초기 기획 기준입니다. 코드의 최대 단계는 해당 SO의 단계 개수이며 가격·효과는 에셋에서 설정합니다.
- 플레이어 HP는 기본값에 추가, 이동·직접 피해는 기본값 × (1 + 증가율)로 적용합니다. 동료 HP·피해도 기본값 대비 증가율입니다.
- 기본 공격 간격은 기본 간격 ÷ (1 + 공격속도 증가율)입니다. 스킬/궁극기 직접 피해에도 무기 피해 증가율을 전달하며 스킬 쿨타임·에너지 비용은 바꾸지 않습니다.
- 설치물 결과는 DamageBonus·IntervalReduction·SlowRateBonus입니다. 피해는 기본 피해 × (1 + 증가율), 주기는 기본 간격 × (1 - 단축률), 감속은 기본 감속률 + 추가량입니다. 설치 시 도전별 확정 결과를 조회하며 실제 설치물 적용은 담당별 연계 계약을 따릅니다.
- 기본 설정 검사는 초기화에서 수행합니다. 중복 기준은 플레이어 항목 타입, 무기 항목 타입+WeaponType, CompanionData, 설치물 항목 타입+TrapData입니다.

## 설치물 강화 계약

각 설치물 강화 데이터는 `ApplyToStats(level, ref stats)`에서 자신이 담당하는 항목만 설정합니다. 계산기는 구체 설치물 타입을 나열하지 않고 같은 결과에 효과를 반영하며, 스냅샷은 입장 시 그 결과를 보존합니다. 새 설치물이 기존 강화 종류를 사용하면 지원 정보와 실제 적용을 연결하며, 새 효과 종류가 필요할 때 해당 강화 데이터와 결과 항목을 확장합니다.

`TrapData.Prefab`과 같은 GameObject에서 `IInstallationUpgradeTarget.SupportedUpgrades`로 Damage / Interval / Slow 지원 조합을 제공합니다. 감속 설치물은 같은 대상의 `IInstallationSlowSource.SlowData`로 실제 사용하는 기본 효과를 제공하며 검사용 기본값을 따로 두지 않습니다.

도전 입장 성공 후 같은 stageId·TrapData의 확정 결과를 설치 초기화에 전달합니다. 설치물 내부 재사용 시 이전 도전 결과가 남지 않게 초기화하며 원본 SO·저장 진행도를 직접 수정하지 않습니다. 호환성 검사는 아래 검사 책임을 따릅니다.

### 감속 강화

`InstallationSlowUpgradeData.GetSlowRateBonus(level)`은 현재 단계의 감속률 추가량을 제공합니다. 0단계는 0이며 이전 단계 추가량을 누적하지 않습니다. `InstallationUpgradeStats.CalculateSlowRate(baseSlowRate)`로 기본 감속률에 확정 추가량을 더합니다. 추가량 `0.2`는 감속률 20%p 추가를 뜻하며, 단계별 실제 밸런스 수치는 콘텐츠 에셋에서 설정합니다.

기본 감속률과 각 단계 추가량은 0~1이며, 실제 기본값으로 계산한 모든 단계의 요청 감속률은 1 이하여야 합니다. 계산한 감속률은 `SlowEffectInfo(context, slowData, slowRate)`에 전달하며 원본 SlowEffectData를 수정하지 않습니다. 수신 측 저항·상한 처리는 [효과 계약](COMBAT_EFFECT_SYSTEM.md#감속과-기절)을 따릅니다.

### 검사 책임

`UpgradeDataMap.Validate()`는 기본 데이터·필수 대상 참조·가격·효과·중복을 검사합니다. 설치물의 지원 인터페이스 미연결이나 강화 미지원 여부를 기본 초기화 실패 조건에 포함하지 않습니다.

실제 설치물 호환성은 기본 데이터 검사 후 `InstallationUpgradeData.ValidateCompatibility(out error)`로 별도 확인합니다. `TrapData.Prefab`에서 지원 종류를 조회하고, 감속 강화는 같은 대상에서 기본 감속 데이터와 단계별 합계도 확인합니다. 기본 맵 검사 통과만으로 실제 적용 가능하다고 판단하지 않습니다.

`Tools > Upgrade Verification`의 실제 설정 검사는 씬 없이 사용할 수 있습니다. 구매·저장·실제 설치 효과 검증은 초기화된 PlayScene/TestScene과 정상 도전 흐름이 필요하며 특정 공용 씬으로 제한되지 않습니다. 강화 결과는 입장 시 확정하므로 변경한 설정의 적용은 새 도전에서 확인합니다.

담당별 연결 방법은 [연계 가이드](../LJ_INTEGRATION_GUIDE.md#강화-연결), 남은 설정·연계·통합 확인은 [TODO](../TODO.md#강화-연결검증)에서 관리합니다.

## 구매·저장 계약

로비는 LobbyController, 플레이 씬은 PlayScene의 `GetUpgradeState(id)`, `TryUpgrade(id)`, `OnUpgradeChanged`를 사용합니다. 기존 TestScene 호출은 상속으로 유지합니다. UpgradeState는 결과·현재 단계·다음 가격만 제공하며 효과는 구체적인 데이터에서 조회합니다. 로비 수명과 대상별 미리보기는 [로비 시스템](LOBBY_SYSTEM.md)을 참조합니다.

구매는 시작 전 로비와 정산·정리가 끝난 스테이지 결과 화면에서 허용합니다. 설치 준비를 포함한 도전 중에는 구매할 수 없습니다. 결과 화면에서 구매한 강화는 다음 스테이지나 재도전 등 새 도전에 적용합니다.

조건 조회 → 잔액·다음 단계 통합 저장 → 진행도 반영 → 잔액 반영·재화 알림 → 강화 변경 알림 순서입니다. 저장 실패 시 런타임 상태를 유지하며 Currency.TrySpend로 먼저 차감하지 않습니다. 결과는 Success / Unavailable / UnknownUpgrade / MaxLevel / CannotSpend / SaveFailed입니다. 저장 형식·실패 처리는 [SAVE_SYSTEM.md](SAVE_SYSTEM.md)를 참조합니다.

## 조회와 구매 API

| 진입점 | 계약 |
| --- | --- |
| PlayScene.GetUpgradeState(id) / TryUpgrade(id) / OnUpgradeChanged | 구매 상태 조회·요청·변경 알림. 구독 후 현재값을 최초 조회하며 효과는 구체적인 강화 데이터에서 조회 |
| TryGetHeroUpgradePreview(type, out stats) / TryGetInstallationUpgradePreview(data, out stats) | 입장 전 현재 진행도의 미리보기. 도전 적용값과 구분 |
| TryGetStageInstallationStats(stageId, trapData, out stats) | 현재 도전의 확정 설치물 결과 조회 |
| InstallationUpgradeStats.CalculateDamage / CalculateInterval / CalculateSlowRate | 실제 기본값에 확정 강화 결과 적용 |

동료는 고용 관리자가 CompanionData로 도전 스냅샷을 조회해 PrepareHire에 전달합니다. 외부에서 저장 단계를 다시 계산하지 않습니다. UI는 동료 HP·피해를 분리하고 감속 추가량은 위 감속 강화 계약에 따라 %p로 표시합니다.

## 초기화·도전 수명

- Init: [공통 진행 복원 순서](PLAY_FLOW.md#진행-연결과-씬-초기화)에 따라 강화 단계를 복원하고 구매·저장 연결을 준비합니다. 파일 준비는 SaveManager/SaveService가 담당합니다.
- BeginStage: 입장 시 강화 결과와 동료·설치물 스냅샷을 확정합니다. 전체 준비 순서는 [공통 도전 시작](PLAY_FLOW.md#도전-시작과-전투-구간)을 따릅니다.
- 입장 실패는 행동을 중단하며, 고용 시작 실패 시 이번에 시작한 재화 도전을 포기 처리합니다. 전체 성공 전에는 설치물 스냅샷을 씬 필드에 확정하지 않습니다.
- 동료 생성·재고용과 실제 설치 요청은 입장 시 결과를 사용합니다. 미리보기는 현재 진행도로 필요한 영역만 계산하며 설치물 미리보기는 지정 대상 하나만 계산합니다.
- 종료 정리 완료 후 도전용 강화 스냅샷을 해제합니다. 저장·정산·정리의 완료 경계와 실패 재시도는 [공통 종료 계약](PLAY_FLOW.md#최종-종료와-완료-통지)을 따릅니다.
- PlayScene 재활성화는 이벤트를 다시 연결하며 Init이나 새 도전 시작을 대신하지 않습니다. TestScene도 같은 수명을 상속합니다.

## CSV 임포트

Editor 도구는 `Assets/10. Editor/Upgrade/`, 테스트 원본은 [Upgrade_Test.csv](../../Assets/03.%20Configs/Upgrade/Upgrade_Test.csv)입니다.

1. `Tools > Upgrade CSV Importer`에서 CSV·기존 Assets 출력 폴더·UpgradeDataMap을 지정합니다.
2. `검사 및 미리보기`에서 오류와 생성/갱신/변경 없음, 기존 값과 반영 값을 확인합니다.
3. `적용` 시 재검사합니다. 입력이나 에셋이 바뀌었으면 미리보기를 갱신하며 다시 적용해야 합니다. Play Mode에서는 적용하지 않습니다.

CSV는 UTF-8이며 한 행이 한 강화의 한 단계입니다. 같은 ID의 단계는 1부터 연속이어야 합니다. 공통 컬럼은 `UpgradeId, Type, WeaponType, TargetAssetPath, Level, Price`이며 가격은 양의 정수입니다.

| Type | 대상 컬럼 | 필수 효과 컬럼 |
| --- | --- | --- |
| PlayerHp | 없음 | MaxHpBonus |
| PlayerMoveSpeed | 없음 | MoveSpeedBonus |
| WeaponDamage | WeaponType: Sword/Bow/Staff | DamageBonus |
| WeaponAttackSpeed | WeaponType: Sword/Bow/Staff | AttackSpeedBonus |
| CompanionStats | TargetAssetPath: CompanionData 경로 | HpBonus, DamageBonus |
| InstallationDamage | TargetAssetPath: TrapData 경로 | DamageBonus |
| InstallationInterval | TargetAssetPath: TrapData 경로 | IntervalReduction |
| InstallationSlow | TargetAssetPath: TrapData 경로 | SlowRateBonus |

헤더에는 공통·효과 컬럼을 포함하고 해당 종류에서 사용하지 않는 칸은 비웁니다. 기존 CSV는 SlowRateBonus 컬럼 없이도 읽을 수 있으며 InstallationSlow 행을 사용할 때는 해당 컬럼과 값이 필요합니다. 효과는 0 이상이며 비율은 `0.2 = 20%`, IntervalReduction은 1 미만, SlowRateBonus는 0~1입니다. 대상 경로는 `Assets/`부터 작성합니다.

전체 CSV와 기존 항목을 합친 최종 맵의 기본 데이터를 검사한 뒤 적용합니다. 실제 설치물 호환성은 별도 확인합니다. 동일 ID는 기존 SO를 제자리 갱신하며 CSV에 없는 에셋·맵 항목은 유지합니다. 타입/경로/ID/대상 중복 등 검사 오류는 적용을 막습니다. 적용 중 저장 예외는 일부 반영 가능성과 확인할 경로를 표시하며 자동 롤백하지 않습니다. ID 변경·최대 단계 축소는 기존 저장 진행도와의 호환성을 확인해야 합니다.

## 설정·연결과 확인 범위

종류별 SO에 고정 ID·대상·단계별 가격/효과를 설정하고 UpgradeDataMap에 등록합니다. 기존 범용 UpgradeData 에셋은 구체적인 종류로 다시 생성합니다. 동료는 고용 설정과 같은 CompanionData, 설치물은 설치 시스템과 같은 TrapData를 연결합니다. 플레이어 기본 공격 모션에는 Animator의 AttackSpeed 파라미터와 상태 속도 배율 연결이 필요합니다.

설치물 강화 설정은 공용 `!_UpgradeDataMap`에서 확인합니다. 설치물별 지원 정보와 감속 적용은 위 계약을 따릅니다.

구매/저장 확인 범위는 [SAVE_TEST.md](Test/SAVE_TEST.md), 실제 LHJ UI/KMB 설치물 적용 계약은 [LJ_INTEGRATION_GUIDE.md](../LJ_INTEGRATION_GUIDE.md#강화-연결)를 참조합니다. 저장 단계 복원과 실제 능력치 적용 검증은 구분합니다.

확인 이력은 [DONE.md](../DONE.md), 남은 검증·연동은 [TODO.md](../TODO.md#강화-연결검증)에서 관리합니다.
