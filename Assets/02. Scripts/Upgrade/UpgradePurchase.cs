/// <summary>
/// 강화 구매 결과
/// </summary>
public enum UpgradePurchaseResult
{
    Success,        //구매 가능 또는 구매 성공
    Unavailable,        //현재 진행 상태에서 요청 불가
    UnknownUpgrade,     //등록되지 않은 강화
    MaxLevel,       //최대 단계
    CannotSpend,        //영구 재화 사용 불가
    SaveFailed      //저장 실패
}

/// <summary>
/// 강화 조회 결과 - 구매 조건과 현재 단계
/// </summary>
public struct UpgradeState
{
    UpgradePurchaseResult _result;      //구매 조건
    int _level;     //현재 단계
    int _price;     //다음 단계 비용

    /// <summary>
    /// 구매 조건
    /// </summary>
    public UpgradePurchaseResult Result => _result;

    /// <summary>
    /// 현재 단계
    /// </summary>
    public int Level => _level;

    /// <summary>
    /// 다음 단계 비용
    /// </summary>
    public int Price => _price;

    /// <summary>
    /// 현재 단계와 다음 구매 정보 생성
    /// </summary>
    /// <param name="result">구매 조건</param>
    /// <param name="level">현재 단계</param>
    /// <param name="price">다음 단계 비용</param>
    public UpgradeState (
        UpgradePurchaseResult result ,
        int level = 0 , int price = 0 )
    {
        _result = result;
        _level = level;
        _price = price;
    }
}

/// <summary>
/// 강화 구매 - 구매 검사와 통합 저장, 상태 반영 조율
/// </summary>
public class UpgradePurchase
{
    UpgradeDataMap _map;        //강화 설정
    UpgradeProgress _progress;      //현재 슬롯 진행도
    Currency _currency;     //현재 슬롯 재화
    SaveService _saveService;       //현재 슬롯 저장 서비스

    /// <summary>
    /// 현재 슬롯의 강화 구매 모듈 연결
    /// </summary>
    /// <param name="map">강화 설정</param>
    /// <param name="progress">강화 진행도</param>
    /// <param name="currency">재화 모듈</param>
    /// <param name="saveService">저장 서비스</param>
    public UpgradePurchase (
        UpgradeDataMap map , UpgradeProgress progress ,
        Currency currency , SaveService saveService )
    {
        _map = map;
        _progress = progress;
        _currency = currency;
        _saveService = saveService;
    }

    /// <summary>
    /// 현재 단계와 다음 구매 조건 조회
    /// </summary>
    /// <param name="upgradeId">강화 ID</param>
    /// <returns>현재 단계와 구매 조건</returns>
    public UpgradeState GetUpgradeState ( string upgradeId )
    {
        if ( _map.TryGet ( upgradeId , out UpgradeData data ) == false )
            return new UpgradeState ( UpgradePurchaseResult.UnknownUpgrade );

        int level = _progress.GetLevel ( upgradeId );

        if ( level == data.MaxLevel )
            return new UpgradeState ( UpgradePurchaseResult.MaxLevel , level );

        int price = data.GetPrice ( level + 1 );
        UpgradePurchaseResult result = _currency.CanSpend ( CurrencyType.Permanent , price )
            ? UpgradePurchaseResult.Success : UpgradePurchaseResult.CannotSpend;
        return new UpgradeState ( result , level , price );
    }

    /// <summary>
    /// 다음 단계의 비용과 진행도를 함께 저장한 뒤 구매 확정
    /// </summary>
    /// <param name="upgradeId">강화 ID</param>
    /// <returns>실제 구매 결과</returns>
    public UpgradePurchaseResult TryUpgrade ( string upgradeId )
    {
        UpgradeState state = GetUpgradeState ( upgradeId );
        if ( state.Result != UpgradePurchaseResult.Success )
            return state.Result;

        int nextLevel = state.Level + 1;
        int nextBalance = _currency.GetBalance ( CurrencyType.Permanent ) - state.Price;

        if ( _saveService.TrySaveUpgradePurchase ( nextBalance , upgradeId , nextLevel ) == false )
            return UpgradePurchaseResult.SaveFailed;

        //재화 알림에서도 새 강화 단계를 조회할 수 있도록 먼저 반영
        _progress.ApplySavedLevel ( upgradeId , nextLevel );
        _currency.ApplySavedPermanentBalance ( nextBalance );
        _progress.NotifyChanged ( );
        return UpgradePurchaseResult.Success;
    }
}
