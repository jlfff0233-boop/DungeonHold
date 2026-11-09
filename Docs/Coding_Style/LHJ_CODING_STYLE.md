# LHJ_CODING_STYLE

## PURPOSE

이 문서는 LHJ 담당 영역에서 AI 에이전트가 코드를 작성하거나 수정할 때 따라야 하는 코딩 스타일을 정의합니다.

기존 코드의 구조와 표현 방식을 우선 존중하며, 스타일 통일을 이유로 요청 범위를 넘어선 리팩터링은 수행하지 않습니다.

## NAMING

### 클래스 / 메서드

- 클래스명과 메서드명은 `PascalCase`를 사용합니다.

```csharp
public class PlayerController : MonoBehaviour
{
    public void InitPlayer()
    {
    }
}
```

### private 필드

- private 필드는 `_camelCase`를 사용합니다.

```csharp
[SerializeField] private float _moveSpeed;
private int _currentHealth;
private Animator _animator;
```

### 지역 변수 / 파라미터

- 지역 변수와 파라미터는 `camelCase`를 사용합니다.

```csharp
float currentSpeed;
int currentIndex;

public void SetMoveSpeed(float moveSpeed)
{
}
```

### 접근 제한자

- 필드와 메서드의 접근 제한자는 가능하면 명시합니다.
- 외부 접근이 필요하지 않은 경우 `private`을 기본으로 사용합니다.
- UnityEvent, UI Event, 다른 시스템 호출 등 외부 접근이 필요한 경우에만 `public`을 사용합니다.

## SERIALIZED_FIELDS

Inspector에서 연결하거나 조정할 값은 `public` 필드보다 `[SerializeField] private` 사용을 우선합니다.

```csharp
[SerializeField] private float _speed;
[SerializeField] private GameObject _resultPanel;
```

관련 Inspector 필드가 여러 개라면 `[Header]`로 역할별 구역을 나눕니다.

```csharp
[Header("이동")]
[SerializeField] private float _walkSpeed;
[SerializeField] private float _runSpeed;

[Header("UI")]
[SerializeField] private GameObject _resultPanel;
```

Header는 실제 구분이 필요한 경우에만 사용하며 불필요하게 세분화하지 않습니다.

## FORMATTING

### 중괄호

Allman 스타일을 사용합니다.

```csharp
if (_isGameClear)
{
    return;
}

private void Update()
{
}
```

### 한 줄 조건문

아주 단순한 단일 동작은 한 줄 조건문을 사용할 수 있습니다.

```csharp
if (_target == null) return;
```

조건 내부가 길어지거나 여러 동작을 수행하면 반드시 중괄호를 사용합니다.

### 공백

조건문과 반복문의 키워드 뒤에는 공백을 둡니다.

```csharp
if (_target != null)
{
}

for (int i = 0; i < count; i++)
{
}
```

## METHOD_STRUCTURE

메서드는 기능 단위로 분리합니다.

하나의 메서드가 서로 다른 책임을 많이 수행하기 시작하면 의미 있는 행동 단위로 나눕니다.

```csharp
private void TakeDamage(int damage)
{
    _currentHealth -= damage;

    UpdateHealthUI();
    PlayDamageEffect();

    if (_currentHealth <= 0)
    {
        GameOver();
    }
}
```

다만 코드 스타일을 맞추기 위한 목적으로 기존 클래스를 임의로 분리하거나 새로운 계층을 만들지 않습니다.

## CONTROL_FLOW

유효하지 않은 상태나 실행할 수 없는 상황은 메서드 초반에 검사하고 Early Return으로 처리하는 방식을 선호합니다.

```csharp
private void TakeDamage(int damage)
{
    if (_currentHealth <= 0)
    {
        return;
    }

    _currentHealth -= damage;
}
```

중첩을 줄일 수 있다면 Guard Clause를 우선합니다.

## NULL_CHECK

Unity 참조나 선택적 의존성은 필요한 경우 null 검사를 수행합니다.

```csharp
if (_dialogueScripting != null)
{
    _dialogueScripting.TryStartDialogue();
}
```

Inspector 누락이나 데이터 부재로 기능을 실행할 수 없는 경우 Warning 또는 Error를 남기고 안전하게 종료할 수 있습니다.

```csharp
if (_dialogueDataMap == null)
{
    Debug.LogWarning("DialogueDataMap이 연결되지 않았습니다.");
    return;
}
```

필수 참조인지 선택 참조인지 구분하며 모든 코드에 기계적으로 null 검사를 추가하지 않습니다.

## EVENTS

이벤트를 구독하면 수명 종료 시 반드시 해제합니다.

```csharp
private void OnEnable()
{
    _gameFlow.OnEnded += HandleGameEnded;
}

private void OnDisable()
{
    _gameFlow.OnEnded -= HandleGameEnded;
}
```

`OnEnable / OnDisable`, `Start / OnDestroy` 등 실제 수명에 맞는 구독 / 해제 쌍을 사용합니다.

## COMMENTS

주석은 필요한 경우에만 짧게 작성합니다.

코드만 읽어도 명확한 내용을 반복해서 설명하지 않습니다.

```csharp
_dialogueList = new List<DialogueLine>(); // 명시적 초기화
```

다음 경우에는 주석을 사용할 수 있습니다.

- 코드만으로 의도가 바로 드러나지 않는 경우
- 임시 우회 또는 특별한 예외 규칙이 있는 경우
- Unity 설정이나 실행 순서에 주의가 필요한 경우

장황한 줄 단위 설명 주석은 지양합니다.

## LOGGING

디버깅과 설정 누락 확인을 위해 `Debug.Log`, `Debug.LogWarning`, `Debug.LogError`를 사용할 수 있습니다.

로그는 한국어로 작성할 수 있으며, 문제의 대상과 이유를 가능한 한 명확하게 남깁니다.

```csharp
Debug.LogWarning("골키퍼 Animator가 연결되지 않았습니다.");
```

## STATE_MANAGEMENT

작은 규모의 상태는 bool이나 일반 필드로 직관적으로 관리할 수 있습니다.

```csharp
private bool _isGameStarted;
private bool _isGameClear;
private bool _isTransitioning;
```

단순한 기능에 불필요하게 FSM, Strategy, Service 계층 등을 도입하지 않습니다.

복잡도가 실제로 증가한 경우에만 별도 구조 분리를 검토합니다.

## UNITY_STYLE

### 컴포넌트 참조

- Inspector에서 연결해야 하는 외부 참조는 `[SerializeField] private`을 사용합니다.
- 자기 GameObject에서 명확하게 찾을 수 있는 컴포넌트는 `GetComponent`를 사용할 수 있습니다.

### UI 메서드

Button, UnityEvent 등에서 직접 호출해야 하는 메서드는 `public`으로 작성할 수 있습니다.

```csharp
public void RestartGame()
{
}

public void OnClickStartButton()
{
}
```

### 튜닝값

게임 플레이 중 조정 가능성이 높은 값은 Inspector에서 조절할 수 있도록 SerializeField로 두는 것을 우선합니다.

## ABSTRACTION

구현은 직관성을 우선합니다.

요청하지 않은 상황에서 다음과 같은 구조를 새로 만들지 않습니다.

- 불필요한 Interface 계층
- 지나친 Manager / Service 분리
- 단순 기능을 위한 Strategy / Factory / State 패턴
- 기존 단일 클래스를 여러 클래스로 강제 분리
- 단순 Animator 호출을 위한 별도 Wrapper

새로운 추상화는 실제 중복이나 의존 문제를 해결하는 데 필요한 경우에만 도입합니다.

## PRESERVE_EXISTING_STYLE

기존 파일을 수정할 때는 해당 파일의 기존 스타일을 우선합니다.

특히 다음은 스타일 통일만을 이유로 대규모 수정하지 않습니다.

- 기존 필드명
- 기존 메서드명
- 기존 public API
- Header 구성
- 주석 형식
- 파일 구조
- 클래스 책임 범위

새로 추가하는 코드만 이 문서의 규칙에 맞추고, 기존 코드는 필요한 범위에서만 정리합니다.

## CHECKLIST

코드 작성 또는 수정 후 다음을 확인합니다.

- 클래스 / 메서드는 `PascalCase`인가
- private 필드는 `_camelCase`인가
- 지역 변수 / 파라미터는 `camelCase`인가
- Inspector 필드는 `[SerializeField] private`을 우선했는가
- 불필요한 public 노출이 없는가
- Guard Clause로 단순화할 수 있는 중첩이 없는가
- 이벤트 구독과 해제가 짝을 이루는가
- 불필요한 추상화나 리팩터링을 추가하지 않았는가
- 주석이 코드 내용을 반복해서 설명하고 있지 않은가
