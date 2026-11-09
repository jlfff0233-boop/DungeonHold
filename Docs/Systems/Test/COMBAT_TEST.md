# COMBAT_TEST

## 문서 역할

PlayerTest의 연결·조작·애니메이션 신호·기즈모를 관리합니다. 실행 계약은 [COMBAT_SYSTEM.md](../COMBAT_SYSTEM.md), 남은 검증은 [COMBAT_TODO.md](../Todo/COMBAT_TODO.md), 확인 결과는 [DONE.md](../../DONE.md)를 참조합니다.

## 현재 확인 범위

사용자가 이동 중 기본 공격·검 스킬, 검 궁극기 사용·애니메이션 재생·궁극기 중 이동 차단을 확인했습니다. 기존 이동·기본 공격·사망 모션과 자동 부활 확인 기록은 유지합니다. 이번 궁극기의 피해량·에너지 차감·최종 복귀와 취소/사망 경로까지 확인한 것으로 확대하지 않습니다.

플레이어 점프·피격·넉백 표현과 회피를 연결했습니다. 점프·공중 넉백 표현, 회피 재생과 Play 종료 시 카메라 참조 오류 해소를 사용자 확인했습니다. 일반 피격 경직 없음 규칙을 유지하며 실제 회피 무적·취소 경로는 추가 검증이 필요합니다. 동료 보완의 기본 동작 확인과 추가 검증은 [COMPANION_TEST.md](COMPANION_TEST.md)에서 관리합니다.

활 전투 로직의 기본 동작과 전용 모션 재생을 사용자 확인했습니다. 저장된 기본 공격·스킬·궁극기 이벤트 연결도 확인했습니다. 모션 교체 후 전체 전투 회귀와 예외 검증은 COMBAT_TODO에서 관리합니다.

상체 일반 피격과 궁극기 대기 자세를 사용자 플레이에서 확인했습니다. 활 기본 발사와 앞뒤 보행 조준 자세는 메인 프로젝트에서 확인했습니다.

화살 트레일·활 궁극기와 스태프 비행·스킬 충돌·불기둥 유지의 기본 출력은 사용자 확인 범위입니다. 잔여 표현·수명 검증은 [공용 VFX TODO](../../TODO.md#vfx-남은-작업)를 참조합니다.

## 씬과 초기화

화살은 기본·관통·동료 궁수·궁극기에서 부착 트레일을 확인합니다. 정상 충돌·착지 후에는 잔상이 자연 소멸하고, 실행 무효화·비활성화에서는 즉시 제거돼야 합니다. 스태프의 FlightVfx도 함께 확인합니다.

PlayScene은 GameManager가 초기화한 Sound·Vfx 서비스를 전달하며 씬에서 별도 매니저를 생성·초기화하지 않습니다. 효과 배정은 [VFX 셋업](../VFX_SYSTEM.md#공용-셋업-사용법)을 사용합니다. 기본 사운드 청취와 일부 VFX 출력 확인, 추가 수명 검증은 [사운드](../SOUND_SYSTEM.md#확인-범위와-후속)·[VFX](../VFX_SYSTEM.md#확인-범위와-후속) 시스템에서 구분합니다.

상단에는 시작 설정·수동 새 도전 시작과 빠른 정산이 있습니다. 빠른 정산은 성공 후 다음 도전을 자동 준비하며 일괄 실행은 [종료 보상 검증](CURRENCY_TEST.md#종료-보상)을 따릅니다. 진행 관리에는 전투 시작·도전 종료/포기·남은 정리·커서 조작을 모았으며 미션/도감은 별도 영역입니다.

플레이 중 TestScene의 **플레이 디버그**에서 상단 저장/조회와 접이식 상세 영역을 사용합니다. 전투 검증은 플레이어·동료·전투 대상, 경제 검증은 재화/보상 추가 검증을 펼칩니다. 디버그 전용 컴포넌트는 필요 없지만 공통 저장 진행은 준비돼 있어야 합니다. 피해/효과에는 대상과 별도 발생원, 효과/넉백에는 해당 SO를 지정합니다. 동료는 고용 기록 선택 후 전투 대상으로 지정하며 사망 후에는 같은 기록으로 재고용합니다. 보상 더미 검증만 선택적으로 EnemyRewardTest를 지정하고 실제 KMB 적의 직접 처치 전달과 중복 연결하지 않습니다. 저장 연결/검증은 [SAVE_TEST.md](SAVE_TEST.md)를 참조합니다.

씬 재초기화는 명시적으로 요청합니다. 종료 예약의 접수와 실제 정산 완료를 구분하며, 상태 표시가 게임 시간을 추가 갱신하지는 않습니다. 기능별 확인 범위는 위 기록과 TODO를 참조합니다.

씬은 PlayerTest입니다. TestScene.Start는 직접 테스트용 진행 준비와 자동 시작을 담당하며 공통 초기화·갱신은 PlayScene에서 상속합니다. PlaySession이 도전 시작에서 Hero.Init/ResetStageResources/ResetReviveCount를 호출합니다. PlayScene.Update는 부활 → 회피 시간·무적 → 상태이상 → 전투 쿨타임·콤보 → 입력 → 카메라·이동 순서로 처리합니다.

| 대상 | 연결할 참조 |
| --- | --- |
| Hero | 기존 공통 모듈과 타입별 Weapon Bindings의 실행자 3개·모델·Source. 스킬/궁극기 데이터는 WeaponData에서 전달 |
| Hero의 Dodge Data | Player_20_DodgeData 연결. HeroDodge는 Hero가 생성하는 일반 C# 클래스이며 별도 부착하지 않음 |
| Attacker | 활·스태프는 각각의 ProjectileAttackExecutor, 검은 AreaAttackExecutor |
| 기본 스킬 SkillCaster | 활은 BowSkillExecutor, 스태프는 StaffSkillExecutor, 검은 SwordSkillExecutor |
| 궁극기 SkillCaster | 활은 BowUltimateExecutor, 스태프는 StaffUltimateExecutor, 검은 AreaAttackExecutor |
| AreaAttackExecutor / SwordSkillExecutor | HitDetector, Detection Center, 방향 기준 Transform, Enemy Layer Mask |
| 공통 저장 관리자 | GameBootstrap으로 생성된 GameManager의 Save 사용. 준비 방법은 [SAVE_TEST.md](SAVE_TEST.md#씬-연결) 참조 |
| TestScene | Hero, InputHandler, CameraFocus, 무기 맵/선택 타입, Currency, 보상/콤보 설정, 고용/배치 컨트롤러와 고용 데이터 맵 |
| 피해 대상 | Health/DamageReceiver와 조회 가능한 Collider, 탐색 마스크에 포함되는 레이어 |

AttackExecutor는 추상 부모이므로 기존 컴포넌트를 그대로 사용하지 않습니다. 기존 프리팹·동료의 교체/재연결 여부를 확인합니다. 실제 실행자는 Hero의 무기별 연결에서 선택합니다.

## 원거리·활 테스트 연결

TestScene은 저장된 선택 무기 또는 명시적으로 전달한 해금 무기를 PlaySession에 전달하고, WeaponDataMap 조회/강화 결과 확정 후 Hero.Init을 호출합니다. 저장 선택 복원은 사용자 확인했으며 실제 로비 연결은 후속입니다. 무기 모델은 기본 비활성화하고 실행자·발사점은 표시 전용 모델 루트 밖의 활성 계층에 둡니다.

| 대상 | 필요한 설정 |
| --- | --- |
| Mover | Use Aim Rotation 활성화 |
| CameraAimProvider | 실제 플레이 Camera, 조준 거리, 적·지형 탐색 마스크 |
| ProjectileAttackExecutor | 조준 제공자, 몸 쪽 Launch Origin, 실제 Fire Point, ProjectileData |
| ProjectileData | 활성 DirectHitProjectile 프리팹, 양수 속도·최대 거리·충돌 반경, 충돌 마스크 |
| Attacker | ProjectileAttackExecutor 연결 |
| BowSkillExecutor / BowSkillData | 조준·Launch Origin·Fire Point, 관통 전용 ProjectileData와 PiercingProjectile 연결 |
| BowUltimateExecutor / BowUltimateData | CameraGroundAimProvider, ArrowRainArea 프리팹과 타격 설정 연결 |
| CameraGroundAimProvider | 실제 Camera, Ground Mask는 Floor, Obstacle Mask는 Wall. 실제 Collider 레이어 일치 |
| ArrowRainArea | 자체 HitDetector, Enemy 대상 마스크. 생성 후 범위 기즈모 표시 |

평지와 단차에서 화살 묶음이 지면에 착지할 때 효과가 한 번 재생되는지 확인합니다. 실행이 무효화되면 남은 화살이 착지 효과 없이 정리돼야 합니다.

투사체는 코드로 충돌을 검사하므로 Rigidbody/Collider가 필요하지 않습니다. 아군 통과는 조준·충돌 마스크에서 Player/Companian을 제외하고 실제 적 피격 Collider·지형 레이어를 포함해 설정합니다. 발사/완료 신호는 기존 Animation Event 경로를 사용합니다.

검·활 Override는 공통 Controller 기반입니다. 활 A 클립은 기본 공격/스킬 이벤트를 함께 가지며 AnimatorHandler의 상태·회차 검사로 구분합니다. B 클립은 궁극기 이벤트를 사용합니다. 화살비의 낙하 표현은 ArrowRainArea에 연결하고 피해는 영역에서만 처리합니다. 남은 검증은 [COMBAT_TODO.md](../Todo/COMBAT_TODO.md)에서 관리합니다.

## 스태프 테스트 연결

스태프를 선택해 도전을 시작하면 WeaponDataMap에서 해당 데이터를 전달합니다. 기본 공격과 스킬은 스태프 발사점을 사용하고 궁극기는 지면 조준 위치에 불기둥을 생성합니다.

| 대상 | 연결과 확인 |
| --- | --- |
| 기본 공격 / 스킬 | 전용 ProjectileAttackExecutor / StaffSkillExecutor가 같은 ExplosionProjectile 프리팹 사용. 스킬 전용 시각 에셋은 미연결. 비행 충돌은 적·지형, 폭발 대상은 Enemy |
| 궁극기 | 지면 조준 후 시전해 불기둥의 생성·유지·종료와 영역 피해를 확인. 기본 유지 출력은 사용자 확인 범위 |
| 애니메이션 | 기본 공격은 AnimationAttackHit/End, 스킬은 AnimationSkillHit/End, 궁극기는 AnimationUltimateHit/End. 완료 이벤트는 해당 상태를 벗어나기 전에 전달 |

피해와 화상은 대상 HP로 확인합니다. 실패 원인을 볼 때는 투사체 또는 FirePillarArea의 Show Debug Log를 사용합니다.

## 조작과 애니메이션

| 입력 | 동작 |
| --- | --- |
| 기본 공격 입력 | 조준 중에는 궁극기 확정, 그 외에는 Attacker의 기본 공격 요청 |
| Space — Player/Jump | 지상 점프 요청 |
| 왼쪽 Shift — Player/Dodge | 현재 이동 입력 방향 회피. 입력이 없으면 현재 정면 |
| Q — Player/Skill | 궁극기 조준 취소 후 선택 무기의 기본 스킬 |
| E — Player/Ultimate | 활·스태프는 범위 지정 시작/확정, 검은 즉시 사용 |

마우스로 시점을 움직여 위치를 고르고 좌클릭 또는 E로 확정합니다. 범위는 가능 초록·거리 초과 빨강·지면 없음 숨김이며, 시전 성공/취소 시 숨깁니다. 2026-10-26 사용자 확인: 화살 수정 후 정상 동작, 플레이 화면 범위 표시와 색상 전환.

Hero의 Ultimate Aim Preview Prefab에 표시 프리팹을 연결합니다. 프리팹은 HeroUltimateAimPreview와 LineRenderer, 전용 URP Unlit 머티리얼을 사용합니다. 에셋 생성·연결은 코드 작업과 구분합니다.

커서 해제 중에는 이동·시점·공격 입력을 전달하지 않습니다. 커서는 SetCursorLocked로 제어합니다. 기존 강제 피해/넉백 키 대신 통합 디버그 Inspector의 상세 영역을 사용합니다.

회피의 거리·시간·무적·쿨타임은 연결된 DodgeData를 기준으로 확인합니다. 입력 비활성화·커서 해제에서는 보관된 이동 입력만 해제합니다. 시점 변화량은 즉시 소비하므로 종료 시 0 입력을 별도 전송하지 않습니다.

| 동작 | Animator 레이어/상태 | Animation Event |
| --- | --- | --- |
| 기본 공격 | Attack / Attack | AnimationAttackHit, AnimationAttackEnd |
| 점프 | Base Layer / Jump | 없음. IsGrounded Bool로 접지 전환 |
| 일반 피격 | Attack / TakeHit | 없음. TakeHit Trigger로 진입하고 Idle 복귀 후 Weight 해제 |
| 넉백 | Base Layer / TakeHit | 없음. 행동 취소 후 직접 전환·IsKnockback Bool 사용 |
| 회피 | Base Layer / Dodge | 없음. IsDodging Bool과 코드의 진행 시간으로 종료 |
| 검 기본 스킬 | Attack / Skill | AnimationSkillHit, AnimationSkillEnd |
| 활·스태프 궁극기 준비 | Attack / UltimateReady | 없음. UltimateReady Bool로 전환 |
| 궁극기 | Base Layer / Ultimate | AnimationUltimateHit, AnimationUltimateEnd |
| 사망 | Base Layer / Die | AnimationDeathEnd |

Attack 레이어의 빈 Idle과 상체 마스크로 기본 공격·검 스킬 중 하체 이동을 유지합니다. 궁극기는 전신 재생하며 시전 중과 Base 레이어 복귀 전까지 일반 이동·회전을 제한합니다. 종료 이벤트와 최종 상태 복귀의 차이는 전투 문서를 참조합니다.

실제 애니메이션을 사용하는 플레이어는 AttackTimingTest/SkillTimingTest를 비활성화하고 임시 연출 완료 경로도 끕니다. 타이머와 Animation Event를 동시에 사용하지 않습니다.

## 스킬 데이터 확인

선택 무기의 WeaponData에서 SkillData·궁극기 데이터를 조회합니다. 활은 BowSkillData/BowUltimateData, 스태프는 StaffSkillData/StaffUltimateData를 사용합니다. 피해·범위·비용·쿨타임의 기대값은 현재 연결된 SO에서 읽고 실제 결과와 비교합니다.

두 SkillCaster는 같은 Energy를 공유하며 새 스테이지에서 에너지를 초기화합니다. 기본 공격의 유효 적중 충전과 각 스킬 소비를 별도로 확인합니다. 검 스킬은 넉백을 적용하고 AreaAttackExecutor를 사용하는 검 궁극기는 넉백을 적용하지 않습니다. 생존 대상과 스킬 피해 이하 HP의 더미를 구분해 검사합니다.

## 상태이상·넉백 연결

디버그 Inspector에서 플레이어 또는 동료를 대상으로, 별도 적을 발생원으로 지정하고 화상·감속·기절을 요청합니다. 이동 중 효과가 따라오고 만료 시 방출·공전이 멈춘 뒤 잔여 출력은 소멸까지 추적하는지 확인합니다. 기절은 캐릭터별 Stun Point 중심의 공전·재적용을 확인하며, 기준점 구성은 [VFX 문서](../VFX_SYSTEM.md#주요-api와-프리팹-계약)를 따릅니다.

플레이어/동료에는 StatusEffectController와 StatusResistanceData를 연결합니다. 감속/기절/화상 SO는 각각 Combat/SlowEffectData, StunEffectData, BurnEffectData로 생성합니다.

| 대상 | 요청 API | 갱신 주체 |
| --- | --- | --- |
| 플레이어 | StatusEffectController.TryApply 계열 | TestScene이 Hero.UpdateEffects 호출 |
| 동료 | CompanionTest.ApplyCompanionStatus(StatusEffectData) | Companion |
| 더미 | DamageTargetTest.ApplyStatus(StatusEffectData) 또는 공통 효과 API | CombatDummy |

넉백은 능력치·저항이 연결된 CombatDummy로 검증합니다. DamageTargetTest를 연결하면 요청을 직접 재현할 수 있습니다. 초기화와 갱신 계약은 [전투 효과 시스템](../COMBAT_EFFECT_SYSTEM.md)을 참조합니다.

## 피격·부활 검증

피해·즉시 부활·연출 완료는 통합 디버그 Inspector에서 실제 모듈 API로 검증합니다.

실제 사망 모션 완료 → 자동 부활 대기 → 체력 복구 → Idle 복귀 → 행동 복구를 확인합니다. 자동 부활 설정은 PlayerReviveData를 사용합니다. 피해 검증은 DamageReceiver를 통해 사망 알림까지 실행하며 HP 필드만 직접 바꾸지 않습니다.

## 탐색 기즈모

기본 공격의 AreaAttackExecutor가 표시하며 SwordSkillExecutor의 탐색 결과는 포함하지 않습니다. Scene/Game 뷰에서 Gizmos를 켭니다.

| 표시 | 의미 |
| --- | --- |
| 노란 작은 구 | 탐색 중심 |
| 하늘색 구 테두리 | 탐색 반경 |
| 파란 선 / 노란 경계선 | 수평 전방 / 좌우 각도 경계. 360도에서는 경계 생략 |
| 초록 구 테두리 | 마지막 탐색 당시 대상 위치. 실제 피해 성공 여부와 별개 |

편집 상태에서는 TestScene이 기본 공격 설정의 반경/각도를 전달하고 플레이 중에는 초기화/실행에서 전달된 범위를 사용합니다. 마지막 탐색 대상 표시는 현재 위치를 추적하지 않습니다. Editor 전용 API는 AreaAttackExecutor.SetGizmoRange(float radius, float angle)입니다.
