using System;
using UnityEngine;

/// <summary>
/// 공격 모듈 - 공격 시작, 회차, 쿨타임 및 타격 승인 관리
/// </summary>
public class Attacker : MonoBehaviour
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] AttackExecutor _attackExecutor;        //타격 모듈

    Health _health;         //초기화 시 전달받은 공격자의 체력 모듈
    GameObject _owner;      //실제 공격 주체
    GameObject _source;     //피해 발생원(무기 등)
    ActorType _actorType;       //공격자의 종류
    Faction _faction;       //공격자의 진영
    AttackData _attackData;     //공격 설정

    AttackContext _attackContext;       //이번 공격의 실행 정보

    float _attackInterval;      //공격 간격
    float _damageBonus;         //초기화에서 전달받은 직접 피해 증가율
    float _remainingCooldown;       //남은 쿨타임

    int _currentAttackId;       //가장 최근에 생성한 공격 회차

    bool _isStarting;       //시작 처리와 알림 진행 여부
    bool _isAttacking;      //공격 진행 상태
    bool _hasCommittedHit;      //타격 승인 여부
    bool _isExecutingHit;       //타격 결과 처리 진행 여부
    bool _attackEnabled = true;     //외부 행동 제한에 따른 공격 허용 여부

    /// <summary>
    /// 공격 시작 가능 여부
    /// </summary>
    public bool CanAttack =>
        isActiveAndEnabled &&
        _attackEnabled &&
        _health.IsAlive &&
        IsBusy == false &&
        _remainingCooldown <= 0f;

    /// <summary>
    /// 시작 처리, 공격 동작 또는 타격 결과 처리 여부
    /// </summary>
    public bool IsBusy => _isStarting || _isAttacking || _isExecutingHit;

    /// <summary>
    /// 공격 진행 여부
    /// </summary>
    public bool IsAttacking => _isAttacking;

    /// <summary>
    /// 가장 최근에 생성한 공격 회차
    /// </summary>
    public int CurrentAttackId => _currentAttackId;

    /// <summary>
    /// 연결된 기본 공격 실행자
    /// </summary>
    public AttackExecutor Executor => _attackExecutor;

    /// <summary>
    /// 상위 초기화에서 기존 실행을 정리한 뒤 기본 공격 실행자 연결
    /// </summary>
    /// <param name="executor">선택한 무기의 실행자</param>
    public void SetExecutor ( AttackExecutor executor )
    {
        _attackExecutor = executor;
    }

    /// <summary>
    /// 공격 시작 이벤트(공격 실행 정보)
    /// </summary>
    public event Action<AttackContext> OnAttackStarted;

    /// <summary>
    /// 공격 종료 이벤트(공격 회차, 종료 사유)
    /// </summary>
    public event Action<int, AttackEndReason> OnAttackEnded;

    /// <summary>
    /// 최초 유효 적중 이벤트(공격 실행 정보)
    /// </summary>
    public event Action<AttackContext> OnFirstValidHit;

    /// <summary>
    /// 공격 설정 초기화
    /// </summary>
    /// <param name="owner">공격자 오브젝트</param>
    /// <param name="health">체력 모듈</param>
    /// <param name="source">피해 발생원</param>
    /// <param name="actorType">주체 종류</param>
    /// <param name="faction">소속 진영</param>
    /// <param name="attackData">공격 설정</param>
    /// <param name="damageBonus">직접 피해 증가율</param>
    /// <param name="attackSpeedBonus">기본 공격속도 증가율</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        GameObject owner, Health health, GameObject source,
        ActorType actorType, Faction faction, AttackData attackData,
        float damageBonus = 0f, float attackSpeedBonus = 0f )
    {
        //처리 중인 공격 정보를 초기화로 덮어쓰지 않도록 검사
        if ( IsBusy == true )
        {
            Debug.LogError( "공격 처리 중에는 초기화할 수 없습니다.", this );
            return false;
        }

        float attackInterval = attackData.AttackInterval / ( 1f + attackSpeedBonus );

        //잘못된 간격으로 쿨타임이 종료되지 않는 상황 방지
        if ( attackInterval < 0f )
        {
            Debug.LogError( "공격 간격은 0 이상이어야 합니다.", this );
            return false;
        }

        _attackExecutor.CancelExecutions ( );

        //검사를 통과한 설정만 함께 반영
        _owner = owner;
        _health = health;
        _source = source;
        _actorType = actorType;
        _faction = faction;
        _attackData = attackData;
        _attackInterval = attackInterval;
        _damageBonus = damageBonus;

        _remainingCooldown = 0f;
        _hasCommittedHit = false;

#if UNITY_EDITOR
        //기즈모도 기존 공격 설정을 사용하며 Inspector에 수치를 중복하지 않음
        if ( _attackExecutor is AreaAttackExecutor areaExecutor )
            areaExecutor.SetGizmoRange ( attackData.Radius , attackData.Angle );
#endif

        //재초기화 후에도 이전 공격과 회차가 겹치지 않도록 유지
        return true;
    }

    /// <summary>
    /// 외부 행동 제한에 따른 공격 허용 여부 설정
    /// </summary>
    /// <param name="enabled">공격 허용 여부</param>
    public void SetAttackEnabled ( bool enabled )
    {
        _attackEnabled = enabled;
    }

    /// <summary>
    /// 공격 쿨타임 갱신
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    public void UpdateAttack ( float deltaTime )
    {
        if ( deltaTime <= 0f )
            return;

        //공격 진행 여부와 별개로 시작 시점의 쿨타임을 감소
        _remainingCooldown = Mathf.Max(
            0f, _remainingCooldown - deltaTime );
    }

    /// <summary>
    /// 공격 시작 요청
    /// </summary>
    /// <returns>공격 시작 여부</returns>
    public bool TryAttack ()
    {
        //진행 중 입력은 저장하지 않고 거절
        if ( CanAttack == false )
            return false;

        _currentAttackId++;
        int attackId = _currentAttackId;

        //시작 시점의 설정을 보존해 이번 공격에 사용
        _attackContext = new AttackContext(
            _owner, _source,
            _actorType, _faction,
            AttackType.BasicAttack,
            _currentAttackId,
            _attackData.RawDamage * ( 1f + _damageBonus ),
            _attackData.Radius,
            _attackData.Angle );

        //외부에서 시작 신호를 받기 전에 회차와 진행 상태 확정
        _remainingCooldown = _attackInterval;
        _hasCommittedHit = false;
        _isAttacking = true;

        _isStarting = true;
        bool startNotified = false;

        try
        {
            //공격 시작 이벤트 발행
            OnAttackStarted?.Invoke ( _attackContext );
            startNotified = true;
            return true;
        }
        finally
        {
            bool notifyCanceled =
                startNotified == false
                && _isAttacking == true
                && _currentAttackId == attackId;

            //시작 연동 오류는 진행 상태를 정리하고 종료 알림
            if ( notifyCanceled == true )
                _isAttacking = false;

            _isStarting = false;

            if ( notifyCanceled == true )
                OnAttackEnded?.Invoke ( attackId , AttackEndReason.Canceled );
        }
    }

    /// <summary>
    /// 타격 승인 및 실행
    /// </summary>
    /// <param name="attackId">타격할 공격 회차</param>
    /// <returns>타격 승인 여부</returns>
    public bool TryCommitHit ( int attackId )
    {
        //공격자의 현재 상태 확인
        if ( isActiveAndEnabled == false
            || _attackEnabled == false
            || _health.IsAlive == false )
            return false;

        //종료 또는 취소된 공격과 이전 회차의 신호 제외
        if ( _isAttacking == false || attackId != _currentAttackId )
            return false;

        if ( _hasCommittedHit == true || _isExecutingHit == true )
            return false;

        //피해 이벤트에서 같은 회차가 다시 요청되기 전에 승인 기록
        _hasCommittedHit = true;
        _isExecutingHit = true;

        AttackContext context = _attackContext;

        try
        {
            //일반 취소/사망은 승인된 실행을 유지하고 실행 무효화는 후속 피해 중단
            _attackExecutor.Execute ( context , HandleFirstValidHit );

            //빗나감 여부와 별개로 실행 승인 결과 반환
            return true;
        }
        finally
        {
            //실행 또는 이벤트에서 오류가 발생해도 실행 상태 해제
            _isExecutingHit = false;
        }
    }

    /// <summary>
    /// 공격 정상 종료
    /// </summary>
    /// <param name="attackId">종료할 공격 회차</param>
    public void CompleteAttack ( int attackId )
    {
        //이전 재생의 종료 신호가 새 공격을 종료하지 않도록 검사
        if ( _isAttacking == false || attackId != _currentAttackId )
            return;

        EndAttack( AttackEndReason.Completed );
    }

    /// <summary>
    /// 공격 취소
    /// </summary>
    /// <param name="reason">취소 사유</param>
    public void CancelAttack ( AttackEndReason reason )
    {
        //정상 종료는 회차를 검사하는 별도 API로 요청
        if ( reason == AttackEndReason.Completed )
        {
            Debug.LogError( "정상 종료는 CompleteAttack을 사용해야 합니다.", this );
            return;
        }

        if ( _isAttacking == false )
            return;

        EndAttack( reason );
    }

    /// <summary>
    /// 공격 종료 상태 반영 및 알림
    /// </summary>
    /// <param name="reason">종료 사유</param>
    void EndAttack ( AttackEndReason reason )
    {
        int attackId = _currentAttackId;

        //종료 이벤트에서 같은 공격을 다시 종료하지 않도록 먼저 반영
        _isAttacking = false;

        //종료하거나 취소해도 남은 쿨타임 유지
        OnAttackEnded?.Invoke( attackId, reason );
    }

    /// <summary>
    /// 원래 실행의 최초 유효 적중 전달
    /// </summary>
    /// <param name="context">원래 공격 실행 정보</param>
    void HandleFirstValidHit ( AttackContext context )
    {
        OnFirstValidHit?.Invoke ( context );
    }

    /// <summary>
    /// 아직 종료되지 않은 공격 실행 정리
    /// </summary>
    public void CancelExecutions ( )
    {
        //최초 초기화 전에는 실행자가 아직 연결되지 않을 수 있음
        _attackExecutor?.CancelExecutions ( );
    }

    private void OnDisable ()
    {
        //비활성화된 공격은 늦은 타격 신호를 승인하지 않음
        CancelAttack( AttackEndReason.Disabled );
        CancelExecutions ( );
    }
}
