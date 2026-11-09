using UnityEngine;

/// <summary>
/// 아이템 슬롯 데이터 - 기존 소모품 사용 요청
/// </summary>
[CreateAssetMenu ( fileName = "ItemQuickSlotData" , menuName = "QuickSlot/Item" )]
public class ItemQuickSlotData : QuickSlotData
{
    [SerializeField] ItemData _item;    //사용할 아이템 설정

    /// <summary>
    /// 아이템 설정
    /// </summary>
    public ItemData Item => _item;

    /// <inheritdoc />
    public override bool Validate (
        ItemDataMap items , CompanionHireDataMap companions )
    {
        if ( _item == null )
            return false;

        return items.TryGet ( _item.ItemId , out ItemData registered )
            && registered == _item;
    }

    /// <inheritdoc />
    public override bool TryExecute ( QuickSlotContext context )
    {
        return context.TryUseItem ( _item.ItemId );
    }
}
