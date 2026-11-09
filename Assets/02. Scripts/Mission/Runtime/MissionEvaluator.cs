using System.Collections.Generic;

/// <summary>
/// 미션 평가기 - 적용 목록을 선행 순서대로 평가
/// </summary>
public class MissionEvaluator
{
    /// <summary>
    /// 준비된 미션 목록의 최초 완료 평가
    /// </summary>
    /// <param name="missions">적용 대상이며 정렬된 목록</param>
    /// <param name="progress">최신 작업 상태</param>
    /// <param name="stage">이번 도전 기록</param>
    public void Evaluate (
        IReadOnlyList<MissionData> missions ,
        MissionProgress progress ,
        MissionStageRecord stage )
    {
        MissionDifficulty difficulty = stage.Context.Difficulty;

        //호출부가 도전 적용 범위와 평가 시점(처치/승리)을 정한 목록을 전달
        //이미 선행 순서로 정렬돼 있어 재평가 반복 없이 같은 순회에서 후행 조건까지 판단
        foreach ( MissionData data in missions )
        {
            if ( progress.IsCompleted ( data.MissionId , difficulty ) == true )
                continue;

            if ( string.IsNullOrEmpty ( data.PrerequisiteId ) == false
                && progress.IsCompleted ( data.PrerequisiteId , difficulty ) == false )
                continue;

            if ( data.IsSatisfied ( progress , stage ) == false )
                continue;

            //이번 순회의 신규 완료도 다음 미션의 선행 검사에 반영하되 저장/보상은 Controller가 처리
            progress.Complete ( data.MissionId , difficulty );
        }
    }
}
