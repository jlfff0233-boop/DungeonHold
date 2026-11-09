# LJ 구현 기능 연계 정리

현재 구현된 공통 기능의 연결 상태와 실제 적·설치물·스테이지·UI·연출에 남은 작업을 정리합니다. 코드 연결과 실제 플레이 검증은 구분하며, 기능 상세는 각 시스템 문서를 참조합니다.

실전 씬 전환 시에는 [플레이 씬의 책임과 의존 관계](ARCHITECTURE.md#플레이-씬의-책임과-의존-관계)를 참조합니다.

## 현재 연계 상태와 후속 작업

로비 기능 연결은 LobbyController, 플레이 진입점은 PlayScene, 도전 조율은 PlaySession이 담당합니다. TestScene은 PlayScene을 상속한 직접 테스트 진입점입니다. SceneFlow는 입장·맵 준비·종료 후 이동 단계를 조율합니다. 남은 연결은 [TODO](TODO.md#로비-연결검증)를 참조합니다.

아래 로비 후속은 전체 연계 목록이 아닙니다. 나머지 담당별 후속은 다음 절에서 관리합니다. 코드가 연결된 항목은 재구현이 아닌 남은 적용·통합 검증을 구분합니다.

| 범위 | 후속과 상세 계약 |
| --- | --- |
| KMB 전투·스테이지 | 추가 적·보스의 [전투](#에너미보스---전투)·[효과](#에너미보스---상태이상-수신)·[넉백](#에너미---넉백-수신)·애니메이션, 다른 스테이지의 방어 이력·[미션 사건](#연계-작업--kmb)·[도감 대상](#kmb--원본-적과-승리-대상) 연결 |
| KMB 설치·강화 | [강화 지원·감속 결과 적용](#강화-연결), [설치 선택·수명](#kmb-작업-범위--설치-선택해제-연결)과 [소비·환불](#설치철거---재화-연결-kmb-담당) 통합 검증 |
| LHJ 화면 | [HUD 통합](#lhj-통합-후속), [동료 고용·대기 표시](#동료-기획-정합성과-표시-연계), [슬롯](#스테이지ui-후속)·상점·강화·[미션](#연계-작업--lhj)·[도감 결과](#lhj--조회신규-확인결과), [종료·이동](#스테이지-판정과-화면-전환의-현재-경계) 연결 |
| 설정·공통 표현 | KMB 공용 입력과 LHJ [단축키](#단축키-연결)·설정·도움말·[백업 UI](#수동-백업복원-연결), 구현된 LJ [사운드](#공통-관리자와-사운드-연결)·[VFX](#공용-vfx-연결)의 리소스 적용·담당별 표현 연결과 애니메이션 보완 |

### 로비 연결

UI는 LobbyController에 조회/요청을 연결하고 씬 연결부는 로비 사용권 반환 후 플레이를 연결합니다. API·구독/해제·이동 실패 계약은 [로비 시스템](Systems/LOBBY_SYSTEM.md), 담당별 작업은 [TODO](TODO.md#로비-연결검증)를 참조합니다.

#### 로비 레이아웃 대조 — 2026-10-30

사용자가 제공한 스테이지 선택 화면을 현재 LJ API와 대조한 결과입니다. 화면의 샘플 값과 그림만으로 새 게임 규칙을 확정하지 않습니다.

| 화면 항목 | LJ 제공 상태 | 남은 연결 또는 결정 |
| --- | --- | --- |
| 왼쪽 맵 목록과 선택 표시 | Stages, SelectedStage, TrySelectStage, OnSelectionChanged 제공. 현재 stage_1~stage_3 총 3개 | LHJ는 실제 목록 개수로 구성하고 선택 표시 갱신. 레이아웃의 추가 칸을 실제 추가 맵으로 취급하지 않음. 추가 맵은 별도 콘텐츠 결정 |
| 맵 이름과 설명 | LobbyStageInfo.MapId / DisplayName / Description 제공 | 기존 이름·공통 소개로 LHJ 표시 연결. 긴 본문·추가 소개는 요구 확정 시 검토 |
| 썸네일과 큰 이미지 | LobbyStageInfo.Preview 한 장 제공. 현재 3개 모두 미연결 | 이미지 지정과 표시 크기/잘림은 LHJ 연결. 썸네일과 큰 그림을 다른 이미지로 사용할 경우 데이터 분리 필요 여부 결정 |
| 난이도 배지와 원형 선택 버튼 | Difficulties, SelectedDifficulty, TrySelectDifficulty 제공 | 선택 표현 연결. 맵 이름·공통 소개는 기존 데이터 사용, 난이도별 소개는 요구 확정 전 추가하지 않음 |
| 세 개의 클리어 표시 | 선택 맵/난이도의 TryGetBestStars 제공. 현재 최고 별 기록은 0~3 | 원이 별 개수인지 다른 진행 표시인지 확인 후 연결. 난이도별 기록을 합쳐 임의 평가하지 않음 |
| 최고 점수 | TryGetBestScore로 맵/난이도별 클리어 최고 점수 조회 제공 | 미기록과 정상 0점을 구분. LJ 보상 정상 지급 확인 완료, 실제 맵/결과 화면 연결 후속 |
| 등장 적 | TryGetStageEnemies와 실제 구성 제공 계약 구현 | 담당자의 실제 목록 제공과 LHJ 표시 연결. 클리어 시 해당 구성의 적 일괄 해금 규칙 유지 |
| 플레이 방식과 전환 아이콘 | 현재 기획은 싱글 플레이 | 솔로 고정 표시. 다른 플레이 방식 전환을 임의 구현하지 않음 |
| 스테이지 소개 확대와 하단 좌우/페이지 표시 | 설명과 이미지 한 장만 제공. 소개 페이지 목록, 하단 인용 문구, 난이도별 소개 이미지는 없음 | 단순 확대/표현 전환이면 LHJ 처리. 페이지마다 다른 설명/이미지/문구가 필요하면 LJ 데이터 규격과 콘텐츠 작성 범위를 먼저 결정 |
| 책 아이콘 | 로비에 구매/강화/무기 선택/미션/도감 API 제공 | 책 화면의 상점·강화·무기 선택과 하위 화면 복귀 연결. 화면 열기로 로비 전체를 다시 초기화하지 않음 |
| 무기 준비 | SelectedWeapon, TrySelectWeapon, IsWeaponUnlocked 제공. 입장 준비에서 미선택 거절 | 책 화면의 선택·미선택 안내 연결 |
| 전투 시작 | TitleScene.TryEnterStage → SceneFlow.TryEnterStage 제공. 선택 무기 확인/미저장 처리/선택 보관/로비 사용권 반환 후 로드 | LHJ 버튼 요청과 실패 안내 연결. 성공 반환은 이동 접수이며 도전 시작 완료가 아님. 이동 중 중복 입력 차단 |
| 뒤로가기 | SceneFlow.TryCloseLobby에서 미저장 처리 후 정상 해제 제공 | 로비 자체 종료면 저장/해제 성공 후 타이틀 표시. 하위 창 닫기면 로비는 유지. 두 경로를 구분하여 LHJ 연결 |

스테이지 선행 클리어에 따른 잠금 규칙은 현재 로비 목록에 없습니다. 목록에 난이도가 표시된다는 사실과 실제 선택 맵/난이도의 준비 완료는 구분합니다.

#### 로비 담당별 후속 연결

- LJ는 보통 난이도 우선 선택, 같은 선택으로 재진입, 다음 스테이지 조회·이동을 코드에 반영했습니다. 정식 맵·화면을 연결한 뒤 지원 범위, 실패·재시도와 씬 왕복을 검증합니다. [플레이 흐름](Systems/PLAY_FLOW.md#결과-화면-버튼)
- LHJ: 로비 이벤트 구독·최초 조회·재조회·해제, 맵/난이도/최고 별·점수/등장 적 표시. 책 화면의 상점·강화·무기 선택·미션·도감 요청 연결. 시작·뒤로·재시도와 처리 중 입력 차단·실패 안내, 맵 이미지 적용. 결과 UI의 호환 종료 요청·직접 씬 로드를 공통 종료·이동 계약으로 전환.
- KMB 연계: PendingEntry·MapRequestId에 맞춘 맵/난이도 준비와 시작 재화·방어 규칙·동료 기준 위치 전달. IStageEndLink 구현·등록/해제, 신규 사건 생산 중단과 마지막 사건 전달 재시도 연결. 실제 구성의 등장 적 목록 제공. 구현 담당자와 구성 조회 경로는 확정 필요하며 이전 요청의 늦은 결과를 재사용하지 않음.
- 공동 결정: 점수별 보상 정책, 재도전/복귀 결과·재화 폐기 정책과 재시도 주체. 점수 공식·집계·확정은 KMB 담당이며 맵 준비 취소·부분 정리는 별도 계약 확인 필요.
- 공통 이동 계약: GameManager.Scenes의 State / Error / OnChanged로 상태 표시. 실패 단계 재시도와 같은 선택으로 플레이 씬을 다시 여는 TryRetryStage는 [공통 재시도와 씬 이동](Systems/PLAY_FLOW.md#재시도와-씬-이동)을 따릅니다. 로드 실패 후 기존 로비 복구는 TryRestoreLobby를 사용합니다.

다음 스테이지의 KMB 연계는 실제 준비 가능한 맵·난이도를 조회하는 함수를 제공하고, `GameManager`에서 `SceneFlow`를 생성할 때 `mapSupport`로 전달하는 작업입니다. 현재 생성 호출에는 이 인자가 없어 조회·이동 요청이 이동 불가를 반환합니다.

LHJ 결과 화면은 표시 시점과 종료·이동 상태가 바뀔 때 `TryGetNextStage`를 호출해 버튼 활성화 여부를 갱신합니다. 클릭 시 `TryGoToNextStage`를 호출하고, 거절되면 반환된 사유를 표시합니다. [결과 화면 버튼](Systems/PLAY_FLOW.md#결과-화면-버튼)

#### 제공 로그의 확인 범위

로비 열기·닫기·재진입, 선택·구매·강화·도감 확인의 기본 요청 결과를 사용자 확인했습니다. 실제 화면, 저장 실패·재실행 복원과 씬 왕복의 통합 검증은 남아 있습니다.

### 점수·종료·등장 적 연계

LJ 점수 보상·최고 기록 저장과 KMB Stage 1·2 점수 확정·전달이 구현됐습니다. Stage 3은 판정·결과 전달 기반과 테스트 씬이 있습니다. Stage 1은 IStageEndLink를 구현했으며 정식 씬의 등록·해제는 후속입니다. KMBStage1Entry와 KMBLobbyEnemySource는 Stage 1 보통을 지원하며, 로비는 보통을 우선 선택합니다. 다른 맵·난이도는 표시 목록과 실제 준비 범위를 맞춰야 합니다. 맵 준비 연결부는 실제 Stage 1 씬에 없습니다. 타이틀 로비에는 등장 적 제공자가 연결돼 있으며, 선택 맵의 등장 적 목록에 적용할 표시 정책은 미지정입니다.

#### 점수와 종료 순서

결과·별·도감 대상·확정 점수 전달과 전체 완료 조건은 [공통 종료 계약](Systems/PLAY_FLOW.md#최종-종료와-완료-통지)을 따릅니다.

| 담당 | 계약 |
| --- | --- |
| KMB | 점수 기준·팀 처치/방어 집계·최종 점수 확정. 마지막 사건과 미션 처리 후 승리는 CompleteEndEvents(stageId, FinalScore), 패배는 점수 없이 완료 전달 |
| LJ | 전달된 점수로 보상 계산·지급·저장·중복 방지. 점수 공식을 재계산하지 않음 |
| LHJ | 원래 도전의 확정 점수와 실제 지급 보상 표시. 화면 조회로 보상을 지급하지 않음 |

2026-11-02 KMB 전달 기준과 머지된 구현은 다음과 같습니다. 계산의 사용자 테스트 확인은 전달받았으며 LJ 저장·재시도 통합 검증과 구분합니다.

- Stage 1: 클리어 기본 점수 + 팀 처치 수 × 처치 점수 − 통과 수 × 통과 감점. 현재 통과 감점은 100점/마리이며 최종 음수는 0점 처리.
- Stage 2: 클리어 기본 점수 + 팀 처치 수 × 처치 점수 + 최대 방어 점수 × 클리어 잔여 HP 비율. 방어 점수 소수점 버림.
- 수치는 StageScoreData로 KMB가 관리하며 팀 처치는 플레이어·동료·설치물을 포함. 별 평가는 점수와 별개.
- HasFinalScore로 미확정과 정상 0점을 구분하고 FinalScore·FinalKillCount를 제공. 한 번 확정한 점수는 재시도에서 유지하며 계산 실패를 임시 0점으로 대체하지 않음.

Stage 1·2는 마지막 사건·미션 처리 후 승리 점수를 전달하고 패배는 점수 없이 완료합니다. stageId는 맵 번호가 아닌 원래 도전 번호입니다. LJ 보상 정책·구현·검증은 [점수 보상 TODO](TODO.md#클리어-점수-보상-연결)에서 관리합니다.

#### 재시도 책임

[공통 재시도 경로](Systems/PLAY_FLOW.md#재시도와-씬-이동)를 사용합니다. KMB 연결부와 LHJ 화면이 같은 실패 단계를 동시에 재시도하지 않도록 실행 주체를 맞춥니다.

#### 등장 적 제공

LobbyEnemySource.TryGetEnemies(mapId, difficulty, out enemies, out reason)는 실제 맵·난이도 구성의 EnemyData 목록을 반환합니다. 로비 목록을 수동 복제하거나 처치 목록으로 대체하지 않습니다. 정상 빈 목록은 true, 구성 미제공·조회 실패는 false와 사유로 구분합니다.

진행·도감 복원 → 제공자·표시 정책 연결 → 로비 준비 완료 → 선택별 조회 순서입니다. LHJ는 LobbyController.TryGetStageEnemies를 사용하고 LJ 조회부가 중복 제거·도감 매핑·공개 범위를 적용합니다. 조회로 해금·신규 확인·보상·저장을 변경하지 않습니다. HideLocked는 이름·이미지를 가리며 항목 자체를 제거하지 않습니다.

도감은 클리어 시 해당 스테이지 구성의 미해금 적을 일괄 해금합니다. 위 표시 옵션은 클리어 전 로비 목록의 이름·이미지에만 적용하며 도감 해금 조건이나 미해금 상세·스토리 비공개 규칙을 바꾸지 않습니다.

#### 미정과 담당별 연결

- 점수: LJ 보상 설정·정상 지급 확인 완료. LHJ 실제 지급 결과 표시와 실제 맵/화면의 종료·재시도 통합 후속.
- 종료: 플레이 중 재도전·복귀의 결과와 재화 폐기 정책, 마지막 사건 완료 보장 시점, 자연 종료 재시도 주체.
- 등장 적: 맵·난이도별 실제 구성 조회 경로·제공 담당자와 화면 연결. 도감 해금 규칙은 확정 상태 유지.
- 맵 준비: PendingEntry·MapRequestId에 맞는 결과와 TryBeginPreparedStage 연결. 준비 취소·부분 정리는 계약 확인이 필요한 미구현 범위.
- LHJ: 목록·난이도·최고 기록·등장 적, 책 화면의 상점·강화·무기 선택과 시작·뒤로·재시도·실패 안내. 기존 결과 UI의 호환 종료 요청과 직접 씬 로드는 이 계약으로 전환 필요.

### 플레이 씬 연결

정식 플레이 씬의 LJ 실행 구성과 KMB 스테이지·LHJ UI는 같은 PlayScene·Hero·Currency·StageRewardController를 참조합니다. 로비 입장은 SceneFlow의 맵 준비·시작 경로를 사용하고 초기화 완료와 도전 시작을 구분합니다. 카메라 장애물 회피의 레이어 기준은 LJ 카메라와 KMB 맵 Collider 구성에서 일치시킵니다.

WaveController는 PlayScene을 참조합니다. 일부 KMB 스테이지와 LHJ 바인더는 TestScene에 의존합니다. 현재 미션 선택 화면은 Stage 1 씬을 직접 열며, 정식 입장은 SceneFlow의 선택값 인계와 PlayScene 초기화로 연결해야 합니다. 테스트 직접 시작은 별도 경로로 유지합니다.

현재 Stage 1 플레이 씬에는 LJ 실행 구성·스테이지/웨이브·기본 HUD가 함께 배치돼 있으며 같은 도전 조율부를 참조합니다. 저장된 구성 확인과 실제 전체 루프 검증은 구분합니다. 최종 타이틀/플레이 2개 씬과 선택 맵 3개 흐름은 아직 완료되지 않았습니다.

StageScoreData는 `!_Play_Stage1`에 연결됐으며 `UI-MapTest`는 후속입니다. 현재 브랜치의 `!_Play_Stage1`은 판정에 Stage1_Normal_Data(기본형 3마리), 스폰에 UI-TemporaryStage(기본형·탱커 각 1마리)를 사용합니다. 둘 다 1웨이브이며 현재 판정 데이터 기준의 승리 해금 대상은 기본형입니다. [머지 전 HUD 브랜치의 참조 상태](#머지-전-hud-브랜치)와 정식 진행 구성은 구분합니다. [로비 연결·검증](TODO.md#로비-연결검증) 참조.

### 입장·종료 API

도전 시작·구간·결과·실패 조회 API는 [공통 플레이 흐름](Systems/PLAY_FLOW.md)을 기준으로 합니다. 정식 맵 준비와 TestScene 직접 시작을 중복 실행하지 않으며 실제 제공자·화면 연결과 통합 검증은 [로비 후속](TODO.md#로비-연결검증)에 둡니다. 고용·공용 슬롯의 API는 아래 [슬롯 계약](#아이템공용-슬롯-연결)을 따릅니다.

### 저장 진행 연결

저장 관리자는 초기화된 GameManager.Instance.Save로 접근합니다. SaveManager는 공유 서비스와 단일 진행을 소유하며 PlayScene은 준비된 진행으로 모듈을 복원합니다. 플레이 씬마다 서비스를 새로 만들거나 저장소 파일을 직접 수정하지 않습니다. 생성·초기화 수명은 [공통 관리자 구조](ARCHITECTURE.md#공통-관리자-수명)를 참조합니다.

저장 준비·조회·저장·삭제 API는 [저장 시스템](Systems/SAVE_SYSTEM.md#공개-요청과-조회), 씬 연결·최초 조회 수명은 [공통 플레이 흐름](Systems/PLAY_FLOW.md#진행-연결과-씬-초기화)을 따릅니다. 외부 화면은 준비된 공유 진행을 사용하며 씬 연결을 파일 재불러오기로 대체하지 않습니다.

승리의 결과·별·도감 대상과 마지막 사건·확정 점수 전달은 [점수·종료 계약](#점수종료등장-적-연계)을 따릅니다. UI가 사건 전달 완료를 임의로 선언하지 않습니다.

LHJ는 재도전·복귀·실패 안내를 [공통 재시도와 씬 이동](Systems/PLAY_FLOW.md#재시도와-씬-이동)에 연결합니다. UI와 KMB 연결부가 같은 실패 단계를 동시에 재시도하지 않도록 실행 주체를 맞춥니다.

첫 타이틀 초기화에서 영구 진행을 준비합니다. 시작 요청은 확정 진행으로 로비를 열며 파일을 다시 읽지 않습니다. UI 시작 모드에서는 TestScene 자동 시작을 끄고 시작 주체를 하나로 유지합니다. 저장 계약은 [SAVE_SYSTEM.md](Systems/SAVE_SYSTEM.md#초기화), 검증 범위는 [SAVE_TEST.md](Systems/Test/SAVE_TEST.md#첫-타이틀-진행-준비)를 참조합니다.

#### 실행 시 불러오기·웨이브 종료 저장 — 2026-11-04

| 담당 | 연결할 내용 | 현재 구분 |
| --- | --- | --- |
| LJ | 첫 타이틀에서 설정 최초 적용·영구 진행 준비. GameManager의 StartupProgress·StartupProgressResult·StartupProgressError와 TryRetryStartupProgress 제공 | 코드 반영·정적 확인 완료. IsInitialized는 저장 서비스 준비 상태. [Unity 플레이 후속](TODO.md#단일-로컬-저장) |
| LHJ / 초기 화면 | InitialSettingsCompleted와 StartupProgress를 조회해 타이틀 표시·시작 버튼을 제어. Ready 전에는 시작을 막고, Failed이면 실패 사유와 재시도 버튼을 연결 | 화면 연결·검증 후속. 설정 최초 적용 완료는 시도 완료이며, 재시도 후 상태를 다시 조회 |
| LHJ / 타이틀 | 시작 / 설정 / 나가기. 시작은 준비된 진행으로 로비 진입, 설정은 설정 화면, 나가기는 게임 종료 | 기존 로비 시작 요청과 중복 구현하지 않고 실제 버튼·화면에 연결. 스테이지 입장에 처음부터 시작/웨이브 이어하기 선택 화면을 추가하지 않음 |
| KMB / 웨이브 완료 | 일반 웨이브는 PlayScene.TrySaveWave로 저장하고 마지막 웨이브는 최종 정산 사용 | LJ 요청·대기 상태 처리 코드 반영. KMB 완료 신호·다음 웨이브 시작 보류·재시도 연결과 Unity 통합 검증은 후속. 아래 연결 순서 참조 |
| LHJ / 설정 | 설정 안의 백업/불러오기에서 수동 백업 생성·복원과 확인/취소·입력 차단·결과 표시 | 기존 [수동 백업/복원 계약](#수동-백업복원-연결) 재사용. 실행 시 로컬 자동 불러오기와 구분 |

웨이브 종료 저장은 영구 진행만 보존하며 진행 중 웨이브·적·설치물·스테이지 재화의 복원 지점을 만들지 않습니다. 스테이지는 항상 첫 웨이브 준비 단계부터 시작합니다. 도감 최초 해금·보상은 기존 통합 저장/지급을 사용하며 별도 수동 수령이나 추가 지급 경로를 만들지 않습니다.

#### KMB 일반 웨이브 저장 연결 — 2026-11-09

LJ는 PlayScene.TrySaveWave(int stageId, int waveNumber)와 PendingWave를 제공합니다. stageId는 맵 번호가 아닌 원래 도전 번호이며 waveNumber는 1부터 시작합니다. 준비 구간에서 현재 도전의 일반 웨이브를 번호 순서대로 저장합니다. true는 저장 완료 또는 이미 저장한 번호의 재요청이며, false는 요청 거절 또는 저장 실패입니다. PendingWave는 접수 후 저장에 실패한 일반 웨이브 번호이고, 대기가 없으면 0입니다. false인데 PendingWave가 0이면 현재 도전·구간·요청 순서를 먼저 확인합니다. 파일 저장 결과는 기존 SaveManager.LastResult·LastError로 확인하되 요청 거절 시 이전 저장 결과일 수 있습니다.

KMB 연결부의 후속 순서는 다음과 같습니다.

1. 일반 웨이브의 마지막 사망·통과 사건과 미션·처치 재화 전달을 마친 뒤 SetPlayPhase(stageId, false)를 호출합니다.
2. 준비 전환 성공 후 TrySaveWave(stageId, waveNumber)를 호출합니다. OnWaveCompleted의 별도 구독만 추가하는 것으로 끝내지 않고, 저장 결과를 다음 웨이브 진행 조건에 연결합니다.
3. 저장이 완료되기 전에는 다음 웨이브의 준비 시간 진행·조기 시작·전투 시작을 보류합니다. 실패한 원래 도전·웨이브 번호를 유지하고 명시적 재시도에서 같은 함수를 호출합니다. 매 프레임 자동 저장이나 같은 처치 사건 재전달은 하지 않습니다. 기존 파일 쓰기 실패 1회 재시도는 LJ 저장 서비스가 처리합니다.
4. true 확인 후 다음 웨이브 준비를 진행합니다. PendingWave는 실패 후 상태 조회용이며 0이라는 이유만으로 최초 저장 요청을 생략하지 않습니다. 기존 PlayScene.TrySaveProgress로 저장을 복구한 경우에도 PendingWave가 해제되며, 원래 번호를 TrySaveWave에 다시 전달하면 추가 쓰기 없이 완료를 확인할 수 있습니다.
5. 마지막 웨이브는 TrySaveWave와 별도 TrySaveProgress를 호출하지 않습니다. 기존 승패 예약 → 마지막 사건·확정 점수 전달 → 최종 정산 순서를 사용합니다. 한 웨이브로 구성된 개발 데이터도 바로 최종 정산 경로를 사용합니다.

종료 예약 후에는 일반 웨이브 저장 재시도로 돌아가지 않습니다. 마지막 사건 전달이 남았다면 KMB 연결부가 CompleteEndEvents를 재요청하고, 사건 완료 이후 저장·정산 실패는 PlayScene.RetryEndStage, 정산 후 정리 실패는 TryCompleteStageCleanup을 사용합니다. 기존 단계별 중복 지급 방지는 유지하며 전체 정리 성공 시 남은 웨이브 저장 대기를 해제합니다. 이전 상태는 새 도전 준비 성공 시 초기화하고 비활성화·재활성화에서는 유지합니다.

현재 KMB WaveController의 완료 이벤트·코루틴 진행과 Stage 1·2·3 종료 전달 코드는 변경하지 않았습니다. 실제 연결 시 일반/마지막 웨이브 분기, 저장 실패 후 다음 준비·조기 시작 보류, 중복 완료 요청, 마지막 처치의 미션 저장 실패 이후 최종 정산과 단계별 재시도를 Unity에서 확인해야 합니다. LJ Inspector의 저장 검증에는 일반 웨이브 저장·같은 번호 재시도와 대기 번호 표시를 추가했으며 실행 검증은 미수행입니다.

### 공통 관리자와 사운드 연결

공통 접근점은 GameManager.Instance.Save/Sound이며 기존 SaveManager.Instance/SoundManager.Instance는 제거했습니다. GameManager.IsInitialized는 저장 준비 상태이고 사운드 재생은 SoundManager.IsReady로 구분합니다. 하위 시스템은 필요한 서비스를 전달받으며 개별 싱글턴을 다시 만들지 않습니다.

선택 음원과 LJ 무기 이벤트를 적용했습니다. 사용자 확인 범위, API·초기화·수명, [툴 사용법](Systems/SOUND_SYSTEM.md#툴-사용법)과 [테스트·문제 전달 방법](Systems/SOUND_SYSTEM.md#담당자-테스트와-문제-전달)은 사운드 시스템에 모읍니다. 적·UI·설치물은 각 담당자가 실제 호출을 연결하고 검증한 뒤 문제가 있으면 ID와 디버그 기록을 LJ에 전달합니다.

| 담당 | 남은 연결 계약 |
| --- | --- |
| LJ | 공용 재생 API·셋업·SoundDebug와 LJ 대상 연결 제공. 담당자가 전달한 재현 정보로 공용 문제 검토 |
| KMB | ArrowTower는 공용 ProjectileAttackExecutor에 SetSound를 전달하며 실제 출력은 미확인. 적·다른 설치물의 출력 컴포넌트·서비스 전달·행동음 호출은 후속. FireTrapSound는 보조 코드가 있으며 화염 장판의 컴포넌트·SetSound·SetOperating 연결이 필요 |
| LHJ | UI 동작 확정 시 Play(UI용 ID)를 한 번 요청하고 Ui 종류 사용. 준비 전 단발 요청은 거절되므로 최초 표시음은 IsReady/OnReadyChanged와 화면 수명에 맞춰 연결. 실제 게임 정지 시 TrySetGameplayPaused 호출, 복귀·화면 해제 시 복구. 현재 메뉴의 시간 정지와 오디오 정지는 미연결 |

SetSound → Init 순서를 지킵니다. 투사체는 Init 안에서 즉시 발사·충돌할 수 있습니다. 발 디딤·휘두르기 등의 모션 신호와 실제 발사·피해 시점은 구분하며, 실행 코드에서 재생하는 발사·충돌음을 모션 이벤트로 중복 요청하지 않습니다. 모션 교체·전환·복귀 보완은 이후 애니메이션 작업으로 유지합니다.

SceneFlow의 씬 이동은 오디오 정지 복구와 플레이 출력 정리를 수행합니다. 직접 로드로 이 처리를 우회하지 않으며 기존 정산·저장 완료 대기를 유지합니다. 공통 관리자 Shutdown은 씬 이동용 함수가 아닙니다.

현재 PauseMenuController의 시간 정지/복귀와 직접 씬 로드 경로는 LHJ 연계 대상입니다. 메뉴 진입에서 플레이 오디오 정지를 적용한 뒤 UI음을 요청하고, 복귀에서는 오디오 정지 복구와 기존 시간/입력 복구를 함께 처리합니다. 재시도·타이틀 이동은 기존 SceneFlow 요청과 정산 완료 대기를 사용합니다. UI 버튼에서 공용 관리자를 다시 Init/Shutdown하지 않습니다.

적의 SoundPlayer 부착은 자동 행동음 연결을 뜻하지 않습니다. 실제 호출과 이벤트 중 한 경로만 선택하고, 사망 직후 비활성화하는 객체는 관리자 소유 출력으로 사망음을 요청합니다. 반복음은 최초 서비스 연결 후 작동 시작/중단을 전달하며 종료·회수 시 대기 요청도 정리합니다. 이 절은 담당자 연결 요구이며 KMB/LHJ 코드 수정이나 플레이 검증 완료를 뜻하지 않습니다.

### 공용 VFX 연결

LJ 피격·상태 효과와 동료 고용 호출은 연결됐습니다. 툴은 무기=LJ, 적·보스·설치물=KMB로 분류하며 참조 배정과 런타임 호출 연결을 구분합니다. 현재 배정·확인 범위는 [VFX 시스템](Systems/VFX_SYSTEM.md), 남은 작업은 [TODO](TODO.md#vfx-남은-작업)를 따릅니다.

| 담당 | 남은 연결 계약 |
| --- | --- |
| LJ | 기절 공전·전용 기준점과 사망 시 정리 보완 반영. 기본 출력·동료 사망 시 제거 확인, 잔여 표현·수명 검증은 [VFX TODO](TODO.md#vfx-남은-작업) 참조 |
| KMB | ArrowTower는 공용 ProjectileAttackExecutor에 SetVfx를 전달하며 실제 출력은 미확인. 적·다른 설치물의 공용 출력 컴포넌트·서비스 전달·재생 호출과 피격·상태 변경, 사망·통과·비활성화 정리는 후속 |
| LHJ | 리소스·화면 준비 후 UiVfxPlayer.Play 요청. 월드 연출은 VfxPlayer.Init(GameManager.Vfx) 후 Play 요청. 화면·연출 해제에서 자신의 출력만 정리하고 LJ가 이미 요청하는 효과는 중복 재생하지 않음. 적·보스 표현은 담당 캐릭터 연결과 함께 적용 |

실제 일반 적·보스의 상태 효과는 KMB 호출부와 LHJ 리소스·표현 연계 항목입니다. 대상의 상태 적용·면역 규칙은 유지하며, 상태 만료의 방출·공전 중단과 사망 시 잔여 출력 정리는 [VFX 종료 계약](Systems/VFX_SYSTEM.md#종료와-일시정지)을 사용합니다. 툴 배정만으로 호출부가 구현되지는 않습니다.

생성되는 투사체·영역은 SetSound·SetVfx → Init 순서를 사용합니다. 월드 효과의 단발/반복과 위치 고정/추적은 별도 선택이며 VfxManager가 객체 제거를 담당합니다. UI는 화면의 UiVfxPlayer가 생성·제거하므로 자체 파괴와 중복 관리하지 않습니다. 새 VFX 전용 모션 이벤트는 추가하지 않았습니다.

마지막 사건 전달 완료 시 실행 무효화 후 전투 범위의 반복 방출을 중단하고, 새 스테이지·씬 해제에서는 전투 범위 효과를 즉시 정리합니다. 연출 범위는 해당 소유자가 정리하며 씬 언로드·공용 서비스 종료를 넘어 유지되지 않습니다. 전투 기본 출력은 게임 시간, 월드·UI 연출 모듈 기본값은 비배율 시간을 사용합니다.

현재 결과 UI는 정산 완료 전에 열릴 수 있습니다. 승리 연출은 화면 활성화에 자동 연결하지 않고 해당 도전의 승리 결과와 전체 종료 성공을 확인한 뒤 한 번 요청합니다. OnStageEndProcessed의 success는 게임 승패가 아니라 종료 처리 성공 여부입니다. 기존 [정산·이동 순서](Systems/PLAY_FLOW.md#최종-종료와-완료-통지)는 유지합니다.

담당 연결부는 최종 사건 마감 전에 새 실행을 중단합니다. 개별 캐릭터·설치물·화면은 공용 매니저의 Init·Shutdown·StopAll을 호출하지 않으며 자신이 보관한 효과만 정리합니다.

### 볼륨 설정 연결

사용자 볼륨 변경은 `GameManager.Instance.Settings.Volume`의 TryApply 또는 TrySaveAndApply로 요청합니다. Settings·DefaultSettings·LoadError를 조회하고 OnSettingsChanged 구독 후 현재 값을 읽습니다. Settings는 복원값 또는 마지막 적용 성공값이며 실제 출력 준비는 SoundManager.IsReady로 구분합니다.

기존 SoundManager.TrySetVolumes의 공개 검사 계약은 유지하지만 직접 호출은 설정 조회값을 갱신하지 않습니다. 설정 화면·검증 Inspector는 Settings.Volume을 사용합니다. 저장 성공 후 출력 적용이 실패할 수도 있으므로 false를 저장 실패로 단정하지 말고 반환 사유를 표시합니다. 저장·실패·초기화/종료의 상세 계약은 [설정 시스템](Systems/SETTINGS_SYSTEM.md#볼륨-설정)에 둡니다.

### 화면 설정 연결

접근점은 `GameManager.Instance.Settings.Display`입니다. Modes·Resolutions·Settings·DefaultSettings를 조회하고 TryApply 또는 TrySaveAndApply로 요청합니다. 저장 성공 후 화면 요청·접수 설정 반영·OnSettingsChanged 순서이며 실제 화면 전환 완료와 구분합니다. 조회자는 변경 알림 구독 후 현재 값을 읽습니다.

SettingsManager는 GameManager가 생성하므로 추가 씬 배치가 필요하지 않습니다. 기본 변경·저장·Play 재실행 복원은 사용자 확인했고, 실패 경로·수명 회귀·Windows 실제 모드 전환은 남아 있습니다. 상세 계약은 [설정 시스템](Systems/SETTINGS_SYSTEM.md)에 둡니다.

### 단축키 연결

접근점은 GameManager.Instance.Settings.KeyBindings이며 기존 GameManager.Instance.KeyBindings도 유지합니다. LJ 단축키의 기존 저장/재실행 복원·실패 처리는 사용자 확인했으며 공통 파일 처리 변경 후 회귀, KMB 공용 액션의 실제 행동 검증과 설정 UI·HUD 연결은 후속입니다. API와 배정 규칙은 [INPUT_SYSTEM.md](Systems/INPUT_SYSTEM.md)를 참조합니다.

| 담당 | 연결 계약과 현재 상태 |
| --- | --- |
| LJ | 첫 씬 입력 연결 전 최초 복원. 설치·철거·회전·웨이브 시작 액션 등록/단축키 설정 연결과 F/V/R/G 예약 해제 완료. InputHandler는 IsCapturingInput 동안 이동값을 해제하고 요청을 전달하지 않음 |
| LHJ 설정 | 기존 플레이 입력 차단 → 편집 이벤트 구독 → TryBeginEdit 성공 후 최초 조회. 재지정 중 클릭·확정·Esc/T 등 메뉴 입력도 IsCapturingInput에 따라 소비. 정상 닫기/비활성화는 구독 해제 후 CancelEdit |
| LHJ 저장 | TryApply 성공 후 적용 배정 조회/알림으로 표시 갱신. 실패하면 사유 표시·편집값 유지 후 명시적 재시도. 기본값 복원도 저장 후 적용해야 재실행에 유지 |
| LHJ HUD·도움말 | OnBindingsChanged 구독 후 실제 적용 배정 최초 조회, 변경/재활성화 때 재조회. 편집값을 HUD에 먼저 표시하지 않으며 슬롯 인덱스와 배정 키를 분리 |
| KMB | 설치·철거·회전·웨이브 시작의 공용 액션 조회 코드 반영. 실제 재배정·행동 실행과 입력 소비·수명 검증은 후속. 액션 경로/기본 키는 INPUT_SYSTEM 참조. IsCapturingInput 동안 요청 차단, 프리뷰 Update → 실제 설치 LateUpdate와 최신 선택/조건 검사 유지 |

현재 SupportQuickSlotHudBinder의 숫자 키 표시와 PauseMenuController의 T/Esc 직접 입력은 변경 배정·재지정 입력 소비에 미연결입니다. 설정 객체가 UI 액션 전체나 시간/커서를 제어하지 않으므로 기존 화면 소유자가 연결합니다. Inspector는 저장 없는 TryApplyRuntime과 저장 후 적용하는 TryApply를 구분합니다. 단축키는 별도 로컬 파일이며 진행 삭제/백업에 포함하지 않고 종료 시 자동 저장하지 않습니다.

### 상점·해금 후속 안내

ShopProducts는 IShopProduct 공통 목록, GetProductPurchaseState(productId, out price)·TryPurchaseProduct(productId)는 전체 상품 조회/구매입니다. 기존 GetPurchaseState/TryPurchaseItem은 소모품 전용입니다. TrySelectWeapon·SelectedWeapon·IsWeaponUnlocked·IsInstallationUnlocked·OnEquipmentChanged로 선택·보유와 재조회를 제공합니다. 초기/재초기화 조회는 기존 OnInitialized를 사용합니다.

LHJ 구매/선택 요청은 입장 전에만 허용되며 실제 저장 성공 후의 조회·변경 알림으로 화면을 갱신합니다. 최초 무료 무기 선택과 기본 설치물 해금 규칙, 저장 후 상태/알림 순서는 [SHOP_SYSTEM.md](Systems/SHOP_SYSTEM.md#구매-계약)을 따릅니다.

KMB 실제 설치 실행은 TestScene.IsInstallationUnlocked(data.TrapId)로 점유·소비·생성 전에 해금을 확인합니다. 직접 선택/설치 경로의 실제 제한 검증은 후속입니다. LHJ의 ConsumableScreenBinder는 소모품 구매/사용 API를, StartupWeaponShop은 테스트/플레이 씬 시작 시 무기 구매·선택 API를 사용합니다. 이 경로의 구현과 정식 로비 상점 연결은 구분하며 나머지 상품 화면은 공통 목록·조건·보유/선택 API에 연결해야 합니다.

StoreScreen의 검·활·스태프 카드에는 SpellbookCardPreview._confirmationCost=300이 저장돼 있습니다. 확인 시 Currency.TrySpend로 재화만 차감하고 상세를 열며 해금 구매/보유 상태를 기록하지 않습니다. 상세 열람에 반복 비용을 부과하지 않도록 공통 상품 구매·해금 상태와 구분해 수정해야 합니다. 정상 구매 API를 사용하는 StartupWeaponShop과 별개 경로입니다.

남은 검증·담당별 연계는 [TODO](TODO.md#무기설치물-해금-후속)를 참조합니다.

상품 정책·가격·구매/저장 계약은 [SHOP_SYSTEM.md](Systems/SHOP_SYSTEM.md), 저장 버전 정책은 [SAVE_SYSTEM.md](Systems/SAVE_SYSTEM.md#저장-버전-정책)를 참조합니다.

## 로비·씬·설정·사운드·VFX 연계 요구

아래는 담당별 남은 연결 작업입니다. 입장/종료/저장에는 앞 절의 API를 사용합니다.

| 영역 | LJ 제공 / 조율 | 외부 연계와 남은 규격 |
| --- | --- | --- |
| 씬 구성·입장 | SceneFlow의 로드·초기화·맵 준비·시작과 단계별 재시도 기반 구현 | KMB 선택 맵/난이도 준비 결과와 LHJ 시작 요청·로딩 표시 연결. 준비 성공 후 TryBeginPreparedStage 사용. 정식 PlayScene 참조 호환과 전체 입장 검증은 후속 |
| 재도전·복귀 | 종료 접수·정산·정리 후 이동과 로드 실패 재시도 기반 구현 | LHJ 요청은 SceneFlow로 전달하고 실제 이동은 전체 종료 성공 후 수행. 담당자 종료 연결과 화면 표시·통합 검증은 후속이며 로드 실패로 재정산하지 않음 |
| 설정·입력 | SettingsManager와 단축키·화면·볼륨 설정 구현. 화면 기본 검증과 볼륨 저장/재실행 복원·최초 적용 사용자 확인 | LHJ 옵션 화면 전환 코드는 있으나 설정 변경·저장 API 호출은 미확인. 실제 기능 연결과 실패·수명 회귀 후속. KMB 직접 입력은 단축키 절 참조 |
| 사운드 | 공용 재생·설정 도구와 LJ 음원·이벤트 적용, 기본 청취 사용자 확인. [사운드 시스템](Systems/SOUND_SYSTEM.md) 참조 | KMB 적·설치물과 LHJ UI·메뉴 정지/복귀 연결, 담당별 테스트와 추가 출력 수명 검증 |
| 공용 VFX | 서비스·호출 계약과 LJ 배정 현황은 [VFX 시스템](Systems/VFX_SYSTEM.md) 참조 | KMB 적·보스·설치물과 LHJ 화면·연출 연결은 [공용 VFX 연결](#공용-vfx-연결) 참조 |
| 수동 백업/복원 | 타이틀용 SaveManager API와 결과 화면용 SceneFlow API 제공 | LHJ 설정 화면과 타이틀 복원 안내 연결. [수동 백업/복원 연결](#수동-백업복원-연결) 참조 |

공용 씬은 한 번에 한 명이 수정하고 시작/종료를 공유한 뒤 저장·커밋·푸시 후 인계합니다. 코드·개별 프리팹·테스트 씬은 병렬 작업 가능하나 에이전트의 에셋 변경/커밋 권한과는 별개입니다. 상세 책임은 [ARCHITECTURE.md](ARCHITECTURE.md#씬설정사운드vfx-공통-기반), 작업·미결정 목록은 [TODO.md](TODO.md#로비씬설정사운드vfx-공통-기반)를 참조합니다.

### 동료 기획 정합성과 표시 연계

개체 상태와 타입 공통 대기를 구분해 연결합니다.

| 제공 API/이벤트 | 연결 용도 |
| --- | --- |
| 개체 기록 / Health / Mode | 고용 순번·HP·배치/추종 표시 |
| CanHireOrRehire(type, out result) | 타입 슬롯의 요청 가능 여부·불가 사유 |
| GetRehireRemainingTime(type) | 타입 공통 대기 표시 |
| OnEntryChanged | 개체 상태와 해당 타입 표시 재조회 |
| OnHireAvailabilityChanged | 타입 대기 만료·요청 허용 변경 시 재조회 |

LHJ 후속: SupportQuickSlotHudBinder.CompanionState의 자체 인원 제한·재화·재고용 대상 판정을 CanHireOrRehire로 교체하고, 개체 ReadyTime으로 타입 대기를 추정하지 않고 GetRehireRemainingTime을 사용합니다. 현재는 같은 타입 대기 중에도 자리가 남으면 고용 가능으로 표시해 실제 요청과 어긋납니다. HUD가 별도 보관하는 최대 인원 값으로 실행 조건을 판정하지 않습니다.

OnEntryChanged·OnHireAvailabilityChanged와 스테이지 재화 변경 시 해당 타입의 상태를 재조회합니다. 초기 연결·재활성화·재초기화 때도 현재 상태를 조회하고 이전 객체 구독을 해제하며, 비활성화 시 위 구독을 함께 해제합니다. 남은 시간의 화면 갱신은 타입 공통 대기 조회값으로 처리합니다. 사망 연출 종료 시 해당 개체 HUD만 해제하고 재고용 시 새 Health에 연결합니다.

조회 성공은 생성 성공까지 보장하지 않으므로 실제 요청 결과를 최종 판정으로 사용합니다. 개체 수명과 호출 순서는 [동료 시스템](Systems/COMPANION_SYSTEM.md)에 둡니다.

## 공통 공격 실행 계약 변경

검 스킬·궁극기에 맞춰 실행자 계약을 정리했습니다. 상세 규칙은 [공통 전투](Systems/COMBAT_SYSTEM.md)를 참조합니다.

| 기존 사용 | 변경 후 |
| --- | --- |
| 범위 공격용 AttackExecutor 컴포넌트 | AreaAttackExecutor로 교체하고 Attacker/SkillCaster 참조 재연결. AttackExecutor는 추상 부모 |
| ExecuteHit(context)의 즉시 반환값 | Execute(context, onFirstValidHit)로 실행하고 AttackExecution.ReportAppliedDamage로 결과 보고 |
| 개별 실행자의 피해 처리 함수 | ExecuteAttack(AttackExecution) 재정의. 처리 종료 시 Complete() 호출 |
| 스킬 전용 사전 준비 | TryPrepare(SkillData) 설정 준비와 TryPrepareUse(GameObject) 소비 전 사용 준비 |
| SkillCaster.Init(...) | 마지막 선택 인자 AttackType attackType = AttackType.Skill 추가. 궁극기는 Ultimate 전달 |

Attacker의 타격/종료 회차 인자와 DamageInfo, IDamageable.TakeDamage, DamageReceiver의 피해 수신·이벤트 계약은 유지합니다. 현재 적 코드의 피해 수신 호출 인자 변경은 없습니다. 실행자를 직접 사용하는 연결부만 위 변경을 적용합니다.

실행 결과 보고는 모션 종료와 분리됩니다. 직격/관통 화살과 화살비는 이 계약으로 연결되어 기본 동작을 확인했습니다. 일반 취소·사망만으로 생성된 효과를 무효화하지 않으며 비활성화·재초기화·새 스테이지에서는 남은 실행을 정리합니다. 추가 수명 검증은 COMBAT_TODO에서 관리합니다.

이번 원거리 확장으로 기존 피해/공격 호출 API는 바뀌지 않습니다. Inspector에는 원거리 실행자·조준 제공자·발사 기준점·투사체 설정 연결이 필요합니다. 상세는 [무기 시스템](Systems/WEAPON_SYSTEM.md), [테스트 연결](Systems/Test/COMBAT_TEST.md)을 참조합니다.

플레이어는 기본 스킬/궁극기용 SkillCaster를 각각 연결하고 같은 Energy를 공유합니다. 동료는 검사·궁수·마법사별 실행자를 연결해 기본 동작을 사용자 확인했습니다. 다른 프리팹에 적용할 때는 해당 타입의 실행자와 Inspector 참조를 연결합니다.

활·스태프 모션과 스태프 기본 동작, 화살비 표현·궁극기 범위 지정·플레이 화면 표시는 사용자 확인했습니다. 표현용 화살은 별도 피해를 주지 않으며 범위 표시는 기존 조준 정보를 조회합니다. 실제 스테이지 연계와 추가 수명 검증은 후속입니다. 상세는 [SKILL_SYSTEM.md](Systems/SKILL_SYSTEM.md)를 참조합니다.

## 에너미·보스 - 전투

일반/빠른/탱커/비행형 4종 프리팹에 공통 전투·효과·넉백 컴포넌트가 있고 EnemyCombat의 초기화·사망 처리와 EnemyAnimation/EnemyAnimationEvents의 연출 완료 후 제거가 연결돼 있습니다. 아래 계약은 기존 4종의 재구현 목록이 아니며 보스·추가 적 적용과 실제 전투 검증을 구분합니다.

| 연결 위치 | 대상/API | 현재 상태 / 후속 |
| --- | --- | --- |
| 적/보스 루트 | `Health`, `DamageReceiver` | 일반 적 4종 프리팹 반영. 보스·추가 프리팹은 별도 적용 |
| 적 초기화 코드 | `DamageReceiver.Init()`, `Health.Init()` | EnemyCombat에서 같은 Health로 Receiver·효과·넉백 초기화 후 HP 초기화 |
| 적 AI | `DamageReceiver.OnDied` | 기본 적의 AI·공격 중단과 사망 전달 연결 |
| 적 생명주기 코드 | 사망 연출 완료 | 기본 적은 연출 완료 후 제거. 풀 반환은 **구현 시** 연결 |

일반/빠른/탱커/비행형은 EnemyData·능력치·프리팹이 있으며 비행형은 Waypoint 직접 이동 코드가 있습니다. 보스와 콘텐츠별 실제 플레이 확인은 후속입니다.

## 에너미·설치물 - 피해·효과 발동

EnemyAttack·SpikeTrap의 피해, FrostTrap의 감속, ShockTrap의 피해·기절, ArrowTower의 투사체와 FireTrap의 직접 지속 피해·화상은 코드에 연결돼 있습니다. 냉기·충격·화염 장판은 비행 적을 제외합니다. 설치물별 실제 적용·밸런스와 통합 검증은 남아 있습니다.

| 연결 위치 | 대상/API | 현재 연결 / 후속 |
| --- | --- | --- |
| 적·설치물 공격 코드 | 대상의 `TakeDamage(DamageInfo)` | EnemyAttack·설치물 5종의 피해/효과 실행 코드 연결. 실제 판정·효과 검증 |
| 냉기 장판 발동 코드 | 대상 적의 `StatusEffectController.TryApply(SlowEffectInfo)` | 감속 요청 코드 반영, 실제 적용 확인 후속 |
| 충격 장치 발동 코드 | 대상의 `TakeDamage()` 및 `StatusEffectController.TryApply(StatusEffectInfo)` | 피해·기절 요청 코드 반영, 실제 적용 확인 후속 |
| 화염 장판 실행 코드 | `TakeDamage(DamageInfo)`, `StatusEffectController.TryApply(BurnEffectInfo)` | 설치 초기화·직접 지속 피해·화상 요청 연결됨. 실제 통합 검증 후속 |

## 에너미·보스 - 상태이상 수신

기본 적은 EnemyCombat에서 효과를 초기화·갱신하고 EnemyMovement에서 속도 배율을 적용합니다. EnemyReaction/EnemyAttack의 기절 제한도 구현되어 있으며, 보스·추가 적 적용과 콘텐츠별 검증은 후속입니다.

| 연결 위치 | 대상/API | 필요한 작업 |
| --- | --- | --- |
| 적/보스 루트 | `StatusEffectController` | 일반 적 4종 프리팹 부착. 추가 대상 적용은 별도 |
| 적/보스 설정 | `StatusResistanceData` | EnemyData의 저항 참조를 EnemyCombat 초기화에 전달. 콘텐츠별 저항 검증 |
| EnemyMovement 클래스 | `IMoveSpeedModifier` | 구현·속도 배율 적용 코드 반영. 실제 이동 검증 |
| 적 초기화 코드 | `StatusEffectController.Init()` | 같은 Health·DamageReceiver·EnemyMovement·저항 전달 코드 반영 |
| 적 실행 코드 | `UpdateEffects()` | EnemyCombat의 프레임 갱신 연결 |
| 적 AI | `OnEffectChanged`, `IsStunned` | 기절 시 이동·공격 제한/취소와 종료 후 복구 코드 반영. 실제 효과 수명 검증 |

## 에너미 - 넉백 수신

기본 적은 EnemyCombat의 초기화·갱신·비활성화 시 취소, EnemyMovement의 강제 이동, EnemyReaction/EnemyAttack의 행동 제한을 구현했습니다. 아래 계약의 추가 적 적용과 치명타·연출 종료 시점 검증은 후속입니다.

| 연결 위치 | 대상/API | 필요한 작업 |
| --- | --- | --- |
| 넉백 대상 적 루트 | `KnockbackController` | 일반 적 4종 프리팹 부착. 추가 대상 적용은 별도 |
| EnemyMovement 클래스 | `IKnockbackMover` | 강제 이동·충돌 처리 구현. 실제 지형 검증 |
| 적 초기화 코드 | `KnockbackController.Init()` | 같은 Health·DamageReceiver·EnemyMovement 전달 코드 반영 |
| 적 실행 코드 | `UpdateKnockback()` | EnemyCombat 갱신 연결. 종료 프레임까지 일반 이동·회전 생략 |
| 적 AI | `OnKnockbackChanged` | AI·공격 제한/취소와 종료 후 복구 코드 반영 |
| 적 AI 비활성화 코드 | `CancelKnockback()` | 진행 넉백 취소 연결. 풀 반환 경로는 **구현 시** 별도 적용 |
| 적 사망 후 이동 | 치명타 넉백 갱신/제거 시점 | 사망 후 이동을 유지하려면 갱신 주체와 이동 모듈의 활성 유지 필요. 실제 비활성화/풀 반환 시점은 KMB 구현 확인 후 연결 |
| 탱커 설정/수신 코드 | 넉백 거리 보정 | 기획의 넉백 거리 절반 적용 연결 |

## 플레이어 스킬·적 공격 - 넉백 발동

| 연결 위치 | 대상/API | 필요한 작업 |
| --- | --- | --- |
| 넉백을 발생시키는 공격 설정 | `KnockbackData` | 거리·시간 SO 연결 |
| 공격 실행 코드 | 대상의 `KnockbackController.TryApply(KnockbackInfo)` | 발생 정보·방향·거리·시간으로 넉백 요청 |
| 검 기본 스킬 실행 코드 | `SwordSkillExecutor`, `TryApplyFromHit(info, appliedDamage, killedTarget)` | 연결 완료, 생존·즉사 더미 사용자 확인. 피해/사망 결과는 `TakeDamage(info, out killedTarget)`로 받아 즉시 전달 |

## 에너미 - 동료 타겟 탐색

동료 탐색은 Collider의 부모 CombatTarget을 요구합니다. 일반 적 4종 프리팹에는 DamageReceiver와 같은 루트의 CombatTarget이 반영돼 있습니다. 기존 컴포넌트를 다시 추가하지 않으며 실제 적 대상 탐색·접근·공격 검증이 남아 있습니다.

| 연결 위치 | 대상/API | 필요한 작업 |
| --- | --- | --- |
| 적 루트 | `CombatTarget` | 일반 적 4종 부착 확인. 추가 대상만 연결 |
| 적 초기화 코드 | `DamageReceiver.Init()` | 기존 Health·진영 전달. 같은 루트의 CombatTarget이 이를 조회하며 별도 Init은 없음 |
| 적 Collider 계층 | 부모 `CombatTarget`, `DamageReceiver` | 자식 Collider에서도 같은 적 루트의 조회/피해 수신 컴포넌트를 찾도록 구성 |
| 동료 `CompanionTargetDetector` | `LayerMask` | 적 Collider가 붙은 오브젝트의 레이어 포함 |

## 스테이지 - 플레이어 초기화·실패

TestScene.Start는 Init() 후 Auto Begin Stage 설정에 따라 BeginStage를 호출합니다. 끄면 입장 전 구매 후 명시적으로 시작합니다. 공통 PlayScene.Init은 공유 진행 복원 → 조회 객체 준비 → 모듈 초기화 → 사용·구매·Context와 PlaySession 연결 → 구독·요청 허용 순서입니다. PlaySession이 강화 결과와 Hero·도전을 준비하며, 첫 전투는 PlayScene.BeginCombat으로 요청하며 기존 TestScene 호출도 상속으로 유지합니다.

스테이지의 결과·점수 전달과 종료 재시도는 [공통 종료 순서](#점수와-종료-순서)·[재시도 책임](#재시도-책임)을 따릅니다. 기존 도전 번호/abandon 종료 요청은 승리를 뜻하지 않는 호환 경로입니다.

WaveController의 도전 준비 대기·첫 BeginCombat·후속 구간 전환과 처치 재화/미션 전달, Stage1Controller의 명시적 종료·마지막 사건 전달 완료 코드는 연결돼 있습니다. 실제 시작부터 정산/정리까지의 통합 검증과 결과·이동 조율은 남아 있습니다. KMBStageEconomyTest의 별도 초기화를 저장 기반 TestScene과 중복 실행하지 않으며, 동료 BeginCombat은 도전당 한 번으로 웨이브 구간 전환과 구분합니다. 상세는 아래 [미션 계약](#미션-집계-연동-요구)을 따릅니다.

| 연결 위치 | 대상/API | 필요한 작업 |
| --- | --- | --- |
| 로비 입장 연결 | `SceneFlow.TryBeginPreparedStage` | KMB는 선택 맵 준비 결과를 전달. 무기 조회·강화 계산·Hero 초기화는 PlaySession이 수행하므로 외부에서 반복하지 않음 |
| 직접 테스트 시작 | `TestScene.BeginStage` | 기존 테스트 경로 유지. 자원·부활 횟수 초기화도 PlaySession이 수행하며 별도로 호출하지 않음 |
| 스테이지 판정 코드 | 플레이어 `Hero.OnReviveExhausted` | Stage 1·2·3 구독·실패 처리 반영. 마지막 사망 연출 이후 정산 통합 검증 후속 |

## 강화 연결

현재 구현·수명 계약은 [UPGRADE_SYSTEM.md](Systems/UPGRADE_SYSTEM.md)를 참조합니다. 설치 준비도 도전 중이므로 구매할 수 없습니다.

| 담당/연결 위치 | 연결 책임 |
| --- | --- |
| LJ | 강화 데이터·계산·도전 스냅샷·구매 저장과 조회 API 제공 |
| KMB 설치 초기화 | 실제 설치물의 지원 정보·기본 효과 제공, 도전별 확정 결과 적용 |
| LHJ 강화 UI | 로비와 정산·정리 후 결과 화면에서 구매 연결. 대상별 카드·효과·구매 결과 표시 |

조회·구매·미리보기 API는 [강화 시스템](Systems/UPGRADE_SYSTEM.md#조회와-구매-api), 설치물의 지원 정보·효과 적용은 [설치물 강화 계약](Systems/UPGRADE_SYSTEM.md#설치물-강화-계약)을 따릅니다.

LJ 강화 데이터·계산·확정 결과와 CSV 지원, KMB 설치물 5종의 지원 정보·초기화/강화 결과 적용 코드가 반영됐습니다. BuildPlacementController가 같은 TrapData의 확정 결과를 조회해 각 설치물에 전달하며 냉기는 CalculateSlowRate를 사용합니다. 신규 강화 7개의 공용 맵 등록도 완료됐고 실제 구매·저장·새 도전 효과 검증은 남아 있습니다.

LHJ의 UpgradeDetailPreview에는 GetUpgradeState·TryUpgrade 호출과 구매 결과 표시가 있습니다. 저장된 UpgradeUI_Sword·UpgradeUI_Spike는 해당 강화 데이터를 참조하지만, UpgradeUI_Bow·UpgradeUI_Staff는 플레이어 HP/이동속도, 동료 3종 UI는 플레이어 HP를 참조합니다. 카드별 실제 데이터로 교체하고, 효과 표시 분기에 빠진 InstallationSlowUpgradeData의 현재/다음 감속 추가량(%p)을 연결해야 합니다. 카드 외형·구매 API 존재를 올바른 대상 연결 완료로 취급하지 않습니다.

공용 맵 등록은 LJ, 설치물별 가격·효과 콘텐츠와 실제 적용은 KMB가 담당합니다. 등록 완료와 호환성·플레이 검증은 구분하며 남은 항목은 [TODO](TODO.md#강화-연결검증)를 따릅니다. 감속 저항·상한은 [효과 계약](Systems/COMBAT_EFFECT_SYSTEM.md#감속과-기절)을 따릅니다.

강화 데이터/CSV 구성은 [UPGRADE_SYSTEM.md](Systems/UPGRADE_SYSTEM.md#csv-임포트), 남은 작업은 [TODO](TODO.md#강화-연결검증)를 참조합니다.

## 에너미·보스 애니메이션 - 공격

공통 AnimatorHandler는 LJ가 구현·관리합니다. 플레이어·동료 애니메이션과 일부 공용 VFX 실행은 LJ, 적·보스 애니메이션은 KMB가 구성·연결하며 LHJ는 리소스·UI·화면 연출을 연계합니다. 기존 기본 동작과 VFX 확인 범위는 [전투](Systems/COMBAT_SYSTEM.md)·[VFX 시스템](Systems/VFX_SYSTEM.md)을 따릅니다. 일반 적 4종은 EnemyAnimation/EnemyAnimationEvents와 공격 클립의 OnAttackHit·OnAttackEnd로 자체 타이밍이 연결돼 있습니다. 아래 Attacker 계약은 해당 구조를 사용하는 추가 대상에 적용하며 기존 4종의 재구현 요구가 아닙니다. 실제 타이밍·취소 검증은 후속입니다.

| 연결 위치 | 대상/API | 필요한 작업 |
| --- | --- | --- |
| Attacker를 사용하는 적/보스의 애니메이션 연결 코드 | `OnAttackStarted` | 공격 재생 및 시작 때 받은 회차 보관 |
| 해당 공격의 타격/종료 콜백 | `TryCommitHit(id)`, `CompleteAttack(id)` | 원래 회차로 타격·전체 동작 종료 신호 전달 |
| 적/보스 공격 애니메이션 연결 코드 | `OnAttackEnded` | 재생 정리 및 취소된 회차의 남은 신호 폐기 |
| 실제 공격 애니메이션을 연결한 적/보스 | `AttackTimingTest` 사용 시 | 해당 테스트 타이머 비활성화 |
| 자체 공격 구조를 사용하는 적의 타격 콜백 | 대상의 `TakeDamage(DamageInfo)` | 자체 공격 판정 타이밍에 피해 요청 연결 |

## 에너미·보스 애니메이션 - 피격·사망

일반 적 4종은 EnemyAnimation에서 DamageReceiver.OnDamaged와 EnemyCombat.OnDied를 받아 모션을 재생합니다. 피격·사망 클립의 OnDamageEnd·OnDeathEnd와 수신 코드가 반영돼 있으며 사망 연출 완료 후 제거합니다. 보스·추가 적은 같은 생명주기 경계를 맞춰 연결하고 실제 피격/사망·제거 타이밍을 검증합니다.

| 연결 위치 | 대상/API | 필요한 작업 |
| --- | --- | --- |
| 적/보스의 애니메이션 연결 코드 | 해당 `DamageReceiver.OnDamaged`, `OnDied` | 일반 적 4종 연결 반영. 보스·추가 대상 적용과 실제 모션/수명 검증 |

## HUD - 기본 표시와 실제 플레이 연결

`!_Play_Stage1`의 기본 HUD 대상 참조와 준비·웨이브·설치 바인더 추가·연결을 저장된 씬에서 확인했습니다. 무기·공용 슬롯·동적 생성 대상·종료 결과의 전체 연계와 실제 표시 검증을 완료한 상태는 아닙니다.

| 구현 모듈 | 현재 연결 | 후속 |
| --- | --- | --- |
| PlayerHudBinder | 실제 Hero의 Health·Energy와 View 참조 연결 | 게이지·최초 조회·변경 표시 검증 |
| CurrencyHudBinder·ComboHudBinder | 실제 Currency·StageRewardController와 콤보 남은 시간 참조 연결 | 재화·콤보·지급 성공 알림 검증 |
| PlayerSkillHudBinder·PlayerWeaponSkillHudBinder·PlayerLifeHudBinder | 실제 Hero·스킬·체력·부활 설정 참조와 무기별 표시 코드 반영. 기본/궁극기는 CanUseSkill/CanUseUltimate로 구분 | 사용·쿨타임·사망·부활·재활성화 표시 검증 |
| WaveHudBinder·PreparationHudBinder·PlacementHudBinder | StageStatus의 준비·시작·설치 표시 구성 반영. WaveHudBinder는 Stage1Data/Stage1Controller 중심 | Stage 1 표시 검증과 Stage 2 방어 HP/시간 표시 연결 구분. 설치 상세 거절 사유·비용/환불 검증 |
| WaveNoticeHudBinder·WaveSummaryHudBinder | 웨이브 알림·요약 표시 코드와 KMB 맵 테스트 씬 참조 | 스테이지 결과 정산·결과 화면 연계와 구분해 검증 |
| TargetHealthHudBinder·DynamicTargetHudBinder | 정적 대상 표시와 동적 적 탐색·동료 기록별 Health/상태 연결 코드, UIControllers 프리팹 반영 | 기존 자동 연결의 생성·재사용·재고용·제거 수명 검증. 별도 생성 통지 구조를 다시 만들지 않음 |
| PlayerWeaponSkillHudBinder·SupportQuickSlotHudBinder | 무기별 표시·동료/소모품 슬롯과 고용 기록 표시 반영. 고용 가능 여부와 대기 시간은 HUD에서 계산 | 공용 CanHireOrRehire·GetRehireRemainingTime 연결, 변경 단축키 반영과 실제 요청/대기 표시 검증 |
| DynamicTargetHudBinder | 동적 적/동료 HP·상태이상·참조 교체 코드 | 실제 생성·재사용·재고용·종료 수명 검증 |
| StageResultUiBinder | Stage 1 승패·별·관측 통계·정산 상태 표시 코드 | Stage 2 결과, 확정 점수·실제 보상·미션/도감 결과와 공통 종료·이동 연결 |

### Play HUD 자동 연결 도구

PlayHudBindingWindow는 편집 중인 현재 씬의 인스턴스만 처리합니다. `Tools → Play HUD 연결`에서 대상 자동 탐색 또는 직접 지정 → 연결 검사 → 빈 참조 자동 연결 → 결과 확인 후 씬 수동 저장 순서로 사용합니다. 비활성 오브젝트도 탐색하며 Play 모드·프리팹 편집 상태에서는 적용하지 않습니다.

- 기존 참조는 유지하고 빈 참조를 채웁니다. 대상 누락·중복·필드 계약 오류는 적용 전에 보고하며, 다른 대상이 연결된 필드는 충돌로 표시하고 덮어쓰지 않습니다.
- 준비·웨이브·설치 바인더가 없으면 StageStatus에 추가합니다. 준비 시작 버튼은 기존 RequestWaveStart의 대상이 끊긴 경우 그 항목만 복구하고, 같은 호출을 중복 등록하거나 다른 호출을 삭제하지 않습니다.
- 적용은 Undo로 복구할 수 있으며 프리팹 원본·레이아웃·게임 진행 상태와 기존 활성 상태는 변경하지 않습니다. 검사 결과는 창에서 복사하거나 Unity 로그로 확인할 수 있습니다.

### LHJ 통합 후속

| 항목 | 현재 확인과 필요한 연결 |
| --- | --- |
| 화면·바인더 활성 상태 | 기존 화면 중첩·비활성 바인더 보고는 당시 상태입니다. 머지된 HUD/도감 화면의 초기 표시·열기/닫기·구독 수명을 함께 검증합니다. |
| 무기·공용 슬롯 | 표시·설치 수량 집계는 반영됐습니다. 동료 슬롯의 자체 판정 교체는 [동료 표시 계약](#동료-기획-정합성과-표시-연계), 변경 배정 안내는 [단축키 계약](#단축키-연결)을 따릅니다. |
| 동적 체력바 | 위 기본 표시 표의 자동 연결 코드에 대한 실제 맵·정식 PlayScene 수명 검증. |
| 결과·이동 | Stage 1 승패·별·관측 통계·정산 표시 구현과 [구형 종료 요청·직접 씬 이동의 전환](#스테이지-판정과-화면-전환의-현재-경계)을 구분합니다. Stage 2 결과 표시도 후속입니다. |
| 입장 | MissionStageEntry의 Stage 1 직접 로드를 TitleScene/SceneFlow 입장 요청으로 전환해야 합니다. 선택 맵·난이도·무기와 맵 준비를 우회하지 않습니다. |
| 점수·보상 | WaveHudBinder.SetScore의 실제 점수 전달 호출은 미확인입니다. 결과 화면은 점수 영역을 숨기고 관측 처치/콤보 지급을 표시합니다. 확정 점수와 실제 영구 클리어 보상 연결은 별개입니다. |
| 구매·강화 | [상점의 공통 구매 경로와 카드 직접 차감](#상점해금-후속-안내), [강화 카드 참조·감속 표시 보완](#강화-연결)을 따릅니다. |
| 미션 | [미션 표시/요청 계약](#연계-작업--lhj)의 실제 화면 호출이 미확인입니다. MissionStageEntry의 이름을 미션 진행도 구현으로 취급하지 않습니다. |
| 도감 | [도감 조회·신규 확인·결과 계약](#lhj--조회신규-확인결과)을 따릅니다. |
| 대표 이미지 | Sprite 표시와 별도 3D 대표 이미지 생성 도구를 구분합니다. 현재 도구 구현은 확인되지 않았습니다. |

과거 UI 연결 계획과 확인 이력은 [UI 연결 계획](UI_PLAN/UI_REMAINING_CONNECTION_PLAN_2026-10-19.md)을 참조하며 현재 연계 상태는 위 표를 따릅니다. 개별 UI 구현·기존 표시 확인과 실제 플레이 씬의 전체 통합을 구분합니다.

### 머지 전 HUD 브랜치

로컬 `origin/feature/hud-base`의 `77a1d7b6` 기준입니다. 아래 항목은 현재 플레이어 브랜치의 완료 상태와 별도로 기록합니다.

- Stage 1 씬은 판정과 스폰 양쪽에 UI-TemporaryStage를 지정합니다. 임시 구성으로 참조는 일치하며 Stage 1 보통의 정식 데이터 적용·검증은 후속입니다.
- 도감 프리팹에 상세 바인더가 연결돼 스토리·수치·이미지 표시 경로가 있습니다. 출현 장소는 CollectionDisplayData.appearanceLocation의 화면용 문구를 읽습니다. 실제 맵·난이도 구성 기반 조회, 로비 연결, 정상 닫기에서의 신규 확인과 결과 보상 표시는 후속입니다. 신규 확인은 현재 브랜치와 같이 상세를 열 때 요청합니다.
- 새 StoreScreen에는 공통 상품 상태 조회, 플레이 중 적 간략 조회와 슬롯 드래그가 연결돼 있습니다. 새 카탈로그의 공통 구매 요청은 확인되지 않았고, 기존 무기 카드의 직접 차감 경로는 이름이 바뀐 UpgradeAndInventory/InventoryScreen에 남아 있습니다.
- StoreLoadoutShortcutBridge가 설치물 슬롯 재배치에 공용 InputAction의 임시 오버라이드를 사용합니다. 머지 전 재배치 이후 설정 편집·적용·저장을 거쳐도 원래 사용자 배정이 보존되는지 확인합니다. 동료 슬롯의 공용 고용 판정·타입 대기 조회 연결은 이 브랜치에도 남아 있습니다.

### 미니맵 대상 연결

KMB 공통 대상 코드·일반 적 4종의 프리팹 연결과 LJ 플레이어 연결이 반영됐습니다. 적 사망·통과 시 표시 변경은 KMB, 플레이어 기본 표시 상태는 LJ 사용자 확인 범위입니다. LHJ HUD 연결과 실제 맵 통합 검증은 후속입니다. WorldPointHudBinder의 월드 스폰/목표 마커는 미니맵 대상 등록·지도 좌표 변환과 별개입니다.

| 담당 | 연결 범위 |
| --- | --- |
| KMB | 공통 대상 코드, 적 대상 연결, 맵 좌표 기준 제공 |
| LJ | 플레이어 대상 등록과 사망·부활 시 표시 상태 갱신 |
| LHJ | 아이콘 생성, 지도 좌표 변환, 표시와 정리 |

**플레이어 연결** — KMB의 `Assets/02. Scripts/KMB/Minimap`에 있는 `MinimapTarget`과 `MinimapTargetType`을 사용합니다. 이동 기준 Hero 루트에 `MinimapTarget`을 하나 두고 `Target Type = Player`, `Is Visible = true`로 설정합니다. `Position Source`는 기본적으로 비워 루트 위치를 사용합니다. 활성화 시 `ActiveTargets`에 자동 등록되고, 비활성화·제거 시 자동 해제됩니다.

`EnemyMinimapTarget`은 적 전용이므로 플레이어에 추가하지 않습니다. 별도 등록·해제 API 호출은 필요하지 않습니다. `Position Source`는 별도 위치 기준점이 필요한 경우에만 지정하며 애니메이션 본보다 캐릭터 이동 기준 루트를 사용합니다.

Hero.Init은 초기화 중 숨기고 성공 후 표시합니다. HandleDied는 사망 알림 전에 숨기며, Health.OnRevived를 받는 HandleRevived는 실제 HP 복구 후 표시를 복원합니다. OnEnable은 초기화 여부·현재 생명 상태를 반영하고 OnDisable은 표시를 해제합니다. 별도 사망·부활 이벤트 연결은 필요하지 않으며 공개 Hero.OnRevived 이벤트는 없습니다. 같은 오브젝트의 부활도 명시적으로 표시를 복구합니다. `SetVisible`은 미니맵 표시만 변경하며 캐릭터 활성 상태·이동·체력에는 영향을 주지 않습니다. [사망·부활 계약](Systems/COMBAT_SYSTEM.md#플레이어-사망부활-처리)

**HUD 연결** — `MinimapTarget.ActiveTargets`를 조회해 대상 참조당 아이콘 하나를 관리합니다. `TargetType`으로 Player / Enemy / Objective를 구분하고, `WorldPosition`의 X/Z로 위치를 갱신합니다. 목록에 남아 있는 대상도 `IsVisible`이 false면 숨기고, 목록에서 사라지면 아이콘을 제거합니다. 실행 중 추가 스폰과 HUD 재활성화에도 목록을 반영합니다.

HUD가 대상 목록과 프로퍼티를 직접 조회하는 구조입니다. WorldPosition은 조회 시 현재 위치를 반환하므로 LJ가 이동 좌표를 별도로 전달하지 않습니다.

높이 Y는 좌표 변환에서 제외합니다. 사망 연출 중인 적도 목록에 남을 수 있으므로 등록 여부와 표시 여부를 함께 확인합니다. UI는 대상 상태를 변경하지 않고 아이콘만 관리합니다. LJ는 플레이어 대상과 표시 상태만 연결하며 아이콘 생성·좌표 변환은 LHJ가 담당합니다.

**KMBMapTest 좌표 기준** — 바닥 범위는 X −8~24, Z −8~100입니다. 지도 오른쪽은 월드 +Z, 위쪽은 월드 −X입니다. 지도 왼쪽 아래를 원점으로 정규화하면 `u = (worldZ + 8) / 108`, `v = (24 - worldX) / 32`입니다. 기준점은 씬의 `MinimapReferences`를 참조합니다. `Map_Stage1.png`는 실제 맵과 구간별 비율이 달라 기준점에 맞춰 이미지를 보정한 뒤 아이콘 위치를 확인합니다.

| 기준 지점 | MinimapReferences 대상 | 월드 위치 |
| --- | --- | --- |
| A 스폰 | MapRef_SpawnA | (9.6, 0, -4.43) |
| 첫 계단 연결부 | MapRef_EastLanding | (17.8, 1, 28) |
| 가운데 방 중심 | MapRef_Center | (8, 0, 46) |
| 두 번째 계단 연결부 | MapRef_WestLanding | (-5.8, 0.5, 64) |
| A 경로 끝 | MapRef_ExitA | (7.73, 0, 96.72) |

공식은 맵 전체 범위의 좌표 기준이며 기존 이미지의 모든 구간과 일치함을 보장하지 않습니다. 대상 수명과 실제 아이콘 위치의 남은 검증은 [미니맵 TODO](TODO.md#미니맵-연계)에서 관리합니다.

## VFX·UI - 피격·상태이상·넉백

Hero·Companion은 기존 핸들러에서 CharacterVfx에 피격·사망·부활과 상태 변경을 전달합니다. 아래는 외부 표현의 연결 기준이며 같은 사건에 모든 구독을 추가하라는 뜻은 아닙니다. 플레이어·동료의 상태 이상 표현 지원과 실제 적·보스 콘텐츠 적용은 구분합니다.

| 연결 위치 | 대상/API | 필요한 작업 |
| --- | --- | --- |
| 피격·생명 표현 | 표현 대상의 `DamageReceiver.OnDamaged/OnDied`, `Health.OnRevived` | LJ 캐릭터의 파티클 요청은 연결됨. LHJ 화면·재질 연출 및 적·보스 표현 연결 시 중복 재생 구분 |
| 상태이상 VFX/UI 연결 코드 | 효과를 받은 적/보스의 `StatusEffectController.OnEffectChanged` | 현재 효과 유무·남은 시간 조회 후 표현 갱신. 적·보스에 CharacterVfx가 자동 연결되는 것은 아님 |
| 넉백 VFX 연결 코드 | 넉백 대상의 `KnockbackController.OnKnockbackChanged` | 시작·종료 VFX 연결 |
| 각 UI/VFX 연결 코드 | 참조 모듈의 상태·이벤트 | 최초 상태 조회 및 활성 수명에 맞춘 구독·해제 |

## 재화 - 초기화·도전·표시

Currency 기본 모듈 구현과 테스트의 기본 동작은 사용자 확인 완료입니다. 아래는 실제 게임 연결 완료 기록이 아닙니다. 상세 공개 계약은 [CURRENCY_SYSTEM.md](Systems/CURRENCY_SYSTEM.md), 테스트 구성/확인 범위는 [CURRENCY_TEST.md](Systems/Test/CURRENCY_TEST.md)를 참조합니다.

영구 재화 파일 저장 연결은 PlayScene이 담당합니다. 씬 초기화는 `Init(int)` 대신 `Init()`을 사용합니다. 저장 요청은 현재 SaveService 하나만 구독하고 비활성화·재초기화 시 해제합니다. 상세는 [SAVE_SYSTEM.md](Systems/SAVE_SYSTEM.md)를 참조합니다.

소모품 구매는 잔액·수량 통합 저장 후 반영합니다. 일반 소비와 다른 구매 경로는 [SHOP_SYSTEM.md](Systems/SHOP_SYSTEM.md)를 참조합니다.

이 표는 Currency의 기본 계약입니다. 처치·콤보가 포함된 도전의 시작/보상/종료는 아래 StageRewardController를 통해 조율합니다. 설치·고용 등의 소비는 Currency를 직접 사용합니다.

| 연결 위치 | 대상/API | 필요한 작업 |
| --- | --- | --- |
| 신규 진행 생성 코드 | CurrencyData.InitialPermanentCurrency | 최초 영구 재화 300을 새 저장 데이터에 설정. 게임 실행/로비 복귀에서는 재지급하지 않음 |
| 저장 데이터 적용 코드 | Currency.Init(permanentCurrencyBalance) | 새 데이터 또는 불러온 데이터의 영구 재화 잔액 전달, 반환 성공 확인 |
| 도전 시작의 재화 처리 | Currency.BeginStage(initialStageCurrencyBalance), CurrentStageId | 보상 연결부가 호출. 시작값 적용 및 새 도전 번호 생성 |
| 보상 요청의 재화 처리 | Currency.TryAddStageReward(stageId, amount) | 보상 연결부가 원래 도전 번호와 계산된 합계를 전달 |
| 도전 종료의 재화 처리 | Currency.EndStage(stageId) | 보상 연결부가 미지급 처리 후 호출. 영구 재화 잔액 유지 |
| HUD 연결 코드 | GetBalance, OnBalanceChanged | 최초 조회와 재화 잔액 변경 표시. 재화 잔액 알림을 도전 시작/종료 이벤트로 사용하지 않음 |

| 계약 구분 | 연결 책임 |
| --- | --- |
| 저장 적용 | 저장 코드가 새 데이터/불러온 데이터의 영구 재화 잔액을 전달. Init이 최초 300을 추가 지급하지 않음 |
| 도전 번호 | 맵 번호가 아닌 도전별 번호. 시작 성공 후 보관하고 지연 요청에는 원래 번호 사용 |
| 지급/소비 결과 | 호출 코드가 bool 결과를 확인. 실패한 구매/복구/환불을 성공으로 표시하지 않음 |
| 결과 확정 | 마지막 처치/콤보 지급→도전 종료 순서는 상위 연결 코드에서 보장 |
| UI 수명 | 활성 수명에 맞춰 구독한 뒤 현재 값을 최초 조회. 재화 잔액 필드 직접 변경 금지 |

## 처치·콤보 - 보상 연결부

StageRewardController와 더미 보상 검증 코드는 구현했고 기본 동작을 사용자 확인했습니다. 테스트 정리 후 현재 더미의 CombatDummy·선택적 보상 테스트 참조는 수동 연결이 필요합니다. 실제 KMB 처치 재화 전달 코드는 연결됐으며 저장 기반 도전 초기화·미션/최종 종료 통합과 플레이 확인은 후속입니다. 기본 확인 범위는 [CURRENCY_TEST.md](Systems/Test/CURRENCY_TEST.md), 공개 API/미지급 정책은 [COMBO_SYSTEM.md](Systems/COMBO_SYSTEM.md)를 참조합니다.

| 담당/모듈 | 호출 경계 |
| --- | --- |
| 상위 스테이지 연결부 | StageRewardController를 통해 도전 시작·정상 종료·포기 조율 |
| KMB 적 사망 전달부 | 사망 확정 시 원래 도전 번호/생명 식별값/보상액을 RegisterKill에 전달 |
| StageRewardController | Combo 계산과 Currency 지급·도전 수명 조율 |
| Currency | 전달받은 금액의 지급·소비 및 잔액 관리 |
| LHJ 표시 연결부 | 최초 조회와 콤보 변경/지급 완료 알림으로 표시 갱신 |

처치 처리 순서와 지급 실패 시 미지급 요청 유지는 [COMBO_SYSTEM.md의 진행과 지급](Systems/COMBO_SYSTEM.md#진행과-지급--현재-구현)을 참조합니다. 이 문서에서는 담당별 연결 위치를 관리합니다.

| 연결 위치 | API/정보 | 필요한 작업 |
| --- | --- | --- |
| 상위 초기화 | Currency.Init → StageRewardController.Init | 저장 잔액 적용 후 재화 모듈/콤보 SO 전달 |
| 새 도전 | StageRewardController.BeginStage, CurrentStageId | 성공 후 원래 도전 번호 보관 |
| 적 사망 확정 | RegisterKill(stageId, lifeId, killAmount, now) | 원래 도전 번호/도전 내 유일한 생명 식별값/보상액/게임 시간 전달. 접수 성공과 지급 완료 구분 |
| 프레임 갱신 | UpdateCombo(now) | 같은 게임 시간 기준으로 만료 처리 |
| 미지급 재시도 | RetryPendingRewards | 사망을 재접수하지 않고 기존 요청 재시도 |
| 정상 결과 확정 | EndStage(stageId) | 마지막 처치 접수 후 지급/종료 성공 확인. 실패하면 결과 확정 순서 처리 필요 |
| 중도 포기 | AbandonStage(stageId) | 종료 성공 후 미지급 요청/스테이지 재화 폐기 |
| LHJ 표시 | 조회 프로퍼티, OnComboChanged, OnRewardPaid | 최초 상태 조회, 활성 수명에 맞춘 구독/해제 |

현재 KMB 경로는 WaveController가 공용 정적 증가 생명 번호를 발급하고 EnemyCombat.TryInitializeReward에 원래 도전 번호와 함께 전달합니다. EnemyCombat.OnDied(EnemyCombat, DamageInfo)를 받은 WaveController가 등록 해제·팀 처치 판정 후 StageRewardController.RegisterKill로 원래 번호/보상액/시각을 전달합니다. EnemyCombat의 Data·RewardStageId·RewardLifeId·KillReward를 사용하며 실제 통합 플레이 확인은 후속입니다.

EnemyRewardReceiver는 별도 수신 경로를 위한 대안입니다. 현재 KMB 사망 경로에 추가 부착하거나 생명 번호를 다시 발급해 이중 전달하지 않습니다. 미션 처치도 원래 도전·생명 번호와 EnemyData를 재사용합니다. 처치 인정 규칙은 확정됐으며 기믹·소환·재사용 적의 전달과 통합 검증이 남아 있습니다.

검증용 더미에는 CombatDummy와 능력치/저항을 연결합니다. 초기화와 효과/넉백/중력 갱신은 더미 실행부가 맡으며 DamageTargetTest는 선택적 요청/로그 도구입니다. 더미 보상 검증은 EnemyRewardTest와 CurrencyTest.Reward Dummies를 사용하며 실전 수신부와 중복 연결하지 않습니다.

## 아이템·공용 슬롯 연결

### 현재 상태

공용 10칸 슬롯의 LJ 구현과 기본 연결을 사용자 확인했습니다. 설치물은 데이터 기반 선택 요청까지만 처리하며 실제 반영은 KMB, 기존 슬롯 표시 코드는 있으며 새 계약 반영과 통합 검증은 LHJ 후속입니다. 소모품 구매·사용·저장 계약은 유지합니다.

| 담당 | 책임 / 상태 |
| --- | --- |
| LJ | 슬롯 데이터·번호 입력·Context·기존 고용/아이템 연결 구현. 설치물 선택 요청을 BuildPreviewController에 전달 |
| KMB | 선택·해제·공통 진행 제한·설치 비용/실패 복구·철거 환불 코드 반영. 실제 수명/플레이 확인 후속 |
| LHJ | 기존 동료·소모품 표시와 설치물 비용·선택 표시 구현. 타입 공통 대기·변경 단축키 반영과 통합 검증 후속. 공통 실행은 PlayScene.TryExecuteQuickSlot, 고용 결과가 필요한 요청은 TryHireCompanion 사용 |

별도 설치 인터페이스·콜백 없이 기존 BuildPreviewController API로 연결합니다.

### 슬롯 구성과 실행

| 인덱스 | 키 | 데이터 | 동작 |
| --- | --- | --- | --- |
| 0~4 | 1~5 | InstallationQuickSlotData → 기존 TrapData | 실제 종류 선택 요청. 설치/비용 처리와 구분 |
| 5~7 | 6~8 | CompanionQuickSlotData → WeaponType | 검사·궁수·마법사 고용/재고용 |
| 8~9 | 9·0 | ItemQuickSlotData → 기존 ItemData | 체력·에너지 포션 사용 |

PlayScene의 `_quickSlots`에 각 SO를 연결합니다. 빈 슬롯은 허용하며 미할당 설치물 슬롯은 비워둘 수 있습니다. 새 씬에서는 Create 메뉴의 QuickSlot 항목으로 SO를 만들고 표의 인덱스에 할당합니다.

번호 입력 → PlayScene.TryExecuteQuickSlot → 슬롯 SO → QuickSlotContext → 기존 기능 API 순서입니다. 같은 프레임은 설치물 선택 요청·고용 → Z/X 배치 → 소모품 순서이며, CompanionHireInput은 Z/X만 구독합니다. 외부 UI도 PlayScene을 통해 요청합니다.

고용 요청 UI는 PlayScene.TryHireCompanion(weaponType, out hireId, out result)로 요청하고 결과를 표시합니다. 전용 고용 패널은 사용하지 않으며 타입 퀵슬롯의 조회·갱신 계약은 위 [동료 표시 연계](#동료-기획-정합성과-표시-연계)를 따릅니다.

설정 검사는 초기화에서, 씬 활성·도전 준비·종료 예약 확인은 실행 진입점에서 처리합니다. 고용·아이템 규칙은 기존 모듈에 위임하며 SO에 런타임 상태를 저장하지 않습니다. 설치 요청은 실제 선택 API의 결과를 반환하며 false는 진행 조건/데이터 등으로 거절된 상태입니다.

### 슬롯 조회와 재조회

| API | 계약 |
| --- | --- |
| PlayScene.QuickSlotCount / TryGetQuickSlot(index, out slot) | 실제 Inspector 배열 조회. 0부터 시작하며 유효한 빈 칸은 true와 null, 범위 밖은 false. 초기화·도전 상태로 조회를 막지 않음 |
| CompanionQuickSlotData.WeaponType / ItemQuickSlotData.Item / InstallationQuickSlotData.Installation | 각 슬롯의 실제 설정 조회 |
| PlayScene.TryGetCompanionHireData(type, out data) | 기존 데이터 맵으로 고용 비용·대기 설정 조회 |
| HireController.EntryCount / TryGetEntryAt(index, out entry) | 기존 고용 순번·상태·ReadyTime 조회. 대기 기록도 개수에 포함 |
| PlayScene.CanRequestQuickSlot | 씬·도전의 공통 요청 조건. 개별 비용·쿨타임·설치 위치 성공을 보장하지 않음 |
| PlayScene.OnInitialized | 성공한 Init의 기존 인벤토리·강화·미션·도감 변경 알림 이후 발행. 거절·실패에는 미발행 |

슬롯 SO는 설정 조회에 사용하며 실행 상태는 기존 시스템에서 읽습니다. 아이템은 Inventory·GetItemUseState·GetRemainingItemCooldown, 고용은 기존 기록과 OnEntryChanged, 설치물은 BuildPreviewController의 기존 상태를 사용합니다. 별도 상태 복사본은 만들지 않습니다.

#### LHJ 퀵슬롯 HUD 판정 위임 후속 — 2026-11-04

SupportQuickSlotHudBinder는 아래 실행 조건을 재구현하지 않고 기존 시스템의 조회 결과를 표시합니다. 현재 코드의 보완 요구이며 연결 완료를 뜻하지 않습니다.

| 대상 | 현재 보완할 내용 | 연결 기준 |
| --- | --- | --- |
| 공통 요청 조건 | Refresh의 inStage가 씬 활성·초기화·도전 준비·종료·재화 상태를 별도로 조합 | 실행 가능 표시의 공통 조건은 PlayScene.CanRequestQuickSlot 사용. 초기 연결 전 참조 확인과 UI 오브젝트 확인은 유지 |
| 동료 | CompanionState의 인원·재화·개체 대기 판정 복제 | [동료 표시 연계](#동료-기획-정합성과-표시-연계)의 조회 API와 갱신 수명 사용. 고용 기록 순회는 개체 표시용이며 타입 고용 가능 여부를 다시 계산하지 않음 |
| 아이템 | GetItemUseState의 CannotUse를 Hero.CanAct와 아이템 종류로 재해석해 회복 불필요로 추정 | 기존 결과로 사용 불가 표시. 회복 불필요를 구분해야 하면 LJ가 명시적 사유를 제공한 뒤 연결하며 HUD에서 추정하지 않음 |
| 상태 문구 | 계산한 state를 쓰지 않고 State를 항상 숨김 | 문구 표시가 필요한지 정리하고, 숨김을 유지하면 사용하지 않는 사유 문자열 계산 제거. 아이콘 활성 여부는 기존 조회 결과 사용 |

표시용 조회 후 실제 실행 API가 현재 조건을 다시 확인하는 것은 유지합니다. 조회는 실행 성공을 보장하지 않으며, HUD에서 재화 소비·고용·아이템 사용을 선행하거나 입력 실행 경로를 추가하지 않습니다.

화면 활성화 시 OnInitialized를 구독하고 이미 IsInitialized이면 즉시 조회합니다. 초기화 완료 때 교체된 조회 객체를 다시 받아 이전 구독을 해제하고 연결하며, 화면 비활성화 때 구독을 해제합니다. 이 알림은 도전 시작이 아니므로 자동 고용·입장을 수행하지 않습니다. 초기화 실패 시 호출자는 Init의 false와 IsInitialized를 확인하고 이전 화면 참조를 사용하지 않습니다. 도전 시작/종료 후에도 상태를 재조회하며 전체 정리 성공 후 고용 기록이 비워진 상태를 표시합니다.

고용/슬롯 검증 결과와 남은 연계는 [TODO.md](TODO.md#lj-공통-연결부-분리api-보완-계획)에서 관리합니다.

### 도전 수명

전체 시작·정산·정리 순서는 [공통 플레이 흐름](Systems/PLAY_FLOW.md)을 따릅니다. 슬롯 연결에서는 다음 조건을 유지합니다.

- 새 도전 시작·정산 성공·비활성화에서 설치 선택을 해제합니다.
- 종료 예약부터 새 슬롯 요청을 막고 정산 실패 중에도 차단을 유지합니다.
- 재초기화 후 현재 Context·조회 객체를 다시 연결하며 재활성화를 새 도전으로 처리하지 않습니다.

### KMB 작업 범위 — 설치 선택·해제 연결

선택·해제·실행 제한과 재화 연결 코드는 반영돼 있습니다. 실제 씬 연결과 플레이 검증은 남아 있습니다.

| 연결 | 현재 코드 |
| --- | --- |
| QuickSlotContext.TrySelectInstallation(data) | BuildPreviewController.TrySelectInstallation 결과 반환 |
| BuildPreviewController.TrySelectInstallation | 공통 진행 조건·데이터 검사 후 이전 프리뷰를 무효화하고 새 종류 적용. 거절 시 기존 선택 유지 |
| BuildPreviewController.ClearSelection | 프리뷰·선택 데이터·회전 해제. 준비 시간 조건 없이 종료/비활성화에서도 사용 |
| BuildPhaseGate.CanBuild | LJ 씬 활성·도전 준비·종료 없음과 KMB 웨이브 준비 조건을 함께 확인 |
| BuildPlacementController | 직접 F/V 입력 시에도 공통 조건 확인. V는 종류 미선택 상태에서도 대상 철거 가능 |
| PlayScene | 전체 도전 시작 성공·정산 성공·비활성화 시 선택 해제 |

선택 성공은 설치 완료나 비용 지불이 아닙니다. 새 종류를 선택하면 이전 계산을 지우고 다음 프리뷰에서 위치/범위를 계산합니다. 실제 설치는 최신 프리뷰와 거리·공간·점유·재화 조건으로 판단합니다. 번호 선택과 프리뷰 Update/LateUpdate의 순서만으로 유효 결과를 가정하지 않습니다.

종료 예약부터 LJ 슬롯과 BuildPhaseGate가 새 요청을 막습니다. 종료 정산 실패에도 예약을 해제해 플레이로 돌아가지 않습니다. 비활성화 후에는 이전 선택을 복원하지 않으며 새 도전/슬롯 초기화를 반복하지 않습니다. 웨이브 간 선택 유지 등 실제 수명 확인은 후속입니다.

남은 확인은 번호 선택과 같은 프레임 설치, 선택 해제 후 재계산 차단, 미선택 철거, 종료/비활성화 요청 차단, 도전 시작/정산/실패/재활성화입니다. 코드 연결과 실제 사용자 확인을 구분합니다.

### 스테이지·UI 후속

- 첫 웨이브는 LJ 도전 준비와 PlayScene.BeginCombat 성공 후 시작하고, 이후 웨이브는 구간만 전환합니다.
- 최종 결과는 아래 미션 계약의 예약 → 마지막 사건 전달 완료 → 전체 종료 성공 순서로 연결합니다.
- LHJ 동료/소모품 슬롯과 설치물 비용·선택 표시 코드는 있습니다. 타입 공통 재고용 대기·변경 단축키 반영, 설치 상세 실패 사유와 전체 통합 검증은 후속입니다.

## 설치·철거 - 재화 연결 (KMB 담당)

| 항목 | 현재 코드 |
| --- | --- |
| 비용/프리뷰 | TrapData.PlacementCost 사용, 프리뷰에서 소비 가능 여부 확인 |
| 설치 | 최신 배치 조건 확인 → 셀 점유 → 스테이지 재화 소비 → 생성/초기화 |
| 실제 지불 기록 | PlacedInstallation.PaidCost/PaidStageId에 설치 시 비용·원래 도전 보관 |
| 설치 실패 | 생성물 비활성화/제거·셀 해제, 실제 지불했으면 같은 도전의 재화로 전액 복구 |
| 철거 | 같은 도전인지 확인 후 실제 지불 비용과 경제 설정의 RefundPercent로 환불액 계산 |
| 환불 | 소수점 버림, 철거 성공 후 원래 도전 번호로 TryAddStageReward. 실패를 성공으로 표시하지 않음 |

설치 실패의 전액 복구와 정상 철거의 비율 환불은 별개입니다. 생성/철거 도중 진행 상태가 바뀌는 경우를 포함한 실제 연동 검증은 남아 있습니다. Currency가 설치 거래 전체를 자동 보장하는 것은 아니며 현재 KMB 호출 순서와 복구 결과를 사용합니다.

## 스테이지 판정과 화면 전환의 현재 경계

Stage1Controller는 통과 한도·웨이브 종료·별 판정을 담당합니다. 현재 Stage1_Normal_Data·Stage2_Normal_Data는 각 1웨이브의 축소 구성이며 기획 목표 5웨이브와 구분합니다. 원본 적 전달은 각 데이터의 실제 구성을 사용합니다.

Stage 1은 승패·별·통과 기록·도감 대상을, Stage 2는 방어목표 HP·피해 이력·별·도감 대상을 종료 예약에 전달합니다. 두 스테이지는 마지막 사건 처리 후 확정 점수 전달과 정산 결과 구독을 구현했습니다. Stage 3은 제한시간·부활 소진 실패와 결과·점수 전달 코드가 있으며, 마지막 사건 완료 요청 거절 시 로그를 남기는 경로까지 확인했습니다. Stage 3의 해당 단계 재시도 연결과 실제 맵·화면의 전체 정산 검증은 후속입니다.

결과 UI는 여전히 구형 RequestEndStage(stageId, false) 경로를 사용하므로 Stage 1 종료와의 중복 요청·실패 재시도·전체 종료 성공 후 전환을 조율해야 합니다. 구형 호출이나 전체 웨이브 종료 요약만으로 승리·미션/보상 정산 완료를 확정하지 않습니다.

PauseMenuController의 재시도/나가기는 현재 씬을 직접 불러옵니다. LHJ는 결과·일시정지의 이동 요청을 공통 씬 흐름에 연결합니다. 결과의 로비 버튼은 타이틀 씬 안의 로비로 복귀하도록 연결하고, 정산·정리 실패 시 이동을 차단하고 재시도 상태를 유지합니다.

## 미션 집계 연동 요구

LJ 미션 전달 API·사용/사망/참여·종료 조율과 CSV 임포터·초기 SO/Inspector 구성을 반영했습니다. 기본형 처치는 디버그를 통한 달성·보상·저장 복원을 사용자 확인했으며 다른 조건·예외 검증과 KMB 실제 사건 전달·LHJ 화면 연결은 후속입니다. 전체 규칙은 [MISSION_SYSTEM.md](Systems/MISSION_SYSTEM.md), 저장 계약은 [SAVE_SYSTEM.md](Systems/SAVE_SYSTEM.md#미션-통합-저장)에 둡니다.

누적·완료·보상은 난이도별이며 최초 완료마다 레벨 +1입니다. 완전 방어와 스테이지 3 OR 실패 기준은 확정 규칙을 따릅니다. 미션 때문에 필요한 별도 KMB 선행 구현은 없으며 아래 실제 연결 작업을 남깁니다.

### 외부 연계 진입점 — PlayScene

도전·구간·결과 예약·완료·재시도는 [공통 플레이 흐름](Systems/PLAY_FLOW.md)을 따릅니다. 미션 처치는 RegisterMissionKill(MissionKillContext)로 원래 도전 번호·생명 번호·EnemyData를 전달합니다. 처치 원인 필드가 없으므로 KMB가 확정된 인정 규칙으로 판정한 뒤 전달합니다.

MissionStageContext는 도전·맵·난이도·방어 규칙, MissionStageResult는 승패·별·방어 이력과 사유를 제공합니다. 방어 이력 누락을 0으로 간주하거나 현재 HP·별로 복원하지 않습니다.

### LJ 내부 사건과 조회

HeroItemUse.OnUseCommitted는 수량 저장/사용 확정 후 효과 적용 전에, Hero.OnDied는 사망 정보/행동 제한 후 공격 취소 전에 발생합니다. 동료 OnParticipationConfirmed(stageId, hireId)는 고용 표시 전에 기록합니다. 수신부는 기록만 반영하며 준비/플레이를 IsStageReady와 혼동하지 않습니다.

첫 전투의 참여 목록은 전체 준비 성공 후 행동 허용 전에 확정하고 미션 반영 후 표시합니다. 같은 고용 번호의 재고용·웨이브 재진입은 한 명으로 계산합니다. 준비 중 고용은 다음 실제 플레이 진입에서 기록합니다.

| MissionController 제공 | 사용 |
| --- | --- |
| Missions / TryGetState(id, difficulty, out state) | 난이도별 현재/저장 진행·목표·잠금·완료·지급·미저장 |
| ExplorationLevel / OnChanged | 저장 완료 레벨·변경 재조회 |
| OnRewardsCommitted(stageId, rewards, amount) | 저장 성공한 신규 보상 배치 표시 |
| GetStageRewards(stageId) / TryGetStartingProgress(stageId, missionId, out count) | 원래 도전 보상과 시작값 조회, 저장된 현재 진행과 비교해 증가분 표시 |
| HasPendingSave / IsFinalized / StageContext / StageResult | 저장 대기·미션 정산 완료·적용 정보·고정 결과 |
| TrySaveProgress() | 미저장 현재 진행만 재저장, 사건 재접수 아님 |

이 함수들은 조건 판정/보상을 UI로 이전하기 위한 API가 아닙니다. MissionController.BeginStage/ApplyPlayPhase/TryFinalizeStage 등은 PlaySession이 순서를 조율하는 내부 연결이며 실제 스테이지는 위 외부 진입점을 사용합니다.

### 연계 작업 — KMB

1. WaveController는 PlayScene의 IsStageReady를 기다리고 첫 적 생성 전에 BeginCombat 성공을 확인합니다. 선택 맵·난이도·시작 재화 전달과 시작 순서 검증은 후속입니다.
2. 이후 웨이브의 준비/플레이 구간 전환 코드가 연결됐습니다. 동료 BeginCombat은 매 웨이브 반복하지 않으며 준비 중 고용 동료의 다음 플레이 참여를 검증합니다.
3. 실제 적 사망에서 Data·RewardStageId·RewardLifeId를 보관해 미션에 전달합니다. 재사용·소환 적·기믹 처치의 원래 식별과 중복 거절은 실제 검증이 남아 있습니다. 통과·단순 제거·생성 실패는 처치가 아닙니다.
4. Stage 1은 NoPass와 통과 기록, Stage 2는 NoDamage와 실제 방어목표 피해 이력을 전달합니다. 정보 누락을 0으로 간주하거나 회복 후 HP/별로 피해 이력을 복구하지 않습니다.
5. Stage 1·2는 승패·별·사유·도감 대상을 종료 예약하고 마지막 사건 처리 후 확정 점수를 전달합니다. Stage 3도 같은 결과 전달 기반이 있으며 마지막 사건·정산·재시도·결과 UI 통합 검증과 정식 적용은 후속입니다. OnAllWavesCompleted 자체를 승리로 사용하지 않습니다.

KMB는 종료 예약 후 새 사건 생산·입력을 막고, 종료 원인의 사망·통과와 남은 사건 전달을 끝낸 뒤 완료를 통지합니다. 예약 순간 마지막 처치 수를 고정하거나 LateUpdate 순서로 전달 완료를 추정하지 않습니다. 이후 저장·정산·정리와 단계별 재시도는 [공통 종료 계약](#점수종료등장-적-연계)을 따릅니다.

비활성화/전환 시 KMB 직접 입력·코루틴·적 생성도 진행 차단에 포함해야 합니다. PlayScene만 비활성화되면 미션 사건 구독/전달은 유지합니다. 전체 씬 중단은 KMB 새 사건 생산 차단과 별도 조율합니다. 상태 유지 일시정지와 고용/웨이브까지 정리된 중단을 구분하며, 후자는 정산 후 새 도전을 준비합니다. 현재 구조만으로 자동 재개를 보장하지 않습니다.

확인 기준: 최초 준비 실패 시 전투 미시작, 매 웨이브 구간 정확성, 마지막 적/통과/시간 초과 처리, 이전 도전·동일 생명 거절, 기믹/화상/소환 적 집계, 저장/정산 실패 중 진행 차단, 전체 종료 성공 후 전환입니다.

### 연계 작업 — LHJ

현재 미션 진행·완료·보상 조회 API를 사용하는 화면 코드는 확인되지 않았습니다. 아래는 구현된 LJ API에 연결할 계약이며, MissionStageEntry의 Stage 1 직접 입장은 [로비 입장 후속](#lhj-통합-후속)에 해당합니다.

| 표시/요청 | 연결 기준 |
| --- | --- |
| 목록·난이도 필터·잠금 | 미션 데이터와 적용 난이도/선행 상태를 조회. 잠긴 처치 단계도 누적 표시 가능 |
| 진행·완료·지급·레벨 | 확정 상태를 조회. 레벨은 난이도별 최초 완료 기록 수 |
| 즉시 보상 토스트 | 저장 성공한 신규 완료 배치만 표시. 수동 수령 버튼 없음 |
| 복수 단계 | 개별 최초 보상을 합산/연속 표시. 단계 보상과 클리어/별 보상 구분 |
| 결과 화면 | 원래 도전의 진행 변화·신규 완료/보상과 KMB 승패 사유 표시. 실패 결과도 확정 누적 미션 표시 |
| 미저장/종료 대기 | 확정 완료/지급과 구분하여 상태와 명시적 재시도 표시 |
| 이동·재도전 | 전체 종료 성공 이후 허용. 미션 알림만으로 씬/도전 전환하지 않음 |

변경 이벤트 구독 → 현재 상태 최초 조회 → 변경 시 재조회 순서로 연결합니다. 재활성화 시에도 현재 상태를 다시 읽습니다. UI는 조건 판정·상태 직접 변경·보상 지급을 하지 않습니다. 결과/토스트 재조회로 재지급하지 않으며 정산 재시도는 실패한 단계 요청을 사용합니다.

확인 기준: 화면을 늦게 열어도 최신 상태 표시, 동시 단계 보상 누락/중복 없음, 쉬움/보통/어려움 구분, 스테이지별 완전 방어, 실패 결과 요약, 저장 실패를 지급 성공으로 표시하지 않음, 전체 종료 전에 화면 이동하지 않음입니다.

## 도감 연동 요구

도감 외부 연결은 원본 적 목록 전달과 조회·신규 확인 요청입니다. 상세 계약은 [COLLECTION_SYSTEM.md](Systems/COLLECTION_SYSTEM.md), 실제 연결·검증은 [TODO](TODO.md#도감--구현-상태와-후속)를 참조합니다.

### KMB — 원본 적과 승리 대상

- EnemyData·수치 SO·적 동작은 KMB가 관리합니다. LJ 도감과 임포터는 원본을 참조만 하며 적 설정을 변경하지 않습니다.
- 플레이 중 상점의 이름·특징·공격 설명 조회 API는 LJ 구현 완료입니다. KMB의 표시 대상 목록과 전달·갱신 시점은 LHJ 화면 연결과 함께 정해야 합니다. 현재 API는 도감에 등록된 적을 매핑하며 이번 스테이지 포함 여부를 검사하지 않습니다.
- 승리 결과에 해당 스테이지 구성의 EnemyData 목록을 전달합니다. 실제 처치 목록이 아니며 적 생명 번호를 새로 발급할 필요가 없습니다.
- Stage 1·2·3 판정 코드는 전체 웨이브의 원본 적 종류를 중복 제거해 전달합니다. 현재 Stage1_Normal_Data의 승리 해금 대상은 기본형입니다. 스테이지별 실제 구성과 보스 추가 시 도감 매핑을 함께 확인합니다.
- 출현 스테이지 조회용 제공 데이터와 실제 스폰 구성을 맵·난이도별로 맞춥니다. 현재 제공자가 읽는 Stage 1 보통 판정 데이터와 Stage 1 씬의 임시 스폰 데이터에는 차이가 있습니다.
- LJ는 전체 대상을 매핑한 후 종료를 접수합니다. 미등록 적·빈 참조·목록 미제공은 오류이며 대상이 없는 승리는 빈 목록을 전달합니다.
- 패배/포기는 도감 해금 없이 기존 미션 누적 저장을 처리합니다. 종료 순서와 재시도는 위 공통 계약을 사용합니다.

### LHJ — 조회·신규 확인·결과

CollectionUnlockView는 플레이 도감의 잠금·신규 상태를 표시하고 상세를 열 때 열람을 요청합니다. 로비 조회와 정상 닫기 시 열람 처리, 상세·간략 정보와 해금 결과 표시는 후속입니다.

| PlayScene 제공 | 연결 기준 |
| --- | --- |
| Collections.GetEntries / TryGetEntryInfo | 잠금 상태에 맞는 표시 값 조회. 원본 SO에서 잠긴 상세 정보를 직접 읽지 않음 |
| CollectionEntryInfo.Kind | 일반 적·보스 책갈피별 목록 구분. 분류 데이터는 기존 구현 사용 |
| CollectionEntryInfo.BackgroundStory | 해금된 항목의 스토리. 미해금은 빈 값이며 특징·공격 설명과 별도 표시 |
| TryGetEnemyBriefInfo(stageId, enemy, out data) | 현재 도전의 준비/전투 중 등록된 적 조회. 반환 SO에서 이름·특징·공격 설명만 간략 표시하며 로비에는 표시하지 않음 |
| OnCollectionChanged | 구독 후 최초 조회, 변경/재활성화 때 재조회 |
| TryMarkCollectionSeen(source, enemyIds) | 화면을 열 때 조회 객체와 당시 신규 ID를 확보해 정상 닫기에서 전달. 실패 시 확인 완료로 표시하지 않음 |
| OnCollectionUnlocked(result) | 저장 성공한 이번 해금/보상 알림. 직접 재화 지급이나 씬 전환을 실행하지 않음 |
| GetCollectionStageResult(stageId) | 원래 도전의 확정 결과 조회. 다음 도전 시작 성공 전까지 보관 |

간략 조회는 해금·신규·보상·저장을 변경하지 않으며 종료 예약 이후와 이전 도전 번호의 요청은 거절합니다. 상세 화면은 Collections를 사용합니다. 표시 목록은 조회 API가 자동 제공하지 않으므로 KMB 대상 전달과 연결해야 합니다.

LHJ는 로비 도감 상세에서 해금된 적을 열 때 출현 스테이지를 조회합니다. 확인된 맵과 조회 범위를 표시하고, 일부 구성만 확인한 빈 목록은 확인 범위 내 결과로 안내합니다. 호출 계약은 [도감 시스템](Systems/COLLECTION_SYSTEM.md#모듈과-공개-조회)을 따릅니다.

단순 비활성화·씬 이동은 신규 확인으로 처리하지 않습니다. 화면이 열린 뒤 해금된 다른 항목은 확인 대상에 합치지 않고, 저장 실패 시 신규 표시를 유지합니다. 이전 진행 객체의 닫기 요청은 거절합니다. 대표 이미지·실루엣·실제 화면 연결은 후속입니다.

결과 UI는 원래 도전 번호의 GetCollectionStageResult로 신규 해금 ID와 도감 보상을 표시하며 재화 잔액 차이로 추정하지 않습니다. 저장 성공 알림에서 보상을 재지급하지 않고 전체 종료 성공 후 이동합니다. 결과는 다음 도전 시작 성공 전까지 보관됩니다. 현재 StageResultUiBinder의 도감 결과 연결은 미구현이며 구형 종료 요청과의 조율도 남아 있습니다.

## 수동 백업/복원 연결

타이틀 설정은 `SaveManager`, 정산·정리를 마친 결과 설정은 `SceneFlow`에 연결합니다. LHJ 화면 연결과 [정식 플레이 씬 진입·종료 연계](#플레이-씬-연결)가 남아 있습니다. KMB의 기존 맵 준비·종료 연결은 결과 경로 검증의 선행 조건입니다. 활성 도전 중 일시정지 복원은 [도전 처리 정책](TODO.md#open_decisions) 확정 후 연결합니다.

| LHJ 연결 지점 | 호출과 표시 |
| --- | --- |
| 타이틀 설정 | 백업 전 `SaveManager.TryLoadProgress()`로 기존 진행을 준비합니다. `PrepareBackupAsync()`에서 `ConfirmationRequired`를 받으면 `ConfirmBackupAsync()` 또는 `CancelPreparedBackup()`을 호출합니다. 로컬 덮어쓰기 확인 후 `RestoreProgressAsync()`를 호출하고 성공 안내를 표시합니다. |
| 결과 설정 | `SceneFlow.CanUseResultBackup`으로 버튼을 제어합니다. `PrepareResultBackupAsync()`에서 `ConfirmationRequired`를 받으면 `ConfirmResultBackupAsync()` 또는 `CancelResultBackup()`을 호출합니다. 로컬 덮어쓰기 확인 후 `RestoreResultProgressAsync()`를 호출하면 성공 시 `SceneFlow`가 타이틀 이동을 예약합니다. |
| 타이틀 복원 안내 | 타이틀 연결과 로드 완료 후 `SceneFlow.TryConsumeRestoreNotice()`가 `true`일 때 `데이터를 불러왔습니다`를 한 번 표시합니다. |
| 진행·결과 표시 | `SaveManager.BackupPhase`·`IsBackupPending`으로 진행/확인 상태를 표시하고 처리 중 입력을 차단합니다. 결과 코드로 안내하고 `UpdatedAt`으로 백업 시각을 표시합니다. `Detail`은 개발자 확인용입니다. |

복원 전 보관본의 수동 복구와 요청 중 사용권 처리는 [저장 계약](Systems/SAVE_SYSTEM.md#firebase-수동-백업복원)을 따릅니다.

설정/검증 방법은 [SAVE_TEST](Systems/Test/SAVE_TEST.md#firebase-설정과-화면-연결), 전체 계약은 [SAVE_SYSTEM](Systems/SAVE_SYSTEM.md#firebase-수동-백업복원), 미완료 작업과 2차는 [TODO](TODO.md#firebase-수동-백업복원)에 둡니다.

## 후속 구현·협의

| 항목 | 필요한 내용 |
| --- | --- |
| 적 처치/종료 연결 | 공용 처치·구간과 Stage 1·2 결과·확정 점수, Stage 2 방어 피해 이력과 Stage 3 결과 전달 기반 반영. 마지막 사건·재시도·결과/이동 통합 검증 후속 |
| 피해·효과 연결 | 피해 적용 여부에 따른 효과 발동 조건 및 처리 순서 |
| 넉백 콘텐츠 | 플레이어·동료에게 넉백을 주는 적 공격 종류·발동 조건 |
| 비행형·보스 넉백 | 비행 이동 처리/복귀 방식 및 보스 넉백 정책 |
| 동료 타겟 보완 | PlayScene.BeginStage의 companionPriorityPosition으로 스테이지2 방어 목표 위치 전달. PlaySession·고용 관리자·동료까지 경로 구현, 실제 목표 연결은 후속 |
| 적/보스 Root Motion | KMB가 EnemyMovement와 애니메이션의 이동·회전 제어 범위를 조율 |
| 넉백 표현 | 방향별 모션/VFX와 진행 중 재적용 표현에 필요한 정보 |

## 상세 문서

- [공통 전투](Systems/COMBAT_SYSTEM.md)
- [상태이상·효과](Systems/COMBAT_EFFECT_SYSTEM.md)
- [에너지·스킬](Systems/SKILL_SYSTEM.md)
- [동료](Systems/COMPANION_SYSTEM.md)
- [아이템](Systems/ITEM_SYSTEM.md)
- [상점](Systems/SHOP_SYSTEM.md)
- [저장](Systems/SAVE_SYSTEM.md)
- [단축키·입력](Systems/INPUT_SYSTEM.md)
- [도감](Systems/COLLECTION_SYSTEM.md)
- Notion 공통 작업 경계
- Notion 플레이어·동료·성장/경제
- Notion 설치·적 AI·스테이지

기획상 화상·감속·기절의 실제 적용 대상은 일반 적·보스입니다. 플레이어·동료의 효과 테스트 연결과 실제 콘텐츠 적용 범위를 구분합니다. 로컬 상세 문서 중 최신 화상·넉백 코드 및 Notion의 애니메이션 담당 경계가 반영되지 않은 내용은 현재 코드와 위 기획을 함께 확인합니다.
