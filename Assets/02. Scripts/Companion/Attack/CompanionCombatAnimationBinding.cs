using System;
using UnityEngine;

/// <summary>
/// 동료 전투 애니메이션 연결 - 공격 알림과 타격/완료 신호 전달
/// </summary>
public class CompanionCombatAnimationBinding
{
    Companion _companion;    //공격 로그의 동료와 출력 문맥
    Attacker _attacker;    //공격 실행 모듈
    AnimatorHandler _animatorHandler;    //공격 표현과 애니메이션 신호
    Func<Vector3?> _getTargetPosition;    //적중 시점의 조준 위치 조회
    Func<bool> _getShowDebugLog;    //현재 디버그 출력 설정 조회
    ITargetedAttack _targetedAttack;    //초기화된 실행자의 조준 기능

    /// <summary>
    /// 공격 모듈, 표현 모듈과 동료의 조준 위치 조회 연결
    /// </summary>
    /// <param name="companion">공격하는 동료</param>
    /// <param name="attacker">공격 모듈</param>
    /// <param name="animatorHandler">애니메이션 모듈</param>
    /// <param name="getTargetPosition">현재 대상의 피격 중심 조회</param>
    /// <param name="getShowDebugLog">현재 로그 출력 설정 조회</param>
    public CompanionCombatAnimationBinding (
        Companion companion , Attacker attacker , AnimatorHandler animatorHandler ,
        Func<Vector3?> getTargetPosition , Func<bool> getShowDebugLog )
    {
        _companion = companion;
        _attacker = attacker;
        _animatorHandler = animatorHandler;
        _getTargetPosition = getTargetPosition;
        _getShowDebugLog = getShowDebugLog;
    }

    /// <summary>
    /// 공격 모듈 초기화 후 현재 실행자의 조준 기능 갱신
    /// </summary>
    public void Init ( )
    {
        _targetedAttack = _attacker.Executor as ITargetedAttack;
    }

    /// <summary>
    /// 활성 수명의 공격과 애니메이션 신호 구독
    /// </summary>
    public void Bind ( )
    {
        _attacker.OnAttackStarted += HandleAttackStarted;
        _attacker.OnAttackEnded += HandleAttackEnded;
        _animatorHandler.OnAttackHit += HandleAnimationAttackHit;
        _animatorHandler.OnAttackCompleted += HandleAnimationAttackCompleted;
    }

    /// <summary>
    /// 비활성화 시 새 공격과 애니메이션 신호를 차단하고 종료 수신 유지
    /// </summary>
    public void StopListening ( )
    {
        _attacker.OnAttackStarted -= HandleAttackStarted;
        _animatorHandler.OnAttackHit -= HandleAnimationAttackHit;
        _animatorHandler.OnAttackCompleted -= HandleAnimationAttackCompleted;
    }

    /// <summary>
    /// 공격 취소 알림까지 반영한 뒤 남은 종료 구독 해제
    /// </summary>
    public void Unbind ( )
    {
        _attacker.OnAttackEnded -= HandleAttackEnded;
    }

    /// <summary>
    /// 승인된 공격의 애니메이션 재생
    /// </summary>
    /// <param name="context">시작한 공격 정보</param>
    void HandleAttackStarted ( AttackContext context )
    {
        if ( _getShowDebugLog ( ) == true )
            Debug.Log ( $"[동료:{_companion.name}] 공격 시작 "
                + $"/ 회차:{context.AttackId}" , _companion );

        _animatorHandler.PlayAttack ( context.AttackId );
    }

    /// <summary>
    /// 종료한 공격의 표현 정리
    /// </summary>
    /// <param name="attackId">종료한 공격 회차</param>
    /// <param name="reason">종료 사유</param>
    void HandleAttackEnded ( int attackId , AttackEndReason reason )
    {
        _animatorHandler.StopAttack ( attackId , reason );

        if ( _getShowDebugLog ( ) == true )
            Debug.Log ( $"[동료:{_companion.name}] 공격 종료 "
                + $"/ 회차:{attackId} / 사유:{reason}" , _companion );
    }

    /// <summary>
    /// 조준 위치 반영 후 같은 공격 회차의 적중 신호 전달
    /// </summary>
    /// <param name="attackId">애니메이션의 공격 회차</param>
    void HandleAnimationAttackHit ( int attackId )
    {
        if ( _targetedAttack != null )
        {
            Vector3? targetPosition = _getTargetPosition ( );
            if ( targetPosition.HasValue == false )
                return;

            _targetedAttack.SetTargetPosition ( targetPosition.Value );
        }

        _attacker.TryCommitHit ( attackId );
    }

    /// <summary>
    /// 애니메이션 완료를 공격 모듈에 전달
    /// </summary>
    /// <param name="attackId">애니메이션의 공격 회차</param>
    void HandleAnimationAttackCompleted ( int attackId )
    {
        _attacker.CompleteAttack ( attackId );
    }
}
