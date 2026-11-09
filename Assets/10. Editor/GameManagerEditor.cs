#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 게임 관리자 에디터 - 단축키와 화면 설정 검증
/// </summary>
[CustomEditor ( typeof ( GameManager ) )]
public class GameManagerEditor : Editor
{
    enum InputKind
    {
        Keyboard,    //키보드 키
        Mouse        //마우스 버튼
    }

    KeyBindingSettings _settings;      //현재 검사하는 런타임 설정 객체
    bool _ownsEdit;                    //이 Inspector에서 시작한 편집 여부
    bool _isExitingPlayMode;           //종료 중인 런타임 객체 재연결 차단
    int _selectedIndex;                //검증할 배정 목록 번호
    InputKind _inputKind;              //직접 지정할 입력 장치 종류
    int _keyboardIndex;                //직접 지정할 키보드 키 번호
    int _mouseIndex;                   //직접 지정할 마우스 버튼 번호
    string[] _keyboardNames;           //키보드 선택 목록 표시
    string[] _keyboardPaths;           //키보드 선택 목록 입력 경로
    string[] _mouseNames = { "왼쪽 버튼", "오른쪽 버튼", "가운데 버튼", "앞으로 버튼", "뒤로 버튼" }; //마우스 표시
    string[] _mousePaths =
    {
        "<Mouse>/leftButton", "<Mouse>/rightButton", "<Mouse>/middleButton",
        "<Mouse>/forwardButton", "<Mouse>/backButton"
    };    //마우스 선택 목록 입력 경로

    KeyBindingEntry _pendingRebind;    //Game 뷰 포커스 이동 후 시작할 입력 대기
    KeyBindingEntry _captureTarget;    //비동기 재지정 결과를 표시할 대상
    string _lastCaptureMessage;        //이미 출력한 재지정 안내
    string _lastResult = "실행 결과 없음"; //마지막 검증 요청 결과
    MessageType _resultType = MessageType.Info; //마지막 결과 표시 종류
    string _savedJson;    //마지막으로 조회한 저장 배정의 JSON 표시
    string _savedFileStatus = "아직 파일을 조회하지 않았습니다."; //마지막 파일 조회 결과
    MessageType _savedResultType = MessageType.Info; //파일 조회 결과 표시 종류
    Vector2 _savedScroll; //저장 배정 표시 스크롤

    DisplaySettings _displaySettings;    //현재 검사하는 화면 설정
    DisplaySetting _displayDraft;       //Inspector에서 선택 중인 화면 설정
    string[] _displayModeNames;          //지원 모드 표시 이름
    int[] _displayModeValues;            //런타임 목록에서 가져온 모드 값
    string[] _displayResolutionNames;    //해상도 선택 목록
    string _displayMessage = "실행 결과 없음";    //마지막 화면 요청 결과
    MessageType _displayResultType = MessageType.Info;    //화면 요청 결과 표시 종류

    private void OnEnable ( )
    {
        BuildKeyboardChoices ( );
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    /// <summary>
    /// 런타임 입력 상태를 Inspector에 반영
    /// </summary>
    /// <returns>플레이 중 검증 객체를 표시하는지 여부</returns>
    public override bool RequiresConstantRepaint ( )
    {
        return Application.isPlaying && _settings != null;
    }

    /// <summary>
    /// 기존 관리자 참조와 설정 검증 화면 표시
    /// </summary>
    public override void OnInspectorGUI ( )
    {
        using ( new EditorGUI.DisabledScope ( Application.isPlaying == true ) )
            DrawDefaultInspector ( );

        GameManager manager = ( GameManager ) target;

        //프리팹이나 종료 중인 관리자가 아닌 현재 실행의 공용 객체만 사용
        if ( Application.isPlaying == false || _isExitingPlayMode == true
            || manager != GameManager.Instance || manager.Settings == null )
        {
            ReleaseDebugSession ( );
            EditorGUILayout.HelpBox (
                "Play 후 자동 생성된 GameManager를 선택하세요.", MessageType.Info );
            return;
        }

        ConnectSettings ( manager.KeyBindings );

        //단축키 연결 변경 시 이전 검증 수명을 정리한 뒤 화면 설정을 연결
        DrawDisplaySettings ( manager.Settings.Display );

        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "단축키 검증", EditorStyles.boldLabel );

        //해제 프레임 소비가 끝나면 이전 재지정 추적을 종료해 다음 직접 지정 로그와 분리
        if ( _settings.IsCapturingInput == false )
            _captureTarget = null;

        EditorGUILayout.HelpBox (
            "저장 없이 적용은 이번 실행에만 반영합니다. "
            + "저장 후 적용은 전체 편집 배정을 파일에 저장한 뒤 반영합니다. "
            + "입력 로그는 실제 행동 성공과 구분합니다.", MessageType.Info );

        if ( string.IsNullOrEmpty ( _settings.LoadError ) == false )
            EditorGUILayout.HelpBox ( _settings.LoadError, MessageType.Warning );

        EditorGUILayout.LabelField ( "편집", _settings.IsEditing == true ? "진행 중" : "종료" );
        EditorGUILayout.LabelField ( "입력 소비", _settings.IsCapturingInput == true ? "진행 중" : "없음" );

        using ( new EditorGUI.DisabledScope (
            _settings.IsEditing == true || _settings.IsCapturingInput == true ) )
        {
            if ( GUILayout.Button ( "1. 편집 시작" ) == true )
            {
                _ownsEdit = _settings.TryBeginEdit ( );
                Report ( _ownsEdit, _ownsEdit == true ? "편집 시작" : "편집을 시작할 수 없습니다." );
            }
        }

        DrawTarget ( );
        DrawEditingControls ( );
        DrawInputLog ( );
        DrawStorageControls ( );
        DrawSaveFailureTests ( );

        EditorGUILayout.Space ( );
        EditorGUILayout.HelpBox ( _lastResult, _resultType );
    }

    /// <summary>
    /// 현재 실행의 화면 설정과 검증 목록 연결
    /// </summary>
    /// <param name="display">검사할 화면 설정</param>
    void ConnectDisplaySettings ( DisplaySettings display )
    {
        if ( _displaySettings == display )
            return;

        _displaySettings = display;
        _displayDraft = display.Settings;
        _displayMessage = "실행 결과 없음";
        _displayResultType = MessageType.Info;

        _displayModeNames = new string [ display.Modes.Count ];
        _displayModeValues = new int [ display.Modes.Count ];

        for ( int i = 0; i < display.Modes.Count; i++ )
        {
            FullScreenMode mode = display.Modes [ i ];
            _displayModeValues [ i ] = ( int ) mode;
            _displayModeNames [ i ] = mode.ToString ( );
        }

        _displayResolutionNames = new string [ display.Resolutions.Count ];

        for ( int i = 0; i < display.Resolutions.Count; i++ )
        {
            Vector2Int size = display.Resolutions [ i ];
            _displayResolutionNames [ i ] = $"{size.x} x {size.y}";
        }
    }

    /// <summary>
    /// 화면 설정 선택과 변경 요청 검증
    /// </summary>
    /// <param name="display">현재 실행의 화면 설정</param>
    void DrawDisplaySettings ( DisplaySettings display )
    {
        ConnectDisplaySettings ( display );

        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "화면 설정 검증", EditorStyles.boldLabel );

        DisplaySetting current = display.Settings;
        EditorGUILayout.LabelField ( "접수 설정",
            $"{current.Mode} / {current.Width} x {current.Height}" );
        EditorGUILayout.LabelField ( "실제 화면",
            $"{Screen.fullScreenMode} / {Screen.width} x {Screen.height}" );

        if ( string.IsNullOrEmpty ( display.LoadError ) == false )
            EditorGUILayout.HelpBox ( display.LoadError, MessageType.Warning );

        int mode = EditorGUILayout.IntPopup ( "화면 모드",
            ( int ) _displayDraft.Mode, _displayModeNames, _displayModeValues );

        int selectedResolution = 0;

        for ( int i = 0; i < display.Resolutions.Count; i++ )
        {
            Vector2Int size = display.Resolutions [ i ];

            if ( size.x == _displayDraft.Width && size.y == _displayDraft.Height )
            {
                selectedResolution = i;
                break;
            }
        }

        selectedResolution = EditorGUILayout.Popup (
            "해상도", selectedResolution, _displayResolutionNames );
        Vector2Int selectedSize = display.Resolutions [ selectedResolution ];
        _displayDraft = new DisplaySetting (
            ( FullScreenMode ) mode, selectedSize.x, selectedSize.y );

        if ( GUILayout.Button ( "기본값 선택" ) == true )
            _displayDraft = display.DefaultSettings;

        bool apply = GUILayout.Button ( "화면 설정 저장 없이 적용" );
        bool save = GUILayout.Button ( "화면 설정 저장 후 적용" );

        if ( apply == true || save == true )
        {
            string reason;
            bool accepted = save == true
                ? display.TrySaveAndApply ( _displayDraft, out reason )
                : display.TryApply ( _displayDraft, out reason );

            _displayMessage = accepted == true
                ? $"{( save == true ? "저장 후" : "저장 없이" )} 변경 요청 접수: "
                    + $"{_displayDraft.Mode} / {_displayDraft.Width} x {_displayDraft.Height}"
                : reason;
            _displayResultType = accepted == true ? MessageType.Info : MessageType.Warning;

            if ( accepted == true )
                Debug.Log ( $"[화면 설정 검증] {_displayMessage}", target );
            else
                Debug.LogWarning ( $"[화면 설정 검증] {_displayMessage}", target );
        }

        EditorGUILayout.HelpBox ( _displayMessage, _displayResultType );
        EditorGUILayout.HelpBox (
            "접수 설정과 실제 화면은 프레임 종료 전에는 다를 수 있습니다. "
            + "전체 창 모드의 선택 해상도는 렌더링 크기이며, 실제 모드 전환은 Windows 실행 파일에서 확인합니다.",
            MessageType.Info );
    }

    /// <summary>
    /// 키보드 장치 연결과 무관한 직접 지정 목록 구성
    /// </summary>
    void BuildKeyboardChoices ( )
    {
        List<string> names = new ( );
        List<string> paths = new ( );

        foreach ( var control in InputSystem.LoadLayout ( "Keyboard" ).controls )
        {
            if ( control.layout.ToString ( ) != "Key" )
                continue;

            names.Add ( control.name.ToString ( ) );
            paths.Add ( $"<Keyboard>/{control.name}" );

            if ( control.name.ToString ( ) == "h" )
                _keyboardIndex = paths.Count - 1;
        }

        _keyboardNames = names.ToArray ( );
        _keyboardPaths = paths.ToArray ( );
    }

    /// <summary>
    /// 현재 실행의 설정 객체 연결과 이전 검증 수명 해제
    /// </summary>
    /// <param name="settings">현재 공용 단축키 설정</param>
    void ConnectSettings ( KeyBindingSettings settings )
    {
        if ( _settings == settings )
            return;

        ReleaseDebugSession ( );
        _settings = settings;
        _selectedIndex = 0;
        _settings.OnEditChanged += HandleEditChanged;
    }

    /// <summary>
    /// 검증 대상과 실제/편집 배정 표시
    /// </summary>
    void DrawTarget ( )
    {
        string[] names = new string [ _settings.Entries.Count ];

        for ( int i = 0; i < names.Length; i++ )
        {
            KeyBindingEntry entry = _settings.Entries [ i ];
            names [ i ] = $"{i + 1}. {entry.Name} [{_settings.GetBindingDisplay ( entry )}]";
        }

        using ( new EditorGUI.DisabledScope (
            _settings.IsCapturingInput == true || _pendingRebind != null ) )
        {
            int index = EditorGUILayout.Popup ( "검증 대상", _selectedIndex, names );

            if ( index != _selectedIndex )
            {
                //로그가 이전 기능을 계속 관찰하지 않도록 대상 변경 시 관찰 종료
                _settings.StopDebugInputLog ( );
                _selectedIndex = index;
            }
        }

        KeyBindingEntry selected = _settings.Entries [ _selectedIndex ];
        EditorGUILayout.LabelField ( "액션", selected.ActionPath );
        EditorGUILayout.LabelField ( "현재 적용", _settings.GetBindingDisplay ( selected ) );
        EditorGUILayout.LabelField ( "편집 배정", _settings.IsEditing == true
            ? _settings.GetDraftBindingDisplay ( selected ) : "편집 시작 후 표시" );
    }

    /// <summary>
    /// 직접 지정과 실제 키 재지정/저장/런타임 적용 요청
    /// </summary>
    void DrawEditingControls ( )
    {
        KeyBindingEntry entry = _settings.Entries [ _selectedIndex ];
        bool canEdit = _ownsEdit && _settings.IsEditing
            && _settings.IsCapturingInput == false && _pendingRebind == null;

        using ( new EditorGUI.DisabledScope ( canEdit == false ) )
        {
            EditorGUILayout.Space ( );
            _inputKind = ( InputKind ) EditorGUILayout.EnumPopup ( "입력 종류", _inputKind );

            string path;
            if ( _inputKind == InputKind.Keyboard )
            {
                _keyboardIndex = EditorGUILayout.Popup ( "변경할 키", _keyboardIndex, _keyboardNames );
                path = _keyboardPaths [ _keyboardIndex ];
            }
            else
            {
                _mouseIndex = EditorGUILayout.Popup ( "변경할 버튼", _mouseIndex, _mouseNames );
                path = _mousePaths [ _mouseIndex ];
            }

            if ( GUILayout.Button ( "2. 선택한 키로 변경" ) == true )
            {
                bool changed = _settings.TrySetDraftBinding ( entry, path, out string reason );
                Report ( changed, changed == true ? _settings.RebindMessage : reason );
            }

            if ( GUILayout.Button ( "실제 키 입력으로 재지정 (Game 뷰로 이동)" ) == true )
                RequestFocusedRebind ( entry );

            if ( GUILayout.Button ( "3. 저장 없이 적용" ) == true )
            {
                string before = _settings.GetBindingDisplay ( entry );
                bool applied = _settings.TryApplyRuntime ( out string reason );
                Report ( applied, applied == true
                    ? $"런타임 적용: {entry.Name} {before} → {_settings.GetBindingDisplay ( entry )} / 파일 저장 없음"
                    : reason );
            }

            if ( GUILayout.Button ( "전체 편집 배정 저장 후 적용" ) == true )
            {
                string before = _settings.GetBindingDisplay ( entry );
                bool saved = _settings.TryApply ( out string reason );

                Report ( saved, saved == true
                    ? $"전체 편집 배정 저장/적용 완료 / 선택 항목: "
                        + $"{entry.Name} {before} → {_settings.GetBindingDisplay ( entry )}"
                    : $"저장 실패: {reason} / 기존 적용값과 편집값 유지" );

                //저장/적용 완료와 뒤이은 파일 조회는 결과를 분리해 표시
                if ( saved == true )
                    RefreshSavedSnapshot ( );
            }

            if ( GUILayout.Button ( "편집값 기본값 복원 (적용 전)" ) == true )
            {
                _settings.ResetDraft ( );
                Report ( true, "편집값을 기본값으로 복원했습니다. "
                    + "이번 실행만 변경하려면 저장 없이 적용, "
                    + "재실행에도 유지하려면 저장 후 적용을 누르세요." );
            }
        }

        using ( new EditorGUI.DisabledScope ( _ownsEdit == false
            || ( _settings.IsCapturingInput == false && _pendingRebind == null ) ) )
        {
            if ( GUILayout.Button ( "입력 대기 취소" ) == true )
            {
                CancelPendingRebind ( );
                _settings.CancelRebind ( );
                Report ( true, "입력 대기 취소 요청" );
            }
        }

        using ( new EditorGUI.DisabledScope ( _ownsEdit == false ) )
        {
            if ( GUILayout.Button ( "편집 종료 (미적용 편집값 폐기)" ) == true )
            {
                CancelPendingRebind ( );
                _captureTarget = null;
                _settings.CancelEdit ( );
                _ownsEdit = false;
                Report ( true, "편집 종료 / 이미 런타임 적용한 배정은 유지됩니다." );
            }
        }

        if ( _pendingRebind != null )
            EditorGUILayout.HelpBox ( "Game 뷰 포커스 이동 후 입력 대기를 시작합니다.", MessageType.Info );
        else if ( _ownsEdit == true && _settings.IsCapturingInput == true )
            EditorGUILayout.HelpBox ( _settings.RebindMessage, MessageType.Info );
    }

    /// <summary>
    /// 선택 기능의 실제 입력 로그 시작/종료
    /// </summary>
    void DrawInputLog ( )
    {
        EditorGUILayout.Space ( );
        KeyBindingEntry entry = _settings.Entries [ _selectedIndex ];
        bool logging = _settings.DebugInputEntry != null;

        if ( GUILayout.Button ( logging == true
            ? "선택 기능 입력 로그 끄기" : "4. 선택 기능 입력 로그 켜기" ) == true )
        {
            if ( logging == true )
                _settings.StopDebugInputLog ( );
            else
                _settings.StartDebugInputLog ( entry );

            Report ( true, logging == true ? "입력 로그 종료"
                : $"입력 로그 시작: {entry.ActionPath} / Game 뷰에서 확인하세요. 기능의 보조 배정도 관찰합니다." );
        }
    }

    /// <summary>
    /// 실제 저장 위치와 마지막으로 조회한 파일 내용 표시
    /// </summary>
    void DrawStorageControls ( )
    {
        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "단축키 저장 파일", EditorStyles.boldLabel );

        EditorGUILayout.SelectableLabel (
            _settings.DebugSavePath, EditorStyles.textField,
            GUILayout.Height ( EditorGUIUtility.singleLineHeight ) );

        if ( GUILayout.Button ( "저장 폴더 열기" ) == true )
        {
            string directory = System.IO.Path.GetDirectoryName ( _settings.DebugSavePath );

            //최초 저장 전 폴더가 없는 경우에는 조회만으로 생성하지 않음
            if ( System.IO.Directory.Exists ( directory ) == true )
                EditorUtility.RevealInFinder ( directory );
            else
                Report ( false, "저장 폴더가 아직 없습니다. 표시된 경로를 확인하세요." );
        }

        if ( GUILayout.Button ( "저장된 배정 조회" ) == true )
            RefreshSavedSnapshot ( );

        EditorGUILayout.HelpBox ( _savedFileStatus, _savedResultType );

        if ( string.IsNullOrEmpty ( _savedJson ) == true )
            return;

        EditorGUILayout.LabelField ( "마지막 조회 내용 — 자동 갱신 아님" );
        _savedScroll = EditorGUILayout.BeginScrollView (
            _savedScroll, GUILayout.Height ( 160f ) );

        using ( new EditorGUI.DisabledScope ( true ) )
            EditorGUILayout.TextArea ( _savedJson );

        EditorGUILayout.EndScrollView ( );
    }

    /// <summary>
    /// 저장된 배정을 읽어 Inspector와 Console에 표시
    /// </summary>
    void RefreshSavedSnapshot ( )
    {
        //이전 성공 내용을 현재 조회 결과로 오해하지 않도록 먼저 비움
        _savedJson = null;

        bool loaded = _settings.TryReadSavedBindings (
            out KeyBindingSaveData data, out string reason );

        if ( loaded == false )
        {
            _savedFileStatus = $"파일 조회 실패: {reason}";
            _savedResultType = MessageType.Warning;
            Debug.LogWarning ( $"[단축키 파일] {_savedFileStatus}", target );
            return;
        }

        _savedResultType = MessageType.Info;

        if ( data == null )
        {
            _savedFileStatus = "저장 파일 없음 / 시작 시 기본 배정 사용";
        }
        else
        {
            _savedJson = JsonUtility.ToJson ( data, true );
            _savedFileStatus = $"파일 조회 성공 / 변경 배정={data.Bindings.Count}";
        }

        //파일 조회 성공은 시작 시 배정 복원 성공이나 저장 요청 성공과 구분
        Debug.Log ( $"[단축키 파일] {_savedFileStatus}", target );
    }

    /// <summary>
    /// 격리된 파일과 액션에서 쓰기 실패/손상 복원 검증 요청
    /// </summary>
    void DrawSaveFailureTests ( )
    {
        EditorGUILayout.Space ( );
        EditorGUILayout.LabelField ( "단축키 저장 예외 검증", EditorStyles.boldLabel );
        EditorGUILayout.HelpBox (
            "테스트 전용 폴더와 비활성 액션 복사본에서 기존 API를 검증합니다. "
            + "현재 설정 파일/편집값/플레이 배정은 변경하지 않으며 완료 후 테스트 파일을 정리합니다.",
            MessageType.Info );

        using ( new EditorGUI.DisabledScope (
            _settings.IsCapturingInput == true || _pendingRebind != null ) )
        {
            if ( GUILayout.Button ( "쓰기 실패/재시도 검증" ) == true )
            {
                bool passed;
                string report;

                //동기 검증이 끝나거나 중간에 반환돼도 자원을 정리한 뒤 최종 결과 표시
                using ( KeyBindingSaveDebug debug = new ( InputSystem.actions, target ) )
                    passed = debug.RunWriteFailureTest ( out report );

                Report ( passed, report );
            }

            if ( GUILayout.Button ( "손상 파일 복원 검증" ) == true )
            {
                bool passed;
                string report;

                using ( KeyBindingSaveDebug debug = new ( InputSystem.actions, target ) )
                    passed = debug.RunCorruptFileTests ( out report );

                Report ( passed, report );
            }
        }
    }

    /// <summary>
    /// Game 뷰 포커스 이동 후 실제 입력 대기 예약
    /// </summary>
    /// <param name="entry">재지정할 배정</param>
    void RequestFocusedRebind ( KeyBindingEntry entry )
    {
        if ( EditorApplication.ExecuteMenuItem ( "Window/General/Game" ) == false )
        {
            Report ( false, "Game 뷰를 열 수 없습니다. 직접 키 지정으로 확인하세요." );
            return;
        }

        //포커스 변경 콜백이 끝난 뒤 시작하여 Inspector 이탈을 취소로 받지 않게 함
        _pendingRebind = entry;
        EditorApplication.delayCall += BeginFocusedRebind;
    }

    /// <summary>
    /// 예약한 대상의 실제 입력 대기 시작
    /// </summary>
    void BeginFocusedRebind ( )
    {
        KeyBindingEntry entry = _pendingRebind;
        _pendingRebind = null;

        if ( Application.isPlaying == false || _settings == null || _ownsEdit == false )
            return;

        if ( EditorWindow.focusedWindow == null
            || EditorWindow.focusedWindow.GetType ( ).Name != "GameView" )
        {
            Report ( false, "Game 뷰 포커스가 변경되어 입력 대기를 시작하지 않았습니다." );
            return;
        }

        _captureTarget = entry;
        _lastCaptureMessage = string.Empty;

        if ( _settings.StartRebind ( entry ) == false )
        {
            _captureTarget = null;
            Report ( false, "현재 편집 상태에서 입력 대기를 시작할 수 없습니다." );
        }
    }

    /// <summary>
    /// 비동기 재지정 상태를 화면과 변경 시점 로그에 반영
    /// </summary>
    void HandleEditChanged ( )
    {
        Repaint ( );

        if ( _captureTarget == null || _ownsEdit == false || _settings.IsEditing == false )
            return;

        string message = _settings.RebindMessage;
        if ( string.IsNullOrEmpty ( message ) == false && message != _lastCaptureMessage )
        {
            _lastCaptureMessage = message;
            _lastResult = $"재지정: {_captureTarget.Name} / {message}";
            _resultType = MessageType.Info;
            Debug.Log ( $"[단축키 검증] {_lastResult}", target );
        }

    }

    /// <summary>
    /// 검증 요청 결과를 Inspector와 Console에 표시
    /// </summary>
    /// <param name="succeeded">요청 성공 여부</param>
    /// <param name="message">실제 요청 결과</param>
    void Report ( bool succeeded, string message )
    {
        _lastResult = message;
        _resultType = succeeded == true ? MessageType.Info : MessageType.Warning;

        if ( succeeded == true )
            Debug.Log ( $"[단축키 검증] {message}", target );
        else
            Debug.LogWarning ( $"[단축키 검증] {message}", target );
    }

    /// <summary>
    /// 아직 시작하지 않은 입력 대기 예약 취소
    /// </summary>
    void CancelPendingRebind ( )
    {
        EditorApplication.delayCall -= BeginFocusedRebind;
        _pendingRebind = null;
    }

    /// <summary>
    /// Inspector가 시작한 편집과 입력 관찰 정리
    /// </summary>
    void ReleaseDebugSession ( )
    {
        CancelPendingRebind ( );

        if ( _settings != null )
        {
            //구독을 먼저 해제하여 닫히는 Inspector에 재지정 결과가 전달되지 않게 함
            _settings.OnEditChanged -= HandleEditChanged;
            _settings.StopDebugInputLog ( );

            if ( _ownsEdit == true )
                _settings.CancelEdit ( );
        }

        _settings = null;
        _displaySettings = null;
        _displayModeNames = null;
        _displayModeValues = null;
        _displayResolutionNames = null;
        _ownsEdit = false;
        _captureTarget = null;
        _lastCaptureMessage = string.Empty;
        _savedJson = null;
        _savedFileStatus = "아직 파일을 조회하지 않았습니다.";
        _savedResultType = MessageType.Info;
        _savedScroll = Vector2.zero;
    }

    /// <summary>
    /// 재실행 때 이전 런타임 참조와 검증 상태를 재사용하지 않도록 정리
    /// </summary>
    /// <param name="state">플레이 모드 전환 상태</param>
    void HandlePlayModeChanged ( PlayModeStateChange state )
    {
        if ( state == PlayModeStateChange.ExitingPlayMode )
        {
            _isExitingPlayMode = true;
            ReleaseDebugSession ( );
        }
        else if ( state == PlayModeStateChange.EnteredEditMode
            || state == PlayModeStateChange.EnteredPlayMode )
        {
            _isExitingPlayMode = false;
            _selectedIndex = 0;
            _lastResult = "실행 결과 없음";
            _resultType = MessageType.Info;
        }
    }

    private void OnDisable ( )
    {
        ReleaseDebugSession ( );
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
    }
}
#endif
