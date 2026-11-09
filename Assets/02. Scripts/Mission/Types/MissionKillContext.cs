/// <summary>
/// 미션 처치 정보 - 인정된 원래 생명과 적 설정 전달
/// </summary>
public readonly struct MissionKillContext
{
    /// <summary>
    /// 원래 도전 번호
    /// </summary>
    public int StageId { get; }
    /// <summary>
    /// 원래 생명 번호
    /// </summary>
    public long LifeId { get; }
    /// <summary>
    /// 적 설정
    /// </summary>
    public EnemyData Enemy { get; }

    /// <summary>
    /// 생성 시 검증된 생명의 처치 정보 생성
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    /// <param name="lifeId">원래 생명 번호</param>
    /// <param name="enemy">적 설정</param>
    public MissionKillContext ( int stageId , long lifeId , EnemyData enemy )
    {
        StageId = stageId;
        LifeId = lifeId;
        Enemy = enemy;
    }
}
