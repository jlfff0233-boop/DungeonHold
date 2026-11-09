using System;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// 진행 백업 - 수동 백업/복원 순서와 작업 수명 관리
/// </summary>
public class ProgressBackupService : IDisposable
{
    SaveService _save;      //기존 로컬 저장
    FirebaseAuthService _auth;      //익명 인증
    FirebaseBackupStorage _cloud;       //원격 저장소
    UpgradeDataMap _upgradeMap;     //현재 강화 설정
    MissionDataMap _missionMap;     //현재 미션 설정
    Func<bool> _canUseProgress;     //관리자 활성/씬 미연결 조건

    bool _cloudConfigured;       //Firebase 설정 연결 여부
    bool _contentConfigured;     //설정 검사 참조 연결 여부
    bool _isProcessing;          //현재 요청 처리 중
    bool _isDisposed;            //서비스 수명 종료
    string _pendingJson;         //확인 중인 업로드 후보
    string _pendingUpdatedAt;    //확인한 서버 문서 버전
    ProgressBackupPhase _phase;  //현재 백업/복원 단계

    /// <summary>
    /// 현재 백업/복원 처리 단계
    /// </summary>
    public ProgressBackupPhase Phase => _phase;

    /// <summary>
    /// 진행 시작/삭제/씬 연결 차단 여부
    /// </summary>
    public bool BlocksProgressChanges =>
        _isProcessing == true || _pendingJson != null;

    /// <summary>
    /// 백업 요청 종류 - 필수 설정과 확인 후보 조건 구분
    /// </summary>
    enum RequestType
    {
        PrepareBackup,      //새 백업 준비
        ConfirmBackup,      //확인한 백업 교체
        Restore,       //서버 백업 복원
        Recover        //로컬 원본 복구
    }

    /// <summary>
    /// 로컬 저장과 백업 실행 모듈 연결
    /// </summary>
    /// <param name="save">기존 저장 서비스</param>
    /// <param name="auth">인증 서비스</param>
    /// <param name="cloud">원격 저장소</param>
    /// <param name="upgradeMap">강화 설정</param>
    /// <param name="missionMap">미션 설정</param>
    /// <param name="cloudConfigured">Firebase 설정 연결 여부</param>
    /// <param name="contentConfigured">설정 검사 참조 연결 여부</param>
    /// <param name="canUseProgress">현재 진행 사용 가능 여부</param>
    public ProgressBackupService (
        SaveService save ,
        FirebaseAuthService auth ,
        FirebaseBackupStorage cloud ,
        UpgradeDataMap upgradeMap ,
        MissionDataMap missionMap ,
        bool cloudConfigured ,
        bool contentConfigured ,
        Func<bool> canUseProgress )
    {
        _save = save;
        _auth = auth;
        _cloud = cloud;
        _upgradeMap = upgradeMap;
        _missionMap = missionMap;
        _cloudConfigured = cloudConfigured;
        _contentConfigured = contentConfigured;
        _canUseProgress = canUseProgress;
    }

    /// <summary>
    /// 수동 백업 준비
    /// </summary>
    /// <param name="isCurrent">원래 요청이 유효한지 확인</param>
    /// <returns>완료 또는 서버 교체 확인 필요</returns>
    public async Task<ProgressBackupResponse> PrepareBackupAsync ( Func<bool> isCurrent = null )
    {
        if ( TryBeginRequest (
            RequestType.PrepareBackup , out ProgressBackupResponse failure ) == false )
            return failure;

        try
        {
            //확정 진행을 JSON으로 고정한 뒤 인증/서버 조회 진행
            _phase = ProgressBackupPhase.PreparingLocal;
            SaveResult localResult = _save.TryCreateBackup ( out string json , out string error );

            if ( localResult != SaveResult.Success )
                return ProgressBackupResponse.FromLocal ( localResult , error , ProgressBackupResult.NoLocalProgress );

            if ( Encoding.UTF8.GetByteCount ( json ) > CloudProgressBackup.MaxJsonBytes )
                return new ProgressBackupResponse ( ProgressBackupResult.TooLarge );

            //로컬 후보 준비에 성공한 요청만 인증 진행
            _phase = ProgressBackupPhase.Authenticating;
            ProgressBackupResponse authResult = await _auth.EnsureAuthenticatedAsync ( );

            if ( authResult.Result != ProgressBackupResult.Success )
                return authResult;

            //현재 사용자의 서버 문서를 조회해 최초 저장/덮어쓰기 구분
            _phase = ProgressBackupPhase.ReadingCloud;
            var read = await _cloud.GetBackupAsync ( _auth.UserId , _auth.IdToken );
            HandleRemoteAuthFailure ( read.Response );

            if ( read.Response.Result != ProgressBackupResult.Success
                && read.Response.Result != ProgressBackupResult.NoCloudBackup )
                return read.Response;

            //씬이 해제된 뒤에는 확인 후보나 업로드를 시작하지 않음
            if ( _isDisposed == true || isCurrent?.Invoke ( ) == false )
                return new ProgressBackupResponse ( ProgressBackupResult.Canceled );

            if ( read.Response.Result == ProgressBackupResult.Success )
            {
                CloudProgressBackup backup = read.Backup;

                //현재 앱보다 최신인 백업은 교체 후보로 만들지 않음
                if ( backup.SaveVersion > SaveData.CurrentVersion
                    || backup.FormatVersion > CloudProgressBackup.CurrentFormat )
                {
                    return new ProgressBackupResponse (
                        ProgressBackupResult.UnsupportedVersion ,
                        "현재 앱보다 최신 형식의 서버 백업입니다." );
                }
            }

            //확인 대기 중 사용할 후보와 조회한 서버 문서의 교체 조건 보관
            _pendingJson = json;
            _pendingUpdatedAt = read.Response.Result == ProgressBackupResult.Success
                ? read.Backup.UpdatedAt : null;

            //기존 문서는 확인을 기다리고, 최초 백업만 바로 전송
            if ( read.Response.Result == ProgressBackupResult.Success )
                return new ProgressBackupResponse ( ProgressBackupResult.ConfirmationRequired , updatedAt: _pendingUpdatedAt );

            //최초 백업은 인증을 반복하지 않고 바로 전송
            return await UploadPendingCoreAsync ( );
        }
        finally
        {
            //요청 잠금은 해제하되 덮어쓰기 확인 후보는 유지
            FinishRequest ( );
        }
    }

    /// <summary>
    /// 사용자가 확인한 서버 백업 교체
    /// </summary>
    /// <param name="isCurrent">원래 요청이 유효한지 확인</param>
    /// <returns>업로드 결과</returns>
    public async Task<ProgressBackupResponse> ConfirmBackupAsync ( Func<bool> isCurrent = null )
    {
        if ( TryBeginRequest (
            RequestType.ConfirmBackup , out ProgressBackupResponse failure ) == false )
            return failure;

        try
        {
            _phase = ProgressBackupPhase.Authenticating;
            //확인 대기 동안 만료될 수 있는 토큰 준비
            ProgressBackupResponse authResult = await _auth.EnsureAuthenticatedAsync ( );

            //인증 중 씬이 해제되면 남은 교체 후보도 폐기
            if ( _isDisposed == true || isCurrent?.Invoke ( ) == false )
            {
                ClearPendingBackup ( );
                return new ProgressBackupResponse ( ProgressBackupResult.Canceled );
            }

            if ( authResult.Result != ProgressBackupResult.Success )
                return authResult;

            //준비 단계의 JSON을 그대로 전송하며 확인 시점에 다시 추출하지 않음
            return await UploadPendingCoreAsync ( );
        }
        finally
        {
            FinishRequest ( );
        }
    }

    /// <summary>
    /// 인증 완료한 업로드 후보 전송
    /// </summary>
    /// <returns>원격 저장 결과</returns>
    async Task<ProgressBackupResponse> UploadPendingCoreAsync ( )
    {
        //인증을 마친 두 경로에서 같은 후보와 교체 조건 사용
        _phase = ProgressBackupPhase.Uploading;
        ProgressBackupResponse response = await _cloud.UploadBackupAsync (
            _auth.UserId , _auth.IdToken , _pendingJson ,
            SaveData.CurrentVersion , _pendingUpdatedAt );

        HandleRemoteAuthFailure ( response );
        //실패나 응답 유실 후 자동 재전송하지 않음
        ClearPendingBackup ( );
        return response;
    }

    /// <summary>
    /// 서버 백업 교체 확인 취소
    /// </summary>
    /// <returns>취소 여부</returns>
    public bool CancelPreparedBackup ( )
    {
        if ( _isProcessing == true || _pendingJson == null )
            return false;

        //확인 후보만 해제하며 진행 중인 네트워크 요청을 취소하는 함수는 아님
        ClearPendingBackup ( );
        _phase = ProgressBackupPhase.Idle;
        return true;
    }

    /// <summary>
    /// 서버 백업 복원
    /// </summary>
    /// <param name="isCurrent">원래 요청이 유효한지 확인</param>
    /// <returns>로컬 반영 결과</returns>
    public async Task<ProgressBackupResponse> RestoreProgressAsync ( Func<bool> isCurrent = null )
    {
        if ( TryBeginRequest (
            RequestType.Restore , out ProgressBackupResponse failure ) == false )
            return failure;

        try
        {
            //사용자 확인은 호출부에서 마친 상태로, 같은 UID의 문서 다운로드
            _phase = ProgressBackupPhase.Authenticating;
            ProgressBackupResponse authResult = await _auth.EnsureAuthenticatedAsync ( );

            if ( authResult.Result != ProgressBackupResult.Success )
                return authResult;

            _phase = ProgressBackupPhase.ReadingCloud;
            var read = await _cloud.GetBackupAsync ( _auth.UserId , _auth.IdToken );
            HandleRemoteAuthFailure ( read.Response );

            if ( read.Response.Result != ProgressBackupResult.Success )
                return read.Response;

            //네트워크 대기 중 요청 씬이 해제됐다면 로컬 파일을 변경하지 않음
            if ( _isDisposed == true || isCurrent?.Invoke ( ) == false )
                return new ProgressBackupResponse ( ProgressBackupResult.Canceled );

            //조회 성공만으로 복원하지 않고 내부 데이터 검사 결과 확인
            _phase = ProgressBackupPhase.ValidatingRestore;
            CloudProgressBackup backup = read.Backup;
            if ( backup.PayloadResult != ProgressBackupResult.Success )
                return new ProgressBackupResponse ( backup.PayloadResult );

            _phase = ProgressBackupPhase.ApplyingRestore;
            //로컬 서비스가 후보 검사/원본 보존/저장을 마쳐야 복원 성공
            SaveResult result = _save.TryRestoreBackup (
                backup.SaveJson , backup.SaveVersion , CheckContentCompatibility );
            ProgressBackupResponse local = ProgressBackupResponse.FromLocal (
                result , _save.LastError , ProgressBackupResult.NoCloudBackup );

            if ( local.Result != ProgressBackupResult.Success )
                return local;

            return new ProgressBackupResponse (
                ProgressBackupResult.Success , updatedAt: backup.UpdatedAt );
        }
        finally
        {
            FinishRequest ( );
        }
    }

    /// <summary>
    /// 인증 없이 복원 전 로컬 진행으로 복구
    /// </summary>
    /// <returns>복구 결과</returns>
    public ProgressBackupResponse RecoverPreviousProgress ( )
    {
        if ( TryBeginRequest (
            RequestType.Recover , out ProgressBackupResponse failure ) == false )
            return failure;

        try
        {
            //서버 접근 없이 복원 직전 보관한 로컬 파일 사용
            _phase = ProgressBackupPhase.ApplyingRestore;
            SaveResult result = _save.TryRecoverBeforeRestore ( CheckContentCompatibility );
            return ProgressBackupResponse.FromLocal (
                result , _save.LastError , ProgressBackupResult.NoRecoveryData );
        }
        finally
        {
            FinishRequest ( );
        }
    }

    /// <summary>
    /// 복원 시점의 설정과 저장 후보 호환 확인
    /// </summary>
    /// <param name="data">형식 검사를 마친 후보</param>
    /// <returns>설정과 기록의 호환 여부</returns>
    bool CheckContentCompatibility ( SaveData data )
    {
        if ( _upgradeMap.Validate ( ) == false || _missionMap.Validate ( ) == false )
            return false;

        //실제 플레이 상태를 변경하지 않는 임시 진행 객체 사용
        UpgradeProgress upgrades = new UpgradeProgress ( );

        if ( upgrades.Init ( data.Upgrades , _upgradeMap ) == false )
            return false;

        MissionProgress missions = new MissionProgress ( );
        return missions.Init ( data.MissionKillCounts , data.Missions , _missionMap );
    }

    /// <summary>
    /// 요청 목적에 필요한 조건 확인 후 작업 시작
    /// </summary>
    /// <param name="request">요청 종류</param>
    /// <param name="failure">접수 실패 결과</param>
    /// <returns>작업 접수 여부</returns>
    bool TryBeginRequest ( RequestType request , out ProgressBackupResponse failure )
    {
        failure = new ProgressBackupResponse ( ProgressBackupResult.Unavailable );
        if ( _isDisposed == true || _isProcessing == true || _canUseProgress ( ) == false )
            return false;

        //로컬 복구는 인증 설정 없이 허용하고, 복원/복구에만 콘텐츠 설정 필요
        bool needsCloud = request != RequestType.Recover;
        bool needsContent = request == RequestType.Restore || request == RequestType.Recover;

        if ( ( needsCloud == true && _cloudConfigured == false )
            || ( needsContent == true && _contentConfigured == false ) )
        {
            failure = new ProgressBackupResponse ( ProgressBackupResult.ConfigurationMissing );
            return false;
        }

        //확인 요청은 후보가 있어야 하며, 다른 요청은 확인 대기 중 접수하지 않음
        if ( request == RequestType.ConfirmBackup )
        {
            if ( _pendingJson == null )
                return false;
        }
        else if ( _pendingJson != null )
        {
            return false;
        }

        //첫 비동기 대기 전에 잠그고 요청 종료 시 FinishRequest에서 해제
        _isProcessing = true;
        return true;
    }

    /// <summary>
    /// 서버가 거절한 ID 토큰만 해제
    /// </summary>
    /// <param name="response">원격 요청 결과</param>
    void HandleRemoteAuthFailure ( ProgressBackupResponse response )
    {
        //현재 요청을 반복하지 않고 다음 수동 요청에서 같은 사용자로 토큰 갱신
        if ( response.Result == ProgressBackupResult.AuthenticationFailed )
            _auth.InvalidateIdToken ( );
    }

    /// <summary>
    /// 작업 종료 후 확인 대기 또는 기본 상태로 전환
    /// </summary>
    void FinishRequest ( )
    {
        //통신 종료와 확인 종료를 구분해 진행 변경 차단 유지
        _isProcessing = false;
        _phase = _pendingJson != null
            ? ProgressBackupPhase.AwaitingConfirmation : ProgressBackupPhase.Idle;
    }

    /// <summary>
    /// 업로드 확인 후보 해제
    /// </summary>
    void ClearPendingBackup ( )
    {
        _pendingJson = null;
        _pendingUpdatedAt = null;
    }

    /// <summary>
    /// 새 요청 차단과 준비 후보/원격 요청 정리
    /// </summary>
    public void Dispose ( )
    {
        //후보를 먼저 해제하고 대기 중 통신 중단
        _isDisposed = true;
        ClearPendingBackup ( );
        _auth.Dispose ( );
        _cloud.Dispose ( );
    }
}
