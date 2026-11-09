using System;
using UnityEngine;

/// <summary>
/// 스킬 모듈 - 시작, 회차, 타격 승인, 종료와 쿨타임 관리
/// </summary>
public class SkillCaster : MonoBehaviour
{
    [Header ( "----- 컴포넌트 -----" )]
    [SerializeField] AttackExecutor _skillExecutor;    //연결된 공격 실행자

    Health _health;     //플레이어 체력
    Energy _energy;     //공용 에너지

    GameObject _owner;      //실제 공격 주체
    GameObject _source;     //실제 피해 발생원

    ActorType _actorType;       //공격 주체 종류
    Faction _faction;       //공격 진영
    AttackType _attackType;     //기본 스킬 또는 궁극기 분류
    SkillData _skillData;       //스킬 설정

    AttackContext _skillContext;   //이번 스킬의 실행 정보
    float _skillCooldown;          //이번 회차의 완료 후 쿨타임
    float _damageBonus;            //초기화에서 전달받은 직접 피해 증가율
    float _remainingCooldown;      //남은 쿨타임
    int _currentSkillId;           //가장 최근 생성한 스킬 회차

    bool _isInitialized;     //초기화 성공 여부
    bool _skillEnabled;      //Hero가 전달한 행동 허용
    bool _isStarting;        //시작 처리와 알림 진행 여부
    bool _isCasting;         //스킬 동작 진행 여부
    bool _hasCommittedHit;   //이번 회차 타격 승인 여부
    bool _isExecutingHit;    //승인된 타격 실행 여부

    /// <summary>
    /// 스킬 시작 가능 여부
    /// </summary>
    public bool CanUse =>
        _isInitialized
        && isActiveAndEnabled
        && _skillExecutor.isActiveAndEnabled
        && _skillEnabled
        && _health.IsAlive
        && IsBusy == false
        && _remainingCooldown <= 0f
        && _energy.CanSpend ( _skillData.EnergyCost );

    /// <summary>
    /// 스킬 동작 진행 여부
    /// </summary>
    public bool IsCasting => _isCasting;

    /// <summary>
    /// 시작 처리, 스킬 동작 또는 타격 실행 여부
    /// </summary>
    public bool IsBusy => _isStarting || _isCasting || _isExecutingHit;

    /// <summary>
    /// 가장 최근 생성한 스킬 회차
    /// </summary>
    public int CurrentSkillId => _currentSkillId;

    /// <summary>
    /// 남은 스킬 쿨타임
    /// </summary>
    public float RemainingCooldown => _remainingCooldown;

    /// <summary>
    /// 스킬 사용에 필요한 에너지
    /// </summary>
    public float EnergyCost => _skillData.EnergyCost;

    /// <summary>
    /// 상위 초기화에서 기존 실행을 정리한 뒤 스킬 실행자 연결
    /// </summary>
    /// <param name="executor">선택한 무기의 실행자</param>
    public void SetExecutor ( AttackExecutor executor )
    {
        _skillExecutor = executor;
    }

    /// <summary>
    /// 스킬 시작 이벤트(스킬 실행 정보)
    /// </summary>
    public event Action<AttackContext> OnSkillStarted;

    /// <summary>
    /// 스킬 종료 이벤트(스킬 회차, 종료 사유)
    /// </summary>
    public event Action<int , AttackEndReason> OnSkillEnded;

    /// <summary>
    /// 스킬 설정과 의존 모듈 연결
    /// </summary>
    /// <param name="owner">공격 주체</param>
    /// <param name="health">체력 모듈</param>
    /// <param name="energy">에너지 모듈</param>
    /// <param name="source">피해 발생원</param>
    /// <param name="actorType">주체 종류</param>
    /// <param name="faction">진영</param>
    /// <param name="skillData">스킬 설정</param>
    /// <param name="attackType">기본 스킬 또는 궁극기 분류</param>
    /// <param name="damageBonus">직접 피해 증가율</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        GameObject owner , Health health , Energy energy ,
        GameObject source , ActorType actorType ,
        Faction faction , SkillData skillData ,
        AttackType attackType = AttackType.Skill, float damageBonus = 0f )
    {
        //이미 처리 중인 동작이 있는지 여부
        if ( IsBusy == true )
        {
            Debug.LogError ( "스킬 처리 중에는 초기화할 수 없습니다." , this );
            return false;
        }

        //유효한 값인지 확인
        if ( skillData.EnergyCost <= 0f || skillData.Cooldown < 0f )
        {
            Debug.LogError (
                "스킬 비용은 양수, 쿨타임은 0 이상이어야 합니다." , this );
            return false;
        }

        //무기별 스킬 실행 모듈 연결
        if ( _skillExecutor == null )
        {
            Debug.LogError ( "스킬 실행 모듈이 연결되지 않았습니다." , this );
            return false;
        }

        //연결 오류를 확인, 실제 사용 직전 다시 회차별 수치를 보존
        if ( TryPrepareExecutor ( skillData ) == false )
            return false;

        _skillExecutor.CancelExecutions ( );

        _owner = owner;
        _health = health;
        _energy = energy;
        _source = source;
        _actorType = actorType;
        _faction = faction;
        _attackType = attackType;
        _skillData = skillData;
        _damageBonus = damageBonus;
        _isInitialized = true;

        //일반 초기화는 기존 쿨타임과 회차를 유지
        return true;
    }

    /// <summary>
    /// 일반 행동 허용 여부 전달
    /// </summary>
    /// <param name="enabled">스킬 허용 여부</param>
    public void SetSkillEnabled ( bool enabled )
    {
        _skillEnabled = enabled;
    }

    /// <summary>
    /// 완료/취소 후 쿨타임 갱신
    /// </summary>
    /// <param name="deltaTime">게임 시간 기준 경과 시간</param>
    public void UpdateSkill ( float deltaTime )
    {
        if ( isActiveAndEnabled == false || IsBusy == true )
            return;

        if ( deltaTime <= 0f )
            return;

        //행동 허용과 생존 여부는 쿨타임 감소 조건에 포함하지 않음
        _remainingCooldown = Mathf.Max ( 0f , _remainingCooldown - deltaTime );
    }

    /// <summary>
    /// 에너지를 소비하고 스킬 시작
    /// </summary>
    /// <returns>시작 승인 여부, 승인 직후 취소된 경우도 true</returns>
    public bool TryUse ( )
    {
        if ( CanUse == false )
            return false;

        //실행 방식별 설정 준비는 회차 생성/에너지 소비/시작 알림 전에 완료
        if ( TryPrepareExecutor ( _skillData ) == false )
            return false;

        //지면 선택 등 실제 사용 직전에만 필요한 조건은 에너지 소비 전에 확인
        if ( _skillExecutor is SkillExecutor executor
            && executor.TryPrepareUse ( _owner ) == false )
            return false;

        AttackData attackData = _skillData.AttackData;

        float energyCost = _skillData.EnergyCost;
        float skillCooldown = _skillData.Cooldown;
        int skillId = _currentSkillId + 1;

        //설정 조회와 실행 정보 생성은 시작 상태 설정 전에 완료
        AttackContext context = new AttackContext (
            _owner , _source , _actorType , _faction ,
            _attackType , skillId ,
            attackData.RawDamage * ( 1f + _damageBonus ),
            attackData.Radius , attackData.Angle );

        _currentSkillId = skillId;
        _skillContext = context;
        _skillCooldown = skillCooldown;
        _hasCommittedHit = false;
        _isCasting = true;
        _isStarting = true;

        bool consumed = false;
        bool startNotified = false;

        try
        {
            //에너지 소비와 실제 차감 여부 확인
            if ( _energy.TrySpend ( energyCost , out consumed ) == false )
                return false;

            //소비 이벤트에서 취소된 회차는 다시 시작하지 않음
            if ( _isCasting == false )
                return true;

            //비활성화됐거나 스킬을 쓸 수 없거나 사망 상태면 스킬 실행 종료
            if ( isActiveAndEnabled == false
                || _skillEnabled == false
                || _health.IsAlive == false )
            {
                CancelSkill ( AttackEndReason.Canceled );
                return true;
            }

            //스킬 실행 이벤트 발행
            OnSkillStarted?.Invoke ( context );
            startNotified = true;

            return true;
        }
        finally
        {
            bool notifyCanceled = false;

            //소비 후 시작 연동 오류는 해당 회차만 취소
            if ( startNotified == false
                && _isCasting == true
                && _currentSkillId == skillId )
            {
                //소비가 끝난 회차는 쿨타임 적용 후 취소 알림 예약
                if ( consumed == true )
                {
                    ApplySkillEnd ( );
                    notifyCanceled = true;
                }
                else
                {
                    //소비 전 실패는 쿨타임 없이 준비 상태만 해제
                    _isCasting = false;
                }
            }

            //종료 알림에서 오류가 나도 내부 시작 상태는 이미 해제
            _isStarting = false;

            if ( notifyCanceled == true )
                OnSkillEnded?.Invoke ( skillId , AttackEndReason.Canceled );
        }
    }

    /// <summary>
    /// 원래 스킬 회차의 단발 타격 승인과 실행
    /// </summary>
    /// <param name="skillId">타격 신호의 원래 회차</param>
    /// <returns>타격 승인 여부</returns>
    public bool TryCommitHit ( int skillId )
    {
        if ( isActiveAndEnabled == false
            || _skillExecutor.isActiveAndEnabled == false
            || _skillEnabled == false
            || _health.IsAlive == false )
            return false;

        if ( _isStarting == true || _isCasting == false
            || skillId != _currentSkillId
            || _hasCommittedHit == true || _isExecutingHit == true )
            return false;

        //피해 이벤트의 재진입 전에 단발 타격 승인 기록
        _hasCommittedHit = true;
        _isExecutingHit = true;

        AttackContext context = _skillContext;

        try
        {
            //공통 회차/중복 승인 검사, 실제 타격 방식은 실행 모듈에 위임
            //승인된 타격은 실행 중 취소되어도 실행 모듈에서 대상 처리를 완료
            _skillExecutor.Execute ( context );
            return true;
        }
        finally
        {
            _isExecutingHit = false;
        }
    }

    /// <summary>
    /// 스킬 동작 종료와 쿨타임 시작
    /// </summary>
    /// <param name="skillId">완료 신호의 원래 회차</param>
    public void CompleteSkill ( int skillId )
    {
        if ( _isCasting == false || skillId != _currentSkillId )
            return;

        EndSkill ( AttackEndReason.Completed );
    }

    /// <summary>
    /// 진행 스킬 취소와 쿨타임 시작
    /// </summary>
    /// <param name="reason">취소 사유</param>
    public void CancelSkill ( AttackEndReason reason )
    {
        if ( reason == AttackEndReason.Completed )
        {
            Debug.LogError (
                "정상 완료는 CompleteSkill을 사용해야 합니다." , this );
            return;
        }

        if ( _isCasting == false )
            return;

        EndSkill ( reason );
    }

    /// <summary>
    /// 스킬 종료 상태와 쿨타임 반영
    /// </summary>
    void ApplySkillEnd ( )
    {
        _isCasting = false;
        _remainingCooldown = _skillCooldown;
    }

    /// <summary>
    /// 종료 상태 반영 후 외부 알림
    /// </summary>
    /// <param name="reason">종료 사유</param>
    void EndSkill ( AttackEndReason reason )
    {
        int skillId = _currentSkillId;

        //종료 이벤트 재진입 전에 상태와 쿨타임 확정
        ApplySkillEnd ( );
        OnSkillEnded?.Invoke ( skillId , reason );
    }

    /// <summary>
    /// 새 스테이지의 스킬 쿨타임 초기화
    /// </summary>
    public void ResetCooldown ( )
    {
        if ( IsBusy == true )
        {
            Debug.LogError (
                "스킬 처리 중에는 쿨타임을 초기화할 수 없습니다." , this );
            return;
        }

        //이전 플레이의 늦은 신호와 구분하기 위해 회차 유지
        _remainingCooldown = 0f;
    }

    /// <summary>
    /// 전용 준비 계약이 있는 실행자의 설정 준비
    /// </summary>
    /// <param name="skillData">스킬 또는 궁극기 설정</param>
    /// <returns>실행 준비 성공 여부</returns>
    bool TryPrepareExecutor ( SkillData skillData )
    {
        if ( _skillExecutor is SkillExecutor executor )
            return executor.TryPrepare ( skillData );

        return true;
    }

    /// <summary>
    /// 아직 종료되지 않은 스킬 실행 정리
    /// </summary>
    public void CancelExecutions ( )
    {
        //최초 초기화 전에는 실행자가 아직 연결되지 않을 수 있음
        _skillExecutor?.CancelExecutions ( );
    }

    private void OnDisable ( )
    {
        CancelSkill ( AttackEndReason.Disabled );
        CancelExecutions ( );
    }
}
