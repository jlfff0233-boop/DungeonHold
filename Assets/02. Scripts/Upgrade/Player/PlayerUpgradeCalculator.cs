/// <summary>
/// 플레이어 강화 계산기 - 현재 진행도의 공통 증가량 계산
/// </summary>
public static class PlayerUpgradeCalculator
{
    /// <summary>
    /// 현재 진행도의 플레이어 공통 증가량 계산
    /// </summary>
    /// <param name="map">검사를 마친 강화 설정</param>
    /// <param name="progress">현재 슬롯 진행도</param>
    /// <param name="maxHpBonus">최대 HP 추가량</param>
    /// <param name="moveSpeedBonus">이동속도 증가율</param>
    public static void Calculate (
        UpgradeDataMap map, UpgradeProgress progress,
        out float maxHpBonus, out float moveSpeedBonus )
    {
        maxHpBonus = 0f;
        moveSpeedBonus = 0f;

        foreach ( PlayerHpUpgradeData data in map.GetAll<PlayerHpUpgradeData>( ) )
        {
            maxHpBonus = data.GetMaxHpBonus( progress.GetLevel( data.UpgradeId ) );
        }

        foreach ( PlayerMoveSpeedUpgradeData data in map.GetAll<PlayerMoveSpeedUpgradeData>( ) )
        {
            moveSpeedBonus = data.GetMoveSpeedBonus( progress.GetLevel( data.UpgradeId ) );
        }
    }
}
