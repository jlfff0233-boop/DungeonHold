/// <summary>
/// 보상 입력 - 도전 결과와 보상 기준
/// </summary>
public struct ClearRewardInput
{
    MissionStageContext _context;    //도전 정보
    MissionStageResult _result;    //확정 결과
    int _score;    //확정 점수
    int _targetScore;    //시작 때 확보한 기준 점수
    int _paidStars;    //기존 별 지급 단계

    /// <summary>
    /// 보상 계산 대상 도전
    /// </summary>
    public MissionStageContext Context => _context;
    /// <summary>
    /// 접수한 승패와 별 결과
    /// </summary>
    public MissionStageResult Result => _result;
    /// <summary>
    /// 이번 클리어의 확정 점수
    /// </summary>
    public int Score => _score;
    /// <summary>
    /// 도전 시작 시 확정한 보상 기준 점수
    /// </summary>
    public int TargetScore => _targetScore;
    /// <summary>
    /// 기존 별 정책을 반영한 지급 단계
    /// </summary>
    public int PaidStars => _paidStars;

    /// <summary>
    /// 계산 입력 생성
    /// </summary>
    /// <param name="context">도전 정보</param>
    /// <param name="result">종료 결과</param>
    /// <param name="score">확정 점수</param>
    /// <param name="targetScore">기준 점수</param>
    /// <param name="paidStars">지급 단계</param>
    public ClearRewardInput (
        MissionStageContext context ,
        MissionStageResult result ,
        int score , int targetScore , int paidStars )
    {
        _context = context;
        _result = result;
        _score = score;
        _targetScore = targetScore;
        _paidStars = paidStars;
    }
}
