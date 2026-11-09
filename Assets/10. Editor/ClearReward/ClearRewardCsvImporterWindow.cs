#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 클리어 보상 CSV 창 - 입력과 미리보기/적용 요청
/// </summary>
public class ClearRewardCsvImporterWindow : EditorWindow
{
    [SerializeField] string _csvPath = "Assets/03. Configs/Currency/ClearRewardTargets.csv"; //CSV 경로
    [SerializeField] ClearRewardData _target; //반영할 보상 설정
    [SerializeField] StageSelectionData _stages; //대조할 스테이지 목록
    ClearRewardCsvImporter.Plan _plan; //현재 미리보기
    Vector2 _scroll; //미리보기 위치
    string _result; //처리 결과
    MessageType _resultType; //결과 종류

    /// <summary>
    /// 메뉴에서 임포터 열기
    /// </summary>
    [MenuItem ( "Tools/Clear Reward CSV Importer" )]
    public static void Open ( )
    {
        GetWindow<ClearRewardCsvImporterWindow> ( "Clear Reward CSV" ).Show ( );
    }

    private void OnEnable ( )
    {
        minSize = new Vector2 ( 640f, 480f );
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    private void OnGUI ( )
    {
        EditorGUILayout.HelpBox (
            "CSV 기준으로 Targets 전체를 교체합니다. 보상 금액/배율/기존 별 정책은 유지합니다.\n"
            + "선택한 보상 설정의 미저장 변경도 함께 저장됩니다.", MessageType.Info );
        using ( new EditorGUI.DisabledScope ( EditorApplication.isPlayingOrWillChangePlaymode ) )
        {
            EditorGUI.BeginChangeCheck ( );
            using ( new EditorGUILayout.HorizontalScope ( ) )
            {
                _csvPath = EditorGUILayout.TextField ( "기준 점수 CSV", _csvPath );
                if ( GUILayout.Button ( "선택", GUILayout.Width ( 55f ) ) == true )
                {
                    string selected = EditorUtility.OpenFilePanel ( "기준 점수 CSV", Application.dataPath, "csv" );
                    if ( selected.Length != 0 )
                    {
                        _csvPath = selected;
                        GUI.changed = true;
                    }
                }
            }
            _target = ( ClearRewardData ) EditorGUILayout.ObjectField (
                "보상 설정", _target, typeof ( ClearRewardData ), false );
            _stages = ( StageSelectionData ) EditorGUILayout.ObjectField (
                "스테이지 목록", _stages, typeof ( StageSelectionData ), false );
            if ( EditorGUI.EndChangeCheck ( ) == true )
                ClearPreview ( );

            if ( GUILayout.Button ( "검사 / 미리보기" ) == true )
            {
                ClearPreview ( );
                _plan = ClearRewardCsvImporter.Prepare ( _csvPath, _target, _stages );
            }
            using ( new EditorGUI.DisabledScope ( _plan == null || _plan.Error != null ) )
            {
                if ( GUILayout.Button ( "기준 점수 적용" ) == true )
                    Apply ( );
            }
        }
        if ( string.IsNullOrEmpty ( _result ) == false )
            EditorGUILayout.HelpBox ( _result, _resultType );
        DrawPreview ( );
    }

    /// <summary>
    /// 미리보기를 해제하고 적용 요청
    /// </summary>
    void Apply ( )
    {
        ClearRewardCsvImporter.Plan plan = _plan;
        //적용 실패 후에도 이전 미리보기는 재사용하지 않음
        ClearPreview ( );
        bool saved = ClearRewardCsvImporter.TryApply ( plan, out _result );
        _resultType = saved == true ? MessageType.Info : MessageType.Error;
    }

    /// <summary>
    /// 기준 점수 변경 목록 표시
    /// </summary>
    void DrawPreview ( )
    {
        if ( _plan == null )
            return;
        using ( EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope ( _scroll ) )
        {
            _scroll = scroll.scrollPosition;
            if ( _plan.Error != null )
                EditorGUILayout.HelpBox ( _plan.Error, MessageType.Error );
            else
                EditorGUILayout.LabelField ( $"기준 점수 {_plan.Targets.Count}개", EditorStyles.boldLabel );
            foreach ( string change in _plan.Changes )
                EditorGUILayout.LabelField ( change, EditorStyles.wordWrappedLabel );
        }
    }

    /// <summary>
    /// 이전 미리보기와 결과 해제
    /// </summary>
    void ClearPreview ( )
    {
        _plan = null;
        _result = null;
        _scroll = Vector2.zero;
    }

    /// <summary>
    /// 플레이 전환 시 미리보기 해제
    /// </summary>
    /// <param name="state">전환 상태</param>
    void HandlePlayModeChanged ( PlayModeStateChange state )
    {
        ClearPreview ( );
        Repaint ( );
    }

    private void OnDisable ( )
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        ClearPreview ( );
    }
}
#endif
