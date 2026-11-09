#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 클리어 보상 CSV 임포터 - 기준 점수 검사와 반영
/// </summary>
public static class ClearRewardCsvImporter
{
    static string [ ] _headers = { "MapId", "EasyScore", "NormalScore", "HardScore" }; //CSV 컬럼
    static MissionDifficulty [ ] _difficulties =
    {
        MissionDifficulty.Easy, MissionDifficulty.Normal, MissionDifficulty.Hard
    }; //점수 컬럼별 난이도

    /// <summary>
    /// 임포트 계획 - 입력과 변경 미리보기
    /// </summary>
    public class Plan
    {
        public string CsvPath; //입력 경로
        public string Source; //검사한 CSV
        public ClearRewardData Target; //반영할 보상 설정
        public StageSelectionData Stages; //맵과 지원 난이도
        public string TargetSnapshot; //검사한 보상 설정
        public string StageSnapshot; //검사한 스테이지 목록
        public bool SavePending; //보상 설정의 미저장 변경
        public List<(string MapId, MissionDifficulty Difficulty, int Score)> Targets = new ( ); //최종 기준 점수
        public List<string> Changes = new ( ); //점수 변경과 삭제 목록
        public string Error; //적용을 막는 오류
    }

    /// <summary>
    /// CSV 행 - 시작 줄과 셀
    /// </summary>
    class CsvRow
    {
        public int Line; //행 번호
        public string [ ] Cells; //셀 목록
    }

    #region ----- 검사/적용 -----

    /// <summary>
    /// CSV 검사와 변경 미리보기 준비
    /// </summary>
    /// <param name="csvPath">CSV 경로</param>
    /// <param name="target">보상 설정</param>
    /// <param name="stages">스테이지 목록</param>
    /// <returns>검사 결과와 반영 계획</returns>
    public static Plan Prepare ( string csvPath, ClearRewardData target, StageSelectionData stages )
    {
        Plan plan = new Plan { CsvPath = csvPath, Target = target, Stages = stages };
        if ( EditorApplication.isPlayingOrWillChangePlaymode == true )
        {
            plan.Error = "편집 모드에서 검사/적용하세요.";
            return plan;
        }
        if ( target == null || AssetDatabase.IsMainAsset ( target ) == false
            || stages == null || AssetDatabase.IsMainAsset ( stages ) == false )
        {
            plan.Error = "저장된 ClearRewardData와 StageSelectionData를 연결하세요.";
            return plan;
        }
        if ( stages.Validate ( out string reason ) == false )
        {
            plan.Error = reason;
            return plan;
        }
        if ( string.IsNullOrWhiteSpace ( csvPath ) == true
            || string.Equals ( Path.GetExtension ( csvPath ), ".csv", StringComparison.OrdinalIgnoreCase ) == false )
        {
            plan.Error = "CSV 파일을 선택하세요.";
            return plan;
        }

        try
        {
            plan.Source = File.ReadAllText ( csvPath, new UTF8Encoding ( false, true ) );
        }
        catch ( Exception exception ) when ( exception is IOException
            || exception is UnauthorizedAccessException || exception is DecoderFallbackException )
        {
            plan.Error = "CSV 읽기 실패: " + exception.Message;
            return plan;
        }

        plan.TargetSnapshot = EditorJsonUtility.ToJson ( target );
        plan.StageSnapshot = EditorJsonUtility.ToJson ( stages );
        plan.SavePending = EditorUtility.IsDirty ( target );
        ReadTargets ( plan );
        if ( plan.Error == null )
            BuildPreview ( plan );
        return plan;
    }

    /// <summary>
    /// 미리보기와 입력을 대조한 뒤 기준 점수 저장
    /// </summary>
    /// <param name="plan">검사한 반영 계획</param>
    /// <param name="message">처리 결과</param>
    /// <returns>저장 완료 여부</returns>
    public static bool TryApply ( Plan plan, out string message )
    {
        //검사 이후 CSV나 Inspector가 바뀌면 이전 미리보기로 적용하지 않음
        Plan latest = Prepare ( plan.CsvPath, plan.Target, plan.Stages );
        if ( latest.Error != null )
        {
            message = latest.Error;
            return false;
        }
        if ( latest.Source != plan.Source || latest.TargetSnapshot != plan.TargetSnapshot
            || latest.StageSnapshot != plan.StageSnapshot || latest.SavePending != plan.SavePending )
        {
            message = "입력 또는 설정이 바뀌었습니다. 다시 검사하세요.";
            return false;
        }

        Undo.RegisterCompleteObjectUndo ( latest.Target, "Import Clear Reward CSV" );
        using ( SerializedObject serialized = new SerializedObject ( latest.Target ) )
        {
            //다른 보상 설정은 유지하고 검사한 전체 목록만 교체
            SerializedProperty targets = serialized.FindProperty ( "_targets" );
            targets.arraySize = latest.Targets.Count;
            for ( int i = 0; i < latest.Targets.Count; i++ )
            {
                var source = latest.Targets [ i ];
                SerializedProperty target = targets.GetArrayElementAtIndex ( i );
                target.FindPropertyRelative ( "_mapId" ).stringValue = source.MapId;
                target.FindPropertyRelative ( "_difficulty" ).intValue = ( int ) source.Difficulty;
                target.FindPropertyRelative ( "_score" ).intValue = source.Score;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo ( );
        }
        EditorUtility.SetDirty ( latest.Target );
        AssetDatabase.SaveAssetIfDirty ( latest.Target );
        if ( EditorUtility.IsDirty ( latest.Target ) == true )
        {
            message = "기준 점수는 반영됐으나 저장되지 않았습니다. Console과 설정 에셋을 확인하세요.";
            return false;
        }
        message = $"기준 점수 {latest.Targets.Count}개 저장 완료.";
        return true;
    }

    /// <summary>
    /// 맵별 CSV를 지원 난이도의 기준 점수로 변환
    /// </summary>
    /// <param name="plan">검사할 반영 계획</param>
    static void ReadTargets ( Plan plan )
    {
        if ( TryParseCsv ( plan.Source, out List<CsvRow> rows, out string error ) == false )
        {
            plan.Error = error;
            return;
        }
        if ( rows.Count < 2 || rows [ 0 ].Cells.Length != _headers.Length )
        {
            plan.Error = "헤더 4개와 맵별 점수 행이 필요합니다.";
            return;
        }
        for ( int i = 0; i < _headers.Length; i++ )
        {
            if ( rows [ 0 ].Cells [ i ] != _headers [ i ] )
            {
                plan.Error = "헤더 순서: MapId,EasyScore,NormalScore,HardScore";
                return;
            }
        }

        Dictionary<string, LobbyStageInfo> stages = new ( StringComparer.Ordinal );
        foreach ( LobbyStageInfo stage in plan.Stages.Stages )
            stages.Add ( stage.MapId, stage );

        HashSet<string> imported = new ( StringComparer.Ordinal );
        for ( int i = 1; i < rows.Count; i++ )
        {
            CsvRow row = rows [ i ];
            if ( row.Cells.Length != _headers.Length )
            {
                plan.Error = $"CSV {row.Line}행: 컬럼이 4개여야 합니다.";
                return;
            }
            string mapId = row.Cells [ 0 ];
            if ( stages.TryGetValue ( mapId, out LobbyStageInfo stage ) == false )
            {
                plan.Error = $"CSV {row.Line}행: 스테이지 목록에 없는 맵입니다: {mapId}";
                return;
            }
            if ( imported.Add ( mapId ) == false )
            {
                plan.Error = $"CSV {row.Line}행: 맵 ID 중복: {mapId}";
                return;
            }
            for ( int j = 0; j < _difficulties.Length; j++ )
            {
                if ( int.TryParse ( row.Cells [ j + 1 ], NumberStyles.None,
                    CultureInfo.InvariantCulture, out int score ) == false || score <= 0 )
                {
                    plan.Error = $"CSV {row.Line}행: {_headers [ j + 1 ]}는 양수 정수여야 합니다.";
                    return;
                }
                MissionDifficulty difficulty = _difficulties [ j ];
                if ( stage.Supports ( difficulty ) == true )
                    plan.Targets.Add ( (mapId, difficulty, score) );
            }
        }

        //선택 가능한 맵이 CSV에서 빠지면 도전 시작 전에 누락을 알림
        foreach ( LobbyStageInfo stage in plan.Stages.Stages )
        {
            if ( imported.Contains ( stage.MapId ) == false )
            {
                plan.Error = $"CSV에 맵이 누락됐습니다: {stage.MapId}";
                return;
            }
        }
    }

    /// <summary>
    /// 기존 점수와 추가/변경/삭제 항목 비교
    /// </summary>
    /// <param name="plan">검사한 반영 계획</param>
    static void BuildPreview ( Plan plan )
    {
        Dictionary<(string, MissionDifficulty), int> existing = new ( );
        using ( SerializedObject serialized = new SerializedObject ( plan.Target ) )
        {
            SerializedProperty targets = serialized.FindProperty ( "_targets" );
            for ( int i = 0; i < targets.arraySize; i++ )
            {
                SerializedProperty target = targets.GetArrayElementAtIndex ( i );
                string mapId = target.FindPropertyRelative ( "_mapId" ).stringValue;
                MissionDifficulty difficulty = ( MissionDifficulty ) target.FindPropertyRelative ( "_difficulty" ).intValue;
                existing [ (mapId, difficulty) ] = target.FindPropertyRelative ( "_score" ).intValue;
            }
        }

        foreach ( var target in plan.Targets )
        {
            var key = (target.MapId, target.Difficulty);
            string label = $"{target.MapId} / {target.Difficulty}";
            if ( existing.TryGetValue ( key, out int before ) == false )
                plan.Changes.Add ( $"[추가] {label}: {target.Score}" );
            else
            {
                plan.Changes.Add ( before == target.Score
                    ? $"[유지] {label}: {before}"
                    : $"[변경] {label}: {before} → {target.Score}" );
                existing.Remove ( key );
            }
        }
        foreach ( var target in existing )
            plan.Changes.Add ( $"[삭제] {target.Key.Item1} / {target.Key.Item2}: {target.Value}" );
    }

    #endregion

    #region ----- CSV 구문 -----

    /// <summary>
    /// 따옴표와 여러 줄 셀을 포함한 CSV 해석
    /// </summary>
    /// <param name="text">원문</param>
    /// <param name="rows">빈 줄을 제외한 행</param>
    /// <param name="error">구문 오류</param>
    /// <returns>해석 여부</returns>
    static bool TryParseCsv ( string text, out List<CsvRow> rows, out string error )
    {
        rows = new List<CsvRow>( );
        error = null;
        List<string> cells = new List<string>( );
        StringBuilder cell = new StringBuilder( );
        bool quoted = false;
        bool closed = false;
        int line = 1;
        int startLine = 1;
        text = text.TrimStart( '\uFEFF' );
        for ( int i = 0; i <= text.Length; i++ )
        {
            bool end = i == text.Length;
            char character = end == true ? '\n' : text [ i ];
            if ( quoted == true )
            {
                if ( end == true )
                {
                    error = $"CSV {startLine}행: 닫히지 않은 따옴표입니다.";
                    return false;
                }
                if ( character == '"' )
                {
                    if ( i + 1 < text.Length && text [ i + 1 ] == '"' )
                    {
                        cell.Append( '"' );
                        i++;
                    }
                    else
                    {
                        quoted = false;
                        closed = true;
                    }
                }
                else
                {
                    cell.Append( character );
                    if ( character == '\n' || ( character == '\r'
                        && ( i + 1 == text.Length || text [ i + 1 ] != '\n' ) ) )
                        line++;
                }
                continue;
            }
            if ( character == ',' || character == '\r' || character == '\n' )
            {
                cells.Add( cell.ToString( ).Trim( ) );
                cell.Clear( );
                closed = false;
                if ( character == ',' )
                    continue;
                if ( cells.Count != 1 || cells [ 0 ].Length != 0 )
                    rows.Add( new CsvRow { Line = startLine, Cells = cells.ToArray( ) } );
                cells.Clear( );
                if ( character == '\r' && i + 1 < text.Length && text [ i + 1 ] == '\n' )
                    i++;
                line++;
                startLine = line;
            }
            else if ( closed == true && char.IsWhiteSpace( character ) == true )
                continue;
            else if ( character == '"' && cell.Length == 0 && closed == false )
                quoted = true;
            else
            {
                if ( closed == true || character == '"' )
                {
                    error = $"CSV {line}행: 따옴표 위치가 잘못됐습니다.";
                    return false;
                }
                cell.Append( character );
            }
        }
        return true;
    }

    #endregion
}
#endif
