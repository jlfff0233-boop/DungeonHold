# SETTINGS_SYSTEM

LJ 환경설정은 SettingsManager가 소유합니다. 단축키·화면 모드/해상도·마스터/BGM/SFX 볼륨 설정을 구현했습니다. 화면 기본 변경·저장·Play 재실행 복원과 볼륨 저장·Play 재실행 복원·최초 믹서 적용을 사용자 확인했습니다.

## 구성과 책임

| 구성 | 책임 |
| --- | --- |
| GameManager | SettingsManager 생성, 초기화·시작·갱신·종료 조율 |
| SettingsManager | 단축키·화면·볼륨 설정 소유와 수명 관리 |
| KeyBindingSettings / KeyBindingStorage | 단축키 편집·적용과 직렬화. 상세는 [입력 시스템](INPUT_SYSTEM.md) 참조 |
| DisplaySetting / DisplaySettings | 화면 설정값 / 지원 목록·기본값·복원·변경 요청 |
| DisplaySettingsStorage | 화면 설정 직렬화와 읽기 연결 |
| VolumeSetting / VolumeSettings | 세 볼륨 설정값 / 기본값·복원·저장과 출력 적용 조율 |
| VolumeSettingsStorage | 볼륨 설정 직렬화와 읽기 연결 |
| LocalSettingsFile / SaveJsonReader | 공통 설정 파일 읽기·교체 / 원문 구조 해석 |

설정 객체는 일반 C# 객체이며 GameManager가 생성합니다. 추가 씬 배치가 필요하지 않습니다. SoundManager는 실제 출력과 볼륨 적용을 담당하며 설정 저장을 소유하지 않습니다.

캐릭터·위치 출력의 호출·수명과 LJ 대상의 청취 확인 범위는 [사운드 시스템](SOUND_SYSTEM.md)에 둡니다. 출력 확인과 볼륨 설정의 저장·복원 검증은 구분합니다.

## 화면 설정

- 지원 모드는 `ExclusiveFullScreen`, `FullScreenWindow`, `Windowed`이며 표시도 enum 이름을 사용합니다. `MaximizedWindow`는 현재 지원 목록에 없습니다.
- 해상도 목록은 실행 환경에서 수집하고 가로·세로 크기가 같은 항목은 합칩니다. 주사율은 별도 설정 항목이 아닙니다.
- 기본값은 현재 모니터 해상도와 FullScreenWindow입니다. 전체 창의 선택 해상도는 렌더링 크기이며 모니터 출력 크기와 구분합니다.
- 선택 중인 값은 호출부가 보관하고 적용 요청 때 전달합니다. 기본값 조회만으로 화면이나 파일이 바뀌지 않습니다.

## 조회와 요청

접근점은 `GameManager.Instance.Settings.Display`입니다. 단축키는 `Settings.KeyBindings`로 접근하며 기존 `GameManager.Instance.KeyBindings`도 유지합니다.

| API | 계약 |
| --- | --- |
| Modes / Resolutions | 지원 모드·해상도 목록 |
| Settings / DefaultSettings | 마지막 접수 설정 / 현재 환경의 기본값 |
| TryApply(setting, out reason) | 저장 없이 이번 실행의 화면 변경 요청 |
| TrySaveAndApply(setting, out reason) | 파일 저장 성공 후 화면 변경 요청 |
| OnSettingsChanged | 접수 설정 반영 이후 변경 알림. 실제 화면 전환 완료 이벤트가 아님 |
| LoadError | 최초 복원 실패 사유 |

화면 조회자는 변경 알림을 구독한 뒤 현재 설정을 최초 조회합니다. 실제 화면 상태가 필요하면 Screen의 현재 모드·크기를 조회하며, 요청과 같은 프레임에 전환 완료를 가정하지 않습니다.

## 볼륨 설정

접근점은 `GameManager.Instance.Settings.Volume`입니다. 마스터/BGM/SFX는 각각 0~1이며 기본값은 모두 1입니다. 편집 중 선택값은 호출부가 보관하고 기본값 선택만으로 출력이나 파일을 바꾸지 않습니다.

| API | 계약 |
| --- | --- |
| Settings / DefaultSettings | 복원값 또는 마지막 적용 성공값 / 기본값 |
| Init(out reason) | 기본값 준비와 파일 복원. 출력은 아직 변경하지 않음 |
| ApplyInitialSettings(out reason) | SoundManager.Init 성공 후 현재 설정 적용. 재검사·저장·변경 알림 없음 |
| TryApply(setting, out reason) | 저장 없이 출력 적용 |
| TrySaveAndApply(setting, out reason) | 파일 저장 후 출력 적용. 전체 성공 여부 반환 |
| OnSettingsChanged / LoadError | 출력 적용 성공 후 변경 알림 / 최초 복원 실패 사유 |

조회자는 변경 알림을 구독한 뒤 현재 값을 읽습니다. Settings만으로 실제 출력 상태를 보장하지 않으며 재생 준비 여부는 SoundManager.IsReady로 구분합니다.

```text
출력 준비 확인 → 볼륨 범위 검사 → 저장 요청이면 파일 저장
→ 세 채널 믹서 적용 → 설정값 반영 → 변경 알림
```

| 실패 위치 | 유지/처리 기준 |
| --- | --- |
| 출력 준비·범위 검사 | 파일·출력·설정값 변경 없음 |
| 파일 저장 | 기존 파일·출력·설정값 유지. 믹서 적용과 변경 알림 없음 |
| 믹서 적용 | 기존 설정값 유지·변경 알림 없음. SoundManager가 이전 믹서 값 복구를 한 번 시도 |
| 저장 성공 후 믹서 적용 | 파일에는 새 값이 남으며 반환 사유에 저장 완료·출력 실패를 명시. 다음 실행은 저장된 새 값으로 최초 적용 |
| 믹서 복구 | 복구도 실패하면 SoundManager.IsReady 해제. 실제 출력값 보장 불가 |

파일 저장과 믹서 적용을 하나의 원자적 작업으로 보장하지 않습니다. 자동 재시도·파일 롤백을 추가하지 않으며 호출부는 실패 사유를 표시하고 재요청을 결정합니다.

볼륨 범위 규칙은 SoundManager.ValidateVolumes로 공유합니다. 기존 공개 TrySetVolumes는 준비·범위를 검사하고, VolumeSettings는 요청 경계에서 검사 후 TryApplyPreparedVolumes를 호출합니다. 검사 완료 적용 경로에서 같은 검사를 반복하지 않으며 믹서 적용·복구는 SoundManager 한 곳에서 처리합니다.

## 저장과 적용

화면 설정은 `displaysettings.json`, 단축키는 `keybindings.json`, 볼륨은 `volumesettings.json`에 따로 저장합니다. 진행 저장·삭제와 Firebase 백업/복원에는 포함하지 않습니다. 공통 파일 읽기와 임시 파일 작성·확정 파일 교체는 LocalSettingsFile을 재사용합니다.

화면 설정의 적용 순서는 다음과 같습니다. 볼륨은 위 별도 계약을 따릅니다.

```text
요청값 검사 → 저장 요청이면 임시 파일 작성·확정 파일 교체
→ 화면 변경 요청 → 접수 설정 반영 → 변경 알림
```

화면 저장 실패 시 기존 파일·접수 설정을 유지하고 화면 요청과 알림을 실행하지 않습니다. 자동 재시도하지 않으며 호출부에서 같은 선택값으로 다시 요청할 수 있습니다. 화면·볼륨의 저장 없는 적용은 파일을 바꾸지 않습니다.

화면·볼륨 파일이 없으면 기본값을 사용합니다. 읽기/내용 오류·화면 지원 목록 불일치·볼륨 범위 오류는 복원 실패로 제공하고 기본값으로 시작하며 원본을 덮어쓰지 않습니다. 종료 시 자동 저장하지 않습니다.

## 초기화와 수명

```text
GameBootstrap → GameManager.Awake → SettingsManager 생성·설정 복원
→ GameManager.Start 또는 첫 타이틀 준비 → 최초 화면 변경 요청
→ SoundManager.Init → 복원한 볼륨 최초 적용 → IsReady 이후 재생 요청
```

단축키는 씬 입력 연결 전에 복원·적용합니다. 화면·볼륨의 최초 적용은 두 시작 경로가 공유하며 한 번만 시도합니다. 사운드 초기화 실패 시 최초 볼륨 적용을 생략하고 InitialAudioError에 사유를 남깁니다. InitialSettingsCompleted는 적용 시도 완료를 뜻합니다. 화면은 변경 요청까지이며, 설정별 복원 오류는 기존 LoadError로 조회합니다. 설정 실패와 별개로 첫 타이틀의 진행 준비는 계속합니다.

씬 전환·재도전에서 설정을 다시 초기화하지 않습니다. SoundManager를 비활성화했다가 재사용할 때는 출력 초기화 → 현재 볼륨 재적용 → 재생 순서이며 파일은 다시 읽지 않습니다. 프레임 갱신과 포커스 상실은 기존 단축키 대기 처리에만 전달합니다. 종료는 단축키 편집·입력 대기와 설정 이벤트 참조를 무알림 정리한 뒤 사운드·저장 순서로 종료하며 자동 저장·볼륨 변경을 하지 않습니다.

## 확인 범위와 후속

- 화면 사용자 확인: 파일 없음의 기본값 초기화, 모드·해상도 변경, 저장 없는 적용, 저장 후 적용, Play 재실행 복원.
- 볼륨 사용자 확인: 저장 후 적용, Play 재실행 시 세 볼륨의 동일값 복원과 최초 믹서 적용 성공. 실제 음원 출력 확인과 구분합니다.
- 화면·볼륨의 추가 실패/수명 경로, Windows 실제 모드 전환과 공통화 후 단축키 회귀는 [TODO](../TODO.md#로비씬설정사운드vfx-공통-기반)에서 관리합니다. 담당별 호출 계약은 [LJ 연계 가이드](../LJ_INTEGRATION_GUIDE.md#공통-관리자와-사운드-연결)를 참조합니다.
