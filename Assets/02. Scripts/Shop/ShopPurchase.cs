/// <summary>
/// 상품 구매 결과
/// </summary>
public enum ShopPurchaseResult
{
    Success,        //구매 성공 또는 구매 조건 충족
    Unavailable,        //현재 씬에서 구매 불가
    UnknownProduct,     //판매하지 않는 상품
    CannotSpend,        //영구 재화 사용 불가 또는 잔액 부족
    LimitReached,       //보유 상한 도달
    SaveFailed,      //구매 저장 실패
    AlreadyOwned,       //이미 해금한 상품
    NotOwned        //선택할 무기 미보유
}

/// <summary>
/// 상점 구매 - 상품별 구매 요청과 저장 흐름 조율
/// </summary>
public class ShopPurchase
{
    ShopData _shopData;     //판매 설정
    Currency _currency;     //현재 슬롯의 재화
    ItemInventory _inventory;       //현재 슬롯의 인벤토리
    SaveService _saveService;       //현재 슬롯의 저장 서비스
    EquipmentUnlock _equipmentUnlock;    //해금/선택 처리

    /// <summary>
    /// 현재 슬롯의 구매 관련 모듈 연결
    /// </summary>
    /// <param name="shopData">판매 설정</param>
    /// <param name="currency">재화 모듈</param>
    /// <param name="inventory">아이템 인벤토리</param>
    /// <param name="saveService">현재 슬롯 저장 서비스</param>
    /// <param name="equipmentUnlock">해금/선택 처리</param>
    public ShopPurchase (
        ShopData shopData ,
        Currency currency ,
        ItemInventory inventory ,
        SaveService saveService ,
        EquipmentUnlock equipmentUnlock )
    {
        _shopData = shopData;
        _currency = currency;
        _inventory = inventory;
        _saveService = saveService;
        _equipmentUnlock = equipmentUnlock;
    }

    /// <summary>
    /// 소모품 구매 조건 조회
    /// </summary>
    /// <param name="itemId">구매할 아이템 식별자</param>
    /// <returns>구매 조건 확인 결과</returns>
    public ShopPurchaseResult GetPurchaseState ( string itemId )
    {
        if ( _shopData.TryGetProduct ( itemId , out IShopProduct product ) == false
            || product is not ShopProduct item )
            return ShopPurchaseResult.UnknownProduct;

        return PrepareItemPurchase ( item , out _ , out _ );
    }

    /// <summary>
    /// 소모품 한 개 구매
    /// </summary>
    /// <param name="itemId">구매할 아이템 식별자</param>
    /// <returns>구매 처리 결과</returns>
    public ShopPurchaseResult TryPurchase ( string itemId )
    {
        if ( _shopData.TryGetProduct ( itemId , out IShopProduct product ) == false
            || product is not ShopProduct item )
            return ShopPurchaseResult.UnknownProduct;

        return TryPurchaseItem ( item );
    }

    /// <summary>
    /// 상품 구매 조건 조회
    /// </summary>
    /// <param name="productId">상품 ID</param>
    /// <param name="price">구매 가격</param>
    /// <returns>구매 조건</returns>
    public ShopPurchaseResult GetProductPurchaseState ( string productId , out int price )
    {
        price = 0;

        if ( _shopData.TryGetProduct ( productId , out IShopProduct product ) == false )
            return ShopPurchaseResult.UnknownProduct;

        switch ( product )
        {
            case ShopProduct item:
                price = item.Price;
                return PrepareItemPurchase ( item , out _ , out _ );
            case WeaponShopProduct weapon:
                return _equipmentUnlock.GetWeaponPurchaseState ( weapon , out price );
            case InstallationShopProduct installation:
                return _equipmentUnlock.GetInstallationPurchaseState ( installation , out price );
            default:
                return ShopPurchaseResult.UnknownProduct;
        }
    }

    /// <summary>
    /// 상품 구매
    /// </summary>
    /// <param name="productId">상품 ID</param>
    /// <returns>구매 결과</returns>
    public ShopPurchaseResult TryPurchaseProduct ( string productId )
    {
        if ( _shopData.TryGetProduct ( productId , out IShopProduct product ) == false )
            return ShopPurchaseResult.UnknownProduct;

        switch ( product )
        {
            case ShopProduct item:
                return TryPurchaseItem ( item );
            case WeaponShopProduct weapon:
                return _equipmentUnlock.TryPurchaseWeapon ( weapon );
            case InstallationShopProduct installation:
                return _equipmentUnlock.TryPurchaseInstallation ( installation );
            default:
                return ShopPurchaseResult.UnknownProduct;
        }
    }

    /// <summary>
    /// 소모품 구매
    /// </summary>
    /// <param name="product">상품</param>
    /// <returns>구매 결과</returns>
    ShopPurchaseResult TryPurchaseItem ( ShopProduct product )
    {
        ShopPurchaseResult result = PrepareItemPurchase (
            product , out int nextBalance , out int nextCount );

        if ( result != ShopPurchaseResult.Success )
            return result;

        string itemId = product.Item.ItemId;
        if ( _saveService.TrySaveItemPurchase (
            nextBalance , itemId , nextCount ) == false )
            return ShopPurchaseResult.SaveFailed;

        //재화 알림에서도 구매 후 수량을 조회할 수 있도록 먼저 반영
        _inventory.ApplySavedCount ( itemId , nextCount );
        _currency.ApplySavedPermanentBalance ( nextBalance );

        _inventory.NotifyChanged ( );
        return ShopPurchaseResult.Success;
    }

    /// <summary>
    /// 소모품 구매 조건 확인
    /// </summary>
    /// <param name="product">상품</param>
    /// <param name="nextBalance">구매 후 영구 재화 잔액</param>
    /// <param name="nextCount">구매 후 수량</param>
    /// <returns>구매 조건 확인 결과</returns>
    ShopPurchaseResult PrepareItemPurchase (
        ShopProduct product , out int nextBalance , out int nextCount )
    {
        nextBalance = 0;
        nextCount = 0;

        if ( _currency.CanSpend (
            CurrencyType.Permanent , product.Price ) == false )
            return ShopPurchaseResult.CannotSpend;

        if ( _inventory.TryGetCountAfterAdd ( product.Item , out nextCount ) == false )
            return ShopPurchaseResult.LimitReached;

        nextBalance = _currency.GetBalance ( CurrencyType.Permanent ) - product.Price;
        return ShopPurchaseResult.Success;
    }
}
