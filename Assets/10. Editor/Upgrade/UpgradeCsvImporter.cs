using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 강화 CSV 임포터 - 전체 검사 후 강화 에셋과 맵 반영
/// </summary>
internal static class UpgradeCsvImporter
{
    static readonly string[] _headers =
    {
        "UpgradeId", "Type", "WeaponType", "TargetAssetPath", "Level", "Price",
        "MaxHpBonus", "MoveSpeedBonus", "DamageBonus", "AttackSpeedBonus", "HpBonus", "IntervalReduction", "SlowRateBonus"
    };    //기존 필수 컬럼과 신규 감속 선택 컬럼
    static readonly string[] _effectFields =
    {
        "_maxHpBonus", "_moveSpeedBonus", "_damageBonus", "_attackSpeedBonus", "_hpBonus", "_intervalReduction", "_slowRateBonus"
    };    //효과 컬럼 순서의 직렬화 필드

    /// <summary>
    /// CSV 행 - 원본 위치와 셀 보관
    /// </summary>
    sealed class CsvRow
    {
        public int Line;    //원본 시작 줄
        public string[] Cells;    //헤더 순서의 셀
    }

    /// <summary>
    /// 강화 단계 입력 - 가격과 효과 보관
    /// </summary>
    internal sealed class Level
    {
        public int Number;    //1부터 시작하는 단계
        public int Price;    //구매 가격
        public float[] Effects = new float[_effectFields.Length];    //효과 컬럼 순서의 값
    }

    /// <summary>
    /// 강화 항목 - 대상과 생성 또는 갱신 후보 보관
    /// </summary>
    internal sealed class Entry
    {
        public string Id;    //저장용 ID
        public string Kind;    //CSV 종류
        public string TargetPath;    //동료 또는 설치물 경로
        public WeaponType Weapon;    //무기 대상
        public Object Target;    //동료 또는 설치물 원본
        public int Line;    //첫 단계의 원본 줄
        public List<Level> Levels = new List<Level>();    //단계 순서 입력
        public UpgradeData Existing;    //기존 에셋
        public UpgradeData Candidate;    //검사용 임시 에셋
        public string AssetPath;    //반영 경로
        public string Before;    //기존 값 표시
        public string After;    //반영 값 표시
        public bool Changed;    //직렬화 값 변경 여부
        public bool AddToMap;    //맵 추가 필요 여부
    }

    /// <summary>
    /// 임포트 계획 - 미리보기와 임시 에셋 수명 관리
    /// </summary>
    internal sealed class Plan : IDisposable
    {
        public List<Entry> Entries = new List<Entry>();    //CSV 순서의 강화
        public List<string> Errors = new List<string>();    //검사 오류
        public List<UpgradeData> Original = new List<UpgradeData>();    //기존 맵 순서
        public UpgradeDataMap Map;    //등록 대상 맵
        public string Fingerprint;    //미리보기 이후 변경 비교값

        /// <summary>
        /// 검사용 임시 강화 해제
        /// </summary>
        public void Dispose ()
        {
            foreach ( Entry entry in Entries )
            {
                if ( entry.Candidate != null )
                    Object.DestroyImmediate( entry.Candidate );
            }
        }
    }

    /// <summary>
    /// CSV와 최종 맵을 검사하고 파일 변경 없는 계획 생성
    /// </summary>
    /// <param name="csvPath">CSV 파일 경로</param>
    /// <param name="folder">기존 Assets 출력 폴더</param>
    /// <param name="map">저장된 대상 맵</param>
    /// <returns>오류와 미리보기를 포함한 계획</returns>
    internal static Plan Prepare ( string csvPath, string folder, UpgradeDataMap map )
    {
        Plan plan = new Plan { Map = map };
        try
        {
            if ( map == null || AssetDatabase.IsMainAsset( map ) == false
                || AssetDatabase.GetAssetPath( map ).StartsWith( "Assets/", StringComparison.Ordinal ) == false )
                throw new FormatException( "Assets 아래에 저장된 독립 UpgradeDataMap을 연결해 주세요." );
            if ( folder.StartsWith( "Assets/", StringComparison.Ordinal ) == false
                || AssetDatabase.IsValidFolder( folder ) == false )
                throw new FormatException( "Assets 아래의 기존 출력 폴더를 선택해 주세요." );
            if ( string.Equals( Path.GetExtension( csvPath ), ".csv", StringComparison.OrdinalIgnoreCase ) == false )
                throw new FormatException( "CSV 파일을 선택해 주세요." );

            string csv = File.ReadAllText( csvPath, new UTF8Encoding( false, true ) );
            List<CsvRow> rows = ParseCsv( csv );
            ReadEntries( rows, plan );
            if ( plan.Errors.Count != 0 )
                return plan;

            if ( map.Upgrades == null )
                throw new FormatException( "대상 맵의 Upgrades 배열이 없습니다." );
            plan.Original.AddRange( map.Upgrades );
            PrepareAssets( folder, plan );
            if ( plan.Errors.Count != 0 )
                return plan;

            ValidateFinalMap( plan );
            StringBuilder fingerprint = new StringBuilder( csv );
            fingerprint.Append( folder ).Append( EditorJsonUtility.ToJson( map ) );
            foreach ( UpgradeData data in plan.Original )
                fingerprint.Append( EditorJsonUtility.ToJson( data ) );
            foreach ( Entry entry in plan.Entries )
            {
                fingerprint.Append( entry.AssetPath );
                if ( entry.Existing != null )
                    fingerprint.Append( EditorJsonUtility.ToJson( entry.Existing ) );
                fingerprint.Append( EditorJsonUtility.ToJson( entry.Candidate ) );
            }
            plan.Fingerprint = fingerprint.ToString();
        }
        catch ( Exception exception ) when ( exception is FormatException
            || exception is IOException || exception is UnauthorizedAccessException
            || exception is DecoderFallbackException )
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
    /// 따옴표와 BOM 및 줄바꿈을 처리하여 CSV 레코드 해석
    /// </summary>
    /// <param name="text">UTF-8 CSV 본문</param>
    /// <returns>빈 줄을 제외한 원본 레코드</returns>
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
                    throw new FormatException( $"CSV {startLine}행: 닫히지 않은 따옴표입니다." );
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
                    if ( character == '\n' || (character == '\r'
                        && (i + 1 == text.Length || text[i + 1] != '\n')) )
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
                    throw new FormatException( $"CSV {line}행: 따옴표 위치가 잘못됐습니다." );
                cell.Append( character );
            }
        }
        return rows;
    }

    /// <summary>
    /// 헤더와 각 행을 검사하고 ID별 단계 구성
    /// </summary>
    /// <param name="rows">원본 레코드</param>
    /// <param name="plan">오류와 항목을 보관할 계획</param>
    static void ReadEntries ( List<CsvRow> rows, Plan plan )
    {
        if ( rows.Count < 2 )
            throw new FormatException( "헤더와 강화 데이터가 필요합니다." );
        Dictionary<string, int> columns = new( StringComparer.Ordinal );
        for ( int i = 0; i < rows[0].Cells.Length; i++ )
        {
            string name = rows[0].Cells[i];
            if ( Array.IndexOf( _headers, name ) < 0 || columns.ContainsKey( name ) == true )
                throw new FormatException( $"CSV 헤더가 알 수 없거나 중복됐습니다: {name}" );
            columns.Add( name, i );
        }
        foreach ( string header in _headers )
        {
            //기존 CSV는 새 감속 컬럼 없이도 읽을 수 있음
            if ( header == "SlowRateBonus" )
                continue;

            if ( columns.ContainsKey( header ) == false )
                throw new FormatException( $"CSV 필수 헤더가 없습니다: {header}" );
        }

        Dictionary<string, Entry> groups = new( StringComparer.Ordinal );
        for ( int i = 1; i < rows.Count; i++ )
        {
            CsvRow row = rows[i];
            try
            {
                if ( row.Cells.Length != rows[0].Cells.Length )
                    throw new FormatException( $"컬럼 수는 {rows[0].Cells.Length}개여야 합니다." );
                string[] values = new string[_headers.Length];
                //입력 순서와 선택 컬럼을 기존 내부 처리 순서로 정규화
                for ( int j = 0; j < _headers.Length; j++ )
                {
                    values[j] = columns.TryGetValue( _headers[j], out int columnIndex ) == true
                        ? row.Cells[columnIndex] : string.Empty;
                }
                ReadRow( values, row.Line, groups, plan );
            }
            catch ( FormatException exception )
            {
                plan.Errors.Add( $"CSV {row.Line}행: {exception.Message}" );
            }
        }
        foreach ( Entry entry in plan.Entries )
        {
            entry.Levels.Sort( ( left, right ) => left.Number.CompareTo( right.Number ) );
            for ( int i = 0; i < entry.Levels.Count; i++ )
            {
                if ( entry.Levels[i].Number != i + 1 )
                {
                    plan.Errors.Add( $"CSV {entry.Line}행 / {entry.Id}: 단계는 중복 없이 1부터 연속이어야 합니다." );
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 한 행의 대상과 종류별 효과 검증
    /// </summary>
    /// <param name="values">표준 헤더 순서의 셀</param>
    /// <param name="line">원본 줄</param>
    /// <param name="groups">ID별 항목</param>
    /// <param name="plan">항목 보관 계획</param>
    static void ReadRow ( string[] values, int line, Dictionary<string, Entry> groups, Plan plan )
    {
        string id = values[0];
        //ID를 파일명으로 사용하므로 경로 구분자와 Windows 예약 파일명을 제한
        if ( string.IsNullOrWhiteSpace( id ) == true || id.IndexOfAny( "<>:\"/\\|?*".ToCharArray() ) >= 0
            || id.EndsWith( ".", StringComparison.Ordinal ) == true )
            throw new FormatException( $"파일명으로 사용할 수 없는 UpgradeId입니다: {id}" );
        foreach ( char character in id )
        {
            if ( char.IsControl( character ) == true )
                throw new FormatException( $"UpgradeId에 제어 문자를 사용할 수 없습니다: {id}" );
        }
        string stem = id.Split( '.' )[0].ToUpperInvariant();
        if ( stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL"
            || (stem.Length == 4 && (stem.StartsWith( "COM" ) || stem.StartsWith( "LPT" ))
                && stem[3] >= '1' && stem[3] <= '9') )
            throw new FormatException( $"Windows 예약 파일명은 사용할 수 없습니다: {id}" );

        Type type = GetDataType( values[1] );
        bool weapon = typeof( WeaponUpgradeData ).IsAssignableFrom( type );
        bool companion = type == typeof( CompanionUpgradeData );
        bool installation = typeof( InstallationUpgradeData ).IsAssignableFrom( type );
        WeaponType weaponType = default;
        if ( weapon == true )
        {
            if ( Enum.TryParse( values[2], out weaponType ) == false
                || Enum.IsDefined( typeof( WeaponType ), weaponType ) == false
                || values[2] != weaponType.ToString() )
                throw new FormatException( $"{id}: WeaponType은 Sword / Bow / Staff 중 하나여야 합니다." );
        }
        else if ( values[2].Length != 0 )
            throw new FormatException( $"{id}: 이 종류에는 WeaponType을 입력하지 않습니다." );

        Object target = null;
        if ( companion == true || installation == true )
        {
            if ( values[3].StartsWith( "Assets/", StringComparison.Ordinal ) == false )
                throw new FormatException( $"{id}: TargetAssetPath는 Assets/로 시작해야 합니다." );
            target = AssetDatabase.LoadAssetAtPath( values[3], companion == true ? typeof( CompanionData ) : typeof( TrapData ) );
            if ( target == null )
                throw new FormatException( $"{id}: 대상 경로 또는 SO 타입을 확인하세요: {values[3]}" );
        }
        else if ( values[3].Length != 0 )
            throw new FormatException( $"{id}: 이 종류에는 TargetAssetPath를 입력하지 않습니다." );

        Level level = new Level();
        if ( int.TryParse( values[4], NumberStyles.None, CultureInfo.InvariantCulture, out level.Number ) == false
            || level.Number < 1 || int.TryParse( values[5], NumberStyles.None, CultureInfo.InvariantCulture, out level.Price ) == false
            || level.Price <= 0 )
            throw new FormatException( $"{id}: Level과 Price는 1 이상의 정수여야 합니다." );

        for ( int i = 0; i < _effectFields.Length; i++ )
        {
            bool required = IsEffectUsed( values[1], i );
            if ( required == false )
            {
                if ( values[i + 6].Length != 0 )
                    throw new FormatException( $"{id} / Lv{level.Number}: 사용하지 않는 {_headers[i + 6]} 값이 있습니다." );
                continue;
            }
            if ( float.TryParse( values[i + 6], NumberStyles.Float, CultureInfo.InvariantCulture, out float value ) == false
                || float.IsNaN( value ) == true || float.IsInfinity( value ) == true || value < 0f
                || (i == 5 && value >= 1f) )
                throw new FormatException( $"{id} / Lv{level.Number}: {_headers[i + 6]} 값을 확인하세요. 비율은 소수로 입력합니다." );
            level.Effects[i] = value;
        }

        if ( groups.TryGetValue( id, out Entry entry ) == false )
        {
            entry = new Entry { Id = id, Kind = values[1], Weapon = weaponType,
                TargetPath = values[3], Target = target, Line = line };
            groups.Add( id, entry );
            plan.Entries.Add( entry );
        }
        if ( entry.Kind != values[1] || entry.Weapon != weaponType || entry.TargetPath != values[3] )
            throw new FormatException( $"{id}: 같은 ID 안의 종류와 대상은 같아야 합니다." );
        entry.Levels.Add( level );
    }

    /// <summary>
    /// CSV 종류에 대응하는 구체 SO 타입 조회
    /// </summary>
    /// <param name="kind">CSV 종류</param>
    /// <returns>생성 타입</returns>
    static Type GetDataType ( string kind )
    {
        switch ( kind )
        {
            case "PlayerHp": return typeof( PlayerHpUpgradeData );
            case "PlayerMoveSpeed": return typeof( PlayerMoveSpeedUpgradeData );
            case "WeaponDamage": return typeof( WeaponDamageUpgradeData );
            case "WeaponAttackSpeed": return typeof( WeaponAttackSpeedUpgradeData );
            case "CompanionStats": return typeof( CompanionUpgradeData );
            case "InstallationDamage": return typeof( InstallationDamageUpgradeData );
            case "InstallationInterval": return typeof( InstallationIntervalUpgradeData );
            case "InstallationSlow": return typeof( InstallationSlowUpgradeData );
            default: throw new FormatException( $"알 수 없는 Type입니다: {kind}" );
        }
    }

    /// <summary>
    /// 종류가 사용하는 효과 컬럼 확인
    /// </summary>
    /// <param name="kind">CSV 종류</param>
    /// <param name="index">효과 컬럼 인덱스</param>
    /// <returns>필수 효과 여부</returns>
    static bool IsEffectUsed ( string kind, int index )
    {
        switch ( kind )
        {
            case "PlayerHp": return index == 0;
            case "PlayerMoveSpeed": return index == 1;
            case "WeaponDamage":
            case "InstallationDamage": return index == 2;
            case "WeaponAttackSpeed": return index == 3;
            case "CompanionStats": return index == 2 || index == 4;
            case "InstallationInterval": return index == 5;
            case "InstallationSlow": return index == 6;
            default: return false;
        }
    }

    /// <summary>
    /// 기존 에셋 충돌 검사와 임시 후보 생성
    /// </summary>
    /// <param name="folder">신규 출력 폴더</param>
    /// <param name="plan">검사 계획</param>
    static void PrepareAssets ( string folder, Plan plan )
    {
        Dictionary<string, UpgradeData> existing = new( StringComparer.Ordinal );
        foreach ( UpgradeData data in plan.Original )
        {
            if ( data == null || string.IsNullOrWhiteSpace( data.UpgradeId ) == true )
                throw new FormatException( "기존 맵에 비어 있는 항목 또는 ID가 있습니다." );
            if ( existing.ContainsKey( data.UpgradeId ) == true )
                throw new FormatException( $"기존 맵의 ID가 중복됐습니다: {data.UpgradeId}" );
            existing.Add( data.UpgradeId, data );
        }

        HashSet<string> paths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
        foreach ( Entry entry in plan.Entries )
        {
            try
            {
                Type type = GetDataType( entry.Kind );
                entry.AddToMap = existing.TryGetValue( entry.Id, out entry.Existing ) == false;
                entry.AssetPath = entry.Existing != null
                    ? AssetDatabase.GetAssetPath( entry.Existing ) : folder + "/" + entry.Id + ".asset";
                if ( paths.Add( entry.AssetPath ) == false )
                    throw new FormatException( "대소문자를 포함하여 출력 경로가 다른 항목과 충돌합니다." );

                if ( entry.Existing == null )
                {
                    Object occupied = AssetDatabase.LoadMainAssetAtPath( entry.AssetPath );
                    if ( occupied != null )
                    {
                        entry.Existing = occupied as UpgradeData;
                        if ( entry.Existing == null || entry.Existing.UpgradeId != entry.Id )
                            throw new FormatException( $"출력 경로에 다른 에셋이 있습니다: {entry.AssetPath}" );
                    }
                    else if ( File.Exists( entry.AssetPath ) == true || Directory.Exists( entry.AssetPath ) == true
                        || File.Exists( entry.AssetPath + ".meta" ) == true )
                        throw new FormatException( $"출력 경로의 파일 또는 메타 충돌을 확인하세요: {entry.AssetPath}" );
                }
                if ( entry.Existing != null && (entry.Existing.GetType() != type
                    || AssetDatabase.IsMainAsset( entry.Existing ) == false
                    || entry.AssetPath.StartsWith( "Assets/", StringComparison.Ordinal ) == false) )
                    throw new FormatException( "기존 에셋의 구체 타입과 Assets 내 독립 에셋 여부를 확인하세요." );

                entry.Candidate = entry.Existing != null
                    ? Object.Instantiate( entry.Existing ) : (UpgradeData) ScriptableObject.CreateInstance( type );
                entry.Candidate.name = entry.Existing != null ? entry.Existing.name : entry.Id;
                WriteEntry( entry.Candidate, entry );
                if ( entry.Candidate.Validate( out string error ) == false )
                    throw new FormatException( error );
                entry.Changed = entry.Existing == null
                    || EditorJsonUtility.ToJson( entry.Existing ) != EditorJsonUtility.ToJson( entry.Candidate );
                entry.Before = entry.Existing != null ? Describe( entry.Existing ) : "신규";
                entry.After = Describe( entry.Candidate );
            }
            catch ( FormatException exception )
            {
                plan.Errors.Add( $"CSV {entry.Line}행 / {entry.Id}: {exception.Message}" );
            }
        }
    }

    /// <summary>
    /// 임시 또는 실제 SO에 CSV 직렬화 값 설정
    /// </summary>
    /// <param name="data">작성 대상</param>
    /// <param name="entry">검증된 입력</param>
    static void WriteEntry ( UpgradeData data, Entry entry )
    {
        using ( SerializedObject serialized = new SerializedObject( data ) )
        {
            serialized.FindProperty( "_upgradeId" ).stringValue = entry.Id;
            if ( data is WeaponUpgradeData )
                serialized.FindProperty( "_weaponType" ).intValue = (int) entry.Weapon;
            if ( data is CompanionUpgradeData )
                serialized.FindProperty( "_companion" ).objectReferenceValue = entry.Target;
            if ( data is InstallationUpgradeData )
                serialized.FindProperty( "_installation" ).objectReferenceValue = entry.Target;
            SerializedProperty levels = serialized.FindProperty( "_levels" );
            levels.arraySize = entry.Levels.Count;
            for ( int i = 0; i < entry.Levels.Count; i++ )
            {
                SerializedProperty level = levels.GetArrayElementAtIndex( i );
                level.FindPropertyRelative( "_price" ).intValue = entry.Levels[i].Price;
                for ( int j = 0; j < _effectFields.Length; j++ )
                {
                    if ( IsEffectUsed( entry.Kind, j ) == true )
                        level.FindPropertyRelative( _effectFields[j] ).floatValue = entry.Levels[i].Effects[j];
                }
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// 기존 항목을 포함한 최종 맵의 대상 중복과 기존 계약 검사
    /// </summary>
    /// <param name="plan">후보를 준비한 계획</param>
    static void ValidateFinalMap ( Plan plan )
    {
        List<UpgradeData> merged = new List<UpgradeData>( plan.Original );
        foreach ( Entry entry in plan.Entries )
        {
            if ( entry.AddToMap == true )
                merged.Add( entry.Candidate );
            else
                merged[merged.IndexOf( entry.Existing )] = entry.Candidate;
        }
        Dictionary<string, string> targets = new( );
        foreach ( UpgradeData data in merged )
        {
            if ( data.Validate( out string error ) == false )
                throw new FormatException( $"최종 맵 / {data.UpgradeId}: {error}" );
            string key = data.GetType().Name;
            if ( data is WeaponUpgradeData weapon )
                key += ":" + weapon.WeaponType;
            else if ( data is CompanionUpgradeData companion )
                key += ":" + companion.Companion.GetInstanceID();
            else if ( data is InstallationUpgradeData installation )
                key += ":" + installation.Installation.GetInstanceID();
            if ( targets.TryGetValue( key, out string id ) == true )
                throw new FormatException( $"최종 맵의 대상/항목 중복: {id} / {data.UpgradeId}" );
            targets.Add( key, data.UpgradeId );
        }

        UpgradeDataMap temporary = ScriptableObject.CreateInstance<UpgradeDataMap>();
        try
        {
            WriteMap( temporary, merged );
            if ( temporary.Validate() == false )
                throw new FormatException( "최종 강화 맵 검사에 실패했습니다. Console을 확인하세요." );
        }
        finally
        {
            Object.DestroyImmediate( temporary );
        }
    }

    /// <summary>
    /// 맵의 직렬화 참조 배열 설정
    /// </summary>
    /// <param name="map">작성할 맵</param>
    /// <param name="entries">전체 참조</param>
    static void WriteMap ( UpgradeDataMap map, List<UpgradeData> entries )
    {
        using ( SerializedObject serialized = new SerializedObject( map ) )
        {
            SerializedProperty upgrades = serialized.FindProperty( "_upgrades" );
            upgrades.arraySize = entries.Count;
            for ( int i = 0; i < entries.Count; i++ )
                upgrades.GetArrayElementAtIndex( i ).objectReferenceValue = entries[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>
    /// 대상과 단계별 가격 및 효과를 미리보기 문자열로 구성
    /// </summary>
    /// <param name="data">검사할 강화</param>
    /// <returns>종류별 직렬화 값</returns>
    static string Describe ( UpgradeData data )
    {
        StringBuilder result = new StringBuilder( data.GetType().Name );
        using ( SerializedObject serialized = new SerializedObject( data ) )
        {
            if ( data is WeaponUpgradeData weapon )
                result.Append( " / " ).Append( weapon.WeaponType );
            if ( data is CompanionUpgradeData companion )
                result.Append( " / " ).Append( AssetDatabase.GetAssetPath( companion.Companion ) );
            if ( data is InstallationUpgradeData installation )
                result.Append( " / " ).Append( AssetDatabase.GetAssetPath( installation.Installation ) );
            SerializedProperty levels = serialized.FindProperty( "_levels" );
            for ( int i = 0; i < levels.arraySize; i++ )
            {
                SerializedProperty level = levels.GetArrayElementAtIndex( i );
                result.Append( "\nLv" ).Append( i + 1 ).Append( " Price=" ).Append( level.FindPropertyRelative( "_price" ).intValue );
                for ( int j = 0; j < _effectFields.Length; j++ )
                {
                    SerializedProperty effect = level.FindPropertyRelative( _effectFields[j] );
                    if ( effect != null )
                        result.Append( " / " ).Append( _headers[j + 6] ).Append( '=' )
                            .Append( effect.floatValue.ToString( "G9", CultureInfo.InvariantCulture ) );
                }
            }
        }
        return result.ToString();
    }

    /// <summary>
    /// 재검사한 계획의 에셋과 맵 반영 및 대상만 저장
    /// </summary>
    /// <param name="plan">검사 통과한 최신 계획</param>
    /// <param name="touched">실패 시 확인할 반영 시작 경로</param>
    internal static void Apply ( Plan plan, List<string> touched )
    {
        if ( EditorApplication.isPlayingOrWillChangePlaymode == true || plan.Errors.Count != 0 )
            throw new InvalidOperationException( "편집 모드의 검사 통과 계획만 적용할 수 있습니다." );
        List<UpgradeData> merged = new List<UpgradeData>( plan.Original );
        foreach ( Entry entry in plan.Entries )
        {
            UpgradeData asset = entry.Existing;
            if ( asset == null )
            {
                asset = Object.Instantiate( entry.Candidate );
                asset.name = entry.Id;
                try
                {
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
                WriteEntry( asset, entry );
                EditorUtility.SetDirty( asset );
            }
            if ( entry.Existing == null || entry.Changed == true )
                SaveAsset( asset );
            if ( entry.AddToMap == true )
                merged.Add( asset );
        }
        if ( merged.Count != plan.Original.Count )
        {
            touched.Add( AssetDatabase.GetAssetPath( plan.Map ) );
            WriteMap( plan.Map, merged );
            EditorUtility.SetDirty( plan.Map );
            SaveAsset( plan.Map );
        }
    }

    /// <summary>
    /// 변경 대상만 저장하고 미저장 상태 또는 파일 생성 실패 확인
    /// </summary>
    /// <param name="asset">저장할 에셋</param>
    static void SaveAsset ( Object asset )
    {
        AssetDatabase.SaveAssetIfDirty( asset );
        string path = AssetDatabase.GetAssetPath( asset );
        if ( EditorUtility.IsDirty( asset ) == true || File.Exists( path ) == false )
            throw new IOException( $"에셋 저장을 완료하지 못했습니다: {path}" );
    }
}
