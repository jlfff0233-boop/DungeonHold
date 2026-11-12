using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LJPlay;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 흐름 상태
/// </summary>
public enum SceneFlowState
{
    Idle,
    OpeningLobby,
    Lobby,
    PreparingEntry,
    Loading,
    InitializingPlay,
    PreparingMap,
    MapFailed,
    StartingStage,
    StartFailed,
    Playing,
    RequestingEnd,
    WaitingForEnd,
    ReadyToLoad,
    InitFailed,
    LoadFailed,
    Closed,
    ResultBackup
}

/// <summary>
/// 로비와 플레이 이동 조율
/// </summary>
public class SceneFlow
{
    /// <summary>
    /// 비동기 씬 작업 단계
    /// </summary>
    enum LoadStep
    {
        Single,     //타이틀 또는 기존 단일 플레이 씬
        PlayBase,   //공용 플레이 씬
        UnloadMap,  //이전 맵 해제
        Map         //대상 맵 로드
    }

    string _titlePath; //타이틀 씬 경로
    string _playPath; //단일 씬 구성의 플레이 경로
    string _playBasePath; //공용 플레이 씬 경로
    SoundManager _sound; //사운드 관리자
    StageEntry _entry = new StageEntry ( ); //입장 선택
    IReadOnlyList<LobbyStageInfo> _stages; //입장에 사용한 스테이지 목록
    SceneMove _move = new SceneMove ( ); //종료 대기
    LobbyController _lobby; //현재 로비
    PlayScene _play; //현재 플레이 연결부
    SaveManager _resultBackupSave; //결과 백업 동안 사용권을 보관한 관리자
    PlayScene _endOwner; //종료 요청 연결 소유자
    IStageEndLink _endLink; //종료와 마지막 사건 전달 요청 대상
    SoundId _bgmId; //현재 씬의 배경음
    AsyncOperation _operation; //실행 중 씬 로드/해제
    LoadStep _loadStep; //현재 씬 작업
    Scene _mapScene; //현재 로드된 맵 씬
    int _readyToLoadFrame; //로딩 화면 표시를 요청한 프레임
    int _loadStepCount; //이번 이동의 씬 작업 수
    int _completedLoadSteps; //완료된 씬 작업 수
    SceneFlowState _state; //현재 처리 단계
    string _path; //요청한 이동 대상
    string _error; //마지막 실패 사유
    int _stageId; //실제 시작한 도전
    int _mapRequestId; //맵 준비 요청 번호
    int _initialStageCurrency; //준비된 맵의 시작 재화
    MissionDefenseRule _defenseRule; //준비된 맵의 방어 규칙
    Vector3? _companionPriorityPosition; //준비된 맵의 동료 기준 위치
    bool _shouldOpenLobby; //이동 후 로비 연결 여부
    bool _restoreNoticePending; //타이틀에서 소비할 복원 성공 알림
    bool _resultBackupRunning; //응답 처리 전까지 결과 백업 요청 차단

    /// <summary>
    /// 흐름 상태
    /// </summary>
    public SceneFlowState State => _state;

    /// <summary>
    /// 마지막 오류
    /// </summary>
    public string Error => _error;

    /// <summary>
    /// 준비할 입장 선택
    /// </summary>
    public StageEntrySelection? PendingEntry => _entry.Pending;

    /// <summary>
    /// 현재 도전 선택
    /// </summary>
    public StageEntrySelection? CurrentEntry => _entry.Current;

    /// <summary>
    /// 맵 준비 요청 번호
    /// </summary>
    public int MapRequestId => _mapRequestId;

    /// <summary>
    /// 씬 로드와 플레이 준비를 포함한 이동 진행률
    /// </summary>
    public float LoadProgress => GetLoadProgress ( );

    /// <summary>
    /// 공용 플레이 씬 사용 여부
    /// </summary>
    bool UsesPlayBase => string.IsNullOrWhiteSpace ( _playBasePath ) == false;

    /// <summary>
    /// 결과 화면의 새 백업/복원 요청 가능 여부
    /// </summary>
    public bool CanUseResultBackup => _state == SceneFlowState.Playing
        && _operation == null && _play.isActiveAndEnabled && _play.IsStageCompleted ( _stageId );

    /// <summary>
    /// 상태 변경
    /// </summary>
    public event Action OnChanged;

    /// <summary>
    /// 씬 흐름 생성
    /// </summary>
    /// <param name="titlePath">타이틀 경로</param>
    /// <param name="playPath">단일 씬 구성의 플레이 경로</param>
    /// <param name="sound">사운드 관리자</param>
    /// <param name="playBasePath">공용 플레이 씬의 Scene List 경로</param>
    public SceneFlow ( string titlePath , string playPath , SoundManager sound ,
        string playBasePath = null )
    {
        _titlePath = titlePath;
        _playPath = playPath;
        _playBasePath = playBasePath;
        _sound = sound;
        _sound.OnReadyChanged += HandleSoundReady;
        _move.OnChanged += HandleMoveChanged;
    }

    /// <summary>
    /// 종료 대기와 로드 갱신
    /// </summary>
    public void Update ( )
    {
        //Start와 로드 완료 알림의 호출 순서에 의존하지 않음
        if ( _state == SceneFlowState.Idle && _operation == null && _shouldOpenLobby == true )
        {
            _shouldOpenLobby = false;
            TryOpenLobby ( _lobby , out _ );
        }

        if ( _state == SceneFlowState.WaitingForEnd
            && _move.TryBeginLoad ( out string path ) == true )
        {
            _path = path;
            SetState ( SceneFlowState.ReadyToLoad );
        }

        //종료 알림 스택 밖에서 로딩 화면 표시 프레임을 확보한 뒤 씬 로드
        if ( _state == SceneFlowState.ReadyToLoad && _operation == null
            && Time.frameCount != _readyToLoadFrame )
            BeginLoad ( );
    }

    #region ----- 공통 연결 -----

    /// <summary>
    /// 현재 씬 배경음 선택과 재생 요청
    /// </summary>
    /// <param name="id">배경음 식별자</param>
    public void SetBgm ( SoundId id )
    {
        _bgmId = id;
        HandleSoundReady ( _sound.IsReady );
    }

    /// <summary>
    /// 준비 완료 후 현재 배경음 요청
    /// </summary>
    /// <param name="isReady">사운드 준비 여부</param>
    void HandleSoundReady ( bool isReady )
    {
        if ( isReady == true )
            _sound.Play ( _bgmId );
    }

    /// <summary>
    /// 종료 요청 연결
    /// </summary>
    /// <param name="owner">플레이 소유자</param>
    /// <param name="link">요청 대상</param>
    /// <returns>연결 여부</returns>
    public bool TryBindEndLink ( PlayScene owner , IStageEndLink link )
    {
        if ( _state == SceneFlowState.Closed || owner == null || link == null )
            return false;

        if ( _endLink != null )
            return _endOwner == owner && ReferenceEquals ( _endLink , link );

        if ( _play != null && _play != owner )
            return false;

        //초기화 전후 모두 연결 가능하며 종료 요청 시 소유자를 확인
        _endOwner = owner;
        _endLink = link;
        return true;
    }

    /// <summary>
    /// 종료 요청 연결 해제
    /// </summary>
    /// <param name="owner">플레이 소유자</param>
    /// <param name="link">해제 대상</param>
    public void UnbindEndLink ( PlayScene owner , IStageEndLink link )
    {
        if ( _endOwner != owner || ReferenceEquals ( _endLink , link ) == false )
            return;

        _endOwner = null;
        _endLink = null;
    }

    #endregion

    #region ----- 로비 -----

    /// <summary>
    /// 타이틀 도착 연결
    /// </summary>
    /// <param name="lobby">로비</param>
    public void AttachTitle ( LobbyController lobby )
    {
        if ( _state != SceneFlowState.Idle
            && ( _state != SceneFlowState.Loading || _path != _titlePath ) )
            return;

        _lobby = lobby;
        SetBgm ( SoundId.TitleBgm );
        _move.Dispose ( );
        _entry.CompleteReturn ( );
        _stages = null;
        _stageId = 0;
        SetState ( SceneFlowState.Idle );

        //복귀 연결은 로드 완료와 타이틀 도착을 모두 확인한 뒤 Update에서 실행
    }

    /// <summary>
    /// 로비 연결
    /// </summary>
    /// <param name="lobby">로비</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>연결 여부</returns>
    public bool TryOpenLobby ( LobbyController lobby , out string reason )
    {
        reason = null;
        if ( _state != SceneFlowState.Idle || _operation != null )
            return false;

        _lobby = lobby;
        //직접 열기 요청도 복귀 시 남은 자동 요청을 소비
        _shouldOpenLobby = false;
        _state = SceneFlowState.OpeningLobby;

        if ( lobby.TryOpen ( out reason ) == false )
        {
            _error = reason;
            SetState ( SceneFlowState.Idle );
            return false;
        }

        _error = null;
        SetState ( SceneFlowState.Lobby );
        return true;
    }

    /// <summary>
    /// 플레이 이동 준비
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>접수 여부</returns>
    public bool TryEnterStage ( out string reason )
    {
        reason = null;

        if ( _state != SceneFlowState.Lobby || _operation != null )
            return false;

        IReadOnlyList<LobbyStageInfo> stages = _lobby.Stages;
        string mapId = _lobby.SelectedStage?.MapId;
        MissionDifficulty difficulty = _lobby.SelectedDifficulty;
        if ( TryResolveStagePath ( stages , mapId , out string path , out reason ) == false )
            return false;

        if ( UsesPlayBase == true
            && TryCheckMapSupport ( stages , mapId , difficulty , out reason ) == false )
            return false;

        //로비 저장 알림 중에도 중복 이동 요청 차단
        _state = SceneFlowState.PreparingEntry;
        EntryPreparationResult result = _lobby.TryPrepareEntry ( out StageEntrySelection entry );

        if ( result != EntryPreparationResult.Success )
        {
            reason = $"입장 준비 실패: {result}";
            _error = reason;
            SetState ( SceneFlowState.Lobby );
            return false;
        }

        //저장 알림 중 선택이 바뀌면 앞서 확인한 맵 씬으로 이동하지 않음
        if ( UsesPlayBase == true && ( entry.MapId != mapId || entry.Difficulty != difficulty ) )
        {
            reason = "입장 준비 중 스테이지 선택이 변경됐습니다.";
            _lobby.ResumeRequests ( );
            _error = reason;
            SetState ( SceneFlowState.Lobby );
            return false;
        }

        _entry.Prepare ( entry );

        if ( _lobby.TryReleaseProgress ( out reason ) == false )
        {
            _entry.Cancel ( );
            _lobby.ResumeRequests ( );
            _error = reason;
            SetState ( SceneFlowState.Lobby );
            return false;
        }

        //로비 해제 후에도 순서 조회에 필요한 설정 목록 유지
        _stages = stages;
        _path = path;
        _error = null;
        SetState ( SceneFlowState.ReadyToLoad );

        return true;
    }

    /// <summary>
    /// 저장 후 로비 해제
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>해제 여부</returns>
    public bool TryCloseLobby ( out string reason )
    {
        reason = null;

        if ( _state != SceneFlowState.Lobby || _operation != null )
            return false;

        _state = SceneFlowState.PreparingEntry;

        if ( _lobby.TrySavePendingProgress ( ) == false )
        {
            reason = "로비의 미저장 진행을 처리하지 못했습니다.";
            _error = reason;
            SetState ( SceneFlowState.Lobby );


            return false;
        }

        if ( _lobby.TryReleaseProgress ( out reason ) == false )
        {
            _error = reason;
            SetState ( SceneFlowState.Lobby );

            return false;
        }

        //닫기 완료 알림 이후 자동 열기가 다시 실행되지 않도록 해제
        _shouldOpenLobby = false;
        _error = null;
        SetState ( SceneFlowState.Idle );

        return true;
    }

    #endregion

    #region ----- 플레이 준비 -----

    /// <summary>
    /// 현재 플레이와 연결된 맵 씬 조회
    /// </summary>
    /// <param name="play">현재 플레이 연결부</param>
    /// <param name="mapScene">현재 맵 씬</param>
    /// <returns>플레이와 맵 참조 조회 여부</returns>
    public bool TryGetPlayContext ( out PlayScene play , out Scene mapScene )
    {
        play = null;
        mapScene = default;

        if ( _state == SceneFlowState.Loading || _state == SceneFlowState.LoadFailed
            || _state == SceneFlowState.Closed || _play == null || _play.IsInitialized == false
            || _mapScene.IsValid ( ) == false || _mapScene.isLoaded == false )
            return false;

        play = _play;
        mapScene = _mapScene;
        return true;
    }

    /// <summary>
    /// 현재 맵의 준비 요청과 공용 플레이 참조 조회
    /// </summary>
    /// <param name="mapScene">준비를 요청하는 맵 씬</param>
    /// <param name="play">공용 플레이 연결부</param>
    /// <param name="entry">준비할 입장 선택</param>
    /// <param name="requestId">맵 준비 요청 번호</param>
    /// <returns>현재 맵의 준비 요청 조회 성공 여부</returns>
    public bool TryGetMapPreparation ( Scene mapScene , out PlayScene play ,
        out StageEntrySelection entry , out int requestId )
    {
        play = null;
        entry = default;
        requestId = 0;

        StageEntrySelection? pending = _entry.Pending;
        if ( _state != SceneFlowState.PreparingMap || pending.HasValue == false
            || TryGetPlayContext ( out PlayScene currentPlay , out Scene currentMap ) == false
            || mapScene != currentMap )
            return false;

        play = currentPlay;
        entry = pending.Value;
        requestId = _mapRequestId;
        return true;
    }

    /// <summary>
    /// 현재 맵의 설치 프리뷰와 동료 배치점 연결
    /// </summary>
    /// <param name="mapScene">준비 중인 맵 씬</param>
    /// <param name="requestId">준비 요청 번호</param>
    /// <param name="buildPreview">설치 프리뷰, 없으면 null</param>
    /// <param name="placementPoints">동료 배치점, 없으면 빈 배열</param>
    /// <returns>참조 연결 여부</returns>
    public bool TryBindMapReferences ( Scene mapScene , int requestId ,
        BuildPreviewController buildPreview , CompanionPlacementPoint [ ] placementPoints )
    {
        if ( requestId != _mapRequestId
            || TryGetMapPreparation ( mapScene , out PlayScene play , out _ , out _ ) == false )
            return false;

        if ( placementPoints == null
            || ( buildPreview != null && buildPreview.gameObject.scene != mapScene ) )
            return false;

        foreach ( CompanionPlacementPoint point in placementPoints )
        {
            if ( point == null || point.gameObject.scene != mapScene )
                return false;
        }

        play.SetMapReferences ( buildPreview , placementPoints );
        return true;
    }

    /// <summary>
    /// 플레이 도착 연결
    /// </summary>
    /// <param name="play">플레이 연결부</param>
    /// <returns>연결 여부</returns>
    public bool TryAttachPlay ( PlayScene play )
    {
        if ( play == null )
            return false;

        //로드 완료 처리와 공용 PlayScene.Start가 같은 객체를 연결할 수 있음
        if ( UsesPlayBase == true && _play == play )
            return true;

        if ( _state != SceneFlowState.Loading
            || _path == _titlePath
            || ( UsesPlayBase == false && _path != _playPath )
            || _entry.Pending.HasValue == false )
            return false;

        if ( UsesPlayBase == true )
        {
            if ( _play != null || play.gameObject.scene.path != _playBasePath )
                return false;

            _play = play;
            return true;
        }

        _play = play;
        _mapScene = play.gameObject.scene;
        return InitPlay ( );
    }

    /// <summary>
    /// 플레이 초기화 재시도
    /// </summary>
    /// <returns>성공 여부</returns>
    public bool RetryInitPlay ( )
    {
        return _state == SceneFlowState.InitFailed && _play != null && InitPlay ( );
    }

    /// <summary>
    /// 플레이 초기화
    /// </summary>
    /// <returns>성공 여부</returns>
    bool InitPlay ( )
    {
        _move.Dispose ( );
        _stageId = 0;
        _state = SceneFlowState.InitializingPlay;
        //맵 교체에서는 공용 플레이 진행과 도전 결과를 보유한 세션 유지
        if ( ( UsesPlayBase == false || _play.IsInitialized == false )
            && _play.Init ( PlayInitMode.Lobby ) == false )
        {
            _error = _play.InitError;
            SetState ( SceneFlowState.InitFailed );
            return false;
        }

        _error = null;
        //초기화와 선택한 맵 준비는 별도 단계
        _mapRequestId++;
        SetState ( SceneFlowState.PreparingMap );
        return true;
    }

    /// <summary>
    /// 준비된 맵으로 도전 시작
    /// </summary>
    /// <param name="play">요청 연결부</param>
    /// <param name="requestId">준비 요청 번호</param>
    /// <param name="mapId">준비한 맵</param>
    /// <param name="difficulty">준비한 난이도</param>
    /// <param name="initialStageCurrency">시작 재화</param>
    /// <param name="defenseRule">방어 규칙</param>
    /// <param name="companionPriorityPosition">동료 기준 위치</param>
    /// <returns>시작 여부</returns>
    public bool TryBeginPreparedStage (
        PlayScene play ,
        int requestId ,
        string mapId ,
        MissionDifficulty difficulty ,
        int initialStageCurrency ,
        MissionDefenseRule defenseRule ,
        Vector3? companionPriorityPosition = null )
    {
        if ( _state != SceneFlowState.PreparingMap || play != _play || requestId != _mapRequestId )
            return false;

        StageEntrySelection entry = _entry.Pending.Value;
        if ( entry.MapId != mapId || entry.Difficulty != difficulty )
            return false;

        //시작 재시도는 맵을 다시 준비하지 않고 이 설정을 사용
        _initialStageCurrency = initialStageCurrency;
        _defenseRule = defenseRule;
        _companionPriorityPosition = companionPriorityPosition;
        return BeginStage ( );
    }

    /// <summary>
    /// 도전 시작 재시도
    /// </summary>
    /// <returns>시작 여부</returns>
    public bool RetryStageStart ( )
    {
        if ( _state != SceneFlowState.StartFailed || _play.IsStageIdle == false )
            return false;

        //실패한 시작의 정리가 끝났을 때만 기존 설정으로 재시도
        return BeginStage ( );
    }

    /// <summary>
    /// 준비된 설정으로 시작
    /// </summary>
    /// <returns>시작 여부</returns>
    bool BeginStage ( )
    {
        StageEntrySelection entry = _entry.Pending.Value;

        //고용과 인벤토리 알림 중 재도전/복귀를 접수하지 않음
        _state = SceneFlowState.StartingStage;
        bool isStarted = _play.BeginStage ( entry.WeaponType , _initialStageCurrency ,
            entry.MapId , entry.Difficulty , _defenseRule , _companionPriorityPosition );

        if ( isStarted == false )
        {
            _error = "도전 시작에 실패했습니다. 시작 실패 후 정리 상태를 확인하세요.";
            SetState ( SceneFlowState.StartFailed );
            return false;
        }

        _entry.Commit ( );
        _stageId = _play.CurrentStageId;
        _error = null;
        SetState ( SceneFlowState.Playing );
        return true;
    }

    /// <summary>
    /// 맵 준비 실패 전달
    /// </summary>
    /// <param name="play">요청 연결부</param>
    /// <param name="requestId">준비 요청 번호</param>
    /// <param name="reason">사유</param>
    /// <returns>접수 여부</returns>
    public bool ReportMapFailure ( PlayScene play , int requestId , string reason )
    {
        if ( _state != SceneFlowState.PreparingMap || play != _play || requestId != _mapRequestId )
            return false;

        _error = reason;
        SetState ( SceneFlowState.MapFailed );
        return true;
    }

    /// <summary>
    /// 맵 준비 재시도
    /// </summary>
    /// <returns>접수 여부</returns>
    public bool RetryMapPreparation ( )
    {
        if ( _state != SceneFlowState.MapFailed )
            return false;

        //이전 준비의 늦은 완료는 새 요청으로 접수하지 않음
        _mapRequestId++;
        _error = null;
        SetState ( SceneFlowState.PreparingMap );
        return true;
    }

    #endregion

    #region ----- 결과 백업 -----

    /// <summary>
    /// 결과 화면의 백업 준비
    /// </summary>
    /// <returns>백업 결과 또는 교체 확인 요청</returns>
    public Task<ProgressBackupResponse> PrepareResultBackupAsync ( )
    {
        if ( TryBeginResultBackup ( out string reason ) == false )
            return Task.FromResult ( new ProgressBackupResponse ( ProgressBackupResult.Unavailable , reason ) );

        return RunResultBackupAsync ( ( save , play ) => save.PrepareBackupAsync ( play ) );
    }

    /// <summary>
    /// 결과 화면에서 확인한 서버 백업 교체
    /// </summary>
    /// <returns>업로드 결과</returns>
    public Task<ProgressBackupResponse> ConfirmResultBackupAsync ( )
    {
        if ( _resultBackupSave == null || _resultBackupRunning == true
            || _resultBackupSave.BackupPhase != ProgressBackupPhase.AwaitingConfirmation )
            return Task.FromResult ( new ProgressBackupResponse ( ProgressBackupResult.Unavailable ) );

        return RunResultBackupAsync ( ( save , play ) => save.ConfirmBackupAsync ( play ) );
    }

    /// <summary>
    /// 서버 백업 교체 확인 취소
    /// </summary>
    /// <returns>취소 여부</returns>
    public bool CancelResultBackup ( )
    {
        if ( _resultBackupSave == null || _resultBackupRunning == true
            || _resultBackupSave.CancelPreparedBackup ( _play ) == false )
            return false;

        TryResumeResultProgress ( );
        return true;
    }

    /// <summary>
    /// 결과 화면에서 확인한 서버 백업 복원
    /// </summary>
    /// <returns>로컬 진행 반영 결과</returns>
    public Task<ProgressBackupResponse> RestoreResultProgressAsync ( )
    {
        //로컬 파일을 교체하기 전에 이동할 타이틀 경로 확인
        if ( CanLoad ( _titlePath , out string reason ) == false
            || TryBeginResultBackup ( out reason ) == false )
            return Task.FromResult ( new ProgressBackupResponse ( ProgressBackupResult.Unavailable , reason ) );

        return RunResultBackupAsync ( ( save , play ) => save.RestoreProgressAsync ( play ) , isRestore: true );
    }

    /// <summary>
    /// 정산을 마친 결과 씬의 백업 요청 준비
    /// </summary>
    /// <param name="reason">거절 사유</param>
    /// <returns>준비 성공 여부</returns>
    bool TryBeginResultBackup ( out string reason )
    {
        reason = null;
        if ( CanUseResultBackup == false )
        {
            reason = "도전 정산과 정리가 끝난 결과 화면에서 요청하세요.";
            return false;
        }

        //미저장 처리의 알림 중에도 재도전/이동 요청 차단
        _state = SceneFlowState.ResultBackup;
        if ( _play.TrySuspendProgressForBackup ( out _resultBackupSave ) == false )
        {
            reason = "결과 화면의 진행 사용권을 반환하지 못했습니다.";
            _error = reason;
            SetState ( SceneFlowState.Playing );
            return false;
        }

        _error = null;
        return true;
    }

    /// <summary>
    /// 결과 백업 완료에 따른 사용권 복구 또는 타이틀 이동
    /// </summary>
    /// <param name="runRequest">실행할 백업 요청</param>
    /// <param name="isRestore">로컬 진행을 교체하는 요청 여부</param>
    /// <returns>요청 결과</returns>
    async Task<ProgressBackupResponse> RunResultBackupAsync (
        Func<SaveManager , PlayScene , Task<ProgressBackupResponse>> runRequest , bool isRestore = false )
    {
        PlayScene play = _play;
        SaveManager save = _resultBackupSave;
        _resultBackupRunning = true;

        try
        {
            Task<ProgressBackupResponse> request = runRequest ( save , play );
            if ( request.IsCompleted == false )
                OnChanged?.Invoke ( );

            ProgressBackupResponse response = await request;

            //씬 해제 뒤 도착한 응답에는 화면 상태나 이동을 적용하지 않음
            if ( IsResultBackupOwner ( play , save ) == true
                && isRestore == true && response.Result == ProgressBackupResult.Success )
            {
                //복원 데이터가 확정됐으므로 이전 결과 세션을 해제
                _resultBackupRunning = false;
                _resultBackupSave = null;
                play.ReleaseProgressAfterRestore ( );

                //로드 실패 시 이미 끝난 복원은 반복하지 않고 이동만 재시도
                _path = _titlePath;
                _shouldOpenLobby = false;
                _restoreNoticePending = true;
                SetState ( SceneFlowState.ReadyToLoad );
            }

            return response;
        }
        finally
        {
            if ( IsResultBackupOwner ( play , save ) == true )
            {
                _resultBackupRunning = false;
                //교체 확인 후보가 남아 있으면 승인/취소까지 사용권 보관
                if ( save.IsBackupPending == true )
                    OnChanged?.Invoke ( );
                else
                    TryResumeResultProgress ( );
            }
        }
    }

    /// <summary>
    /// 결과 백업 요청의 씬과 관리자 일치 확인
    /// </summary>
    /// <param name="play">요청한 씬</param>
    /// <param name="save">사용권을 보관한 관리자</param>
    /// <returns>현재 요청 여부</returns>
    bool IsResultBackupOwner ( PlayScene play , SaveManager save )
    {
        return _state == SceneFlowState.ResultBackup && play != null && _play == play
            && save != null && _resultBackupSave == save;
    }

    /// <summary>
    /// 백업이 끝난 결과 화면에 사용권 반환
    /// </summary>
    /// <returns>재연결 성공 여부</returns>
    public bool TryResumeResultProgress ( )
    {
        if ( _resultBackupSave == null || _resultBackupRunning == true
            || _resultBackupSave.IsBackupPending == true )
            return false;

        if ( _play.TryResumeProgressAfterBackup ( ) == false )
        {
            _error = "결과 화면에 진행 사용권을 다시 연결하지 못했습니다.";
            OnChanged?.Invoke ( );
            return false;
        }

        _resultBackupSave = null;
        _error = null;
        SetState ( SceneFlowState.Playing );
        return true;
    }

    /// <summary>
    /// 타이틀에 도착한 뒤 복원 성공 알림 소비
    /// </summary>
    /// <returns>표시할 복원 성공 알림 여부</returns>
    public bool TryConsumeRestoreNotice ( )
    {
        if ( _restoreNoticePending == false || _operation != null
            || ( _state != SceneFlowState.Idle && _state != SceneFlowState.Lobby ) )
            return false;

        _restoreNoticePending = false;
        return true;
    }

    #endregion

    #region ----- 도전 이동 -----

    /// <summary>
    /// 입장 가능한 다음 스테이지 조회
    /// </summary>
    /// <param name="entry">다음 입장 선택</param>
    /// <param name="reason">이동 불가 사유</param>
    /// <returns>입장 가능 여부</returns>
    public bool TryGetNextStage ( out StageEntrySelection entry , out string reason )
    {
        return TryGetNextStage ( out entry , out _ , out reason );
    }

    /// <summary>
    /// 다음 입장 선택과 대상 씬 경로 조회
    /// </summary>
    /// <param name="entry">다음 입장 선택</param>
    /// <param name="path">이동할 씬 경로</param>
    /// <param name="reason">이동 불가 사유</param>
    /// <returns>입장 가능 여부</returns>
    bool TryGetNextStage ( out StageEntrySelection entry , out string path , out string reason )
    {
        entry = default;
        path = null;

        if ( _state != SceneFlowState.Playing || _operation != null )
        {
            reason = "현재는 다음 스테이지로 이동할 수 없습니다.";
            return false;
        }

        if ( _play.TryGetCompletedResult ( _stageId , out MissionStageResult result ) == false )
        {
            reason = "도전 정산과 정리가 아직 끝나지 않았습니다.";
            return false;
        }

        if ( result.Outcome != MissionOutcome.Victory )
        {
            reason = "스테이지를 클리어해야 이동할 수 있습니다.";
            return false;
        }

        StageEntrySelection current = _entry.Current.Value;

        //목록에서 바로 다음 맵만 확인
        for ( int i = 0 ; i + 1 < _stages.Count ; i++ )
        {
            if ( _stages [ i ].MapId != current.MapId )
                continue;

            LobbyStageInfo next = _stages [ i + 1 ];

            if ( TryCheckMapSupport (
                _stages , next.MapId , current.Difficulty , out reason ) == false )
                return false;

            if ( TryResolveStagePath ( _stages , next.MapId , out path , out reason ) == false )
                return false;

            entry = new StageEntrySelection ( next.MapId , current.Difficulty , current.WeaponType );
            return true;
        }

        reason = "이동할 다음 스테이지가 없습니다.";
        return false;
    }

    /// <summary>
    /// 다음 스테이지 이동 요청
    /// </summary>
    /// <param name="reason">거절 사유</param>
    /// <returns>요청 접수 여부</returns>
    public bool TryGoToNextStage ( out string reason )
    {
        if ( TryGetNextStage ( out StageEntrySelection entry , out string path , out reason ) == false )
            return false;

        //새 도전 시작에 성공할 때까지 현재 선택 유지
        _entry.Prepare ( entry );
        _path = path;
        _shouldOpenLobby = false;
        _error = null;

        SetState ( SceneFlowState.ReadyToLoad );
        return true;
    }

    /// <summary>
    /// 재도전 요청
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>접수 여부</returns>
    public bool TryRetryStage ( out string reason )
    {
        return TryLeavePlay ( true , out reason );
    }

    /// <summary>
    /// 로비 복귀 요청
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>접수 여부</returns>
    public bool TryReturnToLobby ( out string reason )
    {
        return TryLeavePlay ( false , out reason );
    }

    /// <summary>
    /// 플레이 이동 접수
    /// </summary>
    /// <param name="isRetry">재도전 여부</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>접수 여부</returns>
    bool TryLeavePlay ( bool isRetry , out string reason )
    {
        reason = null;

        if ( UsesPlayBase == true && isRetry == false
            && _state == SceneFlowState.LoadFailed && _path != _titlePath )
        {
            //공용 씬 도착 이후에는 기존 타이틀이 제거됐으므로 새로 로드
            if ( _lobby != null )
                return TryRestoreLobby ( out reason );

            if ( CanLoad ( _titlePath , out reason ) == false )
                return false;

            _path = _titlePath;
            _shouldOpenLobby = true;
            _error = null;
            SetState ( SceneFlowState.ReadyToLoad );
            return true;
        }

        if ( _operation != null || ( _state != SceneFlowState.Playing
            && _state != SceneFlowState.PreparingMap && _state != SceneFlowState.MapFailed
            && _state != SceneFlowState.StartFailed
            && _state != SceneFlowState.InitFailed ) )
            return false;

        string path = _titlePath;
        if ( isRetry == true )
        {
            StageEntrySelection? retry = _entry.Pending ?? _entry.Current;
            if ( retry.HasValue == false )
            {
                reason = "재진입할 입장 선택이 없습니다.";
                return false;
            }

            if ( TryResolveStagePath ( _stages , retry.Value.MapId , out path , out reason ) == false )
                return false;
        }

        if ( _state == SceneFlowState.InitFailed )
        {
            if ( isRetry == false && CanLoad ( path , out reason ) == false )
                return false;

            //초기화 실패 시 부분 연결은 이미 해제된 상태
            //재진입에서는 원래 선택을 유지하고 새 씬에서 초기화
            if ( isRetry == true )
                _entry.PrepareRetry ( );

            _path = path;
            _shouldOpenLobby = isRetry == false;
            _error = null;
            SetState ( SceneFlowState.ReadyToLoad );
            return true;
        }

        bool shouldRequestEnd = _stageId != 0
            && _play.IsStageCompleted ( _stageId ) == false
            && _play.IsEndingStage == false && _play.IsStageCleanupPending == false;
        if ( shouldRequestEnd == true && ( _endLink == null || _endOwner != _play ) )
        {
            reason = "도전 종료 요청 연결이 없습니다.";
            return false;
        }

        SceneFlowState previousState = _state;
        _state = SceneFlowState.RequestingEnd;
        if ( _move.TryPrepare ( _play , _stageId , path ) == false )
        {
            reason = "이동 대상과 미저장 진행 또는 도전 상태를 확인하세요.";
            _state = previousState;
            return false;
        }

        string previousPath = _path;
        bool shouldOpenPreviousLobby = _shouldOpenLobby;
        StageEntrySelection? previousEntry = _entry.Pending;
        if ( isRetry == true )
            _entry.PrepareRetry ( );

        _path = path;
        _shouldOpenLobby = isRetry == false;
        HandleMoveChanged ( );

        //결과 구독과 이동 정보 준비 후 접수 결과 확인
        if ( shouldRequestEnd == true && _endLink.TryEndStage ( _stageId ) == false )
        {
            reason = string.IsNullOrEmpty ( _endLink.Error ) == false
                ? _endLink.Error : "도전 종료 요청이 접수되지 않았습니다.";
            _move.Dispose ( );
            _path = previousPath;
            _shouldOpenLobby = shouldOpenPreviousLobby;
            if ( previousEntry.HasValue == true )
                _entry.Prepare ( previousEntry.Value );
            else
                _entry.Cancel ( );

            _error = reason;
            SetState ( previousState );
            return false;
        }

        //동기 종료 결과를 받은 경우에도 요청 호출이 끝난 뒤 이동 허용
        SetState ( SceneFlowState.WaitingForEnd );
        return true;
    }

    /// <summary>
    /// 실패한 종료 단계 재시도
    /// </summary>
    /// <returns>접수 여부</returns>
    public bool RetryStageEnd ( )
    {
        if ( _state != SceneFlowState.WaitingForEnd
            || _move.State != SceneMoveState.WaitingForEnd )
            return false;

        bool isAccepted;
        _error = null;
        _state = SceneFlowState.RequestingEnd;

        if ( _play.IsAwaitingEndEvents == true )
        {
            if ( _endLink == null || _endOwner != _play )
            {
                _error = "마지막 사건 전달 요청 연결이 없습니다.";
                SetState ( SceneFlowState.WaitingForEnd );
                return false;
            }

            //최종 점수와 마지막 사건 처리는 기존 담당자가 완료
            isAccepted = _endLink.TryCompleteEndEvents ( _stageId );
            if ( isAccepted == false && string.IsNullOrEmpty ( _error ) == true )
                _error = string.IsNullOrEmpty ( _endLink.Error ) == false
                    ? _endLink.Error : "마지막 사건 전달이 완료되지 않았습니다.";
        }
        else
        {
            isAccepted = _play.IsStageCleanupPending == true
                ? _play.TryCompleteStageCleanup ( ) : _play.RetryEndStage ( );
            if ( isAccepted == false && string.IsNullOrEmpty ( _error ) == true )
                _error = "종료 재시도 요청이 접수되지 않았습니다.";
        }

        //접수 성공만으로 호출 중 받은 실패를 지우지 않음
        SetState ( SceneFlowState.WaitingForEnd );
        return isAccepted;
    }

    /// <summary>
    /// 종료 결과 전달
    /// </summary>
    void HandleMoveChanged ( )
    {
        _error = _move.Failure == PlayEndFailure.None
            ? null : $"종료 처리 실패: {_move.Failure}";

        //요청 호출 안에서는 결과만 보관하고 호출 종료 후 한 번 알림
        if ( _state != SceneFlowState.RequestingEnd )
            OnChanged?.Invoke ( );
    }

    #endregion

    #region ----- 씬 로드 -----

    /// <summary>
    /// 현재 이동 진행률 계산
    /// </summary>
    /// <returns>0~1 범위의 진행률</returns>
    float GetLoadProgress ( )
    {
        if ( _state == SceneFlowState.ReadyToLoad )
            return 0f;

        if ( _state == SceneFlowState.Playing
            || _state == SceneFlowState.Lobby )
            return 1f;

        if ( _loadStepCount == 0 )
            return 0f;

        float operationProgress = _operation != null
            ? _operation.progress : 0f;

        //씬 작업은 90%까지 표시하고 실제 준비 완료 후 100% 반환
        float progress = ( _completedLoadSteps + operationProgress )
            / _loadStepCount;

        return Mathf.Clamp01 ( progress ) * 0.9f;
    }

    /// <summary>
    /// 로드 시작 재시도
    /// </summary>
    /// <returns>접수 여부</returns>
    public bool RetryLoad ( )
    {
        if ( _state != SceneFlowState.LoadFailed )
            return false;

        _error = null;
        SetState ( SceneFlowState.ReadyToLoad );
        return true;
    }

    /// <summary>
    /// 로드 실패 후 로비 복구
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>복구 여부</returns>
    public bool TryRestoreLobby ( out string reason )
    {
        reason = null;
        if ( _state != SceneFlowState.LoadFailed || _lobby == null
            || _entry.Pending.HasValue == false )
            return false;

        StageEntrySelection entry = _entry.Pending.Value;
        _state = SceneFlowState.OpeningLobby;
        if ( _lobby.TryOpen ( out reason ) == false )
        {
            _error = reason;
            SetState ( SceneFlowState.LoadFailed );
            return false;
        }

        //TryOpen의 첫 맵 선택을 이전 선택으로 복원
        bool isRestored = false;
        for ( int i = 0 ; i < _lobby.Stages.Count ; i++ )
        {
            if ( _lobby.Stages [ i ].MapId != entry.MapId )
                continue;

            isRestored = _lobby.TrySelectStage ( i , out reason )
                && _lobby.TrySelectDifficulty ( entry.Difficulty , out reason );
            break;
        }

        _entry.Cancel ( );
        _error = isRestored == true ? null : reason ?? "이전 맵 선택을 복원하지 못했습니다.";
        reason = _error;
        SetState ( SceneFlowState.Lobby );
        return isRestored;
    }

    /// <summary>
    /// 씬 로드 시작
    /// </summary>
    void BeginLoad ( )
    {
        bool hasLoadedMap = _mapScene.IsValid ( ) && _mapScene.isLoaded;
        _completedLoadSteps = 0;

        //타이틀·단일 씬은 1회, 공용 씬 입장·맵 교체는 2회
        if ( UsesPlayBase == false || _path == _titlePath )
            _loadStepCount = 1;
        else if ( _play == null || hasLoadedMap == true )
            _loadStepCount = 2;
        else
            _loadStepCount = 1;

        _state = SceneFlowState.Loading;
        _sound.TrySetGameplayPaused ( false , out _ );
        _sound.StopSfx ( );
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        //이전 맵을 내리기 전에 HUD가 기존 참조를 해제할 수 있도록 알림
        OnChanged?.Invoke ( );

        if ( UsesPlayBase == true && hasLoadedMap == true && _play != null && _play.IsInitialized == true )
            _play.SetMapReferences ( null , Array.Empty<CompanionPlacementPoint> ( ) );

        if ( _state == SceneFlowState.Closed )
            return;

        if ( UsesPlayBase == false || _path == _titlePath )
        {
            BeginSceneOperation ( LoadStep.Single , _path );
            return;
        }

        if ( _play == null )
        {
            BeginSceneOperation ( LoadStep.PlayBase , _playBasePath );
            return;
        }

        if ( hasLoadedMap == true )
        {
            BeginSceneOperation ( LoadStep.UnloadMap , _mapScene.path );
            return;
        }

        BeginSceneOperation ( LoadStep.Map , _path );
    }

    /// <summary>
    /// 씬 로드 또는 해제 요청 접수
    /// </summary>
    /// <param name="step">실행할 씬 작업</param>
    /// <param name="path">대상 씬 경로</param>
    void BeginSceneOperation ( LoadStep step , string path )
    {
        try
        {
            if ( step == LoadStep.UnloadMap )
                _operation = SceneManager.UnloadSceneAsync ( _mapScene );
            else
                _operation = SceneManager.LoadSceneAsync ( path ,
                    step == LoadStep.Map ? LoadSceneMode.Additive : LoadSceneMode.Single );
        }
        catch ( Exception exception ) when ( exception is UnityException || exception is ArgumentException )
        {
            FailLoad ( exception.Message );
            return;
        }

        if ( _operation == null )
        {
            FailLoad ( $"씬 작업을 시작하지 못했습니다. {path}" );
            return;
        }

        if ( step == LoadStep.UnloadMap )
            UnbindEndLink ( _endOwner , _endLink );

        _loadStep = step;
        _operation.completed += HandleLoaded;
        OnChanged?.Invoke ( );
    }

    /// <summary>
    /// 로드 접수 실패
    /// </summary>
    /// <param name="reason">사유</param>
    void FailLoad ( string reason )
    {
        _error = reason;
        _move.FailLoad ( );
        SetState ( SceneFlowState.LoadFailed );
    }

    /// <summary>
    /// 씬 작업 완료 후 다음 단계 진행
    /// </summary>
    /// <param name="operation">완료 요청</param>
    void HandleLoaded ( AsyncOperation operation )
    {
        operation.completed -= HandleLoaded;
        if ( operation != _operation )
            return;

        _completedLoadSteps++;
        _operation = null;

        switch ( _loadStep )
        {
            case LoadStep.PlayBase:
                if ( TryAttachPlayBase ( ) == false )
                {
                    //Start에서 일부 연결됐어도 실패한 공용 씬은 다음 로드에서 다시 준비
                    _play = null;
                    FailLoad ( "공용 플레이 씬에 활성 PlayScene 루트 하나를 연결하세요." );
                    return;
                }

                BeginSceneOperation ( LoadStep.Map , _path );
                return;
            case LoadStep.UnloadMap:
                if ( _mapScene.IsValid ( ) == true && _mapScene.isLoaded == true )
                {
                    FailLoad ( "이전 맵 씬을 내리지 못했습니다." );
                    return;
                }

                _mapScene = default;
                BeginSceneOperation ( LoadStep.Map , _path );
                return;
            case LoadStep.Map:
                _mapScene = SceneManager.GetSceneByPath ( _path );
                if ( _mapScene.IsValid ( ) == false || _mapScene.isLoaded == false
                    || SceneManager.SetActiveScene ( _mapScene ) == false )
                {
                    FailLoad ( $"맵 씬을 활성화하지 못했습니다. {_path}" );
                    return;
                }

                if ( _play == null || _play.isActiveAndEnabled == false )
                {
                    FailLoad ( "공용 플레이 씬 연결이 해제됐습니다." );
                    return;
                }

                //맵 활성화 이후에만 새 맵 준비 요청 발행
                InitPlay ( );
                return;
            default:
                OnChanged?.Invoke ( );
                return;
        }
    }

    /// <summary>
    /// 로드 완료와 Start 순서에 의존하지 않고 공용 플레이 씬 연결
    /// </summary>
    /// <returns>활성 플레이 루트 연결 여부</returns>
    bool TryAttachPlayBase ( )
    {
        Scene playBase = SceneManager.GetSceneByPath ( _playBasePath );
        if ( playBase.IsValid ( ) == false || playBase.isLoaded == false )
            return false;

        PlayScene play = null;
        foreach ( GameObject root in playBase.GetRootGameObjects ( ) )
        {
            if ( root.TryGetComponent ( out PlayScene candidate ) == false
                || candidate.isActiveAndEnabled == false )
                continue;

            if ( play != null )
                return false;

            play = candidate;
        }

        return TryAttachPlay ( play );
    }

    /// <summary>
    /// 설정 목록에서 맵과 난이도 지원 확인
    /// </summary>
    /// <param name="stages">입장에 사용할 스테이지 목록</param>
    /// <param name="mapId">입장할 맵 ID</param>
    /// <param name="difficulty">입장할 난이도</param>
    /// <param name="reason">입장 불가 사유</param>
    /// <returns>선택한 입장 지원 여부</returns>
    bool TryCheckMapSupport (
        IReadOnlyList<LobbyStageInfo> stages ,
        string mapId ,
        MissionDifficulty difficulty ,
        out string reason )
    {
        reason = null;
        foreach ( LobbyStageInfo stage in stages )
        {
            if ( stage.MapId != mapId )
                continue;

            if ( stage.Supports ( difficulty ) == false )
            {
                reason = $"선택한 스테이지에서 해당 난이도를 지원하지 않습니다. {mapId} / {difficulty}";
                return false;
            }

            return true;
        }

        reason = $"스테이지 목록에 맵이 없습니다. {mapId}";
        return false;
    }

    /// <summary>
    /// 이동할 맵 씬을 찾고 로드 가능 여부 확인
    /// </summary>
    /// <param name="stages">씬 경로를 조회할 스테이지 목록</param>
    /// <param name="mapId">이동할 맵 ID</param>
    /// <param name="path">이동할 씬 경로</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>대상 씬 로드 가능 여부</returns>
    bool TryResolveStagePath ( IReadOnlyList<LobbyStageInfo> stages , string mapId ,
        out string path , out string reason )
    {
        path = _playPath;
        if ( UsesPlayBase == false )
            return CanLoad ( path , out reason );

        path = null;
        foreach ( LobbyStageInfo stage in stages )
        {
            if ( stage.MapId != mapId )
                continue;

            path = stage.ScenePath;
            break;
        }

        if ( string.IsNullOrWhiteSpace ( path ) == true )
        {
            reason = $"맵 씬 경로가 설정되지 않았습니다. {mapId}";
            return false;
        }

        if ( path == _titlePath || path == _playBasePath || _playBasePath == _titlePath )
        {
            reason = "타이틀, 공용 플레이 씬, 맵 씬의 경로를 각각 지정하세요.";
            return false;
        }

        return CanLoad ( _playBasePath , out reason ) && CanLoad ( path , out reason );
    }

    /// <summary>
    /// 이동 대상 검사
    /// </summary>
    /// <param name="path">씬 경로</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>로드 가능 여부</returns>
    bool CanLoad ( string path , out string reason )
    {
        reason = null;
        if ( string.IsNullOrWhiteSpace ( path ) == false
            && Application.CanStreamedLevelBeLoaded ( path ) == true )
            return true;

        reason = "이동할 씬을 Scene List에 등록하세요: " + path;
        return false;
    }

    /// <summary>
    /// 상태 확정과 알림
    /// </summary>
    /// <param name="state">다음 상태</param>
    void SetState ( SceneFlowState state )
    {
        if ( state == SceneFlowState.ReadyToLoad )
            _readyToLoadFrame = Time.frameCount;

        _state = state;
        OnChanged?.Invoke ( );
    }

    #endregion

    #region ----- 연결 해제 -----

    /// <summary>
    /// 플레이 참조 해제
    /// </summary>
    /// <param name="play">해제 대상</param>
    public void DetachPlay ( PlayScene play )
    {
        //초기화 전에 등록된 종료 연결도 소유자 파괴 시 해제
        if ( _endOwner == play )
        {
            _endOwner = null;
            _endLink = null;
        }

        if ( _play != play )
            return;

        if ( _resultBackupSave != null )
        {
            _resultBackupSave.DetachScene ( play );
            _resultBackupSave = null;
            _resultBackupRunning = false;
        }

        _move.Dispose ( );
        _play = null;
        _mapScene = default;

        //로드 외의 강제 제거는 새 저장이나 자동 복구 없이 중단
        if ( _state != SceneFlowState.Loading && _state != SceneFlowState.Closed )
        {
            _error = "이동 절차 밖에서 플레이 연결이 해제되었습니다.";
            _state = SceneFlowState.Closed;
        }
    }

    /// <summary>
    /// 타이틀 참조 해제
    /// </summary>
    /// <param name="lobby">해제 대상</param>
    public void DetachTitle ( LobbyController lobby )
    {
        if ( _lobby == lobby )
            _lobby = null;
    }

    /// <summary>
    /// 공통 흐름 종료
    /// </summary>
    public void Dispose ( )
    {
        _sound.OnReadyChanged -= HandleSoundReady;
        _state = SceneFlowState.Closed;

        if ( _operation != null )
            _operation.completed -= HandleLoaded;

        _operation = null;
        _move.OnChanged -= HandleMoveChanged;
        _move.Dispose ( );
        _entry.CompleteReturn ( );
        _stages = null;

        if ( _resultBackupSave != null )
        {
            _resultBackupSave.DetachScene ( _play );
            _resultBackupSave = null;
            _resultBackupRunning = false;
        }

        _restoreNoticePending = false;
        _play = null;
        _mapScene = default;
        _lobby = null;
        OnChanged = null;
        _endOwner = null;
        _endLink = null;
    }

    #endregion
}
