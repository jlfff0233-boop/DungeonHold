# UI-MapTest 작은 HUD 프리팹 인스턴스 교체

사용자 요청에 따라 씬의 `PlayerStatus (1)`과 `StageStatus (1)`이 기존 HUD를 대신하도록 참조를 이전했다. 작업 전 씬에 있던 사용자 변경을 포함한 상태를 백업한 뒤, 저장된 씬 파일의 HUD 관련 참조만 변경했다.

## 반영

- 새 프리팹 인스턴스를 `PlayerStatus`, `StageStatus`로 이름 변경했다. 정확한 이름으로 찾는 기존 HUD 코드는 수정하지 않았다.
- 기존 두 HUD 인스턴스는 `UI-MapTest` 씬의 제거 Override로 제외했다. `Canvas_HUD.prefab` 및 작은 프리팹 원본은 수정하지 않았다.
- 플레이어 이름·초상화·HP·에너지, 스킬 아이콘·쿨타임, 콤보·보상 알림, 동료 상태 UI의 기존 참조를 새 인스턴스로 이전했다.
- 씬의 웨이브/타이머/남은 적/목표, 준비·설치 안내 참조와 웨이브 시작 버튼 연결을 이전했다.
- 기존 HUD에 씬에서 추가한 고용/배치 상태 아이콘 6개는 삭제하지 않고 새 PlayerStatus의 같은 슬롯으로 옮겼다.
- 새 작은 프리팹 인스턴스의 배치·크기·이미지·폰트 Override를 유지했다. Hero, Currency, WaveController 등 실제 실행 대상은 변경하지 않았다.

## 검사 및 복구

- 기존 HUD로 향하던 씬 참조 별칭 14개를 교체하고 새 UI를 가리키는 참조 별칭 28개를 생성했다.
- 로컬 식별값 중복 및 누락 참조를 정적으로 검사했다. 이전 대상 외 씬 블록은 동일하거나 해당 HUD 참조 값만 변경되었는지 대조했다.
- 스크립트·공용 프리팹·기존 빌드 설정 346개 파일의 해시를 비교하여 변경되지 않았음을 확인했다.
- Unity 화면 제어 도구 연결이 끊겨 Unity 재임포트/Inspector/Play 검증은 수행하지 못했다.
- Unity에서 씬 파일을 다시 불러온 뒤 표시 및 버튼 동작 확인이 필요하다. 원본 프리팹에 Apply All을 수행할 필요는 없다.

백업 및 이번 작업만의 변경 비교:

`C:/Users/bing8/.codex/visualizations/2026/10/25/01a17270-8cb8-7c12-be6e-d9619ef0aa7c/hud-instance-migration/`

- `UI-MapTest.immediate-before.unity`: 적용 직전 씬 파일. 사용자의 기존 변경 포함.
- `offline-preflight.json`: 이전한 참조·추가 오브젝트 기록.
- `migration-only.diff`: 기존 사용자 변경과 구분한 이번 이전 작업의 변경.
