using System;
using System.Text;
using UnityEngine;

/// <summary>
/// 애니메이터 핸들러 - 공통 재생과 애니메이션 이벤트 전달
/// </summary>
[RequireComponent ( typeof ( Animator ) )]
public class AnimatorHandler : MonoBehaviour
{
    const float MoveDirectionDampTime = 0.08f;
    const float MoveSpeedDampTime = 0.05f;
    const float UpperStanceBlendTime = 0.10f;
    const float BowAimBlendInEnd = 0.35f;
    const float BowAimBlendOutStart = 0.55f;
    const float BowAimBlendOutEnd = 0.82f;
    const float BowWalkAttackBlendInEnd = 0.26f;
    const float BowWalkAttackBlendOutStart = 0.66f;
    static readonly int BowMoveReadyStateHash = Animator.StringToHash ( "UpperStance.BowMoveReady" );

    enum HitPhase
    {
        None,
        Requested,
        Playing
    }

    [Header ( "----- 디버그 -----" )]
    [SerializeField] bool _showDebugLog;    //실제 Base 상태 변경과 넉백 피격 요청 로그 출력 여부

    Animator _animator;     //같은 오브젝트의 애니메이터

    int _attackLayerIndex;      //상체 행동 레이어
    int _upperStanceLayerIndex; //상체 대기 레이어
    bool _useUpperStance;      //무기의 상체 대기 설정
    bool _isPresentationActive; //주체가 표현을 갱신하는 상태

    Func<Vector3> _readBowAimDirection;    //활 기본 공격의 월드 조준 방향
    Transform _bowAimSpine;    //양팔과 활을 함께 돌리는 상체 시작 본
    Transform _bowJoint;       //활 손잡이 위치의 본
    Transform _bowStringJoint; //활 시위 위치의 본
    Quaternion _bowAimOriginalRotation;    //보정 전 척추 로컬 회전
    bool _bowAimApplied;       //이번 자세에 상체 보정을 적용했는지 여부

    int _moveSpeedHash;     //이동 속도 파라미터
    int _moveXHash;         //캐릭터 기준 좌우 이동 방향
    int _moveZHash;         //캐릭터 기준 앞뒤 이동 방향
    int _attackHash;        //기본 공격 트리거
    int _skillHash;         //스킬 트리거
    int _ultimateHash;      //궁극기 트리거
    int _ultimateReadyHash; //궁극기 준비 파라미터
    int _dieHash;       //사망 상태 파라미터
    int _takeHitHash;       //피격 트리거
    int _isGroundedHash;    //접지 상태 파라미터
    int _isKnockbackHash;   //넉백 표현 상태 파라미터
    int _isDodgingHash;     //회피 진행 파라미터

    int _idleStateHash;     //대기 상태 경로
    int _runStateHash;      //달리기 상태 경로
    int _attackStateHash;       //기본 공격 상태 경로
    int _dieStateHash;      //사망 상태 경로
    int _attackIdleStateHash;   //상체 행동 대기 상태 경로
    int _hitStateHash;          //상체 피격 상태 경로
    int _skillStateHash;        //스킬 상태 경로
    int _ultimateStateHash;     //전신 궁극기 상태 경로
    int _knockbackHitStateHash; //전신 피격 상태 경로
    int _jumpStateHash;         //점프 상태 경로
    int _dodgeStateHash;        //회피 상태 경로
    int _previousBaseStateHash; //로그에서 마지막으로 확인한 Base 상태
    (int direction, int baseState, int attackState) _lastMoveLog;

    int _attackId;      //재생 중인 원래 공격 회차
    int _skillId;       //재생 중인 원래 스킬 회차
    int _ultimateId;    //재생 중인 원래 궁극기 회차
    HitPhase _hitPhase;
    bool _isAttackActive;       //공격 이벤트 수신 허용
    bool _isSkillActive;        //스킬 이벤트 수신 허용
    bool _isUltimateActive;     //궁극기 이벤트 수신 허용
    bool _isUltimateReturning;  //Base 복귀 후 궁극기 이동 제한을 해제할지 여부
    bool _isReturning;      //Base Layer 초기화/부활/궁극기 복귀 대기
    //회피 종료 후 기본 상태 복귀도 같은 대기로 확인
    bool _isActionReturning;    //상체 행동 복귀 대기
    bool _isUltimateReady;      //상체 궁극기 준비 요청
    bool _isReadyReturning;     //조준 취소 후 상체 복귀 대기
    bool _isDead;       //사망 표현 여부
    bool _isWaitingDeathEnd;        //사망 완료 신호 대기
    bool _isRestoringAlive;     //부활 후 복귀 완료 대기

    Vector3 _previousPosition;      //실제 이동 속도 계산 기준

    StringBuilder _dodgeLog;
    CharacterController _dodgeLogController;
    Transform _dodgeLogHips;
    int _dodgeLogQuarter;

    /// <summary>
    /// 새 행동을 시작할 수 있는지 여부
    /// </summary>
    /// <remarks>
    /// 행동 로직이 종료됐어도 이전 클립의 복귀가 끝나기 전에는 다음 재생을 허용하지 않습니다.
    /// 일반 이동 허용은 Hero/Mover가 별도로 관리합니다.
    /// </remarks>
    public bool CanStartAction =>
        isActiveAndEnabled
        && _animator.isActiveAndEnabled
        && _isDead == false
        && _isAttackActive == false
        && _isSkillActive == false
        && _isUltimateActive == false
        && _isUltimateReturning == false
        && _isReturning == false
        && _isActionReturning == false;

    /// <summary>
    /// 궁극기 동작 또는 복귀 진행 여부
    /// </summary>
    public bool IsUltimatePlaying =>
        _isUltimateActive || _isUltimateReturning;

    /// <summary>
    /// 타격 이벤트(원래 공격 회차)
    /// </summary>
    public event Action<int> OnAttackHit;

    /// <summary>
    /// 하체 이동의 발 디딤 이벤트
    /// </summary>
    public event Action OnFootstep;

    /// <summary>
    /// 행동 모션의 효과음 이벤트(사운드 식별자)
    /// </summary>
    public event Action<SoundId> OnSound;

    /// <summary>
    /// 이동 클립의 발 디딤 전달
    /// </summary>
    /// <param name="animationEvent">발 디딤 재생 정보</param>
    public void AnimationFootstep ( AnimationEvent animationEvent )
    {
        if ( _isDead == true || _isReturning == true )
            return;

        if ( animationEvent.animatorStateInfo.fullPathHash != _runStateHash )
            return;

        OnFootstep?.Invoke ( );
    }

    /// <summary>
    /// 유효한 행동 모션의 효과음 전달
    /// </summary>
    /// <param name="animationEvent">사운드 ID와 재생 정보</param>
    public void AnimationSound ( AnimationEvent animationEvent )
    {
        int state = animationEvent.animatorStateInfo.fullPathHash;
        bool isCurrentAction =
            ( _isAttackActive == true && state == _attackStateHash )
            || ( _isSkillActive == true && state == _skillStateHash )
            || ( _isUltimateActive == true && state == _ultimateStateHash );

        if ( isCurrentAction == false )
            return;

        OnSound?.Invoke ( ( SoundId ) animationEvent.intParameter );
    }

    /// <summary>
    /// 공격 완료 이벤트(원래 공격 회차)
    /// </summary>
    public event Action<int> OnAttackCompleted;

    /// <summary>
    /// 스킬 타격 이벤트(원래 스킬 회차)
    /// </summary>
    public event Action<int> OnSkillHit;

    /// <summary>
    /// 스킬 완료 이벤트(원래 스킬 회차)
    /// </summary>
    public event Action<int> OnSkillCompleted;

    /// <summary>
    /// 궁극기 타격 이벤트(원래 궁극기 회차)
    /// </summary>
    public event Action<int> OnUltimateHit;

    /// <summary>
    /// 궁극기 완료 이벤트(원래 궁극기 회차)
    /// </summary>
    public event Action<int> OnUltimateCompleted;

    /// <summary>
    /// 궁극기 전신 복귀 완료 이벤트
    /// </summary>
    public event Action OnUltimateReturned;

    /// <summary>
    /// 사망 동작 완료 이벤트
    /// </summary>
    public event Action OnDeathCompleted;

    /// <summary>
    /// 부활 후 기본 상태 복귀 완료 이벤트
    /// </summary>
    public event Action OnAliveRestored;

    private void Awake ( )
    {
        _animator = GetComponent<Animator> ( );

        _attackLayerIndex = _animator.GetLayerIndex ( "Attack" );
        _upperStanceLayerIndex = _animator.GetLayerIndex ( "UpperStance" );

        _moveSpeedHash = Animator.StringToHash ( "MoveSpeed" );
        _moveXHash = Animator.StringToHash ( "MoveX" );
        _moveZHash = Animator.StringToHash ( "MoveZ" );
        _attackHash = Animator.StringToHash ( "Attack" );
        _skillHash = Animator.StringToHash ( "Skill" );
        _ultimateHash = Animator.StringToHash ( "Ultimate" );
        _ultimateReadyHash = Animator.StringToHash ( "UltimateReady" );
        _dieHash = Animator.StringToHash ( "Die" );
        _takeHitHash = Animator.StringToHash ( "TakeHit" );
        _isGroundedHash = Animator.StringToHash ( "IsGrounded" );
        _isKnockbackHash = Animator.StringToHash ( "IsKnockback" );
        _isDodgingHash = Animator.StringToHash ( "IsDodging" );

        _idleStateHash = Animator.StringToHash ( "Base Layer.Idle" );
        _runStateHash = Animator.StringToHash ( "Base Layer.Run" );
        _attackStateHash = Animator.StringToHash ( "Attack.Attack" );
        _dieStateHash = Animator.StringToHash ( "Base Layer.Die" );
        _attackIdleStateHash = Animator.StringToHash ( "Attack.Idle" );
        _hitStateHash = Animator.StringToHash ( "Attack.TakeHit" );
        _skillStateHash = Animator.StringToHash ( "Attack.Skill" );
        _ultimateStateHash = Animator.StringToHash ( "Base Layer.Ultimate" );
        _knockbackHitStateHash = Animator.StringToHash ( "Base Layer.TakeHit" );
        _jumpStateHash = Animator.StringToHash ( "Base Layer.Jump" );
        _dodgeStateHash = Animator.StringToHash ( "Base Layer.Dodge" );
    }

    /// <summary>
    /// 초기화 중 무기별 클립 교체 설정 적용
    /// </summary>
    /// <param name="controller">공통 Controller 기반의 무기별 설정</param>
    public void ApplyOverride ( AnimatorOverrideController controller )
    {
        _animator.runtimeAnimatorController = controller;
        _attackLayerIndex = _animator.GetLayerIndex ( "Attack" );
        _upperStanceLayerIndex = _animator.GetLayerIndex ( "UpperStance" );
    }

    /// <summary>
    /// 무기의 상체 대기 자세 사용 설정
    /// </summary>
    /// <param name="enabled">사용 여부</param>
    public void SetUpperStance ( bool enabled )
    {
        _useUpperStance = enabled;
        if ( enabled == false )
            ResetUpperStance ( );
    }

    /// <summary>
    /// 활에만 전후 이동용 상체 자세가 포함된 상태를 선택
    /// </summary>
    /// <param name="useBowReady">활 전용 자세를 사용할지 여부</param>
    internal void SelectUpperStance ( bool useBowReady )
    {
        if ( _upperStanceLayerIndex < 0 )
            return;

        int state = Animator.StringToHash ( useBowReady
            ? "UpperStance.BowMoveReady"
            : "UpperStance.MoveReady" );
        if ( _animator.HasState ( _upperStanceLayerIndex , state ) )
            _animator.Play ( state , _upperStanceLayerIndex , 0f );
    }

    /// <summary>
    /// 활을 장착할 때 기본 공격 조준에 사용할 상체 본과 방향 연결
    /// </summary>
    /// <param name="readDirection">조준 방향 제공 함수. 활 이외 무기에서는 null</param>
    internal void SetBowAttackAim ( Func<Vector3> readDirection )
    {
        RestoreBowAttackPose ( );
        _readBowAimDirection = null;
        if ( readDirection == null )
            return;

        _bowAimSpine = _animator.GetBoneTransform ( HumanBodyBones.Spine );
        _bowJoint = Array.Find (
            _animator.GetComponentsInChildren<Transform> ( true ) , bone => bone.name == "bowJoint" );
        _bowStringJoint = _bowJoint == null ? null : _bowJoint.Find ( "stringJoint" );
        if ( _bowAimSpine == null || _bowStringJoint == null || _bowJoint.IsChildOf ( _bowAimSpine ) == false )
        {
            Debug.LogWarning ( "활 기본 공격 조준에 필요한 척추·활·시위 본을 찾지 못했습니다." , this );
            return;
        }

        _readBowAimDirection = readDirection;
    }

    /// <summary>
    /// 새 초기화의 표현 상태와 위치 기준 설정
    /// </summary>
    /// <param name="position">주체의 월드 위치</param>
    public void ResetPresentation ( Vector3 position )
    {
        _isPresentationActive = true;
        ResetUpperStance ( );
        _animator.SetBool ( _isKnockbackHash , false );
        _animator.SetBool ( _isDodgingHash , false );
        _animator.SetBool ( _isGroundedHash , true );

        //이전 회차의 신호를 닫고 시작 상태를 준비
        ResetUltimatePresentation ( );
        ResetAttackLayer ( );
        //Play 요청 직후가 아닌 실제 Base Idle 진입 이후에 새 행동 허용
        _isReturning = true;
        _isDead = false;
        _isWaitingDeathEnd = false;
        _isRestoringAlive = false;
        _previousPosition = position;

        ResetMoveParameters ( );
        _animator.SetBool ( _dieHash , false );
        _animator.Play ( _idleStateHash , 0 , 0f );
    }

    /// <summary>
    /// Hero의 기본 공격 상태에 재생 배율 적용
    /// </summary>
    /// <param name="multiplier">기본 공격 재생 배율</param>
    public void SetBasicAttackSpeed ( float multiplier )
    {
        _animator.SetFloat ( "AttackSpeed" , multiplier );
    }

    /// <summary>
    /// 실제 수평 이동 속도와 방향을 애니메이터에 전달
    /// </summary>
    /// <param name="owner">이동 방향의 기준이 되는 캐릭터 루트</param>
    /// <param name="canMove">일반 이동 표현 허용 여부</param>
    /// <param name="deltaTime">프레임 경과 시간</param>
    public void UpdateMovement (
        Transform owner , bool canMove , float deltaTime )
    {
        Vector3 position = owner.position;
        Vector3 displacement = position - _previousPosition;
        //이동이 제한된 프레임도 위치 기준을 갱신하여 복구 시 속도 튐 방지
        _previousPosition = position;

        //일시정지 중에는 속도를 계산하지 않음
        if ( deltaTime <= 0f )
            return;

        displacement.y = 0f;

        if ( canMove == false )
        {
            ResetMoveParameters ( );
            return;
        }

        float speed = displacement.magnitude / deltaTime;
        Vector3 direction = owner.InverseTransformDirection ( displacement.normalized );
        _animator.SetFloat ( _moveSpeedHash , speed , MoveSpeedDampTime , deltaTime );
        _animator.SetFloat ( _moveXHash , direction.x , MoveDirectionDampTime , deltaTime );
        _animator.SetFloat ( _moveZHash , direction.z , MoveDirectionDampTime , deltaTime );
    }

    /// <summary>
    /// 이동 애니메이션 파라미터 초기화
    /// </summary>
    void ResetMoveParameters ( )
    {
        _animator.SetFloat ( _moveSpeedHash , 0f );
        _animator.SetFloat ( _moveXHash , 0f );
        _animator.SetFloat ( _moveZHash , 0f );
    }

    /// <summary>
    /// 상체 일반 피격 재생 요청
    /// </summary>
    /// <returns>피격 트리거 설정 여부</returns>
    public bool PlayTakeHit ( )
    {
        if ( CanStartAction == false || _hitPhase != HitPhase.None
            || _isUltimateReady || _isReadyReturning
            || IsKnockbackHit ( ) == true )
            return false;

        _hitPhase = HitPhase.Requested;
        _animator.SetLayerWeight ( _attackLayerIndex , 1f );
        _animator.SetTrigger ( _takeHitHash );
        return true;
    }

    /// <summary>
    /// 전신 넉백 피격 재생 또는 진입 여부
    /// </summary>
    bool IsKnockbackHit ( )
    {
        if ( _animator.GetCurrentAnimatorStateInfo ( 0 ).fullPathHash
            == _knockbackHitStateHash )
            return true;

        if ( _animator.IsInTransition ( 0 ) == false )
            return false;

        return _animator.GetNextAnimatorStateInfo ( 0 ).fullPathHash
            == _knockbackHitStateHash;
    }

    /// <summary>
    /// 새 행동 시작 전에 상체 피격과 준비 자세 정리
    /// </summary>
    void InterruptAttackLayer ( )
    {
        if ( _hitPhase != HitPhase.None || _isUltimateReady || _isReadyReturning )
            ResetAttackLayer ( );
    }

    /// <summary>
    /// 넉백 진행 여부를 애니메이터에 전달
    /// </summary>
    /// <param name="isKnockback">넉백 표현 진행 여부</param>
    public void UpdateKnockback ( bool isKnockback )
    {
        _animator.SetBool ( _isKnockbackHash , isKnockback );
    }

    /// <summary>
    /// 전투 행동 취소 이후 넉백 피격 재생
    /// </summary>
    public void PlayKnockbackHit ( )
    {
        ResetUpperStance ( );

        //궁극기 취소가 같은 프레임에 요청한 Idle 복귀도 피격 전환으로 대체
        bool requestTransition = _isUltimateReturning
            || IsKnockbackHit ( ) == false
            || _animator.IsInTransition ( 0 );

        //호출 전에 Hero가 전투 로직과 종료 이벤트 처리를 완료
        ResetUltimatePresentation ( );
        ResetAttackLayer ( );

        //취소된 궁극기의 Idle 복귀를 더 이상 기다리지 않음
        _isReturning = false;

        //안정된 피격 재생은 유지하고, 전환 중이면 이전 복귀 요청보다 피격을 우선
        if ( requestTransition == true )
        {
            _animator.CrossFadeInFixedTime (
                _knockbackHitStateHash , 0.05f , 0 );
        }

        if ( _showDebugLog == true )
            Debug.Log ( $"넉백 피격 표현: 대상={name} / TakeHit 전환 요청={requestTransition}" , this );
    }

    /// <summary>
    /// 승인된 점프의 공중 표현 시작
    /// </summary>
    public void PlayJump ( )
    {
        ResetUpperStance ( );
        InterruptAttackLayer ( );
        _animator.SetBool ( _isGroundedHash , false );
    }

    /// <summary>
    /// 승인된 회피의 애니메이터 진입 요청
    /// </summary>
    /// <param name="debugController">로그를 켠 플레이어의 이동 콜라이더</param>
    public void StartDodge ( CharacterController debugController )
    {
        ResetUpperStance ( );
        InterruptAttackLayer ( );
        _animator.SetBool ( _isDodgingHash , true );

        if ( debugController == null )
            return;

        _dodgeLogController = debugController;
        _dodgeLogHips = _animator.isHuman
            ? _animator.GetBoneTransform ( HumanBodyBones.Hips )
            : null;
        _dodgeLogQuarter = 0;
        _dodgeLog = new StringBuilder ( $"회피 높이: 대상={debugController.name}" );
        AppendDodgeSample ( "시작" );
    }

    /// <summary>
    /// 회피 종료 전달과 필요한 기본 상태 복귀 대기
    /// </summary>
    /// <param name="waitForReturn">기본 상태 복귀를 기다릴지 여부</param>
    /// <param name="position">회피 종료 후 실제 위치</param>
    public void EndDodge ( bool waitForReturn , Vector3 position )
    {
        _animator.SetBool ( _isDodgingHash , false );

        //마지막 회피 이동량을 일반 이동 속도로 계산해 Run으로 복귀하지 않도록 기준 갱신
        _previousPosition = position;
        ResetMoveParameters ( );

        //넉백/사망/비활성화는 각 기존 처리 경로의 표현을 유지
        if ( waitForReturn == true )
            _isReturning = true;

        if ( _dodgeLog != null )
        {
            float progress = ReadDodgeProgress ( );
            AppendDodgeSample ( progress >= 0f ? $"종료 {progress:P0}" : "종료 Dodge 아님" );
            Debug.Log ( _dodgeLog.ToString ( ) , this );
            _dodgeLog = null;
            _dodgeLogController = null;
            _dodgeLogHips = null;
        }
    }

    /// <summary>
    /// 실제 이동 결과의 접지 여부 전달
    /// </summary>
    /// <param name="isGrounded">현재 접지 여부</param>
    public void UpdateGrounded ( bool isGrounded )
    {
        _animator.SetBool ( _isGroundedHash , isGrounded );
    }

    /// <summary>
    /// 승인된 기본 공격 재생
    /// </summary>
    /// <param name="attackId">시작 승인된 공격 회차</param>
    public void PlayAttack ( int attackId )
    {
        InterruptAttackLayer ( );

        //시작 알림에서 받은 회차를 보관하고 클립 이벤트에서 그대로 반환
        _attackId = attackId;
        _isAttackActive = true;

        //Base 이동은 계속 재생하고 상체에만 기본 공격 적용
        _animator.SetLayerWeight ( _attackLayerIndex , 1f - ReadBowWalkAimWeight ( ) );
        _animator.ResetTrigger ( _attackHash );
        _animator.ResetTrigger ( _skillHash );
        _animator.SetTrigger ( _attackHash );
    }

    /// <summary>
    /// 종료한 공격의 이벤트 수신과 재생 정리
    /// </summary>
    /// <param name="attackId">종료한 공격 회차</param>
    /// <param name="reason">종료 사유</param>
    public void StopAttack ( int attackId , AttackEndReason reason )
    {
        if ( attackId != _attackId )
            return;

        //이전 클립의 이벤트부터 차단하고 상체 복귀를 기다림
        _isAttackActive = false;
        _isActionReturning = true;
        _animator.ResetTrigger ( _attackHash );

        //정상 완료는 Controller의 상체 대기 상태 복귀 전환 사용
        if ( reason == AttackEndReason.Completed )
            return;

        //사망과 비활성화는 주체의 별도 표현 처리 사용
        if ( reason == AttackEndReason.Died
            || reason == AttackEndReason.Disabled )
            return;

        //일반 취소만 상체 Idle로 전환하고, Weight 해제는 복귀 완료 후 처리
        _animator.CrossFadeInFixedTime (
            _attackIdleStateHash , 0.05f , _attackLayerIndex );
    }

    /// <summary>
    /// 승인된 스킬 재생
    /// </summary>
    /// <param name="skillId">시작 승인된 스킬 회차</param>
    public void PlaySkill ( int skillId )
    {
        InterruptAttackLayer ( );

        //기본 공격 회차와 별도로 스킬 재생의 원래 회차 보관
        _skillId = skillId;
        _isSkillActive = true;

        _animator.SetLayerWeight ( _attackLayerIndex , 1f );
        _animator.ResetTrigger ( _attackHash );
        _animator.ResetTrigger ( _skillHash );
        _animator.SetTrigger ( _skillHash );
    }

    /// <summary>
    /// 종료한 스킬의 이벤트 수신과 재생 정리
    /// </summary>
    /// <param name="skillId">종료한 스킬 회차</param>
    /// <param name="reason">종료 사유</param>
    public void StopSkill ( int skillId , AttackEndReason reason )
    {
        if ( skillId != _skillId )
            return;

        //로직에서 취소된 재생의 잔여 타격/완료 신호부터 차단
        _isSkillActive = false;
        _isActionReturning = true;
        _animator.ResetTrigger ( _skillHash );

        //정상 완료는 Controller의 상체 대기 상태 복귀 전환 사용
        if ( reason == AttackEndReason.Completed )
            return;

        //사망과 비활성화는 전체 표현 정리 경로 사용
        if ( reason == AttackEndReason.Died
            || reason == AttackEndReason.Disabled )
            return;

        //일반 취소는 상체만 복귀시켜 Base 이동 상태를 유지
        _animator.CrossFadeInFixedTime (
            _attackIdleStateHash , 0.05f , _attackLayerIndex );
    }

    /// <summary>
    /// 공격 클립의 타격 신호 전달
    /// </summary>
    /// <param name="animationEvent">타격 이벤트의 재생 정보</param>
    public void AnimationAttackHit ( AnimationEvent animationEvent )
    {
        //취소 여부와 신호를 보낸 상태를 확인한 뒤 전투 모듈에 타격 승인 요청
        if ( _isAttackActive == false )
            return;

        if ( animationEvent.animatorStateInfo.fullPathHash != _attackStateHash )
            return;

        //회차와 단발 타격 승인은 Attacker가 검사
        OnAttackHit?.Invoke ( _attackId );
    }

    /// <summary>
    /// 공격 클립의 완료 신호 전달
    /// </summary>
    /// <param name="animationEvent">완료 이벤트의 재생 정보</param>
    public void AnimationAttackEnd ( AnimationEvent animationEvent )
    {
        if ( _isAttackActive == false )
            return;

        if ( animationEvent.animatorStateInfo.fullPathHash
            != _attackStateHash )
            return;

        int attackId = _attackId;

        //완료 알림에서 다음 행동을 요청해도 복귀 전에는 거절
        _isAttackActive = false;
        _isActionReturning = true;

        OnAttackCompleted?.Invoke ( attackId );
    }

    /// <summary>
    /// 스킬 클립의 타격 신호 전달
    /// </summary>
    /// <param name="animationEvent">타격 이벤트의 재생 정보</param>
    public void AnimationSkillHit ( AnimationEvent animationEvent )
    {
        if ( _isSkillActive == false )
            return;

        if ( animationEvent.animatorStateInfo.fullPathHash
            != _skillStateHash )
            return;

        //피해 실행과 회차별 중복 타격 검사는 SkillCaster에서 수행
        OnSkillHit?.Invoke ( _skillId );
    }

    /// <summary>
    /// 스킬 클립의 완료 신호 전달
    /// </summary>
    /// <param name="animationEvent">완료 이벤트의 재생 정보</param>
    public void AnimationSkillEnd ( AnimationEvent animationEvent )
    {
        if ( _isSkillActive == false )
            return;

        if ( animationEvent.animatorStateInfo.fullPathHash
            != _skillStateHash )
            return;

        int skillId = _skillId;

        //완료 알림으로 로직이 끝나도 상체 복귀 전까지 다음 행동은 제한
        _isSkillActive = false;
        _isActionReturning = true;

        OnSkillCompleted?.Invoke ( skillId );
    }

    /// <summary>
    /// 상체 궁극기 준비 자세 재생
    /// </summary>
    public void StartUltimateReady ( )
    {
        //상체 피격이나 이전 준비 복귀가 진행 중이면 레이어를 먼저 정리
        InterruptAttackLayer ( );

        _isUltimateReady = true;
        _animator.SetLayerWeight ( _attackLayerIndex , 1f );
        _animator.SetBool ( _ultimateReadyHash , true );
    }

    /// <summary>
    /// 궁극기 준비 자세의 복귀 요청
    /// </summary>
    public void StopUltimateReady ( )
    {
        if ( _isUltimateReady == false )
            return;

        _isUltimateReady = false;
        _isReadyReturning = true;
        _animator.SetBool ( _ultimateReadyHash , false );
    }

    /// <summary>
    /// 승인된 궁극기의 전신 재생
    /// </summary>
    /// <param name="ultimateId">시작 승인된 궁극기 회차</param>
    public void PlayUltimate ( int ultimateId )
    {
        //상체 레이어가 전신 궁극기를 덮지 않도록 먼저 Weight와 잔여 신호 정리
        ResetUpperStance ( );
        ResetAttackLayer ( );

        _ultimateId = ultimateId;
        _isUltimateActive = true;

        //이동 제한은 이 재생 상태와 SkillCaster 상태를 보고 Hero가 반영
        _animator.ResetTrigger ( _ultimateHash );
        _animator.SetTrigger ( _ultimateHash );
    }

    /// <summary>
    /// 종료한 궁극기의 이벤트 수신과 재생 정리
    /// </summary>
    /// <param name="ultimateId">종료한 궁극기 회차</param>
    /// <param name="reason">종료 사유</param>
    public void StopUltimate ( int ultimateId , AttackEndReason reason )
    {
        if ( ultimateId != _ultimateId )
            return;

        _isUltimateActive = false;
        _animator.ResetTrigger ( _ultimateHash );

        if ( reason == AttackEndReason.Died
            || reason == AttackEndReason.Disabled )
        {
            //사망/비활성화는 별도 정리 경로를 사용하므로 궁극기 복귀 알림 생략
            _isUltimateReturning = false;
            return;
        }

        //완료 또는 일반 취소 후에도 Base 복귀까지 이동 제한 유지
        _isUltimateReturning = true;
        _isReturning = true;

        //정상 완료는 Controller의 복귀 전환 사용
        if ( reason == AttackEndReason.Completed )
            return;

        _animator.CrossFadeInFixedTime ( _idleStateHash , 0.05f , 0 );
    }

    /// <summary>
    /// 궁극기 클립의 타격 신호 전달
    /// </summary>
    /// <param name="animationEvent">타격 이벤트의 재생 정보</param>
    public void AnimationUltimateHit ( AnimationEvent animationEvent )
    {
        if ( _isUltimateActive == false )
            return;

        if ( animationEvent.animatorStateInfo.fullPathHash
            != _ultimateStateHash )
            return;

        //회전 모션의 신호만 전달하며 주변 피해 판정은 실행자가 담당
        OnUltimateHit?.Invoke ( _ultimateId );
    }

    /// <summary>
    /// 궁극기 클립의 완료 신호 전달
    /// </summary>
    /// <param name="animationEvent">완료 이벤트의 재생 정보</param>
    public void AnimationUltimateEnd ( AnimationEvent animationEvent )
    {
        if ( _isUltimateActive == false )
            return;

        if ( animationEvent.animatorStateInfo.fullPathHash
            != _ultimateStateHash )
            return;

        int ultimateId = _ultimateId;

        //사용 모듈 종료 알림 전에 이동 복귀 대기 확정
        _isUltimateActive = false;
        _isUltimateReturning = true;
        _isReturning = true;

        OnUltimateCompleted?.Invoke ( ultimateId );
    }

    /// <summary>
    /// 사망 상태 재생 요청
    /// </summary>
    public void SetDead ( )
    {
        ResetUpperStance ( );
        _animator.SetBool ( _isKnockbackHash , false );
        _animator.SetBool ( _isDodgingHash , false );

        //취소된 클립의 이벤트를 닫아 사망 이후 추가 타격/완료 전달 방지
        ResetUltimatePresentation ( );
        //상체 행동을 제거한 뒤 전신 사망 모션 사용
        ResetAttackLayer ( );
        _isReturning = false;
        _isRestoringAlive = false;
        _isDead = true;
        _isWaitingDeathEnd = true;

        ResetMoveParameters ( );
        _animator.SetBool ( _dieHash , true );
    }

    /// <summary>
    /// 사망 클립의 완료 신호 전달
    /// </summary>
    /// <param name="animationEvent">사망 완료 이벤트의 재생 정보</param>
    public void AnimationDeathEnd ( AnimationEvent animationEvent )
    {
        if ( _isWaitingDeathEnd == false )
            return;

        if ( animationEvent.animatorStateInfo.fullPathHash != _dieStateHash )
            return;

        //신호 대기를 먼저 닫고 Hero에 부활 대기 또는 소진 판정 요청
        _isWaitingDeathEnd = false;
        OnDeathCompleted?.Invoke ( );
    }

    /// <summary>
    /// 부활 후 기본 상태로 복귀
    /// </summary>
    /// <param name="position">부활한 주체의 월드 위치</param>
    public void RestoreAlive ( Vector3 position )
    {
        _isPresentationActive = true;
        ResetUpperStance ( );
        _animator.SetBool ( _isKnockbackHash , false );
        _animator.SetBool ( _isDodgingHash , false );
        _animator.SetBool ( _isGroundedHash , true );

        ResetUltimatePresentation ( );
        ResetAttackLayer ( );

        _isDead = false;
        _isWaitingDeathEnd = false;
        _isReturning = true;
        //HP 복구와 행동 복구를 구분하여 Base 복귀 이후에만 완료 알림
        _isRestoringAlive = true;
        _previousPosition = position;

        _animator.SetBool ( _dieHash , false );
        ResetMoveParameters ( );
        _animator.CrossFadeInFixedTime ( _idleStateHash , 0.05f , 0 );
    }

    /// <summary>
    /// 상체 행동의 신호와 레이어 적용 상태 정리
    /// </summary>
    void ResetAttackLayer ( )
    {
        RestoreBowAttackPose ( );

        //Animator를 변경하기 전에 이전 기본 공격/스킬 신호부터 무효화
        _attackId = 0;
        _skillId = 0;
        _isAttackActive = false;
        _isSkillActive = false;
        _isActionReturning = false;
        _isUltimateReady = false;
        _isReadyReturning = false;
        _hitPhase = HitPhase.None;

        _animator.SetBool ( _ultimateReadyHash , false );
        _animator.ResetTrigger ( _attackHash );
        _animator.ResetTrigger ( _skillHash );
        _animator.ResetTrigger ( _takeHitHash );
        //대기 중에는 Base Layer의 상체 모션을 그대로 사용
        _animator.SetLayerWeight ( _attackLayerIndex , 0f );

        //비활성화 경로에서도 호출되므로 상태 재생은 활성 Animator에만 요청
        if ( _animator.isActiveAndEnabled == true )
        {
            _animator.Play (
                _attackIdleStateHash , _attackLayerIndex , 0f );
        }
    }

    /// <summary>
    /// 이전 궁극기의 표현 상태 정리
    /// </summary>
    void ResetUltimatePresentation ( )
    {
        //초기화/사망/재활성화는 이전 궁극기를 이어 재생하지 않음
        _ultimateId = 0;
        _isUltimateActive = false;
        _isUltimateReturning = false;

        _animator.ResetTrigger ( _ultimateHash );
    }

    /// <summary>
    /// 주체 비활성화 시 이전 행동 표현 정리
    /// </summary>
    public void StopPresentation ( )
    {
        _isPresentationActive = false;
        ResetUpperStance ( );
        _animator.SetBool ( _isKnockbackHash , false );
        _animator.SetBool ( _isDodgingHash , false );

        //Hero의 전투 취소 처리보다 먼저 호출되어 이전 클립 신호를 차단
        ResetUltimatePresentation ( );
        ResetAttackLayer ( );

        ResetMoveParameters ( );

        if ( _isDead == true )
            return;

        _isReturning = true;

        if ( _animator.isActiveAndEnabled == true )
            _animator.Play ( _idleStateHash , 0 , 0f );
    }

    /// <summary>
    /// 생존 주체 재활성화 시 남은 행동 표현 정리
    /// </summary>
    /// <param name="position">재활성화한 주체의 월드 위치</param>
    public void ResumePresentation ( Vector3 position )
    {
        _isPresentationActive = true;
        ResetUpperStance ( );
        _animator.SetBool ( _isKnockbackHash , false );
        _animator.SetBool ( _isDodgingHash , false );
        _animator.SetBool ( _isGroundedHash , true );

        //생존자는 대기 상태로 복귀하고, 사망자는 기존 사망 표현을 유지
        ResetUltimatePresentation ( );
        ResetAttackLayer ( );

        _previousPosition = position;

        if ( _isDead == true )
            return;

        _isReturning = true;

        _animator.Play ( _idleStateHash , 0 , 0f );
    }

    /// <summary>
    /// 실제 Base 상태가 바뀐 프레임에만 로그 출력
    /// </summary>
    void LogBaseStateChanged ( )
    {
        if ( _showDebugLog == false )
            return;

        int stateHash = _animator.GetCurrentAnimatorStateInfo ( 0 ).fullPathHash;

        if ( stateHash == _previousBaseStateHash )
            return;

        if ( _previousBaseStateHash == _knockbackHitStateHash )
            Debug.Log ( $"애니메이션 상태: 대상={name} / TakeHit 이탈" , this );

        if ( _previousBaseStateHash == _jumpStateHash )
            Debug.Log ( $"애니메이션 상태: 대상={name} / Jump 이탈" , this );

        if ( _previousBaseStateHash == _dodgeStateHash )
            Debug.Log ( $"애니메이션 상태: 대상={name} / Dodge 이탈" , this );

        if ( stateHash == _knockbackHitStateHash )
            Debug.Log ( $"애니메이션 상태: 대상={name} / TakeHit 진입" , this );

        if ( stateHash == _jumpStateHash )
            Debug.Log ( $"애니메이션 상태: 대상={name} / Jump 진입" , this );

        if ( stateHash == _dodgeStateHash )
            Debug.Log ( $"애니메이션 상태: 대상={name} / Dodge 진입" , this );

        _previousBaseStateHash = stateHash;
    }

    /// <summary>
    /// 방향이 바뀔 때 실제 재생 중인 이동 클립 기록
    /// </summary>
    void LogMoveClips ( )
    {
        if ( _showDebugLog == false )
            return;

        float x = _animator.GetFloat ( _moveXHash );
        float z = _animator.GetFloat ( _moveZHash );
        if ( Mathf.Abs ( x ) < 0.9f && Mathf.Abs ( z ) < 0.9f )
        {
            _lastMoveLog = default;
            return;
        }

        int direction = Mathf.Abs ( x ) > Mathf.Abs ( z )
            ? ( x > 0f ? 1 : -1 )
            : ( z > 0f ? 2 : -2 );

        if ( _animator.IsInTransition ( 0 )
            || _animator.IsInTransition ( _attackLayerIndex ) )
            return;

        int baseState = _animator.GetCurrentAnimatorStateInfo ( 0 ).fullPathHash;
        int attackState = _animator.GetCurrentAnimatorStateInfo ( _attackLayerIndex ).fullPathHash;
        var current = (direction, baseState, attackState);
        if ( current == _lastMoveLog )
            return;

        _lastMoveLog = current;
        bool isRun = baseState == _runStateHash;
        float attackWeight = _animator.GetLayerWeight ( _attackLayerIndex );
        Debug.Log ( $"이동 클립: X={x:F2}, Z={z:F2}, Run={isRun}"
            + $" / Base={DescribeClips ( 0 )}"
            + $" / Attack({attackWeight:F2})={DescribeClips ( _attackLayerIndex )}" , this );
    }

    /// <summary>
    /// 레이어의 현재 클립 이름과 가중치 표시
    /// </summary>
    string DescribeClips ( int layer )
    {
        AnimatorClipInfo[] clips = _animator.GetCurrentAnimatorClipInfo ( layer );
        return string.Join ( ", " , Array.ConvertAll ( clips , info =>
        {
            AnimationClip clip = info.clip;
            return clip == null ? "Missing" : $"{clip.name} {info.weight:P0}";
        } ) );
    }

    /// <summary>
    /// 현재 회피 클립 진행률 조회
    /// </summary>
    float ReadDodgeProgress ( )
    {
        if ( _animator.isInitialized == false )
            return -1f;

        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo ( 0 );
        if ( state.fullPathHash != _dodgeStateHash && _animator.IsInTransition ( 0 ) )
            state = _animator.GetNextAnimatorStateInfo ( 0 );

        return state.fullPathHash == _dodgeStateHash ? state.normalizedTime : -1f;
    }

    /// <summary>
    /// Animator 평가 후 분기점의 회피 높이 수집
    /// </summary>
    void SampleDodge ( )
    {
        if ( _dodgeLog == null || _dodgeLogQuarter == 3 )
            return;

        float progress = ReadDodgeProgress ( );
        int quarter = Mathf.Min ( 3 , Mathf.FloorToInt ( progress * 4f ) );
        if ( quarter <= _dodgeLogQuarter )
            return;

        //저프레임에서 여러 구간을 건너뛰면 실제 평가된 자세만 기록
        _dodgeLogQuarter = quarter;
        AppendDodgeSample ( $"{progress:P0}" );
    }

    /// <summary>
    /// 루트 높이와 골반 상대 높이, 접지 기록
    /// </summary>
    void AppendDodgeSample ( string label )
    {
        float rootY = _dodgeLogController.transform.position.y;
        string hipsY = _dodgeLogHips == null
            ? "없음"
            : ( _dodgeLogHips.position.y - rootY ).ToString ( "F3" );

        _dodgeLog.Append (
            $" / {label}: rootY={rootY:F3}, hipsY={hipsY}, grounded={_dodgeLogController.isGrounded}" );
    }

    /// <summary>
    /// 일반 피격 진입과 복귀 확인
    /// </summary>
    void UpdateTakeHit ( )
    {
        if ( _hitPhase == HitPhase.None )
            return;

        int stateHash = _animator
            .GetCurrentAnimatorStateInfo ( _attackLayerIndex ).fullPathHash;

        //트리거가 소비되기 전의 Idle을 복귀로 처리하지 않습니다.
        if ( _hitPhase == HitPhase.Requested )
        {
            if ( stateHash == _hitStateHash )
                _hitPhase = HitPhase.Playing;

            return;
        }

        if ( stateHash == _attackIdleStateHash
            && _animator.IsInTransition ( _attackLayerIndex ) == false )
        {
            _hitPhase = HitPhase.None;
            _animator.SetLayerWeight ( _attackLayerIndex , 0f );
        }
    }

    /// <summary>
    /// 걷기 상태에 맞춰 상체 공격 준비 자세를 전환
    /// </summary>
    void UpdateUpperStance ( )
    {
        if ( _useUpperStance == false || _isPresentationActive == false
            || _upperStanceLayerIndex < 0 )
            return;

        int state = ReadUpperStanceState ( );
        float weight = _animator.GetLayerWeight ( _upperStanceLayerIndex );
        if ( state == 0 )
        {
            if ( weight > 0f )
                ResetUpperStance ( );

            return;
        }

        float target = state == _runStateHash ? 1f : 0f;
        if ( weight != target )
        {
            float next = Mathf.MoveTowards ( weight , target , Time.deltaTime / UpperStanceBlendTime );
            _animator.SetLayerWeight ( _upperStanceLayerIndex , next );
        }
    }

    /// <summary>
    /// 상체 자세의 적용 기준이 되는 이동 상태 조회
    /// </summary>
    int ReadUpperStanceState ( )
    {
        if ( _isDead || _isReturning || IsUltimatePlaying
            || _animator.GetBool ( _isDodgingHash )
            || _animator.GetBool ( _isKnockbackHash )
            || _animator.GetBool ( _isGroundedHash ) == false )
            return 0;

        int state = _animator.GetCurrentAnimatorStateInfo ( 0 ).fullPathHash;
        if ( IsLocomotionState ( state ) == false )
            return 0;

        //정지와 걷기 사이에서는 Base 전환의 목적지에 맞춰 Weight를 보간
        if ( _animator.IsInTransition ( 0 ) )
            state = _animator.GetNextAnimatorStateInfo ( 0 ).fullPathHash;

        return IsLocomotionState ( state ) ? state : 0;
    }

    bool IsLocomotionState ( int state )
    {
        return state == _idleStateHash || state == _runStateHash;
    }

    void ResetUpperStance ( )
    {
        if ( _upperStanceLayerIndex >= 0 )
            _animator.SetLayerWeight ( _upperStanceLayerIndex , 0f );
    }

    private void Update ( )
    {
        //애니메이터가 이번 프레임을 평가하기 전에 이전 보정을 원복
        RestoreBowAttackPose ( );
    }

    private void OnDisable ( )
    {
        RestoreBowAttackPose ( );
    }

    /// <summary>
    /// 전후 이동 중 앞을 겨누는 상체 보정 비중 조회
    /// </summary>
    /// <returns>앞뒤 이동은 1, 좌우 이동과 대기는 0</returns>
    float ReadBowWalkAimWeight ( )
    {
        if ( _readBowAimDirection == null || _upperStanceLayerIndex < 0
            || _animator.GetCurrentAnimatorStateInfo ( _upperStanceLayerIndex ).fullPathHash != BowMoveReadyStateHash )
            return 0f;

        float walkWeight = _animator.GetLayerWeight ( _upperStanceLayerIndex );
        float lateral = Mathf.Abs ( _animator.GetFloat ( _moveXHash ) );
        return walkWeight * ( 1f - Mathf.Clamp01 ( lateral ) );
    }

    /// <summary>
    /// 전후 이동과 기본 공격의 합성 자세에 상체 조준 회전을 적용
    /// </summary>
    void UpdateBowAttackPose ( )
    {
        if ( _readBowAimDirection == null || _isSkillActive || IsUltimatePlaying
            || _isUltimateReady || _isReadyReturning || _hitPhase != HitPhase.None )
            return;

        AnimatorStateInfo current = _animator.GetCurrentAnimatorStateInfo ( _attackLayerIndex );
        AnimatorStateInfo state = _animator.IsInTransition ( _attackLayerIndex )
            ? _animator.GetNextAnimatorStateInfo ( _attackLayerIndex )
            : current;
        if ( _isActionReturning && current.fullPathHash == _skillStateHash )
            return;

        float walkWeight = ReadBowWalkAimWeight ( );
        float weight = walkWeight;
        if ( _isAttackActive && state.fullPathHash == _attackStateHash )
        {
            //시위 당기기 전후에는 걷기 조준 자세와 섞고 발사 구간에는 기존 공격을 전부 사용
            float progress = state.normalizedTime;
            float attackIn = Mathf.SmoothStep ( 0f , 1f , progress / BowWalkAttackBlendInEnd );
            float attackOut = Mathf.SmoothStep ( 1f , 0f ,
                Mathf.InverseLerp ( BowWalkAttackBlendOutStart , BowAimBlendOutEnd , progress ) );
            _animator.SetLayerWeight ( _attackLayerIndex ,
                Mathf.Lerp ( 1f , attackIn * attackOut , walkWeight ) );

            //기본 활 클립은 약 49.5%에서 발사. 기존 공격 조준 보정은 유지
            float blendIn = Mathf.SmoothStep ( 0f , 1f , progress / BowAimBlendInEnd );
            float blendOut = Mathf.SmoothStep ( 1f , 0f ,
                Mathf.InverseLerp ( BowAimBlendOutStart , BowAimBlendOutEnd , progress ) );
            weight = Mathf.Max ( walkWeight , blendIn * blendOut );
        }

        if ( weight <= 0f )
            return;

        Vector3 bowForward = Vector3.ProjectOnPlane ( _bowJoint.position - _bowStringJoint.position , Vector3.up );
        Vector3 aimForward = Vector3.ProjectOnPlane ( _readBowAimDirection ( ) , Vector3.up );
        if ( bowForward.sqrMagnitude <= 0.0001f || aimForward.sqrMagnitude <= 0.0001f )
            return;

        float yaw = Vector3.SignedAngle ( bowForward , aimForward , Vector3.up );
        _bowAimOriginalRotation = _bowAimSpine.localRotation;
        _bowAimSpine.rotation = Quaternion.AngleAxis ( yaw * weight , Vector3.up ) * _bowAimSpine.rotation;
        _bowAimApplied = true;
    }

    /// <summary>
    /// 상체 본을 보정 전 로컬 회전으로 복원
    /// </summary>
    void RestoreBowAttackPose ( )
    {
        if ( _bowAimApplied == false )
            return;

        _bowAimSpine.localRotation = _bowAimOriginalRotation;
        _bowAimApplied = false;
    }

    private void LateUpdate ( )
    {
        //로그 함수의 조기 반환은 아래 기존 복귀 처리를 중단하지 않음
        LogBaseStateChanged ( );
        LogMoveClips ( );
        SampleDodge ( );
        UpdateTakeHit ( );
        UpdateUpperStance ( );
        UpdateBowAttackPose ( );

        //1. 공격/스킬 완료와 준비 취소는 Attack 레이어에서 확인
        //Base가 이미 Idle/Run이어도 상체 전환이 끝나기 전에는 복귀 완료가 아님
        if ( ( _isActionReturning || _isReadyReturning )
            && _animator.IsInTransition ( _attackLayerIndex ) == false )
        {
            int actionStateHash = _animator
                .GetCurrentAnimatorStateInfo ( _attackLayerIndex )
                .fullPathHash;

            if ( actionStateHash == _attackIdleStateHash )
            {
                //블렌딩을 끝낸 뒤 상체 적용을 해제하여 마지막 자세가 갑자기 끊기지 않게 함
                _animator.SetLayerWeight ( _attackLayerIndex , 0f );
                _isActionReturning = false;
                _isReadyReturning = false;
            }
        }

        //2. 초기화/부활/궁극기는 Base Layer의 전환 종료와 Idle/Run 진입을 함께 확인
        //회피도 코드 종료와 실제 표현 복귀를 구분하며 같은 확인으로 새 행동 허용
        if ( _isReturning == false )
            return;

        if ( _animator.IsInTransition ( 0 ) == true )
            return;

        int stateHash =
            _animator.GetCurrentAnimatorStateInfo ( 0 ).fullPathHash;

        if ( stateHash != _idleStateHash && stateHash != _runStateHash )
            return;

        //외부 알림 전에 복귀 상태부터 확정
        _isReturning = false;

        if ( _isRestoringAlive == true )
        {
            _isRestoringAlive = false;
            OnAliveRestored?.Invoke ( );
        }

        if ( _isUltimateReturning == true )
        {
            //궁극기 로직 종료와 구분된 최종 복귀 알림으로 Hero가 이동 허용을 재계산
            _isUltimateReturning = false;
            OnUltimateReturned?.Invoke ( );
        }
    }
}
