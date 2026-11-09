/// <summary>
/// 정산 계획 - 재시도에서 유지할 계산 결과
/// </summary>
public class ClearRewardPlan
{
    string _settlementId;    //저장용 도전 식별자
    ClearRewardInput _input;    //계산에 사용한 기록
    ClearRewardAmounts _amounts;    //계산 금액

    /// <summary>
    /// 중복 지급을 구분할 정산 ID
    /// </summary>
    public string SettlementId => _settlementId;
    /// <summary>
    /// 계산에 사용한 도전 결과와 기준
    /// </summary>
    public ClearRewardInput Input => _input;
    /// <summary>
    /// 저장 재시도에서도 유지할 계산 금액
    /// </summary>
    public ClearRewardAmounts Amounts => _amounts;

    /// <summary>
    /// 정산 계획 생성
    /// </summary>
    /// <param name="settlementId">정산 ID</param>
    /// <param name="input">계산 입력</param>
    /// <param name="amounts">계산 금액</param>
    public ClearRewardPlan (
        string settlementId ,
        ClearRewardInput input ,
        ClearRewardAmounts amounts )
    {
        _settlementId = settlementId;
        _input = input;
        _amounts = amounts;
    }
}
