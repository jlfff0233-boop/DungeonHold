/// <summary>
/// 익명 인증 세션 - 재실행에 필요한 사용자와 갱신 토큰
/// </summary>
public class FirebaseAuthSession
{
    /// <summary>
    /// 백업 저장 경로를 구분하는 Firebase 사용자 UID
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// 재실행과 ID 토큰 갱신에 사용하는 인증 토큰
    /// </summary>
    public string RefreshToken { get; }

    /// <summary>
    /// 재실행에 사용할 인증 정보 구성
    /// </summary>
    /// <param name="userId">Firebase 사용자 UID</param>
    /// <param name="refreshToken">인증 갱신 토큰</param>
    public FirebaseAuthSession ( string userId , string refreshToken )
    {
        UserId = userId;
        RefreshToken = refreshToken;
    }
}
