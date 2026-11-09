/// <summary>
/// 저장 결과 - 지급 내역과 확정 잔액
/// </summary>
public struct ClearRewardCommit
{
    ClearRewardRecord _record;    //저장한 지급 내역
    int _savedBalance;    //확정 영구 잔액

    /// <summary>
    /// 저장에 성공한 지급 내역
    /// </summary>
    public ClearRewardRecord Record => _record;
    /// <summary>
    /// 보상 저장 후 확정된 영구 재화 잔액
    /// </summary>
    public int SavedBalance => _savedBalance;

    /// <summary>
    /// 저장 결과 생성
    /// </summary>
    /// <param name="record">지급 내역</param>
    /// <param name="savedBalance">확정 잔액</param>
    public ClearRewardCommit (
        ClearRewardRecord record , int savedBalance )
    {
        _record = record;
        _savedBalance = savedBalance;
    }
}
