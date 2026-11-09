using UnityEngine;

/// <summary>
/// 재화 설정 - 새 저장 데이터의 최초 영구 재화 설정
/// </summary>
[CreateAssetMenu( fileName = "CurrencyData", menuName = "Currency/CurrencyData" )]
public class CurrencyData : ScriptableObject
{
    [Header( "----- 최초 지급 -----" )]
    [SerializeField] int _initialPermanentCurrency = 300;       //빈 저장 슬롯의 최초 영구 재화

    /// <summary>
    /// 새 저장 데이터의 최초 영구 재화
    /// </summary>
    public int InitialPermanentCurrency => _initialPermanentCurrency;
}
