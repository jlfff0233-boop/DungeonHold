# UI 리소스 적용 — 2026-10-26

기본 Background/빈 Image를 역할에 맞는 UI 리소스로 교체했다. 기존 게임 데이터 연결과 게이지 Fill 설정은 유지했다. 사용 리소스는 단일 Sprite로 임포트하도록 설정하고 GUID를 보존했다.

## 적용 목록

- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/CampanionStatusRoot/CampanionRow01/EnergyBackground -> bar_Background
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/CampanionStatusRoot/CampanionRow01 (4)/HealthBackground -> bar_Background
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/PortraitFrame -> Profile_Frame
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/HealthFrame/Health -> Hp_Bar
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/CampanionStatusRoot/CampanionRow01 (3)/EnergyBackground -> bar_Background
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/Loadout/ItemBarRoot/InstallationSlots (7) -> item_frame
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/CampanionStatusRoot/CampanionRow01 (4)/EnergyBackground/EnergyFill -> Mp_Bar
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/CampanionStatusRoot/CampanionRow01/EnergyBackground/EnergyFill -> Mp_Bar
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/Loadout/ItemBarRoot/InstallationSlots (8) -> item_frame
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/CampanionStatusRoot/CampanionRow01/HealthBackground -> bar_Background
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/Loadout/ItemBarRoot/InstallationSlots (6) -> item_frame
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/Loadout/ItemBarRoot/WeaponSlots -> Weapon_Frame
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/EnergyFrame -> bar_Background
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/Loadout/ItemBarRoot/InstallationSlots (2) -> item_frame
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/EnergyFrame/Energy -> Mp_Bar
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/CampanionStatusRoot/CampanionRow01 (3)/HealthBackground -> bar_Background
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/Loadout/ItemBarRoot/InstallationSlots (4) -> item_frame
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/Loadout/ItemBarRoot/InstallationSlots -> item_frame
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/CampanionStatusRoot/CampanionRow01 (3)/HealthBackground/HealthFill -> Hp_Bar
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/Loadout/ItemBarRoot/InstallationSlots (5) -> item_frame
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/CampanionStatusRoot/CampanionRow01 (4)/HealthBackground/HealthFill -> Hp_Bar
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/Loadout/ItemBarRoot/InstallationSlots (1) -> item_frame
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/CampanionStatusRoot/CampanionRow01 (3)/EnergyBackground/EnergyFill -> Mp_Bar
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/Loadout/ItemBarRoot/InstallationSlots (3) -> item_frame
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/CampanionStatusRoot/CampanionRow01 (4)/EnergyBackground -> bar_Background
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/HealthFrame -> bar_Background
- Assets/04. Prefabs/LHJ/HUD/PlayerStatus.prefab: PlayerStatus/CampanionStatusRoot/CampanionRow01/HealthBackground/HealthFill -> Hp_Bar
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/PreparationHintRoot/PlacementGuideRoot/ConfirmHint/KeyBox -> Keyboard_Frame
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/MinimapRoot/MarkerRoot/PlayerMarker -> Map_Player_Marker
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/MinimapRoot/MarkerRoot/EnemyMarker -> Map_Common_Enemy
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/SkillRoot/SkillSlot (1)/Background -> skill_frame_off
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/PreparationHintRoot/StarWaveHint/KeyBox -> Keyboard_Frame
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/PreparationHintRoot/SellHint/KeyBox -> Keyboard_Frame
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/Currency/CurrencyIcon -> coin_shop
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/PreparationHintRoot/PlacementGuideRoot/ForbiddenIcon -> icon_cancel
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/PreparationHintRoot/StarWaveHint -> Common_Frame
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/PreparationHintRoot/PlacementGuideRoot/CancelHint/KeyBox -> Keyboard_Frame
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/SkillRoot/SkillSlot/Background -> skill_frame_off
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/PreparationHintRoot/DefenseHint/KeyBox -> Keyboard_Frame
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/MinimapRoot/MarkerRoot/ObjectiveMarker -> Map_Defense_Point
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/PreparationHintRoot/PlacementGuideRoot/RotateHint/KeyBox -> Keyboard_Frame
- Assets/04. Prefabs/LHJ/HUD/StageStatus.prefab: StageStatus/MinimapRoot/MapImage -> Map_Stage1
- Assets/04. Prefabs/LHJ/Parts/EquipmentSlot.prefab: EquipmentSlot/SlotBackground/SelectedFrame -> item_frame_select
- Assets/04. Prefabs/LHJ/Parts/KeyHint.prefab: KeyHint/KeyBox -> Keyboard_Frame

## 검증

프리팹 참조/GUID와 변경 범위를 정적으로 확인했다. Unity 재임포트·화면 표시·Play 검증은 수행하지 않았다.

## 추가 반영

- 1번 칸은 기존 이름 WeaponSlots와 무관하게 설치물 슬롯이다. icon_trap과 item_frame을 적용했다. 무기 표시 연결은 제거했다.
- Canvas_TargetHealth와 Canvas_Health의 HP/배경을 Hp_Bar/bar_Background로 교체했다.
- 적 상태이상 3종에 화상/감속/기절 스프라이트를 적용했다. 표시 조건은 기존 상태이상 판정을 따른다.
- UI-MapTest 씬의 실제 조준점 FocusPoint에 crosshair 리소스를 적용하고 빨간 틴트를 흰색으로 변경했다. 기존 중앙 위치와 10×10 크기를 유지한다. Canvas_HUD에 중복 추가했던 Crosshair는 제거했다.
- StageStatus에 콤보 바/배경/적 아이콘/효과를 추가하고 ComboHudBinder가 기존 이미지를 재사용하도록 변경했다.
- 프리팹 ID 중복/로컬 참조를 정적으로 검사했다. Unity 실행 화면 확인은 미수행이다.

## 가이드 이미지 기준 HUD / 인벤토리 재배치

사용자가 제시한 인게임 HUD 이미지와 `Assets/99. Assets/UI/Actioin_Defense_UI/UI_guide/shop.png`를 배치 기준으로 사용했다. 리소스 검색/사용 범위는 `Assets/99. Assets/UI`로 제한했으며 개별 원본이 없는 플레이어·동료 초상화와 설치물 썸네일은 비워 둔다. 가이드에 포함된 예시 가격/수량/초상화를 실제 데이터로 복제하지 않는다.

- PlayerStatus: 좌상단 상태 정보, 두 동료 행, 하단 설치물 1~5 / 동료 6~8 / 물약 9~0 구성. 빈 이미지의 흰 사각형을 숨긴다.
- StageStatus: 상단 중앙 타이머·남은 방어 목표 수/게이지, 우상단 웨이브·미니맵, 좌하단 콤보·재화, 우하단 Q/E 스킬, 설치 안내 위치를 재배치했다. 미니맵의 기존 마커 구조는 유지하며 새 지도 추적 시스템은 추가하지 않았다.
- ReferenceHudDetails: 현재 Hero의 HP/에너지/상태이상을 읽고 설치물 비용과 현재 선택 테두리를 표시한다. 기존 실행 컨트롤러를 변경하지 않는다. 설치물 설정 사본은 현재 Test_0~4 InstallationQuickSlotData를 기준으로 연결했으며 실행 슬롯 설정 변경 시 표시 사본도 함께 갱신해야 한다.
- InventoryScreen: 보라색 배경, 왼쪽 전체/무기/함정/물약 카테고리, 5열×4행, 위쪽 빈 적 프레임 4개, 오른쪽 양피지 정보, 아래 10칸과 재화를 배치했다. 기존 15개 데이터 카드 외 마지막 행은 정보가 없는 빈 칸이다. 물약 카테고리는 기존 소모품 보유 목록 진입에 연결된다.
- ReferenceInventoryLoadout: 하단 슬롯의 표시와 현재 물약 수량/동료 비용을 읽어 표시한다. 인벤토리 표시 중 조준점을 숨기며 닫으면 이전 상태를 복원한다. 설치/고용/사용 실행 경로는 추가하지 않았다.
- 실시간 표시 코드의 위치·콤보 문구·준비 스킬 프레임을 새 배치에 맞췄다. 실제 가격/목표 수/수량을 사용하므로 가이드의 예시 숫자와 다를 수 있다.

### 확인

Unity 6000.0.68f1 UI-MapTest의 Play 모드에서 HUD와 인벤토리를 열어 배치, 빈 이미지 처리, HP/에너지 수치, 방어 목표, 인벤토리 목록·양피지·물약 아이콘·하단 수치를 확인했다. 프리팹 식별값 중복/로컬 참조, Unity 스크립트 컴파일·임포트 결과와 diff 공백 검사를 통과했다. 빌드는 수행하지 않았다. 인벤토리 시각 확인에 사용한 임시 에디터 명령은 제거했으며 검증 종료 후 Play 모드를 종료했다.

변경 전 화면 프리팹 백업: `C:/Users/bing8/.codex/visualizations/2026/10/25/01a17270-8cb8-7c12-be6e-d9619ef0aa7c/reference-layout-backup`.

## 보라색 표시 안의 남은 적 숫자

사용자가 지정한 위치는 상단 중앙 보라색 PortalLife 이미지 안이다. 기존 남은 적 TMP를 그 이미지 위로 이동하고 숫자만 크게 표시한다. 보라색 안에 표시하던 방어 목표 잔여 수는 숨기며 아래 노란 막대는 기존 방어 목표 게이지로 유지한다. 우상단에는 웨이브 번호만 표시하고 잘못 추가했던 파란 게이지는 제거했다. 남은 적 계산은 미출현 적을 포함한 현재 웨이브의 전체 예정 수에서 처치/통과 수를 차감하는 기존 연결을 유지한다.

Unity Play에서 보라색 원 안에 실제 남은 적 수(테스트 웨이브 1)가 숫자로 표시되고 우상단 파란 게이지가 제거된 것을 확인했다. 검증 후 Play를 종료했다.

## StoreScreen 이미지 적용 취소

사용자 요청으로 StoreScreen.prefab의 이미지·선택 테두리·카드 투명도·Image Type·재화 아이콘 변경을 적용 전 상태로 되돌렸다. StoreScreen은 Git HEAD와 동일함을 확인했다. 인벤토리와 HUD 변경은 유지한다.
