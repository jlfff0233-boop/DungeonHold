using System;
using UnityEngine;

/// <summary>
/// 소모품 상품 - 판매 아이템과 한 개 구매 가격 설정
/// </summary>
[Serializable]
public class ShopProduct : IShopProduct
{
    [SerializeField] ItemData _item;    //판매할 아이템 설정
    [SerializeField] int _price;    //영구 재화 구매 가격

    /// <summary>
    /// 판매 아이템 설정
    /// </summary>
    public ItemData Item => _item;

    /// <summary>
    /// 상품 ID
    /// </summary>
    public string ProductId => _item.ItemId;

    /// <summary>
    /// 한 개 구매 가격
    /// </summary>
    public int Price => _price;
}