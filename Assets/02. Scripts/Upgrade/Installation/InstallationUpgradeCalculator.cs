/// <summary>
/// 설치물 강화 계산기 - 지정 설치물의 현재 단계 효과 계산
/// </summary>
public static class InstallationUpgradeCalculator
{
    /// <summary>
    /// 지정 설치물의 현재 강화 결과 계산
    /// </summary>
    /// <param name="map">강화 설정</param>
    /// <param name="progress">강화 진행도</param>
    /// <param name="installation">대상 설치물</param>
    /// <returns>현재 단계의 설치물 강화 결과</returns>
    public static InstallationUpgradeStats Calculate (
        UpgradeDataMap map,
        UpgradeProgress progress,
        TrapData installation )
    {
        InstallationUpgradeStats stats = default;

        foreach ( InstallationUpgradeData data in map.GetAll<InstallationUpgradeData>( ) )
        {
            if ( data.Installation != installation )
                continue;

            int level = progress.GetLevel( data.UpgradeId );
            data.ApplyToStats( level, ref stats );
        }

        return stats;
    }
}
