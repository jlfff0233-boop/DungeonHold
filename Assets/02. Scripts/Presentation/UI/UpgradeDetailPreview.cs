using System;
using LJTest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeDetailPreview : MonoBehaviour
{
    [Serializable]
    private sealed class UpgradeRow
    {
        public GameObject Root;
        public TMP_Text Heading;
        public TMP_Text Level;
        public TMP_Text Effect;
        public TMP_Text Cost;
        public Button Purchase;
        public GameObject LockedRoot;
        public TMP_Text LockReason;
    }

    [SerializeField] private GameObject _spellbookScreen;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _descriptionText;
    [SerializeField] private Image _artwork;

    [Header("실제 강화 연결")]
    [SerializeField] private TestScene _scene;
    [SerializeField] private TMP_Text _currencyText;
    [SerializeField] private TMP_Text _typeText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private UpgradeData[] _defaultUpgrades = Array.Empty<UpgradeData>();
    [SerializeField] private bool _showNextLevel;
    [SerializeField] private UpgradeRow[] _rows = Array.Empty<UpgradeRow>();
    [SerializeField] private GameObject[] _unusedRoots = Array.Empty<GameObject>();

    private UpgradeData[] _upgrades = Array.Empty<UpgradeData>();
    private string _description;
    private string _purchaseMessage;
    private bool _isPurchasing;
    private bool _wasAvailable;
    private int _lastPurchaseFrame = -1;
    private Currency _boundCurrency;
    private bool _hasSelection;
    private UpgradeDetailUIData _selectedUIData;
    private Transform[] _uniqueGroups;
    private GameObject _returnScreen;
    private GameObject _returnSelection;
    private Button[] _backButtons = Array.Empty<Button>();
    private TestScene _boundScene;
    private UnityEngine.Events.UnityAction[] _purchaseActions;

    private bool CanQuery => _scene != null && _scene.isActiveAndEnabled &&
        _scene.IsInitialized && !_scene.IsStageReady && !_scene.IsEndingStage;

    private void Awake()
    {
        if (!_hasSelection)
        {
            _upgrades = _defaultUpgrades ?? Array.Empty<UpgradeData>();
            _description = "플레이어의 능력치를 강화합니다.";
            if (_nameText != null) _nameText.text = "플레이어";
            if (_typeText != null) _typeText.text = "";
        }
        _purchaseActions = new UnityEngine.Events.UnityAction[_rows.Length];
        for (int i = 0; i < _rows.Length; i++)
        {
            int index = i;
            _purchaseActions[i] = () => Purchase(index);
            if (_rows[i].Purchase != null)
            {
                if (_rows[i].Purchase.targetGraphic != null)
                    _rows[i].Purchase.targetGraphic.raycastTarget = true;
                _rows[i].Purchase.onClick.AddListener(_purchaseActions[i]);
            }
            if (_rows[i].Effect != null)
            {
                _rows[i].Effect.enableAutoSizing = true;
                _rows[i].Effect.fontSizeMax = _rows[i].Effect.fontSize;
                _rows[i].Effect.fontSizeMin = Mathf.Min(14f, _rows[i].Effect.fontSize);
            }
        }
        _backButtons = Array.FindAll(GetComponentsInChildren<Button>(true), button =>
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            return button.name == "BackButton" ||
                (label != null && (label.text.Trim() == "뒤로" || label.text.Trim() == "닫기"));
        });
        foreach (Button button in _backButtons)
            button.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        if (_purchaseActions != null)
            for (int i = 0; i < _rows.Length; i++)
                if (_rows[i].Purchase != null)
                    _rows[i].Purchase.onClick.RemoveListener(_purchaseActions[i]);
        foreach (Button button in _backButtons)
            if (button != null) button.onClick.RemoveListener(Close);
    }

    private void OnEnable()
    {
        HideUnusedGroups();

        BindScene();
        Refresh();
    }

    private void Start() => Refresh();

    private void LateUpdate()
    {
        HideUnusedGroups();
    }

    private void Update()
    {
        if (_scene == null || _boundScene != _scene) BindScene();
        if (_wasAvailable != CanQuery ||
            (_scene != null && _scene.Currency != _boundCurrency)) Refresh();
    }

    private void OnDisable()
    {
        if (_boundScene != null) _boundScene.OnUpgradeChanged -= Refresh;
        _boundScene = null;
        if (_boundCurrency != null)
            _boundCurrency.OnBalanceChanged -= HandleBalanceChanged;
        _boundCurrency = null;
    }

    private void BindScene()
    {
        if (_scene == null || _scene.gameObject.scene != gameObject.scene)
        {
            _scene = null;
            if (gameObject.scene.IsValid() && gameObject.scene.isLoaded)
                foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                {
                    _scene = root.GetComponentInChildren<TestScene>(true);
                    if (_scene != null) break;
                }
        }
        if (_boundScene == _scene) return;
        if (_boundScene != null) _boundScene.OnUpgradeChanged -= Refresh;
        _boundScene = _scene;
        if (_boundScene != null) _boundScene.OnUpgradeChanged += Refresh;
    }

    public void Open(UpgradeDetailUIData data)
    {
        Open(data, _spellbookScreen);
    }

    public void Open(UpgradeDetailUIData data, GameObject returnScreen)
    {
        if (data == null) return;
        OpenInternal(data.NameText, data.DescriptionText, data.Artwork, new UpgradeData[]
        {
            data.FirstCard != null ? data.FirstCard.Upgrade : null,
            data.SecondCard != null ? data.SecondCard.Upgrade : null
        }, data, returnScreen);
    }

    public void Open(string itemName, string description, Sprite artwork)
    {
        Open(itemName, description, artwork, Array.Empty<UpgradeData>());
    }

    public void Open(string itemName, string description, Sprite artwork, UpgradeData[] upgrades)
    {
        OpenInternal(itemName, description, artwork, upgrades, null, _spellbookScreen);
    }

    private void OpenInternal(string itemName, string description, Sprite artwork,
        UpgradeData[] upgrades, UpgradeDetailUIData uiData, GameObject returnScreen)
    {
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        _returnSelection = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        // Set both cards and their display data before SetActive invokes OnEnable.
        _returnScreen = ResolveReturnScreen(returnScreen);
        _selectedUIData = uiData;
        _hasSelection = true;
        _upgrades = upgrades ?? Array.Empty<UpgradeData>();
        _description = description;
        _purchaseMessage = null;
        if (_typeText != null)
            _typeText.text = uiData != null ? uiData.TypeText :
                _upgrades.Length == 0 ? "" : GetTargetType(_upgrades[0]);
        if (_nameText != null)
            _nameText.text = itemName;

        if (_artwork != null)
        {
            _artwork.sprite = artwork;
            _artwork.enabled = artwork != null;
        }

        gameObject.SetActive(true);

        if (_returnScreen != null)
            _returnScreen.SetActive(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Refresh();
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            Button first = Array.Find(_backButtons, button => button != null && button.isActiveAndEnabled && button.interactable);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : null);
        }
    }

    public void Close()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        GameObject destination = ResolveReturnScreen(_returnScreen);
        gameObject.SetActive(false);
        if (destination != null) ShowWithParents(destination.transform);
        else Debug.LogError("업그레이드 상세 화면에서 돌아갈 StoreScreen을 찾지 못했습니다.", this);
        if (UnityEngine.EventSystems.EventSystem.current != null && _returnSelection != null && _returnSelection.activeInHierarchy)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(_returnSelection);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private GameObject ResolveReturnScreen(GameObject requested)
    {
        if (requested != null && requested != gameObject && requested.scene == gameObject.scene) return requested;
        if (_spellbookScreen != null && _spellbookScreen != gameObject &&
            _spellbookScreen.scene == gameObject.scene) return _spellbookScreen;
        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded) return null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child != transform && !child.IsChildOf(transform) &&
                    (child.name.StartsWith("StoreScreen", StringComparison.Ordinal) ||
                     child.name.StartsWith("SpellbookScreen", StringComparison.Ordinal)))
                    return child.gameObject;
        return null;
    }

    private static void ShowWithParents(Transform target)
    {
        if (target.parent != null && !target.parent.gameObject.activeInHierarchy)
            ShowWithParents(target.parent);
        target.gameObject.SetActive(true);
    }

    private void Purchase(int index)
    {
        if (_isPurchasing || _lastPurchaseFrame == Time.frameCount || !CanQuery ||
            index < 0 || index >= _upgrades.Length || _upgrades[index] == null) return;

        _lastPurchaseFrame = Time.frameCount;
        _isPurchasing = true;
        _purchaseMessage = null;
        try
        {
            UpgradePurchaseResult result = _scene.TryUpgrade(_upgrades[index].UpgradeId);
            _purchaseMessage = result == UpgradePurchaseResult.Success
                ? "강화 완료 · 다음 스테이지 입장 시 적용됩니다." : GetReason(result);
        }
        finally
        {
            _isPurchasing = false;
            Refresh();
        }
    }

    private void HandleBalanceChanged(CurrencyType type, int amount)
    {
        if (type == CurrencyType.Permanent) Refresh();
    }

    private void Refresh()
    {
        Currency currency = _scene != null ? _scene.Currency : null;
        if (_boundCurrency != currency)
        {
            if (_boundCurrency != null) _boundCurrency.OnBalanceChanged -= HandleBalanceChanged;
            _boundCurrency = currency;
            if (_boundCurrency != null) _boundCurrency.OnBalanceChanged += HandleBalanceChanged;
        }
        _wasAvailable = CanQuery;
        if (_currencyText != null)
            _currencyText.text = _scene != null && _scene.IsInitialized && currency != null
                ? currency.GetBalance(CurrencyType.Permanent).ToString("N0") : "—";
        if (_descriptionText != null)
            _descriptionText.text = _description ?? "";
        if (_statusText != null)
        {
            string notice = _purchaseMessage ?? (_upgrades.Length == 0
                ? "강화 데이터가 연결되지 않았습니다."
                : CanQuery ? "" : GetReason(UpgradePurchaseResult.Unavailable));
            _statusText.text = notice;
        }
        for (int i = 0; i < _rows.Length; i++)
        {
            UpgradeRow row = _rows[i];
            UpgradeData data = i < _upgrades.Length ? _upgrades[i] : null;
            if (row.Root != null) row.Root.SetActive(data != null);
            if (data == null) continue;
            UpgradeState state = CanQuery ? _scene.GetUpgradeState(data.UpgradeId)
                : new UpgradeState(UpgradePurchaseResult.Unavailable);
            bool known = state.Result == UpgradePurchaseResult.Success ||
                state.Result == UpgradePurchaseResult.CannotSpend || state.Result == UpgradePurchaseResult.MaxLevel;
            bool purchasable = state.Result == UpgradePurchaseResult.Success && !_isPurchasing;
            if (row.Heading != null) row.Heading.text = GetHeading(data);
            int displayLevel = _showNextLevel && state.Level < data.MaxLevel ? state.Level + 1 : state.Level;
            if (row.Level != null)
                row.Level.text = state.Result == UpgradePurchaseResult.MaxLevel
                    ? "MAX" : known ? displayLevel.ToString() : "—";
            if (row.Effect != null)
                row.Effect.text = known
                    ? GetEffectPreview(data, state.Level, GetSelectedEffectText(i))
                    : GetSelectedEffectText(i);
            if (row.Cost != null) row.Cost.text = known && state.Result != UpgradePurchaseResult.MaxLevel
                ? state.Price.ToString("N0") : "—";
            if (row.Purchase != null) row.Purchase.interactable = purchasable;
            // Availability applies to the whole screen and is already shown in
            // _statusText. Do not repeat that long message over every card.
            bool showCardLock = CanQuery && !purchasable;
            if (row.LockedRoot != null) row.LockedRoot.SetActive(showCardLock);
            if (row.LockReason != null)
                row.LockReason.text = showCardLock ? GetCardLockReason(state.Result) : string.Empty;
        }

        HideUnusedGroups();
    }

    private void HideUnusedGroups()
    {
        foreach (GameObject root in _unusedRoots)
            if (root != null) root.SetActive(false);

        // Scene instances may have a different layout or stale serialized references.
        // Find the unused group by name rather than assuming a fixed parent path.
        if (_uniqueGroups == null)
            _uniqueGroups = Array.FindAll(GetComponentsInChildren<Transform>(true),
                child => child != transform && child.name == "UniqueGroup");
        foreach (Transform child in _uniqueGroups)
            if (child != null && child.gameObject.activeSelf)
                child.gameObject.SetActive(false);
    }

    private string GetSelectedEffectText(int index)
    {
        UpgradeDetailUIData.CardData card = _selectedUIData == null ? null :
            index == 0 ? _selectedUIData.FirstCard :
            index == 1 ? _selectedUIData.SecondCard : null;
        if (card != null && !string.IsNullOrWhiteSpace(card.EffectText))
            return card.EffectText;
        UpgradeData upgrade = index >= 0 && index < _upgrades.Length ? _upgrades[index] : null;
        return GetEffectDescription(upgrade);
    }

    private static string GetEffectPreview(UpgradeData data, int level, string description)
    {
        int current = Mathf.Clamp(level, 0, data.MaxLevel);
        string value = GetEffectValue(data, current);
        if (string.IsNullOrEmpty(value)) return description;
        if (current >= data.MaxLevel)
            return $"{description}\n현재: {value} · 최대 단계";
        return $"{description}\n현재: {value}\n다음: {GetEffectValue(data, current + 1)}";
    }

    private static string GetEffectValue(UpgradeData data, int level)
    {
        switch (data)
        {
            case PlayerHpUpgradeData hp:
                return $"HP +{hp.GetMaxHpBonus(level):0.##}";
            case PlayerMoveSpeedUpgradeData move:
                return $"이동속도 +{move.GetMoveSpeedBonus(level) * 100f:0.##}%";
            case WeaponDamageUpgradeData damage:
                return $"공격력 +{damage.GetDamageBonus(level) * 100f:0.##}%";
            case WeaponAttackSpeedUpgradeData speed:
                return $"공격속도 +{speed.GetAttackSpeedBonus(level) * 100f:0.##}%";
            case InstallationDamageUpgradeData trapDamage:
                return $"피해량 +{trapDamage.GetDamageBonus(level) * 100f:0.##}%";
            case InstallationIntervalUpgradeData interval:
                return $"공격 주기 -{interval.GetIntervalReduction(level) * 100f:0.##}%";
            case CompanionUpgradeData companion:
                CompanionUpgradeStats stats = companion.GetStats(level);
                return $"HP +{stats.HpBonus * 100f:0.##}% / 공격력 +{stats.DamageBonus * 100f:0.##}%";
            default:
                return string.Empty;
        }
    }

    private static string GetReason(UpgradePurchaseResult result)
    {
        switch (result)
        {
            case UpgradePurchaseResult.Success: return "강화 처리 중";
            case UpgradePurchaseResult.Unavailable: return "강화는 로비 또는 스테이지 입장 전에만 가능합니다.";
            case UpgradePurchaseResult.UnknownUpgrade: return "등록되지 않은 강화입니다.";
            case UpgradePurchaseResult.MaxLevel: return "최대 단계에 도달했습니다.";
            case UpgradePurchaseResult.CannotSpend: return "영구 골드가 부족합니다.";
            case UpgradePurchaseResult.SaveFailed: return "저장 실패 · 강화는 반영되지 않았습니다. 다시 시도하세요.";
            default: return "강화 상태를 확인할 수 없습니다.";
        }
    }

    private static string GetCardLockReason(UpgradePurchaseResult result)
    {
        switch (result)
        {
            case UpgradePurchaseResult.CannotSpend: return "골드 부족";
            case UpgradePurchaseResult.MaxLevel: return "최대 강화";
            case UpgradePurchaseResult.UnknownUpgrade: return "강화 미등록";
            case UpgradePurchaseResult.SaveFailed: return "저장 실패";
            default: return "강화 불가";
        }
    }

    private static string GetHeading(UpgradeData data)
    {
        switch (data)
        {
            case PlayerHpUpgradeData _: return "최대 HP";
            case PlayerMoveSpeedUpgradeData _: return "이동속도";
            case WeaponDamageUpgradeData _: return "공격력";
            case WeaponAttackSpeedUpgradeData _: return "공격속도";
            case CompanionUpgradeData _: return "동료 HP · 공격력";
            case InstallationDamageUpgradeData _: return "설치물 피해량";
            case InstallationIntervalUpgradeData _: return "공격 주기 단축";
            default: return "영구 강화";
        }
    }

    private static string GetTargetType(UpgradeData data)
    {
        if (data is PlayerHpUpgradeData || data is PlayerMoveSpeedUpgradeData) return "";
        if (data is WeaponUpgradeData) return "무기";
        if (data is CompanionUpgradeData) return "동료";
        if (data is InstallationUpgradeData) return "설치물";
        return "강화";
    }

    private static string GetEffectDescription(UpgradeData data)
    {
        switch (data)
        {
            case PlayerHpUpgradeData _: return "최대 HP가 증가합니다.";
            case PlayerMoveSpeedUpgradeData _: return "이동속도가 증가합니다.";
            case WeaponDamageUpgradeData _: return "공격력이 증가합니다.";
            case WeaponAttackSpeedUpgradeData _: return "공격속도가 증가합니다.";
            case CompanionUpgradeData _: return "동료의 최대 HP와 공격력이 증가합니다.";
            case InstallationDamageUpgradeData _: return "설치물의 피해량이 증가합니다.";
            case InstallationIntervalUpgradeData _: return "설치물의 공격 주기가 단축됩니다.";
            default: return "";
        }
    }

}
