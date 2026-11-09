/// <summary>
/// 미션 표시 상태 - 확정 상태와 미저장 진행 구분
/// </summary>
public readonly struct MissionState
{
    /// <summary>
    /// 미저장 접수를 포함한 진행
    /// </summary>
    public int Progress { get; }
    /// <summary>
    /// 확정된 진행
    /// </summary>
    public int SavedProgress { get; }
    /// <summary>
    /// 목표 수량
    /// </summary>
    public int TargetCount { get; }
    /// <summary>
    /// 저장된 완료
    /// </summary>
    public bool IsCompleted { get; }
    /// <summary>
    /// 저장된 지급
    /// </summary>
    public bool IsRewardPaid { get; }
    /// <summary>
    /// 선행 완료 대기
    /// </summary>
    public bool IsLocked { get; }
    /// <summary>
    /// 해당 상태의 미저장 변경
    /// </summary>
    public bool HasPendingSave { get; }

    /// <summary>
    /// 표시 상태 생성
    /// </summary>
    /// <param name="progress">최신 진행</param>
    /// <param name="savedProgress">확정 진행</param>
    /// <param name="targetCount">목표</param>
    /// <param name="isCompleted">확정 완료</param>
    /// <param name="isRewardPaid">확정 지급</param>
    /// <param name="isLocked">잠금</param>
    /// <param name="hasPendingSave">미저장 변경</param>
    public MissionState ( int progress , int savedProgress , int targetCount ,
        bool isCompleted , bool isRewardPaid , bool isLocked , bool hasPendingSave )
    {
        Progress = progress;
        SavedProgress = savedProgress;
        TargetCount = targetCount;
        IsCompleted = isCompleted;
        IsRewardPaid = isRewardPaid;
        IsLocked = isLocked;
        HasPendingSave = hasPendingSave;
    }
}
