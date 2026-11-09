using UnityEngine;

/// <summary>
/// 클리어 미션 데이터 - 대상 맵 범위 설정
/// </summary>
public abstract class ClearMissionData : MissionData
{
    [SerializeField] string _mapId;     //빈 값이면 맵 공통

    /// <summary>
    /// 대상 맵 존재 여부
    /// </summary>
    protected bool HasTargetMap => string.IsNullOrWhiteSpace ( _mapId ) == false;

    /// <summary>
    /// 적용 난이도와 대상 맵 범위 확인
    /// </summary>
    /// <param name="context">확정된 도전 정보</param>
    /// <returns>적용 여부</returns>
    public override bool MatchesStage ( MissionStageContext context )
    {
        return base.MatchesStage ( context )
            && ( string.IsNullOrEmpty ( _mapId ) || _mapId == context.MapId );
    }
}
