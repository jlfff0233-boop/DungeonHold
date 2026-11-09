using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 미션 CSV 편집 창 - 입력 선택과 변경 미리보기 및 적용 요청
/// </summary>
public class MissionCsvImporterWindow : EditorWindow
{
    [SerializeField] string _missionCsvPath = "Assets/03. Configs/Mission/Mission_Test.csv";    //필수 미션 CSV
    [SerializeField] string _killCountCsvPath = "Assets/03. Configs/Mission/MissionKillCount_Test.csv";    //선택 집계 CSV
    [SerializeField] DefaultAsset _outputFolder;    //신규 에셋 출력 폴더
    [SerializeField] MissionDataMap _map;    //등록할 영속 미션 맵
    MissionCsvImporter.Plan _plan;    //검사 결과와 임시 객체
    Vector2 _scroll;    //미리보기 스크롤
    string _result;    //적용 결과
    MessageType _resultType;    //결과 안내 종류

    /// <summary>
    /// 메뉴에서 미션 임포터 열기
    /// </summary>
    [MenuItem( "Tools/Mission CSV Importer" )]
    public static void Open ()
    {
        GetWindow<MissionCsvImporterWindow>( "Mission CSV Importer" ).Show();
    }

    private void OnEnable ()
    {
        minSize = new Vector2( 700f, 520f );
        if ( _outputFolder == null )
            _outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>( "Assets/03. Configs/Mission" );
        if ( _map == null )
            _map = AssetDatabase.LoadAssetAtPath<MissionDataMap>( "Assets/03. Configs/Mission/!_MissionDataMap.asset" );
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    private void OnGUI ()
    {
        EditorGUILayout.HelpBox(
            "출력 폴더와 Mission > Data Map으로 만든 빈 맵을 연결한 뒤 검사하세요. 한 행은 한 미션입니다.\n"
            + "난이도는 Easy|Normal|Hard로 입력합니다. 처치 집계 CSV는 선택이며, 생략하면 기존 집계를 사용합니다.\n"
            + "CSV에 없는 기존 항목은 유지합니다. 같은 집계 ID의 대상 적/맵과 같은 미션 ID의 구체 타입은 변경할 수 없습니다.",
            MessageType.Info );
        EditorGUILayout.HelpBox(
            "저장 슬롯은 변경하지 않습니다. 완료/지급 기록은 유지되며 조건 재평가는 다음 정상 처치/승리 평가에서 이뤄집니다.\n"
            + "테스트 CSV는 미션 10개/집계 2개이며 밸런스 초기값입니다. 완전 방어 규칙은 실제 도전 시작 설정을 사용합니다.",
            MessageType.Info );

        using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode ) )
        {
            EditorGUI.BeginChangeCheck();
            _missionCsvPath = DrawCsvPath( "미션 CSV(필수)", _missionCsvPath );
            _killCountCsvPath = DrawCsvPath( "처치 집계 CSV(선택)", _killCountCsvPath );
            _outputFolder = (DefaultAsset) EditorGUILayout.ObjectField( "출력 폴더", _outputFolder, typeof( DefaultAsset ), false );
            _map = (MissionDataMap) EditorGUILayout.ObjectField( "미션 맵", _map, typeof( MissionDataMap ), false );
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
    /// 경로 입력과 CSV 선택 버튼 표시
    /// </summary>
    /// <param name="label">입력 이름</param>
    /// <param name="path">현재 경로</param>
    /// <returns>선택 또는 입력한 경로</returns>
    string DrawCsvPath ( string label, string path )
    {
        using ( new EditorGUILayout.HorizontalScope() )
        {
            path = EditorGUILayout.TextField( label, path );
            if ( GUILayout.Button( "선택", GUILayout.Width( 55f ) ) == true )
            {
                string selected = EditorUtility.OpenFilePanel( label, Application.dataPath, "csv" );
                if ( selected.Length != 0 )
                {
                    path = selected;
                    GUI.changed = true;
                }
            }
        }
        return path;
    }

    /// <summary>
    /// 입력과 최종 맵을 검사하여 미리보기 준비
    /// </summary>
    void Inspect ()
    {
        ClearPlan();
        _result = null;
        try
        {
            _plan = MissionCsvImporter.Prepare( _missionCsvPath, _killCountCsvPath, AssetDatabase.GetAssetPath( _outputFolder ), _map );
        }
        catch ( Exception exception )
        {
            _result = "검사 중 예외가 발생했습니다. 파일은 변경하지 않았습니다. Console을 확인하세요.";
            _resultType = MessageType.Error;
            Debug.LogException( exception );
        }
    }

    /// <summary>
    /// 최신 입력 재검사 후 미리보기와 같을 때만 적용
    /// </summary>
    void Apply ()
    {
        List<string> touched = new List<string>();
        try
        {
            MissionCsvImporter.Plan latest = MissionCsvImporter.Prepare(
                _missionCsvPath, _killCountCsvPath, AssetDatabase.GetAssetPath( _outputFolder ), _map );
            bool changed = latest.Errors.Count != 0 || latest.Fingerprint != _plan.Fingerprint;
            ClearPlan();
            _plan = latest;
            if ( changed == true )
            {
                _result = "입력 또는 관련 에셋이 변경되어 미리보기를 갱신했습니다. 결과를 확인한 뒤 다시 적용하세요.";
                _resultType = MessageType.Info;
                return;
            }
            MissionCsvImporter.Apply( _plan, touched );
            _result = $"적용 완료: 정의 {_plan.Entries.Count}개 검사, 에셋/맵 {touched.Count}개 반영.\n"
                + "TestScene의 미션 맵을 Inspector에서 연결하고 맵 ID/난이도/방어 규칙을 확인하세요. 씬 연결과 플레이 검증은 별도입니다.";
            _resultType = MessageType.Info;
            ClearPlan();
        }
        catch ( Exception exception )
        {
            ClearPlan();
            _result = touched.Count == 0 ? "적용 중 예외가 발생했습니다. Console을 확인하세요."
                : "일부 에셋이 반영됐을 수 있습니다. 다음 경로와 Console을 확인한 뒤 다시 검사하세요:\n" + string.Join( "\n", touched );
            _resultType = MessageType.Error;
            Debug.LogException( exception );
        }
    }

    /// <summary>
    /// 행별 오류/저장 영향과 생성/갱신/등록 예정값 표시
    /// </summary>
    void DrawPreview ()
    {
        if ( _plan == null )
            return;
        _scroll = EditorGUILayout.BeginScrollView( _scroll );
        foreach ( string error in _plan.Errors )
            EditorGUILayout.HelpBox( error, MessageType.Error );
        foreach ( string warning in _plan.Warnings )
            EditorGUILayout.HelpBox( warning, MessageType.Warning );
        EditorGUILayout.LabelField( $"정의 {_plan.Entries.Count}개 / 오류 {_plan.Errors.Count}개 / 안내 {_plan.Warnings.Count}개", EditorStyles.boldLabel );
        foreach ( MissionCsvImporter.Entry entry in _plan.Entries )
        {
            if ( entry.After == null )
                continue;
            string action = entry.Existing == null ? "생성" : entry.Changed == true ? "갱신" : "변경 없음";
            EditorGUILayout.Space();
            EditorGUILayout.LabelField( $"[{action}] {entry.Id}" + (entry.AddToMap == true ? " / 맵 추가" : "")
                + (entry.SavePending == true ? " / 미저장 저장" : ""), EditorStyles.boldLabel );
            DrawValue( entry.AssetPath );
            if ( entry.Existing != null && entry.Changed == true )
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
    /// 여러 줄 설정을 내용 높이에 맞춰 표시
    /// </summary>
    /// <param name="value">표시할 내용</param>
    void DrawValue ( string value )
    {
        float height = EditorStyles.wordWrappedLabel.CalcHeight( new GUIContent( value ), position.width - 50f );
        EditorGUILayout.LabelField( value, EditorStyles.wordWrappedLabel, GUILayout.Height( height ) );
    }

    /// <summary>
    /// 이전 검사 후보 전체 해제
    /// </summary>
    void ClearPlan ()
    {
        _plan?.Dispose();
        _plan = null;
    }

    /// <summary>
    /// 모드 전환 시 이전 미리보기 해제
    /// </summary>
    /// <param name="state">변경된 플레이 모드 상태</param>
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
