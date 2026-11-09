
/// <summary>
/// 서버 백업 - 문서 정보와 내부 저장 데이터 상태
/// </summary>
public class CloudProgressBackup
{
    public const int CurrentFormat = 1;     //백업 포장 형식
    public const int MaxJsonBytes = 512 * 1024;     //JSON 본문 상한, Firestore 문서 제한에 여유 확보

    /// <summary>
    /// 백업 형식 버전
    /// </summary>
    public int FormatVersion { get; }

    /// <summary>
    /// 백업에 포함된 저장 데이터 버전
    /// </summary>
    public int SaveVersion { get; }

    /// <summary>
    /// 로컬 복원에 사용할 저장 JSON
    /// </summary>
    public string SaveJson { get; }

    /// <summary>
    /// 조건부 덮어쓰기에 사용하는 서버 문서 갱신 시각
    /// </summary>
    public string UpdatedAt { get; }

    /// <summary>
    /// 문서 조회 성공과 구분한 내부 데이터 검사 결과
    /// </summary>
    public ProgressBackupResult PayloadResult { get; }

    /// <summary>
    /// 조회한 백업 정보와 내부 데이터 검사 결과 구성
    /// </summary>
    /// <param name="formatVersion">백업 형식 버전</param>
    /// <param name="saveVersion">저장 데이터 버전</param>
    /// <param name="saveJson">저장 JSON</param>
    /// <param name="updatedAt">서버 문서 갱신 시각</param>
    /// <param name="payloadResult">내부 데이터 검사 결과</param>
    public CloudProgressBackup (
        int formatVersion , int saveVersion , string saveJson ,
        string updatedAt , ProgressBackupResult payloadResult )
    {
        FormatVersion = formatVersion;
        SaveVersion = saveVersion;
        SaveJson = saveJson;
        UpdatedAt = updatedAt;
        PayloadResult = payloadResult;
    }
}
