/// <summary>
/// 미션 최종 결과 - 사건 전달 완료 이후 평가할 기록
/// </summary>
public readonly struct MissionStageResult
{
    /// <summary>
    /// 원래 도전 번호
    /// </summary>
    public int StageId { get; }
    /// <summary>
    /// 승패/포기
    /// </summary>
    public MissionOutcome Outcome { get; }
    /// <summary>
    /// 최종 통과 수
    /// </summary>
    public int PassedCount { get; }
    /// <summary>
    /// 목표 피해 이력
    /// </summary>
    public bool TargetWasDamaged { get; }
    /// <summary>
    /// 방어 기록 확인 여부
    /// </summary>
    public bool DefenseRecordValid { get; }
    /// <summary>
    /// 종료 사유
    /// </summary>
    public string ReasonId { get; }

    /// <summary>
    /// 최종 별, 패배/포기는 0
    /// </summary>
    public int StarCount { get; }

    /// <summary>
    /// 확정된 결과 생성
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    /// <param name="outcome">승패/포기</param>
    /// <param name="passedCount">최종 통과 수</param>
    /// <param name="targetWasDamaged">목표 피해 이력</param>
    /// <param name="defenseRecordValid">방어 기록 유효 여부</param>
    /// <param name="reasonId">종료 사유</param>
    /// <param name="starCount">승리 1~3, 패배/포기 0</param>
    public MissionStageResult ( int stageId , MissionOutcome outcome ,
        int passedCount , bool targetWasDamaged , bool defenseRecordValid ,
        string reasonId , int starCount )
    {
        StageId = stageId;
        Outcome = outcome;
        PassedCount = passedCount;
        TargetWasDamaged = targetWasDamaged;
        DefenseRecordValid = defenseRecordValid;
        ReasonId = reasonId;
        StarCount = starCount;
    }
}
