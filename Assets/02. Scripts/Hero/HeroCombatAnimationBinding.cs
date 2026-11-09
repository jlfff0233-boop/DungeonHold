using System;

/// <summary>
/// 히어로 전투 애니메이션 연결 - 전투 알림과 타격/완료 신호 전달
/// </summary>
public class HeroCombatAnimationBinding
{
    Attacker _attacker;                      //기본 공격 모듈
    SkillCaster _skillCaster;                //기본 스킬 모듈
    SkillCaster _ultimateCaster;             //궁극기 모듈
    AnimatorHandler _animatorHandler;        //전투 표현과 애니메이션 신호

    /// <summary>
    /// 궁극기 표현 반영 후 행동 상태 재계산 알림
    /// </summary>
    public event Action OnActionStateChanged;

    /// <summary>
    /// 전투 모듈과 애니메이션 전달 대상 연결
    /// </summary>
    /// <param name="attacker">기본 공격 모듈</param>
    /// <param name="skillCaster">기본 스킬 모듈</param>
    /// <param name="ultimateCaster">궁극기 모듈</param>
    /// <param name="animatorHandler">애니메이션 모듈</param>
    public HeroCombatAnimationBinding (
        Attacker attacker , SkillCaster skillCaster ,
        SkillCaster ultimateCaster , AnimatorHandler animatorHandler )
    {
        _attacker = attacker;
        _skillCaster = skillCaster;
        _ultimateCaster = ultimateCaster;
        _animatorHandler = animatorHandler;
    }

    /// <summary>
    /// 활성 수명의 전투 시작/종료와 애니메이션 신호 구독
    /// </summary>
    public void Bind ( )
    {
        _attacker.OnAttackStarted += HandleAttackStarted;
        _attacker.OnAttackEnded += HandleAttackEnded;
        _animatorHandler.OnAttackHit += HandleAnimationAttackHit;
        _animatorHandler.OnAttackCompleted += HandleAnimationAttackCompleted;
        _skillCaster.OnSkillStarted += HandleSkillStarted;
        _skillCaster.OnSkillEnded += HandleSkillEnded;
        _animatorHandler.OnSkillHit += HandleAnimationSkillHit;
        _animatorHandler.OnSkillCompleted += HandleAnimationSkillCompleted;
        _ultimateCaster.OnSkillStarted += HandleUltimateStarted;
        _ultimateCaster.OnSkillEnded += HandleUltimateEnded;
        _animatorHandler.OnUltimateHit += HandleAnimationUltimateHit;
        _animatorHandler.OnUltimateCompleted += HandleAnimationUltimateCompleted;
        _animatorHandler.OnUltimateReturned += HandleUltimateReturned;
    }

    /// <summary>
    /// 전투 알림과 애니메이션 신호 구독 해제
    /// </summary>
    public void Unbind ( )
    {
        _attacker.OnAttackStarted -= HandleAttackStarted;
        _attacker.OnAttackEnded -= HandleAttackEnded;
        _animatorHandler.OnAttackHit -= HandleAnimationAttackHit;
        _animatorHandler.OnAttackCompleted -= HandleAnimationAttackCompleted;
        _skillCaster.OnSkillStarted -= HandleSkillStarted;
        _skillCaster.OnSkillEnded -= HandleSkillEnded;
        _animatorHandler.OnSkillHit -= HandleAnimationSkillHit;
        _animatorHandler.OnSkillCompleted -= HandleAnimationSkillCompleted;
        _ultimateCaster.OnSkillStarted -= HandleUltimateStarted;
        _ultimateCaster.OnSkillEnded -= HandleUltimateEnded;
        _animatorHandler.OnUltimateHit -= HandleAnimationUltimateHit;
        _animatorHandler.OnUltimateCompleted -= HandleAnimationUltimateCompleted;
        _animatorHandler.OnUltimateReturned -= HandleUltimateReturned;
    }

    /// <summary>
    /// 승인된 궁극기의 전신 재생과 이동 제한
    /// </summary>
    /// <param name="context">원래 궁극기 실행 정보</param>
    void HandleUltimateStarted ( AttackContext context )
    {
        _animatorHandler.PlayUltimate ( context.AttackId );
        OnActionStateChanged?.Invoke ( );
    }

    /// <summary>
    /// 종료한 궁극기의 표현과 행동 상태 정리
    /// </summary>
    /// <param name="ultimateId">종료한 궁극기 회차</param>
    /// <param name="reason">종료 사유</param>
    void HandleUltimateEnded ( int ultimateId , AttackEndReason reason )
    {
        _animatorHandler.StopUltimate ( ultimateId , reason );
        OnActionStateChanged?.Invoke ( );
    }

    /// <summary>
    /// 궁극기 타격 신호를 사용 모듈에 전달
    /// </summary>
    /// <param name="ultimateId">원래 궁극기 회차</param>
    void HandleAnimationUltimateHit ( int ultimateId )
    {
        _ultimateCaster.TryCommitHit ( ultimateId );
    }

    /// <summary>
    /// 궁극기 완료 신호를 사용 모듈에 전달
    /// </summary>
    /// <param name="ultimateId">원래 궁극기 회차</param>
    void HandleAnimationUltimateCompleted ( int ultimateId )
    {
        _ultimateCaster.CompleteSkill ( ultimateId );
    }

    /// <summary>
    /// 승인된 공격의 애니메이션 재생
    /// </summary>
    /// <param name="context">원래 공격 실행 정보</param>
    void HandleAttackStarted ( AttackContext context )
    {
        _animatorHandler.PlayAttack ( context.AttackId );
    }

    /// <summary>
    /// 종료한 공격의 애니메이션 정리
    /// </summary>
    /// <param name="attackId">종료한 공격 회차</param>
    /// <param name="reason">종료 사유</param>
    void HandleAttackEnded ( int attackId , AttackEndReason reason )
    {
        _animatorHandler.StopAttack ( attackId , reason );
    }

    /// <summary>
    /// 애니메이션 타격 신호를 전투 모듈에 전달
    /// </summary>
    /// <param name="attackId">원래 공격 회차</param>
    void HandleAnimationAttackHit ( int attackId )
    {
        _attacker.TryCommitHit ( attackId );
    }

    /// <summary>
    /// 애니메이션 완료 신호를 전투 모듈에 전달
    /// </summary>
    /// <param name="attackId">원래 공격 회차</param>
    void HandleAnimationAttackCompleted ( int attackId )
    {
        _attacker.CompleteAttack ( attackId );
    }

    /// <summary>
    /// 승인된 스킬의 애니메이션 재생
    /// </summary>
    /// <param name="context">원래 스킬 실행 정보</param>
    void HandleSkillStarted ( AttackContext context )
    {
        _animatorHandler.PlaySkill ( context.AttackId );
    }

    /// <summary>
    /// 종료한 스킬의 애니메이션 정리
    /// </summary>
    /// <param name="skillId">종료한 스킬 회차</param>
    /// <param name="reason">종료 사유</param>
    void HandleSkillEnded ( int skillId , AttackEndReason reason )
    {
        _animatorHandler.StopSkill ( skillId , reason );
    }

    /// <summary>
    /// 애니메이션 타격 신호를 스킬 모듈에 전달
    /// </summary>
    /// <param name="skillId">원래 스킬 회차</param>
    void HandleAnimationSkillHit ( int skillId )
    {
        _skillCaster.TryCommitHit ( skillId );
    }

    /// <summary>
    /// 애니메이션 완료 신호를 스킬 모듈에 전달
    /// </summary>
    /// <param name="skillId">원래 스킬 회차</param>
    void HandleAnimationSkillCompleted ( int skillId )
    {
        _skillCaster.CompleteSkill ( skillId );
    }

    /// <summary>
    /// 궁극기 복귀 완료 후 행동 상태 재계산 요청
    /// </summary>
    void HandleUltimateReturned ( )
    {
        OnActionStateChanged?.Invoke ( );
    }
}
