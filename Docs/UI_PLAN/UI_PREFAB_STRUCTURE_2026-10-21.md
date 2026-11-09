# UI 프리팹 중첩 구조 정리 — 2026-10-21

## 반영한 구조

`UI-MapTest`가 참조하는 `Canvas_Screens.prefab`의 직계 화면 18개를 각 화면의 작은 프리팹 인스턴스로 연결했다. 원래 연결돼 있던 5개는 유지하고, 일반 오브젝트였던 13개를 중첩 프리팹으로 바꿨다.

- TitleScreen, CampaignModeScreen, DifficultyPlayerScreen, HeroSelectScreen
- MissionSelectLobbyScreen, InventoryScreen, SpellbookScreen, UpgradeDetailScreen
- ResultScreen, ResultStatistics, OptionsHub
- AudioSettings, GraphicsSettings, GamePlaySettings, ControlsSettings
- HelpBookScreen, SessionBrowserScreen, LeaderboardScreen

`Canvas_HUD.prefab`의 PlayerStatus·StageStatus 기존 중첩 연결을 유지하고, Test·CompanionHudBindings도 각각의 작은 프리팹에 연결했다.

이는 화면 조합 구조의 정리이며, 마지막에 진행하기로 한 실제 게임 씬의 HUD 통합 완료를 뜻하지 않는다. 화면들은 원래의 시작 활성 상태를 유지한다.

## 기존 작업 보존

- 큰 프리팹에만 있던 인벤토리 선택 배경·빈 목록 안내, 스크롤 연결 컴포넌트, 강화 표시 컴포넌트, 도움말 Navigator·페이지 버튼, 그래픽 설정 버튼을 작은 프리팹에 보존했다. C# 소스는 변경하지 않았다.
- 통계는 사용자가 작은 ResultStatistics 프리팹에서 수정한 줄바꿈·Scroll Rect·Rect Mask 2D 설정을 사용한다.
- 통계 이외 화면의 기존 배치·표시 차이는 작은 프리팹에 반영해 화면 교체에 따른 배치 후퇴를 줄였다.
- 화면 간 버튼 이동과 다른 화면을 가리키는 참조는 Canvas_Screens의 조합용 Override로 유지한다.
- 기존 Canvas_Screens의 내보낸 로컬 식별값 5,131개를 유지했다. 씬의 Prefab 참조와 버튼 대상이 기존 식별값을 계속 사용할 수 있도록 중첩 소스 참조로 연결했다.
- UI-MapTest 씬 파일과 .meta/GUID는 변경하지 않았다.

## 앞으로 수정할 위치

| 변경 | 수정할 위치 |
| --- | --- |
| 화면 내부 배치·폰트·줄바꿈·이미지·공통 컴포넌트 | 해당 작은 화면 프리팹 |
| 서로 다른 화면 간 이동·연결, 시작 활성 상태 | Canvas_Screens |
| Hero·Wave·설치 컨트롤러·UIControllers 등 씬 대상 연결 | 씬 |

큰 프리팹의 루트 배치와 화면 간 연결에는 의도적인 Override가 남아 있다. 작은 프리팹의 같은 속성 변경이 반영되지 않으면 해당 속성의 상위 Override부터 확인한다. 일괄 Apply All/Revert All 대신 개별 변경을 검토한다.

## 확인 범위와 백업

- 정적 검사: 새 중첩 화면 13개 및 기존 화면 5개의 연결, 수정 프리팹 12개의 로컬 참조·중첩 소스·Override 대상, 기존 내보낸 식별값 보존, 통계 스크롤/마스크 유지, 씬 파일 동일성.
- Unity 재임포트·Inspector·Play·프로젝트 빌드 검증은 수행하지 않았다.
- 교체 전 백업과 작업 목록:
  - `C:/Users/User/.codex/visualizations/2026/10/20/01a1569f-00e2-77a3-bdcc-710a40f998ec/ui-prefab-backup-2026-10-21T01-18-43-695Z`
  - HUD 구조 백업: 같은 경로의 `ui-prefab-backup-2026-10-21T01-18-44-144Z`

Unity에서 통계 열기/닫기, 옵션 하위 화면 왕복, 도움말 페이지 전환, 인벤토리 필터 및 준비 시작 버튼을 확인해야 한다.

## Canvas_Popup · Canvas_Check · Canvas_HUD 추가 확인

- Canvas_Popup의 PopupRoot/MenuTextButton 복사본을 MenuTextButton.prefab의 중첩 인스턴스로 교체했다. 기존 내보낸 식별값 154개를 유지하고, 교체 대상 9개 식별값을 새 인스턴스에 연결했다. 버튼의 배치·시작 활성 상태를 상위 Override로 보존했다. 공용 MenuTextButton 원본은 수정하지 않았다.
- Canvas_Popup의 GuideOverlayRoot·ToastRoot는 이미 작은 프리팹 인스턴스였다. UI-MapTest의 PopupRoot에 추가된 PauseMenuRoot·ConfirmDialogRoot도 이미 작은 프리팹 인스턴스라 유지했다.
- Canvas_Check의 EquipmentSlot·KeyHint, Canvas_HUD의 PlayerStatus·StageStatus·Test·CompanionHudBindings는 이미 중첩 연결되어 있어 추가 교체하지 않았다.
- PopupRoot·DimmedBackground 등 대응하는 독립 프리팹이 없는 컨테이너와 내부 요소는 유지했다. 이름이 Background라는 이유만으로 용도가 다른 Background.prefab으로 교체하지 않았다.
- 이번 변경 파일은 Canvas_Popup.prefab과 이 문서다. 씬·C#·.meta는 수정하지 않았다. 로컬 참조, 기존 식별값, 중첩 소스, Override 대상 및 씬 파일 동일성을 정적으로 확인했다. Unity 재임포트 및 Play 확인은 필요하다.
- 이번 교체 전 백업: `C:/Users/User/.codex/visualizations/2026/10/20/01a1569f-00e2-77a3-bdcc-710a40f998ec/ui-prefab-backup-2026-10-21T01-23-23-001Z`
