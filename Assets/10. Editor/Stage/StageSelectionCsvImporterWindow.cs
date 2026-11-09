#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 스테이지 CSV 편집 창 - 입력과 미리보기/적용 요청
/// </summary>
public class StageSelectionCsvImporterWindow : EditorWindow
{
    [SerializeField] string _csvPath = "Assets/03. Configs/Stage/StageSelection.csv"; //입력 경로
    [SerializeField] StageSelectionData _target; //반영할 설정
    StageSelectionCsvImporter.Plan _plan; //현재 미리보기
    Vector2 _scroll; //미리보기 위치
    string _result; //처리 결과
    MessageType _resultType; //결과 종류

    /// <summary>
    /// 메뉴에서 창 열기
    /// </summary>
    [MenuItem( "Tools/Stage Selection CSV Importer" )]
    public static void Open ()
    {
        GetWindow<StageSelectionCsvImporterWindow>( "Stage Selection CSV" ).Show( );
    }

    private void OnEnable ()
    {
        minSize = new Vector2( 640f, 480f );
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    private void OnGUI ()
    {
        EditorGUILayout.HelpBox(
            "Create > Stage > Selection Data로 만든 설정을 연결하세요.\n"
            + "CSV 순서로 반영하며 같은 ID의 이미지는 보존합니다. CSV에서 빠진 항목은 뒤에 유지합니다.\n"
            + "선택한 설정의 미저장 변경도 함께 저장합니다. 씬/프리팹과 플레이 설정은 변경하지 않습니다.",
            MessageType.Info );
        using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode ) )
        {
            EditorGUI.BeginChangeCheck( );
            using ( new EditorGUILayout.HorizontalScope( ) )
            {
                _csvPath = EditorGUILayout.TextField( "스테이지 CSV", _csvPath );
                if ( GUILayout.Button( "선택", GUILayout.Width( 55f ) ) == true )
                {
                    string selected = EditorUtility.OpenFilePanel( "스테이지 CSV", Application.dataPath, "csv" );
                    if ( selected.Length != 0 )
                    {
                        _csvPath = selected;
                        GUI.changed = true;
                    }
                }
            }
            _target = ( StageSelectionData ) EditorGUILayout.ObjectField(
                "스테이지 목록 설정", _target, typeof( StageSelectionData ), false );
            if ( EditorGUI.EndChangeCheck( ) == true )
                ClearPreview( );
            if ( GUILayout.Button( "검사 및 미리보기" ) == true )
            {
                ClearPreview( );
                _plan = StageSelectionCsvImporter.Prepare( _csvPath, _target );
            }
            using ( new EditorGUI.DisabledScope( _plan == null || _plan.Error != null ) )
            {
                if ( GUILayout.Button( "적용" ) == true )
                    Apply( );
            }
        }

        if ( string.IsNullOrEmpty( _result ) == false )
            EditorGUILayout.HelpBox( _result, _resultType );
        DrawPreview( );
    }

    /// <summary>
    /// 미리보기를 소비하고 적용 요청
    /// </summary>
    void Apply ()
    {
        StageSelectionCsvImporter.Plan plan = _plan;
        //저장 중 실패해도 이전 계획을 재사용하지 않도록 먼저 해제
        ClearPreview( );
        bool saved = StageSelectionCsvImporter.TryApply( plan, out _result );
        _resultType = saved == true ? MessageType.Info : MessageType.Error;
    }

    /// <summary>
    /// 최종 순서와 변경 내용 표시
    /// </summary>
    void DrawPreview ()
    {
        if ( _plan == null )
            return;
        using ( EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope( _scroll ) )
        {
            _scroll = scroll.scrollPosition;
            if ( _plan.Error != null )
                EditorGUILayout.HelpBox( _plan.Error, MessageType.Error );
            else
                EditorGUILayout.LabelField( $"최종 스테이지 {_plan.Stages.Count}개", EditorStyles.boldLabel );
            if ( _plan.SavePending == true )
                EditorGUILayout.HelpBox( "설정의 미저장 변경이 함께 저장됩니다.", MessageType.Info );
            for ( int i = 0; i < _plan.Changes.Count; i++ )
            {
                EditorGUILayout.Space( );
                string value = $"{i + 1}. {_plan.Changes [ i ]}";
                float height = EditorStyles.wordWrappedLabel.CalcHeight( new GUIContent( value ), position.width - 40f );
                EditorGUILayout.LabelField( value, EditorStyles.wordWrappedLabel, GUILayout.Height( height ) );
            }
        }
    }

    /// <summary>
    /// 이전 입력과 결과 참조 해제
    /// </summary>
    void ClearPreview ()
    {
        _plan = null;
        _result = null;
        _scroll = Vector2.zero;
    }

    /// <summary>
    /// 플레이 전환 시 이전 미리보기 해제
    /// </summary>
    /// <param name="state">전환 상태</param>
    void HandlePlayModeChanged ( PlayModeStateChange state )
    {
        ClearPreview( );
        Repaint( );
    }

    private void OnDisable ()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        ClearPreview( );
    }
}
#endif
