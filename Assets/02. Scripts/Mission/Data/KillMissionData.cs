using UnityEngine;

/// <summary>
/// 처치 미션 데이터 - 처치 집계의 목표 수량 평가
/// </summary>
[CreateAssetMenu ( fileName = "KillMissionData" , menuName = "Mission/Kill" )]
public class KillMissionData : MissionData
{
    [SerializeField] MissionKillCountData _killCountData;       //처치 집계 설정
    [SerializeField] int _targetCount = 1;            //완료 목표

    /// <summary>
    /// 처치 집계 설정
    /// </summary>
    public MissionKillCountData KillCountData => _killCountData;
    /// <summary>
    /// 완료 목표
    /// </summary>
    public override int TargetCount => _targetCount;

    /// <summary>
    /// 처치 집계 참조와 목표 수량 설정 검사
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    public override bool Validate ( )
    {
        return base.Validate ( ) && _killCountData != null && _targetCount > 0;
    }

    /// <summary>
    /// 적용 난이도와 처치 집계의 맵 범위 확인
    /// </summary>
    /// <param name="context">확정된 도전 정보</param>
    /// <returns>적용 여부</returns>
    public override bool MatchesStage ( MissionStageContext context )
    {
        return base.MatchesStage ( context ) && _killCountData.MatchesMap ( context.MapId );
    }

    /// <summary>
    /// 난이도별 처치 집계가 목표 수량에 도달했는지 평가
    /// </summary>
    /// <param name="progress">최신 미션 진행</param>
    /// <param name="stage">이번 도전 기록</param>
    /// <returns>목표 도달 여부</returns>
    public override bool IsSatisfied (
        MissionProgress progress , MissionStageRecord stage )
    {
        return progress.GetKillCount ( _killCountData.KillCountId , stage.Context.Difficulty ) >= _targetCount;
    }

    /// <summary>
    /// 난이도별 현재/저장 처치 수 조회
    /// </summary>
    /// <param name="progress">슬롯의 미션 진행</param>
    /// <param name="difficulty">조회 난이도</param>
    /// <param name="saved">저장 완료한 상태 조회 여부</param>
    /// <returns>처치 집계 수</returns>
    public override int GetProgress (
        MissionProgress progress , MissionDifficulty difficulty , bool saved )
    {
        return progress.GetKillCount ( _killCountData.KillCountId , difficulty , saved );
    }
}
