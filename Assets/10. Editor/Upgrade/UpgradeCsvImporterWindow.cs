using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 강화 CSV 편집 창 - 입력 선택과 변경 미리보기 및 임포트 요청
/// </summary>
public class UpgradeCsvImporterWindow : EditorWindow
{
    [SerializeField] string _csvPath = "Assets/03. Configs/Upgrade/Upgrade_Test.csv";    //입력 CSV 경로
    [SerializeField] DefaultAsset _outputFolder;    //신규 에셋 출력 폴더
    [SerializeField] UpgradeDataMap _map;    //등록할 강화 맵
    UpgradeCsvImporter.Plan _plan;    //현재 검사 결과와 임시 에셋
    Vector2 _scroll;    //미리보기 스크롤
    string _result;    //적용 결과 안내
    MessageType _resultType;    //결과 메시지 종류

    /// <summary>
    /// 메뉴에서 강화 CSV 창 열기
    /// </summary>
    [MenuItem( "Tools/Upgrade CSV Importer" )]
    public static void Open ()
    {
        GetWindow<UpgradeCsvImporterWindow>( "Upgrade CSV Importer" ).Show();
    }

    private void OnEnable ()
    {
        minSize = new Vector2( 640f, 480f );
        if ( _outputFolder == null )
            _outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>( "Assets/03. Configs/Upgrade" );
        if ( _map == null )
            _map = AssetDatabase.LoadAssetAtPath<UpgradeDataMap>( "Assets/03. Configs/Upgrade/!_UpgradeDataMap.asset" );
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    private void OnGUI ()
    {
        EditorGUILayout.HelpBox(
            "한 행은 한 강화의 한 단계입니다. 비율 0.2는 20%이며, 사용하지 않는 효과 칸은 비워 둡니다.\n"
            + "CSV에 없는 맵 항목은 유지합니다. ID 변경과 최대 단계 축소는 기존 저장 데이터와의 호환성을 확인하세요.", MessageType.Info );
        using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode ) )
        {
            EditorGUI.BeginChangeCheck();
            _csvPath = EditorGUILayout.TextField( "CSV 파일", _csvPath );
            if ( GUILayout.Button( "CSV 파일 선택" ) == true )
            {
                string selected = EditorUtility.OpenFilePanel( "강화 CSV 선택", Application.dataPath, "csv" );
                if ( selected.Length != 0 )
                {
                    _csvPath = selected;
                    GUI.changed = true;
                }
            }
            _outputFolder = (DefaultAsset) EditorGUILayout.ObjectField( "출력 폴더", _outputFolder, typeof( DefaultAsset ), false );
            _map = (UpgradeDataMap) EditorGUILayout.ObjectField( "강화 맵", _map, typeof( UpgradeDataMap ), false );
            if ( EditorGUI.EndChangeCheck() == true )
            {
                ClearPlan();
                _result = null;
            }

            if ( GUILayout.Button( "검사 및 미리보기" ) == true )
                Inspect();
            using ( new EditorGUI.DisabledScope( _plan == null || _plan.Errors.Count != 0 ) )
            {
                if ( GUILayout.Button( "적용" ) == true )
                    Apply();
            }
        }
        if ( string.IsNullOrEmpty( _result ) == false )
            EditorGUILayout.HelpBox( _result, _resultType );
        DrawPreview();
    }

    /// <summary>
    /// 최신 입력으로 미리보기 검사
    /// </summary>
    void Inspect ()
    {
        ClearPlan();
        _result = null;
        try
        {
            _plan = UpgradeCsvImporter.Prepare( _csvPath, AssetDatabase.GetAssetPath( _outputFolder ), _map );
        }
        catch ( Exception exception )
        {
            _result = "검사 중 예외가 발생했습니다. 에셋은 변경하지 않았습니다. Console을 확인하세요.";
            _resultType = MessageType.Error;
            Debug.LogException( exception );
        }
    }

    /// <summary>
    /// 적용 직전 재검사와 입력 변경 확인 후 반영
    /// </summary>
    void Apply ()
    {
        List<string> touched = new List<string>();
        try
        {
            UpgradeCsvImporter.Plan latest = UpgradeCsvImporter.Prepare(
                _csvPath, AssetDatabase.GetAssetPath( _outputFolder ), _map );
            bool changed = latest.Errors.Count != 0 || latest.Fingerprint != _plan.Fingerprint;
            ClearPlan();
            _plan = latest;
            if ( changed == true )
            {
                _result = "입력 또는 에셋 변경을 감지하여 검사 결과를 갱신했습니다. 미리보기를 확인한 뒤 다시 적용해 주세요.";
                _resultType = MessageType.Info;
                return;
            }
            UpgradeCsvImporter.Apply( _plan, touched );
            _result = $"적용 완료: 강화 {_plan.Entries.Count}개 검사, 에셋/맵 {touched.Count}개 반영. 씬과 Animator 연결은 직접 확인해 주세요.";
            _resultType = MessageType.Info;
            ClearPlan();
        }
        catch ( Exception exception )
        {
            ClearPlan();
            _result = touched.Count == 0
                ? "적용 중 예외가 발생했습니다. Console을 확인하세요."
                : "적용 중 예외가 발생하여 일부 에셋이 반영됐을 수 있습니다. 다음 경로와 Console을 확인하세요:\n"
                    + string.Join( "\n", touched );
            _resultType = MessageType.Error;
            Debug.LogException( exception );
        }
    }

    /// <summary>
    /// 오류와 기존 값 및 반영 예정 값 표시
    /// </summary>
    void DrawPreview ()
    {
        if ( _plan == null )
            return;
        _scroll = EditorGUILayout.BeginScrollView( _scroll );
        foreach ( string error in _plan.Errors )
            EditorGUILayout.HelpBox( error, MessageType.Error );
        EditorGUILayout.LabelField( $"강화 {_plan.Entries.Count}개 / 오류 {_plan.Errors.Count}개", EditorStyles.boldLabel );
        foreach ( UpgradeCsvImporter.Entry entry in _plan.Entries )
        {
            if ( entry.Candidate == null )
                continue;
            string action = entry.Existing == null ? "생성" : entry.Changed == true ? "갱신" : "변경 없음";
            EditorGUILayout.Space();
            EditorGUILayout.LabelField( $"[{action}] {entry.Id}" + (entry.AddToMap == true ? " / 맵 추가" : ""), EditorStyles.boldLabel );
            EditorGUILayout.SelectableLabel( entry.AssetPath, EditorStyles.wordWrappedLabel,
                GUILayout.Height( EditorStyles.wordWrappedLabel.CalcHeight( new GUIContent( entry.AssetPath ), position.width - 50f ) ) );
            if ( entry.Changed == true && entry.Existing != null )
            {
                EditorGUILayout.LabelField( "기존 값", EditorStyles.boldLabel );
                DrawValue( entry.Before );
            }
            EditorGUILayout.LabelField( entry.Changed == true ? "반영 값" : "현재 값", EditorStyles.boldLabel );
            DrawValue( entry.After );
        }
        EditorGUILayout.EndScrollView();
    }

    /// <summary>
    /// 여러 줄의 단계별 값이 잘리지 않도록 높이를 계산하여 표시
    /// </summary>
    /// <param name="value">표시할 값</param>
    void DrawValue ( string value )
    {
        float height = EditorStyles.wordWrappedLabel.CalcHeight( new GUIContent( value ), position.width - 50f );
        EditorGUILayout.LabelField( value, EditorStyles.wordWrappedLabel, GUILayout.Height( height ) );
    }

    /// <summary>
    /// 검사 결과와 임시 후보 해제
    /// </summary>
    void ClearPlan ()
    {
        _plan?.Dispose();
        _plan = null;
    }

    /// <summary>
    /// 모드 전환 시 이전 검사 결과 해제
    /// </summary>
    /// <param name="state">플레이 모드 전환 상태</param>
    void HandlePlayModeChanged ( PlayModeStateChange state )
    {
        ClearPlan();
        Repaint();
    }

    private void OnDisable ()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        ClearPlan();
    }
}
