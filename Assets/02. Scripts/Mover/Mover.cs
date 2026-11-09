using UnityEngine;

/// <summary>
/// CharacterController 기반 가감속 이동과 회전 처리
/// </summary>
[RequireComponent( typeof( CharacterController ) )]
public class Mover : MonoBehaviour, IMoveSpeedModifier, IKnockbackMover
{
    const float DodgeTurnDuration = 0.18f;

    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] CharacterController _ctrl;    //이동과 충돌 처리 컴포넌트

    [Header( "----- 이동 -----" )]
    [SerializeField] float _moveSpeed;     //이동 속력
    [SerializeField] float _rotationSpeed;       //회전 속력
    [SerializeField] float _acceleration;     //수평 속도 변화량

    [Header( "----- 조준 회전 -----" )]
    [SerializeField] bool _useAimRotation;    //조준 방향 회전 사용 여부

    [Header( "----- 중력 -----" )]
    [SerializeField] float _gravity;      //중력 가속도
    [SerializeField] float _groundedDownSpeed;     //착지 유지를 위한 하강 속력

    [Header ( "----- 점프 -----" )]
    [SerializeField] float _jumpHeight = 1.5f;    //점프 높이

    Vector3 _direction;     //월드 이동 방향과 입력 크기
    Vector3 _horizontalVelocity;        //가감속 적용 후 수평 이동 속도
    Quaternion _targetRotation;     //마지막 이동 입력의 목표 회전
    Quaternion _aimRotation;    //수평 조준 방향의 목표 회전
    float _dodgeTurnRemaining;      //회피 후 회전 가속 시간

    float _verticalSpeed;       //수직 속도
    float _moveSpeedMultiplier = 1f;        //이동 속력 배율
    bool _canMove = true;       //수평 이동과 회전 허용 여부
    int _moveFrame = -1; //일반 이동 적용 프레임
    float _moveSqrSpeed; //실제 수평 이동 속도 제곱

    /// <summary>
    /// 현재 프레임의 실제 일반 이동 여부
    /// </summary>
    public bool HasMovedThisFrame =>
        _moveFrame == Time.frameCount && _moveSqrSpeed > 0.01f;

    /// <summary>
    /// 수평 이동과 회전 허용 여부
    /// </summary>
    public bool CanMove => _canMove;
    /// <summary>
    /// 일반 행동 허용과 별개인 강제 이동 가능 여부
    /// </summary>
    public bool CanKnockback => isActiveAndEnabled && _ctrl.enabled;

    /// <summary>
    /// 지상 회피를 시작할 수 있는 이동 상태인지 여부
    /// </summary>
    public bool CanDodge => CanKnockback && _canMove && IsGrounded;
    /// <summary>
    /// 배율이 적용된 목표 이동 속력
    /// </summary>
    public float MoveSpeed => _moveSpeed * _moveSpeedMultiplier;
    /// <summary>
    /// 상승 중이 아닌 실제 접지 여부
    /// </summary>
    public bool IsGrounded => _ctrl.isGrounded && _verticalSpeed <= 0f;
    /// <summary>
    /// 충돌 결과가 반영된 이동 속도 벡터
    /// </summary>
    public Vector3 Velocity => _ctrl.velocity;

    private void Awake ()
    {
        _targetRotation = transform.rotation;
        _aimRotation = _targetRotation;
    }

    /// <summary>
    /// 기본 이동 속력 설정
    /// </summary>
    /// <param name="moveSpeed">초당 이동 거리</param>
    public void SetMoveSpeed ( float moveSpeed )
    {
        _moveSpeed = Mathf.Max( 0f, moveSpeed );
    }

    /// <summary>
    /// 무기 설정에 따른 이동/조준 방향 회전 선택
    /// </summary>
    /// <param name="enabled">조준 방향 회전 사용 여부</param>
    public void SetAimRotationEnabled ( bool enabled )
    {
        _useAimRotation = enabled;

        //초기화 당시 방향을 기준으로 이전 무기의 회전 목표 정리
        _targetRotation = transform.rotation;
        _aimRotation = _targetRotation;
    }

    /// <summary>
    /// 월드 기준 수평 이동 방향과 목표 회전 설정
    /// </summary>
    /// <param name="direction">이동 벡터</param>
    public void SetDirection ( Vector3 direction )
    {
        if ( _canMove == false )
            return;

        direction.y = 0f;

        //대각선 최대 속력을 제한하고 작은 입력 크기는 유지
        _direction = Vector3.ClampMagnitude( direction, 1f );

        if ( _direction.sqrMagnitude <= 0.0001f )
            return;

        //입력이 해제돼도 마지막 목표 방향은 유지
        _targetRotation = Quaternion.LookRotation( _direction, Vector3.up );
    }

    /// <summary>
    /// 수평 조준 방향의 목표 회전 설정
    /// </summary>
    /// <param name="direction">월드 기준 조준 방향</param>
    public void SetAimDirection ( Vector3 direction )
    {
        direction.y = 0f;

        //수직 방향만 전달되면 마지막 수평 방향 유지
        if ( direction.sqrMagnitude <= 0.0001f )
            return;

        _aimRotation = Quaternion.LookRotation ( direction , Vector3.up );
    }

    /// <summary>
    /// 이동 방향 회전 속력 설정
    /// </summary>
    /// <param name="rotationSpeed">초당 회전 각도</param>
    public void SetRotationSpeed ( float rotationSpeed )
    {
        _rotationSpeed = Mathf.Max( 0f, rotationSpeed );
    }

    /// <summary>
    /// 수평 이동과 이동 방향 회전 허용 여부 설정
    /// </summary>
    /// <param name="enabled">이동 허용 여부</param>
    public void SetMoveEnabled ( bool enabled )
    {
        _canMove = enabled;

        if ( enabled == true )
            return;

        //즉시 정지
        _moveFrame = -1;
        _moveSqrSpeed = 0f;
        _direction = Vector3.zero;
        _horizontalVelocity = Vector3.zero;
        _dodgeTurnRemaining = 0f;
        _targetRotation = transform.rotation;
        _aimRotation = _targetRotation;
    }

    /// <summary>
    /// 이동 속력에 적용할 배율 설정
    /// </summary>
    /// <param name="multiplier">기본 속력에 곱할 배율</param>
    public void SetMoveSpeedMultiplier ( float multiplier )
    {
        _moveSpeedMultiplier = Mathf.Max( 0f, multiplier );
    }

    /// <summary>
    /// 지정 월드 위치로 순간 이동
    /// </summary>
    /// <param name="position">이동할 월드 위치</param>
    public void Teleport ( Vector3 position )
    {
        _moveFrame = -1;
        _moveSqrSpeed = 0f;
        bool wasEnabled = _ctrl.enabled;

        _ctrl.enabled = false;

        try
        {
            transform.position = position;

            //이전 위치의 이동 입력과 낙하 속도를 새 위치에 적용하지 않음
            _direction = Vector3.zero;
            _horizontalVelocity = Vector3.zero;
            _verticalSpeed = 0f;
            _dodgeTurnRemaining = 0f;
            _targetRotation = transform.rotation;
            _aimRotation = _targetRotation;
        }
        finally
        {
            _ctrl.enabled = wasEnabled;
        }
    }

    /// <summary>
    /// 접지 상태에서 점프 상승 속도 적용
    /// </summary>
    /// <returns>점프 시작 여부</returns>
    public bool TryJump ( )
    {
        if ( isActiveAndEnabled == false || _ctrl.enabled == false
            || _canMove == false || IsGrounded == false )
            return false;

        //마지막 접지 결과가 남아 있어도 상승 속도 적용 후에는 공중으로 판단
        _verticalSpeed = Mathf.Sqrt ( 2f * _gravity * _jumpHeight );
        return true;
    }

    /// <summary>
    /// 현재 프레임의 회전과 이동 처리
    /// </summary>
    /// <param name="deltaTime">프레임 경과 시간</param>
    public void UpdateMove ( float deltaTime )
    {
        if ( isActiveAndEnabled == false || _ctrl.enabled == false )
            return;

        if ( deltaTime <= 0f )
            return;

        Rotate( deltaTime );
        UpdateGravity( deltaTime );
        Move( deltaTime );
    }

    /// <summary>
    /// 이동 또는 조준 방향의 목표 회전 적용
    /// </summary>
    /// <param name="deltaTime">프레임 경과 시간</param>
    void Rotate ( float deltaTime )
    {
        if ( _canMove == false )
            return;

        //이동속력이 없어도 조준 회전은 허용
        if ( _useAimRotation == false && MoveSpeed <= 0f )
            return;

        Quaternion targetRotation = _useAimRotation == true
            ? _aimRotation
            : _targetRotation;

        float rotationSpeed = _rotationSpeed;
        if ( _dodgeTurnRemaining > 0f )
        {
            _dodgeTurnRemaining = Mathf.Max( 0f, _dodgeTurnRemaining - deltaTime );
            float progress = 1f - _dodgeTurnRemaining / DodgeTurnDuration;
            rotationSpeed = Mathf.SmoothStep( 0f, _rotationSpeed, progress );
        }

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, rotationSpeed * deltaTime );
    }

    /// <summary>
    /// 착지 상태에 따라 수직 속도 갱신
    /// </summary>
    /// <param name="deltaTime">프레임 경과 시간</param>
    void UpdateGravity ( float deltaTime )
    {
        if ( _ctrl.isGrounded == true && _verticalSpeed <= 0f )
        {
            //착지 중에도 작은 하강 이동을 요청해 바닥 접촉 유지
            _verticalSpeed = -_groundedDownSpeed;
            return;
        }

        _verticalSpeed -= _gravity * deltaTime;
    }

    /// <summary>
    /// 수평 가감속과 중력을 합쳐 실제 이동 적용
    /// </summary>
    /// <param name="deltaTime">프레임 경과 시간</param>
    void Move ( float deltaTime )
    {
        if ( _canMove == true )
        {
            Vector3 targetVelocity = _direction * MoveSpeed;

            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, targetVelocity, _acceleration * deltaTime );
        }
        else
        {
            _horizontalVelocity = Vector3.zero;
        }

        //수평 이동 제한 중에도 중력은 적용
        Vector3 velocity = _horizontalVelocity;
        velocity.y = _verticalSpeed;

        Vector3 previousPosition = transform.position;
        ApplyMove( velocity * deltaTime );

        Vector3 displacement = transform.position - previousPosition;
        displacement.y = 0f;
        _moveFrame = Time.frameCount;
        //프레임당 거리 대신 속도로 판단해 높은 FPS에서도 발소리 유지
        _moveSqrSpeed = displacement.sqrMagnitude / ( deltaTime * deltaTime );
    }

    /// <summary>
    /// 강제 수평 이동과 중력을 함께 적용
    /// </summary>
    /// <param name="displacement">요청 수평 이동량</param>
    /// <param name="deltaTime">중력 갱신에 사용할 프레임 시간</param>
    /// <returns>측면 충돌 없이 이동했는지 여부</returns>
    public bool MoveKnockback ( Vector3 displacement, float deltaTime )
    {
        //수평 넉백과 중력을 합쳐 CharacterController.Move를 한 번만 호출
        return MoveForced ( displacement , deltaTime );
    }

    /// <summary>
    /// 일반 이동과 별개로 강제 수평 이동과 중력을 한 번 적용
    /// </summary>
    /// <param name="displacement">요청 수평 이동량</param>
    /// <param name="deltaTime">중력 갱신에 사용할 프레임 시간</param>
    /// <returns>측면 충돌 없이 이동했는지 여부</returns>
    public bool MoveForced ( Vector3 displacement , float deltaTime )
    {
        if ( CanKnockback == false )
            return false;

        //일반 이동 입력/감속/회전은 사용하지 않고 중력만 기존 방식으로 갱신
        UpdateGravity ( deltaTime );
        displacement.y = _verticalSpeed * deltaTime;

        //넉백과 회피가 같은 이동/충돌 경로를 사용하며 중력도 중복 적용하지 않음
        CollisionFlags flags = ApplyMove ( displacement );
        return ( flags & CollisionFlags.Sides ) == 0;
    }

    /// <summary>
    /// 캐릭터와 회전 목표를 회피 방향으로 설정
    /// </summary>
    /// <param name="direction">정규화한 수평 회피 방향</param>
    public void FaceDodgeDirection ( Vector3 direction )
    {
        Quaternion rotation = Quaternion.LookRotation ( direction , Vector3.up );
        transform.rotation = rotation;
        _targetRotation = rotation;
        _aimRotation = rotation;
    }

    /// <summary>
    /// 회피 후 일반 회전 속도까지 부드럽게 가속
    /// </summary>
    public void BeginDodgeTurn ( )
    {
        _dodgeTurnRemaining = DodgeTurnDuration;
    }

    /// <summary>
    /// 실제 이동과 접지, 천장 충돌 결과 반영
    /// </summary>
    /// <param name="displacement">월드 이동량</param>
    /// <returns>CharacterController 충돌 결과</returns>
    CollisionFlags ApplyMove ( Vector3 displacement )
    {
        CollisionFlags flags = _ctrl.Move( displacement );

        if ( ( flags & CollisionFlags.Below ) != 0
            && _verticalSpeed < 0f )
        {
            _verticalSpeed = -_groundedDownSpeed;
        }

        if ( ( flags & CollisionFlags.Above ) != 0
            && _verticalSpeed > 0f )
        {
            _verticalSpeed = 0f;
        }

        return flags;
    }

    private void OnDisable ()
    {
        _direction = Vector3.zero;
        _horizontalVelocity = Vector3.zero;
        _verticalSpeed = 0f;
        _dodgeTurnRemaining = 0f;
        _targetRotation = transform.rotation;
        _aimRotation = _targetRotation;
    }
}
