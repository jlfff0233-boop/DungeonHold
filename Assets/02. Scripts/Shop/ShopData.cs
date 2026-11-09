using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 데이터 - 판매 상품 목록과 설정 검사
/// </summary>
[CreateAssetMenu( fileName = "ShopData", menuName = "Shop/ShopData" )]
public class ShopData : ScriptableObject
{
    [SerializeField] ShopProduct [ ] _products = new ShopProduct [ 0 ];    //소모품 상품 목록
    [SerializeField] WeaponShopProduct [ ] _weaponProducts = new WeaponShopProduct [ 0 ];    //무기 상품
    [SerializeField] InstallationShopProduct [ ] _installationProducts = new InstallationShopProduct [ 0 ];    //설치물 상품

    /// <summary>
    /// 상품 ID별 조회표
    /// </summary>
    Dictionary<string , IShopProduct> _productMap = new Dictionary<string , IShopProduct> ( );
    /// <summary>
    /// 소모품, 무기와 설치물의 통합 판매 목록
    /// </summary>
    List<IShopProduct> _allProducts = new List<IShopProduct> ( );

    /// <summary>
    /// 판매 상품 목록
    /// </summary>
    public IReadOnlyList<IShopProduct> Products => _allProducts;

    /// <summary>
    /// 상품 설정 검사와 맵 구성
    /// </summary>
    /// <param name="itemDataMap">아이템 설정</param>
    /// <param name="weaponDataMap">무기 설정</param>
    /// <returns>판매 설정 사용 가능 여부</returns>
    public bool Validate ( ItemDataMap itemDataMap , WeaponDataMap weaponDataMap )
    {
        _productMap.Clear ( );
        _allProducts.Clear ( );

        Dictionary<string , IShopProduct> products = new Dictionary<string , IShopProduct> ( );
        List<IShopProduct> ordered = new List<IShopProduct> ( );

        foreach ( ShopProduct product in _products )
        {
            if ( product == null || product.Item == null )
            {
                Debug.LogError( "상점 목록의 상품 또는 아이템 설정이 비어 있습니다.", this );
                return false;
            }

            ItemData item = product.Item;

            //구매와 사용에서 같은 아이템 설정을 사용하도록 초기화에서 확인
            if ( itemDataMap.TryGet( item.ItemId, out ItemData registeredItem ) == false
                || registeredItem != item )
            {
                Debug.LogError( $"상점 아이템을 ItemDataMap의 동일한 에셋으로 연결하세요: {item.ItemId}", this );
                return false;
            }

            if ( AddProduct ( product , products , ordered ) == false )
                return false;
        }

        if ( _weaponProducts.Length == 0 )
        {
            Debug.LogError ( "무기 상품이 없습니다." , this );
            return false;
        }

        foreach ( WeaponShopProduct product in _weaponProducts )
        {
            if ( product == null
                || weaponDataMap.TryGet ( product.WeaponType , out _ ) == false )
            {
                Debug.LogError ( "무기 상품 설정을 확인하세요." , this );
                return false;
            }

            if ( AddProduct ( product , products , ordered ) == false )
                return false;
        }

        foreach ( InstallationShopProduct product in _installationProducts )
        {
            if ( product == null || product.Installation == null )
            {
                Debug.LogError ( "설치물 상품 설정이 비어 있습니다." , this );
                return false;
            }

            if ( string.IsNullOrWhiteSpace ( product.Installation.TrapId ) == true )
            {
                Debug.LogError (
                    $"설치물 상품의 ID가 없습니다: {product.Installation.name}" , this );
                return false;
            }

            if ( AddProduct ( product , products , ordered ) == false )
                return false;
        }

        _productMap = products;
        _allProducts = ordered;
        return true;
    }

    /// <summary>
    /// 공통 상품 등록
    /// </summary>
    /// <param name="product">상품</param>
    /// <param name="products">상품 맵</param>
    /// <param name="ordered">상품 목록</param>
    /// <returns>등록 성공 여부</returns>
    bool AddProduct (
        IShopProduct product ,
        Dictionary<string , IShopProduct> products ,
        List<IShopProduct> ordered )
    {
        if ( product.Price < 0 )
        {
            Debug.LogError (
                $"상품 가격을 확인하세요: {product.ProductId}, {product.Price}" , this );
            return false;
        }

        if ( products.TryAdd ( product.ProductId , product ) == false )
        {
            Debug.LogError ( $"상품 ID가 중복됐습니다: {product.ProductId}" , this );
            return false;
        }

        ordered.Add ( product );
        return true;
    }

    /// <summary>
    /// 상품 조회
    /// </summary>
    /// <param name="productId">상품 ID</param>
    /// <param name="product">상품</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetProduct ( string productId , out IShopProduct product )
    {
        product = null;

        if ( productId == null )
            return false;

        return _productMap.TryGetValue ( productId , out product );
    }
}
