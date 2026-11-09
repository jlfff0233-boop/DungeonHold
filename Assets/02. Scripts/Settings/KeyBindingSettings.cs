using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// 단축키 설정 - 편집과 입력 대기/저장/적용 조율
/// </summary>
public class KeyBindingSettings : IDisposable
{
    enum CaptureState
    {
        None,                       //입력 대기 없음
        WaitingForStartRelease,     //시작 입력 해제 대기
        Listening,                  //새 입력 대기
        WaitingForFinishRelease     //완료/취소 입력 해제 대기
    }

    InputActionAsset _actions;         //실제 공용 액션
    InputActionAsset _draftActions;    //편집용 액션 복사본
    KeyBindingStorage _storage;        //로컬 설정 저장소
    List<KeyBindingEntry> _entries = new ( );    //변경 가능한 배정 목록

    HashSet<string> _allowedPaths = new ( StringComparer.OrdinalIgnoreCase ); //허용 버튼 경로

    HashSet<string> _reservedPaths = new ( StringComparer.OrdinalIgnoreCase )
    {
        "<Keyboard>/escape",
        "<Keyboard>/t"
    };    //재지정 취소 키와 정보 패널 키

    KeyBindingEntry _captureEntry;    //현재 재지정 대상
    CaptureState _captureState;       //입력 대기 단계
    int _consumedFrame = -1;          //입력 해제 프레임의 추가 실행 차단
    string _loadError;                //최초 설정 복원 실패 사유
    string _rebindMessage;            //재지정 상태와 결과 안내

#if UNITY_EDITOR
    KeyBindingEntry _debugInputEntry;    //입력 로그를 확인할 배정
    InputAction _debugInputAction;       //관찰할 실제 공용 액션
    Vector2 _debugMoveInput;             //마지막으로 출력한 이동 입력

    /// <summary>
    /// 현재 입력 로그 관찰 대상
    /// </summary>
    public KeyBindingEntry DebugInputEntry => _debugInputEntry;
#endif

    /// <summary>
    /// 변경 가능한 단축키 목록
    /// </summary>
    public IReadOnlyList<KeyBindingEntry> Entries => _entries;

    /// <summary>
    /// 편집 진행 여부
    /// </summary>
    public bool IsEditing => _draftActions != null;

    /// <summary>
    /// 최초 설정 복원 실패 사유
    /// </summary>
    public string LoadError => _loadError;

    /// <summary>
    /// 재지정 상태와 결과 안내
    /// </summary>
    public string RebindMessage => _rebindMessage;

    /// <summary>
    /// 재지정 입력과 해제 프레임의 추가 입력 소비 여부
    /// </summary>
    public bool IsCapturingInput =>
        _captureState != CaptureState.None
        || _consumedFrame == Time.frameCount;

    /// <summary>
    /// 실제 적용 배정 변경 이벤트
    /// </summary>
    public event Action OnBindingsChanged;

    /// <summary>
    /// 편집 배정과 입력 대기 상태 변경 이벤트
    /// </summary>
    public event Action OnEditChanged;

    /// <summary>
    /// 공용 액션과 변경 가능한 기능 연결
    /// </summary>
    /// <param name="actions">프로젝트 공용 액션</param>
    /// <param name="storage">단축키 저장소</param>
    public KeyBindingSettings (
        InputActionAsset actions , KeyBindingStorage storage )
    {
        _actions = actions;
        _storage = storage;

        BuildAllowedPaths ( );

        //변경 가능한 공용 액션을 등록하며 이동 방향/보조 배정은 개별 항목으로 유지
        AddEntries ( "Player/Move" , "이동" );
        AddEntries ( "Player/Jump" , "점프" );
        AddEntries ( "Player/Dodge" , "회피" );
        AddEntries ( "Player/Attack" , "기본 공격" );
        AddEntries ( "Player/Skill" , "기본 스킬" );
        AddEntries ( "Player/Ultimate" , "궁극기" );

        //설치 실행과 슬롯 선택은 서로 다른 배정으로 관리
        AddEntries ( "Player/PlaceInstallation" , "설치" );
        AddEntries ( "Player/DemolishInstallation" , "철거" );
        AddEntries ( "Player/RotateInstallation" , "설치물 회전" );
        AddEntries ( "Player/StartWave" , "웨이브 시작" );

        AddEntries ( "Player/SelectInstallationA" , "설치물 슬롯 1" );
        AddEntries ( "Player/SelectInstallationB" , "설치물 슬롯 2" );
        AddEntries ( "Player/SelectInstallationC" , "설치물 슬롯 3" );
        AddEntries ( "Player/SelectInstallationD" , "설치물 슬롯 4" );
        AddEntries ( "Player/SelectInstallationE" , "설치물 슬롯 5" );

        AddEntries ( "Player/HireCompanionA" , "동료 고용 슬롯 1" );
        AddEntries ( "Player/HireCompanionB" , "동료 고용 슬롯 2" );
        AddEntries ( "Player/HireCompanionC" , "동료 고용 슬롯 3" );
        AddEntries ( "Player/PlaceCompanionA" , "동료 배치 1" );
        AddEntries ( "Player/PlaceCompanionB" , "동료 배치 2" );

        AddEntries ( "Player/UseItemA" , "소모품 슬롯 1" );
        AddEntries ( "Player/UseItemB" , "소모품 슬롯 2" );
    }

    /// <summary>
    /// 기본 배정 또는 저장한 변경 배정 최초 적용
    /// </summary>
    /// <param name="reason">복원 실패 사유</param>
    /// <returns>저장 설정 복원 성공 여부</returns>
    public bool Init ( out string reason )
    {
        //이전 실행의 변경값에 의존하지 않고 에셋 기본 배정에서 복원 시작
        _draftActions = CreateDraft ( );
        _draftActions.RemoveAllBindingOverrides ( );

        bool loaded = _storage.TryLoad (
            out KeyBindingSaveData data , out reason );

        if ( loaded == true && data != null )
        {
            loaded = RestoreDraft ( data , out reason );

            //키를 서로 바꾼 정상 기록을
            //중간 상태의 중복으로 거절하지 않도록 전체 복원 후 검사
            if ( loaded == true )
                loaded = ValidateDraft ( out reason );
        }

        //실패한 복원의 일부 배정은 폐기하며 원본 파일은 덮어쓰지 않음
        if ( loaded == false )
            _draftActions.RemoveAllBindingOverrides ( );

        ApplyDraft ( );
        ReleaseDraft ( );

        //최초 적용은 화면 생성 전이므로
        //이벤트 대신 이후 최초 조회에서 결과 제공
        _loadError = reason;
        return loaded;
    }

    /// <summary>
    /// 현재 적용 상태로 편집 시작
    /// </summary>
    /// <returns>편집 시작 성공 여부</returns>
    public bool TryBeginEdit ( )
    {
        if ( IsEditing == true || IsCapturingInput == true )
            return false;

        _draftActions = CreateDraft ( );
        _rebindMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// 게임 시간과 무관하게 입력 대기 갱신
    /// </summary>
    public void UpdateCapture ( )
    {
        switch ( _captureState )
        {
            case CaptureState.WaitingForStartRelease:
                if ( IsAnyButtonPressed ( ) == true )
                    return;

                //재지정 버튼의 클릭/확정 입력이
                //새 배정으로 들어가지 않도록 해제 후 대기
                _captureState = CaptureState.Listening;
                _rebindMessage = "새 키를 누르세요. Esc: 취소";
                OnEditChanged?.Invoke ( );
                return;

            case CaptureState.Listening:
                ReadRebindInput ( );
                return;

            case CaptureState.WaitingForFinishRelease:
                if ( IsAnyButtonPressed ( ) == true )
                    return;

                //Update 호출 순서가 달라도 해제된 이번 프레임까지 메뉴/플레이 입력 소비
                _captureState = CaptureState.None;
                _consumedFrame = Time.frameCount;

                if ( IsEditing == true )
                    OnEditChanged?.Invoke ( );

                return;
        }
    }

    /// <summary>
    /// 선택한 배정의 새 입력 대기 요청
    /// </summary>
    /// <param name="entry">변경할 배정</param>
    /// <returns>입력 대기 시작 여부</returns>
    public bool StartRebind ( KeyBindingEntry entry )
    {
        if ( IsEditing == false || IsCapturingInput == true )
            return false;

        _captureEntry = entry;
        _captureState = CaptureState.WaitingForStartRelease;
        _rebindMessage = "버튼을 놓은 뒤 새 키를 누르세요.";

        //상태를 먼저 확정한 뒤 화면이 입력 소비 여부와 안내를 재조회
        OnEditChanged?.Invoke ( );
        return true;
    }

    /// <summary>
    /// 지정한 입력 경로로 편집 배정 변경
    /// </summary>
    /// <param name="entry">변경할 배정</param>
    /// <param name="path">배정할 입력 경로</param>
    /// <param name="reason">변경 실패 사유</param>
    /// <returns>편집 배정 변경 성공 여부</returns>
    public bool TrySetDraftBinding (
        KeyBindingEntry entry , string path , out string reason )
    {
        if ( CanEditDraft ( out reason ) == false )
            return false;

        if ( TryChangeDraftBinding ( entry , path , out reason ) == false )
            return false;

        OnEditChanged?.Invoke ( );
        return true;
    }

    /// <summary>
    /// 현재 입력 대기만 취소
    /// </summary>
    public void CancelRebind ( )
    {
        if ( _captureState != CaptureState.WaitingForStartRelease
            && _captureState != CaptureState.Listening )
            return;

        //이전에 편집한 다른 배정은 유지하며 취소 키 해제까지 소비
        WaitForFinishRelease ( );
        _rebindMessage = "키 변경을 취소했습니다.";
        OnEditChanged?.Invoke ( );
    }

    /// <summary>
    /// 편집 배정을 기본값으로 변경
    /// </summary>
    public void ResetDraft ( )
    {
        if ( IsEditing == false || IsCapturingInput == true )
            return;

        //실제 입력과 저장 파일은 적용 요청이 성공할 때까지 유지
        _draftActions.RemoveAllBindingOverrides ( );
        _rebindMessage = string.Empty;
        OnEditChanged?.Invoke ( );
    }

    /// <summary>
    /// 편집 배정 저장 후 실제 입력에 반영
    /// </summary>
    /// <param name="reason">적용 실패 사유</param>
    /// <returns>저장과 적용 성공 여부</returns>
    public bool TryApply ( out string reason )
    {
        if ( CanEditDraft ( out reason ) == false )
            return false;

        //배정은 재지정/복원 경계에서 검사했으므로 여기서 반복 검사하지 않음
        KeyBindingSaveData data = CreateSaveData ( );

        //저장 실패 시 편집값은 남겨 재시도할 수 있게 하고 실제 입력은 변경하지 않음
        if ( _storage.TrySave ( data , out reason ) == false )
            return false;

        ApplyDraft ( );
        _loadError = string.Empty;
        OnBindingsChanged?.Invoke ( );

        return true;
    }

    /// <summary>
    /// 파일 저장 없이 편집 배정을 현재 실행에만 적용
    /// </summary>
    /// <param name="reason">적용 실패 사유</param>
    /// <returns>런타임 적용 성공 여부</returns>
    public bool TryApplyRuntime ( out string reason )
    {
        if ( CanEditDraft ( out reason ) == false )
            return false;

        //검증용 적용은 저장소를 호출하지 않으며 기존 파일의 복원 오류도 지우지 않음
        ApplyDraft ( );
        OnBindingsChanged?.Invoke ( );
        return true;
    }

    /// <summary>
    /// 미적용 편집값 폐기와 입력 대기 취소
    /// </summary>
    public void CancelEdit ( )
    {
        //닫히는 화면에는 이벤트를 보내지 않으며 남은 키 해제 대기만 유지
        if ( _captureState != CaptureState.None )
            WaitForFinishRelease ( );

        ReleaseDraft ( );
    }

    /// <summary>
    /// 현재 적용 중인 키 표시 조회
    /// </summary>
    /// <param name="entry">조회할 배정</param>
    /// <returns>현재 적용 키 표시</returns>
    public string GetBindingDisplay ( KeyBindingEntry entry )
    {
        InputAction action = FindBinding ( _actions , entry , out int index );

        return action.GetBindingDisplayString ( index );
    }

    /// <summary>
    /// 편집 중인 키 표시 조회
    /// </summary>
    /// <param name="entry">조회할 배정</param>
    /// <returns>편집 중인 키 표시</returns>
    public string GetDraftBindingDisplay ( KeyBindingEntry entry )
    {
        InputAction action = FindBinding ( _draftActions , entry , out int index );

        return action.GetBindingDisplayString ( index );
    }

#if UNITY_EDITOR
    /// <summary>
    /// 검증할 실제 설정 파일 경로
    /// </summary>
    public string DebugSavePath => _storage.DebugSavePath;

    /// <summary>
    /// 실제 입력 변경 없이 저장된 배정 조회
    /// </summary>
    /// <param name="data">저장 데이터, 파일이 없으면 null</param>
    /// <param name="reason">읽기 실패 사유</param>
    /// <returns>조회 성공 또는 저장 파일 없음</returns>
    public bool TryReadSavedBindings (
        out KeyBindingSaveData data , out string reason )
    {
        //Init을 다시 호출하지 않아 현재 적용값과 편집값을 유지
        return _storage.TryLoad ( out data , out reason );
    }

    /// <summary>
    /// 선택한 기능의 실제 Input Action 입력 관찰 시작
    /// </summary>
    /// <param name="entry">입력 로그를 확인할 배정</param>
    public void StartDebugInputLog ( KeyBindingEntry entry )
    {
        _debugInputEntry = entry;
        _debugInputAction = _actions.FindAction ( entry.ActionPath , true );
        _debugMoveInput = Vector2.zero;
    }

    /// <summary>
    /// 선택 기능의 입력 관찰 종료
    /// </summary>
    public void StopDebugInputLog ( )
    {
        _debugInputEntry = null;
        _debugInputAction = null;
        _debugMoveInput = Vector2.zero;
    }

    /// <summary>
    /// 선택 기능의 입력만 게임 프레임에서 확인해 출력
    /// </summary>
    public void UpdateDebugInputLog ( )
    {
        if ( _debugInputEntry == null || IsCapturingInput == true
            || Application.isFocused == false )
            return;

        //이동은 유지 중 반복 출력하지 않고 정지를 포함한 입력값 변화만 기록
        if ( _debugInputEntry.ActionPath == "Player/Move" )
        {
            Vector2 input = _debugInputAction.ReadValue<Vector2> ( );

            if ( input == _debugMoveInput )
                return;

            _debugMoveInput = input;
            Debug.Log ( $"[단축키 입력] {_debugInputEntry.ActionPath} / 이동 값={input}" );
            return;
        }

        if ( _debugInputAction.WasPressedThisFrame ( ) == false )
            return;

        //같은 기능의 보조 키로 입력됐는지도 구분하며 실제 행동 승인 여부와는 분리
        string control = _debugInputAction.activeControl?.path ?? "이번 프레임 해제됨";
        Debug.Log ( $"[단축키 입력] {_debugInputEntry.ActionPath}"
            + $" / 선택 배정={GetBindingDisplay ( _debugInputEntry )} / 실제 입력={control}" );
    }
#endif

    /// <summary>
    /// 장치 연결 여부와 무관한 허용 버튼 경로 구성
    /// </summary>
    void BuildAllowedPaths ( )
    {
        //시작 시 장치 생성 순서에 의존하지 않고 저장 배정 검사에 사용할 경로 구성
        foreach ( var control in InputSystem.LoadLayout ( "Keyboard" ).controls )
        {
            if ( control.layout.ToString ( ) != "Key" )
                continue;

            _allowedPaths.Add ( $"<Keyboard>/{control.name}" );
        }

        _allowedPaths.Add ( "<Mouse>/leftButton" );
        _allowedPaths.Add ( "<Mouse>/rightButton" );
        _allowedPaths.Add ( "<Mouse>/middleButton" );
        _allowedPaths.Add ( "<Mouse>/forwardButton" );
        _allowedPaths.Add ( "<Mouse>/backButton" );
    }

    /// <summary>
    /// 실제 사용하는 액션의 개별 버튼 배정 등록
    /// </summary>
    /// <param name="path">공용 액션 경로</param>
    /// <param name="name">기능명</param>
    void AddEntries ( string path , string name )
    {
        InputAction action = _actions.FindAction ( path , true );

        foreach ( InputBinding binding in action.bindings )
        {
            if ( binding.isComposite == true
                || _allowedPaths.Contains ( binding.path ) == false )
                continue;

            string label = binding.isPartOfComposite
                ? $"{name}/{binding.name}"
                : name;

            _entries.Add ( new KeyBindingEntry ( path , binding.id , label ) );
        }
    }

    /// <summary>
    /// 현재 배정의 비활성 편집 복사본 생성
    /// </summary>
    /// <returns>편집용 액션 에셋</returns>
    InputActionAsset CreateDraft ( )
    {
        InputActionAsset draft = UnityEngine.Object.Instantiate ( _actions );
        draft.Disable ( );
        return draft;
    }

    /// <summary>
    /// 편집 복사본 해제
    /// </summary>
    void ReleaseDraft ( )
    {
        if ( _draftActions != null )
            UnityEngine.Object.Destroy ( _draftActions );

        _draftActions = null;
    }

    /// <summary>
    /// 저장 기록을 현재 바인딩에 연결
    /// </summary>
    /// <param name="data">저장된 변경 배정</param>
    /// <param name="reason">복원 실패 사유</param>
    /// <returns>현재 구성과 연결 성공 여부</returns>
    bool RestoreDraft (
        KeyBindingSaveData data , out string reason )
    {
        reason = string.Empty;

        foreach ( KeyBindingOverride saved in data.Bindings )
        {
            if ( saved == null
                || string.IsNullOrEmpty ( saved.BindingId ) == true
                || string.IsNullOrEmpty ( saved.Path ) == true )
            {
                reason = "단축키 변경 기록이 올바르지 않습니다.";
                return false;
            }

            KeyBindingEntry entry = _entries.Find (
                item => string.Equals ( item.BindingId.ToString ( ) , saved.BindingId ,
                    StringComparison.OrdinalIgnoreCase ) );

            if ( entry == null )
            {
                reason = $"현재 구성에 없는 바인딩입니다: {saved.BindingId}";
                return false;
            }

            InputAction action = FindBinding ( _draftActions , entry , out int index );

            action.ApplyBindingOverride ( index , saved.Path );
        }

        return true;
    }

    /// <summary>
    /// 복원한 최종 배정의 입력 규칙 검사
    /// </summary>
    /// <param name="reason">배정 실패 사유</param>
    /// <returns>최종 배정 사용 가능 여부</returns>
    bool ValidateDraft ( out string reason )
    {
        reason = string.Empty;

        foreach ( KeyBindingEntry entry in _entries )
        {
            InputAction action = FindBinding ( _draftActions , entry , out int index );

            string path = action.bindings [ index ].effectivePath;

            if ( CanAssign ( entry , path , out reason ) == false )
                return false;
        }

        return true;
    }

    /// <summary>
    /// 편집값 변경과 적용을 요청할 수 있는 수명 확인
    /// </summary>
    /// <param name="reason">요청 불가 사유</param>
    /// <returns>편집 요청 가능 여부</returns>
    bool CanEditDraft ( out string reason )
    {
        reason = string.Empty;

        if ( IsEditing == false )
        {
            reason = "단축키 설정을 편집 중이 아닙니다.";
            return false;
        }

        if ( IsCapturingInput == true )
        {
            reason = "키 입력 대기를 먼저 종료하세요.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 직접 지정과 실제 키 입력이 공유하는 배정 검사/변경
    /// </summary>
    /// <param name="entry">변경할 배정</param>
    /// <param name="path">배정할 입력 경로</param>
    /// <param name="reason">변경 실패 사유</param>
    /// <returns>편집 배정 변경 성공 여부</returns>
    bool TryChangeDraftBinding (
        KeyBindingEntry entry , string path , out string reason )
    {
        if ( CanAssign ( entry , path , out reason ) == false )
            return false;

        InputAction action = FindBinding ( _draftActions , entry , out int index );
        string before = action.GetBindingDisplayString ( index );
        action.ApplyBindingOverride ( index , path );
        _rebindMessage = $"{entry.Name}: {before} → {action.GetBindingDisplayString ( index )}";

        //호출부에서 직접 지정 완료 또는 키 해제 대기 상태를 확정한 뒤 알림
        return true;
    }

    /// <summary>
    /// 허용 입력과 예약 키/다른 배정 충돌 검사
    /// </summary>
    /// <param name="entry">변경 대상</param>
    /// <param name="path">배정할 입력 경로</param>
    /// <param name="reason">배정 실패 사유</param>
    /// <returns>배정 가능 여부</returns>
    bool CanAssign (
        KeyBindingEntry entry , string path , out string reason )
    {
        reason = string.Empty;

        if ( _allowedPaths.Contains ( path ) == false )
        {
            reason = "키보드와 마우스 버튼만 배정할 수 있습니다.";
            return false;
        }

        if ( _reservedPaths.Contains ( path ) == true )
        {
            reason = "현재 시스템에서 예약한 키입니다.";
            return false;
        }

        foreach ( KeyBindingEntry otherEntry in _entries )
        {
            if ( otherEntry.BindingId == entry.BindingId )
                continue;

            InputAction other = FindBinding (
                _draftActions , otherEntry , out int index );

            string otherPath = other.bindings [ index ].effectivePath;

            if ( string.Equals ( path , otherPath ,
                StringComparison.OrdinalIgnoreCase ) == false )
                continue;

            reason = $"{otherEntry.Name}에 이미 배정된 키입니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 누른 버튼을 확인해 편집 배정 변경
    /// </summary>
    void ReadRebindInput ( )
    {
        //여러 키를 동시에 눌러도 후보 선택보다 취소를 먼저 처리
        if ( Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame == true )
        {
            CancelRebind ( );
            return;
        }

        foreach ( ButtonControl button in EnumerateButtons ( ) )
        {
            if ( button.wasPressedThisFrame == false )
                continue;

            string device = button.device is Keyboard ? "<Keyboard>" : "<Mouse>";
            string path = $"{device}/{button.name}";

            if ( TryChangeDraftBinding ( _captureEntry , path , out string reason ) == false )
            {
                _rebindMessage = reason;
                OnEditChanged?.Invoke ( );
                return;
            }

            //변경은 공통 함수에서 완료하고 키 해제 대기 상태를 확정한 뒤 알림
            WaitForFinishRelease ( );
            OnEditChanged?.Invoke ( );
            return;
        }
    }

    /// <summary>
    /// 완료/취소 입력 해제 대기로 전환
    /// </summary>
    void WaitForFinishRelease ( )
    {
        _captureEntry = null;
        _captureState = CaptureState.WaitingForFinishRelease;
    }

    /// <summary>
    /// 변경한 배정만 저장 데이터로 구성
    /// </summary>
    /// <returns>기본 배정을 제외한 변경 목록</returns>
    KeyBindingSaveData CreateSaveData ( )
    {
        List<KeyBindingOverride> bindings = new ( );

        foreach ( KeyBindingEntry entry in _entries )
        {
            InputAction action = FindBinding ( _draftActions , entry , out int index );

            string path = action.bindings [ index ].overridePath;

            if ( path == null )
                continue;

            bindings.Add ( new KeyBindingOverride ( entry.BindingId.ToString ( ) , path ) );
        }

        return new KeyBindingSaveData ( bindings );
    }

    /// <summary>
    /// 원래 활성 상태를 유지하며 편집 배정 실제 적용
    /// </summary>
    void ApplyDraft ( )
    {
        List<InputAction> enabledActions = new ( );

        //변경 중 입력을 읽지 않게 하되 원래 비활성 상태였던 액션은 복구 대상에서 제외
        foreach ( KeyBindingEntry entry in _entries )
        {
            InputAction action = _actions.FindAction ( entry.ActionPath , true );

            if ( action.enabled == false )
                continue;

            //같은 액션의 다음 배정에서는 이미 비활성 상태이므로 한 번만 보관
            enabledActions.Add ( action );
            action.Disable ( );
        }

        //공용 액션 객체는 교체하지 않아 기존 InputHandler 참조와 구독 유지
        foreach ( KeyBindingEntry entry in _entries )
        {
            InputAction source = FindBinding ( _draftActions , entry , out int sourceIndex );
            InputAction target = FindBinding ( _actions , entry , out int targetIndex );

            string path = source.bindings [ sourceIndex ].overridePath;

            if ( path == null )
                target.RemoveBindingOverride ( targetIndex );
            else
                target.ApplyBindingOverride ( targetIndex , path );
        }

        foreach ( InputAction action in enabledActions )
            action.Enable ( );
    }

    /// <summary>
    /// 바인딩 식별자로 현재 액션과 인덱스 조회
    /// </summary>
    /// <param name="actions">조회할 액션 에셋</param>
    /// <param name="entry">배정 식별 정보</param>
    /// <param name="index">액션 내부 바인딩 인덱스</param>
    /// <returns>해당 입력 액션</returns>
    InputAction FindBinding (
        InputActionAsset actions , KeyBindingEntry entry , out int index )
    {
        InputAction action = actions.FindAction ( entry.ActionPath , true );
        index = action.GetBindingIndex ( new InputBinding { id = entry.BindingId } );
        return action;
    }

    /// <summary>
    /// 재지정 가능한 현재 장치의 버튼 열거
    /// </summary>
    /// <returns>키보드 키와 마우스 버튼</returns>
    IEnumerable<ButtonControl> EnumerateButtons ( )
    {
        if ( Keyboard.current != null )
        {
            foreach ( KeyControl key in Keyboard.current.allKeys )
                yield return key;
        }

        Mouse mouse = Mouse.current;

        if ( mouse == null )
            yield break;

        yield return mouse.leftButton;
        yield return mouse.rightButton;
        yield return mouse.middleButton;
        yield return mouse.forwardButton;
        yield return mouse.backButton;
    }

    /// <summary>
    /// 입력 대기 전후 버튼 유지 여부 조회
    /// </summary>
    /// <returns>누른 상태의 버튼 존재 여부</returns>
    bool IsAnyButtonPressed ( )
    {
        foreach ( ButtonControl button in EnumerateButtons ( ) )
        {
            if ( button.isPressed == true )
                return true;
        }

        return false;
    }

    /// <summary>
    /// 입력 대기와 편집 객체/이벤트 참조 정리
    /// </summary>
    public void Dispose ( )
    {
#if UNITY_EDITOR
        StopDebugInputLog ( );
#endif
        //앱 종료는 정상 취소와 구분하며
        //화면 알림이나 미적용 설정 저장을 실행하지 않음
        _captureEntry = null;
        _captureState = CaptureState.None;
        _consumedFrame = -1;
        ReleaseDraft ( );

        OnBindingsChanged = null;
        OnEditChanged = null;
    }
}
