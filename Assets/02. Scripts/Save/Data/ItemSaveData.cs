using System;
using UnityEngine;

/// <summary>
/// 아이템 저장 데이터 - 식별자와 보유 수량
/// </summary>
[Serializable]
public class ItemSaveData
{
    [SerializeField] string _itemId; //아이템 고정 ID
    [SerializeField] int _count;     //보유 수량

    /// <summary>
    /// 아이템 고정 ID
    /// </summary>
    public string ItemId => _itemId;

    /// <summary>
    /// 보유 수량
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// 저장할 아이템 기록 생성
    /// </summary>
    /// <param name="itemId">아이템 ID</param>
    /// <param name="count">보유 수량</param>
    public ItemSaveData ( string itemId, int count )
    {
        _itemId = itemId;
        _count = count;
    }
}

