using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 튜토리얼 CSV 임포터 - 단계 원본 갱신과 데이터맵 순서 반영
/// </summary>
static class TutorialCsvImporter
{
    /// <summary>
    /// 단계 변경 - 입력 값과 저장할 SO
    /// </summary>
    public class Step
    {
        public string Path;    //기존 또는 신규 SO 경로
        public TutorialData Existing;    //같은 파일명의 기존 단계
        public string TargetName;    //강조 대상 이름
        public string Description;    //단계 문구
        public bool HasChanges;    //단계 파일을 저장할지 여부
    }

    /// <summary>
    /// 임포트 계획 - 검사 결과와 최종 단계 순서
    /// </summary>
    public class Plan
    {
        public string Source;    //검사 시 읽은 CSV 원문
        public List<Step> Steps = new List<Step> ( );    //최종 데이터맵 순서
        public List<string> Preview = new List<string> ( );    //생성/갱신/연결 해제 내역
        public List<string> Errors = new List<string> ( );    //적용을 막는 오류
        public string Notice;    //씬 대상 검사 범위 안내
        public bool HasMapChanges;    //순서 또는 구성 변경 여부
    }

    /// <summary>
    /// CSV와 기존 SO를 대조해 쓰기 없는 변경 계획 작성
    /// </summary>
    /// <param name="csvPath">입력 CSV 경로</param>
    /// <param name="map">반영할 데이터맵</param>
    /// <returns>검사와 미리보기 결과</returns>
    public static Plan Prepare ( string csvPath , TutorialStepDataMap map )
    {
        Plan plan = new Plan ( );
        string mapPath = map == null ? string.Empty : AssetDatabase.GetAssetPath ( map );
        if ( string.IsNullOrEmpty ( mapPath ) == true || mapPath.StartsWith ( "Assets/" , StringComparison.Ordinal ) == false
            || AssetDatabase.IsMainAsset ( map ) == false )
        {
            plan.Errors.Add ( "Assets 아래에 저장된 독립 튜토리얼 데이터맵을 선택하세요." );
            return plan;
        }

        List<TutorialCsvFormat.Row> rows;
        try
        {
            plan.Source = TutorialCsvFormat.Read ( csvPath );
            rows = TutorialCsvFormat.Parse ( plan.Source );
        }
        catch ( Exception exception ) when ( exception is IOException || exception is UnauthorizedAccessException
            || exception is ArgumentException || exception is FormatException || exception is NotSupportedException )
        {
            plan.Errors.Add ( "CSV 읽기 또는 형식 오류: " + exception.Message );
            return plan;
        }

        if ( rows.Count < 2 )
        {
            plan.Errors.Add ( "지정된 헤더와 최소 한 개의 단계가 필요합니다." );
            return plan;
        }

        string [ ] header = rows [ 0 ].Cells;
        bool isLegacy = header.SequenceEqual ( TutorialCsvFormat.LegacyHeader.Split ( ',' ) );
        if ( isLegacy == false && header.SequenceEqual ( TutorialCsvFormat.Header.Split ( ',' ) ) == false )
        {
            plan.Errors.Add ( "지원하는 CSV 헤더를 확인하세요: " + TutorialCsvFormat.Header );
            return plan;
        }

        //이미 맵에 연결된 단계는 다른 폴더에 있어도 원래 SO를 재사용
        Dictionary<string, TutorialData> current = new Dictionary<string, TutorialData> ( StringComparer.OrdinalIgnoreCase );
        for ( int i = 0; i < map.StepCount; i++ )
        {
            TutorialData step = map.GetStep ( i );
            if ( step == null )
                continue;

            string name = Path.GetFileNameWithoutExtension ( AssetDatabase.GetAssetPath ( step ) );
            if ( current.TryGetValue ( name , out TutorialData previous ) == true && previous != step )
                plan.Errors.Add ( $"데이터맵에 같은 파일명의 단계가 있습니다: {name}" );
            else
                current [ name ] = step;
        }

        string folder = mapPath.Substring ( 0 , mapPath.LastIndexOf ( '/' ) );
        HashSet<string> names = new HashSet<string> ( StringComparer.OrdinalIgnoreCase );
        for ( int i = 1; i < rows.Count; i++ )
            PrepareStep ( rows [ i ] , header.Length , folder , names , current , plan );

        if ( plan.Errors.Count > 0 )
            return plan;

        //CSV의 순서가 최종 순서이며 빠진 항목은 참조만 해제
        plan.HasMapChanges = map.StepCount != plan.Steps.Count || EditorUtility.IsDirty ( map ) == true;
        for ( int i = 0; i < plan.Steps.Count; i++ )
        {
            Step step = plan.Steps [ i ];
            string action = step.Existing == null ? "생성" : step.HasChanges == true ? "갱신" : "유지";
            string before = step.Existing == null ? string.Empty
                : $"\n현재: {step.Existing.TargetId} / {step.Existing.Description}";
            plan.Preview.Add ( $"{i + 1}. {action} / {step.Path}{before}\n반영: {step.TargetName} / {step.Description}" );
            if ( i >= map.StepCount || step.Existing == null || map.GetStep ( i ) != step.Existing )
                plan.HasMapChanges = true;
        }

        for ( int i = 0; i < map.StepCount; i++ )
        {
            TutorialData previous = map.GetStep ( i );
            string path = previous == null ? "비어 있는 참조" : AssetDatabase.GetAssetPath ( previous );
            if ( previous == null || plan.Steps.Any ( step => step.Existing == previous ) == false )
                plan.Preview.Add ( $"연결 해제 / 기존 {i + 1}단계 / {path}" );
            else if ( plan.HasMapChanges == true )
                plan.Preview.Add ( $"기존 {i + 1}단계 / {path}" );
        }

        if ( plan.HasMapChanges == true && AssetDatabase.IsOpenForEdit ( mapPath ) == false )
            plan.Errors.Add ( "데이터맵을 편집할 수 없습니다: " + mapPath );

        CheckSceneTargets ( map , plan );
        if ( isLegacy == true )
            plan.Notice += "\n이전 형식의 여백 열은 적용하지 않습니다.";

        return plan;
    }

    /// <summary>
    /// 한 행을 검사해 생성 또는 갱신 대상 확정
    /// </summary>
    /// <param name="row">CSV 행</param>
    /// <param name="columnCount">헤더에 따른 열 개수</param>
    /// <param name="folder">신규 단계 출력 폴더</param>
    /// <param name="names">이번 CSV에서 사용한 파일명</param>
    /// <param name="current">맵에 연결된 기존 단계</param>
    /// <param name="plan">변경과 오류를 기록할 계획</param>
    static void PrepareStep ( TutorialCsvFormat.Row row , int columnCount , string folder , HashSet<string> names ,
        Dictionary<string, TutorialData> current , Plan plan )
    {
        string [ ] cells = row.Cells;
        if ( cells.Length != columnCount )
        {
            plan.Errors.Add ( $"CSV {row.Line}행: 열이 {columnCount}개여야 합니다." );
            return;
        }

        string name = cells [ 0 ];
        if ( IsValidAssetName ( name ) == false || names.Add ( name ) == false )
        {
            plan.Errors.Add ( $"CSV {row.Line}행: 잘못됐거나 중복된 단계 파일명입니다: {name}" );
            return;
        }

        string targetName = cells [ 1 ];
        if ( string.IsNullOrWhiteSpace ( targetName ) == true )
        {
            plan.Errors.Add ( $"CSV {row.Line}행: 대상 이름을 입력하세요." );
            return;
        }

        string path = folder + "/" + name + ".asset";
        UnityEngine.Object output = AssetDatabase.LoadMainAssetAtPath ( path );
        current.TryGetValue ( name , out TutorialData existing );
        if ( output != null && ( output is TutorialData == false || ( existing != null && output != existing ) ) )
        {
            plan.Errors.Add ( $"CSV {row.Line}행: 출력 경로에 다른 에셋이 있습니다: {path}" );
            return;
        }

        if ( output == null && ( File.Exists ( path ) || Directory.Exists ( path ) || File.Exists ( path + ".meta" ) ) )
        {
            plan.Errors.Add ( $"CSV {row.Line}행: 출력 경로에 기존 파일 또는 메타가 있습니다: {path}" );
            return;
        }

        if ( existing == null )
            existing = output as TutorialData;
        if ( existing != null )
        {
            path = AssetDatabase.GetAssetPath ( existing );
            if ( path.StartsWith ( "Assets/" , StringComparison.Ordinal ) == false || AssetDatabase.IsMainAsset ( existing ) == false )
            {
                plan.Errors.Add ( $"CSV {row.Line}행: Assets 아래의 독립 단계 SO가 필요합니다: {path}" );
                return;
            }
        }

        bool hasChanges = existing == null || existing.TargetId != targetName
            || ( existing.Description ?? string.Empty ) != cells [ 2 ]
            || EditorUtility.IsDirty ( existing ) == true;
        if ( hasChanges == true && existing != null && AssetDatabase.IsOpenForEdit ( path ) == false )
        {
            plan.Errors.Add ( $"CSV {row.Line}행: 단계 SO를 편집할 수 없습니다: {path}" );
            return;
        }

        plan.Steps.Add ( new Step
        {
            Path = path , Existing = existing , TargetName = targetName ,
            Description = cells [ 2 ] , HasChanges = hasChanges
        } );
    }

    /// <summary>
    /// 경로가 아닌 단일 에셋 파일명인지 확인
    /// </summary>
    /// <param name="name">확장자를 제외한 이름</param>
    /// <returns>사용 가능한 이름 여부</returns>
    static bool IsValidAssetName ( string name )
    {
        return string.IsNullOrWhiteSpace ( name ) == false && name == name.Trim ( )
            && name.EndsWith ( "." , StringComparison.Ordinal ) == false
            && name.EndsWith ( ".asset" , StringComparison.OrdinalIgnoreCase ) == false
            && name.IndexOfAny ( Path.GetInvalidFileNameChars ( ) ) < 0
            && name.Contains ( "/" ) == false && name.Contains ( "\\" ) == false && name.Contains ( ":" ) == false
            && name.Contains ( "<" ) == false && name.Contains ( ">" ) == false
            && name.Contains ( "\"" ) == false && name.Contains ( "|" ) == false
            && name.Contains ( "?" ) == false && name.Contains ( "*" ) == false
            && System.Text.RegularExpressions.Regex.IsMatch ( name , @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)" ,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase ) == false;
    }

    /// <summary>
    /// 열린 씬에서 같은 데이터맵을 쓰는 컨트롤러의 대상 확인
    /// </summary>
    /// <param name="map">반영할 데이터맵</param>
    /// <param name="plan">단계 계획과 검사 결과</param>
    static void CheckSceneTargets ( TutorialStepDataMap map , Plan plan )
    {
        bool found = false;
        for ( int i = 0; i < SceneManager.sceneCount; i++ )
        {
            Scene scene = SceneManager.GetSceneAt ( i );
            if ( scene.isLoaded == false )
                continue;

            foreach ( GameObject root in scene.GetRootGameObjects ( ) )
            {
                foreach ( TutorialController controller in root.GetComponentsInChildren<TutorialController> ( true ) )
                {
                    SerializedObject connection = new SerializedObject ( controller );
                    if ( connection.FindProperty ( "_data" ).objectReferenceValue != map )
                        continue;

                    found = true;
                    CheckControllerTargets ( connection.FindProperty ( "_targets" ) ,
                        scene.name + "/" + controller.name , plan );
                }
            }
        }

        plan.Notice = found == true
            ? "열린 씬에서 이 데이터맵을 쓰는 컨트롤러의 Targets를 검사했습니다."
            : "열린 씬에서 이 데이터맵을 쓰는 컨트롤러를 찾지 못해 대상 이름 검사를 건너뛰었습니다.";
    }

    /// <summary>
    /// 컨트롤러 한 개의 등록 이름과 CSV의 대상 이름 대조
    /// </summary>
    /// <param name="bindings">등록된 Targets</param>
    /// <param name="owner">씬과 컨트롤러 표시 이름</param>
    /// <param name="plan">단계 계획과 검사 결과</param>
    static void CheckControllerTargets ( SerializedProperty bindings , string owner , Plan plan )
    {
        HashSet<string> names = new HashSet<string> ( StringComparer.Ordinal );
        for ( int i = 0; i < bindings.arraySize; i++ )
        {
            RectTransform rect = bindings.GetArrayElementAtIndex ( i )
                .FindPropertyRelative ( "_target" ).objectReferenceValue as RectTransform;
            if ( rect == null )
                plan.Errors.Add ( $"{owner}: Targets의 {i + 1}번째 참조가 비어 있습니다." );
            else if ( names.Add ( rect.name ) == false )
                plan.Errors.Add ( $"{owner}: 중복 대상 이름 {rect.name}" );
        }

        foreach ( string name in plan.Steps.Select ( step => step.TargetName ).Distinct ( StringComparer.Ordinal ) )
        {
            if ( names.Contains ( name ) == false )
                plan.Errors.Add ( $"{owner}: 등록된 대상 이름을 찾을 수 없습니다: {name}" );
        }
    }

    /// <summary>
    /// 검사 결과가 유지될 때 단계 저장 후 데이터맵 순서 확정
    /// </summary>
    /// <param name="csvPath">입력 CSV 경로</param>
    /// <param name="map">반영할 데이터맵</param>
    /// <param name="preview">사용자가 확인한 계획</param>
    /// <param name="message">저장 또는 중단 결과</param>
    /// <returns>적용 성공 여부</returns>
    public static bool TryApply ( string csvPath , TutorialStepDataMap map , Plan preview , out string message )
    {
        Plan latest = Prepare ( csvPath , map );
        if ( latest.Errors.Count > 0 || latest.Source != preview.Source || latest.Notice != preview.Notice
            || latest.Preview.SequenceEqual ( preview.Preview ) == false
            || latest.Steps.Select ( step => step.Existing ).SequenceEqual ( preview.Steps.Select ( step => step.Existing ) ) == false )
        {
            message = "검사 이후 입력이나 대상 상태가 바뀌었습니다. 다시 검사하세요.";
            if ( latest.Errors.Count > 0 )
                message += "\n" + string.Join ( "\n" , latest.Errors );
            return false;
        }

        List<TutorialData> steps = new List<TutorialData> ( );
        List<string> attempted = new List<string> ( );
        try
        {
            foreach ( Step step in latest.Steps )
            {
                TutorialData asset = step.Existing;
                if ( step.HasChanges == true )
                {
                    attempted.Add ( step.Path );
                    if ( asset == null )
                    {
                        asset = ScriptableObject.CreateInstance<TutorialData> ( );
                        WriteStep ( asset , step );
                        AssetDatabase.CreateAsset ( asset , step.Path );
                        if ( AssetDatabase.LoadMainAssetAtPath ( step.Path ) != asset )
                            throw new IOException ( "단계 SO를 생성하지 못했습니다: " + step.Path );
                    }
                    else
                        WriteStep ( asset , step );

                    Save ( asset );
                }
                steps.Add ( asset );
            }

            //단계 저장을 끝낸 뒤에만 맵 참조를 바꿔 실패 중간에 순서가 바뀌지 않게 함
            if ( latest.HasMapChanges == true )
            {
                attempted.Add ( AssetDatabase.GetAssetPath ( map ) );
                SerializedObject serialized = new SerializedObject ( map );
                SerializedProperty order = serialized.FindProperty ( "_steps" );
                order.arraySize = steps.Count;
                for ( int i = 0; i < steps.Count; i++ )
                    order.GetArrayElementAtIndex ( i ).objectReferenceValue = steps [ i ];
                serialized.ApplyModifiedProperties ( );
                Save ( map );
            }
        }
        catch ( Exception exception ) when ( exception is IOException || exception is UnauthorizedAccessException
            || exception is UnityException || exception is InvalidOperationException || exception is ArgumentException )
        {
            message = "저장을 중단했습니다. 아래 시도 경로에는 일부 변경이 반영됐을 수 있습니다.\n"
                + exception.Message + "\n" + string.Join ( "\n" , attempted );
            return false;
        }

        message = $"최종 단계 {steps.Count}개 / 저장한 에셋 {attempted.Count}개";
        return true;
    }

    /// <summary>
    /// 기존 직렬화 필드에 단계 내용 반영
    /// </summary>
    /// <param name="asset">편집할 단계 SO</param>
    /// <param name="step">검사한 CSV 단계</param>
    static void WriteStep ( TutorialData asset , Step step )
    {
        SerializedObject serialized = new SerializedObject ( asset );
        serialized.FindProperty ( "_targetId" ).stringValue = step.TargetName;
        serialized.FindProperty ( "_description" ).stringValue = step.Description;
        serialized.ApplyModifiedProperties ( );
    }

    /// <summary>
    /// 변경 에셋만 저장하고 저장 실패 확인
    /// </summary>
    /// <param name="asset">저장할 에셋</param>
    static void Save ( UnityEngine.Object asset )
    {
        AssetDatabase.SaveAssetIfDirty ( asset );
        if ( EditorUtility.IsDirty ( asset ) == true )
            throw new IOException ( "에셋 변경을 저장하지 못했습니다: " + AssetDatabase.GetAssetPath ( asset ) );
    }

    /// <summary>
    /// 현재 데이터맵 순서와 문구를 CSV로 내보내기
    /// </summary>
    /// <param name="map">내보낼 데이터맵</param>
    /// <param name="path">출력 CSV 경로</param>
    public static void Export ( TutorialStepDataMap map , string path )
    {
        if ( string.Equals ( Path.GetExtension ( path ) , ".csv" , StringComparison.OrdinalIgnoreCase ) == false )
            throw new FormatException ( "출력 파일 확장자는 .csv여야 합니다." );

        if ( map.StepCount == 0 )
            throw new FormatException ( "내보낼 단계가 없습니다." );

        StringBuilder csv = new StringBuilder ( );
        csv.AppendLine ( TutorialCsvFormat.Header );
        HashSet<string> names = new HashSet<string> ( StringComparer.OrdinalIgnoreCase );
        for ( int i = 0; i < map.StepCount; i++ )
        {
            TutorialData step = map.GetStep ( i );
            if ( step == null )
                throw new FormatException ( $"데이터맵의 {i + 1}단계가 비어 있습니다." );

            string name = Path.GetFileNameWithoutExtension ( AssetDatabase.GetAssetPath ( step ) );
            if ( IsValidAssetName ( name ) == false || names.Add ( name ) == false )
                throw new FormatException ( $"내보낼 수 없는 파일명 또는 중복 단계입니다: {name}" );

            csv.Append ( TutorialCsvFormat.Quote ( name ) ).Append ( ',' )
                .Append ( TutorialCsvFormat.Quote ( step.TargetId ) ).Append ( ',' )
                .AppendLine ( TutorialCsvFormat.Quote ( step.Description ) );
        }

        File.WriteAllText ( path , csv.ToString ( ) , new UTF8Encoding ( true ) );
    }
}
