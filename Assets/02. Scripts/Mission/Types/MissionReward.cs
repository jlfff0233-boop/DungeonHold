/// <summary>
/// 미션 보상 결과 - 최초 지급 당시 정보 전달
/// </summary>
public readonly struct MissionReward
{
    /// <summary>
    /// 미션 ID
    /// </summary>
    public string MissionId { get; }
    /// <summary>
    /// 완료 난이도
    /// </summary>
    public MissionDifficulty Difficulty { get; }
    /// <summary>
    /// 실제 지급액
    /// </summary>
    public int Amount { get; }

    /// <summary>
    /// 최초 보상 결과 생성
    /// </summary>
    /// <param name="missionId">미션 ID</param>
    /// <param name="difficulty">완료 난이도</param>
    /// <param name="amount">지급액</param>
    public MissionReward (
        string missionId , MissionDifficulty difficulty , int amount )
    {
        MissionId = missionId;
        Difficulty = difficulty;
        Amount = amount;
    }
}
