using System.Collections.Generic;

/// <summary>
/// 도감 해금 결과 - 원래 도전에서 실제로 확정한 해금과 영구 보상
/// </summary>
public class CollectionUnlockInfo
{
    int _stageId;    //결과의 원래 도전 번호
    IReadOnlyList<string> _enemyIds;    //실제로 해금한 읽기 전용 ID 목록
    int _rewardAmount;    //실제로 저장한 최초 보상 합계

    /// <summary>
    /// 해금 결과가 확정된 도전 번호
    /// </summary>
    public int StageId => _stageId;

    /// <summary>
    /// 이번 도전에서 최초 해금한 적 ID 목록
    /// </summary>
    public IReadOnlyList<string> EnemyIds => _enemyIds;

    /// <summary>
    /// 이번 해금으로 저장한 영구 재화 보상 합계
    /// </summary>
    public int RewardAmount => _rewardAmount;

    /// <summary>
    /// 실행부가 소유한 읽기 전용 목록으로 결과 보관
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    /// <param name="enemyIds">이후 변경하지 않을 해금 목록</param>
    /// <param name="rewardAmount">확정 보상</param>
    public CollectionUnlockInfo (
        int stageId,
        IReadOnlyList<string> enemyIds,
        int rewardAmount )
    {
        _stageId = stageId;
        _enemyIds = enemyIds;
        _rewardAmount = rewardAmount;
    }
}
