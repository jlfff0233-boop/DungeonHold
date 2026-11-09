using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 동료 CSV 편집 창 - 입력 선택, 변경 미리보기와 기존 설정 적용 요청
/// </summary>
public class CompanionCsvImporterWindow : EditorWindow
{
    [SerializeField] string _csvPath = "Assets/03. Configs/Combat/Companion/Companion_Test.csv";    //입력 CSV 경로
    [SerializeField] CompanionHireDataMap _map;    //기존 동료의 연결 기준
    CompanionCsvImporter.Plan _plan;    //현재 검사와 변경 미리보기
    Vector2 _scroll;    //미리보기 스크롤
    string _result;    //적용 결과
    MessageType _resultType;    //결과 안내 종류

    /// <summary>
    /// 메뉴에서 동료 CSV 편집 창 열기
    /// </summary>
    [MenuItem ( "Tools/Companion CSV Importer" )]
    public static void Open ( )
    {
        GetWindow<CompanionCsvImporterWindow> ( "동료 CSV 임포터" ).Show ( );
    }

    private void OnEnable ( )
    {
        minSize = new Vector2 ( 720f , 480f );
        if ( _map == null )
            _map = AssetDatabase.LoadAssetAtPath<CompanionHireDataMap> (
                "Assets/03. Configs/Combat/Companion/Hire/!_CompanionHireDataMap.asset" );
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    private void OnGUI ( )
    {
        EditorGUILayout.HelpBox (
            "한 행은 동료 한 타입입니다. 기존 SO의 수치만 갱신하며 프리팹/참조/고용 맵은 유지합니다.\n"
            + "비율 0.2는 20%, 거리는 m, 시간은 초입니다. 적용값은 다음 동료 초기화부터 사용합니다." , MessageType.Info );
        using ( new EditorGUI.DisabledScope ( EditorApplication.isPlayingOrWillChangePlaymode ) )
        {
            EditorGUI.BeginChangeCheck ( );
            _csvPath = EditorGUILayout.TextField ( "CSV 파일" , _csvPath );
            if ( GUILayout.Button ( "CSV 파일 선택" ) == true )
            {
                string selected = EditorUtility.OpenFilePanel ( "동료 CSV 선택" , Application.dataPath , "csv" );
                if ( selected.Length != 0 )
                {
                    _csvPath = selected;
                    GUI.changed = true;
                }
            }
            _map = (CompanionHireDataMap) EditorGUILayout.ObjectField (
                "동료 고용 데이터 맵" , _map , typeof ( CompanionHireDataMap ) , false );
            if ( EditorGUI.EndChangeCheck ( ) == true )
            {
                _plan = null;
                _result = null;
            }
            if ( GUILayout.Button ( "검사 및 미리보기" ) == true )
                Inspect ( );
            using ( new EditorGUI.DisabledScope ( _plan == null || _plan.Errors.Count != 0 ) )
            {
                if ( GUILayout.Button ( "적용" ) == true )
                    Apply ( );
            }
        }
        if ( string.IsNullOrEmpty ( _result ) == false )
            EditorGUILayout.HelpBox ( _result , _resultType );
        DrawPreview ( );
    }

    /// <summary>
    /// 에셋을 수정하지 않고 CSV 전체 검사
    /// </summary>
    void Inspect ( )
    {
        _plan = null;
        _result = null;
        try
        {
            _plan = CompanionCsvImporter.Prepare ( _csvPath , _map );
        }
        catch ( Exception exception )
        {
            //예상하지 못한 도구 오류는 숨기지 않고 원본 스택과 함께 표시
            _result = $"검사 실패: {exception.Message}\n에셋은 변경하지 않았습니다. Console을 확인하세요.";
            _resultType = MessageType.Error;
            Debug.LogException ( exception );
        }
    }

    /// <summary>
    /// 미리보기 이후 입력/연결/현재 값 변경을 확인한 뒤 반영
    /// </summary>
    void Apply ( )
    {
        List<string> touched = new List<string> ( );
        try
        {
            CompanionCsvImporter.Plan latest = CompanionCsvImporter.Prepare ( _csvPath , _map );
            bool changed = latest.Errors.Count != 0 || latest.Snapshot != _plan.Snapshot;
            _plan = latest;
            if ( changed == true )
            {
                _result = "CSV 또는 대상 설정이 변경되어 미리보기를 갱신했습니다. 내용을 확인한 뒤 적용하세요.";
                _resultType = MessageType.Info;
                return;
            }
            CompanionCsvImporter.Apply ( latest , touched );
            _result = touched.Count == 0 ? "변경할 값이 없습니다."
                : $"적용 완료: 동료 {latest.EntryCount}타입, 에셋 {touched.Count}개 저장. Undo로 값을 되돌릴 수 있습니다.";
            _resultType = MessageType.Info;
            _plan = null;
        }
        catch ( Exception exception )
        {
            _plan = null;
            _result = $"적용 실패: {exception.Message}\n"
                + ( touched.Count == 0 ? "에셋 변경 전에 실패했습니다."
                    : "일부 값이 반영됐을 수 있습니다. 수정 시도한 에셋:\n" + string.Join ( "\n" , touched ) )
                + "\nConsole을 확인하세요.";
            _resultType = MessageType.Error;
            Debug.LogException ( exception );
        }
    }

    /// <summary>
    /// 오류와 필드별 변경 전후 값 표시
    /// </summary>
    void DrawPreview ( )
    {
        if ( _plan == null )
            return;
        _scroll = EditorGUILayout.BeginScrollView ( _scroll );
        foreach ( string error in _plan.Errors )
            EditorGUILayout.HelpBox ( error , MessageType.Error );
        foreach ( string notice in _plan.Notices )
            EditorGUILayout.HelpBox ( notice , MessageType.Info );
        int changed = 0;
        foreach ( CompanionCsvImporter.Change change in _plan.Changes )
        {
            if ( change.Changed == true )
                changed++;
        }
        EditorGUILayout.LabelField (
            $"동료 {_plan.EntryCount}타입 / 변경 {changed}항목 / 오류 {_plan.Errors.Count}개" , EditorStyles.boldLabel );
        UnityEngine.Object previous = null;
        foreach ( CompanionCsvImporter.Change change in _plan.Changes )
        {
            if ( change.Changed == false )
                continue;
            if ( previous != change.Target )
            {
                previous = change.Target;
                EditorGUILayout.Space ( );
                EditorGUILayout.LabelField ( change.Label , EditorStyles.boldLabel );
                EditorGUILayout.LabelField ( AssetDatabase.GetAssetPath ( change.Target ) , EditorStyles.wordWrappedLabel );
            }
            EditorGUILayout.LabelField ( change.Column.Name ,
                CompanionCsvImporter.FormatValue ( change.Before ) + " → "
                + CompanionCsvImporter.FormatValue ( change.After ) );
        }
        EditorGUILayout.EndScrollView ( );
    }

    /// <summary>
    /// 편집/플레이 전환 시 이전 미리보기 해제
    /// </summary>
    /// <param name="state">플레이 모드 전환 상태</param>
    void HandlePlayModeChanged ( PlayModeStateChange state )
    {
        _plan = null;
        _result = null;
        Repaint ( );
    }

    private void OnDisable ( )
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        _plan = null;
    }
}
