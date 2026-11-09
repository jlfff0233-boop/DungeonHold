using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 미션 CSV 임포터 - 최종 참조 검사와 미션/처치 집계 에셋 반영
/// </summary>
internal static class MissionCsvImporter
{
    static readonly string[] _missionHeaders =
    {
        "MissionId", "Type", "DisplayName", "Difficulties", "PrerequisiteId", "RewardAmount",
        "MapId", "Comparison", "Count", "KillCountId", "TargetCount"
    };    //미션 CSV 컬럼
    static readonly string[] _killCountHeaders = { "KillCountId", "EnemyAssetPath", "MapId" };    //집계 CSV 컬럼

    /// <summary>
    /// CSV 행 - 원본 위치와 표준 컬럼 순서의 입력 보관
    /// </summary>
    sealed class CsvRow
    {
        public int Line;    //레코드 시작 줄
        public string[] Cells;    //행의 셀
    }

    /// <summary>
    /// 임포트 항목 - 입력과 에셋 후보 및 변경 미리보기 보관
    /// </summary>
    internal sealed class Entry
    {
        public string Id;    //고정 미션 또는 집계 ID
        public string Kind;    //구체 데이터 종류
        public string[] Values;    //표준 컬럼 순서의 입력
        public int Line;    //CSV 시작 줄
        public EnemyData Enemy;    //집계 대상 원본 적
        public ScriptableObject Existing;    //제자리 갱신할 기존 에셋
        public ScriptableObject Candidate;    //파일 변경 없는 검사 후보
        public ScriptableObject Applied;    //적용 단계의 영속 에셋
        public string AssetPath;    //실제 반영 경로
        public string Before;    //기존 값 표시
        public string After;    //반영 값 표시
        public bool Changed;    //설정 변경 여부
        public bool AddToMap;    //미션 맵 추가 여부
        public bool SavePending;    //기존 에셋의 미저장 내용 존재 여부

        /// <summary>
        /// 처치 집계 정의 여부
        /// </summary>
        public bool IsKillCount => Kind == "KillCount";
    }

    /// <summary>
    /// 임포트 계획 - 기존 구성과 검사 후보의 수명 관리
    /// </summary>
    internal sealed class Plan : IDisposable
    {
        public MissionDataMap Map;    //등록할 영속 미션 맵
        public List<Entry> Entries = new List<Entry>();    //집계 다음 미션 순서의 입력
        public List<string> Errors = new List<string>();    //적용을 막는 오류
        public List<string> Warnings = new List<string>();    //저장 기록 영향 안내
        public List<MissionData> Original = new List<MissionData>();    //기존 직렬화 배열 순서
        public Dictionary<string, MissionData> Missions = new( StringComparer.Ordinal );    //기존 미션 조회
        public Dictionary<string, MissionKillCountData> KillCounts = new( StringComparer.Ordinal );    //기존 집계 조회
        public Dictionary<string, Entry> KillEntries = new( StringComparer.Ordinal );    //입력 집계 조회
        public List<ScriptableObject> Temporary = new List<ScriptableObject>();    //해제할 검사 전용 객체
        public string Fingerprint;    //임시 객체 식별값을 제외한 변경 비교값
        public bool MapSavePending;    //맵의 기존 미저장 내용 존재 여부

        /// <summary>
        /// 파일에 저장하지 않은 검사 객체 해제
        /// </summary>
        public void Dispose ()
        {
            for ( int i = Temporary.Count - 1; i >= 0; i-- )
            {
                if ( Temporary[i] != null )
                    Object.DestroyImmediate( Temporary[i] );
            }
            Temporary.Clear();
        }
    }

    /// <summary>
    /// CSV와 기존 에셋을 읽고 변경 없는 최종 구성 검사
    /// </summary>
    /// <param name="missionCsvPath">필수 미션 CSV 경로</param>
    /// <param name="killCountCsvPath">선택 집계 CSV 경로</param>
    /// <param name="folder">신규 에셋을 둘 기존 Assets 폴더</param>
    /// <param name="map">등록할 미션 맵</param>
    /// <returns>오류와 미리보기를 보관한 계획</returns>
    internal static Plan Prepare ( string missionCsvPath, string killCountCsvPath, string folder, MissionDataMap map )
    {
        Plan plan = new Plan { Map = map };
        try
        {
            RequireAsset( map, "Assets 아래에 저장된 독립 MissionDataMap을 연결하세요." );
            if ( string.IsNullOrEmpty( folder ) == true || folder.StartsWith( "Assets/", StringComparison.Ordinal ) == false
                || AssetDatabase.IsValidFolder( folder ) == false )
                throw new FormatException( "Assets 아래의 기존 출력 폴더를 선택하세요." );

            string missionCsv = ReadCsv( missionCsvPath );
            string killCountCsv = string.IsNullOrWhiteSpace( killCountCsvPath ) == true ? null : ReadCsv( killCountCsvPath );
            if ( killCountCsv != null )
                ReadEntries( killCountCsv, true, plan );
            ReadEntries( missionCsv, false, plan );
            if ( plan.Errors.Count != 0 )
                return plan;

            ReadExisting( folder, plan );
            plan.MapSavePending = EditorUtility.IsDirty( map );
            if ( plan.MapSavePending == true )
                plan.Warnings.Add( "선택한 미션 맵에 미저장 변경이 있습니다. 현재 구성을 검사하고 적용 시 함께 저장합니다." );
            PrepareAssets( folder, plan );
            if ( plan.Errors.Count != 0 )
                return plan;

            PrepareFinalMap( plan );
            if ( plan.Errors.Count == 0 )
                plan.Fingerprint = CreateFingerprint( missionCsv, killCountCsv, folder, plan );
        }
        catch ( Exception exception ) when ( exception is FormatException || exception is IOException
            || exception is UnauthorizedAccessException || exception is DecoderFallbackException )
        {
            plan.Errors.Add( exception.Message );
        }
        catch
        {
            plan.Dispose();
            throw;
        }
        return plan;
    }

    /// <summary>
    /// UTF-8 CSV 파일 읽기
    /// </summary>
    /// <param name="path">읽을 파일 경로</param>
    /// <returns>CSV 원문</returns>
    static string ReadCsv ( string path )
    {
        if ( string.IsNullOrWhiteSpace( path ) == true
            || string.Equals( Path.GetExtension( path ), ".csv", StringComparison.OrdinalIgnoreCase ) == false )
            throw new FormatException( "CSV 파일 경로를 지정하세요." );
        return File.ReadAllText( path, new UTF8Encoding( false, true ) );
    }

    /// <summary>
    /// BOM/따옴표/줄바꿈을 처리하여 레코드 해석
    /// </summary>
    /// <param name="text">CSV 원문</param>
    /// <returns>빈 줄을 제외한 레코드</returns>
    static List<CsvRow> ParseCsv ( string text )
    {
        List<CsvRow> rows = new List<CsvRow>();
        List<string> cells = new List<string>();
        StringBuilder cell = new StringBuilder();
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
                    throw new FormatException( $"{startLine}행: 닫히지 않은 따옴표입니다." );
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
                    if ( character == '\n' || (character == '\r' && (i + 1 == text.Length || text[i + 1] != '\n')) )
                        line++;
                }
                continue;
            }

            if ( character == ',' || character == '\r' || character == '\n' )
            {
                cells.Add( cell.ToString().Trim() );
                cell.Clear();
                closed = false;
                if ( character == ',' )
                    continue;
                if ( cells.Count != 1 || cells[0].Length != 0 )
                    rows.Add( new CsvRow { Line = startLine, Cells = cells.ToArray() } );
                cells.Clear();
                if ( character == '\r' && i + 1 < text.Length && text[i + 1] == '\n' )
                    i++;
                line++;
                startLine = line;
            }
            else if ( character == '"' && cell.Length == 0 && closed == false )
                quoted = true;
            else
            {
                if ( closed == true || character == '"' )
                    throw new FormatException( $"{line}행: 따옴표 위치가 잘못됐습니다." );
                cell.Append( character );
            }
        }
        return rows;
    }

    /// <summary>
    /// 컬럼과 행별 입력 검사
    /// </summary>
    /// <param name="csv">CSV 원문</param>
    /// <param name="killCount">집계 CSV 여부</param>
    /// <param name="plan">입력과 오류를 보관할 계획</param>
    static void ReadEntries ( string csv, bool killCount, Plan plan )
    {
        string source = killCount == true ? "처치 집계 CSV" : "미션 CSV";
        string[] headers = killCount == true ? _killCountHeaders : _missionHeaders;
        List<CsvRow> rows;
        try
        {
            rows = ParseCsv( csv );
            if ( rows.Count < (killCount == true ? 1 : 2) )
                throw new FormatException( "헤더와 미션 행이 필요합니다. 집계 CSV는 헤더만 둘 수 있습니다." );
        }
        catch ( FormatException exception )
        {
            plan.Errors.Add( source + " / " + exception.Message );
            return;
        }

        Dictionary<string, int> columns = new( StringComparer.Ordinal );
        foreach ( string name in rows[0].Cells )
        {
            if ( Array.IndexOf( headers, name ) < 0 || columns.ContainsKey( name ) == true )
                throw new FormatException( $"{source}: 알 수 없거나 중복된 헤더입니다: {name}" );
            columns.Add( name, columns.Count );
        }
        foreach ( string header in headers )
        {
            if ( columns.ContainsKey( header ) == false )
                throw new FormatException( $"{source}: 필수 헤더가 없습니다: {header}" );
        }

        HashSet<string> ids = new HashSet<string>( StringComparer.Ordinal );
        for ( int i = 1; i < rows.Count; i++ )
        {
            CsvRow row = rows[i];
            try
            {
                if ( row.Cells.Length != headers.Length )
                    throw new FormatException( $"컬럼 수는 {headers.Length}개여야 합니다." );
                string[] values = new string[headers.Length];
                for ( int j = 0; j < headers.Length; j++ )
                    values[j] = row.Cells[columns[headers[j]]];
                ValidateId( values[0] );
                if ( ids.Add( values[0] ) == false )
                    throw new FormatException( $"ID가 중복됐습니다: {values[0]}" );
                Entry entry = new Entry { Id = values[0], Kind = killCount == true ? "KillCount" : values[1], Values = values, Line = row.Line };
                if ( killCount == true )
                {
                    if ( values[1].StartsWith( "Assets/", StringComparison.Ordinal ) == false )
                        throw new FormatException( "EnemyAssetPath는 Assets/로 시작해야 합니다." );
                    entry.Enemy = AssetDatabase.LoadAssetAtPath<EnemyData>( values[1] );
                    RequireAsset( entry.Enemy, "EnemyAssetPath에 독립 EnemyData 에셋이 없습니다: " + values[1] );
                    plan.KillEntries.Add( entry.Id, entry );
                }
                else
                    ValidateMissionInput( entry );
                plan.Entries.Add( entry );
            }
            catch ( FormatException exception )
            {
                plan.Errors.Add( $"{source} {row.Line}행: {exception.Message}" );
            }
        }
    }

    /// <summary>
    /// 파일명으로 사용할 고정 ID 검사
    /// </summary>
    /// <param name="id">검사할 ID</param>
    static void ValidateId ( string id )
    {
        if ( string.IsNullOrWhiteSpace( id ) == true || id.IndexOfAny( "<>:\"/\\|?*".ToCharArray() ) >= 0
            || id.EndsWith( ".", StringComparison.Ordinal ) == true )
            throw new FormatException( "파일명으로 사용할 수 없는 ID입니다: " + id );
        foreach ( char character in id )
        {
            if ( char.IsControl( character ) == true )
                throw new FormatException( "ID에 제어 문자를 사용할 수 없습니다." );
        }
        string stem = id.Split( '.' )[0].ToUpperInvariant();
        if ( stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL"
            || (stem.Length == 4 && (stem.StartsWith( "COM" ) || stem.StartsWith( "LPT" )) && stem[3] >= '1' && stem[3] <= '9') )
            throw new FormatException( "Windows 예약 파일명은 사용할 수 없습니다: " + id );
    }

    /// <summary>
    /// 종류별 필수 컬럼과 사용하지 않는 입력 검사
    /// </summary>
    /// <param name="entry">미션 입력</param>
    static void ValidateMissionInput ( Entry entry )
    {
        if ( typeof( MissionData ).IsAssignableFrom( GetDataType( entry.Kind ) ) == false )
            throw new FormatException( "미션 Type에는 처치 집계 정의를 사용할 수 없습니다." );
        string[] values = entry.Values;
        if ( values[2].Length == 0 )
            throw new FormatException( "DisplayName이 비어 있습니다." );
        HashSet<string> difficulties = new HashSet<string>( StringComparer.Ordinal );
        foreach ( string value in values[3].Split( '|' ) )
        {
            ReadEnum<MissionDifficulty>( value );
            if ( difficulties.Add( value ) == false )
                throw new FormatException( "Difficulties가 중복됐습니다: " + value );
        }
        ReadNumber( values[5], 0, "RewardAmount" );
        bool kill = entry.Kind == "Kill";
        bool count = UsesCount( entry.Kind );
        if ( entry.Kind == "PerfectDefense" && values[6].Length == 0 )
            throw new FormatException( "완전 방어에는 MapId가 필요합니다." );
        if ( count == true )
        {
            ReadEnum<MissionCountComparison>( values[7] );
            ReadNumber( values[8], 0, "Count" );
        }
        if ( kill == true )
        {
            if ( values[9].Length == 0 )
                throw new FormatException( "KillCountId가 필요합니다." );
            ReadNumber( values[10], 1, "TargetCount" );
        }
        for ( int i = 6; i < values.Length; i++ )
        {
            bool used = i == 6 ? kill == false : i <= 8 ? count : kill;
            if ( used == false && values[i].Length != 0 )
                throw new FormatException( $"{entry.Kind}에는 {_missionHeaders[i]}를 입력하지 않습니다." );
        }
    }

    /// <summary>
    /// CSV 이름을 구체 SO 타입으로 변환
    /// </summary>
    /// <param name="kind">CSV 종류</param>
    /// <returns>생성할 타입</returns>
    static Type GetDataType ( string kind )
    {
        switch ( kind )
        {
            case "KillCount": return typeof( MissionKillCountData );
            case "Kill": return typeof( KillMissionData );
            case "PerfectDefense": return typeof( PerfectDefenseMissionData );
            case "ItemUse": return typeof( ItemUseMissionData );
            case "PlayerDeath": return typeof( PlayerDeathMissionData );
            case "CompanionParticipation": return typeof( CompanionParticipationMissionData );
            case "StageClear": return typeof( StageClearMissionData );
            default: throw new FormatException( "알 수 없는 Type입니다: " + kind );
        }
    }

    /// <summary>
    /// 횟수 비교 설정을 사용하는 종류 확인
    /// </summary>
    /// <param name="kind">미션 종류</param>
    /// <returns>횟수 조건 사용 여부</returns>
    static bool UsesCount ( string kind )
    {
        return kind == "ItemUse" || kind == "PlayerDeath" || kind == "CompanionParticipation";
    }

    /// <summary>
    /// 이름으로 지정한 enum 값 해석
    /// </summary>
    /// <typeparam name="T">enum 타입</typeparam>
    /// <param name="value">정확한 항목 이름</param>
    /// <returns>해석한 값</returns>
    static T ReadEnum<T> ( string value ) where T : struct, Enum
    {
        if ( Enum.TryParse( value, out T result ) == false || Enum.IsDefined( typeof( T ), result ) == false || result.ToString() != value )
            throw new FormatException( $"{typeof( T ).Name} 이름을 확인하세요: {value}" );
        return result;
    }

    /// <summary>
    /// 음수가 아닌 정수 입력 해석
    /// </summary>
    /// <param name="value">입력 문자열</param>
    /// <param name="minimum">허용 최솟값</param>
    /// <param name="column">오류에 표시할 컬럼</param>
    /// <returns>해석한 정수</returns>
    static int ReadNumber ( string value, int minimum, string column )
    {
        if ( int.TryParse( value, NumberStyles.None, CultureInfo.InvariantCulture, out int result ) == false || result < minimum )
            throw new FormatException( $"{column}은 {minimum} 이상의 정수여야 합니다." );
        return result;
    }

    /// <summary>
    /// 빈 맵을 허용하여 직렬화 목록과 출력 폴더의 기존 에셋 수집
    /// </summary>
    /// <param name="folder">재임포트 시 기존 파일도 찾을 출력 폴더</param>
    /// <param name="plan">기존 구성을 보관할 계획</param>
    static void ReadExisting ( string folder, Plan plan )
    {
        using ( SerializedObject serialized = new SerializedObject( plan.Map ) )
        {
            SerializedProperty missions = serialized.FindProperty( "_missions" );
            HashSet<string> registered = new HashSet<string>( StringComparer.Ordinal );
            //OrderedMissions는 런타임 Validate 이후의 캐시이므로 원본 배열을 직접 읽음
            for ( int i = 0; i < missions.arraySize; i++ )
            {
                MissionData data = missions.GetArrayElementAtIndex( i ).objectReferenceValue as MissionData;
                RequireAsset( data, $"기존 맵 {i}번 미션 참조를 확인하세요." );
                if ( registered.Add( data.MissionId ) == false )
                    throw new FormatException( "기존 맵 미션 ID가 중복됐습니다: " + data.MissionId );
                plan.Original.Add( data );
                RegisterExisting( data, plan );
            }
        }
        //맵 등록 전에 적용이 실패했거나 폴더 안에서 이름이 바뀐 에셋도 같은 ID로 재사용
        string[] guids = AssetDatabase.FindAssets( "t:ScriptableObject", new[] { folder } );
        Array.Sort( guids, StringComparer.Ordinal );
        foreach ( string guid in guids )
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath( AssetDatabase.GUIDToAssetPath( guid ) );
            if ( asset is MissionData || asset is MissionKillCountData )
                RegisterExisting( (ScriptableObject) asset, plan );
        }
    }

    /// <summary>
    /// 기존 미션과 공유 집계를 중복 ID 없이 등록
    /// </summary>
    /// <param name="asset">수집한 원본 에셋</param>
    /// <param name="plan">기존 조회 목록</param>
    static void RegisterExisting ( ScriptableObject asset, Plan plan )
    {
        RequireAsset( asset, "Assets 내 독립 미션/집계 에셋이 필요합니다." );
        if ( asset is MissionData mission )
        {
            if ( string.IsNullOrWhiteSpace( mission.MissionId ) == true )
                throw new FormatException( "기존 미션 ID가 비어 있습니다: " + AssetDatabase.GetAssetPath( asset ) );
            if ( plan.Missions.TryGetValue( mission.MissionId, out MissionData other ) == true && other != mission )
                throw new FormatException( "다른 미션 SO의 ID가 충돌합니다: " + mission.MissionId );
            plan.Missions[mission.MissionId] = mission;
            if ( mission is KillMissionData kill && kill.KillCountData != null )
                RegisterExisting( kill.KillCountData, plan );
        }
        else if ( asset is MissionKillCountData count )
        {
            if ( string.IsNullOrWhiteSpace( count.KillCountId ) == true )
                throw new FormatException( "기존 집계 ID가 비어 있습니다: " + AssetDatabase.GetAssetPath( asset ) );
            if ( plan.KillCounts.TryGetValue( count.KillCountId, out MissionKillCountData other ) == true && other != count )
                throw new FormatException( "다른 처치 집계 SO의 ID가 충돌합니다: " + count.KillCountId );
            plan.KillCounts[count.KillCountId] = count;
        }
    }

    /// <summary>
    /// 경로/타입/고정 집계 의미를 검사하고 후보 객체 작성
    /// </summary>
    /// <param name="folder">신규 출력 폴더</param>
    /// <param name="plan">입력과 원본 구성</param>
    static void PrepareAssets ( string folder, Plan plan )
    {
        HashSet<string> paths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
        paths.Add( AssetDatabase.GetAssetPath( plan.Map ) );
        foreach ( Entry entry in plan.Entries )
        {
            try
            {
                if ( entry.IsKillCount == true )
                {
                    plan.KillCounts.TryGetValue( entry.Id, out MissionKillCountData count );
                    entry.Existing = count;
                }
                else
                {
                    plan.Missions.TryGetValue( entry.Id, out MissionData mission );
                    entry.Existing = mission;
                    entry.AddToMap = mission == null || plan.Original.Contains( mission ) == false;
                }
                entry.AssetPath = entry.Existing == null ? folder + "/" + entry.Id + ".asset" : AssetDatabase.GetAssetPath( entry.Existing );
                if ( paths.Add( entry.AssetPath ) == false )
                    throw new FormatException( "맵 또는 다른 입력과 출력 경로가 충돌합니다: " + entry.AssetPath );
                if ( entry.Existing == null && (AssetDatabase.LoadMainAssetAtPath( entry.AssetPath ) != null
                    || File.Exists( entry.AssetPath ) == true || Directory.Exists( entry.AssetPath ) == true
                    || File.Exists( entry.AssetPath + ".meta" ) == true) )
                    throw new FormatException( "출력 경로의 다른 파일/메타와 충돌합니다: " + entry.AssetPath );
                if ( entry.Existing != null && entry.Existing.GetType() != GetDataType( entry.Kind ) )
                    throw new FormatException( "동일 ID의 구체 SO 타입을 변경할 수 없습니다." );
                entry.SavePending = entry.Existing != null && EditorUtility.IsDirty( entry.Existing );
                if ( entry.SavePending == true )
                    plan.Warnings.Add( $"{entry.Id}: 기존 에셋의 미저장 변경을 포함하여 검사하고 적용 시 함께 저장합니다." );

                if ( entry.IsKillCount == true && entry.Existing != null )
                {
                    using ( SerializedObject serialized = new SerializedObject( entry.Existing ) )
                    {
                        if ( serialized.FindProperty( "_enemy" ).objectReferenceValue != entry.Enemy
                            || serialized.FindProperty( "_mapId" ).stringValue != entry.Values[2] )
                            throw new FormatException( "같은 집계 ID의 적/맵은 변경할 수 없습니다. 새 집계 ID를 사용하세요." );
                    }
                }
                entry.Candidate = entry.Existing == null ? (ScriptableObject) ScriptableObject.CreateInstance( GetDataType( entry.Kind ) )
                    : Object.Instantiate( entry.Existing );
                plan.Temporary.Add( entry.Candidate );
                entry.Candidate.name = entry.Existing == null ? entry.Id : entry.Existing.name;
                WriteEntry( entry.Candidate, entry, plan, false );
            }
            catch ( FormatException exception )
            {
                plan.Errors.Add( $"{(entry.IsKillCount == true ? "처치 집계" : "미션")} CSV {entry.Line}행 / {entry.Id}: {exception.Message}" );
            }
        }
    }

    /// <summary>
    /// 후보 또는 실제 에셋에 검사한 입력 설정
    /// </summary>
    /// <param name="asset">설정할 SO</param>
    /// <param name="entry">검사한 입력</param>
    /// <param name="plan">집계 조회 정보</param>
    /// <param name="applying">영속 참조를 연결하는 적용 단계 여부</param>
    static void WriteEntry ( ScriptableObject asset, Entry entry, Plan plan, bool applying )
    {
        string[] values = entry.Values;
        using ( SerializedObject serialized = new SerializedObject( asset ) )
        {
            if ( entry.IsKillCount == true )
            {
                serialized.FindProperty( "_killCountId" ).stringValue = entry.Id;
                serialized.FindProperty( "_enemy" ).objectReferenceValue = entry.Enemy;
                serialized.FindProperty( "_mapId" ).stringValue = values[2];
            }
            else
            {
                serialized.FindProperty( "_missionId" ).stringValue = entry.Id;
                serialized.FindProperty( "_displayName" ).stringValue = values[2];
                serialized.FindProperty( "_prerequisiteId" ).stringValue = values[4];
                serialized.FindProperty( "_rewardAmount" ).intValue = int.Parse( values[5], CultureInfo.InvariantCulture );
                string[] names = values[3].Split( '|' );
                SerializedProperty difficulties = serialized.FindProperty( "_difficulties" );
                difficulties.arraySize = names.Length;
                for ( int i = 0; i < names.Length; i++ )
                    difficulties.GetArrayElementAtIndex( i ).intValue = (int) Enum.Parse( typeof( MissionDifficulty ), names[i] );
                if ( asset is ClearMissionData )
                    serialized.FindProperty( "_mapId" ).stringValue = values[6];
                if ( UsesCount( entry.Kind ) == true )
                {
                    SerializedProperty condition = serialized.FindProperty( "_condition" );
                    condition.FindPropertyRelative( "_comparison" ).intValue = (int) Enum.Parse( typeof( MissionCountComparison ), values[7] );
                    condition.FindPropertyRelative( "_count" ).intValue = int.Parse( values[8], CultureInfo.InvariantCulture );
                }
                if ( asset is KillMissionData )
                {
                    serialized.FindProperty( "_killCountData" ).objectReferenceValue = ResolveKillCount( values[9], plan, applying );
                    serialized.FindProperty( "_targetCount" ).intValue = int.Parse( values[10], CultureInfo.InvariantCulture );
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// 입력 또는 기존 설정에서 단계에 맞는 공유 집계 조회
    /// </summary>
    /// <param name="id">집계 ID</param>
    /// <param name="plan">집계 조회 정보</param>
    /// <param name="applying">실제 저장 단계 여부</param>
    /// <returns>검사 후보 또는 영속 집계</returns>
    static MissionKillCountData ResolveKillCount ( string id, Plan plan, bool applying )
    {
        if ( plan.KillEntries.TryGetValue( id, out Entry entry ) == true )
        {
            MissionKillCountData count = (applying == true ? entry.Applied : entry.Candidate) as MissionKillCountData;
            if ( count == null )
                throw new FormatException( "처치 집계 입력을 먼저 확인하세요: " + id );
            return count;
        }
        if ( plan.KillCounts.TryGetValue( id, out MissionKillCountData existing ) == true )
            return existing;
        throw new FormatException( "참조할 처치 집계 ID가 없습니다: " + id );
    }

    /// <summary>
    /// 유지할 미션까지 후보 집계로 연결한 최종 맵 검사와 미리보기 준비
    /// </summary>
    /// <param name="plan">검사할 계획</param>
    static void PrepareFinalMap ( Plan plan )
    {
        List<MissionData> merged = new List<MissionData>( plan.Original );
        foreach ( Entry entry in plan.Entries )
        {
            if ( entry.IsKillCount == true )
            {
                if ( ((MissionKillCountData) entry.Candidate).Validate() == false )
                    throw new FormatException( "처치 집계 설정 오류: " + entry.Id );
                continue;
            }
            if ( entry.AddToMap == true )
                merged.Add( (MissionData) entry.Candidate );
            else
                merged[merged.IndexOf( (MissionData) entry.Existing )] = (MissionData) entry.Candidate;
        }

        for ( int i = 0; i < merged.Count; i++ )
        {
            if ( merged[i] is KillMissionData kill && kill.KillCountData != null
                && plan.KillEntries.ContainsKey( kill.KillCountData.KillCountId ) == true
                && plan.Temporary.Contains( kill ) == false )
            {
                //원본을 건드리지 않고 유지 항목도 동일 후보 집계를 참조하도록 검사 복제본 작성
                KillMissionData copy = Object.Instantiate( kill );
                plan.Temporary.Add( copy );
                using ( SerializedObject serialized = new SerializedObject( copy ) )
                {
                    serialized.FindProperty( "_killCountData" ).objectReferenceValue = ResolveKillCount( kill.KillCountData.KillCountId, plan, false );
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                merged[i] = copy;
            }
        }
        MissionDataMap temporaryMap = ScriptableObject.CreateInstance<MissionDataMap>();
        plan.Temporary.Add( temporaryMap );
        WriteMap( temporaryMap, merged );
        if ( temporaryMap.Validate() == false )
            throw new FormatException( "최종 미션 맵 검사에 실패했습니다. Console에서 미션 ID와 선행/난이도/집계 설정을 확인하세요." );

        foreach ( Entry entry in plan.Entries )
        {
            entry.Changed = entry.Existing == null || Snapshot( entry.Existing, plan ) != Snapshot( entry.Candidate, plan );
            entry.Before = entry.Existing == null ? "신규" : Describe( entry.Existing, plan );
            entry.After = Describe( entry.Candidate, plan );
            if ( entry.IsKillCount == true )
            {
                List<string> users = new List<string>();
                foreach ( MissionData mission in merged )
                {
                    if ( mission is KillMissionData kill && kill.KillCountData.KillCountId == entry.Id )
                        users.Add( mission.MissionId );
                }
                entry.After += "\n최종 맵 참조: " + (users.Count == 0 ? "없음" : string.Join( ", ", users ));
            }
            else if ( entry.Existing is MissionData before && entry.Candidate is MissionData after && entry.Changed == true )
            {
                if ( before.Difficulties != null )
                {
                    foreach ( MissionDifficulty difficulty in before.Difficulties )
                    {
                        if ( after.Supports( difficulty ) == false )
                            plan.Warnings.Add( $"{entry.Id}: {difficulty} 난이도 제거. 해당 난이도 기록이 있는 저장 슬롯은 초기화에 실패할 수 있습니다." );
                    }
                }
                if ( before is KillMissionData oldKill && after is KillMissionData newKill
                    && oldKill.KillCountData != newKill.KillCountData
                    && ReferenceIdentity( oldKill.KillCountData, plan ) != ReferenceIdentity( newKill.KillCountData, plan ) )
                    plan.Warnings.Add( $"{entry.Id}: 집계 참조 변경. 기존 완료/지급은 유지되고 진행도는 새 집계에서 조회합니다." );
                if ( before.RewardAmount != after.RewardAmount )
                    plan.Warnings.Add( $"{entry.Id}: 보상 변경. 이미 지급한 보상은 유지되며 미지급 완료에는 새 보상이 사용됩니다." );
            }
        }
    }

    /// <summary>
    /// 미션 맵의 직렬화 참조 배열 작성
    /// </summary>
    /// <param name="map">작성할 맵</param>
    /// <param name="missions">등록 순서의 미션</param>
    static void WriteMap ( MissionDataMap map, List<MissionData> missions )
    {
        using ( SerializedObject serialized = new SerializedObject( map ) )
        {
            SerializedProperty list = serialized.FindProperty( "_missions" );
            list.arraySize = missions.Count;
            for ( int i = 0; i < missions.Count; i++ )
                list.GetArrayElementAtIndex( i ).objectReferenceValue = missions[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// 임시 객체도 실제 반영 대상의 안정된 식별 정보로 변환
    /// </summary>
    /// <param name="asset">참조 대상</param>
    /// <param name="plan">후보와 영속 에셋 대응 정보</param>
    /// <returns>GUID/경로 또는 신규 예정 경로</returns>
    static string ReferenceIdentity ( Object asset, Plan plan )
    {
        if ( asset == null )
            return "미연결";
        foreach ( Entry entry in plan.Entries )
        {
            if ( entry.Candidate != asset )
                continue;
            return entry.Existing == null ? "신규:" + entry.AssetPath : AssetIdentity( entry.Existing );
        }
        return AssetIdentity( asset );
    }

    /// <summary>
    /// 영속 에셋의 GUID와 경로 조회
    /// </summary>
    /// <param name="asset">저장된 독립 에셋</param>
    /// <returns>안정된 식별 정보</returns>
    static string AssetIdentity ( Object asset )
    {
        string path = AssetDatabase.GetAssetPath( asset );
        return AssetDatabase.AssetPathToGUID( path ) + ":" + path;
    }

    /// <summary>
    /// 실제 데이터 필드만 안정된 순서로 읽기
    /// </summary>
    /// <param name="asset">비교하거나 표시할 데이터</param>
    /// <param name="plan">후보 참조 해석 정보</param>
    /// <returns>미리보기와 비교에 사용할 값 목록</returns>
    static List<string> ReadValues ( ScriptableObject asset, Plan plan )
    {
        List<string> values = new List<string> { asset.GetType().Name };
        using ( SerializedObject serialized = new SerializedObject( asset ) )
        {
            if ( asset is MissionKillCountData count )
            {
                values.Add( count.KillCountId );
                values.Add( ReferenceIdentity( serialized.FindProperty( "_enemy" ).objectReferenceValue, plan ) );
                values.Add( serialized.FindProperty( "_mapId" ).stringValue );
                return values;
            }
            MissionData mission = (MissionData) asset;
            values.Add( mission.MissionId );
            values.Add( mission.DisplayName );
            List<string> difficulties = new List<string>();
            if ( mission.Difficulties != null )
            {
                foreach ( MissionDifficulty difficulty in mission.Difficulties )
                    difficulties.Add( difficulty.ToString() );
            }
            difficulties.Sort( StringComparer.Ordinal );
            values.Add( string.Join( "|", difficulties ) );
            values.Add( mission.PrerequisiteId ?? "" );
            values.Add( mission.RewardAmount.ToString( CultureInfo.InvariantCulture ) );
            if ( mission is ClearMissionData )
                values.Add( serialized.FindProperty( "_mapId" ).stringValue );
            if ( serialized.FindProperty( "_condition" ) is SerializedProperty condition )
            {
                values.Add( ((MissionCountComparison) condition.FindPropertyRelative( "_comparison" ).intValue).ToString() );
                values.Add( condition.FindPropertyRelative( "_count" ).intValue.ToString( CultureInfo.InvariantCulture ) );
            }
            if ( mission is KillMissionData kill )
            {
                values.Add( kill.KillCountData == null ? "" : kill.KillCountData.KillCountId );
                values.Add( ReferenceIdentity( kill.KillCountData, plan ) );
                values.Add( kill.TargetCount.ToString( CultureInfo.InvariantCulture ) );
            }
        }
        return values;
    }

    /// <summary>
    /// 셀 내부 줄바꿈이나 구분자도 충돌하지 않는 비교값 작성
    /// </summary>
    /// <param name="asset">비교할 데이터</param>
    /// <param name="plan">후보 참조 정보</param>
    /// <returns>길이를 포함한 설정 비교값</returns>
    static string Snapshot ( ScriptableObject asset, Plan plan )
    {
        StringBuilder result = new StringBuilder();
        foreach ( string value in ReadValues( asset, plan ) )
            AppendToken( result, value );
        return result.ToString();
    }

    /// <summary>
    /// 종류에 맞는 이름표로 변경값 표시
    /// </summary>
    /// <param name="asset">표시할 데이터</param>
    /// <param name="plan">후보 참조 정보</param>
    /// <returns>종류별 설정 설명</returns>
    static string Describe ( ScriptableObject asset, Plan plan )
    {
        List<string> values = ReadValues( asset, plan );
        List<string> labels = new List<string> { "타입", "ID" };
        if ( asset is MissionKillCountData )
            labels.AddRange( new[] { "대상 적", "맵(빈 값=공통)" } );
        else
        {
            labels.AddRange( new[] { "이름", "난이도", "선행 ID", "보상" } );
            if ( asset is ClearMissionData )
                labels.Add( "맵(빈 값=공통)" );
            if ( asset is ItemUseMissionData || asset is PlayerDeathMissionData || asset is CompanionParticipationMissionData )
                labels.AddRange( new[] { "비교", "기준 횟수" } );
            if ( asset is KillMissionData )
                labels.AddRange( new[] { "집계 ID", "집계 에셋", "목표" } );
        }
        StringBuilder result = new StringBuilder();
        for ( int i = 0; i < values.Count; i++ )
        {
            if ( i > 0 )
                result.Append( '\n' );
            result.Append( labels[i] ).Append( ": " ).Append( values[i] );
        }
        return result.ToString();
    }

    /// <summary>
    /// 관련 입력/원본/후보의 안정된 변경 비교값 생성
    /// </summary>
    /// <param name="missionCsv">미션 CSV 원문</param>
    /// <param name="killCountCsv">선택 집계 CSV 원문</param>
    /// <param name="folder">출력 폴더</param>
    /// <param name="plan">검사한 구성</param>
    /// <returns>적용 직전 재검사 비교값</returns>
    static string CreateFingerprint ( string missionCsv, string killCountCsv, string folder, Plan plan )
    {
        StringBuilder result = new StringBuilder();
        AppendToken( result, missionCsv );
        AppendToken( result, killCountCsv ?? "" );
        AppendToken( result, folder );
        AppendToken( result, AssetIdentity( plan.Map ) );
        AppendToken( result, plan.MapSavePending.ToString() );
        foreach ( MissionData mission in plan.Original )
        {
            AppendToken( result, AssetIdentity( mission ) );
            AppendToken( result, Snapshot( mission, plan ) );
        }
        List<string> ids = new List<string>( plan.KillCounts.Keys );
        ids.Sort( StringComparer.Ordinal );
        foreach ( string id in ids )
        {
            AppendToken( result, AssetIdentity( plan.KillCounts[id] ) );
            AppendToken( result, Snapshot( plan.KillCounts[id], plan ) );
        }
        foreach ( Entry entry in plan.Entries )
        {
            AppendToken( result, entry.AssetPath );
            AppendToken( result, entry.SavePending.ToString() );
            AppendToken( result, entry.Existing == null ? "" : AssetIdentity( entry.Existing ) );
            AppendToken( result, entry.Existing == null ? "" : Snapshot( entry.Existing, plan ) );
            AppendToken( result, Snapshot( entry.Candidate, plan ) );
        }
        return result.ToString();
    }

    /// <summary>
    /// 문자열 경계를 길이로 구분하여 비교값에 추가
    /// </summary>
    /// <param name="builder">작성 중인 비교값</param>
    /// <param name="value">추가할 값</param>
    static void AppendToken ( StringBuilder builder, string value )
    {
        value = value ?? "";
        builder.Append( value.Length ).Append( ':' ).Append( value );
    }

    /// <summary>
    /// Assets 안의 영속 독립 에셋 확인
    /// </summary>
    /// <param name="asset">검사할 참조</param>
    /// <param name="error">실패 설명</param>
    static void RequireAsset ( Object asset, string error )
    {
        if ( asset == null || AssetDatabase.IsMainAsset( asset ) == false
            || AssetDatabase.GetAssetPath( asset ).StartsWith( "Assets/", StringComparison.Ordinal ) == false )
            throw new FormatException( error );
    }

    /// <summary>
    /// 집계 다음 미션 순서로 영속 참조를 작성하고 맵 등록
    /// </summary>
    /// <param name="plan">적용 직전 재검사를 통과한 계획</param>
    /// <param name="touched">부분 실패 시 확인할 반영 시작 경로</param>
    internal static void Apply ( Plan plan, List<string> touched )
    {
        if ( EditorApplication.isPlayingOrWillChangePlaymode == true || plan.Errors.Count != 0 )
            throw new InvalidOperationException( "편집 모드의 검사 통과 계획만 적용할 수 있습니다." );
        List<MissionData> merged = new List<MissionData>( plan.Original );
        foreach ( Entry entry in plan.Entries )
        {
            ScriptableObject asset = entry.Existing;
            if ( asset == null )
            {
                asset = Object.Instantiate( entry.Candidate );
                asset.name = entry.Id;
                try
                {
                    //검사용 후보가 참조하던 임시 집계를 실제 저장한 집계로 교체한 뒤 파일 생성
                    WriteEntry( asset, entry, plan, true );
                    touched.Add( entry.AssetPath );
                    AssetDatabase.CreateAsset( asset, entry.AssetPath );
                }
                finally
                {
                    if ( AssetDatabase.Contains( asset ) == false )
                        Object.DestroyImmediate( asset );
                }
            }
            else if ( entry.Changed == true )
            {
                touched.Add( entry.AssetPath );
                WriteEntry( asset, entry, plan, true );
                EditorUtility.SetDirty( asset );
            }
            entry.Applied = asset;
            if ( entry.SavePending == true && entry.Changed == false )
                touched.Add( entry.AssetPath );
            if ( entry.Existing == null || entry.Changed == true || entry.SavePending == true )
                SaveAsset( asset );
            if ( entry.AddToMap == true )
                merged.Add( (MissionData) asset );
        }

        foreach ( MissionData mission in merged )
        {
            RequireAsset( mission, "저장 대상에 임시 미션 참조가 있습니다." );
            if ( mission is KillMissionData kill )
                RequireAsset( kill.KillCountData, "저장 대상에 임시 처치 집계 참조가 있습니다: " + mission.MissionId );
        }
        if ( merged.Count != plan.Original.Count || plan.MapSavePending == true )
        {
            touched.Add( AssetDatabase.GetAssetPath( plan.Map ) );
            if ( merged.Count != plan.Original.Count )
            {
                WriteMap( plan.Map, merged );
                EditorUtility.SetDirty( plan.Map );
            }
            SaveAsset( plan.Map );
        }
        if ( plan.Map.Validate() == false )
            throw new InvalidOperationException( "반영한 미션 맵 검사에 실패했습니다. 부분 반영 경로와 Console을 확인하세요." );
    }

    /// <summary>
    /// 변경 대상만 저장하고 파일/미저장 상태 확인
    /// </summary>
    /// <param name="asset">저장할 에셋</param>
    static void SaveAsset ( Object asset )
    {
        AssetDatabase.SaveAssetIfDirty( asset );
        string path = AssetDatabase.GetAssetPath( asset );
        if ( EditorUtility.IsDirty( asset ) == true || File.Exists( path ) == false )
            throw new IOException( "에셋 저장을 완료하지 못했습니다: " + path );
    }
}
