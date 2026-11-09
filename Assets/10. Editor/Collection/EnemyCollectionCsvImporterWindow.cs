using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 도감 CSV 편집 창 - 입력 선택과 변경 미리보기 및 적용 요청
/// </summary>
public class EnemyCollectionCsvImporterWindow : EditorWindow
{
    [SerializeField] string _csvPath = "Assets/03. Configs/Collection/EnemyCollection.csv";    //도감 CSV
    [SerializeField] DefaultAsset _outputFolder;    //신규 도감 SO를 둘 폴더
    [SerializeField] EnemyCollectionDataMap _map;    //등록할 도감 맵
    EnemyCollectionCsvImporter.Plan _plan;    //검사 결과와 임시 객체
    Vector2 _scroll;    //미리보기 스크롤
    string _result;    //적용 결과
    MessageType _resultType;    //결과 안내 종류

    /// <summary>
    /// 메뉴에서 도감 임포터 열기
    /// </summary>
    [MenuItem( "Tools/Enemy Collection CSV Importer" )]
    public static void Open ()
    {
        GetWindow<EnemyCollectionCsvImporterWindow>( "Enemy Collection CSV Importer" ).Show( );
    }

    private void OnEnable ()
    {
        minSize = new Vector2( 700f, 520f );
        if ( _outputFolder == null )
            _outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>( "Assets/03. Configs/Collection" );
        if ( _map == null )
            _map = AssetDatabase.LoadAssetAtPath<EnemyCollectionDataMap>( "Assets/03. Configs/Collection/!_EnemyCollectionDataMap.asset" );
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    private void OnGUI ()
    {
        EditorGUILayout.HelpBox(
            "Collection > Data Map으로 만든 빈 맵과 출력 폴더를 연결하세요. 한 행은 적 한 종류입니다.\n"
            + "같은 ID는 기존 SO를 갱신하고 CSV에 없는 항목은 유지합니다. 같은 ID의 원본 적은 바꿀 수 없습니다.\n"
            + "BackgroundStory 컬럼은 선택입니다. 없으면 기존 스토리를 유지하고, 빈 셀이면 스토리를 지웁니다.\n"
            + "대표 이미지는 Inspector에서 연결하며 재임포트 시 유지합니다. KMB 원본 적과 저장 슬롯은 변경하지 않습니다.",
            MessageType.Info );

        using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode ) )
        {
            EditorGUI.BeginChangeCheck( );
            _csvPath = DrawCsvPath( _csvPath );
            _outputFolder = ( DefaultAsset ) EditorGUILayout.ObjectField( "출력 폴더", _outputFolder, typeof( DefaultAsset ), false );
            _map = ( EnemyCollectionDataMap ) EditorGUILayout.ObjectField( "도감 맵", _map, typeof( EnemyCollectionDataMap ), false );
            if ( EditorGUI.EndChangeCheck( ) == true )
            {
                ClearPlan( );
                _result = null;
            }
            if ( GUILayout.Button( "검사 및 미리보기" ) == true )
            {
                ClearPlan( );
                _result = null;
                _plan = EnemyCollectionCsvImporter.Prepare( _csvPath, AssetDatabase.GetAssetPath( _outputFolder ), _map );
            }
            using ( new EditorGUI.DisabledScope( _plan == null || _plan.Errors.Count != 0 ) )
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
    /// CSV 경로 입력과 파일 선택 표시
    /// </summary>
    /// <param name="path">현재 경로</param>
    /// <returns>선택 또는 입력한 경로</returns>
    string DrawCsvPath ( string path )
    {
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            path = EditorGUILayout.TextField( "도감 CSV", path );
            if ( GUILayout.Button( "선택", GUILayout.Width( 55f ) ) == true )
            {
                string selected = EditorUtility.OpenFilePanel( "도감 CSV", Application.dataPath, "csv" );
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
    /// 최신 입력을 재확인하고 미리보기와 같을 때만 실제 에셋 반영
    /// </summary>
    void Apply ()
    {
        //창을 연 동안 CSV/Inspector가 바뀔 수 있으므로 이전 미리보기로 덮어쓰지 않음
        EnemyCollectionCsvImporter.Plan latest = EnemyCollectionCsvImporter.Prepare(
            _csvPath, AssetDatabase.GetAssetPath( _outputFolder ), _map );
        bool changed = latest.Errors.Count != 0 || latest.Fingerprint != _plan.Fingerprint;
        ClearPlan( );
        _plan = latest;
        if ( changed == true )
        {
            _result = "입력 또는 관련 에셋이 변경되어 미리보기를 갱신했습니다. 내용을 확인한 뒤 다시 적용하세요.";
            _resultType = MessageType.Info;
            return;
        }

        List<string> touched = new List<string>( );
        //실제 에셋 쓰기의 예외만 여기서 보고. 자동 롤백 없이 부분 반영 경로와 원인을 남김
        try
        {
            bool saved = EnemyCollectionCsvImporter.Apply( _plan, touched, out string error );
            _result = saved == true
                ? $"적용 완료: 도감 {_plan.Entries.Count}개 검사 / 에셋/맵 {touched.Count}개 반영.\n"
                    + "도감 맵과 대표 이미지 연결, 플레이 검증은 별도로 확인하세요."
                : error + "\n다시 검사하기 전에 반영 경로를 확인하세요:\n" + string.Join( "\n", touched );
            _resultType = saved == true ? MessageType.Info : MessageType.Error;
        }
        catch ( Exception exception )
        {
            _result = "에셋 적용 중 예외가 발생했습니다. Console을 확인하세요."
                + ( touched.Count == 0 ? "" : "\n일부 반영됐을 수 있는 경로:\n" + string.Join( "\n", touched ) );
            _resultType = MessageType.Error;
            Debug.LogException( exception );
        }
        finally
        {
            //성공/저장 실패 모두 다음 검사에서 실제 에셋을 다시 읽도록 후보 해제
            ClearPlan( );
        }
    }

    /// <summary>
    /// 행별 오류와 도감 생성/갱신/맵 등록 예정 표시
    /// </summary>
    void DrawPreview ()
    {
        if ( _plan == null )
            return;
        _scroll = EditorGUILayout.BeginScrollView( _scroll );
        foreach ( string error in _plan.Errors )
            EditorGUILayout.HelpBox( error, MessageType.Error );
        foreach ( string notice in _plan.Notices )
            EditorGUILayout.HelpBox( notice, MessageType.Info );
        EditorGUILayout.LabelField( $"도감 {_plan.Entries.Count}개 / 오류 {_plan.Errors.Count}개", EditorStyles.boldLabel );
        foreach ( EnemyCollectionCsvImporter.Entry entry in _plan.Entries )
        {
            if ( entry.After == null )
                continue;
            string action = entry.Existing == null ? "생성" : entry.Changed == true ? "갱신" : "변경 없음";
            EditorGUILayout.Space( );
            EditorGUILayout.LabelField( $"[{action}] {entry.Id}" + ( entry.AddToMap == true ? " / 맵 추가" : "" ), EditorStyles.boldLabel );
            DrawValue( entry.AssetPath );
            if ( entry.Existing != null && entry.Changed == true )
            {
                EditorGUILayout.LabelField( "기존 값", EditorStyles.boldLabel );
                DrawValue( entry.Before );
            }
            EditorGUILayout.LabelField( "반영 값", EditorStyles.boldLabel );
            DrawValue( entry.After );
        }
        EditorGUILayout.EndScrollView( );
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
        _plan?.Dispose( );
        _plan = null;
    }

    /// <summary>
    /// 플레이 모드 전환 시 이전 미리보기 해제
    /// </summary>
    /// <param name="state">변경된 모드 상태</param>
    void HandlePlayModeChanged ( PlayModeStateChange state )
    {
        ClearPlan( );
        Repaint( );
    }

    private void OnDisable ()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        ClearPlan( );
    }
}
