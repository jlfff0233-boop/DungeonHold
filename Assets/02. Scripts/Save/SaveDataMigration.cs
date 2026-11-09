/// <summary>
/// 구형 저장 구조 변환
/// </summary>
public static class SaveDataMigration
{
    //실제로 클리어 목록이 없던 과거 형식
    public const int LegacyWithoutStageRecords = 6;

    /// <summary>
    /// 지원 형식 확인
    /// </summary>
    /// <param name="version">저장 버전</param>
    /// <returns>지원 여부</returns>
    public static bool IsSupported ( int version )
    {
        return version >= LegacyWithoutStageRecords
            && version <= SaveData.CurrentVersion;
    }

    /// <summary>
    /// 기존 진행을 현재 형식으로 변환
    /// </summary>
    /// <param name="data">파일에서 읽은 미확정 데이터</param>
    /// <param name="migrated">변환 여부</param>
    /// <param name="error">지원하지 않는 형식 설명</param>
    /// <returns>변환 결과</returns>
    public static SaveResult TryMigrate (
        SaveData data,
        out bool migrated,
        out string error )
    {
        migrated = false;
        error = null;

        int sourceVersion = data.Version;
        if ( IsSupported( sourceVersion ) == false )
        {
            error = $"지원하지 않는 저장 형식입니다: {sourceVersion}";
            return SaveResult.UnsupportedVersion;
        }

        //실제 구형 구조만 보완하고, 추가 점수 필드는 읽기 기본값 사용
        if ( sourceVersion == LegacyWithoutStageRecords )
            data.PrepareLegacyStageRecords( );

        if ( sourceVersion == SaveData.CurrentVersion )
            return SaveResult.Success;

        if ( sourceVersion < 9 )
            data.PrepareLegacyStarRewards ( );

        data.SetCurrentVersion( );
        migrated = true;

        return SaveResult.Success;
    }
}
