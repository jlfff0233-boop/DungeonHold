using System;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Firebase 익명 인증 - 기존 사용자 복구와 토큰 갱신
/// </summary>
public class FirebaseAuthService : IDisposable
{
    const string AnonymousSignUpUrl =
        "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=";       //익명 가입
    const string TokenRefreshUrl =
        "https://securetoken.googleapis.com/v1/token?key=";     //토큰 갱신

    string _apiKey;     //Firebase 웹 API 키
    FirebaseRestClient _http;            //인증 요청 소유
    FirebaseAuthSessionStorage _storage;        //Windows 인증 보관소
    FirebaseAuthSession _session;       //현재 익명 사용자
    string _idToken;        //현재 ID 토큰
    double _expiresAt;      //토큰 갱신 기준 시각
    bool _sessionNeedsSave;     //새 인증 정보 보관 필요
    bool _isDisposed;       //인증 서비스 수명 종료

    /// <summary>
    /// 인증 준비한 사용자 UID
    /// </summary>
    public string UserId => _session.UserId;

    /// <summary>
    /// 인증 준비한 ID 토큰
    /// </summary>
    public string IdToken => _idToken;

    /// <summary>
    /// 인증 설정과 보관소 연결
    /// </summary>
    /// <param name="projectId">Firebase 프로젝트 ID</param>
    /// <param name="apiKey">Firebase 웹 API 키</param>
    public FirebaseAuthService ( string projectId , string apiKey )
    {
        _apiKey = apiKey;
        _http = new FirebaseRestClient ( );
        _storage = new FirebaseAuthSessionStorage ( projectId );
    }

    /// <summary>
    /// 기존 익명 사용자의 유효한 인증 준비
    /// </summary>
    /// <returns>인증 준비 결과</returns>
    public async Task<ProgressBackupResponse> EnsureAuthenticatedAsync ( )
    {
        if ( _isDisposed == true )
            return new ProgressBackupResponse ( ProgressBackupResult.Canceled );

        //발급 완료 후 보관만 실패한 정보는 같은 내용으로 보관 재시도
        if ( _sessionNeedsSave == true )
        {
            if ( _storage.TrySave ( _session , out string saveError ) == false )
                return new ProgressBackupResponse (
                    ProgressBackupResult.AuthenticationStorageFailed , saveError );
            _sessionNeedsSave = false;
        }

        //보관까지 마친 유효 토큰은 네트워크 요청 없이 재사용
        if ( _idToken != null && Time.realtimeSinceStartupAsDouble < _expiresAt )
            return new ProgressBackupResponse ( ProgressBackupResult.Success );

        //재실행 후 첫 요청에서 Windows에 보관한 사용자 복구
        if ( _session == null )
        {
            AuthSessionReadResult readResult = _storage.TryLoad (
                out FirebaseAuthSession stored , out string error );

            //미지원 플랫폼
            if ( readResult == AuthSessionReadResult.Unsupported )
                return new ProgressBackupResponse ( ProgressBackupResult.UnsupportedPlatform , error );

            //기존 정보 읽기 실패 시
            if ( readResult == AuthSessionReadResult.Failed )
                return new ProgressBackupResponse ( ProgressBackupResult.AuthenticationStorageFailed , error );

            //보관 정보가 없는 경우에만 최초 익명 사용자 생성
            if ( readResult == AuthSessionReadResult.NotFound )
                return await CreateAnonymousAsync ( );

            _session = stored;
        }

        //기존 사용자의 갱신 실패를 새 사용자 생성으로 대체하지 않음
        return await RefreshAsync ( );
    }

    /// <summary>
    /// 최초 익명 사용자 생성
    /// </summary>
    /// <returns>인증 결과</returns>
    async Task<ProgressBackupResponse> CreateAnonymousAsync ( )
    {
        //returnSecureToken으로 생성한 사용자의 ID/갱신 토큰도 함께 요청
        string url = AnonymousSignUpUrl + UnityWebRequest.EscapeURL ( _apiKey );
        FirebaseHttpResponse response = await _http.SendAsync (
            url , "POST" , "{\"returnSecureToken\":true}" , "application/json" );

        ProgressBackupResponse result = CheckResponse ( response );

        if ( result.Result != ProgressBackupResult.Success )
            return result;

        if ( BackupJson.TryRead (
            response.Body , out AnonymousResponse data ) == false )
            return InvalidResponse ( );

        return AcceptTokens (
            data.localId , data.idToken , data.refreshToken , data.expiresIn );
    }

    /// <summary>
    /// 보관한 사용자의 인증 토큰 갱신
    /// </summary>
    /// <returns>인증 결과</returns>
    async Task<ProgressBackupResponse> RefreshAsync ( )
    {
        //grant_type은 갱신 방식, refresh_token은 보관한 사용자의 갱신 토큰
        string url = TokenRefreshUrl + UnityWebRequest.EscapeURL ( _apiKey );
        string body = "grant_type=refresh_token&refresh_token="
            + UnityWebRequest.EscapeURL ( _session.RefreshToken );

        FirebaseHttpResponse response = await _http.SendAsync (
            url , "POST" , body , "application/x-www-form-urlencoded" );

        ProgressBackupResponse result = CheckResponse ( response );

        if ( result.Result != ProgressBackupResult.Success )
            return result;

        if ( BackupJson.TryRead (
            response.Body , out RefreshResponse data ) == false )
            return InvalidResponse ( );

        //발급/갱신 응답의 필드 이름은 달라도 검사와 보관은 같은 경로 사용
        return AcceptTokens (
            data.user_id , data.id_token , data.refresh_token , data.expires_in );
    }

    /// <summary>
    /// 인증 응답 반영과 재실행용 정보 보관
    /// </summary>
    /// <param name="userId">응답 UID</param>
    /// <param name="idToken">새 ID 토큰</param>
    /// <param name="refreshToken">새 갱신 토큰</param>
    /// <param name="expiresIn">유효 시간 초</param>
    /// <returns>인증 반영 결과</returns>
    ProgressBackupResponse AcceptTokens (
        string userId , string idToken , string refreshToken , string expiresIn )
    {
        if ( _isDisposed == true )
            return new ProgressBackupResponse ( ProgressBackupResult.Canceled );

        if ( string.IsNullOrWhiteSpace ( userId ) == true
            || string.IsNullOrWhiteSpace ( idToken ) == true
            || string.IsNullOrWhiteSpace ( refreshToken ) == true
            || int.TryParse ( expiresIn , NumberStyles.None ,
                CultureInfo.InvariantCulture , out int seconds ) == false
            || seconds <= 0 )
            return InvalidResponse ( );

        //갱신 응답으로 백업 소유자가 바뀌지 않도록 기존 UID 대조
        if ( _session != null && _session.UserId != userId )
            return new ProgressBackupResponse (
                ProgressBackupResult.AuthenticationFailed , "인증 갱신 결과의 사용자 UID가 다릅니다." );

        //새 갱신 토큰을 유지하되 보관 성공 전까지 사용 성공을 반환하지 않음
        _session = new FirebaseAuthSession ( userId , refreshToken );
        _idToken = idToken;
        //요청 도중 만료되지 않도록 실제 만료보다 앞서 갱신
        _expiresAt = Time.realtimeSinceStartupAsDouble + Math.Max ( 0 , seconds - 60 );
        _sessionNeedsSave = true;

        //보관 실패 시 새 정보를 유지하고 다음 수동 요청에서 보관만 재시도
        if ( _storage.TrySave ( _session , out string error ) == false )
            return new ProgressBackupResponse ( ProgressBackupResult.AuthenticationStorageFailed , error );

        _sessionNeedsSave = false;
        return new ProgressBackupResponse ( ProgressBackupResult.Success );
    }

    /// <summary>
    /// 거절된 ID 토큰 해제
    /// </summary>
    public void InvalidateIdToken ( )
    {
        //UID와 갱신 토큰은 유지해 다음 요청도 같은 사용자로 인증
        _idToken = null;
        _expiresAt = 0;
    }

    /// <summary>
    /// 인증 요청의 HTTP 결과 변환
    /// </summary>
    /// <param name="response">HTTP 응답</param>
    /// <returns>인증 요청 결과</returns>
    ProgressBackupResponse CheckResponse ( FirebaseHttpResponse response )
    {
        if ( response.Canceled == true )
            return new ProgressBackupResponse ( ProgressBackupResult.Canceled );

        if ( response.NetworkFailed == true )
            return new ProgressBackupResponse (
                ProgressBackupResult.NetworkFailed , response.Error );

        if ( response.StatusCode < 200 || response.StatusCode >= 300 )
            return new ProgressBackupResponse ( ProgressBackupResult.AuthenticationFailed ,
                $"인증 요청 실패: HTTP {response.StatusCode}" );

        return new ProgressBackupResponse ( ProgressBackupResult.Success );
    }

    /// <summary>
    /// 인증 응답 형식 오류 구성
    /// </summary>
    /// <returns>인증 실패 응답</returns>
    ProgressBackupResponse InvalidResponse ( )
    {
        return new ProgressBackupResponse ( ProgressBackupResult.AuthenticationFailed ,
            "Firebase 인증 응답 형식이 올바르지 않습니다." );
    }

    /// <summary>
    /// 인증 요청 중단과 메모리 인증 해제
    /// </summary>
    public void Dispose ( )
    {
        //메모리 인증만 해제하고 재실행용 Windows 보관 정보는 유지
        _isDisposed = true;
        _http.Dispose ( );
        _idToken = null;
        _session = null;
        _sessionNeedsSave = false;
    }

    /// <summary>
    /// 익명 가입 응답 - 최초 UID와 인증 토큰 수신
    /// </summary>
    [Serializable]
    class AnonymousResponse
    {
        public string localId;      //사용자 UID
        public string idToken;      //ID 토큰
        public string refreshToken;     //갱신 토큰
        public string expiresIn;    //유효 시간 초
    }

    /// <summary>
    /// 인증 갱신 응답 - 기존 사용자의 새 토큰 수신
    /// </summary>
    [Serializable]
    class RefreshResponse
    {
        public string user_id;       //사용자 UID
        public string id_token;      //ID 토큰
        public string refresh_token;        //갱신 토큰
        public string expires_in;    //유효 시간 초
    }
}
