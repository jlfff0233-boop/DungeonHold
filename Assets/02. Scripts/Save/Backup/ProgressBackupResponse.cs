/// <summary>
/// 백업 응답 - 처리 결과와 서버 백업 시각 전달
/// </summary>
public struct ProgressBackupResponse
{
    /// <summary>
    /// 수동 백업 처리 결과
    /// </summary>
    public ProgressBackupResult Result { get; }
    /// <summary>
    /// 개발자 확인용 오류 내용
    /// </summary>
    public string Detail { get; }
    /// <summary>
    /// 서버 문서 갱신 시각
    /// </summary>
    public string UpdatedAt { get; }

    /// <summary>
    /// 처리 결과 구성
    /// </summary>
    /// <param name="result">처리 결과</param>
    /// <param name="detail">개발자 확인용 오류 내용</param>
    /// <param name="updatedAt">서버 문서 갱신 시각</param>
    public ProgressBackupResponse (
        ProgressBackupResult result ,
        string detail = null ,
        string updatedAt = null )
    {
        Result = result;
        Detail = detail;
        UpdatedAt = updatedAt;
    }

    /// <summary>
    /// 로컬 처리 결과를 백업 응답으로 변환
    /// </summary>
    /// <param name="result">로컬 처리 결과</param>
    /// <param name="error">개발자 확인용 오류 내용</param>
    /// <param name="notFound">호출 목적에 맞는 없음 결과</param>
    /// <returns>백업 응답</returns>
    public static ProgressBackupResponse FromLocal (
        SaveResult result , string error , ProgressBackupResult notFound )
    {
        //파일 없음은 백업할 진행 없음/복구 보관본 없음 등 호출 목적에 맞춰 구분
        ProgressBackupResult converted = result switch
        {
            SaveResult.Success => ProgressBackupResult.Success,
            SaveResult.NotFound => notFound,
            SaveResult.InvalidData => ProgressBackupResult.InvalidData,
            SaveResult.UnsupportedVersion => ProgressBackupResult.UnsupportedVersion,
            SaveResult.Unavailable => ProgressBackupResult.Unavailable,
            _ => ProgressBackupResult.LocalFileFailed
        };

        return new ProgressBackupResponse ( converted , error );
    }
}
