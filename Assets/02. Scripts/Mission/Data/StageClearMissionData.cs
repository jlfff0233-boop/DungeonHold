using UnityEngine;

/// <summary>
/// 스테이지 클리어 미션 데이터 - 적용 맵/난이도의 승리 인정
/// </summary>
[CreateAssetMenu (
    fileName = "StageClearMissionData" , menuName = "Mission/Stage Clear" )]
public class StageClearMissionData : ClearMissionData
{
    /// <summary>
    /// 적용 범위와 승리 확인을 마친 도전의 클리어 인정
    /// </summary>
    /// <param name="progress">최신 미션 진행</param>
    /// <param name="stage">이번 도전 기록</param>
    /// <returns>클리어 조건 충족 여부</returns>
    public override bool IsSatisfied (
        MissionProgress progress , MissionStageRecord stage )
    {
        //적용 난이도는 시작 때, 승리는 최종 평가 진입점에서 확인
        return true;
    }
}
