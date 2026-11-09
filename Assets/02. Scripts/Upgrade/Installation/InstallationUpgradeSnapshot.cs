using System.Collections.Generic;

/// <summary>
/// 설치물 강화 스냅샷 - 입장 시 확정한 설치물별 결과 보관
/// </summary>
public class InstallationUpgradeSnapshot
{
    Dictionary<TrapData, InstallationUpgradeStats> _stats = new( );    //설치물별 확정 결과

    /// <summary>
    /// 현재 진행도로 설치물별 결과 확정
    /// </summary>
    /// <param name="map">강화 설정</param>
    /// <param name="progress">현재 슬롯 진행도</param>
    public InstallationUpgradeSnapshot (
        UpgradeDataMap map,
        UpgradeProgress progress )
    {
        HashSet<TrapData> targets = new HashSet<TrapData>( );

        //강화 항목이 여러 개여도 설치물별 결과는 한 번만 생성
        foreach ( InstallationUpgradeData data in map.GetAll<InstallationUpgradeData>( ) )
        {
            targets.Add( data.Installation );
        }

        foreach ( TrapData target in targets )
        {
            _stats.Add( target,
                InstallationUpgradeCalculator.Calculate( map, progress, target ) );
        }
    }

    /// <summary>
    /// 해당 설치물의 입장 시점 강화 결과 조회
    /// </summary>
    /// <param name="data">설치물 설정</param>
    /// <returns>등록한 강화가 없으면 증가량 0</returns>
    public InstallationUpgradeStats GetStats ( TrapData data )
    {
        _stats.TryGetValue( data, out InstallationUpgradeStats stats );
        return stats;
    }
}
