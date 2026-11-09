# CURRENCY_TEST

재화·콤보·처치 보상의 연결과 검증 방법입니다. 공개 계약은 [CURRENCY_SYSTEM.md](../CURRENCY_SYSTEM.md), [COMBO_SYSTEM.md](../COMBO_SYSTEM.md)를 참조합니다.

## 현재 상태

- 기본 재화 지급/소비/도전 수명, 더미 처치 집계·5킬 보상·콤보 만료는 기존 사용자 확인 범위입니다.
- 공통 초기화·갱신은 PlayScene, 도전 조율은 PlaySession이 담당합니다. TestScene은 상속한 기능을 직접 시작하는 테스트 진입점이며 CurrencyTest는 선택적 검증 도구입니다.
- 실제 KMB 적 보상은 WaveController의 직접 전달 경로를 사용합니다. 대안 EnemyRewardReceiver를 같은 적에 중복 연결하지 않습니다.
- 영구 재화 지급·소비 후 도전 종료와 씬 재초기화에서 잔액 유지를 사용자 확인했습니다.
- 더미 수동 연결과 통합 디버그 Inspector의 나머지 기능·전체 회귀 검증은 남아 있습니다.

## 연결

| 대상 | 설정 |
| --- | --- |
| 공통 저장 관리자 | GameBootstrap으로 생성된 GameManager의 Save 사용. 준비 방법은 [SAVE_TEST.md](SAVE_TEST.md#씬-연결) 참조 |
| TestScene | 기존 재화/콤보·고용/배치·무기 설정에 MissionDataMap과 미션 맵/난이도/방어 규칙 필요 |
| CombatDummy | 능력치와 저항 설정. 같은 오브젝트의 Health/Receiver/Mover/효과/넉백 모듈 사용 |
| CurrencyTest | 같은 씬의 Scene·Currency·Rewards, 검증할 재화 종류/금액 |
| 보상 더미 검증 | 선택적 EnemyRewardTest의 Health/Receiver와 CurrencyTest의 Reward Dummies |
| 실제 KMB 적 보상 | WaveController → EnemyCombat.OnDied → StageRewardController.RegisterKill. 별도 Receiver 연결·생명 번호 재발급 없음 |
| 대안 보상 수신부 | WaveController의 직접 전달을 사용하지 않는 별도 경로에서만 EnemyRewardReceiver.Init 사용 |

더미의 초기화와 프레임 갱신은 CombatDummy가 맡습니다. EnemyRewardTest는 더미 부활/처치 검증에만 사용하며 실제 적 보상 수신부와 중복 연결하지 않습니다.

## 실행

테스트 씬에 진입해 도전을 시작합니다. 시작 스테이지 재화는 TestScene 설정을 사용합니다. 수동 검증에서는 Auto Begin Stage를 끄고 준비를 마친 뒤 BeginCombat을 요청합니다.

CurrencyTest Inspector는 지급/소비, 전투 시작, 도전 종료/포기와 추가 검증을 요청합니다. 새 도전은 이전 도전의 실제 종료 성공 후 시작합니다. 종료 예약의 접수와 OnStageEndProcessed의 성공을 구분합니다.

같은 기본 조작과 추가 재화 검증은 TestScene의 통합 플레이 디버그 Inspector에서도 CurrencyTest 없이 실행할 수 있습니다. 더미는 선택한 EnemyRewardTest를 보상 연결부에 직접 연결하며 기존 CurrencyTest 연결 함수도 유지합니다. 사용 방법은 [COMBAT_TEST.md](COMBAT_TEST.md)를 참조합니다.

승리 결과와 정산·정리 재시도는 [공통 플레이 흐름](../PLAY_FLOW.md#최종-종료와-완료-통지)을 따릅니다. 저장·재실행 절차는 [SAVE_TEST](SAVE_TEST.md), 날짜별 확인 이력은 [DONE](../../DONE.md)을 참조합니다.

## 확인 순서

1. Play 진입과 시작 잔액, 6/7/8 고용 비용·재화 부족 시 거절 확인.
2. 준비·전투 단계에서 지급/소비와 결과 표시 확인.
3. 더미 생명 연결 후 처치 보상·콤보 단계·시간 만료 확인.
4. 종료/포기의 실제 결과를 받은 뒤 재도전하고 영구 잔액 유지 확인.

## 남은 검증

- 중복 사망, 이전 도전 요청, 부활·재사용 시 새 생명 번호.
- 미지급 재시도와 정산 실패 시 도전/고용 기록 유지.
- 추가 콤보 단계와 만료 경계, 동료/설치물/화상 처치.
- KMB 적/웨이브, 결과·설치·UI 연계와 현재 저장 범위의 통합 검증.

확인 이력은 [DONE.md](../../DONE.md), 남은 작업은 [TODO.md](../../TODO.md#재화-상세-작업)에 둡니다.

## 종료 보상

PlayScene의 ClearRewardData를 연결한 뒤 플레이 디버그의 빠른 정산을 사용합니다. 선택한 맵/난이도는 다음 도전부터 적용되며 현재 도전의 기준은 적용 기준 표시를 따릅니다. 승리 별·확정 점수를 선택하고 즉시 승리/패배/포기 정산을 요청합니다. 성공 후 다음 도전을 자동 준비하며 기존 수동 시작도 유지합니다.

Hard 도전에서 어려움 보상 자동 비교를 켜고 `프리셋 일괄 검증 (7개)`를 실행합니다. 현재 맵/승리 별을 유지하고 아래 순서로 실제 지급·저장을 수행합니다. 기대 금액은 편집할 수 있으며 보상 설정 변경 시 함께 조정합니다.

| 항목 | 초기 설정의 예상 클리어/패배 보상 |
| --- | ---: |
| 0점 | 360 |
| 기준 절반 | 450 |
| 기준 -1 | 539 |
| 기준점 / 기준 +1 | 540 |
| 패배 | 50 |
| 포기 | 0 |

최초 별 보상은 정산 전 지급 이력에 따라 별도로 합산합니다. 각 종료·정리 완료 후 다음 항목으로 진행하며 요청 실패·지급 불일치·다음 도전 시작 실패 시 중단합니다. 실행 중 설정 변경은 잠기며 중단 시 이미 접수한 정산은 완료됩니다. 마지막 포기 후에는 새 도전을 만들지 않습니다.

재실행 복원 절차는 [저장 검증](SAVE_TEST.md#종료-보상-저장), 실제 확인 범위는 [DONE](../../DONE.md#2026-11-05-클리어-보상-설정과-정상-지급-확인)을 참조합니다.
