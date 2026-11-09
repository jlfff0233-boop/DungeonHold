using System;
using UnityEngine;

/// <summary>
/// 히어로 - 이동, 공격, 사망, 부활
/// </summary>
public class Hero : MonoBehaviour
{
    [Header( "----- 디버그 -----" )]
    [SerializeField] bool _showDebugLog = true;     //기본 공격 에너지 충전과 피격 표현 로그 출력 여부

    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] Mover _mover;      //이동 모듈
    [SerializeField] AnimatorHandler _animatorHandler;      //플레이어 애니메이션
    [SerializeField] Health _health;        //체력 모듈
    [SerializeField] DamageReceiver _damageReceiver;        //피해 수신 모듈
    [SerializeField] MinimapTarget _minimapTarget;    //플레이어 미니맵 대상

    [SerializeField] Attacker _attacker;        //공격 관리 모듈
    [SerializeField] Energy _energy;        //에너지 모듈
    [SerializeField] SkillCaster _skillCaster;      //스킬 모듈
    [SerializeField] SkillCaster _ultimateCaster;       //궁극기 사용 모듈
    [SerializeField] StatusEffectController _statusEffects;     //상태이상 모듈
    [SerializeField] StatusResistanceData _statusResistance;        //상태 저항 설정
    [SerializeField] KnockbackController _knockback;        //강제 이동 모듈

    [Header( "----- 스킬 -----" )]
    [SerializeField] EnergyData _energyData;        //에너지 설정

    [Header ( "----- 궁극기 범위 표시 -----" )]
    [SerializeField] HeroUltimateAimPreview _ultimateAimPreviewPrefab;    //조준 표시 프리팹

    [Header( "----- 무기 -----" )]
    [SerializeField] HeroWeaponBinding [ ] _weaponBindings;     //캐릭터의 타입별 무기 연결

    [Header( "----- 부활 -----" )]
    [SerializeField] PlayerReviveData _reviveData;      //플레이어 부활 설정
    [SerializeField] Transform _revivePoint;        //지정 부활 위치, 미지정 시 사망 위치

    [Header( "----- 회피 -----" )]
    [SerializeField] DodgeData _dodgeData;      //회피 설정
    [SerializeField] SoundId _footstepSound = SoundId.HeroFootstep; //발소리 ID

    HeroWeaponSetup _weaponSetup;       //무기 구성 조회와 적용
    SoundManager _sound; //공통 사운드 서비스
    SoundPlayer _soundPlayer; //캐릭터 로컬 출력
    VfxManager _vfx; //실행자에 전달할 VFX 서비스
    CharacterVfx _vfxPlayer; //캐릭터 효과 연결

    bool _isInitialized;        //전투 초기화 성공 여부
    bool _isResettingStageResources;        //스테이지 자원 초기화 중 여부

    HeroRevive _revive;     //사망/부활 상태와 자동 부활 처리
    HeroCombatAnimationBinding _combatAnimation;        //전투 모듈과 애니메이션 연결
    HeroDodge _dodge;       //회피 상태와 무적/이동 관리
    ItemUseContext _itemUseContext;        //아이템 효과에 전달할 대상 모듈
    HeroUltimateAim _ultimateAim;       //궁극기 범위 지정
    int _lastUltimateInputFrame = -1;   //같은 프레임의 확정 입력 중복 방지


    #region ----- 프로퍼티/이벤트 -----

    /// <summary>
    /// 일반 행동 허용 여부
    /// </summary>
    public bool CanAct =>
        _isInitialized
        && isActiveAndEnabled
        && _health.IsAlive
        && _revive.IsWaitingReviveEnd == false
        && _isResettingStageResources == false
        && _statusEffects.IsStunned == false
        && _knockback.IsKnockback == false
        && _dodge.IsDodging == false;

    /// <summary>
    /// 스킬 사용 가능 여부
    /// </summary>
    public bool CanUseSkill =>
        CanStartNewAction( )
        && _mover.IsGrounded
        && _skillCaster.CanUse;

    /// <summary>
    /// 궁극기 사용 가능 여부
    /// </summary>
    public bool CanUseUltimate =>
        CanStartNewAction( )
        && _mover.IsGrounded
        && _ultimateCaster.CanUse;

    /// <summary>
    /// 현재 적용한 무기 설정
    /// </summary>
    public WeaponData WeaponData => _weaponSetup.WeaponData;

    /// <summary>
    /// 기본 스킬 모듈
    /// </summary>
    public SkillCaster SkillCaster => _skillCaster;

    /// <summary>
    /// 궁극기 모듈
    /// </summary>
    public SkillCaster UltimateCaster => _ultimateCaster;

    /// <summary>
    /// 궁극기 범위 지정 상태와 표시 정보
    /// </summary>
    public HeroUltimateAim UltimateAim => _ultimateAim;

    /// <summary>
    /// 궁극기 범위 지정 중 여부
    /// </summary>
    public bool IsAimingUltimate => _ultimateAim.IsAiming;

    /// <summary>
    /// 기본 공격 실행자
    /// </summary>
    public AttackExecutor BasicAttackExecutor => _attacker.Executor;

    /// <summary>
    /// 회피 진행 여부
    /// </summary>
    public bool IsDodging => _dodge.IsDodging;

    /// <summary>
    /// 남은 회피 쿨타임
    /// </summary>
    public float RemainingDodgeCooldown => _dodge.RemainingCooldown;

    /// <summary>
    /// 사용한 부활 횟수
    /// </summary>
    public int UsedReviveCount => _revive.UsedReviveCount;

    /// <summary>
    /// 자동 부활 대기 여부
    /// </summary>
    public bool IsWaitingRevive => _revive.IsWaitingRevive;

    /// <summary>
    /// 자동 부활까지 남은 게임 시간
    /// </summary>
    public float RemainingReviveTime => _revive.RemainingReviveTime;

    /// <summary>
    /// 부활 횟수 소진 이벤트(마지막 사망 피해 정보)
    /// </summary>
    public event Action<DamageInfo> OnReviveExhausted;

    /// <summary>
    /// 플레이어 사망 확정 이벤트(피해 정보)
    /// </summary>
    public event Action<DamageInfo> OnDied;

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

        //생성 단계에서는 참조만 연결하고 초기화/표현은 기존 Init 순서로 처리
        _weaponSetup = new HeroWeaponSetup(
            this, _weaponBindings, _attacker, _skillCaster, _ultimateCaster,
            _mover, _animatorHandler );
        _revive = new HeroRevive( transform, _health, _mover );
        _dodge = new HeroDodge( _mover, _damageReceiver );
        _combatAnimation = new HeroCombatAnimationBinding(
            _attacker, _skillCaster, _ultimateCaster, _animatorHandler );
        _itemUseContext = new ItemUseContext ( _health , _energy );
        _ultimateAim = new HeroUltimateAim ( gameObject , TryUseUltimate );

        HeroUltimateAimPreview preview = Instantiate (
            _ultimateAimPreviewPrefab , transform );
        preview.Init ( _ultimateAim );
    }

    /// <summary>
    /// 캐릭터 사운드 서비스 연결
    /// </summary>
    /// <param name="sound">공통 관리자</param>
    public void SetSound ( SoundManager sound )
    {
        SetSound( sound, false );
    }

    /// <summary>
    /// 조작 대상 여부에 따라 본인 출력 또는 기존 위치 출력으로 서비스 연결
    /// </summary>
    /// <param name="sound">공통 관리자</param>
    /// <param name="isLocalPlayer">현재 조작하는 본인 캐릭터인지 여부</param>
    public void SetSound ( SoundManager sound, bool isLocalPlayer )
    {
        _sound = sound;
        if ( isLocalPlayer == true )
            _soundPlayer.Init( sound, 0f );
        else
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
    /// 선택한 무기 구성 적용과 플레이어 전투 초기화
    /// </summary>
    /// <param name="weaponData">선택한 무기 설정</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ( WeaponData weaponData )
    {
        return Init( weaponData, default );
    }

    /// <summary>
    /// 선택 무기와 이번 도전의 강화 결과로 전투 초기화
    /// </summary>
    /// <param name="weaponData">선택 무기 설정</param>
    /// <param name="upgrades">이번 도전에 확정된 강화 증가량</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ( WeaponData weaponData, HeroUpgradeStats upgrades )
    {
        //처리 중인 행동을 초기화로 덮어쓰지 않음
        if ( IsActionBusy( ) == true )
        {
            Debug.LogError( "행동 처리 중에는 Hero를 초기화할 수 없습니다.", this );
            return false;
        }

        //초기화 중 발생하는 이벤트에서는 행동 요청 거절
        _isInitialized = false;
        _minimapTarget.SetVisible( false );
        SetActionsEnabled( false );
        _soundPlayer.StopAll( );
        _vfxPlayer.Clear( );

        CancelUltimateAim ( );

        //구성 검사보다 먼저 이전 모션 신호와 실행을 정리
        _animatorHandler.StopPresentation( );
        CancelExecutions( );

        if ( _weaponSetup.TryResolve(
            weaponData, out HeroWeaponBinding binding ) == false )
            return false;

        if ( _revive.Init( _reviveData, _revivePoint ) == false )
            return false;

        _weaponSetup.ApplyExecutors( binding );
        binding.AttackExecutor.SetSound( _sound );
        binding.SkillExecutor.SetSound( _sound );
        binding.UltimateExecutor.SetSound( _sound );
        binding.AttackExecutor.SetVfx( _vfx );
        binding.SkillExecutor.SetVfx( _vfx );
        binding.UltimateExecutor.SetVfx( _vfx );

        CombatStatsData stats = weaponData.CombatStatsData;
        GameObject source = binding.Source;

        //체력 모듈을 전달하고 피해 수신 설정 실패 시 이후 초기화 중단
        if ( _damageReceiver.Init(
            gameObject, _health, Faction.Ally, stats.DamageReductionRate ) == false )
            return false;

        //피해 수신이 준비된 뒤, 초기 HP 이벤트보다 먼저 회피 설정 연결
        if ( _dodge.Init( _dodgeData ) == false )
            return false;

        //초기 HP 이벤트에서 참조할 공격 모듈을 먼저 준비
        if ( _attacker.Init(
            gameObject, _health, source,
            ActorType.Player, Faction.Ally, weaponData.AttackData,
            upgrades.DamageBonus, upgrades.AttackSpeedBonus ) == false )
            return false;

        if ( _energy.Init( _energyData ) == false )
            return false;

        if ( _skillCaster.Init( gameObject, _health, _energy,
            source, ActorType.Player, Faction.Ally, weaponData.SkillData,
            damageBonus: upgrades.DamageBonus ) == false )
            return false;

        if ( _ultimateCaster.Init( gameObject, _health, _energy,
            source, ActorType.Player, Faction.Ally, weaponData.UltimateData,
            AttackType.Ultimate, upgrades.DamageBonus ) == false )
            return false;

        if ( _statusEffects.Init(
            this, _health, _damageReceiver,
            _mover, _statusResistance ) == false )
            return false;

        //행동을 제한한 초기화 단계에서 이전 넉백도 정리
        if ( _knockback.Init(
            this, _health, _damageReceiver, _mover ) == false )
            return false;

        _weaponSetup.ApplyPresentation( weaponData, binding, transform.position, upgrades );
        float maxHp = stats.MaxHp + upgrades.MaxHpBonus;
        _health.Init( maxHp );

        //Health.Init은 반환값이 없으므로 적용된 초기 상태 확인
        if ( _health.IsAlive == false || _health.MaxHp != maxHp )
            return false;

        //명시적인 전투 초기화는 대기를 정리하지만 스테이지 사용 횟수는 유지
        _ultimateAim.Init ( binding.UltimateExecutor as IUltimateAimTarget );
        _isInitialized = true;
        _revive.ResetPresentationState( );
        RefreshActionState( );
        _vfxPlayer.RefreshEffects( _statusEffects );
        _minimapTarget.SetVisible( true );

        return true;
    }

    #endregion

    private void LateUpdate ()
    {
        if ( _isInitialized == false )
            return;

        //이번 프레임의 이동과 충돌 결과를 표현에 전달
        _animatorHandler.UpdateGrounded( _mover.IsGrounded );
        _animatorHandler.UpdateMovement(
            transform, _mover.CanMove, Time.deltaTime );
    }

    #region ----- 이동 -----

    /// <summary>
    /// 월드 기준 이동 방향 전달
    /// </summary>
    /// <param name="direction">수평 이동 방향과 입력 크기</param>
    public void Move ( Vector3 direction )
    {
        _mover.SetDirection( direction );
    }

    /// <summary>
    /// 월드 기준 조준 방향 전달
    /// </summary>
    /// <param name="direction">조준 방향</param>
    public void SetAimDirection ( Vector3 direction )
    {
        _mover.SetAimDirection( direction );
    }

    /// <summary>
    /// 지상에서 점프 시작 요청
    /// </summary>
    /// <returns>점프 시작 여부</returns>
    public bool TryJump ()
    {
        if ( CanStartNewAction( ) == false )
            return false;

        if ( _mover.TryJump( ) == false )
            return false;

        CancelUltimateAim ( );

        //실제 상승 속도 적용에 성공한 뒤 피격 요청 정리와 공중 표현 시작
        _animatorHandler.PlayJump( );
        _soundPlayer.Play( SoundId.HeroJump );
        return true;
    }

    /// <summary>
    /// 현재 프레임의 이동 실행
    /// </summary>
    /// <param name="deltaTime">프레임 경과 시간</param>
    public void UpdateMove ( float deltaTime )
    {
        if ( isActiveAndEnabled == false )
            return;

        //사망으로 수평 이동이 제한되어도 중력과 지면 접촉은 유지
        if ( _knockback.IsKnockback == true )
        {
            _knockback.UpdateKnockback( deltaTime );

            //종료 이벤트로 행동이 복구되어도 이번 프레임 일반 이동은 생략
            //넉백 Mover가 중력까지 처리하므로 중력도 중복 실행하지 않음
            return;
        }

        if ( _dodge.IsDodging == true )
        {
            _dodge.UpdateMovement( deltaTime );

            //회피 종료로 행동이 복구돼도 이번 프레임의 일반 이동/중력은 생략
            return;
        }

        _mover.UpdateMove( deltaTime );
    }

    #endregion

    #region ----- 회피 -----

    /// <summary>
    /// 회피 시작 요청
    /// </summary>
    /// <param name="direction">월드 기준 이동 입력 방향</param>
    /// <returns>회피 시작 여부</returns>
    public bool TryDodge ( Vector3 direction )
    {
        if ( CanStartNewAction( ) == false )
            return false;

        return _dodge.TryStart( direction );
    }

    /// <summary>
    /// 회피 시간과 무적 갱신
    /// </summary>
    /// <param name="deltaTime">프레임 경과 시간</param>
    public void UpdateDodge ( float deltaTime )
    {
        if ( _isInitialized == false || isActiveAndEnabled == false )
            return;

        bool wasInvulnerable = _dodge.IsInvulnerable;
        _dodge.UpdateTimers( deltaTime );

        if ( _showDebugLog == true
            && wasInvulnerable == true && _dodge.IsInvulnerable == false )
            Debug.Log( $"회피 무적 종료: 대상={name}", this );
    }

    /// <summary>
    /// 승인된 회피의 행동 제한과 애니메이션 요청
    /// </summary>
    void HandleDodgeStarted ()
    {
        CancelUltimateAim ( );

        //모듈이 진행 상태를 확정한 뒤
        //일반 행동 제한과 회피 진입 요청
        RefreshActionState( );
        CharacterController debugController = _showDebugLog
            ? _mover.GetComponent<CharacterController>( )
            : null;
        _animatorHandler.StartDodge( debugController );
        _soundPlayer.StopAll( );
        _soundPlayer.Play( SoundId.HeroDodge );

        if ( _showDebugLog == true )
            Debug.Log( $"회피 시작: 대상={name}", this );
    }

    /// <summary>
    /// 종료 상황에 맞는 행동 복구
    /// </summary>
    /// <param name="reason">종료 사유</param>
    void HandleDodgeEnded ( AttackEndReason reason )
    {
        //넉백/사망/비활성화는 기존 처리 경로가 최종 모션을 결정
        bool waitForReturn =
            reason == AttackEndReason.Completed
            || ( reason == AttackEndReason.Canceled
                && _knockback.IsKnockback == false );

        _animatorHandler.EndDodge( waitForReturn, transform.position );
        RefreshActionState( );

        if ( _showDebugLog == true )
        {
            Debug.Log(
                $"회피 종료: 대상={name} / 사유={reason}"
                + $" / 넉백={_knockback.IsKnockback}"
                + $" / 남은 쿨타임={_dodge.RemainingCooldown:F2}", this );
        }
    }

    #endregion

    #region ----- 공격 -----

    /// <summary>
    /// 현재 프레임의 공격 쿨타임 갱신
    /// </summary>
    /// <param name="deltaTime">프레임 경과 시간</param>
    public void UpdateAttack ( float deltaTime )
    {
        if ( isActiveAndEnabled == false )
            return;

        _attacker.UpdateAttack( deltaTime );
    }

    /// <summary>
    /// 스킬 쿨타임 갱신
    /// </summary>
    /// <param name="deltaTime">게임 시간 기준 경과 시간</param>
    public void UpdateSkill ( float deltaTime )
    {
        if ( isActiveAndEnabled == false )
            return;

        //사망과 부활 연출 중에도 쿨타임은 감소
        _skillCaster.UpdateSkill( deltaTime );
        _ultimateCaster.UpdateSkill( deltaTime );
    }

    /// <summary>
    /// 공격 시작 요청
    /// </summary>
    /// <returns>공격 시작 여부</returns>
    public bool TryAttack ()
    {
        if ( CanStartNewAction( ) == false || _mover.IsGrounded == false )
            return false;

        return _attacker.TryAttack( );
    }

    /// <summary>
    /// 스킬 시작 요청
    /// </summary>
    /// <returns>스킬 시작 승인 여부</returns>
    public bool TryUseSkill ()
    {
        if ( CanUseSkill == false )
            return false;

        return _skillCaster.TryUse( );
    }

    /// <summary>
    /// 궁극기 시작 요청
    /// </summary>
    /// <returns>궁극기 시작 승인 여부</returns>
    public bool TryUseUltimate ()
    {
        if ( CanUseUltimate == false )
            return false;

        bool started = _ultimateCaster.TryUse ( );

        //시작 알림에서 취소된 회차도 기존 승인 반환 계약에 따라 조준 종료
        if ( started == true )
            CancelUltimateAim ( );

        return started;
    }

    /// <summary>
    /// 궁극기 범위 지정 시작 또는 사용 확정 요청
    /// </summary>
    /// <returns>요청 처리 성공 여부</returns>
    public bool TryRequestUltimate ()
    {
        if ( _lastUltimateInputFrame == Time.frameCount )
            return false;

        _lastUltimateInputFrame = Time.frameCount;

        if ( CanUseUltimate == false )
            return false;

        if ( _ultimateAim.IsAiming == true )
            return _ultimateAim.TryConfirm ( );

        if ( _ultimateAim.HasTarget == true )
        {
            if ( _ultimateAim.TryBegin ( ) == false )
                return false;

            _animatorHandler.StartUltimateReady ( );
            return true;
        }

        return TryUseUltimate ( );
    }

    /// <summary>
    /// 궁극기 범위 지정 후보 갱신
    /// </summary>
    public void UpdateUltimateAim ()
    {
        _ultimateAim.UpdateAim ( );
    }

    /// <summary>
    /// 궁극기 조준과 준비 자세 해제
    /// </summary>
    public void CancelUltimateAim ()
    {
        _animatorHandler.StopUltimateReady ( );
        _ultimateAim.Cancel ( );
    }

    /// <summary>
    /// 현재 상태에서 아이템 효과를 사용할 수 있는지 확인
    /// </summary>
    /// <param name="data">사용할 아이템 설정</param>
    /// <returns>사용 가능 여부</returns>
    public bool CanUseItem ( ItemData data )
    {
        return CanAct && data.CanApply ( _itemUseContext );
    }

    /// <summary>
    /// 사용이 승인된 아이템 효과 적용
    /// </summary>
    /// <param name="data">사용할 아이템 설정</param>
    /// <returns>실제 효과 적용량</returns>
    public float ApplyItemEffect ( ItemData data )
    {
        return data.Apply ( _itemUseContext );
    }

    /// <summary>
    /// 적용을 마친 아이템의 사용 효과 요청
    /// </summary>
    /// <param name="prefab">사용 효과</param>
    public void PlayItemVfx ( VfxEffect prefab )
    {
        _vfxPlayer.Play( prefab );
    }

    /// <summary>
    /// 기본 공격 유효 적중에 따른 에너지 충전
    /// </summary>
    /// <param name="context">원래 공격의 실행 정보</param>
    void HandleFirstValidHit ( AttackContext context )
    {
        if ( context.Attacker != gameObject
            || context.ActorType != ActorType.Player
            || context.AttackType != AttackType.BasicAttack )
            return;

        //공격 종료나 사망 이후 전달된 정상 결과도 충전
        float previousEnergy = _energy.CurrentEnergy;
        float addedEnergy = _energy.Add( _energyData.BasicAttackGain );

        if ( _showDebugLog == true )
        {
            Debug.Log(
                $"기본 공격 에너지 충전: 공격자={name}, 회차={context.AttackId}, " +
                $"이전={previousEnergy}, 현재={_energy.CurrentEnergy}, " +
                $"실제 충전={addedEnergy}, 최대={_energy.MaxEnergy}",
                this );
        }
    }

    #endregion

    #region ----- 애니메이션 -----

    /// <summary>
    /// 실제 지상 이동의 발소리 요청
    /// </summary>
    void HandleFootstep ()
    {
        if ( CanAct == false || _mover.IsGrounded == false
            || _mover.HasMovedThisFrame == false )
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
    /// 생존 중 유효 피격의 표현 요청
    /// </summary>
    /// <param name="damageInfo">피해 정보</param>
    /// <param name="appliedDamage">실제 적용 피해량</param>
    void HandleDamaged ( DamageInfo damageInfo, float appliedDamage )
    {
        //치명타격은 사망음으로 처리하고 생존 피격음은 모션과 별도 요청
        if ( _isInitialized == true && _health.IsAlive == true )
        {
            _soundPlayer.Play( SoundId.HeroHurt );
            _vfxPlayer.PlayHit( );
        }

        bool canAct = CanAct;
        bool isGrounded = _mover.IsGrounded;
        bool requested = false;

        if ( canAct == true && isGrounded == true )
            requested = _animatorHandler.PlayTakeHit( );

        if ( _showDebugLog == true )
        {
            Debug.Log(
                $"일반 피격 표현: 대상={name} / 요청={requested}"
                + $" / 실제 피해={appliedDamage}"
                + $" / 행동 가능={canAct} / 접지={isGrounded}"
                + $" / 공격 Busy={_attacker.IsBusy}"
                + $" / 스킬 Busy={_skillCaster.IsBusy}"
                + $" / 궁극기 Busy={_ultimateCaster.IsBusy}"
                + $" / 넉백={_knockback.IsKnockback}",
                this );
        }
    }

    #endregion

    #region ----- 상태 이상 -----

    /// <summary>
    /// 현재 프레임의 상태이상 갱신
    /// </summary>
    /// <param name="deltaTime">게임 시간 기준 경과 시간</param>
    public void UpdateEffects ( float deltaTime )
    {
        if ( _isInitialized == false || isActiveAndEnabled == false )
            return;

        _statusEffects.UpdateEffects( deltaTime );
    }

    #endregion

    #region ----- 사망/부활 -----

    /// <summary>
    /// 자동 부활 대기 갱신과 실행
    /// </summary>
    /// <param name="deltaTime">프레임 경과 시간</param>
    public void UpdateRevive ( float deltaTime )
    {
        if ( isActiveAndEnabled == false )
            return;

        _revive.UpdateRevive( deltaTime );
    }

    /// <summary>
    /// 테스트용 지정 체력 부활 요청 전달
    /// </summary>
    /// <param name="hp">적용할 체력</param>
    public void Revive ( float hp )
    {
        _revive.Revive( hp );
    }

    /// <summary>
    /// 사망에 따른 행동 정지와 연출 완료 대기
    /// </summary>
    /// <param name="damageInfo">피해 정보</param>
    void HandleDied ( DamageInfo damageInfo )
    {
        _revive.PrepareDeath( damageInfo );

        //사망 알림 전에 미니맵 표시 해제
        _minimapTarget.SetVisible( false );

        //공격 종료 이벤트 전에 행동 제한과 현재 사망 정보 확정
        SetActionsEnabled( false );

        //공격 취소 알림 전에 사망 이력 전달
        _soundPlayer.StopAll( );
        _vfxPlayer.Clear( );
        _sound.PlayAt( SoundId.HeroDeath, transform.position, _soundPlayer.Is2DOutput );
        _vfxPlayer.PlayDeath( );
        OnDied?.Invoke( damageInfo );

        CancelActions( AttackEndReason.Died );

        //소진 이벤트와 자동 부활 대기는 사망 연출 완료 시 처리
        _animatorHandler.SetDead( );
    }

    /// <summary>
    /// 실제 체력 복구에 따른 사망 대기 정리
    /// </summary>
    void HandleRevived ()
    {
        _revive.HandleRevived( );

        //부활 위치와 HP가 적용된 뒤 표시 복원
        _minimapTarget.SetVisible( true );

        //HP 복구만으로 행동을 허용하지 않고 연출 완료 상태 반영
        _animatorHandler.RestoreAlive( transform.position );
        RefreshActionState( );

        if ( _isInitialized == true )
            _vfxPlayer.PlayRevive( );
    }

    /// <summary>
    /// 부활 횟수 소진 알림 또는 자동 부활 대기 시작
    /// </summary>
    public void CompleteDeathPresentation ()
    {
        _revive.CompleteDeathPresentation( );
    }

    /// <summary>
    /// 현재 부활 연출 완료 신호 처리
    /// </summary>
    public void CompleteRevivePresentation ()
    {
        if ( _revive.CompleteRevivePresentation( ) == true )
            RefreshActionState( );
    }

    /// <summary>
    /// 자동 부활 사용 횟수 초기화
    /// </summary>
    public void ResetReviveCount ()
    {
        _revive.ResetReviveCount( );
    }

    /// <summary>
    /// 부활 체력 복구 전에 일반 행동 제한
    /// </summary>
    void HandleRevivePreparing ()
    {
        SetActionsEnabled( false );
    }

    /// <summary>
    /// 부활 소진 결과 전달
    /// </summary>
    /// <param name="damageInfo">마지막 사망 피해 정보</param>
    void HandleReviveExhausted ( DamageInfo damageInfo )
    {
        OnReviveExhausted?.Invoke( damageInfo );
    }

    #endregion

    #region ----- 공용/캔슬 -----

    /// <summary>
    /// 전투 행동 처리 중인지 확인
    /// </summary>
    /// <returns>전투 모듈 처리 또는 회피 진행 여부</returns>
    bool IsActionBusy ()
    {
        return _attacker.IsBusy
            || _skillCaster.IsBusy
            || _ultimateCaster.IsBusy
            || _dodge.IsDodging;
    }

    /// <summary>
    /// 새 행동을 시작할 수 있는지 확인
    /// </summary>
    /// <returns>공통 행동 시작 조건 충족 여부</returns>
    bool CanStartNewAction ()
    {
        return CanAct
            && IsActionBusy( ) == false
            && _animatorHandler.CanStartAction;
    }

    /// <summary>
    /// 현재 상태에 맞는 일반 행동 허용 반영
    /// </summary>
    void RefreshActionState ()
    {
        SetActionsEnabled( CanAct );
    }

    /// <summary>
    /// 효과 상태에 따른 행동 제한과 진행 행동 취소
    /// </summary>
    /// <param name="type">변경된 효과 종류</param>
    void HandleEffectChanged ( StatusEffectType type )
    {
        if ( _isInitialized == true && _health.IsAlive == true )
            _vfxPlayer.RefreshEffects( _statusEffects );

        RefreshActionState( );

        if ( _statusEffects.IsStunned == false )
            return;

        CancelActions( AttackEndReason.Canceled );
    }

    /// <summary>
    /// 넉백 상태에 따른 행동 제한과 피격 표현 연결
    /// </summary>
    /// <param name="isKnockback">넉백 진행 여부</param>
    void HandleKnockbackChanged ( bool isKnockback )
    {
        //치명타 넉백의 이동은 유지하되 사망 표현을 우선
        _animatorHandler.UpdateKnockback(
            isKnockback && _health.IsAlive );

        //상태 확정 후 이동/공격을 먼저 제한하고 기존 회차를 취소
        //종료 시에도 기절/사망/부활 연출 제한을 함께 재계산
        RefreshActionState( );

        if ( isKnockback == false )
            return;

        //종료 이벤트의 모션 정리까지 끝난 뒤 넉백 피격을 재생
        CancelActions( AttackEndReason.Canceled );

        if ( _isInitialized == false
            || _health.IsAlive == false
            || _revive.IsWaitingReviveEnd == true )
            return;

        _animatorHandler.PlayKnockbackHit( );
    }

    /// <summary>
    /// 진행 중인 행동 취소
    /// </summary>
    /// <param name="reason">종료 사유</param>
    void CancelActions ( AttackEndReason reason )
    {
        CancelUltimateAim ( );

        //종료 알림을 먼저 처리해 이후 넉백/사망 표현을 덮지 않음
        _dodge.Cancel( reason );
        _attacker.CancelAttack( reason );
        _skillCaster.CancelSkill( reason );
        _ultimateCaster.CancelSkill( reason );
    }

    /// <summary>
    /// 이전 플레이에서 남은 공격 실행 정리
    /// </summary>
    void CancelExecutions ()
    {
        _attacker.CancelExecutions( );
        _skillCaster.CancelExecutions( );
        _ultimateCaster.CancelExecutions( );
    }

    /// <summary>
    /// 이동과 공격 허용 여부 전달
    /// </summary>
    /// <param name="enabled">일반 행동 허용 여부</param>
    void SetActionsEnabled ( bool enabled )
    {
        bool canMove =
            enabled
            && _ultimateCaster.IsBusy == false
            && _animatorHandler.IsUltimatePlaying == false;

        _mover.SetMoveEnabled( canMove );
        _attacker.SetAttackEnabled( enabled );
        _skillCaster.SetSkillEnabled( enabled );
        _ultimateCaster.SetSkillEnabled( enabled );
    }

    /// <summary>
    /// 도전 종료 시 진행 행동과 남은 실행을 정리하고 조작 중단
    /// </summary>
    public void StopStageActions ( )
    {
        //취소 알림에서 행동이 복구되지 않도록 종료 상태부터 반영
        _isInitialized = false;
        SetActionsEnabled( false );

        //이후 씬 갱신이 멈춰도 회피와 모션 회차가 다음 도전에 남지 않음
        _animatorHandler.StopPresentation( );
        CancelExecutions( );
        CancelActions( AttackEndReason.Canceled );
        _knockback.CancelKnockback( );
        _mover.SetDirection( Vector3.zero );
        _soundPlayer.StopAll( );
        _vfxPlayer.Clear( );
    }

    /// <summary>
    /// 새 스테이지의 상태이상, 에너지와 스킬 쿨타임 초기화
    /// </summary>
    /// <returns>초기화 성공 여부</returns>
    public bool ResetStageResources ()
    {
        if ( _isInitialized == false
            || _isResettingStageResources == true
            || IsActionBusy( ) == true )
        {
            Debug.LogError(
                "Hero 초기화와 행동 종료 후 스테이지 자원을 초기화해야 합니다.", this );
            return false;
        }

        _isResettingStageResources = true;
        //효과/에너지 초기화 이벤트에서도 행동이 시작되지 않도록 제한
        SetActionsEnabled( false );

        CancelUltimateAim ( );

        try
        {
            CancelExecutions( );
            _knockback.CancelKnockback( );
            _statusEffects.ClearEffects( );
            _skillCaster.ResetCooldown( );
            _ultimateCaster.ResetCooldown( );
            _dodge.ResetCooldown( );
            _energy.ResetEnergy( );
            return true;
        }
        finally
        {
            _isResettingStageResources = false;
            RefreshActionState( );
        }
    }

    private void OnEnable ()
    {
        _animatorHandler.OnFootstep += HandleFootstep;
        _animatorHandler.OnSound += HandleSound;
        //생명 상태 변화에 필요한 하위 모듈의 행동 연결
        _damageReceiver.OnDamaged += HandleDamaged;
        _damageReceiver.OnDied += HandleDied;
        _health.OnRevived += HandleRevived;
        _attacker.OnFirstValidHit += HandleFirstValidHit;
        _revive.OnRevivePreparing += HandleRevivePreparing;
        _revive.OnReviveExhausted += HandleReviveExhausted;
        _combatAnimation.OnActionStateChanged += RefreshActionState;
        _combatAnimation.Bind( );
        _dodge.OnStarted += HandleDodgeStarted;
        _dodge.OnEnded += HandleDodgeEnded;
        _animatorHandler.OnDeathCompleted += CompleteDeathPresentation;
        _animatorHandler.OnAliveRestored += CompleteRevivePresentation;
        _statusEffects.OnEffectChanged += HandleEffectChanged;
        _knockback.OnKnockbackChanged += HandleKnockbackChanged;

        //최초 활성화와 재활성화에서 현재 생명 상태 반영
        _minimapTarget.SetVisible( _isInitialized && _health.IsAlive );

        if ( _isInitialized == false )
            return;

        //재활성화는 HP, 횟수, 연출 대기 및 남은 시간을 초기화하지 않음
        if ( _health.IsAlive == true && _revive.IsWaitingReviveEnd == true )
            _animatorHandler.RestoreAlive( transform.position );
        else
            _animatorHandler.ResumePresentation( transform.position );

        RefreshActionState( );

        _revive.Resume( );

        if ( _health.IsAlive == true )
            _vfxPlayer.RefreshEffects( _statusEffects );
    }

    private void OnDisable ()
    {
        //Hero만 비활성화된 경우에도 표시 해제
        _minimapTarget.SetVisible( false );

        _animatorHandler.OnFootstep -= HandleFootstep;
        _animatorHandler.OnSound -= HandleSound;
        //전투 취소보다 먼저 이전 클립의 이벤트 수신 차단
        _animatorHandler.StopPresentation( );
        CancelExecutions( );
        _damageReceiver.OnDamaged -= HandleDamaged;
        _damageReceiver.OnDied -= HandleDied;
        _health.OnRevived -= HandleRevived;
        _attacker.OnFirstValidHit -= HandleFirstValidHit;
        _statusEffects.OnEffectChanged -= HandleEffectChanged;
        _knockback.OnKnockbackChanged -= HandleKnockbackChanged;

        //Hero만 비활성화해도 진행 기록을 제거, 종료 알림으로 행동을 복구하지 않음
        _knockback.CancelKnockback( );

        //Hero 컴포넌트만 비활성화되어도 하위 행동 제한
        SetActionsEnabled( false );
        _mover.SetDirection( Vector3.zero );

        CancelActions( AttackEndReason.Disabled );

        //취소에 따른 동기 종료 알림을 처리한 뒤 연결 해제
        _dodge.OnStarted -= HandleDodgeStarted;
        _dodge.OnEnded -= HandleDodgeEnded;
        _combatAnimation.Unbind( );
        _combatAnimation.OnActionStateChanged -= RefreshActionState;
        _revive.OnRevivePreparing -= HandleRevivePreparing;
        _revive.OnReviveExhausted -= HandleReviveExhausted;

        _animatorHandler.OnDeathCompleted -= CompleteDeathPresentation;
        _animatorHandler.OnAliveRestored -= CompleteRevivePresentation;
        _soundPlayer.StopAll( );
        _vfxPlayer.Clear( );
    }

    /// <summary>
    /// 선택 무기의 기본 공격 범위 기즈모 설정
    /// </summary>
    /// <param name="weaponData">에디터에서 선택한 무기 설정</param>
    public void SetWeaponGizmo ( WeaponData weaponData )
    {
        //에디터에서는 Awake 이전에도 호출되므로 인스턴스 생성 없이 설정 조회
        HeroWeaponSetup.SetWeaponGizmo( _weaponBindings, weaponData );
    }

    private void OnDrawGizmos ()
    {
        //편집 중에는 Awake 이전이므로 조준 객체를 조회하지 않음
        if ( Application.isPlaying == false
            || _ultimateAim == null
            || _ultimateAim.IsAiming == false
            || _ultimateAim.HasGroundPoint == false )
            return;

        Color previousColor = Gizmos.color;
        Gizmos.color = _ultimateAim.IsValid ? Color.green : Color.red;
        Gizmos.DrawWireSphere ( _ultimateAim.Point , _ultimateAim.Radius );
        Gizmos.color = previousColor;
    }

    #endregion
}
