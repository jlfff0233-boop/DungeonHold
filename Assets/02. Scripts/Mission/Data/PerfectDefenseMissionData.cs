using UnityEngine;

/// <summary>
/// 완전 방어 미션 데이터 - 대상 스테이지 방어 이력 평가
/// </summary>
[CreateAssetMenu (
    fileName = "PerfectDefenseMissionData" , menuName = "Mission/Perfect Defense" )]
public class PerfectDefenseMissionData : ClearMissionData
{
    /// <summary>
    /// 완전 방어 미션의 필수 대상 맵 설정 검사
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    public override bool Validate ( )
    {
        return base.Validate ( ) && HasTargetMap;
    }

    /// <summary>
    /// 대상 맵의 완전 방어 규칙과 최종 방어 기록 평가
    /// </summary>
    /// <param name="progress">최신 미션 진행</param>
    /// <param name="stage">이번 도전 기록</param>
    /// <returns>완전 방어 조건 충족 여부</returns>
    public override bool IsSatisfied (
        MissionProgress progress , MissionStageRecord stage )
    {
        MissionStageResult result = stage.Result;
        MissionDefenseRule rule = stage.Context.DefenseRule;

        //승리는 최종 평가 진입점에서 확인했으며 방어 기록 미제공을 통과 0/무피해로 간주하지 않음
        if ( result.DefenseRecordValid == false || rule == MissionDefenseRule.None )
            return false;
        //현재 맵이 요구한 항목을 모두 만족해야 하며 회복 후 HP 대신 피해 발생 이력을 사용
        if ( ( rule & MissionDefenseRule.NoPass ) != 0 && result.PassedCount != 0 )
            return false;
        if ( ( rule & MissionDefenseRule.NoDamage ) != 0 && result.TargetWasDamaged == true )
            return false;
        return true;
    }
}
