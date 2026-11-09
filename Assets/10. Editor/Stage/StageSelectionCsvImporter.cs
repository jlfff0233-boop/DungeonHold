#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 스테이지 CSV 임포터 - 목록 검사와 설정 반영
/// </summary>
public static class StageSelectionCsvImporter
{
    static string [ ] _headers = { "MapId", "DisplayName", "Description", "Difficulties" }; //필수 컬럼

    /// <summary>
    /// 임포트 계획 - 입력 스냅샷과 반영 목록
    /// </summary>
    public class Plan
    {
        public StageSelectionData Target; //선택한 설정 에셋
        public string CsvPath; //CSV 경로
        public string Source; //검사한 원문
        public string Snapshot; //검사한 설정 상태
        public bool SavePending; //설정의 미저장 변경 여부
        public List<LobbyStageInfo> Stages = new List<LobbyStageInfo>( ); //최종 목록
        public List<string> Changes = new List<string>( ); //변경 미리보기
        public string Error; //적용을 막는 오류
    }

    /// <summary>
    /// CSV 행 - 위치와 셀 보관
    /// </summary>
    class CsvRow
    {
        public int Line; //시작 줄
        public string [ ] Cells; //셀 목록
    }

    #region ----- 검사/적용 -----

    /// <summary>
    /// 파일 변경 없이 최종 목록 준비
    /// </summary>
    /// <param name="csvPath">UTF-8 CSV 경로</param>
    /// <param name="target">반영할 설정</param>
    /// <returns>미리보기와 검사 결과</returns>
    public static Plan Prepare ( string csvPath, StageSelectionData target )
    {
        Plan plan = new Plan { CsvPath = csvPath, Target = target };
        if ( EditorApplication.isPlayingOrWillChangePlaymode == true )
        {
            plan.Error = "편집 모드에서 검사/적용하세요.";
            return plan;
        }
        if ( target == null || AssetDatabase.IsMainAsset( target ) == false
            || AssetDatabase.GetAssetPath( target ).StartsWith( "Assets/", StringComparison.Ordinal ) == false )
        {
            plan.Error = "Assets 아래에 저장한 StageSelectionData를 연결하세요.";
            return plan;
        }
        if ( string.IsNullOrWhiteSpace( csvPath ) == true
            || string.Equals( Path.GetExtension( csvPath ), ".csv", StringComparison.OrdinalIgnoreCase ) == false )
        {
            plan.Error = "CSV 파일 경로를 지정하세요.";
            return plan;
        }

        //외부 파일 읽기에서 발생하는 실패만 결과로 전달
        try
        {
            plan.Source = File.ReadAllText( csvPath, new UTF8Encoding( false, true ) );
        }
        catch ( Exception exception ) when ( exception is IOException
            || exception is UnauthorizedAccessException || exception is DecoderFallbackException )
        {
            plan.Error = "CSV 읽기 실패: " + exception.Message;
            return plan;
        }

        plan.Snapshot = EditorJsonUtility.ToJson( target );
        plan.SavePending = EditorUtility.IsDirty( target );
        ReadStages( plan );
        if ( plan.Error == null
            && StageSelectionData.ValidateStages( plan.Stages, out string reason ) == false )
            plan.Error = reason;
        return plan;
    }

    /// <summary>
    /// 최신 입력이 미리보기와 같을 때 설정 저장
    /// </summary>
    /// <param name="plan">확인한 미리보기</param>
    /// <param name="message">처리 결과</param>
    /// <returns>저장 완료 여부</returns>
    public static bool TryApply ( Plan plan, out string message )
    {
        //CSV/Inspector 변경과 플레이 전환은 적용 진입점에서 재확인
        Plan latest = Prepare( plan.CsvPath, plan.Target );
        if ( latest.Error != null )
        {
            message = latest.Error;
            return false;
        }
        if ( latest.Source != plan.Source || latest.Snapshot != plan.Snapshot
            || latest.SavePending != plan.SavePending )
        {
            message = "입력 또는 설정이 바뀌었습니다. 다시 검사하고 미리보기를 확인하세요.";
            return false;
        }

        //검사가 끝난 목록만 기록하고 선택한 에셋만 저장
        Undo.RegisterCompleteObjectUndo( latest.Target, "Import Stage Selection CSV" );
        using ( SerializedObject serialized = new SerializedObject( latest.Target ) )
        {
            SerializedProperty stages = serialized.FindProperty( "_stages" );
            stages.arraySize = latest.Stages.Count;
            for ( int i = 0; i < latest.Stages.Count; i++ )
            {
                LobbyStageInfo source = latest.Stages [ i ];
                SerializedProperty stage = stages.GetArrayElementAtIndex( i );
                stage.FindPropertyRelative( "_mapId" ).stringValue = source.MapId;
                stage.FindPropertyRelative( "_displayName" ).stringValue = source.DisplayName;
                stage.FindPropertyRelative( "_description" ).stringValue = source.Description;
                stage.FindPropertyRelative( "_preview" ).objectReferenceValue = source.Preview;
                SerializedProperty difficulties = stage.FindPropertyRelative( "_difficulties" );
                difficulties.arraySize = source.Difficulties.Count;
                for ( int j = 0; j < source.Difficulties.Count; j++ )
                    difficulties.GetArrayElementAtIndex( j ).intValue = ( int ) source.Difficulties [ j ];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo( );
        }
        EditorUtility.SetDirty( latest.Target );
        AssetDatabase.SaveAssetIfDirty( latest.Target );
        if ( EditorUtility.IsDirty( latest.Target ) == true )
        {
            message = "목록은 메모리에 반영됐으나 저장되지 않았습니다. Console과 설정 에셋을 확인하세요.";
            return false;
        }
        message = $"스테이지 {latest.Stages.Count}개 저장 완료. 이미지와 로비 연결을 확인하세요.";
        return true;
    }

    /// <summary>
    /// CSV 순서로 병합하고 이미지와 누락 항목 보존
    /// </summary>
    /// <param name="plan">검사할 입력</param>
    static void ReadStages ( Plan plan )
    {
        if ( TryParseCsv( plan.Source, out List<CsvRow> rows, out string error ) == false )
        {
            plan.Error = error;
            return;
        }
        if ( rows.Count < 2 || rows [ 0 ].Cells.Length != _headers.Length )
        {
            plan.Error = "필수 헤더 4개와 스테이지 행이 필요합니다.";
            return;
        }
        Dictionary<string, int> columns = new Dictionary<string, int>( StringComparer.Ordinal );
        for ( int i = 0; i < rows [ 0 ].Cells.Length; i++ )
        {
            string header = rows [ 0 ].Cells [ i ];
            if ( Array.IndexOf( _headers, header ) < 0 || columns.TryAdd( header, i ) == false )
            {
                plan.Error = "알 수 없거나 중복된 헤더: " + header;
                return;
            }
        }

        Dictionary<string, LobbyStageInfo> existing = new Dictionary<string, LobbyStageInfo>( StringComparer.Ordinal );
        foreach ( LobbyStageInfo stage in plan.Target.Stages )
        {
            //기존 ID가 모호하면 이미지 보존 대상을 결정할 수 없음
            if ( stage == null || string.IsNullOrWhiteSpace( stage.MapId ) == true
                || existing.TryAdd( stage.MapId, stage ) == false )
            {
                plan.Error = "기존 목록의 빈 항목/ID 또는 중복 ID를 먼저 수정하세요.";
                return;
            }
        }
        HashSet<string> imported = new HashSet<string>( StringComparer.Ordinal );
        for ( int i = 1; i < rows.Count; i++ )
        {
            CsvRow row = rows [ i ];
            if ( row.Cells.Length != _headers.Length )
            {
                plan.Error = $"CSV {row.Line}행: 컬럼이 4개여야 합니다.";
                return;
            }
            string id = row.Cells [ columns [ "MapId" ] ];
            string [ ] tokens = row.Cells [ columns [ "Difficulties" ] ].Split( '|' );
            MissionDifficulty [ ] difficulties = new MissionDifficulty [ tokens.Length ];
            for ( int j = 0; j < tokens.Length; j++ )
            {
                string token = tokens [ j ].Trim( );
                if ( Enum.TryParse( token, out difficulties [ j ] ) == false
                    || difficulties [ j ].ToString( ) != token )
                {
                    plan.Error = $"CSV {row.Line}행: 난이도는 Easy/Normal/Hard 이름으로 입력하세요: {token}";
                    return;
                }
            }
            existing.TryGetValue( id, out LobbyStageInfo before );
            LobbyStageInfo after = new LobbyStageInfo( id,
                row.Cells [ columns [ "DisplayName" ] ], row.Cells [ columns [ "Description" ] ],
                before == null ? null : before.Preview, difficulties );
            plan.Stages.Add( after );
            imported.Add( id );
            plan.Changes.Add( before == null
                ? $"[추가] {Describe( after )}"
                : $"[CSV 반영] {id}\n변경 전: {Describe( before )}\n변경 후: {Describe( after )}" );
        }
        foreach ( LobbyStageInfo stage in plan.Target.Stages )
        {
            if ( imported.Contains( stage.MapId ) == true )
                continue;
            plan.Stages.Add( stage );
            plan.Changes.Add( "[CSV 누락/뒤에 유지] " + Describe( stage ) );
        }
    }

    /// <summary>
    /// 행 미리보기 구성
    /// </summary>
    /// <param name="stage">표시할 정보</param>
    /// <returns>설정 내용</returns>
    static string Describe ( LobbyStageInfo stage )
    {
        string difficulties = stage.Difficulties == null ? "없음" : string.Join( "|", stage.Difficulties );
        return $"{stage.MapId} / {stage.DisplayName}\n{stage.Description}\n난이도: {difficulties} / 이미지: "
            + ( stage.Preview == null ? "미연결" : stage.Preview.name );
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
