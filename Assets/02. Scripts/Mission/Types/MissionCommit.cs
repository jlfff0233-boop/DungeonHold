using System.Collections.Generic;

/// <summary>
/// 미션 저장 결과 - 내부 확정 이후 알림 정보 전달
/// </summary>
public readonly struct MissionCommit
{
    /// <summary>
    /// 저장한 변경 존재
    /// </summary>
    public bool HasChanges { get; }
    /// <summary>
    /// 저장된 잔액
    /// </summary>
    public int SavedBalance { get; }
    /// <summary>
    /// 합산 보상
    /// </summary>
    public int RewardAmount { get; }
    /// <summary>
    /// 신규 지급 목록
    /// </summary>
    public IReadOnlyList<MissionReward> NewlyPaid { get; }

    /// <summary>
    /// 저장에 성공한 결과 생성
    /// </summary>
    /// <param name="savedBalance">저장 잔액</param>
    /// <param name="rewardAmount">합산 보상</param>
    /// <param name="newlyPaid">신규 지급 목록</param>
    public MissionCommit (
        int savedBalance , int rewardAmount ,
        IReadOnlyList<MissionReward> newlyPaid )
    {
        HasChanges = true;
        SavedBalance = savedBalance;
        RewardAmount = rewardAmount;
        NewlyPaid = newlyPaid;
    }
}
