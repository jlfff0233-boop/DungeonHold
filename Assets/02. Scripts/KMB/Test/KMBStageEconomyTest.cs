using UnityEngine;

[RequireComponent(typeof(Currency))]
[RequireComponent(typeof(StageRewardController))]
public class KMBStageEconomyTest : MonoBehaviour
{
    [SerializeField] private ComboData _comboData;

    // 저장 연동 전 KMB 씬 전용 테스트 초기값
    [SerializeField] private int _testPermanentCurrency = 0;
    [SerializeField] private int _initialStageCurrency = 500;

    private Currency _currency;
    private StageRewardController _rewards;

    private bool _isReady;

    private void Awake()
    {
        _currency = GetComponent<Currency>();
        _rewards = GetComponent<StageRewardController>();
    }

    private void Start()
    {
        bool hasValidSettings =
            _comboData != null &&
            _testPermanentCurrency >= 0 &&
            _initialStageCurrency >= 0;

        if (!hasValidSettings)
        {
            Debug.LogError(
                "Combo Data와 테스트 시작 재화를 확인하세요.",
                this);

            enabled = false;
            return;
        }

        // 실제 저장 데이터가 아닌 테스트 잔액으로 초기화
        if (!_currency.Init(_testPermanentCurrency))
        {
            Debug.LogError("재화 초기화에 실패했습니다.", this);
            enabled = false;
            return;
        }

        if (!_rewards.Init(_currency, _comboData))
        {
            Debug.LogError("보상 연결부 초기화에 실패했습니다.", this);
            enabled = false;
            return;
        }

        // 도전 시작은 보상 연결부를 통해 한 번만 실행
        if (!_rewards.BeginStage(_initialStageCurrency))
        {
            Debug.LogError("재화 도전 시작에 실패했습니다.", this);
            enabled = false;
            return;
        }

        _isReady = true;
        PrintCurrencyState();
    }

    private void Update()
    {
        if (!_isReady)
        {
            return;
        }

        _rewards.UpdateCombo(Time.timeAsDouble);
    }

    [ContextMenu("테스트/재화 상태 확인")]
    private void PrintCurrencyState()
    {
        if (!Application.isPlaying || !_isReady)
        {
            return;
        }

        Debug.Log(
            $"재화 준비 완료: 도전 {_rewards.CurrentStageId}, " +
            $"스테이지 재화 {_currency.GetBalance(CurrencyType.Stage)}, " +
            $"영구 재화 {_currency.GetBalance(CurrencyType.Permanent)}",
            this);
    }
}