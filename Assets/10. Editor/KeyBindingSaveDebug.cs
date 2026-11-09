#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 단축키 저장 검증 - 격리된 파일과 액션으로 실패/복원/재시도 확인
/// </summary>
public class KeyBindingSaveDebug : IDisposable
{
    string _directory;                 //이번 검증에서만 사용할 고유 폴더
    string _temporaryPath;             //실제 임시 파일 작성 실패를 유도할 경로
    InputActionAsset _actions;         //현재 플레이 입력과 분리된 액션 복사본
    KeyBindingStorage _storage;        //테스트 폴더를 사용하는 기존 저장소
    KeyBindingSettings _settings;      //각 검증 단계의 기존 설정 객체
    UnityEngine.Object _context;       //Console에서 선택할 Inspector 대상
    int _checkCount;                   //수행한 확인 항목 수
    int _failureCount;                 //기대와 다른 결과 수
    StringBuilder _failures = new ( ); //실패 항목의 실제 결과

    /// <summary>
    /// 실제 저장 경로와 플레이 액션에서 분리한 검증 환경 생성
    /// </summary>
    /// <param name="source">복사할 프로젝트 공용 액션</param>
    /// <param name="context">로그의 Inspector 대상</param>
    public KeyBindingSaveDebug (
        InputActionAsset source , UnityEngine.Object context )
    {
        _directory = Path.Combine ( Application.temporaryCachePath,
            "KeyBindingSaveDebug-" + Guid.NewGuid ( ).ToString ( "N" ) );
        Directory.CreateDirectory ( _directory );

        _storage = new KeyBindingStorage ( _directory );
        _temporaryPath = _storage.DebugSavePath + ".tmp";
        _context = context;

        //공용 에셋을 교체하거나 활성화하지 않고 복사본의 바인딩만 검증
        _actions = UnityEngine.Object.Instantiate ( source );
        _actions.Disable ( );
    }

    /// <summary>
    /// 실제 쓰기 실패 후 파일/배정 유지와 같은 편집값의 재시도 확인
    /// </summary>
    /// <param name="report">전체 결과와 실패 항목</param>
    /// <returns>모든 확인 항목의 통과 여부</returns>
    public bool RunWriteFailureTest ( out string report )
    {
        if ( Check ( CreateSession ( out string reason ),
            "쓰기 검증 초기화", reason ) == false )
            return Finish ( "쓰기 실패/재시도", out report );

        if ( Check ( SaveBaseline ( out KeyBindingEntry entry, out reason ),
            "기준 H 저장/적용", reason ) == false )
            return Finish ( "쓰기 실패/재시도", out report );

        string originalFile = File.ReadAllText ( _storage.DebugSavePath );
        Dictionary<Guid, string> originalPaths = CaptureAppliedPaths ( );

        if ( Check ( _settings.TrySetDraftBinding ( entry, "<Keyboard>/m", out reason ),
            "편집값 M 준비", reason ) == false )
            return Finish ( "쓰기 실패/재시도", out report );

        string draftBefore = _settings.GetDraftBindingDisplay ( entry );
        int notifications = 0;
        //실패 때 적용 완료 알림이 발생하지 않는지도 실제 이벤트로 확인
        _settings.OnBindingsChanged += ( ) => notifications++;

        //가짜 실패 분기 없이 기존 File.WriteAllText가 실패하도록 같은 경로에 디렉터리 생성
        Directory.CreateDirectory ( _temporaryPath );
        bool saved = _settings.TryApply ( out reason );

        Check ( saved == false, "쓰기 실패 반환", $"기대=False / 실제={saved} / 사유={reason}" );
        Check ( string.IsNullOrEmpty ( reason ) == false, "쓰기 실패 사유", reason );
        Check ( File.ReadAllText ( _storage.DebugSavePath ) == originalFile,
            "기존 파일 유지", "기대=쓰기 실패 전 파일 내용" );
        Check ( MatchesAppliedPaths ( originalPaths ),
            "전체 적용 배정 유지", $"선택 배정 기대=<Keyboard>/h / 실제={ReadAppliedPath ( entry )}" );

        string draftAfter = _settings.GetDraftBindingDisplay ( entry );
        Check ( _settings.IsEditing == true && draftAfter == draftBefore,
            "편집 상태와 M 유지", $"편집={_settings.IsEditing} / 기대={draftBefore} / 실제={draftAfter}" );
        Check ( notifications == 0, "실패 시 적용 완료 알림 없음",
            $"기대=0 / 실제={notifications}" );

        //현재 편집값을 다시 만들지 않고 방해 조건만 제거한 뒤 같은 API 재호출
        Directory.Delete ( _temporaryPath );
        bool retried = _settings.TryApply ( out reason );
        Check ( retried, "같은 편집값 저장 재시도", reason );
        Check ( ReadAppliedPath ( entry ) == "<Keyboard>/m",
            "재시도 후 실제 적용 M", $"기대=<Keyboard>/m / 실제={ReadAppliedPath ( entry )}" );
        Check ( notifications == 1, "재시도 성공 알림 한 번",
            $"기대=1 / 실제={notifications}" );

        bool loaded = _storage.TryLoad ( out KeyBindingSaveData data, out reason );
        bool savedM = loaded == true && data != null && data.Bindings.Count == 1
            && data.Bindings [ 0 ].BindingId == entry.BindingId.ToString ( )
            && data.Bindings [ 0 ].Path == "<Keyboard>/m";
        Check ( savedM, "재시도 후 파일 기록 M", $"파일 읽기={loaded} / 사유={reason}" );

        return Finish ( "쓰기 실패/재시도", out report );
    }

    /// <summary>
    /// 손상 기록의 기본값 복귀/원본 보존과 이후 정상 복원 확인
    /// </summary>
    /// <param name="report">전체 결과와 실패 항목</param>
    /// <returns>모든 확인 항목의 통과 여부</returns>
    public bool RunCorruptFileTests ( out string report )
    {
        if ( Check ( CreateSession ( out string reason ),
            "손상 검증 초기화", reason ) == false )
            return Finish ( "손상 파일 복원", out report );

        //기본값은 현재 적용값이 아니라 첫 Init이 원본 배정에서 준비한 값으로 보관
        Dictionary<Guid, string> defaults = CaptureAppliedPaths ( );

        if ( Check ( SaveBaseline ( out KeyBindingEntry first, out reason ),
            "정상 복원용 H 저장", reason ) == false )
            return Finish ( "손상 파일 복원", out report );

        string validFile = File.ReadAllText ( _storage.DebugSavePath );
        Dictionary<Guid, string> validPaths = CaptureAppliedPaths ( );
        KeyBindingEntry second = _settings.Entries [ 1 ];
        KeyBindingOverride valid = new ( first.BindingId.ToString ( ), "<Keyboard>/h" );

        RunCorruptCase ( "JSON 구문 손상", "{ broken", defaults );
        RunCorruptCase ( "필수 목록 누락", "{}", defaults );
        RunCorruptCase ( "목록 null", "{\"_bindings\":null}", defaults );
        RunCorruptCase ( "목록 타입 오류", "{\"_bindings\":\"invalid\"}", defaults );

        //유효한 첫 기록을 먼저 반영한 뒤 실패시켜 부분 복원의 폐기까지 확인
        RunCorruptCase ( "미등록 ID", CreateJson ( valid,
            new KeyBindingOverride ( Guid.NewGuid ( ).ToString ( ), "<Keyboard>/m" ) ), defaults );
        RunCorruptCase ( "예약 키", CreateJson ( valid,
            new KeyBindingOverride ( second.BindingId.ToString ( ), "<Keyboard>/t" ) ), defaults );
        RunCorruptCase ( "중복 키", CreateJson ( valid,
            new KeyBindingOverride ( second.BindingId.ToString ( ), "<Keyboard>/h" ) ), defaults );

        //손상 복원 실패 후에도 정상 파일을 읽는 새 객체가 정상 배정을 복원하는지 확인
        File.WriteAllText ( _storage.DebugSavePath, validFile );
        bool restored = CreateSession ( out reason );
        Check ( restored && string.IsNullOrEmpty ( _settings.LoadError ) == true,
            "손상 후 정상 파일 복원", $"복원={restored} / 사유={reason}" );
        Check ( MatchesAppliedPaths ( validPaths ),
            "정상 파일의 전체 배정 복원", $"선택 배정 기대=<Keyboard>/h / 실제={ReadAppliedPath ( _settings.Entries [ 0 ] )}" );
        Check ( File.ReadAllText ( _storage.DebugSavePath ) == validFile,
            "정상 복원 중 파일 유지", "기대=정상 저장 파일 내용" );

        //목록 누락은 거절하되 기본값 저장으로 생긴 명시적인 빈 목록은 정상 허용
        File.WriteAllText ( _storage.DebugSavePath, CreateJson ( ) );
        bool defaultsLoaded = CreateSession ( out reason );
        Check ( defaultsLoaded && string.IsNullOrEmpty ( _settings.LoadError ) == true
            && MatchesAppliedPaths ( defaults ),
            "명시적인 빈 목록의 기본값 복원", $"기대=True / 실제={defaultsLoaded} / 사유={reason}" );

        return Finish ( "손상 파일 복원", out report );
    }

    /// <summary>
    /// 이전 객체를 정리한 뒤 같은 테스트 파일에서 새 실행의 초기 복원 수행
    /// </summary>
    /// <param name="reason">초기 복원 실패 사유</param>
    /// <returns>초기 복원 성공 여부</returns>
    bool CreateSession ( out string reason )
    {
        _settings?.Dispose ( );
        _settings = new KeyBindingSettings ( _actions, _storage );
        return _settings.Init ( out reason );
    }

    /// <summary>
    /// 정상 API로 첫 배정을 H로 저장하여 실패 전 기준 구성
    /// </summary>
    /// <param name="entry">검증에 사용할 첫 배정</param>
    /// <param name="reason">준비 실패 사유</param>
    /// <returns>정상 기준 저장 성공 여부</returns>
    bool SaveBaseline ( out KeyBindingEntry entry , out string reason )
    {
        //현재 등록 순서의 첫 항목은 이동/up의 W 배정
        entry = _settings.Entries [ 0 ];
        reason = string.Empty;

        if ( _settings.TryBeginEdit ( ) == false )
        {
            reason = "검증용 편집을 시작할 수 없습니다.";
            return false;
        }

        if ( _settings.TrySetDraftBinding ( entry, "<Keyboard>/h", out reason ) == false )
            return false;

        return _settings.TryApply ( out reason );
    }

    /// <summary>
    /// 손상 파일 한 사례의 복원 거절/전체 기본값/원본 보존 확인
    /// </summary>
    /// <param name="name">검증 사례 이름</param>
    /// <param name="json">테스트 파일에 기록할 내용</param>
    /// <param name="defaults">원본 에셋의 전체 기본 배정</param>
    void RunCorruptCase (
        string name , string json , Dictionary<Guid, string> defaults )
    {
        File.WriteAllText ( _storage.DebugSavePath, json );
        bool loaded = CreateSession ( out string reason );

        Check ( loaded == false && string.IsNullOrEmpty ( _settings.LoadError ) == false,
            $"{name}: 복원 거절과 오류 제공", $"기대=False / 실제={loaded} / 사유={reason}" );
        Check ( MatchesAppliedPaths ( defaults ) && _settings.IsEditing == false,
            $"{name}: 전체 기본값과 편집 정리", $"편집 종료 기대=True / 실제={_settings.IsEditing == false}" );
        Check ( File.ReadAllText ( _storage.DebugSavePath ) == json,
            $"{name}: 원본 미덮어쓰기", "기대=입력한 손상 파일 내용" );
    }

    /// <summary>
    /// 기존 저장 데이터 형식으로 검증 기록 직렬화
    /// </summary>
    /// <param name="bindings">검증할 배정 기록</param>
    /// <returns>실제 저장 형식의 JSON</returns>
    string CreateJson ( params KeyBindingOverride[] bindings )
    {
        KeyBindingSaveData data = new ( new List<KeyBindingOverride> ( bindings ) );
        return JsonUtility.ToJson ( data, true );
    }

    /// <summary>
    /// 테스트 액션에 적용된 전체 등록 배정 보관
    /// </summary>
    /// <returns>바인딩 ID별 실제 적용 경로</returns>
    Dictionary<Guid, string> CaptureAppliedPaths ( )
    {
        Dictionary<Guid, string> paths = new ( );

        foreach ( KeyBindingEntry entry in _settings.Entries )
            paths.Add ( entry.BindingId, ReadAppliedPath ( entry ) );

        return paths;
    }

    /// <summary>
    /// 보관한 배정과 현재 전체 적용값 비교
    /// </summary>
    /// <param name="expected">기대한 바인딩 ID별 경로</param>
    /// <returns>전체 배정 일치 여부</returns>
    bool MatchesAppliedPaths ( Dictionary<Guid, string> expected )
    {
        foreach ( KeyBindingEntry entry in _settings.Entries )
        {
            if ( ReadAppliedPath ( entry ) != expected [ entry.BindingId ] )
                return false;
        }

        return true;
    }

    /// <summary>
    /// 표시 문자열이 아닌 테스트 액션의 실제 적용 경로 조회
    /// </summary>
    /// <param name="entry">조회할 배정</param>
    /// <returns>실제 적용 입력 경로</returns>
    string ReadAppliedPath ( KeyBindingEntry entry )
    {
        InputAction action = _actions.FindAction ( entry.ActionPath, true );
        int index = action.GetBindingIndex ( new InputBinding { id = entry.BindingId } );
        return action.bindings [ index ].effectivePath;
    }

    /// <summary>
    /// 기대와 실제 결과를 비교한 항목의 통과 여부 기록
    /// </summary>
    /// <param name="passed">기대 결과와 일치하는지 여부</param>
    /// <param name="name">확인 항목</param>
    /// <param name="detail">기대한 값과 실제 결과</param>
    /// <returns>해당 항목의 통과 여부</returns>
    bool Check ( bool passed , string name , string detail )
    {
        _checkCount++;
        string message = $"[단축키 저장 검증] {name}: {( passed == true ? "통과" : "실패" )} / {detail}";

        if ( passed == true )
        {
            Debug.Log ( message, _context );
        }
        else
        {
            _failureCount++;
            _failures.AppendLine ( message );
            Debug.LogError ( message, _context );
        }

        return passed;
    }

    /// <summary>
    /// 검증 묶음의 전체 결과 구성
    /// </summary>
    /// <param name="name">검증 묶음 이름</param>
    /// <param name="report">통과 수와 실패 상세</param>
    /// <returns>전체 항목 통과 여부</returns>
    bool Finish ( string name , out string report )
    {
        report = $"{name}: {_checkCount - _failureCount}/{_checkCount} 통과";

        if ( _failureCount != 0 )
            report += "\n" + _failures.ToString ( );

        return _failureCount == 0;
    }

    /// <summary>
    /// 검증 객체와 직접 만든 파일/빈 폴더 정리
    /// </summary>
    public void Dispose ( )
    {
        _settings?.Dispose ( );
        _settings = null;

        if ( _actions != null )
            UnityEngine.Object.Destroy ( _actions );

        _actions = null;

        //조기 반환/예외에도 방해 디렉터리 또는 남은 임시 파일을 해당 경로에서만 제거
        if ( Directory.Exists ( _temporaryPath ) == true )
            Directory.Delete ( _temporaryPath );
        else
            File.Delete ( _temporaryPath );

        File.Delete ( _storage.DebugSavePath );
        //재귀 삭제 없이 이번 검증이 만든 빈 폴더만 제거
        Directory.Delete ( _directory );
    }
}
#endif
