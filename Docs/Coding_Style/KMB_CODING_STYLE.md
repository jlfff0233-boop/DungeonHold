# KMB_CODING_STYLE

## PURPOSE

이 문서는 KMB 담당 영역에서 AI 에이전트가 코드를 작성하거나 수정할 때 따라야 하는 코딩 스타일을 정의합니다.

기존 코드의 절차적 흐름과 설명형 주석 스타일을 유지하며, 스타일 통일을 이유로 기존 구조를 과도하게 추상화하거나 재설계하지 않습니다.

## NAMING

### 클래스 / 메서드

클래스명과 메서드명은 `PascalCase`를 사용합니다.

```csharp
public class Player : MonoBehaviour
{
    private void MovePlayer()
    {
    }

    public void SetCanMove(bool canMove)
    {
    }
}
```

### private 필드

private 필드는 `_camelCase`를 사용합니다.

```csharp
private float _moveSpeedMultiplier;
private bool _canMove;
private CharacterController _characterController;
```

### 지역 변수 / 파라미터

지역 변수와 파라미터는 `camelCase`를 사용합니다.

```csharp
bool isMoving;
float currentSpeed;
Vector3 moveDirection;

public void SetMoveSpeedMultiplier(float speedMultiplier)
{
}
```

## ACCESS_MODIFIERS

접근 제한자는 생략하지 않습니다.

Inspector 필드도 `[SerializeField] private` 형태를 사용합니다.

```csharp
[SerializeField] private float _walkSpeed = 3f;
[SerializeField] private Animator _animator;

private float _lastGroundedTime;
```

외부에서 사용할 필요가 없는 메서드는 `private`을 기본으로 합니다.

외부 시스템이나 UnityEvent 등에서 호출해야 하는 경우에만 `public`을 사용합니다.

## FIELD_LAYOUT

필드는 클래스 상단에 배치합니다.

관련성이 높은 필드끼리 가까이 배치하고, 그룹 사이에는 빈 줄을 사용해 구분할 수 있습니다.

```csharp
[SerializeField] private float _walkSpeed = 3f;
[SerializeField] private float _runSpeed = 6f;
[SerializeField] private float _jumpPower = 4f;

private float _moveSpeedMultiplier = 1f;
private bool _canMove = true;

[SerializeField] private CameraCtrl _cameraCtrl;
[SerializeField] private Animator _animator;
```

기존 파일에서 `[Header]`를 사용하지 않는 경우 스타일 통일을 이유로 새 Header를 대량 추가하지 않습니다.

## FORMATTING

### 중괄호

Allman 스타일을 사용합니다.

```csharp
if (_playerHealth.IsDead)
{
    return;
}

private void ApplyGravity()
{
}
```

### 단순 조건문

실행문이 하나이고 짧다면 중괄호를 생략할 수 있습니다.

```csharp
if (Keyboard.current.wKey.isPressed)
    input.y += 1f;
```

조건 내부가 여러 줄이거나 복잡하면 반드시 중괄호를 사용합니다.

## LINE_BREAKS

긴 조건, 계산식, 함수 호출은 가독성을 위해 여러 줄로 분리할 수 있습니다.

```csharp
Vector3 moveDirection =
    _cameraCtrl.Forward * input.y +
    _cameraCtrl.Right * input.x;
```

```csharp
bool isRunning =
    isMoving &&
    !isLockedOn &&
    !isBowAiming &&
    Keyboard.current.leftShiftKey.isPressed;
```

단순하고 짧은 함수 호출까지 불필요하게 여러 줄로 나누지는 않습니다.

## METHOD_FLOW

하나의 메서드 안에서 관련된 처리 과정을 위에서 아래로 읽히도록 순차적으로 구성합니다.

예:

```text
입력 확인
→ 현재 상태 계산
→ 이동 속도 결정
→ 실제 이동
→ 방향 회전
→ Animator 갱신
```

서로 밀접한 동작은 같은 메서드 안에서 절차적으로 처리할 수 있습니다.

다만 완전히 다른 기능까지 하나의 메서드에 합치지는 않습니다.

## STATE_VARIABLES

복잡한 조건은 의미 있는 bool 지역 변수로 먼저 계산합니다.

```csharp
bool isMoving = moveDirection != Vector3.zero;
bool isLockedOn = _playerLockOn != null && _playerLockOn.IsLockedOn;
bool isBowAiming = _playerBowAttack != null && _playerBowAttack.IsBusy;
```

동일한 복합 조건을 여러 위치에 반복해서 작성하기보다 상태 변수로 이름을 부여해 사용합니다.

상태 변수는 `is`, `can`, `has`, `was` 등의 접두어를 사용해 의미를 명확하게 합니다.

```csharp
_isGameStarted
_canMove
_isGrounded
_isFalling
_wasFalling
```

## CONTROL_FLOW

현재 상태를 먼저 계산한 뒤 그 결과를 적용하는 흐름을 선호합니다.

예:

```text
Ground 감지
→ isGrounded 계산
→ isFalling 계산
→ Animator 상태 갱신
→ 낙하 / 착지 처리
→ 점프 입력
→ 중력 적용
```

필요한 경우 Early Return을 사용할 수 있지만 모든 메서드를 Guard Clause 중심으로 억지로 재구성하지 않습니다.

## COMMENTS

한국어 설명 주석을 적극 사용합니다.

특히 다음 내용을 주석으로 남기는 스타일을 유지합니다.

- 코드 블록이 어떤 역할을 하는지
- 특정 판정이 필요한 이유
- 수치나 상태가 어떤 의미인지
- 일반적인 구현과 다른 예외 규칙
- 애니메이션 / 입력 / 상태 처리의 의도

```csharp
// 이동 가능한 상태에서만 이동 입력 처리

// 대각선 이동 속도가 빨라지지 않게 막기

// 현재 바닥 상태 판단
// 지금 시간 - 마지막 바닥 감지 시간 <= 여유 시간이면 아직 바닥에 있다고 판단
```

단, 코드의 모든 줄을 번역하듯 주석 처리하지 않습니다.

주석은 논리 단위 앞에 배치하는 것을 우선합니다.

## COMPONENT_REFERENCES

자기 GameObject에서 찾을 수 있는 컴포넌트는 `GetComponent`를 사용할 수 있습니다.

```csharp
private void Awake()
{
    _characterController = GetComponent<CharacterController>();
    _playerLockOn = GetComponent<PlayerLockOn>();
}
```

외부 오브젝트나 명시적인 연결이 필요한 참조는 Inspector에서 할당합니다.

```csharp
[SerializeField] private CameraCtrl _cameraCtrl;
[SerializeField] private Animator _animator;
```

## NULL_HANDLING

필수 의존성과 선택 의존성을 구분합니다.

### 필수 의존성

항상 존재해야 하는 컴포넌트는 존재를 전제로 사용할 수 있습니다.

```csharp
if (_playerHealth.IsDead)
{
    return;
}

_characterController.Move(moveDirection);
```

### 선택 의존성

없을 수 있는 기능은 null 검사를 수행합니다.

```csharp
bool isLockedOn =
    _playerLockOn != null &&
    _playerLockOn.IsLockedOn;
```

```csharp
if (_cameraCtrl != null)
{
    moveDirection =
        _cameraCtrl.Forward * input.y +
        _cameraCtrl.Right * input.x;
}
```

모든 참조에 기계적으로 null 검사를 추가하지 않습니다.

## UNITY_INPUT

현재 파일 구조가 Input System 직접 조회 방식이라면 같은 방식을 유지할 수 있습니다.

```csharp
Keyboard.current.wKey.isPressed
Keyboard.current.leftShiftKey.isPressed
Keyboard.current.spaceKey.wasPressedThisFrame
```

다만 프로젝트 전체에서 별도 입력 시스템이 확정되면 공용 구조를 우선합니다.

기존 입력 방식을 스타일 개선만을 이유로 임의 교체하지 않습니다.

## ANIMATOR

기능 클래스에서 Animator를 직접 제어하는 기존 구조를 유지할 수 있습니다.

```csharp
_animator.SetBool("IsGrounded", isGrounded);
_animator.SetFloat("Speed", animationSpeed);
_animator.SetTrigger("Land");
```

Animator 파라미터를 자동으로 별도 Wrapper, Hash 클래스, Service로 이전하지 않습니다.

프로젝트 공용 규격이 별도로 확정된 경우에만 그 구조를 따릅니다.

## TUNING_VALUES

플레이 감각이나 밸런스 조정에 사용되는 값은 `[SerializeField] private`로 Inspector에 노출하는 방식을 우선합니다.

```csharp
[SerializeField] private float _walkSpeed = 3f;
[SerializeField] private float _runSpeed = 6f;
[SerializeField] private float _rotationSpeed = 12f;
[SerializeField] private float _groundedGraceTime = 0.15f;
```

구현 내부에서만 사용하는 단순 보정값은 코드 안에서 직접 사용할 수 있습니다.

## METHOD_AND_CLASS_SPLIT

큰 기능은 별도 컴포넌트로 분리할 수 있습니다.

예:

```text
Player
PlayerAttack
PlayerHealth
PlayerLockOn
PlayerBowAttack
```

그러나 해당 기능 컴포넌트 내부에서는 관련 세부 로직을 직접적이고 절차적으로 구현하는 방식을 우선합니다.

단순 기능을 다음과 같은 과도한 계층으로 확장하지 않습니다.

```text
IMovementStrategy
IGravityProvider
PlayerMovementStateMachine
MovementContext
AnimatorParameterService
```

실제 중복, 교체 필요성, 의존 문제 등 명확한 이유가 있을 때만 새로운 추상화를 도입합니다.

## CONDITIONAL_EXPRESSIONS

단순한 값 선택에는 삼항 연산자를 사용할 수 있습니다.

```csharp
float baseSpeed = isBowAiming
    ? _bowAimWalkSpeed
    : (isRunning ? _runSpeed : _walkSpeed);
```

복잡한 게임 로직 전체를 삼항 연산자로 압축하지 않습니다.

## PRESERVE_EXISTING_STYLE

기존 KMB 파일을 수정할 때는 해당 파일의 흐름과 주석 스타일을 우선합니다.

스타일 통일만을 이유로 다음을 임의 변경하지 않습니다.

- 메서드 분리 구조
- Animator 직접 호출 구조
- 입력 방식
- 필드 순서
- 기존 주석
- public API
- 클래스 책임
- 컴포넌트 분리 구조

새로 작성하는 코드가 기존 흐름에 자연스럽게 이어지도록 합니다.

## CHECKLIST

코드 작성 또는 수정 후 다음을 확인합니다.

- 클래스 / 메서드는 `PascalCase`인가
- private 필드는 `_camelCase`인가
- 지역 변수 / 파라미터는 `camelCase`인가
- 접근 제한자를 명시했는가
- Inspector 필드는 `[SerializeField] private` 형태인가
- 복합 조건을 의미 있는 bool 상태로 정리할 수 있는가
- 로직이 위에서 아래로 자연스럽게 읽히는가
- 긴 식은 필요한 경우 적절히 줄바꿈했는가
- 중요한 판정 이유에 한국어 주석이 있는가
- 필수 / 선택 참조의 null 처리 방식이 적절한가
- 기존 구조를 불필요하게 추상화하지 않았는가
- 스타일 정리만을 이유로 기존 코드를 대규모 수정하지 않았는가
