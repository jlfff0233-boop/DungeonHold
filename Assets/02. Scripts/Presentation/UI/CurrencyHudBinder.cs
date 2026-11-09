using TMPro;
using UnityEngine;

public class CurrencyHudBinder : MonoBehaviour
{
    [SerializeField] private Currency _currency;
    [SerializeField] private CurrencyType _currencyType;
    [SerializeField] private TMP_Text _amountText;

    private Currency _boundCurrency;

    private void OnEnable()
    {
        if (_currency == null || _amountText == null)
        {
            Debug.LogWarning(
                "재화 원본과 표시할 텍스트를 연결하세요.", this);
            return;
        }

        _boundCurrency = _currency;
        _boundCurrency.OnBalanceChanged += HandleBalanceChanged;

        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (_boundCurrency != null)
        {
            _boundCurrency.OnBalanceChanged -= HandleBalanceChanged;
        }

        _boundCurrency = null;
    }

    private void Refresh()
    {
        if (_boundCurrency == null || _amountText == null)
            return;

        SetAmount(_boundCurrency.GetBalance(_currencyType));
    }

    private void HandleBalanceChanged(CurrencyType type, int amount)
    {
        if (type != _currencyType)
            return;

        SetAmount(amount);
    }

    private void SetAmount(int amount)
    {
        _amountText.text = Mathf.Max(0, amount).ToString("N0");
    }
}