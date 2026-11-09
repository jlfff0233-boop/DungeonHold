using System;
using UnityEngine;

/// <summary>
/// 무기 강화 데이터 - 공통 대상 설정
/// </summary>
public abstract class WeaponUpgradeData : UpgradeData
{
    [SerializeField] WeaponType _weaponType;    //강화 대상 무기

    /// <summary>
    /// 강화 대상 무기
    /// </summary>
    public WeaponType WeaponType => _weaponType;

    /// <inheritdoc />
    protected override bool ValidateTarget ()
    {
        return Enum.IsDefined( typeof( WeaponType ), _weaponType );
    }
}
