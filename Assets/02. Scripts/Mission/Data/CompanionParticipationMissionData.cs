using UnityEngine;

/// <summary>
/// 동료 참여 미션 데이터 - 실제 플레이에 참여한 동료 수 평가
/// </summary>
[CreateAssetMenu (
    fileName = "CompanionParticipationMissionData" , menuName = "Mission/Companion Participation" )]
public class CompanionParticipationMissionData : ClearMissionData
{
    [SerializeField] MissionCountCondition _condition;      //참여 동료 수 조건

    /// <summary>
    /// 동료 참여 수 조건 설정 검사
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    public override bool Validate ( )
    {
        return base.Validate ( ) && _condition.Validate ( );
    }

    /// <summary>
    /// 서로 다른 고용 번호의 플레이 참여 수 조건 평가
    /// </summary>
    /// <param name="progress">최신 미션 진행</param>
    /// <param name="stage">이번 도전 기록</param>
    /// <returns>조건 충족 여부</returns>
    public override bool IsSatisfied (
        MissionProgress progress , MissionStageRecord stage )
    {
        return _condition.IsSatisfied ( stage.CompanionParticipantCount );
    }
}
