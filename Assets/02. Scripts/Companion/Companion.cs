using System;
using UnityEngine;

/// <summary>
/// 동료의 현재 배치 또는 추종 방식
/// </summary>
public enum CompanionMode
{
    Follow,     //플레이어 추종
    Defend      //배치점 기준 방어
}

/// <summary>
/// 동료 - 행동 진행과 전투/생명주기 연결
/// </summary>
public class Companion : MonoBehaviour
{
    [Header( "----- 디버그 -----" )]
    [SerializeField] bool _showDebugLog = true;     //일반 디버그 로그 출력 여부

    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] CompanionMover _mover;     //동료 이동 모듈
    [SerializeField] AnimatorHandler _animatorHandler;    //동료 애니메이션

    [SerializeField] CompanionTargetDetector _targetDetector;       //적 탐색 모듈
    [SerializeField] Health _health;        //체력 모듈
    [SerializeField] DamageReceiver _damageReceiver;        //피해 수신 모듈
    [SerializeField] Attacker _attacker;        //공격 모듈

    [SerializeField] StatusEffectController _statusEffects;    //상태이상 모듈
    [SerializeField] StatusResistanceData _statusResistance;  //상태 저항 설정
    [SerializeField] KnockbackController _knockback;           //강제 이동 모듈

    [SerializeField] SoundId _footstepSound = SoundId.CompanionFootstep;        //발소리 ID

    [Header( "----- 공격 -----" )]
    [SerializeField] GameObject _source;    //프리팹 내부의 피해 발생원

    [Header( "----- 고용 효과 -----" )]
    [SerializeField] VfxEffect _hireVfx;    //등장 성공 단발 효과

    CompanionData _data;        //동료 데이터
    SoundManager _sound; //공통 사운드 서비스
    SoundPlayer _soundPlayer; //동료 로컬 출력

    VfxManager _vfx;        //실행자에 전달할 VFX 서비스
    CharacterVfx _vfxPlayer;        //캐릭터 효과 연결
    CompanionCombatAnimationBinding _combatAnimationBinding;        //공격과 애니메이션 신호 연결

    Transform _hero;        //추종할 플레이어
    CompanionMode _mode;        //현재 배치 또는 추종 방식
    Vector3 _activityCenter;        //현재 활동과 복귀 기준 위치
    Vector3? _priorityPosition;    //도전별 공격 우선순위 기준 위치
    bool _isRetreating;    //확보 거리까지 후퇴 판단을 유지하는지 여부
    bool _retreatPathFailed;    //같은 후퇴 경로 실패의 로그 반복 방지

    CombatTarget _target;       //현재 추적하는 적
    Collider _targetCollider;    //현재 적의 조준 기준 피격 콜라이더
    CombatTarget _excludedTarget;    //다음 탐색에서 한 번 제외할 접근 실패 대상
    float _remainingSearchTime;     //다음 우선순위/후퇴 갱신까지 남은 시간
    bool _isInitialized;        //초기화 성공 여부
    bool _isDeploymentEnabled;    //고용 확정 이후 행동 허용 여부

    #region ----- 프로퍼티/이벤트 -----

    /// <summary>
    /// 전투와 이동 초기화 완료 여부
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// 초기화된 활성 생존 동료의 행동 가능 여부
    /// </summary>
    public bool CanAct =>
        _isInitialized
        && _isDeploymentEnabled
        && isActiveAndEnabled
        && _health.IsAlive
        && _statusEffects.IsStunned == false
        && _knockback.IsKnockback == false;

    /// <summary>
    /// 현재 배치 또는 추종 방식
    /// </summary>
    public CompanionMode Mode => _mode;

    /// <summary>
    /// 현재 추적하는 적
    /// </summary>
    public CombatTarget CurrentTarget => _target;

    /// <summary>
    /// 현재 활동과 복귀 기준 위치
    /// </summary>
    public Vector3 ActivityCenter => _activityCenter;

    /// <summary>
    /// 현재 이동 방식의 활동 반경
    /// </summary>
    float ActivityRadius => _mode == CompanionMode.Defend
        ? _data.DefendRadius : _data.ActivityRadius;

    /// <summary>
    /// 현재 모드의 복귀 정지 거리
    /// </summary>
    float ReturnDistance => _mode == CompanionMode.Follow
        ? _data.FollowDistance : _data.ArrivalDistance;

    /// <summary>
    /// 사망 이벤트(사망한 동료, 피해 정보)
    /// </summary>
    public event Action<Companion, DamageInfo> OnDied;

    /// <summary>
    /// 사망 연출 완료 이벤트(완료한 동료)
    /// </summary>
    public event Action<Companion> OnDeathPresentationCompleted;

    #endregion

    #region ----- 초기화 -----

    private void Awake ()
    {
        _soundPlayer = GetComponent<SoundPlayer>( );
        if ( _soundPlayer == null )
            _soundPlayer = gameObject.AddComponent<SoundPlayer>( );

        _vfxPlayer = GetComponent<CharacterVfx>( );
        if ( _vfxPlayer == null )
            _vfxPlayer = gameObject.AddComponent<CharacterVfx>( );

        //활성 수명의 구독 전에 연결 객체를 한 번 구성
        _combatAnimationBinding = new CompanionCombatAnimationBinding(
            this, _attacker, _animatorHandler,
            GetAttackTargetPosition, () => _showDebugLog );
    }

    /// <summary>
    /// 동료 전투와 고용 시 이동 방식 초기화
    /// </summary>
    /// <param name="data">동료 설정</param>
    /// <param name="player">추종할 플레이어, 배치형에서는 생략 가능</param>
    /// <param name="placementPoint">월드 배치점, 없으면 플레이어 추종</param>
    /// <param name="source">검 등 실제 피해 발생원</param>
    /// <param name="activate">행동 및 피격 가능 여부</param>
    /// <param name="upgrades">이번 도전의 HP/직접 피해 증가율</param>
    /// <param name="priorityPosition">공격 우선순위 기준, 없으면 기존 근접 탐색</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        CompanionData data, Transform player,
        Vector3? placementPoint, GameObject source,
        bool activate = true, CompanionUpgradeStats upgrades = default,
        Vector3? priorityPosition = null )
    {
        if ( _showDebugLog == true )
            Debug.Log(
                $"[동료:{name}] 초기화 시작 / 현재 위치:{transform.position}"
                + $" / 전달받은 배치점:{placementPoint}", this );

        //초기화는 기존 공격이 중지된 상태에서 호출
        _isInitialized = false;
        _soundPlayer.StopAll( );
        _vfxPlayer.Clear( );
        _isDeploymentEnabled = false;
        ClearTargetState( );
        _mover.SetMoveEnabled( false );
        _attacker.SetAttackEnabled( false );

        if ( placementPoint.HasValue == false && player == null )
        {
            Debug.LogError( "추종형 동료는 플레이어 참조가 필요합니다.", this );
            return false;
        }

        //이동 방식 설정
        CompanionMode mode = placementPoint.HasValue == true
            ? CompanionMode.Defend : CompanionMode.Follow;
        //활동 중심 설정
        Vector3 activityCenter = placementPoint ?? player.position;
        //능력치 설정
        CombatStatsData stats = data.CombatStatsData;

        //피해와 공격은 같은 Health를 참조하여
        //HP와 생존 상태를 중복 관리하지 않음
        if ( _damageReceiver.Init(
            gameObject, _health,
            Faction.Ally, stats.DamageReductionRate ) == false )
            return false;

        //Init이 무적을 초기화하므로 이후 설정, HP 초기화 알림 중에도 준비 객체 보호
        _damageReceiver.SetInvulnerable( true );

        //프리팹에 연결된 실행자로 초기화하고 이전 실행은 Attacker 내부에서 정리
        if ( _attacker.Init(
            gameObject, _health, source,
            ActorType.Companion, Faction.Ally, data.AttackData,
            upgrades.DamageBonus ) == false )
            return false;

        //실행자의 조준 기능은 초기화 때 한 번 조회
        _attacker.Executor.SetSound( _sound );
        _attacker.Executor.SetVfx( _vfx );
        _combatAnimationBinding.Init( );

        //배치 위치 적용과 이동 준비 후 HP 초기화와 행동 허용
        float activityRadius = mode == CompanionMode.Defend
            ? data.DefendRadius : data.ActivityRadius;
        if ( _mover.Init( data, activityCenter, placementPoint, activityRadius ) == false )
            return false;

        if ( _statusEffects.Init(
            this, _health, _damageReceiver, _mover, _statusResistance ) == false )
            return false;

        //NavMesh 이동 준비 이후 강제 이동 모듈도 같은 Health에 연결
        if ( _knockback.Init(
            this, _health, _damageReceiver, _mover ) == false )
            return false;

        //기존 효과와 넉백 정리 이후 HP 초기화
        _animatorHandler.ResetPresentation( transform.position );
        float maxHp = stats.MaxHp * ( 1f + upgrades.HpBonus );
        _health.Init( maxHp );

        if ( _health.IsAlive == false || _health.MaxHp != maxHp )
            return false;

        _data = data;
        _hero = player;
        _mode = mode;
        _priorityPosition = priorityPosition;

        _activityCenter = activityCenter;
        _isInitialized = true;

        if ( activate == true )
            ConfirmHire( );

        if ( _showDebugLog == true )
            Debug.Log(
                $"[동료:{name}] 초기화 완료 / 모드:{_mode}"
                + $" / 활동 중심:{_activityCenter} / 행동 가능:{CanAct}", this );
        return true;
    }

    /// <summary>
    /// 프리팹 내부의 피해 발생원으로 행동을 보류한 상태에서 동료 초기화
    /// </summary>
    /// <param name="data">동료 설정</param>
    /// <param name="player">추종할 플레이어</param>
    /// <param name="placementPoint">방어 기준점, 없으면 추종</param>
    /// <param name="upgrades">이번 도전의 HP/직접 피해 증가율</param>
    /// <param name="priorityPosition">공격 우선순위 기준 위치</param>
    /// <returns>초기화 성공 여부</returns>
    public bool PrepareHire (
        CompanionData data, Transform player, Vector3? placementPoint,
        CompanionUpgradeStats upgrades = default, Vector3? priorityPosition = null )
    {
        return Init(
            data, player, placementPoint,
            _source, false, upgrades, priorityPosition );
    }

    /// <summary>
    /// 동료 사운드 서비스 연결
    /// </summary>
    /// <param name="sound">공통 관리자</param>
    public void SetSound ( SoundManager sound )
    {
        _sound = sound;
        _soundPlayer.Init( sound );
    }

    /// <summary>
    /// 캐릭터 VFX 서비스 연결
    /// </summary>
    /// <param name="vfx">씬 재생 서비스</param>
    public void SetVfx ( VfxManager vfx )
    {
        _vfx = vfx;
        _vfxPlayer.Init( vfx );
    }

    /// <summary>
    /// 실제 일반 이동의 발소리 요청
    /// </summary>
    void HandleFootstep ()
    {
        if ( CanAct == false || _mover.HasMovedThisFrame == false )
            return;

        _soundPlayer.Play( _footstepSound );
    }

    /// <summary>
    /// 현재 행동 모션의 효과음 요청
    /// </summary>
    /// <param name="id">사운드 식별자</param>
    void HandleSound ( SoundId id )
    {
        if ( _isInitialized == false || _health.IsAlive == false )
            return;

        _soundPlayer.Play( id );
    }

    /// <summary>
    /// 초기화와 비용 처리가 끝난 동료의 행동과 피격 허용
    /// </summary>
    public void ConfirmHire ()
    {
        _isDeploymentEnabled = true;
        _damageReceiver.SetInvulnerable( false );
        RefreshActionState( );
        _vfxPlayer.RefreshEffects( _statusEffects );
    }

    /// <summary>
    /// 등장 확정 후 고용 효과 재생
    /// </summary>
    public void PlayHireEffect ()
    {
        _vfxPlayer.Play( _hireVfx );
    }

    /// <summary>
    /// 종료 정산 대기 중 행동과 남은 공격 실행을 중단하고 객체 유지
    /// </summary>
    public void StopStageActions ()
    {
        _isInitialized = false;
        _isDeploymentEnabled = false;

        ClearTargetState( );
        _damageReceiver.SetInvulnerable( true );
        _mover.SetMoveEnabled( false );
        _attacker.SetAttackEnabled( false );
        _animatorHandler.StopPresentation( );

        //현재 공격 회차와 이미 발사된 실행은 별도로 정리
        _attacker.CancelExecutions( );
        _attacker.CancelAttack( AttackEndReason.Canceled );
        //초기화 플래그가 해제되어 종료 알림에서 행동을 다시 갱신하지 않음
        _knockback.CancelKnockback( );
        _soundPlayer.StopAll( );
        _vfxPlayer.Clear( );
    }

    private void OnEnable ()
    {
        _animatorHandler.OnFootstep += HandleFootstep;
        _animatorHandler.OnSound += HandleSound;
        _damageReceiver.OnDamaged += HandleDamaged;
        _damageReceiver.OnDied += HandleDied;
        _combatAnimationBinding.Bind( );
        _attacker.OnFirstValidHit += HandleFirstValidHit;
        _statusEffects.OnEffectChanged += HandleEffectChanged;
        _knockback.OnKnockbackChanged += HandleKnockbackChanged;
        _animatorHandler.OnDeathCompleted += HandleDeathPresentationCompleted;

        if ( _isInitialized == false )
            return;

        //기존 전투 상태를 유지하며 행동과 표현 복구
        _animatorHandler.ResumePresentation( transform.position );
        RefreshActionState( );

        if ( _isDeploymentEnabled == true && _health.IsAlive == true )
            _vfxPlayer.RefreshEffects( _statusEffects );
    }

    #endregion

    private void Update ()
    {
        if ( _isInitialized == false )
            return;

        float deltaTime = Time.deltaTime;

        if ( deltaTime <= 0f )
            return;

        //기절 중에도 효과 시간은 갱신
        _statusEffects.UpdateEffects( deltaTime );

        if ( _health.IsAlive == false )
            return;

        _attacker.UpdateAttack( deltaTime );

        //화상/기절 시간과 공격 쿨타임은 넉백 중에도 갱신
        //강제 이동 종료 프레임은 일반 AI를 생략하고 다음 프레임에 재판단
        if ( _knockback.IsKnockback == true )
        {
            _knockback.UpdateKnockback( deltaTime );
            return;
        }

        if ( CanAct == false )
            return;

        UpdateBehavior( deltaTime );
    }

    private void LateUpdate ()
    {
        if ( _isInitialized == false )
            return;

        _animatorHandler.UpdateMovement(
            transform, CanAct, Time.deltaTime );
    }

    #region ----- 이동 -----

    /// <summary>
    /// 공통 이동과 탐색을 진행한 뒤 현재 상황에 맞는 행동 실행
    /// </summary>
    /// <param name="deltaTime">프레임 경과 시간</param>
    void UpdateBehavior ( float deltaTime )
    {
        _remainingSearchTime -= deltaTime;

        //공격 요청 전 정지 완료, 모션 중 대상 교체와 일반 행동 판단 보류
        if ( _attacker.IsAttacking == true )
            return;

        if ( _mode == CompanionMode.Follow )
        {
            if ( _hero == null || _hero.gameObject.activeInHierarchy == false )
            {
                ClearTargetState( );

                if ( _mover.IsMoving == true )
                    _mover.Stop( );

                return;
            }

            _activityCenter = _hero.position;
        }

        _mover.SetActivityCenter( _activityCenter, ActivityRadius );

        //갱신된 중심 기준으로 범위를 벗어났으면 추적보다 복귀 우선
        if ( _mover.IsAt( _activityCenter, ActivityRadius ) == false )
        {
            ClearTargetState( );
            ReturnToAnchor( );
            _mover.UpdateMove( deltaTime, false );
            return;
        }

        //복귀 경로는 우회할 수 있고, 적 추적/후퇴는 실제 이동 반경을 제한
        //이번 프레임의 일반 이동 결과는 여기서 한 번만 반영
        bool moved = _mover.UpdateMove(
            deltaTime, _target != null || _isRetreating );

        UpdateTargetTracking( moved );
        UpdateAction( deltaTime );
    }

    /// <summary>
    /// 이동 결과와 대상 유효성을 반영하고 필요한 탐색 수행
    /// </summary>
    /// <param name="movementSucceeded">일반 이동 유지 가능 여부</param>
    void UpdateTargetTracking ( bool movementSucceeded )
    {
        if ( movementSucceeded == false && _isRetreating == false )
        {
            //추가 이동 실패에서도 기존 제외 대상 보존
            ExcludeCurrentTarget( );
        }
        else if ( _target != null
            && ( IsTargetColliderValid( ) == false
                || _targetDetector.IsValidTarget( _target, transform.position, _data.DetectionRadius ) == false ) )
        {
            SetTarget( null, null, "대상 상실/탐색 범위 이탈" );
            _remainingSearchTime = 0f;
        }

        if ( _remainingSearchTime <= 0f )
            SearchTarget( );
    }

    #endregion

    #region ----- 공격 -----

    /// <summary>
    /// 유효 대상과 사거리가 확인된 상태에서 공격 요청
    /// </summary>
    /// <param name="deltaTime">프레임 경과 시간</param>
    void TryAttackTarget ( float deltaTime )
    {
        if ( _animatorHandler.CanStartAction == false )
            return;

        //공격 직전에 방향을 맞추고,
        //시작 이후에는 위의 공격 진행 검사에서 회전 차단
        if ( _mover.Face( _target.Position, deltaTime ) == false )
            return;

        _attacker.TryAttack( );
    }

    /// <summary>
    /// 상태이상 표현과 행동 제한 반영
    /// </summary>
    /// <param name="type">변경된 효과 종류</param>
    void HandleEffectChanged ( StatusEffectType type )
    {
        //초기화/종료/사망 정리 알림은 표현과 행동 처리에서 함께 제외
        if ( _isInitialized == false || _health.IsAlive == false )
            return;

        if ( _isDeploymentEnabled == true )
            _vfxPlayer.RefreshEffects( _statusEffects );

        //모든 상태의 표현 갱신 후 기존 기절 행동 처리
        if ( type != StatusEffectType.Stun )
            return;

        RefreshActionState( );

        if ( _statusEffects.IsStunned == true )
            _attacker.CancelAttack( AttackEndReason.Canceled );
    }

    /// <summary>
    /// 적중 시점에 사용할 현재 대상의 피격 중심 조회
    /// </summary>
    /// <returns>현재 조준 위치, 대상이 사라졌으면 null</returns>
    Vector3? GetAttackTargetPosition ()
    {
        //탐색 이후 공격 모션 중 대상이 사망하거나 콜라이더가 사라질 수 있음
        if ( _target == null || _target.IsValid == false
            || IsTargetColliderValid( ) == false )
            return null;

        return _targetCollider.bounds.center;
    }

    /// <summary>
    /// 실제 피해가 발생한 공격 확인용 로그
    /// </summary>
    /// <param name="context">유효 피해가 발생한 공격 정보</param>
    void HandleFirstValidHit ( AttackContext context )
    {
        if ( _showDebugLog == true )
            Debug.Log( $"[동료:{name}] 유효 타격 " +
                $"/ 회차:{context.AttackId} / 실제 피해 발생", this );
    }

    /// <summary>
    /// 생존 중 유효 피격의 표현 요청
    /// </summary>
    /// <param name="damageInfo">피해 정보</param>
    /// <param name="appliedDamage">실제 적용 피해량</param>
    void HandleDamaged ( DamageInfo damageInfo, float appliedDamage )
    {
        if ( _isInitialized == true && _health.IsAlive == true )
        {
            _soundPlayer.Play( SoundId.CompanionHurt );
            _vfxPlayer.PlayHit( );
        }

        if ( CanAct == false )
            return;

        _animatorHandler.PlayTakeHit( );
    }

    /// <summary>
    /// 공격 우선 대상과 가까운 위협 탐색
    /// </summary>
    void SearchTarget ()
    {
        Vector3 center = transform.position;
        float searchRadius = _data.DetectionRadius;
        if ( _data.CanRetreat == true )
        {
            searchRadius = Mathf.Max( searchRadius,
                Mathf.Max( _data.RetreatStartDistance, _data.RetreatDistance ) );
        }

        //접근 실패 대상은 이번 공격 후보 선택에서만 제외
        CombatTarget excludedTarget = _excludedTarget;
        _excludedTarget = null;

        CombatTarget target = _targetDetector.FindTargets(
            center, searchRadius, _data.DetectionRadius,
            _priorityPosition ?? center, out Collider targetCollider,
            out CombatTarget nearestTarget, excludedTarget );
        SetTarget( target, targetCollider, "우선순위 탐색" );

        _remainingSearchTime = _data.SearchInterval;
        UpdateRetreat( nearestTarget );
    }

    /// <summary>
    /// 현재 대상을 다음 탐색에서 한 번 제외하고 재탐색 예약
    /// </summary>
    void ExcludeCurrentTarget ()
    {
        //대상이 이미 없으면 이전에 예약한 제외 정보 유지
        if ( _target != null )
            _excludedTarget = _target;

        SetTarget( null, null, "접근 경로 실패 / 다음 탐색에서 한 번 제외" );
        _remainingSearchTime = 0f;
    }

    #endregion

    #region ----- 피격 -----

    /// <summary>
    /// 동료 사망에 따른 이동과 공격 중단
    /// </summary>
    /// <param name="damageInfo">사망 원인 정보</param>
    void HandleDied ( DamageInfo damageInfo )
    {
        if ( _showDebugLog == true )
            Debug.Log( $"[동료:{name}] 사망 " +
                $"/ 이동과 공격 중단 / 이후 AI 갱신 중단", this );

        //현재 행동 단계와 관계없이
        //사망 이벤트에서 즉시 중단하고 진행 공격 취소
        ClearTargetState( );
        _mover.SetMoveEnabled( false );
        _attacker.SetAttackEnabled( false );
        _attacker.CancelAttack( AttackEndReason.Died );
        _animatorHandler.SetDead( );

        //내부 사망 처리와 재생 요청 이후 고용 관리자에 전달
        _soundPlayer.StopAll( );
        _vfxPlayer.Clear( );
        _sound.PlayAt( SoundId.CompanionDeath, transform.position );
        _vfxPlayer.PlayDeath( );
        OnDied?.Invoke( this, damageInfo );
    }

    /// <summary>
    /// 애니메이션 모듈이 확정한 사망 완료 전달
    /// </summary>
    void HandleDeathPresentationCompleted ()
    {
        OnDeathPresentationCompleted?.Invoke( this );
    }

    /// <summary>
    /// 넉백 상태에 따른 행동 제한과 피격 표현 연결
    /// </summary>
    /// <param name="isKnockback">넉백 진행 여부</param>
    void HandleKnockbackChanged ( bool isKnockback )
    {
        //초기화/종료/사망 함수에서 이미 행동과 표현을 정리하는 중
        if ( _isInitialized == false || _health.IsAlive == false )
            return;

        _animatorHandler.UpdateKnockback( isKnockback );

        //시작 시 이동/공격 제한, 종료 시 남은 기절과 생존 상태 반영
        RefreshActionState( );

        if ( isKnockback == false )
            return;

        //공격 종료 콜백의 모션 정리를 먼저 완료
        _attacker.CancelAttack( AttackEndReason.Canceled );

        if ( _health.IsAlive == false )
            return;

        _animatorHandler.PlayKnockbackHit( );
    }

    #endregion

    #region ----- 공용 -----

    /// <summary>
    /// 후퇴, 복귀, 접근, 정지 공격 순서로 행동 진행
    /// </summary>
    /// <param name="deltaTime">프레임 경과 시간</param>
    void UpdateAction ( float deltaTime )
    {
        if ( _isRetreating == true )
        {
            //경로 도착은 후퇴 판단 종료와 별개
            if ( _mover.HasReachedDestination == true )
                _mover.Stop( );

            if ( _mover.IsMoving == false
                && _target != null
                && _mover.IsAt( _target.Position, _data.AttackRange ) == true )
            {
                TryAttackTarget( deltaTime );
            }

            return;
        }

        if ( _target == null )
        {
            ReturnToAnchor( );
            return;
        }

        if ( _mover.IsAt( _target.Position, _data.AttackRange ) == false )
        {
            if ( _mover.MoveToAttackRange(
                _target.Position, _data.AttackRange ) == false )
            {
                //실패 경로는 이동 모듈이 정리, 다음 공통 탐색에서 재판단
                ExcludeCurrentTarget( );
            }

            return;
        }

        //사거리 안에서 쿨타임을 기다릴 때도 이동은 중단
        if ( _mover.IsMoving == true )
            _mover.Stop( );

        TryAttackTarget( deltaTime );
    }

    /// <summary>
    /// 현재 상태에 맞는 동료 행동 허용 반영
    /// </summary>
    void RefreshActionState ()
    {
        bool canAct = CanAct;

        _mover.SetMoveEnabled( canAct );
        _attacker.SetAttackEnabled( canAct );

        if ( canAct == false )
        {
            //기절/넉백 중 대상은 보존하고 후퇴 판단만 해제
            SetRetreating( false, "행동 제한" );
            _remainingSearchTime = 0f;
        }
    }

    /// <summary>
    /// 가까운 위협을 기준으로 후퇴 판단과 경로 갱신
    /// </summary>
    /// <param name="nearestTarget">가장 가까운 위협, 없으면 null</param>
    void UpdateRetreat ( CombatTarget nearestTarget )
    {
        bool wasRetreating = _isRetreating;
        bool retreating = false;

        if ( _data.CanRetreat == true && nearestTarget != null )
        {
            float distance =
                ( nearestTarget.Position - transform.position ).sqrMagnitude;
            //후퇴 시작 후에는 확보 거리까지 후퇴 판단 유지
            retreating = wasRetreating == true
                ? distance < _data.RetreatDistance * _data.RetreatDistance
                : distance <= _data.RetreatStartDistance * _data.RetreatStartDistance;
        }

        SetRetreating( retreating, nearestTarget == null ? "주변 위협 없음" : "거리 조건" );

        if ( _isRetreating == true )
        {
            bool pathFailed = _mover.MoveAwayFrom( nearestTarget.Position ) == false;
            if ( _showDebugLog == true && _retreatPathFailed != pathFailed )
            {
                Debug.Log( $"[동료:{name}] 후퇴 경로 {( pathFailed == true ? "확보 실패" : "확보 복구" )}"
                    + $" / 위협 위치:{nearestTarget.Position} / 활동 중심:{_activityCenter}"
                    + $" / 이동 반경:{ActivityRadius} / 확보 거리:{_data.RetreatDistance}", this );
            }
            _retreatPathFailed = pathFailed;
        }
        else if ( wasRetreating == true && _mover.IsMoving == true )
        {
            //확보 거리 충족 또는 위협 상실로 기존 후퇴 이동 중단
            _mover.Stop( );
        }
    }

    /// <summary>
    /// 현재 모드의 기준점으로 복귀하거나 정지 거리 안에서 대기
    /// </summary>
    void ReturnToAnchor ()
    {
        bool wasMoving = _mover.IsMoving;

        if ( _mover.IsAt( _activityCenter, ReturnDistance ) == true )
        {
            if ( wasMoving == true )
                _mover.Stop( );

            return;
        }

        bool requested = _mode == CompanionMode.Follow
            ? _mover.MoveToFollow( _activityCenter )
            : _mover.MoveTo( _activityCenter, false );

        if ( requested == false && wasMoving == true )
        {
            Debug.LogWarning(
                $"[동료:{name}] 복귀/추종 이동 중단"
                + $" / 경로 요청 실패 / 활동 중심:{_activityCenter}", this );
        }
    }

    /// <summary>
    /// 기존 전투 상태를 유지한 채 방어 위치 변경
    /// </summary>
    /// <param name="position">공간 검사를 통과한 배치점</param>
    /// <returns>위치 변경 성공 여부</returns>
    public bool TrySetPlacement ( Vector3 position )
    {
        //행동 보류 중인 Prepared는 허용하고 사망/강제 행동 제한은 거절
        if ( isActiveAndEnabled == false || _isInitialized == false
            || _health.IsAlive == false || _statusEffects.IsStunned == true
            || _knockback.IsKnockback == true )
            return false;

        if ( _mover.TryReposition( position ) == false )
            return false;

        _mode = CompanionMode.Defend;
        _activityCenter = position;
        ClearTargetState( );
        _mover.SetActivityCenter( position, ActivityRadius );
        _mover.Stop( );
        //순간 이동 거리가 이동 애니메이션 속도로 반영되지 않도록 기준 갱신
        _animatorHandler.UpdateMovement( transform, false, Time.deltaTime );
        return true;
    }

    /// <summary>
    /// 현재 타겟의 피격 콜라이더 사용 가능 여부 확인
    /// </summary>
    /// <returns>활성화된 피격 콜라이더가 있는지 여부</returns>
    bool IsTargetColliderValid ()
    {
        return _targetCollider != null
            && _targetCollider.enabled
            && _targetCollider.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// 대상과 조준 콜라이더를 함께 반영하고 대상 변경 때만 출력
    /// </summary>
    /// <param name="target">새 대상, 없으면 null</param>
    /// <param name="collider">새 대상의 피격 콜라이더</param>
    /// <param name="reason">선택/해제 이유</param>
    void SetTarget ( CombatTarget target, Collider collider, string reason )
    {
        if ( _showDebugLog == true && _target != target )
        {
            string before = _target == null ? "없음" : _target.name;
            string after = target == null ? "없음" : target.name;
            Debug.Log( $"[동료:{name}] 공격 대상 변경 / {before} → {after} / {reason}"
                + $" / 우선순위 기준:{( _priorityPosition.HasValue == true ? _priorityPosition.Value.ToString( ) : "동료 위치" )}", this );
        }
        _target = target;
        _targetCollider = collider;
    }

    /// <summary>
    /// 후퇴 판단을 반영하고 시작/종료 때만 출력
    /// </summary>
    /// <param name="retreating">후퇴 판단 유지 여부</param>
    /// <param name="reason">전환 이유</param>
    void SetRetreating ( bool retreating, string reason )
    {
        if ( _showDebugLog == true && _isRetreating != retreating )
            Debug.Log( $"[동료:{name}] 후퇴 {( retreating == true ? "시작" : "종료" )} / {reason}", this );

        _isRetreating = retreating;
        if ( retreating == false )
            _retreatPathFailed = false;
    }

    /// <summary>
    /// 대상, 후퇴 판단과 다음 탐색 정보 해제
    /// </summary>
    void ClearTargetState ()
    {
        //종료/재배치에서는 예약된 제외 대상까지 해제하고 다음 판단부터 새로 탐색
        SetTarget( null, null, "초기화/사망/종료/재배치 또는 활동 범위 이탈" );
        _excludedTarget = null;
        _remainingSearchTime = 0f;
        SetRetreating( false, "대상/행동 정리" );
    }

    /// <summary>
    /// 점유 기록 반영 후 이전 공격 회차 취소
    /// </summary>
    public void CompletePlacement ()
    {
        _attacker.CancelAttack( AttackEndReason.Canceled );
    }

    private void OnDisable ()
    {
        _animatorHandler.OnFootstep -= HandleFootstep;
        _animatorHandler.OnSound -= HandleSound;
        //전투 취소보다 먼저 이전 클립의 이벤트 수신 차단
        _animatorHandler.StopPresentation( );
        _damageReceiver.OnDamaged -= HandleDamaged;
        _damageReceiver.OnDied -= HandleDied;
        _combatAnimationBinding.StopListening( );
        _statusEffects.OnEffectChanged -= HandleEffectChanged;
        _knockback.OnKnockbackChanged -= HandleKnockbackChanged;

        ClearTargetState( );
        _mover.SetMoveEnabled( false );
        _attacker.SetAttackEnabled( false );

        //Companion 컴포넌트만 꺼진 경우도 남은 실행 무효화
        _attacker.CancelExecutions( );
        _attacker.CancelAttack( AttackEndReason.Disabled );
        _knockback.CancelKnockback( );
        //취소에 따른 공격 종료 표현까지 반영한 뒤 남은 구독 해제
        _combatAnimationBinding.Unbind( );
        _attacker.OnFirstValidHit -= HandleFirstValidHit;
        _animatorHandler.OnDeathCompleted -= HandleDeathPresentationCompleted;
        _soundPlayer.StopAll( );
        _vfxPlayer.Clear( );
    }

    #endregion

}
