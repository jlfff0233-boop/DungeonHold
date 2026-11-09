using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 저장 관리자 - 단일 영구 진행과 씬 연결 수명 관리
/// </summary>
public class SaveManager : MonoBehaviour
{
    [SerializeField] CurrencyData _currencyData;    //신규 진행의 최초 재화

    [Header ( "----- 수동 백업 -----" )]
    [SerializeField] string _firebaseProjectId;         //Firebase 프로젝트
    [SerializeField] string _firebaseApiKey;            //Firebase 웹 API 키
    [SerializeField] UpgradeDataMap _backupUpgradeMap;      //복원 호환 검사
    [SerializeField] MissionDataMap _backupMissionMap;      //복원 호환 검사

    SaveService _service;    //앱에서 공유하는 저장 서비스
    MonoBehaviour _sceneOwner;    //현재 진행을 사용하는 씬 연결부
    MonoBehaviour _backupScene;   //백업 후 사용권을 돌려받을 결과 씬
    bool _isSceneRestoreLocked;   //복원 처리 중 또는 복원 성공 후 이전 씬 재연결 차단
    ProgressBackupService _backupService;       //수동 백업 실행
    bool _isInitialized;    //저장 기반 준비 여부

    #region ----- 프로퍼티 -----

    /// <summary>
    /// 저장 기반 준비 여부
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// 확정된 영구 진행 보유 여부
    /// </summary>
    public bool HasProgress => _isInitialized && _service.HasData;

    /// <summary>
    /// 마지막 파일 처리 결과
    /// </summary>
    public SaveResult LastResult => _service.LastResult;

    /// <summary>
    /// 마지막 파일 오류 설명
    /// </summary>
    public string LastError => _service.LastError;

    /// <summary>
    /// 현재 백업 처리 단계
    /// </summary>
    public ProgressBackupPhase BackupPhase => _backupService.Phase;

    /// <summary>
    /// 백업 요청 또는 확인 대기 중 여부
    /// </summary>
    public bool IsBackupPending => _backupService.BlocksProgressChanges;

    /// <summary>
    /// 진행 변경 요청 가능 여부
    /// </summary>
    bool CanChangeProgress =>
        _isInitialized
        && isActiveAndEnabled
        && ReferenceEquals ( _backupScene , null )
        && _backupService.BlocksProgressChanges == false;

    #endregion

    #region ----- 초기화 -----

    /// <summary>
    /// 공유 저장 서비스와 백업 모듈 준비
    /// </summary>
    /// <returns>저장 기반 준비 성공 여부</returns>
    public bool Init ( )
    {
        //종료한 서비스를 다시 만들거나 기존 공유 진행을 교체하지 않음
        if ( _service != null )
            return _isInitialized;

        if ( _currencyData == null )
        {
            Debug.LogError ( "SaveManager의 최초 재화 설정이 필요합니다." , this );
            return false;
        }

        //씬 이동에도 같은 저장소와 서비스를 유지
        JsonSaveStorage storage = new JsonSaveStorage ( 0 );
        _service = new SaveService ( 0 , _currencyData , storage );

        //같은 진행을 사용하는 백업 모듈 연결
        InitializeBackup ( );

        _isInitialized = true;
        return true;
    }

    /// <summary>
    /// 로컬 저장과 별도로 백업 모듈 연결
    /// </summary>
    void InitializeBackup ( )
    {
        bool cloudConfigured =
            string.IsNullOrWhiteSpace ( _firebaseProjectId ) == false
            && string.IsNullOrWhiteSpace ( _firebaseApiKey ) == false;

        //설정 전체 검사는 실제 복원/복구 시점에 수행
        bool contentConfigured =
            _backupUpgradeMap != null && _backupMissionMap != null;

        //인증 설정/보관소 연결
        FirebaseAuthService auth = new FirebaseAuthService ( _firebaseProjectId , _firebaseApiKey );
        //저장 위치 준비
        FirebaseBackupStorage cloud = new FirebaseBackupStorage ( _firebaseProjectId , 0 );

        //플레이 씬이 진행을 사용하는 동안 백업/복원 접수 차단
        _backupService = new ProgressBackupService (
            _service , auth , cloud , _backupUpgradeMap , _backupMissionMap ,
            cloudConfigured , contentConfigured ,
            ( ) => _isInitialized && isActiveAndEnabled && _sceneOwner == null );
    }

    #endregion

    #region ----- 진행 관리 -----

    /// <summary>
    /// 기존 진행만 준비
    /// </summary>
    /// <returns>불러오기 결과, 빈 진행은 NotFound</returns>
    public SaveResult TryLoadProgress ( )
    {
        //제목 화면 등에서 기존 진행만 읽고, 파일이 없으면 신규 생성하지 않음
        if ( CanChangeProgress == false )
            return SaveResult.Unavailable;

        if ( HasProgress == true )
            return SaveResult.Success;

        return _service.TryLoad ( out _ );
    }

    /// <summary>
    /// 플레이 요청에 사용할 진행 준비
    /// </summary>
    /// <returns>진행 준비 결과</returns>
    public SaveResult TryStartProgress ( )
    {
        //플레이 시작 요청에서만 빈 슬롯에 신규 진행 생성 허용
        if ( CanChangeProgress == false || _sceneOwner != null )
            return SaveResult.Unavailable;

        return _service.TryLoadOrCreate ( out _ );
    }

    /// <summary>
    /// 미저장 시스템 처리 후 확정 데이터 저장
    /// </summary>
    /// <returns>파일 저장 결과</returns>
    public SaveResult TrySaveCurrent ( )
    {
        if ( CanChangeProgress == false || HasProgress == false )
            return SaveResult.Unavailable;

        return _service.TrySaveCurrent ( );
    }

    /// <summary>
    /// 씬 연결이 없는 진행 삭제
    /// </summary>
    /// <returns>삭제 결과</returns>
    public SaveResult TryDeleteProgress ( )
    {
        if ( CanChangeProgress == false || _sceneOwner != null )
            return SaveResult.Unavailable;

        return _service.TryDelete ( );
    }

    #endregion

    #region ----- 진행 조회 -----

    /// <summary>
    /// 준비된 진행의 최고 별 조회
    /// </summary>
    /// <param name="mapId">맵 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="bestStars">최고 별, 미클리어는 0</param>
    /// <returns>진행 조회 가능 여부</returns>
    public bool TryGetBestStars (
        string mapId , MissionDifficulty difficulty , out int bestStars )
    {
        bestStars = 0;

        if ( HasProgress == false )
            return false;

        bestStars = _service.GetBestStars ( mapId , difficulty );
        return true;
    }

    /// <summary>
    /// 최고 점수 조회
    /// </summary>
    /// <param name="mapId">맵</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="hasScore">기록 여부</param>
    /// <param name="score">최고 점수</param>
    /// <returns>조회 가능 여부</returns>
    public bool TryGetBestScore (
        string mapId , MissionDifficulty difficulty , out bool hasScore , out int score )
    {
        hasScore = false;
        score = 0;
        if ( HasProgress == false )
            return false;

        hasScore = _service.TryGetBestScore ( mapId , difficulty , out score );
        return true;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 준비된 확정 진행의 디버그용 복사본 조회
    /// </summary>
    /// <returns>저장 진행 복사본</returns>
    public SaveData GetDebugSnapshot ( )
    {
        return _service.GetSnapshot ( );
    }
#endif

    #endregion

    #region ----- 사용권 -----

    /// <summary>
    /// 단일 씬에 서비스와 복원 데이터 연결
    /// </summary>
    /// <param name="owner">연결할 씬</param>
    /// <param name="service">공유 서비스</param>
    /// <param name="data">복원 데이터</param>
    /// <returns>연결 성공 여부</returns>
    public bool TryAttachScene (
        MonoBehaviour owner ,
        out SaveService service ,
        out SaveData data )
    {
        service = null;
        data = null;

        if ( CanChangeProgress == false || HasProgress == false
            || ( _sceneOwner != null && _sceneOwner != owner ) )
            return false;

        //서비스는 공유하되 씬 초기화에는 분리된 복사본 전달
        _sceneOwner = owner;
        service = _service;
        data = _service.GetSnapshot ( );
        return true;
    }

    /// <summary>
    /// 해당 씬의 진행 사용 해제
    /// </summary>
    /// <param name="owner">해제할 씬</param>
    public void DetachScene ( MonoBehaviour owner )
    {
        if ( _sceneOwner == owner )
            _sceneOwner = null;

        if ( ReferenceEquals ( owner , null ) == false && ReferenceEquals ( _backupScene , owner ) )
        {
            _backupScene = null;
            _isSceneRestoreLocked = false;
            _backupService.CancelPreparedBackup ( );
        }
    }

    /// <summary>
    /// 결과 씬 사용권을 백업 동안 보관
    /// </summary>
    /// <param name="owner">현재 진행을 사용하는 씬</param>
    /// <returns>전환 성공 여부</returns>
    public bool TrySuspendScene ( MonoBehaviour owner )
    {
        if ( owner == null || _sceneOwner != owner || CanChangeProgress == false )
            return false;

        _backupScene = owner;
        _sceneOwner = null;
        return true;
    }

    /// <summary>
    /// 백업이 끝난 결과 씬에 사용권 반환
    /// </summary>
    /// <param name="owner">사용권을 보관한 씬</param>
    /// <returns>반환 성공 여부</returns>
    public bool TryResumeScene ( MonoBehaviour owner )
    {
        if ( owner == null || _backupScene != owner || _isInitialized == false
            || isActiveAndEnabled == false || _isSceneRestoreLocked == true || IsBackupPending == true )
            return false;

        _sceneOwner = owner;
        _backupScene = null;
        return true;
    }

    /// <summary>
    /// 백업 요청과 사용권 보관 대상 일치 확인
    /// </summary>
    /// <param name="owner">결과 씬 또는 기존 요청의 null</param>
    /// <returns>요청 가능 여부</returns>
    bool MatchesBackupScene ( MonoBehaviour owner )
    {
        return ReferenceEquals ( _backupScene , owner )
            && ( ReferenceEquals ( owner , null ) || owner != null );
    }

    #endregion

    #region ----- 백업 -----

    /// <summary>
    /// 수동 백업 요청
    /// </summary>
    /// <param name="owner">백업 중 사용권을 맡긴 결과 씬</param>
    /// <returns>완료 또는 서버 교체 확인 필요</returns>
    public Task<ProgressBackupResponse> PrepareBackupAsync ( MonoBehaviour owner = null )
    {
        return MatchesBackupScene ( owner )
            ? _backupService.PrepareBackupAsync ( ( ) => MatchesBackupScene ( owner ) )
            : Task.FromResult ( new ProgressBackupResponse ( ProgressBackupResult.Unavailable ) );
    }

    /// <summary>
    /// 확인한 서버 백업 교체 요청
    /// </summary>
    /// <param name="owner">백업 중 사용권을 맡긴 결과 씬</param>
    /// <returns>업로드 결과</returns>
    public Task<ProgressBackupResponse> ConfirmBackupAsync ( MonoBehaviour owner = null )
    {
        return MatchesBackupScene ( owner )
            ? _backupService.ConfirmBackupAsync ( ( ) => MatchesBackupScene ( owner ) )
            : Task.FromResult ( new ProgressBackupResponse ( ProgressBackupResult.Unavailable ) );
    }

    /// <summary>
    /// 서버 백업 교체 확인 취소
    /// </summary>
    /// <param name="owner">백업 중 사용권을 맡긴 결과 씬</param>
    /// <returns>취소 여부</returns>
    public bool CancelPreparedBackup ( MonoBehaviour owner = null )
    {
        return MatchesBackupScene ( owner ) && _backupService.CancelPreparedBackup ( );
    }

    /// <summary>
    /// 사용자 확인 후 수동 복원 요청
    /// </summary>
    /// <param name="owner">복원 중 사용권을 맡긴 결과 씬</param>
    /// <returns>로컬 파일과 공유 진행 반영 결과</returns>
    public Task<ProgressBackupResponse> RestoreProgressAsync ( MonoBehaviour owner = null )
    {
        if ( MatchesBackupScene ( owner ) == false || _isSceneRestoreLocked == true )
            return Task.FromResult ( new ProgressBackupResponse ( ProgressBackupResult.Unavailable ) );

        return owner == null ? _backupService.RestoreProgressAsync ( ) : RestoreForSceneAsync ( owner );
    }

    /// <summary>
    /// 복원 결과 확정까지 이전 씬 재연결 차단
    /// </summary>
    /// <param name="owner">복원을 요청한 결과 씬</param>
    /// <returns>로컬 진행 반영 결과</returns>
    async Task<ProgressBackupResponse> RestoreForSceneAsync ( MonoBehaviour owner )
    {
        _isSceneRestoreLocked = true;
        bool restored = false;
        try
        {
            ProgressBackupResponse response = await _backupService.RestoreProgressAsync (
                ( ) => MatchesBackupScene ( owner ) );
            restored = response.Result == ProgressBackupResult.Success;
            return response;
        }
        finally
        {
            //복원 성공 시 이전 진행의 재연결은 해제까지 계속 차단
            if ( restored == false && MatchesBackupScene ( owner ) == true )
                _isSceneRestoreLocked = false;
        }
    }

    /// <summary>
    /// 복원 전 로컬 진행으로 복구 요청
    /// </summary>
    /// <returns>복구 결과</returns>
    public ProgressBackupResponse RecoverPreviousProgress ( )
    {
        if ( MatchesBackupScene ( null ) == false )
            return new ProgressBackupResponse ( ProgressBackupResult.Unavailable );

        return _backupService.RecoverPreviousProgress ( );
    }

    #endregion

    #region ----- 정리 -----

    /// <summary>
    /// 진행 변경 차단과 백업 통신 정리
    /// </summary>
    public void Shutdown ( )
    {
        if ( _isInitialized == false )
            return;

        _isInitialized = false;
        _backupService.Dispose ( );
    }

    private void OnDestroy ( )
    {
        Shutdown ( );
    }

    #endregion
}
