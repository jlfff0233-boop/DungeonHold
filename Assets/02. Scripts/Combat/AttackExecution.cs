using System;

/// <summary>
/// 공격 실행 - 원래 실행 정보, 최초 유효 적중과 실행 수명 관리
/// </summary>
public class AttackExecution
{
    readonly Action<AttackContext> _onFirstValidHit;      //최초 유효 적중 전달
    readonly Action<AttackExecution> _onCompleted;       //실행 종료 전달

    bool _isActive = true;      //실행 유효 여부
    bool _hasValidHit;          //최초 유효 적중 보고 여부

    /// <summary>
    /// 원래 공격 실행 정보
    /// </summary>
    public AttackContext Context { get; }

    /// <summary>
    /// 실행 유효 여부
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// 이번 실행의 정보와 결과 전달 함수 설정
    /// </summary>
    /// <param name="context">원래 공격 실행 정보</param>
    /// <param name="onFirstValidHit">최초 유효 적중 전달 함수</param>
    /// <param name="onCompleted">실행 종료 전달 함수</param>
    public AttackExecution (
        AttackContext context ,
        Action<AttackContext> onFirstValidHit ,
        Action<AttackExecution> onCompleted )
    {
        Context = context;
        _onFirstValidHit = onFirstValidHit;
        _onCompleted = onCompleted;
    }

    /// <summary>
    /// 실제 적용 피해에 따른 최초 유효 적중 보고
    /// </summary>
    /// <param name="appliedDamage">실제 적용 피해량</param>
    public void ReportAppliedDamage ( float appliedDamage )
    {
        if ( _isActive == false
            || _hasValidHit == true
            || appliedDamage <= 0f )
            return;

        //알림보다 먼저 기록하여 같은 실행의 중복 충전 방지
        _hasValidHit = true;
        _onFirstValidHit?.Invoke ( Context );
    }

    /// <summary>
    /// 정상 실행 종료와 관리 목록 해제
    /// </summary>
    public void Complete ( )
    {
        if ( _isActive == false )
            return;

        _isActive = false;
        _onCompleted ( this );
    }

    /// <summary>
    /// 외부 정리에 따른 실행 무효화
    /// </summary>
    public void Cancel ( )
    {
        _isActive = false;
    }
}
