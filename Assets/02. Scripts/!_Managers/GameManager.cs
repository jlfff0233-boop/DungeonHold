using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 게임 실행 시 영구 진행의 준비 상태
/// </summary>
public enum StartupProgressState
{
    NotStarted,    //준비 전
    Preparing,     //준비 중
    Ready,         //진행 확정
    Failed         //준비 실패
}

/// <summary>
/// 게임 관리자 - 공통 관리자 소유와 초기화/종료 조율
/// </summary>
public class GameManager : Singleton<GameManager>
{
    [Header ( "----- 공통 관리자 -----" )]
    [SerializeField] SaveManager _save;		//영구 진행과 수동 백업
    [SerializeField] SoundManager _sound;		//공통 볼륨과 사운드 재생
    [SerializeField] VfxManager _vfx;		//공용 VFX 서비스

    [Header ( "----- 씬 경로 -----" )]
    [SerializeField] string _titleScenePath = "Assets/01. Scenes/!_Title.unity";		//타이틀 씬
    [SerializeField] string _playScenePath = "Assets/01. Scenes/!_Play_Stage1.unity";		//플레이 씬
    [SerializeField] string _playBaseScenePath;		//공용 플레이 씬

    [Header ( "----- 공용 로딩 -----" )]
    [SerializeField] GameObject _loadingScreenPrefab; //공용 로딩 화면 원본

    SettingsManager _settings;		//공통 설정 소유와 수명 조율
    SceneFlow _scenes;		//씬 이동과 입장 선택 수명
    GameObject _loadingScreen; //씬 전환 중 유지할 로딩 루트
    bool _isInitialized;		//공통 저장 기반 준비 여부
    StartupProgressState _startupProgress;		//영구 진행 준비 상태
    SaveResult? _startupProgressResult;		//최근 진행 준비 결과
    string _startupProgressError;		//진행 준비 실패 사유
    bool _initialSettingsCompleted;		//설정 최초 적용 처리 완료 여부
    string _initialAudioError;		//사운드 초기화 또는 최초 볼륨 적용 실패 사유

    /// <summary>
    /// 씬 흐름
    /// </summary>
    public SceneFlow Scenes => _scenes;

    /// <summary>
    /// 공통 저장 기반 준비 여부
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// 게임 실행 시 영구 진행 준비 상태
    /// </summary>
    public StartupProgressState StartupProgress => _startupProgress;

    /// <summary>
    /// 최근 영구 진행 준비 결과
    /// </summary>
    public SaveResult? StartupProgressResult => _startupProgressResult;

    /// <summary>
    /// 영구 진행 준비 실패 사유
    /// </summary>
    public string StartupProgressError => _startupProgressError;

    /// <summary>
    /// 설정 최초 적용 시도 완료 여부
    /// </summary>
    public bool InitialSettingsCompleted => _initialSettingsCompleted;

    /// <summary>
    /// 사운드 초기화 또는 최초 볼륨 적용 실패 사유
    /// </summary>
    public string InitialAudioError => _initialAudioError;

    /// <summary>
    /// 저장 관리자
    /// </summary>
    public SaveManager Save => _save;

    /// <summary>
    /// 사운드 관리자
    /// </summary>
    public SoundManager Sound => _sound;

    /// <summary>
    /// VFX 관리자
    /// </summary>
    public VfxManager Vfx => _vfx;

    /// <summary>
    /// 단축키 설정
    /// </summary>
    public KeyBindingSettings KeyBindings => _settings?.KeyBindings;

    /// <summary>
    /// 공통 설정
    /// </summary>
    public SettingsManager Settings => _settings;

    /// <summary>
    /// 새 플레이 실행의 정적 참조 초기화
    /// </summary>
    [RuntimeInitializeOnLoadMethod ( RuntimeInitializeLoadType.SubsystemRegistration )]
    static void ResetRuntimeState ( )
    {
        ResetInstance ( );
    }

    /// <summary>
    /// 공통 인스턴스 등록과 저장 기반 준비
    /// </summary>
    protected override void Awake ( )
    {
        base.Awake ( );

        //부모에서 제거 요청한 중복 객체는 서비스를 만들지 않음
        if ( Instance != this )
            return;

        _isInitialized = _save.Init ( );

        //씬의 Start에서 서비스를 전달하기 전에 준비
        _vfx.Init ( );
        _scenes = new SceneFlow ( _titleScenePath , _playScenePath , _sound ,
            playBasePath: _playBaseScenePath );

        //씬의 입력 참조 연결 전에 설정을 복원하며 진행 저장과는 별도 관리
        _settings = new SettingsManager ( InputSystem.actions , Application.persistentDataPath , _sound );
        _settings.Init ( );

        //임시: LHJ 로딩 프리팹 연결 전에도 개별 플레이 검증이 가능하도록 미할당 시 생성 생략
        //프리팹 연결 완료 후 필수 참조로 전환하며 이 분기 제거
        if ( _loadingScreenPrefab != null )
        {
            _loadingScreen = Instantiate ( _loadingScreenPrefab );
            DontDestroyOnLoad ( _loadingScreen );
        }
    }

    private void Start ( )
    {
        EnsureInitialSettingsApplied ( );
    }

    /// <summary>
    /// 첫 타이틀에서 설정을 적용하고 영구 진행을 준비
    /// </summary>
    internal void PrepareForTitle ( )
    {
        EnsureInitialSettingsApplied ( );

        if ( _startupProgress == StartupProgressState.NotStarted )
            PrepareStartupProgress ( );
    }

    /// <summary>
    /// 실패한 영구 진행 준비를 다시 요청
    /// </summary>
    /// <returns>진행 준비 성공 여부</returns>
    public bool TryRetryStartupProgress ( )
    {
        if ( _startupProgress != StartupProgressState.Failed )
            return false;

        PrepareStartupProgress ( );
        return _startupProgress == StartupProgressState.Ready;
    }

    /// <summary>
    /// 화면 변경을 요청하고 사운드와 최초 볼륨을 한 번 적용
    /// </summary>
    void EnsureInitialSettingsApplied ( )
    {
        if ( _initialSettingsCompleted == true )
            return;

        _settings.ApplyInitialSettings ( );

        if ( _sound.Init ( out string reason ) == false )
        {
            _initialAudioError = $"사운드 초기화 실패: {reason}";
        }
        else if ( _settings.Volume.ApplyInitialSettings ( out reason ) == false )
        {
            _initialAudioError = $"최초 볼륨 적용 실패: {reason}";
        }
        else
        {
            VolumeSetting setting = _settings.Volume.Settings;
            Debug.Log ( $"[볼륨 설정 검증] 최초 적용 성공: "
                + $"마스터 {setting.Master} / BGM {setting.Bgm} / SFX {setting.Sfx}" , this );
        }

        _initialSettingsCompleted = true;

        if ( _initialAudioError != null )
            Debug.LogError ( _initialAudioError , this );
    }

    /// <summary>
    /// 기존 저장을 불러오거나 빈 슬롯에 신규 진행을 준비
    /// </summary>
    void PrepareStartupProgress ( )
    {
        _startupProgress = StartupProgressState.Preparing;
        _startupProgressResult = null;
        _startupProgressError = null;

        if ( _isInitialized == false )
        {
            _startupProgressResult = SaveResult.Unavailable;
            _startupProgressError = "저장 서비스가 준비되지 않았습니다.";
            _startupProgress = StartupProgressState.Failed;
            return;
        }

        SaveResult result = _save.TryStartProgress ( );
        _startupProgressResult = result;

        if ( result == SaveResult.Success )
        {
            _startupProgress = StartupProgressState.Ready;
            return;
        }

        string detail = result == SaveResult.Unavailable ? null : _save.LastError;
        _startupProgressError = string.IsNullOrEmpty ( detail )
            ? $"진행 준비 실패: {result}"
            : $"진행 준비 실패: {result} / {detail}";
        _startupProgress = StartupProgressState.Failed;
    }

    private void Update ( )
    {
        //일시정지 중에도 입력 대기와 키 해제를 처리
        _settings?.Update ( );
        _scenes?.Update ( );
    }

    private void OnApplicationFocus ( bool hasFocus )
    {
        //복귀 후 다른 용도의 첫 입력을 새 배정으로 받지 않도록 대기만 취소
        if ( hasFocus == false )
            _settings?.HandleFocusLost ( );
    }

    /// <summary>
    /// 공통 연결과 표현 서비스, 저장 통신 종료
    /// </summary>
    public void Shutdown ( )
    {
        if ( Instance != this )
            return;

        _isInitialized = false;

        //표시 구독을 해제한 뒤 씬 흐름 종료
        if ( _loadingScreen != null )
        {
            _loadingScreen.SetActive ( false );
            Destroy ( _loadingScreen );
            _loadingScreen = null;
        }

        //종료 알림과 비동기 로드 구독부터 해제
        _scenes?.Dispose ( );

        //설정 이벤트를 발행하지 않고 편집 수명을 끝낸 뒤 기존 공통 관리자 종료
        _settings?.Dispose ( );
        _settings = null;

        //자식이 먼저 파괴된 경우 각 관리자의 종료 콜백에서 정리
        if ( _vfx != null )
            _vfx.Shutdown ( );

        if ( _sound != null )
            _sound.Shutdown ( );

        if ( _save != null )
            _save.Shutdown ( );
    }

    /// <summary>
    /// 공통 관리자 종료와 인스턴스 등록 해제
    /// </summary>
    protected override void OnDestroy ( )
    {
        if ( Instance != this )
            return;

        Shutdown ( );
        base.OnDestroy ( );
    }
}
