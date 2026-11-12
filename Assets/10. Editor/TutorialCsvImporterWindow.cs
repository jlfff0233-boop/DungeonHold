using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 튜토리얼 CSV 편집 창 - 내보내기/검사/적용 요청
/// </summary>
public class TutorialCsvImporterWindow : EditorWindow
{
    const string DefaultCsvPath = "Assets/03. Configs/Tutorial/Tutorial.csv";    //기본 예제 CSV
    const string DefaultMapPath = "Assets/03. Configs/Tutorial/!_TutorialStepDataMap.asset";    //기본 테스트 데이터맵

    [SerializeField] string _csvPath = DefaultCsvPath;    //선택한 입력 CSV
    [SerializeField] TutorialStepDataMap _map;    //반영할 데이터맵

    TutorialCsvImporter.Plan _plan;    //현재 검사 및 미리보기
    Vector2 _scroll;    //미리보기 스크롤 위치
    string _result;    //내보내기 또는 적용 결과
    MessageType _resultType;    //결과 메시지 종류

    /// <summary>
    /// 도구 메뉴에서 튜토리얼 CSV 창 열기
    /// </summary>
    [MenuItem ( "Tools/Tutorial CSV Importer" )]
    public static void Open ( )
    {
        TutorialCsvImporterWindow window = GetWindow<TutorialCsvImporterWindow> ( "Tutorial CSV" );
        if ( window._map == null )
            window._map = AssetDatabase.LoadAssetAtPath<TutorialStepDataMap> ( DefaultMapPath );
        window.Show ( );
    }

    private void OnEnable ( )
    {
        minSize = new Vector2 ( 540f , 380f );
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    private void OnGUI ( )
    {
        EditorGUILayout.HelpBox (
            "CSV의 행 순서가 데이터맵 순서가 됩니다. 빠진 행은 맵에서 연결만 해제합니다.\n"
            + "신규 단계는 데이터맵 폴더에 생성합니다. 적용 시 해당 에셋의 미저장 변경도 함께 저장됩니다." ,
            MessageType.Info );

        using ( new EditorGUI.DisabledScope ( EditorApplication.isPlayingOrWillChangePlaymode ) )
        {
            EditorGUI.BeginChangeCheck ( );
            using ( new EditorGUILayout.HorizontalScope ( ) )
            {
                _csvPath = EditorGUILayout.TextField ( "입력 CSV" , _csvPath );
                if ( GUILayout.Button ( "선택" , GUILayout.Width ( 55f ) ) == true )
                {
                    string path = EditorUtility.OpenFilePanel ( "튜토리얼 CSV" , Application.dataPath , "csv" );
                    if ( path.Length > 0 )
                    {
                        _csvPath = path;
                        GUI.changed = true;
                    }
                }
            }

            _map = ( TutorialStepDataMap ) EditorGUILayout.ObjectField (
                "단계 데이터맵" , _map , typeof ( TutorialStepDataMap ) , false );
            if ( EditorGUI.EndChangeCheck ( ) == true )
                ClearPreview ( );

            using ( new EditorGUI.DisabledScope ( _map == null ) )
            {
                if ( GUILayout.Button ( "데이터맵을 CSV로 내보내기" ) == true )
                    Export ( );

                if ( GUILayout.Button ( "검사 및 미리보기" ) == true )
                {
                    ClearPreview ( );
                    _plan = TutorialCsvImporter.Prepare ( _csvPath , _map );
                }
            }

            using ( new EditorGUI.DisabledScope ( _plan == null || _plan.Errors.Count > 0 ) )
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
    /// 적용 직전에 검사 상태를 대조하고 결과 표시
    /// </summary>
    void Apply ( )
    {
        TutorialCsvImporter.Plan preview = _plan;
        ClearPreview ( );
        bool saved = TutorialCsvImporter.TryApply ( _csvPath , _map , preview , out _result );
        _resultType = saved == true ? MessageType.Info : MessageType.Error;
    }

    /// <summary>
    /// 출력 파일 선택 후 현재 데이터맵을 CSV로 저장
    /// </summary>
    void Export ( )
    {
        string path = EditorUtility.SaveFilePanel ( "튜토리얼 CSV 내보내기" , Application.dataPath , "Tutorial" , "csv" );
        if ( path.Length == 0 )
            return;

        ClearPreview ( );
        try
        {
            TutorialCsvImporter.Export ( _map , path );
            _csvPath = path;
            _result = "CSV를 저장했습니다: " + path;
            _resultType = MessageType.Info;
        }
        catch ( Exception exception ) when ( exception is IOException || exception is UnauthorizedAccessException
            || exception is FormatException || exception is ArgumentException || exception is NotSupportedException )
        {
            _result = "CSV를 내보내지 못했습니다: " + exception.Message;
            _resultType = MessageType.Error;
        }
    }

    /// <summary>
    /// 오류, 씬 검사 범위와 반영 순서 표시
    /// </summary>
    void DrawPreview ( )
    {
        if ( _plan == null )
            return;

        using ( EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope ( _scroll ) )
        {
            _scroll = scroll.scrollPosition;
            foreach ( string error in _plan.Errors )
                EditorGUILayout.HelpBox ( error , MessageType.Error );
            if ( string.IsNullOrEmpty ( _plan.Notice ) == false )
                EditorGUILayout.HelpBox ( _plan.Notice , MessageType.Info );
            foreach ( string change in _plan.Preview )
                EditorGUILayout.HelpBox ( change , MessageType.None );
        }
    }

    /// <summary>
    /// 이전 검사 결과와 스크롤 초기화
    /// </summary>
    void ClearPreview ( )
    {
        _plan = null;
        _result = null;
        _scroll = Vector2.zero;
    }

    /// <summary>
    /// 플레이 전환 후 이전 검사 결과 재사용 방지
    /// </summary>
    /// <param name="state">플레이 전환 상태</param>
    void HandlePlayModeChanged ( PlayModeStateChange state )
    {
        ClearPreview ( );
        Repaint ( );
    }

    private void OnDisable ( )
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
    }
}
