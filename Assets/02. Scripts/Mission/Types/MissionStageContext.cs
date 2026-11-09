/// <summary>
/// 미션 도전 정보 - 원래 도전과 적용 범위 전달
/// </summary>
public readonly struct MissionStageContext
{
    /// <summary>
    /// 원래 도전 번호
    /// </summary>
    public int StageId { get; }
    /// <summary>
    /// 맵 고정 ID
    /// </summary>
    public string MapId { get; }
    /// <summary>
    /// 실제 난이도
    /// </summary>
    public MissionDifficulty Difficulty { get; }
    /// <summary>
    /// 적용 방어 조건
    /// </summary>
    public MissionDefenseRule DefenseRule { get; }

    /// <summary>
    /// 확정된 도전 정보 생성
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    /// <param name="mapId">맵 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="defenseRule">방어 조건</param>
    public MissionStageContext ( int stageId , string mapId ,
        MissionDifficulty difficulty , MissionDefenseRule defenseRule )
    {
        StageId = stageId;
        MapId = mapId;
        Difficulty = difficulty;
        DefenseRule = defenseRule;
    }
}
