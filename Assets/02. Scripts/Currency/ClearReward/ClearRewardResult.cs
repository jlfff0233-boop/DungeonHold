/// <summary>
/// 보상 처리 상태 - 계산과 지급 완료 구분
/// </summary>
public enum ClearRewardState
{
    Pending,        //사건/계산 대기
    Prepared,       //계산 완료
    Committed,      //저장 완료
    NotEligible     //포기로 지급 제외
}

/// <summary>
/// 보상 조회 결과 - 원래 도전의 계산/실제 지급 상태
/// </summary>
public struct ClearRewardResult
{
    int _stageId;    //런타임 도전 번호
    bool _hasFinalScore;    //정상 0점과 미확정 구분
    int _score;    //확정 점수
    ClearRewardState _state;    //처리 단계
    ClearRewardRecord _record;    //계산 또는 지급 내역

    /// <summary>
    /// 보상 결과의 도전 번호
    /// </summary>
    public int StageId => _stageId;
    /// <summary>
    /// 클리어 점수 확정 여부, 정상 0점 포함
    /// </summary>
    public bool HasFinalScore => _hasFinalScore;
    /// <summary>
    /// 이번 클리어의 확정 점수
    /// </summary>
    public int Score => _score;
    /// <summary>
    /// 보상 계산과 저장 처리 단계
    /// </summary>
    public ClearRewardState State => _state;
    /// <summary>
    /// 계산된 보상 금액
    /// </summary>
    public ClearRewardAmounts CalculatedAmounts => _record != null ? _record.Amounts : default;
    /// <summary>
    /// 실제 지급 보상 금액
    /// </summary>
    public ClearRewardAmounts PaidAmounts => _state == ClearRewardState.Committed ? _record.Amounts : default;

    /// <summary>
    /// 사건 접수 상태 생성
    /// </summary>
    /// <param name="stageId">도전 번호</param>
    /// <param name="hasScore">점수 확정 여부</param>
    /// <param name="score">확정 점수</param>
    /// <param name="state">처리 단계</param>
    public ClearRewardResult (
        int stageId , bool hasScore , int score , ClearRewardState state )
    {
        _stageId = stageId;
        _hasFinalScore = hasScore;
        _score = score;
        _state = state;
        _record = null;
    }

    /// <summary>
    /// 계산/저장 결과 생성
    /// </summary>
    /// <param name="stageId">도전 번호</param>
    /// <param name="record">보상 내역</param>
    /// <param name="state">처리 단계</param>
    public ClearRewardResult (
        int stageId , ClearRewardRecord record , ClearRewardState state )
    {
        _stageId = stageId;
        _hasFinalScore = record.Outcome == MissionOutcome.Victory;
        _score = record.Score;
        _state = state;
        _record = record;
    }
}
