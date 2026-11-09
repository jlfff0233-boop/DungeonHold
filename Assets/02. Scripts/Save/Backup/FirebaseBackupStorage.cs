using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Firebase 백업 저장소 - Firestore 문서 조회와 조건부 교체
/// </summary>
public class FirebaseBackupStorage : IDisposable
{
    const string DocumentUrlFormat =
        "https://firestore.googleapis.com/v1/projects/{0}"
        + "/databases/(default)/documents/users/{1}/saves/{2}"; //문서 주소

    string _projectId;        //Firebase 프로젝트
    int _slotId;              //기존 저장 슬롯
    FirebaseRestClient _http;       //Firestore 요청 소유

    /// <summary>
    /// 원격 저장 위치 준비
    /// </summary>
    /// <param name="projectId">Firebase 프로젝트 ID</param>
    /// <param name="slotId">기존 슬롯 번호</param>
    public FirebaseBackupStorage ( string projectId , int slotId )
    {
        _projectId = projectId;
        _slotId = slotId;
        _http = new FirebaseRestClient ( );
    }

    /// <summary>
    /// 사용자 문서와 내부 데이터 상태 조회
    /// </summary>
    /// <param name="userId">인증한 UID</param>
    /// <param name="idToken">유효한 ID 토큰</param>
    /// <returns>문서 조회 결과와 백업 정보</returns>
    public async Task<(
        ProgressBackupResponse Response,
        CloudProgressBackup Backup)> GetBackupAsync (
            string userId , string idToken )
    {
        FirebaseHttpResponse response = await _http.SendAsync (
            GetDocumentUrl ( userId ) , "GET" , null , null , idToken );

        ProgressBackupResponse status = CheckHttpResponse ( response , "백업 조회" );
        if ( status.Result != ProgressBackupResult.Success )
        {
            //404는 백업 없음으로 구분하고 인증/권한 실패는 그대로 반환
            if ( status.Result == ProgressBackupResult.RemoteFailed
                && response.StatusCode == 404 )
                return (new ProgressBackupResponse (
                    ProgressBackupResult.NoCloudBackup ), null);

            return (status, null);
        }

        //덮어쓰기 확인에 필요한 서버 갱신 시각부터 확보
        if ( BackupJson.TryRead ( response.Body , out ReadDocument document ) == false
            || string.IsNullOrWhiteSpace ( document.updateTime ) == true )
            return (new ProgressBackupResponse ( ProgressBackupResult.InvalidData ,
                "서버 문서의 교체 조건을 확인할 수 없습니다." ), null);

        //내용이 손상된 문서도 유효한 로컬 백업으로 교체할 수 있도록 조회 결과와 구분
        ProgressBackupResult payloadResult = ReadPayload (
            document , out int formatVersion , out int saveVersion , out string saveJson );
        CloudProgressBackup backup = new CloudProgressBackup (
            formatVersion , saveVersion , saveJson , document.updateTime , payloadResult );
        return (new ProgressBackupResponse (
            ProgressBackupResult.Success , updatedAt: backup.UpdatedAt ), backup);
    }

    /// <summary>
    /// 확인한 문서를 조건으로 백업 전체 교체
    /// </summary>
    /// <param name="userId">인증한 UID</param>
    /// <param name="idToken">유효한 ID 토큰</param>
    /// <param name="saveJson">검사한 저장 JSON</param>
    /// <param name="saveVersion">저장 버전</param>
    /// <param name="expectedUpdatedAt">확인한 문서 시각, 최초는 null</param>
    /// <returns>서버 저장 결과</returns>
    public async Task<ProgressBackupResponse> UploadBackupAsync (
        string userId , string idToken , string saveJson ,
        int saveVersion , string expectedUpdatedAt )
    {
        //최초는 문서 없음, 덮어쓰기는 조회 시각이 같은 경우에만 저장
        string condition = expectedUpdatedAt == null
            ? "?currentDocument.exists=false"
            : "?currentDocument.updateTime="
                + UnityWebRequest.EscapeURL ( expectedUpdatedAt );

        //로컬 JSON을 Firestore REST 필드 구조로 감싸서 전송
        WriteDocument document = new WriteDocument
        {
            fields = new DocumentFields
            {
                backupFormatVersion = new IntegerField
                {
                    integerValue =
                    CloudProgressBackup.CurrentFormat.ToString ( CultureInfo.InvariantCulture )
                } ,
                saveVersion = new IntegerField
                {
                    integerValue = saveVersion.ToString ( CultureInfo.InvariantCulture )
                } ,
                saveJson = new StringField { stringValue = saveJson }
            }
        };

        //기존 문서를 삭제하지 않고 조건부 쓰기로 교체
        FirebaseHttpResponse response = await _http.SendAsync (
            GetDocumentUrl ( userId ) + condition , "PATCH" ,
            JsonUtility.ToJson ( document ) , "application/json" , idToken );

        if ( response.Canceled == true )
            return new ProgressBackupResponse ( ProgressBackupResult.Canceled );

        //서버 반영 후 응답만 유실됐을 수 있어 저장 실패로 단정하지 않음
        if ( response.NetworkFailed == true || response.StatusCode >= 500 )
            return new ProgressBackupResponse ( ProgressBackupResult.UploadUnconfirmed ,
                $"백업 저장 여부 재확인 필요: HTTP {response.StatusCode}" );

        ProgressBackupResponse status = CheckHttpResponse ( response , "백업 저장" );
        if ( status.Result != ProgressBackupResult.Success )
            return status;

        //성공 응답에서도 갱신 시각을 확인해야 서버 저장 완료로 반환
        if ( BackupJson.TryRead (
            response.Body , out ReadDocument saved ) == false
            || string.IsNullOrWhiteSpace ( saved.updateTime ) == true )
            return new ProgressBackupResponse ( ProgressBackupResult.UploadUnconfirmed ,
                "서버 저장 응답의 갱신 시각을 확인할 수 없습니다." );

        return new ProgressBackupResponse (
            ProgressBackupResult.Success , updatedAt: saved.updateTime );
    }

    /// <summary>
    /// 프로젝트와 인증 사용자의 문서 주소 구성
    /// </summary>
    /// <param name="userId">인증한 UID</param>
    /// <returns>Firestore 문서 주소</returns>
    string GetDocumentUrl ( string userId )
    {
        return string.Format (
            CultureInfo.InvariantCulture , DocumentUrlFormat ,
            UnityWebRequest.EscapeURL ( _projectId ) ,
            UnityWebRequest.EscapeURL ( userId ) , _slotId );
    }

    /// <summary>
    /// 문서 내부 백업 형식과 크기 검사
    /// </summary>
    /// <param name="document">조회한 문서</param>
    /// <param name="formatVersion">읽은 백업 형식 버전</param>
    /// <param name="saveVersion">읽은 저장 데이터 버전</param>
    /// <param name="saveJson">읽은 저장 JSON</param>
    /// <returns>내부 데이터 검사 결과</returns>
    ProgressBackupResult ReadPayload (
        ReadDocument document , out int formatVersion ,
        out int saveVersion , out string saveJson )
    {
        formatVersion = 0;
        saveVersion = 0;
        saveJson = null;

        if ( document.fields == null
            || document.fields.backupFormatVersion == null
            || document.fields.saveVersion == null
            || document.fields.saveJson == null
            || string.IsNullOrWhiteSpace ( document.fields.saveJson.stringValue ) == true )
            return ProgressBackupResult.InvalidData;

        //Firestore REST의 integerValue는 문자열이므로 저장 버전으로 변환
        if ( int.TryParse (
            document.fields.backupFormatVersion.integerValue , NumberStyles.None ,
            CultureInfo.InvariantCulture , out formatVersion ) == false
            || int.TryParse ( document.fields.saveVersion.integerValue , NumberStyles.None ,
                CultureInfo.InvariantCulture , out saveVersion ) == false )
            return ProgressBackupResult.InvalidData;

        saveJson = document.fields.saveJson.stringValue;

        //여기서는 백업 포장만 검사하고 SaveData 내용은 로컬 서비스에서 검사
        if ( formatVersion != CloudProgressBackup.CurrentFormat )
            return ProgressBackupResult.UnsupportedVersion;

        if ( Encoding.UTF8.GetByteCount ( saveJson )
            > CloudProgressBackup.MaxJsonBytes )
            return ProgressBackupResult.TooLarge;

        return ProgressBackupResult.Success;
    }

    /// <summary>
    /// HTTP 상태와 서버 오류 상태를 요청 결과로 변환
    /// </summary>
    /// <param name="response">HTTP 응답</param>
    /// <param name="operation">요청 작업</param>
    /// <returns>요청 결과</returns>
    ProgressBackupResponse CheckHttpResponse (
        FirebaseHttpResponse response , string operation )
    {
        if ( response.Canceled == true )
            return new ProgressBackupResponse ( ProgressBackupResult.Canceled );

        if ( response.NetworkFailed == true )
            return new ProgressBackupResponse ( ProgressBackupResult.NetworkFailed , response.Error );

        if ( response.StatusCode >= 200 && response.StatusCode < 300 )
            return new ProgressBackupResponse ( ProgressBackupResult.Success );

        //서버 오류 상태를 우선 사용하고 없으면 HTTP 코드로 구분
        string serverStatus = ReadErrorStatus ( response.Body );
        ProgressBackupResult result = serverStatus switch
        {
            "UNAUTHENTICATED" => ProgressBackupResult.AuthenticationFailed,
            "PERMISSION_DENIED" => ProgressBackupResult.PermissionDenied,
            "FAILED_PRECONDITION" or "ALREADY_EXISTS" or "ABORTED" =>
                ProgressBackupResult.PreconditionFailed,
            _ => response.StatusCode switch
            {
                401 => ProgressBackupResult.AuthenticationFailed,
                403 => ProgressBackupResult.PermissionDenied,
                409 or 412 => ProgressBackupResult.PreconditionFailed,
                _ => ProgressBackupResult.RemoteFailed
            }
        };

        return new ProgressBackupResponse ( result ,
            $"Firestore {operation} 실패: HTTP {response.StatusCode}, 상태={serverStatus}" );
    }

    /// <summary>
    /// 오류 응답에서 서버 상태 이름만 조회
    /// </summary>
    /// <param name="json">실패 응답 JSON</param>
    /// <returns>상태 이름, 해석할 수 없으면 null</returns>
    string ReadErrorStatus ( string json )
    {
        if ( BackupJson.TryRead ( json , out ErrorResponse response ) == false )
            return null;
        return response.error?.status;
    }

    /// <summary>
    /// 진행 중 원격 요청 중단
    /// </summary>
    public void Dispose ( )
    {
        _http.Dispose ( );
    }

    /// <summary>
    /// Firestore 쓰기 문서 - 백업 저장 필드 전달
    /// </summary>
    [Serializable]
    class WriteDocument
    {
        public DocumentFields fields; //전송할 필드
    }

    /// <summary>
    /// Firestore 읽기 문서 - 백업 필드와 서버 시각 수신
    /// </summary>
    [Serializable]
    class ReadDocument
    {
        public DocumentFields fields;       //다운로드한 필드
        public string updateTime;     //Firestore가 반환한 갱신 시각, 확인 후 조건부 쓰기에 재사용
    }

    /// <summary>
    /// Firestore 백업 필드 - 백업 형식과 기존 저장 JSON 보관
    /// </summary>
    [Serializable]
    class DocumentFields
    {
        public IntegerField backupFormatVersion;        //백업 형식
        public IntegerField saveVersion;         //저장 버전
        public StringField saveJson;             //저장 JSON
    }

    /// <summary>
    /// Firestore 정수 값 - REST 규격의 정수 표현
    /// </summary>
    [Serializable]
    class IntegerField
    {
        public string integerValue;     //Firestore REST가 문자열로 주고받는 정수 값
    }

    /// <summary>
    /// Firestore 문자열 값 - REST 규격의 문자열 표현
    /// </summary>
    [Serializable]
    class StringField
    {
        public string stringValue;      //Firestore 문자열 표현
    }

    /// <summary>
    /// Firestore 오류 응답 - 실패 상태 수신
    /// </summary>
    [Serializable]
    class ErrorResponse
    {
        public ErrorStatus error;       //서버 오류 정보
    }

    /// <summary>
    /// Firestore 오류 상태 - 요청 실패 분류
    /// </summary>
    [Serializable]
    class ErrorStatus
    {
        public string status;       //서버 오류 상태 이름
    }
}
