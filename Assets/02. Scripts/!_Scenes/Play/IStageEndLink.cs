/// <summary>
/// 스테이지 종료 요청 연결
/// </summary>
public interface IStageEndLink
{
    /// <summary>
    /// 마지막 요청 오류
    /// </summary>
    string Error { get; }

    /// <summary>
    /// 종료 요청
    /// </summary>
    /// <param name="stageId">도전</param>
    /// <returns>접수 여부</returns>
    bool TryEndStage ( int stageId );

    /// <summary>
    /// 마지막 사건 전달 재시도
    /// </summary>
    /// <param name="stageId">도전</param>
    /// <returns>접수 여부</returns>
    bool TryCompleteEndEvents ( int stageId );
}
