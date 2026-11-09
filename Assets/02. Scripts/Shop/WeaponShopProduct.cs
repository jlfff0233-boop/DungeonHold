using System;
using UnityEngine;

/// <summary>
/// 무기 상품
/// </summary>
[Serializable]
public class WeaponShopProduct : IShopProduct
{
    [SerializeField] WeaponType _weaponType;    //무기 타입
    [SerializeField] int _price;    //해금 가격

    /// <summary>
    /// 상품 ID
    /// </summary>
    public string ProductId => GetProductId ( _weaponType );

    /// <summary>
    /// 무기 타입
    /// </summary>
    public WeaponType WeaponType => _weaponType;

    /// <summary>
    /// 기본 가격
    /// </summary>
    public int Price => _price;

    /// <summary>
    /// 무기 상품 ID 조회
    /// </summary>
    /// <param name="weaponType">무기 타입</param>
    /// <returns>상품 ID</returns>
    public static string GetProductId ( WeaponType weaponType )
    {
        return $"weapon:{( int ) weaponType}";
    }
}