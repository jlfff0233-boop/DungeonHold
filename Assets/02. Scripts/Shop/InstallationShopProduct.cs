using System;
using UnityEngine;

/// <summary>
/// 설치물 상품
/// </summary>
[Serializable]
public class InstallationShopProduct : IShopProduct
{
    [SerializeField] TrapData _installation;    //설치물 설정
    [SerializeField] int _price;    //해금 가격
    [SerializeField] bool _initiallyUnlocked;    //기본 해금 여부

    /// <summary>
    /// 상품 ID
    /// </summary>
    public string ProductId => GetProductId ( _installation.TrapId );

    /// <summary>
    /// 설치물 설정
    /// </summary>
    public TrapData Installation => _installation;

    /// <summary>
    /// 기본 가격
    /// </summary>
    public int Price => _price;

    /// <summary>
    /// 기본 해금 여부
    /// </summary>
    public bool InitiallyUnlocked => _initiallyUnlocked;

    /// <summary>
    /// 설치물 상품 ID 조회
    /// </summary>
    /// <param name="installationId">설치물 ID</param>
    /// <returns>상품 ID</returns>
    public static string GetProductId ( string installationId )
    {
        return $"installation:{installationId}";
    }
}