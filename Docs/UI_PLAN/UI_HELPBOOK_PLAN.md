# 도움말 제작 기준 — 2026-10-16

## 사용자 확정 기준

- 기존 PageArea, AbilitiesPage, ControlsPage와 이미 작성한 페이지는 보존한다. 참고 자료가 늘었다는 이유로 재제작하거나 삭제하지 않는다.
- ControlsSettings는 키를 변경하는 설정 화면, ControlsPage는 기존 도움말 조작 목록이다. 둘을 혼동하지 않는다.
- 신규 도움말은 큰 상황 이미지 위에 노란 제목, 짙은 설명 배경, 청록색 테두리, 흰 본문을 겹쳐 배치한다. 번호와 별도 범례 목록으로 대체하지 않는다.
- 현재는 페이지/이미지 자리/설명 상자/문구만 제작한다. Arrow는 OFF로 보관한다. 실제 우리 게임 스크린샷을 넣은 다음 상자와 화살표의 위치·방향을 정한다. 스크린샷 전 좌표는 시작값이며 최종값이 아니다.
- 현재 Arrow는 빈 부모 아래 Shaft/LeftHead/RightHead Image 3개를 조합한 구조다. 단일 Sprite나 TMP라고 가정하지 않고 저장 파일의 실제 계층을 확인한다.
- 참고 이미지는 분석 자료다. 참고 게임의 배경과 글자가 포함된 이미지를 우리 게임의 최종 도움말 콘텐츠로 취급하지 않는다.
- 뒤로/이전/다음/페이지 점은 공통으로 유지한다. 단계별 튜토리얼의 다음·계속과 도움말 책의 페이지 이동은 별개다.
- 14개 참고 이미지는 14개 실제 게임 기능이나 14개 확정 페이지를 뜻하지 않는다. 최종 페이지 수를 확인한 후 Dot 개수와 순서를 정리한다.
- 전체 UI 외형 제작 후 버튼·데이터·화면 전환을 연결하며, FX는 전체 UI 외형 제작 후 진행한다.

## 이미지 분석 및 저장 목록

원본 14장과 대화 중 재확인한 참고 4장을 [HelpBook_2026-10-16](UI_References/HelpBook_2026-10-16)에 저장했다. 크기/내용을 편집하지 않았고 원본과 SHA-256 일치를 확인했다. 개발 중 Unity 화면 캡처는 이 참고 묶음에 포함하지 않는다.

| 파일 | 주제와 재사용할 구성 | 우리 프로젝트 반영 |
| --- | --- | --- |
| [01](UI_References/HelpBook_2026-10-16/01_HudOverview.png) | 게임 화면 위 목표·화폐·슬롯 Callout과 화살표 | 실제 HUD 배치와 스테이지별 목표/시간 규칙에 맞춤 |
| [02](UI_References/HelpBook_2026-10-16/02_Placement.png) | 설치 미리보기 화면과 선택/설치 설명 | F 설치, G 시작, 설치물 슬롯 1~5 |
| [03](UI_References/HelpBook_2026-10-16/03_Abilities.png) | 좌우 상황 이미지와 각 설명 상자 | 기존 AbilitiesPage 보존, Q/E 설명 적용 |
| [04](UI_References/HelpBook_2026-10-16/04_KeyboardMouse.png) | 키보드/마우스 도해, 키 강조, 연결선 | 기존 ControlsPage 목록 보존. 시각 도해는 추가 후보 |
| [05](UI_References/HelpBook_2026-10-16/05_Minimap.png) | 확대 지도와 아이콘 범례/연결선 | 실제 지도·아이콘 규격 확인 후 추가 |
| [06](UI_References/HelpBook_2026-10-16/06_Barricade.png) | 상황 이미지+장비 확대+하단 설명 | 바리케이드/경로 변경은 현 기획 제외. 기능 설명 제작 보류, 레이아웃 참고 보존 |
| [07](UI_References/HelpBook_2026-10-16/07_Spellbook.png) | 필터·강화·영구 재화 Callout | SpellbookPage. 강화 상세 페이지와 구분 |
| [08](UI_References/HelpBook_2026-10-16/08_Upgrade.png) | 강화 상세 화면의 구매·마일스톤 Callout | UpgradeHelpPage |
| [09](UI_References/HelpBook_2026-10-16/09_Inventory.png) | 적/아이템 정보·선택·슬롯 구성 Callout | InventoryHelpPage. 전투 중 무기 교체로 설명하지 않음 |
| [10](UI_References/HelpBook_2026-10-16/10_Endless.png) | 큰 게임 이미지+장문 설명 | 모드 상세 규칙 필요. 지원 기능으로 임의 확정하지 않음 |
| [11](UI_References/HelpBook_2026-10-16/11_Scramble.png) | 로비 이미지 위 다중 Callout | 모드 상세 규칙 필요. 참고 보존 |
| [12](UI_References/HelpBook_2026-10-16/12_StatusA.png) | 상태 효과 3열×2행 이미지 카드 | 현재 기획의 감속·기절·화상만 우선 반영 |
| [13](UI_References/HelpBook_2026-10-16/13_StatusB.png) | 상태 효과 3열×2행 이미지 카드 | 원본 효과를 신규 기능으로 추가하지 않음 |
| [14](UI_References/HelpBook_2026-10-16/14_StatusC.png) | 상태 효과 세로 카드 3개 | 현재 효과 수에 맞게 레이아웃 참고 |
| [15](UI_References/HelpBook_2026-10-16/15_AbilitiesBookFrame.png) | 능력 페이지의 뒤로·Dot·이전/다음 포함 | 공통 책 프레임 참고 |
| [16](UI_References/HelpBook_2026-10-16/16_SpellbookConfirmed.png) | 마법책 목록 재확인 | 사용자가 확인한 SpellbookPage 참고 |
| [17](UI_References/HelpBook_2026-10-16/17_UpgradeConfirmed.png) | 강화 상세 재확인, 계속 포함 | 사용자가 확인한 UpgradeHelpPage 참고. 계속은 책 이동과 분리 |
| [18](UI_References/HelpBook_2026-10-16/18_InventoryConfirmed.png) | 인벤토리 재확인 | 사용자가 확인한 InventoryHelpPage 참고 |

## 콘텐츠 기준

사용자가 제공한 최신 단축키 표를 사용한다. WASD 이동, Q 스킬, E 궁극기, 준비 단계 F 설치/G 게임 시작, Z/X 고용된 첫째/둘째 동료 배치·재배치, T 정보 패널, Esc 메뉴/뒤로. 공용 슬롯은 왼쪽부터 설치물 1~5, 동료 종류 6~8, 소모 아이템 9·0이며 무기는 입장 전 고정이다. 동료 전용 스킬/궁극기는 없다. 동료 배치·재배치는 준비 및 웨이브 중 가능하다. 참고 게임의 키 표시는 이 규칙을 덮어쓰지 않는다.

## 앞으로 마감할 순서

1. 작성 중인 InventoryHelpPage의 이미지 자리와 Callout 구조/문구를 완성한다. Arrow는 OFF 유지.
2. 동료 고용·배치 도움말 누락 여부 확인 후 필요 페이지 추가. 기존 페이지는 보존.
3. 실제 씬의 페이지 목록과 빈 예약 페이지를 확인하고 표시 순서/단일 활성 상태/Dot 수를 정리한다. 파일 검수와 Unity 표시 검수를 구분한다.
4. 상태이상 페이지와 도움말 전체 프리팹 저장을 확인한다. 데이터 연결이나 스크린샷 최종 배치 완료로 기록하지 않는다.
5. 우리 게임 스크린샷 준비 후 삽입→비율 확인→설명 상자 이동→Arrow 활성화 및 방향/끝점 조정→두 해상도 검수.

이 문서는 기존 UI_WORK_PLAN의 도움말 7페이지 고정과 이전 대화의 번호·범례형 신규 배치 안내보다 우선한다. 기존 ControlsPage를 목록으로 보존하는 사용자 결정은 유지한다.
