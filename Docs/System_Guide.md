# 던전 홀드 (Dungeon Hold) — 시스템 기획 정리

> **프로젝트 기준:** Orcs Must Die! 3 기반 3인칭 3D 액션 디펜스 · 개발 4주 + 폴리싱 2주. 이 페이지는 전체 개요와 담당별 시스템 기획 색인입니다.
> 

> **표기:** 확정 / 추천 초기값 / 임시 / 제외·후순위는 기존 상태를 유지합니다. 협의가 필요한 기술 규격은 추후 논의하며 임의로 확정하지 않습니다.
> 

# 01. 프로젝트 개요

## 프로젝트 기본 정보

Unity 기반 싱글 플레이 3인칭 액션 디펜스입니다. 로비에서 무기·강화를 준비하고 플레이어·설치물·동료로 웨이브를 방어합니다. 목표는 타이틀/플레이 2개 씬과 최소 3개 스테이지이며, 목표 구성과 실제 연결 완료는 구분합니다.

## 주요 확정 규칙

게임 규칙·스테이지 조건·보상 방향은 [PROJECT_CONTEXT](PROJECT_CONTEXT.md#current_confirmed_scope)에서 관리합니다. 현재 API와 사용 계약은 [구조 문서](ARCHITECTURE.md)가 연결한 시스템 문서를 따릅니다.

## 콘텐츠 규모 요약

수량과 밸런스 초기값은 [콘텐츠 시작값](PROJECT_CONTEXT.md#content_starting_values)을 참조합니다. 이 색인에 별도 수치표를 유지하지 않습니다.

---

# 02. 담당별 시스템 기획

| 담당 | 주 책임 | 참조 |
| --- | --- | --- |
| LJ | 플레이어·동료·전투·성장/경제, 로비·저장·씬 흐름·설정·사운드와 공용 VFX 재생·셋업 | [구조와 시스템 색인](ARCHITECTURE.md), [연계 계약](LJ_INTEGRATION_GUIDE.md) |
| KMB | 설치·적 AI·맵·웨이브·스테이지 판정 | [담당 경계](ARCHITECTURE.md#responsibility_boundaries) |
| LHJ | UI·리소스·화면 연출과 VFX | [UI 연결 계획](UI_PLAN/UI_REMAINING_CONNECTION_PLAN_2026-10-19.md) |

진행 상태와 남은 작업은 [TODO](TODO.md), 완료 이력은 [DONE](DONE.md), 작업 순서는 [ROADMAP](ROADMAP.md#현재-lj-실행-순서)에서 관리합니다. 기능별 API·초기화·종료 계약은 구조 문서가 연결한 시스템 문서를 따릅니다.

입장부터 도전 종료·씬 이동까지의 공통 순서는 [PLAY_FLOW](Systems/PLAY_FLOW.md)를 참조합니다.

---

# 03. 개발 일정

## 개발 일정

개발 4주·폴리싱 2주의 주차별 목표와 현재 실행 순서는 [ROADMAP](ROADMAP.md)에서 관리합니다.

---

# 04. 구현 제외 / 후순위

제외·후순위 기능은 [PROJECT_CONTEXT](PROJECT_CONTEXT.md#deferred_scope)를 따릅니다. 실제 제작 범위 변경은 팀 결정 후 반영합니다.

---

# 05. 추가 결정 항목 및 추천안

추천안은 [PROJECT_CONTEXT](PROJECT_CONTEXT.md#recommended_decisions), 남은 결정은 [TODO](TODO.md#open_decisions)에서 관리합니다. 미확정 기술 규격을 현재 구현 계약으로 취급하지 않습니다.
