using System;
using System.Collections.Generic;

/// <summary>
/// 장비 해금 - 보유/선택과 구매 저장 처리
/// </summary>
public class EquipmentUnlock
{
    ShopData _shopData;     //상점 데이터
    Currency _currency;     //재화
    SaveService _saveService;       //저장 서비스
    /// <summary>
    /// 해금된 무기 목록
    /// </summary>
    HashSet<WeaponType> _unlockedWeapons;
    /// <summary>
    /// 구매한 설치물 아이디 목록
    /// </summary>
    HashSet<string> _purchasedInstallationIds;
    /// <summary>
    /// 선택한 무기
    /// </summary>
    WeaponType? _selectedWeapon;

    /// <summary>
    /// 선택 무기
    /// </summary>
    public WeaponType? SelectedWeapon => _selectedWeapon;

    /// <summary>
    /// 해금/선택 변경 이벤트
    /// </summary>
    public event Action OnChanged;

    /// <summary>
    /// 저장 기록 복원과 구매 연결
    /// </summary>
    /// <param name="shopData">상품 설정</param>
    /// <param name="currency">재화</param>
    /// <param name="saveService">저장 서비스</param>
    /// <param name="saveData">저장 기록</param>
    public EquipmentUnlock (
        ShopData shopData , Currency currency ,
        SaveService saveService , SaveData saveData )
    {
        _shopData = shopData;
        _currency = currency;
        _saveService = saveService;
        _unlockedWeapons = new HashSet<WeaponType> ( saveData.UnlockedWeapons );
        _purchasedInstallationIds = new HashSet<string> ( saveData.PurchasedInstallationIds );
        _selectedWeapon = saveData.HasSelectedWeapon
            ? saveData.SelectedWeapon : ( WeaponType? ) null;
    }

    /// <summary>
    /// 무기 해금 여부 조회
    /// </summary>
    /// <param name="weaponType">무기 타입</param>
    /// <returns>해금 여부</returns>
    public bool IsWeaponUnlocked ( WeaponType weaponType )
    {
        return _unlockedWeapons.Contains ( weaponType );
    }

    /// <summary>
    /// 설치물 해금 여부 조회
    /// </summary>
    /// <param name="installationId">설치물 ID</param>
    /// <returns>해금 여부</returns>
    public bool IsInstallationUnlocked ( string installationId )
    {
        if ( _shopData.TryGetProduct (
            InstallationShopProduct.GetProductId ( installationId ) ,
            out IShopProduct product ) == false
            || product is not InstallationShopProduct installation )
            return false;

        return installation.InitiallyUnlocked
            || _purchasedInstallationIds.Contains ( installationId );
    }

    /// <summary>
    /// 무기 구매 조건 조회
    /// </summary>
    /// <param name="product">상품</param>
    /// <param name="price">구매 가격</param>
    /// <returns>구매 조건</returns>
    internal ShopPurchaseResult GetWeaponPurchaseState (
        WeaponShopProduct product , out int price )
    {
        price = _unlockedWeapons.Count == 0 ? 0 : product.Price;

        if ( _unlockedWeapons.Contains ( product.WeaponType ) == true )
            return ShopPurchaseResult.AlreadyOwned;

        return _currency.CanSpend ( CurrencyType.Permanent , price )
            ? ShopPurchaseResult.Success : ShopPurchaseResult.CannotSpend;
    }

    /// <summary>
    /// 설치물 구매 조건 조회
    /// </summary>
    /// <param name="product">상품</param>
    /// <param name="price">구매 가격</param>
    /// <returns>구매 조건</returns>
    internal ShopPurchaseResult GetInstallationPurchaseState (
        InstallationShopProduct product , out int price )
    {
        price = product.Price;

        if ( product.InitiallyUnlocked
            || _purchasedInstallationIds.Contains ( product.Installation.TrapId ) )
            return ShopPurchaseResult.AlreadyOwned;

        return _currency.CanSpend ( CurrencyType.Permanent , price )
            ? ShopPurchaseResult.Success : ShopPurchaseResult.CannotSpend;
    }

    /// <summary>
    /// 무기 구매 해금
    /// </summary>
    /// <param name="product">상품</param>
    /// <returns>구매 결과</returns>
    internal ShopPurchaseResult TryPurchaseWeapon ( WeaponShopProduct product )
    {
        ShopPurchaseResult result = GetWeaponPurchaseState ( product , out int price );
        if ( result != ShopPurchaseResult.Success )
            return result;

        bool selectWeapon = _unlockedWeapons.Count == 0;
        int nextBalance = _currency.GetBalance ( CurrencyType.Permanent ) - price;

        if ( _saveService.TrySaveWeaponUnlock (
            nextBalance , product.WeaponType , selectWeapon ) == false )
            return ShopPurchaseResult.SaveFailed;

        _unlockedWeapons.Add ( product.WeaponType );
        if ( selectWeapon == true )
            _selectedWeapon = product.WeaponType;

        _currency.ApplySavedPermanentBalance ( nextBalance );
        OnChanged?.Invoke ( );
        return ShopPurchaseResult.Success;
    }

    /// <summary>
    /// 설치물 구매 해금
    /// </summary>
    /// <param name="product">상품</param>
    /// <returns>구매 결과</returns>
    internal ShopPurchaseResult TryPurchaseInstallation ( InstallationShopProduct product )
    {
        ShopPurchaseResult result = GetInstallationPurchaseState ( product , out int price );
        if ( result != ShopPurchaseResult.Success )
            return result;

        string installationId = product.Installation.TrapId;
        int nextBalance = _currency.GetBalance ( CurrencyType.Permanent ) - price;

        if ( _saveService.TrySaveInstallationUnlock ( nextBalance , installationId ) == false )
            return ShopPurchaseResult.SaveFailed;

        _purchasedInstallationIds.Add ( installationId );
        _currency.ApplySavedPermanentBalance ( nextBalance );
        OnChanged?.Invoke ( );
        return ShopPurchaseResult.Success;
    }

    /// <summary>
    /// 보유 무기 선택
    /// </summary>
    /// <param name="weaponType">무기 타입</param>
    /// <returns>선택 결과</returns>
    public ShopPurchaseResult TrySelectWeapon ( WeaponType weaponType )
    {
        if ( _shopData.TryGetProduct (
            WeaponShopProduct.GetProductId ( weaponType ) , out IShopProduct product ) == false
            || product is not WeaponShopProduct )
            return ShopPurchaseResult.UnknownProduct;

        if ( _unlockedWeapons.Contains ( weaponType ) == false )
            return ShopPurchaseResult.NotOwned;

        if ( _selectedWeapon == weaponType )
            return ShopPurchaseResult.Success;

        if ( _saveService.TrySaveSelectedWeapon ( weaponType ) == false )
            return ShopPurchaseResult.SaveFailed;

        _selectedWeapon = weaponType;
        OnChanged?.Invoke ( );
        return ShopPurchaseResult.Success;
    }
}
