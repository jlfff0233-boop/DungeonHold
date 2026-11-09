using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 목록 설정 - 표시 순서와 선택 정보
/// </summary>
[CreateAssetMenu ( fileName = "StageSelectionData" , menuName = "Stage/Selection Data" )]
public class StageSelectionData : ScriptableObject
{
    [SerializeField] LobbyStageInfo [ ] _stages = Array.Empty<LobbyStageInfo> ( ); //선택 목록

    /// <summary>
    /// 선택 목록
    /// </summary>
    public IReadOnlyList<LobbyStageInfo> Stages => _stages;

    /// <summary>
    /// 설정 목록 검사
    /// </summary>
    /// <param name="reason">오류 내용</param>
    /// <returns>사용 가능 여부</returns>
    public bool Validate ( out string reason )
    {
        return ValidateStages ( _stages , out reason );
    }

    /// <summary>
    /// 최종 목록 검사
    /// </summary>
    /// <param name="stages">검사 목록</param>
    /// <param name="reason">오류 내용</param>
    /// <returns>사용 가능 여부</returns>
    public static bool ValidateStages (
        IReadOnlyList<LobbyStageInfo> stages , out string reason )
    {
        reason = null;
        if ( stages.Count == 0 )
        {
            reason = "스테이지 목록이 비어 있습니다.";
            return false;
        }

        HashSet<string> ids = new HashSet<string> ( StringComparer.Ordinal );
        for ( int i = 0 ; i < stages.Count ; i++ )
        {
            LobbyStageInfo stage = stages [ i ];
            if ( stage == null || string.IsNullOrWhiteSpace ( stage.MapId ) == true
                || string.IsNullOrWhiteSpace ( stage.DisplayName ) == true
                || string.IsNullOrWhiteSpace ( stage.Description ) == true
                || stage.Difficulties == null || stage.Difficulties.Count == 0 )
            {
                reason = $"스테이지 {i + 1}번의 ID/이름/설명/난이도를 확인하세요.";
                return false;
            }
            if ( ids.Add ( stage.MapId ) == false )
            {
                reason = $"스테이지 ID 중복: {stage.MapId}";
                return false;
            }

            HashSet<MissionDifficulty> difficulties = new HashSet<MissionDifficulty> ( );
            foreach ( MissionDifficulty difficulty in stage.Difficulties )
            {
                if ( Enum.IsDefined ( typeof ( MissionDifficulty ) , difficulty ) == false
                    || difficulties.Add ( difficulty ) == false )
                {
                    reason = $"{stage.MapId}: 정의되지 않았거나 중복된 난이도입니다.";
                    return false;
                }
            }
        }
        return true;
    }
}
