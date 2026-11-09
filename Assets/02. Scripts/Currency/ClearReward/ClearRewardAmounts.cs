/// <summary>
/// 보상 금액 - 클리어/최초 별/실패 내역
/// </summary>
public struct ClearRewardAmounts
{
    int _clearAmount;    //점수 배율을 반영한 클리어 보상
    int _starAmount;    //미지급 최초 별 보상
    int _failureAmount;    //실패 보상

    /// <summary>
    /// 점수와 난이도 배율을 반영한 클리어 보상
    /// </summary>
    public int ClearAmount => _clearAmount;
    /// <summary>
    /// 이번 최초 별 보상
    /// </summary>
    public int StarAmount => _starAmount;
    /// <summary>
    /// 패배 보상
    /// </summary>
    public int FailureAmount => _failureAmount;
    /// <summary>
    /// 종료 보상 합계
    /// </summary>
    public long Total => ( long ) _clearAmount + _starAmount + _failureAmount;

    /// <summary>
    /// 보상 금액 생성
    /// </summary>
    /// <param name="clearAmount">클리어 금액</param>
    /// <param name="starAmount">별 금액</param>
    /// <param name="failureAmount">실패 금액</param>
    public ClearRewardAmounts (
        int clearAmount , int starAmount , int failureAmount )
    {
        _clearAmount = clearAmount;
        _starAmount = starAmount;
        _failureAmount = failureAmount;
    }
}
