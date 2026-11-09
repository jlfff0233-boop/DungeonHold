#if UNITY_EDITOR
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 저장 관리자 에디터 - 수동 백업과 복원 검증 요청 및 결과 표시
/// </summary>
[CustomEditor ( typeof ( SaveManager ) )]
public class SaveManagerEditor : Editor
{
    ( Task<ProgressBackupResponse> Task, string Label ) _request;    //현재 요청과 결과에 표시할 작업명
    string _lastResult = "아직 요청하지 않았습니다.";    //마지막 요청 결과
    string _snapshotJson;    //마지막으로 조회한 확정 저장 복사본
    Vector2 _snapshotScroll;    //저장 내용 표시 스크롤

    /// <summary>
    /// 기존 설정과 백업 검증 도구 표시
    /// </summary>
    public override void OnInspectorGUI ( )
    {
        //GameManager의 Awake에서 설정을 읽으므로 실행 중 설정 편집은 막음
        using ( new EditorGUI.DisabledScope ( Application.isPlaying == true ) )
            DrawDefaultInspector ( );

        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "백업/불러오기 검증", EditorStyles.boldLabel );
        EditorGUILayout.HelpBox (
            "Play 전에 Firebase 설정과 강화/미션 맵을 연결하세요. "
            + "TestScene이 없는 검증용 씬에서 사용합니다. "
            + "Auto Begin Stage 해제나 TestScene 비활성화만으로는 백업이 허용되지 않습니다.",
            MessageType.Info );

        SaveManager manager = ( SaveManager ) target;
        GameManager game = GameManager.Instance;
        if ( Application.isPlaying == false
            || manager.isActiveAndEnabled == false
            || game == null || game.IsInitialized == false
            || game.Save != manager )
        {
            EditorGUILayout.HelpBox (
                "플레이 모드에서 초기화된 GameManager의 SaveManager를 사용합니다.", MessageType.Info );
            return;
        }

        CompleteRequest ( manager );
        EditorGUILayout.LabelField ( "현재 단계", manager.BackupPhase.ToString ( ) );
        EditorGUILayout.LabelField ( "확정 진행", manager.HasProgress == true ? "있음" : "없음" );

        //확인 대기 중에는 준비/복원 대신 후보 승인과 취소만 허용
        using ( new EditorGUI.DisabledScope (
            _request.Task != null || manager.IsBackupPending == true ) )
        {
            if ( GUILayout.Button ( "진행 JSON 구조/복원 검증 (격리된 테스트 파일)" ) == true )
            {
                bool passed;
                string report;

                //기존 진행/서버 요청은 사용하지 않고 테스트 정리를 마친 뒤 결과 표시
                using ( SaveJsonDebug debug = new ( manager ) )
                    passed = debug.Run ( out report );

                _lastResult = report;

                if ( passed == true )
                    Debug.Log ( report, manager );
                else
                    Debug.LogWarning ( report, manager );
            }

            if ( GUILayout.Button ( "1. 로컬 진행 불러오기" ) == true )
            {
                SaveResult result = manager.TryLoadProgress ( );
                _lastResult = $"로컬 불러오기: {result}\n{manager.LastError}";
                Debug.Log ( _lastResult, manager );
                RefreshSnapshot ( manager );
            }

            if ( GUILayout.Button ( "2. 클라우드 백업 요청" ) == true )
                _request = ( manager.PrepareBackupAsync ( ), "클라우드 백업 요청" );

            if ( GUILayout.Button ( "클라우드 불러오기" ) == true
                && EditorUtility.DisplayDialog (
                    "클라우드 불러오기",
                    "현재 로컬 진행을 클라우드 백업으로 교체합니다. 계속하시겠습니까?",
                    "불러오기", "취소" ) == true )
                _request = ( manager.RestoreProgressAsync ( ), "클라우드 복원" );

            if ( GUILayout.Button ( "복원 직전 로컬 진행으로 복구" ) == true
                && EditorUtility.DisplayDialog (
                    "복원 직전 진행 복구",
                    "현재 로컬 진행을 복원 직전 보관본으로 교체합니다. 계속하시겠습니까?",
                    "복구", "취소" ) == true )
                ShowBackupResult ( manager, "복원 전 로컬 진행 복구", manager.RecoverPreviousProgress ( ) );
        }

        if ( manager.BackupPhase == ProgressBackupPhase.AwaitingConfirmation )
        {
            EditorGUILayout.HelpBox (
                "서버에 기존 백업이 있습니다. 승인하면 이번에 준비한 로컬 진행으로 교체합니다.",
                MessageType.Warning );
            using ( new EditorGUI.DisabledScope ( _request.Task != null ) )
            {
                using ( new EditorGUILayout.HorizontalScope ( ) )
                {
                    if ( GUILayout.Button ( "백업 덮어쓰기 승인" ) == true )
                        _request = ( manager.ConfirmBackupAsync ( ), "백업 덮어쓰기 승인" );

                    if ( GUILayout.Button ( "백업 덮어쓰기 취소" ) == true )
                    {
                        bool canceled = manager.CancelPreparedBackup ( );
                        _lastResult = $"백업 확인 취소: {canceled}";
                        Debug.Log ( _lastResult, manager );
                    }
                }
            }
        }

        EditorGUILayout.Space ( );
        EditorGUILayout.HelpBox ( _lastResult, MessageType.Info );
        if ( GUILayout.Button ( "현재 확정 저장값 조회" ) == true )
            RefreshSnapshot ( manager );

        if ( string.IsNullOrEmpty ( _snapshotJson ) == false )
        {
            EditorGUILayout.LabelField ( "마지막 조회 저장값", EditorStyles.boldLabel );
            _snapshotScroll = EditorGUILayout.BeginScrollView (
                _snapshotScroll, GUILayout.Height ( 180 ) );
            EditorGUILayout.TextArea ( _snapshotJson );
            EditorGUILayout.EndScrollView ( );
        }
    }

    /// <summary>
    /// 실행 중 요청의 단계와 완료 상태 갱신
    /// </summary>
    /// <returns>플레이 중 Inspector 재표시 여부</returns>
    public override bool RequiresConstantRepaint ( )
    {
        return Application.isPlaying;
    }

    /// <summary>
    /// 완료한 비동기 요청 결과를 한 번 표시
    /// </summary>
    /// <param name="manager">요청 대상 저장 관리자</param>
    void CompleteRequest ( SaveManager manager )
    {
        if ( _request.Task == null || _request.Task.IsCompleted == false )
            return;

        //결과 표시 전에 요청 참조를 해제하여 같은 완료를 반복 처리하지 않음
        ( Task<ProgressBackupResponse> completed, string label ) = _request;
        _request = default;
        if ( completed.IsFaulted == true )
        {
            _lastResult = $"{label} 예외: {completed.Exception.GetBaseException ( ).Message}";
            Debug.LogException ( completed.Exception, manager );
            return;
        }

        if ( completed.IsCanceled == true )
        {
            _lastResult = $"{label} 요청이 취소되었습니다.";
            return;
        }

        ShowBackupResult ( manager, label, completed.GetAwaiter ( ).GetResult ( ) );
    }

    /// <summary>
    /// 백업 결과와 서버 시각 표시 후 현재 확정 진행 재조회
    /// </summary>
    /// <param name="manager">조회할 저장 관리자</param>
    /// <param name="label">작업명</param>
    /// <param name="response">실제 요청 결과</param>
    void ShowBackupResult ( SaveManager manager, string label, ProgressBackupResponse response )
    {
        _lastResult = $"{label}: {response.Result}\n서버 시각: {response.UpdatedAt}\n{response.Detail}";
        Debug.Log ( _lastResult, manager );
        RefreshSnapshot ( manager );
    }

    /// <summary>
    /// 파일 읽기 없이 확정된 메모리 저장 복사본 표시
    /// </summary>
    /// <param name="manager">조회할 저장 관리자</param>
    void RefreshSnapshot ( SaveManager manager )
    {
        _snapshotJson = manager.HasProgress == true
            ? JsonUtility.ToJson ( manager.GetDebugSnapshot ( ), true )
            : "확정된 로컬 진행이 없습니다.";
        _snapshotScroll = Vector2.zero;
    }
}
#endif
