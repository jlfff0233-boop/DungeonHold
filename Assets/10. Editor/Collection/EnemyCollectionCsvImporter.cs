using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 도감 CSV 임포터 - 원본 적을 참조하여 도감 SO와 맵만 생성/갱신
/// </summary>
internal static class EnemyCollectionCsvImporter
{
    static readonly string[] _headers =
    {
        "EnemyId", "EnemyAssetPath", "Kind", "DisplayName", "Feature", "AttackDescription", "RewardAmount"
    };    //CSV 필수 컬럼 순서

    /// <summary>
    /// CSV 행 - 오류 위치와 셀 보관
    /// </summary>
    sealed class CsvRow
    {
        public int Line;    //레코드 시작 줄
        public string[] Cells;    //원본 셀
    }

    /// <summary>
    /// 도감 입력 - 검사 후보와 기존 에셋의 변경 내용 보관
    /// </summary>
    internal sealed class Entry
    {
        public int Line;    //CSV 시작 줄
        public string Id;    //고정 도감 ID와 신규 파일명
        public EnemyData Enemy;    //읽기만 하는 KMB 원본 적
        public EnemyCollectionKind Kind;    //일반 적/보스
        public string DisplayName;    //도감 표시 이름
        public string Feature;    //특징 설명
        public string AttackDescription;    //공격 설명
        public bool HasBackgroundStory;    //CSV의 스토리 컬럼 포함 여부
        public string BackgroundStory;    //컬럼이 있을 때 적용할 스토리
        public int RewardAmount;    //최초 해금 보상
        public EnemyCollectionData Existing;    //제자리 갱신할 기존 SO
        public EnemyCollectionData Candidate;    //파일을 바꾸지 않는 검사 후보
        public string AssetPath;    //실제 생성/갱신 경로
        public string Before;    //변경 전 표시
        public string After;    //변경 후 표시
        public bool Changed;    //CSV 관리 값의 변경 여부
        public bool AddToMap;    //선택한 맵에 추가할지 여부
        public bool SavePending;    //기존 SO의 미저장 변경 여부
    }

    /// <summary>
    /// 임포트 계획 - 미리보기와 미저장 임시 객체의 수명 관리
    /// </summary>
    internal sealed class Plan : IDisposable
    {
        public EnemyCollectionDataMap Map;    //사용자가 선택한 영속 도감 맵
        public List<Entry> Entries = new List<Entry>( );    //CSV 순서의 입력
        public List<string> Errors = new List<string>( );    //적용을 막는 오류
        public List<string> Notices = new List<string>( );    //미저장 변경/씬 연결 안내
        public List<EnemyCollectionData> Original = new List<EnemyCollectionData>( );    //기존 맵의 원래 순서
        public Dictionary<string, EnemyCollectionData> Existing = new( StringComparer.Ordinal );    //맵/폴더에서 찾은 ID
        public List<ScriptableObject> Temporary = new List<ScriptableObject>( );    //검사 후보와 생성 실패 시 해제할 객체
        public string Fingerprint;    //미리보기 이후 입력/에셋 변경 비교값
        public bool MapSavePending;    //맵의 미저장 변경 여부

        /// <summary>
        /// 실제 에셋을 보존하고 임시 객체만 해제
        /// </summary>
        public void Dispose ()
        {
            foreach ( ScriptableObject temporary in Temporary )
            {
                //CreateAsset 중 예외가 발생했더라도 이미 영속화된 객체는 제거하지 않음
                if ( temporary != null && EditorUtility.IsPersistent( temporary ) == false )
                    Object.DestroyImmediate( temporary );
            }
            Temporary.Clear( );
        }
    }

    /// <summary>
    /// CSV와 기존 에셋을 읽어 파일 변경 없이 최종 구성 검사
    /// </summary>
    /// <param name="csvPath">UTF-8 CSV 경로</param>
    /// <param name="folder">신규 도감 SO를 둘 기존 Assets 폴더</param>
    /// <param name="map">선택한 영속 도감 맵</param>
    /// <returns>미리보기와 오류를 보관한 계획</returns>
    internal static Plan Prepare ( string csvPath, string folder, EnemyCollectionDataMap map )
    {
        Plan plan = new Plan { Map = map };
        if ( EditorApplication.isPlayingOrWillChangePlaymode == true )
        {
            plan.Errors.Add( "편집 모드에서 검사/적용하세요." );
            return plan;
        }
        if ( map == null || AssetDatabase.IsMainAsset( map ) == false
            || AssetDatabase.GetAssetPath( map ).StartsWith( "Assets/", StringComparison.Ordinal ) == false )
        {
            plan.Errors.Add( "Assets 아래에 저장된 독립 EnemyCollectionDataMap을 연결하세요." );
            return plan;
        }
        if ( string.IsNullOrEmpty( folder ) == true || folder.StartsWith( "Assets/", StringComparison.Ordinal ) == false
            || AssetDatabase.IsValidFolder( folder ) == false )
        {
            plan.Errors.Add( "Assets 아래의 기존 출력 폴더를 선택하세요." );
            return plan;
        }
        if ( string.IsNullOrWhiteSpace( csvPath ) == true
            || string.Equals( Path.GetExtension( csvPath ), ".csv", StringComparison.OrdinalIgnoreCase ) == false )
        {
            plan.Errors.Add( "CSV 파일 경로를 지정하세요." );
            return plan;
        }

        string csv;
        //파일 읽기 실패만 처리하며 CSV 내용 오류는 아래 검사 결과로 반환
        try
        {
            csv = File.ReadAllText( csvPath, new UTF8Encoding( false, true ) );
        }
        catch ( Exception exception ) when ( exception is IOException || exception is UnauthorizedAccessException
            || exception is DecoderFallbackException )
        {
            plan.Errors.Add( $"CSV 읽기 실패: {csvPath}\n{exception.Message}" );
            return plan;
        }

        ReadEntries( csv, plan );
        if ( plan.Errors.Count != 0 )
            return plan;
        ReadExisting( folder, plan );
        if ( plan.Errors.Count != 0 )
            return plan;
        PrepareCandidates( folder, plan );
        if ( plan.Errors.Count != 0 )
            return plan;

        //기존 SO를 먼저 검사하면 CSV로 수정할 값까지 막히므로 합친 최종 구성만 검사
        List<EnemyCollectionData> merged = new List<EnemyCollectionData>( plan.Original );
        foreach ( Entry entry in plan.Entries )
        {
            if ( entry.AddToMap == true )
                merged.Add( entry.Candidate );
            else
                merged[merged.IndexOf( entry.Existing )] = entry.Candidate;
        }
        EnemyCollectionDataMap candidateMap = ScriptableObject.CreateInstance<EnemyCollectionDataMap>( );
        plan.Temporary.Add( candidateMap );
        WriteMap( candidateMap, merged );
        if ( candidateMap.Validate( ) == false )
        {
            plan.Errors.Add( "최종 도감 맵 검사에 실패했습니다. Console의 도감 ID/원본 참조를 확인하세요." );
            return plan;
        }

        plan.MapSavePending = EditorUtility.IsDirty( map );
        if ( plan.MapSavePending == true )
            plan.Notices.Add( "선택한 맵의 미저장 변경도 적용 시 함께 저장합니다." );
        DescribeSceneConnections( plan );
        plan.Fingerprint = CreateFingerprint( csv, folder, plan );
        return plan;
    }

    /// <summary>
    /// BOM/따옴표/쉼표/줄바꿈을 처리하여 CSV 레코드 해석
    /// </summary>
    /// <param name="text">CSV 원문</param>
    /// <param name="rows">빈 줄을 제외한 레코드</param>
    /// <param name="error">구문 오류와 위치</param>
    /// <returns>해석 성공 여부</returns>
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
            char character = end == true ? '\n' : text[i];
            if ( quoted == true )
            {
                if ( end == true )
                {
                    error = $"CSV {startLine}행: 닫히지 않은 따옴표입니다.";
                    return false;
                }
                if ( character == '"' )
                {
                    if ( i + 1 < text.Length && text[i + 1] == '"' )
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
                    if ( character == '\n' || ( character == '\r' && ( i + 1 == text.Length || text[i + 1] != '\n' ) ) )
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
                if ( cells.Count != 1 || cells[0].Length != 0 )
                    rows.Add( new CsvRow { Line = startLine, Cells = cells.ToArray( ) } );
                cells.Clear( );
                if ( character == '\r' && i + 1 < text.Length && text[i + 1] == '\n' )
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

    /// <summary>
    /// 컬럼을 정렬하고 행별 입력값과 원본 적 참조 해석
    /// </summary>
    /// <param name="csv">CSV 원문</param>
    /// <param name="plan">입력과 오류를 보관할 계획</param>
    static void ReadEntries ( string csv, Plan plan )
    {
        if ( TryParseCsv( csv, out List<CsvRow> rows, out string error ) == false )
        {
            plan.Errors.Add( error );
            return;
        }
        if ( rows.Count == 0 )
        {
            plan.Errors.Add( "CSV 헤더가 없습니다." );
            return;
        }
        Dictionary<string, int> columns = new( StringComparer.Ordinal );
        foreach ( string header in rows[0].Cells )
        {
            if ( ( Array.IndexOf( _headers, header ) < 0 && header != "BackgroundStory" )
                || columns.ContainsKey( header ) == true )
            {
                plan.Errors.Add( "알 수 없거나 중복된 CSV 헤더입니다: " + header );
                return;
            }
            columns.Add( header, columns.Count );
        }
        foreach ( string header in _headers )
        {
            if ( columns.ContainsKey( header ) == false )
                plan.Errors.Add( "필수 CSV 헤더가 없습니다: " + header );
        }
        if ( plan.Errors.Count != 0 )
            return;

        //컬럼 누락과 빈 셀을 구분하여 기존 스토리의 보존/삭제 결정
        bool hasBackgroundStory = columns.TryGetValue(
            "BackgroundStory", out int backgroundStoryColumn );

        HashSet<string> ids = new HashSet<string>( StringComparer.Ordinal );
        for ( int i = 1; i < rows.Count; i++ )
        {
            CsvRow row = rows[i];
            if ( row.Cells.Length != columns.Count )
            {
                plan.Errors.Add( $"CSV {row.Line}행: 컬럼 수는 {columns.Count}개여야 합니다." );
                continue;
            }
            string[] values = new string[_headers.Length];
            for ( int j = 0; j < values.Length; j++ )
                values[j] = row.Cells[columns[_headers[j]]];
            string location = $"CSV {row.Line}행 / {values[0]}";
            if ( TryValidateId( values[0], out error ) == false )
            {
                plan.Errors.Add( location + " / EnemyId: " + error );
                continue;
            }
            if ( ids.Add( values[0] ) == false )
            {
                plan.Errors.Add( location + " / EnemyId 중복" );
                continue;
            }
            if ( values[2] != "Normal" && values[2] != "Boss" )
            {
                plan.Errors.Add( location + " / Kind는 Normal 또는 Boss여야 합니다: " + values[2] );
                continue;
            }
            if ( values[3].Length == 0 )
            {
                plan.Errors.Add( location + " / DisplayName이 비어 있습니다." );
                continue;
            }
            if ( int.TryParse( values[6], NumberStyles.None, CultureInfo.InvariantCulture, out int reward ) == false )
            {
                plan.Errors.Add( location + " / RewardAmount는 0 이상의 int 정수여야 합니다: " + values[6] );
                continue;
            }
            EnemyData enemy = AssetDatabase.LoadAssetAtPath<EnemyData>( values[1] );
            if ( values[1].StartsWith( "Assets/", StringComparison.Ordinal ) == false
                || enemy == null || AssetDatabase.IsMainAsset( enemy ) == false )
            {
                plan.Errors.Add( location + " / EnemyAssetPath에 독립 EnemyData 에셋이 없습니다: " + values[1] );
                continue;
            }
            plan.Entries.Add( new Entry
            {
                Line = row.Line, Id = values[0], Enemy = enemy,
                Kind = values[2] == "Normal" ? EnemyCollectionKind.Normal : EnemyCollectionKind.Boss,
                DisplayName = values[3], Feature = values[4], AttackDescription = values[5],
                HasBackgroundStory = hasBackgroundStory,
                BackgroundStory = hasBackgroundStory
                    ? row.Cells[backgroundStoryColumn]
                    : string.Empty,
                RewardAmount = reward
            } );
        }
    }

    /// <summary>
    /// 신규 파일명으로 사용할 ID 검사
    /// </summary>
    /// <param name="id">고정 ID</param>
    /// <param name="error">사용할 수 없는 이유</param>
    /// <returns>파일명 사용 가능 여부</returns>
    static bool TryValidateId ( string id, out string error )
    {
        error = null;
        if ( string.IsNullOrWhiteSpace( id ) == true || id.IndexOfAny( Path.GetInvalidFileNameChars( ) ) >= 0
            || id.EndsWith( ".", StringComparison.Ordinal ) == true )
        {
            error = "파일명으로 사용할 수 없는 ID입니다.";
            return false;
        }
        string stem = id.Split( '.' )[0].ToUpperInvariant( );
        if ( stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL"
            || ( stem.Length == 4 && ( stem.StartsWith( "COM" ) || stem.StartsWith( "LPT" ) ) && stem[3] >= '1' && stem[3] <= '9' ) )
        {
            error = "Windows 예약 파일명입니다.";
            return false;
        }
        return true;
    }

    /// <summary>
    /// 기존 맵 순서와 출력 폴더의 도감 SO 수집
    /// </summary>
    /// <param name="folder">출력 폴더</param>
    /// <param name="plan">기존 에셋을 보관할 계획</param>
    static void ReadExisting ( string folder, Plan plan )
    {
        foreach ( EnemyCollectionData data in plan.Map.Entries )
        {
            plan.Original.Add( data );
            RegisterExisting( data, plan );
        }
        //이전 적용이 맵 등록 전에 실패했어도 생성된 SO를 ID로 찾아 재사용
        string[] guids = AssetDatabase.FindAssets( "t:EnemyCollectionData", new[] { folder } );
        Array.Sort( guids, StringComparer.Ordinal );
        foreach ( string guid in guids )
            RegisterExisting( AssetDatabase.LoadAssetAtPath<EnemyCollectionData>( AssetDatabase.GUIDToAssetPath( guid ) ), plan );
    }

    /// <summary>
    /// 영속 도감 에셋을 ID 중복 없이 수집
    /// </summary>
    /// <param name="data">기존 도감 SO</param>
    /// <param name="plan">수집 상태와 오류</param>
    static void RegisterExisting ( EnemyCollectionData data, Plan plan )
    {
        if ( data == null || AssetDatabase.IsMainAsset( data ) == false
            || AssetDatabase.GetAssetPath( data ).StartsWith( "Assets/", StringComparison.Ordinal ) == false )
        {
            plan.Errors.Add( "기존 맵/폴더의 도감 참조를 확인하세요. Assets 내 독립 EnemyCollectionData가 필요합니다." );
            return;
        }
        if ( string.IsNullOrWhiteSpace( data.EnemyId ) == true )
        {
            plan.Errors.Add( "기존 도감 ID가 없습니다: " + AssetDatabase.GetAssetPath( data ) );
            return;
        }
        if ( plan.Existing.TryGetValue( data.EnemyId, out EnemyCollectionData other ) == true && other != data )
        {
            plan.Errors.Add( "다른 도감 SO의 ID가 충돌합니다: " + data.EnemyId );
            return;
        }
        plan.Existing[data.EnemyId] = data;
    }

    /// <summary>
    /// 고정 ID의 원본 적 연결과 출력 경로를 확인한 뒤 후보 작성
    /// </summary>
    /// <param name="folder">신규 출력 폴더</param>
    /// <param name="plan">입력과 기존 에셋</param>
    static void PrepareCandidates ( string folder, Plan plan )
    {
        HashSet<string> paths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
        paths.Add( AssetDatabase.GetAssetPath( plan.Map ) );
        foreach ( Entry entry in plan.Entries )
        {
            plan.Existing.TryGetValue( entry.Id, out entry.Existing );
            entry.AddToMap = entry.Existing == null || plan.Original.Contains( entry.Existing ) == false;
            entry.AssetPath = entry.Existing == null ? folder + "/" + entry.Id + ".asset" : AssetDatabase.GetAssetPath( entry.Existing );
            string location = $"CSV {entry.Line}행 / {entry.Id}";
            if ( paths.Add( entry.AssetPath ) == false )
            {
                plan.Errors.Add( location + " / 출력 경로가 맵 또는 다른 항목과 충돌합니다: " + entry.AssetPath );
                continue;
            }
            if ( entry.Existing == null && ( AssetDatabase.LoadMainAssetAtPath( entry.AssetPath ) != null
                || File.Exists( entry.AssetPath ) == true || Directory.Exists( entry.AssetPath ) == true
                || File.Exists( entry.AssetPath + ".meta" ) == true ) )
            {
                plan.Errors.Add( location + " / 출력 경로에 다른 파일/메타가 있습니다: " + entry.AssetPath );
                continue;
            }
            //에셋 이동으로 CSV 경로만 바뀐 경우에는 같은 원본 참조이므로 허용
            if ( entry.Existing != null && entry.Existing.Enemy != entry.Enemy )
            {
                plan.Errors.Add( location + " / 같은 ID의 원본 적은 변경할 수 없습니다. 새 적에는 새 도감 ID를 사용하세요." );
                continue;
            }
            entry.Candidate = entry.Existing == null ? ScriptableObject.CreateInstance<EnemyCollectionData>( )
                : Object.Instantiate( entry.Existing );
            plan.Temporary.Add( entry.Candidate );
            entry.Candidate.name = entry.Existing == null ? entry.Id : entry.Existing.name;
            WriteData( entry.Candidate, entry );
            entry.Changed = entry.Existing == null || EditorJsonUtility.ToJson( entry.Existing ) != EditorJsonUtility.ToJson( entry.Candidate );
            entry.SavePending = entry.Existing != null && EditorUtility.IsDirty( entry.Existing );
            entry.Before = entry.Existing == null ? "신규" : Describe( entry.Existing );
            entry.After = Describe( entry.Candidate );
            if ( entry.SavePending == true )
                plan.Notices.Add( entry.Id + ": 기존 SO의 미저장 변경도 함께 저장합니다." );
            if ( entry.Existing != null && entry.Existing.RewardAmount != entry.RewardAmount )
                plan.Notices.Add( entry.Id + ": 보상 변경은 앞으로의 최초 해금에 적용됩니다. 이미 지급한 보상은 유지합니다." );
        }
    }

    /// <summary>
    /// CSV 관리 필드 적용. 스토리 컬럼이 없으면 기존 값 보존
    /// </summary>
    /// <param name="data">후보 또는 영속 도감 SO</param>
    /// <param name="entry">검사한 입력</param>
    static void WriteData ( EnemyCollectionData data, Entry entry )
    {
        using ( SerializedObject serialized = new SerializedObject( data ) )
        {
            serialized.FindProperty( "_enemyId" ).stringValue = entry.Id;
            serialized.FindProperty( "_enemy" ).objectReferenceValue = entry.Enemy;
            serialized.FindProperty( "_kind" ).intValue = ( int ) entry.Kind;
            serialized.FindProperty( "_displayName" ).stringValue = entry.DisplayName;
            serialized.FindProperty( "_feature" ).stringValue = entry.Feature;
            serialized.FindProperty( "_attackDescription" ).stringValue = entry.AttackDescription;

            //컬럼이 있을 때만 적용하며 빈 셀은 명시적인 삭제로 처리
            if ( entry.HasBackgroundStory == true )
                serialized.FindProperty( "_backgroundStory" ).stringValue = entry.BackgroundStory;

            serialized.FindProperty( "_rewardAmount" ).intValue = entry.RewardAmount;
            serialized.ApplyModifiedPropertiesWithoutUndo( );
        }
    }

    /// <summary>
    /// 도감 맵의 원본 배열만 갱신
    /// </summary>
    /// <param name="map">후보 또는 영속 맵</param>
    /// <param name="entries">표시 순서의 도감 항목</param>
    static void WriteMap ( EnemyCollectionDataMap map, List<EnemyCollectionData> entries )
    {
        using ( SerializedObject serialized = new SerializedObject( map ) )
        {
            SerializedProperty list = serialized.FindProperty( "_entries" );
            list.arraySize = entries.Count;
            for ( int i = 0; i < entries.Count; i++ )
                list.GetArrayElementAtIndex( i ).objectReferenceValue = entries[i];
            serialized.ApplyModifiedPropertiesWithoutUndo( );
        }
    }

    /// <summary>
    /// 행별 도감 설정 미리보기 생성
    /// </summary>
    /// <param name="data">표시할 설정</param>
    /// <returns>원본 참조와 도감 표시/보상</returns>
    static string Describe ( EnemyCollectionData data )
    {
        return $"분류={data.Kind} / 이름={data.DisplayName} / 최초 보상={data.RewardAmount}\n"
            + $"원본 적={AssetDatabase.GetAssetPath( data.Enemy )}\n특징={data.Feature}\n공격={data.AttackDescription}\n"
            + $"배경 스토리={data.BackgroundStory}\n"
            + $"대표 이미지={( data.RepresentativeImage == null ? "미연결" : data.RepresentativeImage.name )} (기존 연결 유지)";
    }

    /// <summary>
    /// 열린 씬의 도감 맵 연결 상태를 읽기만 하여 안내
    /// </summary>
    /// <param name="plan">선택한 맵과 안내 목록</param>
    static void DescribeSceneConnections ( Plan plan )
    {
        bool found = false;
        foreach ( LJPlay.PlayScene scene in Resources.FindObjectsOfTypeAll<LJPlay.PlayScene>( ) )
        {
            if ( EditorUtility.IsPersistent( scene ) == true || scene.gameObject.scene.IsValid( ) == false )
                continue;
            found = true;
            using ( SerializedObject serialized = new SerializedObject( scene ) )
            {
                Object connected = serialized.FindProperty( "_collectionDataMap" ).objectReferenceValue;
                string state = connected == plan.Map ? "선택한 맵 연결됨" : connected == null ? "도감 맵 미연결" : "다른 도감 맵 연결됨";
                plan.Notices.Add( $"{scene.gameObject.scene.name} / {scene.name}: {state}. 씬 연결은 Inspector에서 관리합니다." );
            }
        }
        if ( found == false )
            plan.Notices.Add( "열린 씬에 PlayScene이 없습니다. 사용할 씬의 도감 맵은 Inspector에서 연결하세요." );
    }

    /// <summary>
    /// 임시 객체 식별값을 제외하고 입력과 실제 수정 대상의 변경 비교값 생성
    /// </summary>
    /// <param name="csv">CSV 원문</param>
    /// <param name="folder">신규 출력 폴더</param>
    /// <param name="plan">기존 맵과 입력 후보</param>
    /// <returns>길이로 구분한 비교 문자열</returns>
    static string CreateFingerprint ( string csv, string folder, Plan plan )
    {
        StringBuilder result = new StringBuilder( );
        AppendToken( result, csv );
        AppendToken( result, folder );
        AppendToken( result, AssetDatabase.GetAssetPath( plan.Map ) );
        AppendToken( result, EditorJsonUtility.ToJson( plan.Map ) );
        AppendToken( result, plan.MapSavePending.ToString( ) );
        foreach ( EnemyCollectionData data in plan.Original )
            AppendToken( result, EditorJsonUtility.ToJson( data ) );
        foreach ( Entry entry in plan.Entries )
        {
            AppendToken( result, entry.AssetPath );
            AppendToken( result, entry.SavePending.ToString( ) );
            AppendToken( result, entry.Existing == null ? "" : EditorJsonUtility.ToJson( entry.Existing ) );
            //후보 자체의 JSON 대신 값으로 비교하여 재검사마다 다른 임시 객체 번호를 제외
            AppendToken( result, entry.After );
            AppendToken( result, AssetDatabase.AssetPathToGUID( AssetDatabase.GetAssetPath( entry.Enemy ) ) );
        }
        return result.ToString( );
    }

    /// <summary>
    /// 구분 문자가 포함된 입력도 충돌하지 않도록 길이와 함께 추가
    /// </summary>
    /// <param name="builder">비교값을 쓸 대상</param>
    /// <param name="value">추가할 문자열</param>
    static void AppendToken ( StringBuilder builder, string value )
    {
        builder.Append( value.Length ).Append( ':' ).Append( value );
    }

    /// <summary>
    /// 검사한 도감 SO를 먼저 저장하고 성공 후 선택한 맵에 등록
    /// </summary>
    /// <param name="plan">적용 직전에 재검사한 계획</param>
    /// <param name="touched">일부 반영됐을 수 있는 경로</param>
    /// <param name="error">저장 미완료 내용</param>
    /// <returns>대상 저장 완료 여부</returns>
    internal static bool Apply ( Plan plan, List<string> touched, out string error )
    {
        error = null;
        List<EnemyCollectionData> merged = new List<EnemyCollectionData>( plan.Original );
        bool mapChanged = false;
        foreach ( Entry entry in plan.Entries )
        {
            EnemyCollectionData data = entry.Existing;
            if ( data == null )
            {
                data = ScriptableObject.CreateInstance<EnemyCollectionData>( );
                plan.Temporary.Add( data );
                data.name = entry.Id;
                WriteData( data, entry );
                touched.Add( entry.AssetPath );
                AssetDatabase.CreateAsset( data, entry.AssetPath );
                plan.Temporary.Remove( data );
            }
            else if ( entry.Changed == true )
            {
                touched.Add( entry.AssetPath );
                WriteData( data, entry );
            }
            else if ( entry.SavePending == true )
                touched.Add( entry.AssetPath );

            if ( entry.Existing == null || entry.Changed == true || entry.SavePending == true )
            {
                if ( TrySaveAsset( data, out error ) == false )
                    return false;
            }
            if ( entry.AddToMap == true )
            {
                merged.Add( data );
                mapChanged = true;
            }
        }
        //기존 SO는 같은 참조에서 갱신했으므로 신규 등록이 있을 때만 배열 변경
        if ( mapChanged == true )
        {
            touched.Add( AssetDatabase.GetAssetPath( plan.Map ) );
            WriteMap( plan.Map, merged );
        }
        else if ( plan.MapSavePending == true )
            touched.Add( AssetDatabase.GetAssetPath( plan.Map ) );
        if ( mapChanged == true || plan.MapSavePending == true )
            return TrySaveAsset( plan.Map, out error );
        return true;
    }

    /// <summary>
    /// 변경 대상 에셋만 저장하고 미완료 경로 반환
    /// </summary>
    /// <param name="asset">변경한 도감 SO 또는 선택한 맵</param>
    /// <param name="error">저장 미완료 경로</param>
    /// <returns>저장 완료 여부</returns>
    static bool TrySaveAsset ( Object asset, out string error )
    {
        EditorUtility.SetDirty( asset );
        AssetDatabase.SaveAssetIfDirty( asset );
        error = EditorUtility.IsDirty( asset ) == true ? "에셋 저장을 완료하지 못했습니다: " + AssetDatabase.GetAssetPath( asset ) : null;
        return error == null;
    }
}
