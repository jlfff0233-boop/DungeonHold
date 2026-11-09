using UnityEngine;

/// <summary>
/// 아이템 사용 미션 데이터 - 플레이 중 사용 횟수 평가
/// </summary>
[CreateAssetMenu (
    fileName = "ItemUseMissionData" , menuName = "Mission/Item Use" )]
public class ItemUseMissionData : ClearMissionData
{
    [SerializeField] MissionCountCondition _condition;      //사용 횟수 조건

    /// <summary>
    /// 아이템 사용 횟수 조건 설정 검사
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    public override bool Validate ( )
    {
        return base.Validate ( ) && _condition.Validate ( );
    }

    /// <summary>
    /// 플레이 중 아이템 사용 횟수 조건 평가
    /// </summary>
    /// <param name="progress">최신 미션 진행</param>
    /// <param name="stage">이번 도전 기록</param>
    /// <returns>조건 충족 여부</returns>
    public override bool IsSatisfied (
        MissionProgress progress , MissionStageRecord stage )
    {
        return _condition.IsSatisfied ( stage.ItemUseCount );
    }
}
