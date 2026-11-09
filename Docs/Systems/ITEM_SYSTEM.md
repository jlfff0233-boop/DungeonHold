# ITEM_SYSTEM

LJ 담당 소모품 사용·보유 상태입니다. 소모품 구매·사용 기본 동작과 공용 10칸 슬롯에서 기존 사용 처리로의 연결을 사용자 확인했습니다.

## 구성

| 모듈 | 책임 |
| --- | --- |
| ItemData / RecoveryItemData | ID·이름·아이콘·보유 상한·쿨타임·선택적 UseVfx와 회복량 설정 |
| HealthItemData / EnergyItemData | 체력 회복·에너지 충전 조건과 효과 |
| ItemDataMap | 설정 검사와 ID 조회 |
| ItemInventory | 보유 수량·사용 가능 시각·도전 사용 횟수 |
| HeroItemUse / ItemUseContext | 사용 조건·저장·효과 적용 조율 / Health·Energy 참조 |
| ItemQuickSlotData / QuickSlotContext | 슬롯의 ItemData 참조 / 기존 사용 처리 호출 |
| PlayScene / PlaySession | 초기화·입력·저장 연결 / 도전 수명 조율 |

현재 ItemData는 소모품 효과 계약이며 무기·설치물 상품의 공통 부모가 아닙니다. SO에는 런타임 수량과 쿨타임을 기록하지 않습니다.

TestScene은 PlayScene을 상속한 직접 테스트 시작 경로이며 기존 호출 API는 유지합니다.

## 사용 계약

- 씬 활성·도전 준비 완료·종료 예약 없음일 때 요청을 처리합니다.
- ID 조회 → 쿨타임 → Hero.CanUseItem(CanAct와 효과 조건) → 보유 수량 순서로 확인합니다.
- 저장 성공 → 수량·다음 사용 시각·사용 횟수 반영 → OnUseCommitted(itemId) → 효과 적용과 자원 알림 → UseVfx 요청 → 사용음 요청 → 인벤토리 OnChanged 순서입니다. 저장 실패 시 상태와 효과를 바꾸지 않으며, 재생 실패로 사용 결과를 되돌리지 않습니다. 출력 계약은 [사운드](SOUND_SYSTEM.md)·[VFX](VFX_SYSTEM.md) 시스템을 참조합니다.
- PlaySession은 OnUseCommitted에서 미션 사용 횟수를 기록합니다. 준비 구간은 제외하며 효과/HP 알림보다 먼저 기록합니다. ItemInventory의 전체 도전 사용 횟수와 미션의 플레이 구간 사용 횟수는 구분합니다. 미션 수명은 [MISSION_SYSTEM.md](MISSION_SYSTEM.md)를 참조합니다.
- 쿨타임은 아이템별 독립이며 Time.timeAsDouble 기준입니다. 준비·사망 중에도 게임 시간이 흐르면 경과하고 timeScale 0에서는 멈춥니다.
- 전체 도전 시작 성공 후 쿨타임·사용 횟수를 초기화하고 수량은 유지합니다. 단순 비활성화·재활성화에서는 초기화하지 않습니다.
- 도전 정산 성공 시 LastStageUsedItemCount를 보존하고 후속 정리·예약 해제 후 OnStageEndProcessed(int, bool, bool)를 알립니다. 상세 수명은 [연계 문서](../LJ_INTEGRATION_GUIDE.md#아이템공용-슬롯-연결)를 참조합니다.

## 현재 입력·설정

- PlayScene의 `_quickSlots`에 ItemQuickSlotData를 연결하고 해당 SO가 기존 ItemData를 참조합니다. 기본 슬롯 배정은 [연계 가이드](../LJ_INTEGRATION_GUIDE.md#슬롯-구성과-실행)를 참조합니다.
- 회복량·충전량·보유 상한·쿨타임은 연결된 ItemData에서 조회합니다.
- 판매 목록과 사용 슬롯은 별도입니다.
- 초기화에서 아이템 목록·공용 10칸·할당된 SO 참조를 확인합니다. 실행 흐름은 번호 입력 → PlayScene.TryExecuteQuickSlot → ItemQuickSlotData → QuickSlotContext → HeroItemUse입니다.
- UI의 슬롯 조회와 초기화 후 재연결은 [아이템·공용 슬롯 연결](../LJ_INTEGRATION_GUIDE.md#아이템공용-슬롯-연결)을 따릅니다.

## 구매에서 재사용할 API

- TryGetCountAfterAdd: 보유 상한 안에서 한 개 추가한 수량 계산. 상태 변경 없음.
- ApplySavedCount / NotifyChanged: 저장된 수량 반영과 변경 알림. 구매 후 관련 상태를 모두 반영한 뒤 알립니다.
- ApplyUse: 쿨타임·사용 횟수도 변경하므로 구매에는 호출하지 않습니다.

구매 계약은 [SHOP_SYSTEM](SHOP_SYSTEM.md), 저장은 [SAVE_SYSTEM](SAVE_SYSTEM.md)을 참조합니다. 확인 이력은 [DONE](../DONE.md), 남은 작업은 [TODO](../TODO.md#아이템상점공용-슬롯-후속-계획)에서 관리합니다.
