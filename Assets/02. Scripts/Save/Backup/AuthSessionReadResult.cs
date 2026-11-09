
/// <summary>
/// 보관한 인증 정보 읽기 결과
/// </summary>
public enum AuthSessionReadResult
{
    Success,        //기존 인증 정보를 읽음
    NotFound,       //보관한 인증 정보 없음
    Failed,     //기존 인증 정보 읽기 실패
    Unsupported     //현재 플랫폼 미지원
}
