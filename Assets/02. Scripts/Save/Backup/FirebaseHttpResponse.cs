/// <summary>
/// Firebase HTTP 응답 - 통신 상태와 응답 본문 전달
/// </summary>
public readonly struct FirebaseHttpResponse
{
    /// <summary>
    /// 서버가 반환한 HTTP 상태 코드
    /// </summary>
    public long StatusCode { get; }

    /// <summary>
    /// 연결 또는 응답 데이터 처리 실패 여부
    /// </summary>
    public bool NetworkFailed { get; }

    /// <summary>
    /// 클라이언트 수명 종료에 따른 요청 취소 여부
    /// </summary>
    public bool Canceled { get; }

    /// <summary>
    /// 호출부에서 해석할 응답 본문
    /// </summary>
    public string Body { get; }

    /// <summary>
    /// 응답 본문을 제외한 통신 오류 내용
    /// </summary>
    public string Error { get; }

    /// <summary>
    /// 통신 완료 또는 취소 결과 구성
    /// </summary>
    /// <param name="statusCode">HTTP 상태</param>
    /// <param name="networkFailed">통신 실패 여부</param>
    /// <param name="canceled">요청 취소 여부</param>
    /// <param name="body">응답 본문</param>
    /// <param name="error">통신 오류</param>
    public FirebaseHttpResponse (
        long statusCode = 0 , bool networkFailed = false ,
        bool canceled = false , string body = null , string error = null )
    {
        StatusCode = statusCode;
        NetworkFailed = networkFailed;
        Canceled = canceled;
        Body = body;
        Error = error;
    }
}
