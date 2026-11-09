using System.Collections.Generic;

/// <summary>
/// 동료 강화 스냅샷 - 입장 시 확정한 동료별 결과 보관
/// </summary>
public class CompanionUpgradeSnapshot
{
    Dictionary<CompanionData, CompanionUpgradeStats> _stats = new( );    //동료별 확정 결과

    /// <summary>
    /// 기존 미강화 호출에 사용할 빈 결과 생성
    /// </summary>
    public CompanionUpgradeSnapshot ()
    {
    }

    /// <summary>
    /// 현재 진행도로 동료별 결과 확정
    /// </summary>
    /// <param name="map">검사를 마친 강화 설정</param>
    /// <param name="progress">현재 슬롯 진행도</param>
    public CompanionUpgradeSnapshot ( UpgradeDataMap map, UpgradeProgress progress )
    {
        foreach ( CompanionUpgradeData data in map.GetAll<CompanionUpgradeData>( ) )
        {
            int level = progress.GetLevel( data.UpgradeId );
            _stats.Add( data.Companion, data.GetStats( level ) );
        }
    }

    /// <summary>
    /// 해당 동료의 입장 시점 강화 결과 조회
    /// </summary>
    /// <param name="data">동료 설정</param>
    /// <returns>등록한 강화가 없으면 증가량 0</returns>
    public CompanionUpgradeStats GetStats ( CompanionData data )
    {
        _stats.TryGetValue( data, out CompanionUpgradeStats stats );
        return stats;
    }
}
