using UnityEngine;

/// <summary>
/// 동료 슬롯 데이터 - 타입별 고용과 재고용 요청
/// </summary>
[CreateAssetMenu (
    fileName = "CompanionQuickSlotData" , menuName = "QuickSlot/Companion" )]
public class CompanionQuickSlotData : QuickSlotData
{
    [SerializeField] WeaponType _weaponType;    //고용할 동료 타입

    /// <summary>
    /// 동료 무기 타입
    /// </summary>
    public WeaponType WeaponType => _weaponType;

    /// <inheritdoc />
    public override bool Validate (
        ItemDataMap items , CompanionHireDataMap companions )
    {
        return companions.TryGet ( _weaponType , out _ );
    }

    /// <inheritdoc />
    public override bool TryExecute ( QuickSlotContext context )
    {
        return context.TryHireCompanion ( _weaponType );
    }
}
