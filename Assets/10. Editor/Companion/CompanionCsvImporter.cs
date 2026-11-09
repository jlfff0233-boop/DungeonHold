using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 동료 CSV 임포터 - 기존 동료 설정의 변경 계획 작성과 수치 반영
/// </summary>
public static class CompanionCsvImporter
{
    /// <summary>
    /// 컬럼 정의 - CSV 이름과 대상 에셋의 직렬화 필드 연결
    /// </summary>
    public class Column
    {
        public string Name;    //CSV 헤더
        public string Property;    //직렬화 필드명
        public int Group;    //고용, 동료, 능력치, 공격 순서의 대상 번호
        public SerializedPropertyType Type;    //저장할 값의 종류

        /// <summary>
        /// CSV 컬럼과 기존 필드의 대응 설정
        /// </summary>
        /// <param name="name">CSV 헤더</param>
        /// <param name="property">직렬화 필드명</param>
        /// <param name="group">대상 에셋 번호</param>
        /// <param name="type">값 종류</param>
        public Column ( string name , string property , int group ,
            SerializedPropertyType type = SerializedPropertyType.Float )
        {
            Name = name;
            Property = property;
            Group = group;
            Type = type;
        }
    }

    static Column [ ] _columns =
    {
        new Column ( "MaxHp" , "_maxHp" , 2 ),
        new Column ( "DamageReductionRate" , "_damageReductionRate" , 2 ),
        new Column ( "RawDamage" , "_rawDamage" , 3 ),
        new Column ( "AttackInterval" , "_attackInterval" , 3 ),
        new Column ( "AttackRadius" , "_radius" , 3 ),
        new Column ( "AttackAngle" , "_angle" , 3 ),
        new Column ( "MoveSpeed" , "_moveSpeed" , 1 ),
        new Column ( "RotationSpeed" , "_rotationSpeed" , 1 ),
        new Column ( "FollowDistance" , "_followDistance" , 1 ),
        new Column ( "ArrivalDistance" , "_arrivalDistance" , 1 ),
        new Column ( "ActivityRadius" , "_activityRadius" , 1 ),
        new Column ( "AttackRange" , "_attackRange" , 1 ),
        new Column ( "DetectionMargin" , "_detectionMargin" , 1 ),
        new Column ( "DefendRadius" , "_defendRadius" , 1 ),
        new Column ( "SearchInterval" , "_searchInterval" , 1 ),
        new Column ( "CanRetreat" , "_canRetreat" , 1 , SerializedPropertyType.Boolean ),
        new Column ( "RetreatStartDistance" , "_retreatStartDistance" , 1 ),
        new Column ( "RetreatDistance" , "_retreatDistance" , 1 ),
        new Column ( "RetreatCandidateCount" , "_retreatCandidateCount" , 1 , SerializedPropertyType.Integer ),
        new Column ( "RetreatSampleDistance" , "_retreatSampleDistance" , 1 ),
        new Column ( "HireCost" , "_hireCost" , 0 , SerializedPropertyType.Integer ),
        new Column ( "RehireDelay" , "_rehireDelay" , 0 )
    };    //관리 대상 수치와 SO 필드의 대응

    /// <summary>
    /// 변경 항목 - 기존 에셋의 필드별 이전 값과 입력 값
    /// </summary>
    public class Change
    {
        public Object Target;    //기존 에셋
        public Column Column;    //필드 대응
        public object Before;    //현재 직렬화 값
        public object After;    //CSV 입력 값
        public string Label;    //원본 행과 동료 타입

        /// <summary>
        /// 실제 값 변경 여부
        /// </summary>
        public bool Changed => Equals ( Before , After ) == false;
    }

    /// <summary>
    /// 임포트 계획 - 변경 미리보기와 적용을 막는 입력 오류 보관
    /// </summary>
    public class Plan
    {
        public List<Change> Changes = new List<Change> ( );    //중복 에셋 필드는 한 번만 보관
        public List<string> Errors = new List<string> ( );    //원본 위치를 포함한 오류
        public List<string> Notices = new List<string> ( );    //미저장 에셋 안내
        public string Snapshot;    //CSV와 연결 대상 및 기존 값의 비교 기록
        public int EntryCount;    //입력 동료 수
    }

    /// <summary>
    /// CSV 행 - 원본 시작 줄과 셀 보관
    /// </summary>
    class CsvRow
    {
        public int Line;    //원본 시작 줄
        public string [ ] Cells;    //헤더 순서의 입력
    }

    /// <summary>
    /// CSV 전체와 기존 연결을 읽어 에셋 변경 없이 미리보기 준비
    /// </summary>
    /// <param name="csvPath">UTF-8 CSV 경로</param>
    /// <param name="map">기존 동료 고용 데이터 맵</param>
    /// <returns>변경 항목과 입력 오류</returns>
    public static Plan Prepare ( string csvPath , CompanionHireDataMap map )
    {
        Plan plan = new Plan ( );
        if ( EditorApplication.isPlayingOrWillChangePlaymode == true )
        {
            plan.Errors.Add ( "편집 모드에서 검사/적용하세요." );
            return plan;
        }
        if ( map == null || AssetDatabase.Contains ( map ) == false )
        {
            plan.Errors.Add ( "저장된 동료 고용 데이터 맵을 선택하세요." );
            return plan;
        }
        string text;
        try
        {
            text = File.ReadAllText ( csvPath , new UTF8Encoding ( false , true ) );
        }
        catch ( Exception exception ) when ( exception is IOException
            || exception is UnauthorizedAccessException || exception is ArgumentException )
        {
            plan.Errors.Add ( $"CSV 읽기 실패: {exception.Message}" );
            return plan;
        }

        try
        {
            List<CsvRow> rows = ParseCsv ( text );
            if ( rows.Count < 2 )
                throw new FormatException ( "헤더와 동료 데이터가 필요합니다." );

            Dictionary<string , int> headers = new Dictionary<string , int> ( StringComparer.OrdinalIgnoreCase );
            for ( int i = 0 ; i < rows [ 0 ].Cells.Length ; i++ )
            {
                string header = rows [ 0 ].Cells [ i ];
                if ( headers.ContainsKey ( header ) == true )
                    throw new FormatException ( $"중복 헤더: {header}" );
                headers.Add ( header , i );
            }
            if ( headers.ContainsKey ( "WeaponType" ) == false )
                throw new FormatException ( "필수 헤더 누락: WeaponType" );
            foreach ( Column column in _columns )
            {
                if ( headers.ContainsKey ( column.Name ) == false )
                    throw new FormatException ( $"필수 헤더 누락: {column.Name}" );
            }

            HashSet<WeaponType> types = new HashSet<WeaponType> ( );
            Dictionary<string , Change> fields = new Dictionary<string , Change> ( );
            HashSet<Object> noticed = new HashSet<Object> ( );
            StringBuilder snapshot = new StringBuilder ( text );
            snapshot.Append ( '\n' ).Append ( map.GetInstanceID ( ) );
            for ( int i = 1 ; i < rows.Count ; i++ )
            {
                CsvRow row = rows [ i ];
                try
                {
                    ReadEntry ( row , headers , map , types , fields , noticed , snapshot , plan );
                }
                catch ( FormatException exception )
                {
                    plan.Errors.Add ( $"CSV {row.Line}행: {exception.Message}" );
                }
            }
            plan.Snapshot = snapshot.ToString ( );
        }
        catch ( FormatException exception )
        {
            plan.Errors.Add ( exception.Message );
        }
        return plan;
    }

    /// <summary>
    /// 한 타입의 입력을 해석하고 기존 에셋의 변경 목록 구성
    /// </summary>
    /// <param name="row">입력 행</param>
    /// <param name="headers">헤더별 위치</param>
    /// <param name="map">기존 고용 설정 맵</param>
    /// <param name="types">이미 읽은 타입</param>
    /// <param name="fields">에셋 필드별 입력</param>
    /// <param name="noticed">미저장 안내를 완료한 에셋</param>
    /// <param name="snapshot">입력과 기존 값 비교 기록</param>
    /// <param name="plan">검사 결과</param>
    static void ReadEntry ( CsvRow row , Dictionary<string , int> headers ,
        CompanionHireDataMap map , HashSet<WeaponType> types , Dictionary<string , Change> fields ,
        HashSet<Object> noticed , StringBuilder snapshot , Plan plan )
    {
        if ( row.Cells.Length != headers.Count )
            throw new FormatException ( $"컬럼 수가 헤더의 {headers.Count}개와 다릅니다." );
        string typeName = row.Cells [ headers [ "WeaponType" ] ];
        if ( Enum.TryParse ( typeName , true , out WeaponType type ) == false
            || Enum.IsDefined ( typeof ( WeaponType ) , type ) == false )
            throw new FormatException ( $"알 수 없는 WeaponType: {typeName}" );
        if ( types.Add ( type ) == false )
            throw new FormatException ( $"동료 타입 중복: {type}" );
        if ( map.TryGet ( type , out CompanionHireData hire ) == false )
            throw new FormatException ( $"{type}의 고용 설정을 맵에서 찾을 수 없습니다. 중복/미연결 항목을 확인하세요." );

        CompanionData data = hire.CompanionData;
        Object [ ] targets = { hire , data , data.CombatStatsData , data.AttackData };
        foreach ( Object target in targets )
        {
            if ( target == null || AssetDatabase.Contains ( target ) == false )
                throw new FormatException ( $"{type}의 고용/동료/능력치/공격 SO 연결을 확인하세요." );
            snapshot.Append ( '\n' ).Append ( target.GetInstanceID ( ) );
            if ( EditorUtility.IsDirty ( target ) == true && noticed.Add ( target ) == true )
                plan.Notices.Add ( $"{AssetDatabase.GetAssetPath ( target )}: 적용 시 기존 미저장 수정도 함께 저장됩니다." );
        }

        bool retreat = ReadBoolean ( row.Cells [ headers [ "CanRetreat" ] ] , "CanRetreat" );
        Dictionary<string , object> values = new Dictionary<string , object> ( );
        foreach ( Column column in _columns )
        {
            string cell = row.Cells [ headers [ column.Name ] ];
            //후퇴를 쓰지 않는 타입은 사용하지 않는 숫자 칸을 비워도 됨
            if ( retreat == false && column.Name.StartsWith ( "Retreat" , StringComparison.Ordinal )
                && cell.Length == 0 )
                cell = "0";
            object value = column.Type == SerializedPropertyType.Boolean
                ? (object) retreat : ReadValue ( cell , column );
            values.Add ( column.Name , value );
        }
        ValidateValues ( values , retreat );

        //모든 입력값을 확인한 후 기존 필드와 비교, 공유 에셋의 충돌은 적용 전에 발견
        foreach ( Column column in _columns )
        {
            Object target = targets [ column.Group ];
            using ( SerializedObject serialized = new SerializedObject ( target ) )
            {
                SerializedProperty property = serialized.FindProperty ( column.Property );
                if ( property == null || property.propertyType != column.Type )
                    throw new FormatException ( $"{target.name}: 필드 {column.Property}의 선언을 확인하세요." );
                object before = ReadProperty ( property );
                object after = values [ column.Name ];
                snapshot.Append ( '\n' ).Append ( column.Name ).Append ( '=' ).Append ( FormatValue ( before ) );
                string key = target.GetInstanceID ( ) + ":" + column.Property;
                if ( fields.TryGetValue ( key , out Change existing ) == true )
                {
                    if ( Equals ( existing.After , after ) == false )
                        throw new FormatException ( $"{existing.Label}와 {type}이 공유하는 {target.name}/{column.Name} 값이 다릅니다." );
                    continue;
                }
                Change change = new Change
                {
                    Target = target , Column = column , Before = before , After = after ,
                    Label = $"{type} ({row.Line}행)"
                };
                fields.Add ( key , change );
                plan.Changes.Add ( change );
            }
        }
        plan.EntryCount++;
    }

    /// <summary>
    /// 실제 동작에 필요한 범위만 검사하고 기획 수치는 강제하지 않음
    /// </summary>
    /// <param name="values">컬럼별 해석된 값</param>
    /// <param name="retreat">후퇴 사용 여부</param>
    static void ValidateValues ( Dictionary<string , object> values , bool retreat )
    {
        foreach ( KeyValuePair<string , object> pair in values )
        {
            if ( pair.Value is bool )
                continue;
            if ( retreat == false && pair.Key.StartsWith ( "Retreat" , StringComparison.Ordinal ) )
                continue;
            double value = Convert.ToDouble ( pair.Value , CultureInfo.InvariantCulture );
            if ( value < 0d )
                throw new FormatException ( $"{pair.Key}: 음수는 사용할 수 없습니다." );
        }
        foreach ( string name in new [ ] { "MaxHp" , "MoveSpeed" ,
            "RotationSpeed" , "ActivityRadius" , "AttackRange" , "DefendRadius" } )
        {
            if ( (float) values [ name ] <= 0f )
                throw new FormatException ( $"{name}: 0보다 큰 값이 필요합니다." );
        }
        if ( (float) values [ "DamageReductionRate" ] > 1f )
            throw new FormatException ( "DamageReductionRate: 0~1 비율로 입력하세요. 0.2는 20%입니다." );
        if ( (float) values [ "AttackAngle" ] > 360f )
            throw new FormatException ( "AttackAngle: 0~360도로 입력하세요." );
        if ( retreat == true && ( (float) values [ "RetreatStartDistance" ] <= 0f
            || (float) values [ "RetreatDistance" ] <= (float) values [ "RetreatStartDistance" ]
            || (int) values [ "RetreatCandidateCount" ] <= 0
            || (float) values [ "RetreatSampleDistance" ] <= 0f ) )
            throw new FormatException ( "후퇴 사용 시 시작 거리/후보 수/보정 거리는 양수, 확보 거리는 시작 거리보다 커야 합니다." );
    }

    /// <summary>
    /// 숫자 컬럼을 직렬화 형식으로 해석
    /// </summary>
    /// <param name="cell">입력 셀</param>
    /// <param name="column">컬럼 정의</param>
    /// <returns>직렬화 형식의 값</returns>
    static object ReadValue ( string cell , Column column )
    {
        if ( column.Type == SerializedPropertyType.Integer )
        {
            if ( int.TryParse ( cell , NumberStyles.Integer , CultureInfo.InvariantCulture , out int integer ) == true )
                return integer;
            throw new FormatException ( $"{column.Name}: 정수 입력이 필요합니다: {cell}" );
        }
        if ( float.TryParse ( cell , NumberStyles.Float , CultureInfo.InvariantCulture , out float number ) == false
            || float.IsNaN ( number ) == true || float.IsInfinity ( number ) == true )
            throw new FormatException ( $"{column.Name}: 유한한 숫자 입력이 필요합니다: {cell}" );
        return number;
    }

    /// <summary>
    /// true/false 또는 1/0 입력 해석
    /// </summary>
    /// <param name="cell">입력 셀</param>
    /// <param name="name">오류 표시용 컬럼명</param>
    /// <returns>해석된 논리값</returns>
    static bool ReadBoolean ( string cell , string name )
    {
        if ( bool.TryParse ( cell , out bool value ) == true )
            return value;
        if ( cell == "1" || cell == "0" )
            return cell == "1";
        throw new FormatException ( $"{name}: true/false 또는 1/0으로 입력하세요: {cell}" );
    }

    /// <summary>
    /// 관리 대상 직렬화 값 조회
    /// </summary>
    /// <param name="property">형식을 확인한 필드</param>
    /// <returns>비교할 현재 값</returns>
    static object ReadProperty ( SerializedProperty property )
    {
        if ( property.propertyType == SerializedPropertyType.Boolean )
            return property.boolValue;
        if ( property.propertyType == SerializedPropertyType.Integer )
            return property.intValue;
        return property.floatValue;
    }

    /// <summary>
    /// 미리보기와 비교 기록의 숫자 표기 통일
    /// </summary>
    /// <param name="value">표시할 값</param>
    /// <returns>문화권에 영향을 받지 않는 표기</returns>
    public static string FormatValue ( object value )
    {
        return value is float number ? number.ToString ( "R" , CultureInfo.InvariantCulture )
            : Convert.ToString ( value , CultureInfo.InvariantCulture );
    }

    /// <summary>
    /// 검사된 변경 항목만 반영하고 해당 에셋만 저장
    /// </summary>
    /// <param name="plan">적용 직전 재확인된 계획</param>
    /// <param name="touched">수정 시도한 에셋 경로, 저장 실패 보고에도 사용</param>
    public static void Apply ( Plan plan , List<string> touched )
    {
        Dictionary<Object , List<Change>> targets = new Dictionary<Object , List<Change>> ( );
        foreach ( Change change in plan.Changes )
        {
            //이전 저장 실패로 값만 반영된 에셋도 재시도 대상에 포함
            if ( change.Changed == false && EditorUtility.IsDirty ( change.Target ) == false )
                continue;
            if ( targets.TryGetValue ( change.Target , out List<Change> changes ) == false )
            {
                changes = new List<Change> ( );
                targets.Add ( change.Target , changes );
            }
            if ( change.Changed == true )
                changes.Add ( change );
        }

        Undo.IncrementCurrentGroup ( );
        int undoGroup = Undo.GetCurrentGroup ( );
        Undo.SetCurrentGroupName ( "동료 CSV 적용" );
        foreach ( KeyValuePair<Object , List<Change>> pair in targets )
        {
            if ( pair.Value.Count > 0 )
                Undo.RecordObject ( pair.Key , "동료 CSV 적용" );
            touched.Add ( AssetDatabase.GetAssetPath ( pair.Key ) );
            using ( SerializedObject serialized = new SerializedObject ( pair.Key ) )
            {
                foreach ( Change change in pair.Value )
                {
                    SerializedProperty property = serialized.FindProperty ( change.Column.Property );
                    if ( change.Column.Type == SerializedPropertyType.Boolean )
                        property.boolValue = (bool) change.After;
                    else if ( change.Column.Type == SerializedPropertyType.Integer )
                        property.intValue = (int) change.After;
                    else
                        property.floatValue = (float) change.After;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo ( );
            }
            EditorUtility.SetDirty ( pair.Key );
        }
        Undo.CollapseUndoOperations ( undoGroup );
        //전체 메모리 반영 후 수정한 에셋만 저장, 프로젝트의 다른 미저장 에셋은 제외
        foreach ( Object target in targets.Keys )
        {
            AssetDatabase.SaveAssetIfDirty ( target );
            //Unity가 예외 대신 오류 로그만 남긴 경우에도 저장 성공으로 보고하지 않음
            if ( EditorUtility.IsDirty ( target ) == true )
                throw new IOException ( $"에셋 저장을 완료하지 못했습니다: {AssetDatabase.GetAssetPath ( target )}" );
        }
    }

    /// <summary>
    /// BOM, 따옴표, 쉼표와 줄바꿈을 처리하여 원본 행 해석
    /// </summary>
    /// <param name="text">CSV 본문</param>
    /// <returns>빈 줄을 제외한 행 목록</returns>
    static List<CsvRow> ParseCsv ( string text )
    {
        List<CsvRow> rows = new List<CsvRow> ( );
        List<string> cells = new List<string> ( );
        StringBuilder cell = new StringBuilder ( );
        bool quoted = false;
        bool closed = false;
        int line = 1;
        int startLine = 1;
        text = text.TrimStart ( '\uFEFF' );
        for ( int i = 0 ; i <= text.Length ; i++ )
        {
            bool end = i == text.Length;
            char character = end == true ? '\n' : text [ i ];
            if ( quoted == true )
            {
                if ( end == true )
                    throw new FormatException ( $"CSV {startLine}행: 닫히지 않은 따옴표입니다." );
                if ( character == '"' )
                {
                    if ( i + 1 < text.Length && text [ i + 1 ] == '"' )
                    {
                        cell.Append ( '"' );
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
                    cell.Append ( character );
                    if ( character == '\n' || ( character == '\r'
                        && ( i + 1 == text.Length || text [ i + 1 ] != '\n' ) ) )
                        line++;
                }
                continue;
            }
            if ( character == ',' || character == '\r' || character == '\n' )
            {
                cells.Add ( cell.ToString ( ).Trim ( ) );
                cell.Clear ( );
                closed = false;
                if ( character == ',' )
                    continue;
                if ( cells.Count != 1 || cells [ 0 ].Length != 0 )
                    rows.Add ( new CsvRow { Line = startLine , Cells = cells.ToArray ( ) } );
                cells.Clear ( );
                if ( character == '\r' && i + 1 < text.Length && text [ i + 1 ] == '\n' )
                    i++;
                line++;
                startLine = line;
            }
            else if ( closed == true && char.IsWhiteSpace ( character ) == true )
                continue;
            else if ( character == '"' && cell.ToString ( ).Trim ( ).Length == 0 && closed == false )
            {
                cell.Clear ( );
                quoted = true;
            }
            else
            {
                if ( closed == true || character == '"' )
                    throw new FormatException ( $"CSV {line}행: 따옴표 위치가 잘못됐습니다." );
                cell.Append ( character );
            }
        }
        return rows;
    }
}
