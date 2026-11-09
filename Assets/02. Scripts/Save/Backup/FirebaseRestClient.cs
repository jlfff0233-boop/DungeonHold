using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Firebase REST 요청 - 비동기 전송과 요청 자원 관리
/// </summary>
public class FirebaseRestClient : IDisposable
{
    const int TimeoutSeconds = 15;      //단일 요청 제한시간

    UnityWebRequest _activeRequest;     //현재 전송 중인 요청
    bool _isDisposed;              //클라이언트 수명 종료

    /// <summary>
    /// HTTP 요청 전송과 완료 대기
    /// </summary>
    /// <param name="url">요청 주소</param>
    /// <param name="method">HTTP 메서드</param>
    /// <param name="body">전송 본문, GET은 null</param>
    /// <param name="contentType">본문 형식</param>
    /// <param name="idToken">Firebase ID 토큰</param>
    /// <returns>HTTP 응답</returns>
    public async Task<FirebaseHttpResponse> SendAsync (
        string url , string method , string body ,
        string contentType , string idToken = null )
    {
        if ( _isDisposed == true )
            return new FirebaseHttpResponse ( canceled: true );

        //요청마다 자원을 소유하고 응답 반환/취소 후 함께 해제
        using ( UnityWebRequest request = new UnityWebRequest ( url , method ) )
        {
            request.downloadHandler = new DownloadHandlerBuffer ( );
            request.timeout = TimeoutSeconds;

            if ( body != null )
            {
                request.uploadHandler = new UploadHandlerRaw ( Encoding.UTF8.GetBytes ( body ) );
                request.SetRequestHeader ( "Content-Type" , contentType );
            }

            //인증 발급 요청에는 토큰 없이, Firestore 요청에는 Bearer 인증 사용
            if ( idToken != null )
                request.SetRequestHeader ( "Authorization" , "Bearer " + idToken );

            //종료 시 Dispose가 대기 중인 요청을 중단할 수 있도록 등록
            _activeRequest = request;

            try
            {
                UnityWebRequestAsyncOperation operation = request.SendWebRequest ( );

                //메인 스레드를 막지 않고 다음 프레임으로 양보
                while ( operation.isDone == false )
                    await Awaitable.NextFrameAsync ( );

                //종료로 중단된 응답은 호출부에서 정상 결과로 사용하지 않음
                if ( _isDisposed == true )
                    return new FirebaseHttpResponse ( canceled: true );

                //통신 결과만 전달하고 인증/Firestore 오류 해석은 호출부에서 수행
                return new FirebaseHttpResponse (
                    statusCode: request.responseCode ,
                    networkFailed:
                        request.result == UnityWebRequest.Result.ConnectionError
                        || request.result == UnityWebRequest.Result.DataProcessingError ,
                    body: request.downloadHandler.text ,
                    error: request.error );
            }
            finally
            {
                //현재 참조를 해제하고 요청 자원은 using에서 반환
                _activeRequest = null;
            }
        }
    }

    /// <summary>
    /// 현재 요청 중단과 이후 전송 차단
    /// </summary>
    public void Dispose ( )
    {
        //전송을 중단하고 자원 반환은 SendAsync의 using에서 수행
        _isDisposed = true;
        if ( _activeRequest != null )
            _activeRequest.Abort ( );
    }
}
