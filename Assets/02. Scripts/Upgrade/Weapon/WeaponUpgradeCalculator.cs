/// <summary>
/// 무기 강화 계산기 - 선택한 무기의 현재 증가량 계산
/// </summary>
public static class WeaponUpgradeCalculator
{
    /// <summary>
    /// 현재 진행도의 무기 증가량 계산
    /// </summary>
    /// <param name="map">검사를 마친 강화 설정</param>
    /// <param name="progress">현재 슬롯 진행도</param>
    /// <param name="type">대상 무기 타입</param>
    /// <param name="damageBonus">직접 피해 증가율</param>
    /// <param name="attackSpeedBonus">기본 공격속도 증가율</param>
    public static void Calculate (
        UpgradeDataMap map, UpgradeProgress progress, WeaponType type,
        out float damageBonus, out float attackSpeedBonus )
    {
        damageBonus = 0f;
        attackSpeedBonus = 0f;

        foreach ( WeaponDamageUpgradeData data in map.GetAll<WeaponDamageUpgradeData>( ) )
        {
            if ( data.WeaponType != type )
                continue;

            damageBonus = data.GetDamageBonus( progress.GetLevel( data.UpgradeId ) );
        }

        foreach ( WeaponAttackSpeedUpgradeData data in map.GetAll<WeaponAttackSpeedUpgradeData>( ) )
        {
            if ( data.WeaponType != type )
                continue;

            attackSpeedBonus = data.GetAttackSpeedBonus( progress.GetLevel( data.UpgradeId ) );
        }
    }
}
