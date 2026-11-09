using System;
using LJTest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lives on PauseMenuRoot so its existing input/cursor lifetime also owns this panel.
public class CompanionHirePanel : MonoBehaviour
{
    [SerializeField] private CompanionHireDataMap _hireDataMap;
    [SerializeField] private TMP_FontAsset _font;

    private TestScene _scene;
    private CompanionHireController _controller;
    private Currency _currency;
    private Button _entryButton;
    private GameObject _panel;
    private TMP_Text _balance;
    private TMP_Text _message;
    private readonly TMP_Text[] _details = new TMP_Text[3];
    private readonly Button[] _hireButtons = new Button[3];
    private readonly TMP_Text[] _slotTexts = new TMP_Text[2];
    private readonly Button[] _rehireButtons = new Button[2];
    private Button _backButton;
    private bool _requesting;
    private int _lastRequestFrame = -1;
    private int _openedStage;

    private bool Available => _scene != null && _scene.isActiveAndEnabled &&
        _scene.IsInitialized && !_scene.IsEndingStage && _controller != null &&
        _controller.isActiveAndEnabled && _controller.CurrentStageId != 0 &&
        _currency != null && _currency.IsStageActive &&
        _currency.CurrentStageId == _controller.CurrentStageId;

    private void Awake()
    {
        if (_font == null)
        {
            TMP_Text source = GetComponentInChildren<TMP_Text>(true);
            if (source != null) _font = source.font;
        }
        _entryButton = MakeButton(transform, "CompanionHireButton", "동료 고용", Vector2.zero,
            new Vector2(480, 64), Open);
        _entryButton.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        LayoutElement layout = _entryButton.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 64;
        RectTransform root = transform as RectTransform;
        if (root != null) root.sizeDelta += new Vector2(0, 80);
    }

    private void OnEnable() => Bind();

    private void Update()
    {
        if (_scene == null || _controller != _scene.HireController || _currency != _scene.Currency)
            Bind();
        _entryButton.interactable = Available;
        if (_panel == null || !_panel.activeSelf) return;
        if (!Available || _openedStage != _controller.CurrentStageId)
        {
            Close();
            return;
        }
        Refresh(); // Rehire countdown is a clock value, not an entry-change event.
    }

    private void Bind()
    {
        Unbind();
        if (_scene == null)
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                _scene = root.GetComponentInChildren<TestScene>(true);
                if (_scene != null) break;
            }
        if (_scene == null) return;
        _controller = _scene.HireController;
        _currency = _scene.Currency;
        if (_controller != null) _controller.OnEntryChanged += HandleEntryChanged;
        if (_currency != null) _currency.OnBalanceChanged += HandleBalanceChanged;
    }

    private void Unbind()
    {
        if (_controller != null) _controller.OnEntryChanged -= HandleEntryChanged;
        if (_currency != null) _currency.OnBalanceChanged -= HandleBalanceChanged;
        _controller = null;
        _currency = null;
    }

    private void HandleEntryChanged(CompanionHireEntry entry) => Refresh();
    private void HandleBalanceChanged(CurrencyType type, int amount)
    {
        if (type == CurrencyType.Stage) Refresh();
    }

    public void Open()
    {
        Bind();
        if (!Available) return;
        if (_panel == null) CreatePanel();
        if (_panel == null) return;
        _openedStage = _controller.CurrentStageId;
        _panel.SetActive(true);
        _message.text = "고용 비용은 스테이지 재화로 지불합니다. 준비 중 고용한 동료는 Z/X로 배치할 수 있습니다.";
        Refresh();
        _backButton.Select();
    }

    public void Close()
    {
        if (_panel != null) _panel.SetActive(false);
        if (_entryButton != null && isActiveAndEnabled) _entryButton.Select();
    }

    public bool TryCloseHirePanel()
    {
        if (_panel == null || !_panel.activeInHierarchy) return false;
        Close();
        return true;
    }

    private void OnDisable()
    {
        Close();
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
        if (_panel != null) Destroy(_panel);
    }

    private void RequestHire(int index)
    {
        if (!CanRequest() || !TryGetData(index, out CompanionHireData data)) return;
        _requesting = true;
        _lastRequestFrame = Time.frameCount;
        try
        {
            bool success = _controller.TryHire(_controller.CurrentStageId, data, out _, out CompanionHireResult result);
            _message.text = success ? $"{GetName(index)} 고용 완료" : GetReason(result);
        }
        finally { _requesting = false; Refresh(); }
    }

    private void RequestRehire(int index)
    {
        if (!CanRequest() || !_controller.TryGetEntryAt(index, out CompanionHireEntry entry)) return;
        _requesting = true;
        _lastRequestFrame = Time.frameCount;
        try
        {
            bool success = _controller.TryRehire(_controller.CurrentStageId, entry.Id, out CompanionHireResult result);
            _message.text = success ? "재고용 완료" : GetReason(result);
        }
        finally { _requesting = false; Refresh(); }
    }

    private bool CanRequest() => Available && _panel != null && _panel.activeSelf &&
        _openedStage == _controller.CurrentStageId && !_requesting && _lastRequestFrame != Time.frameCount;

    private bool TryGetData(int index, out CompanionHireData data)
    {
        data = null;
        return _hireDataMap != null && _hireDataMap.TryGet((WeaponType)index, out data) &&
            data.CompanionData.CombatStatsData != null && data.CompanionData.AttackData != null;
    }

    private void Refresh()
    {
        if (_panel == null || !_panel.activeSelf) return;
        bool available = Available;
        _balance.text = available ? $"스테이지 재화: {_currency.GetBalance(CurrencyType.Stage):N0}" : "고용할 수 없는 상태입니다.";
        // Capacity/placement/hire-enabled checks remain authoritative in the controller.
        int count = 0;
        if (available)
            while (_controller.TryGetEntryAt(count, out _)) count++;
        if (available) _balance.text += $" · 동료 {count}/2";
        for (int i = 0; i < 3; i++)
        {
            bool valid = TryGetData(i, out CompanionHireData data);
            _details[i].text = valid
                ? $"{GetName(i)}\n기본 HP {data.CompanionData.CombatStatsData.MaxHp:0.##}\n기본 공격력 {data.CompanionData.AttackData.RawDamage:0.##}\n고용 {data.HireCost:N0} / 재고용 {data.RehireCost:N0}"
                : $"{GetName(i)}\n고용 데이터 미연결";
            _hireButtons[i].interactable = available && valid && !_requesting && count < 2 &&
                _currency.CanSpend(CurrencyType.Stage, data.HireCost);
            _hireButtons[i].GetComponentInChildren<TMP_Text>().text = !valid ? "데이터 미연결" :
                count >= 2 ? "고용 인원 최대" : available && !_currency.CanSpend(CurrencyType.Stage, data.HireCost)
                    ? "재화 부족" : "고용";
        }
        for (int i = 0; i < 2; i++)
        {
            CompanionHireEntry entry = null;
            bool exists = available && _controller.TryGetEntryAt(i, out entry);
            string key = i == 0 ? "Z" : "X";
            _slotTexts[i].text = exists
                ? $"{key} · {GetName((int)entry.Data.CompanionData.WeaponType)} · {GetState(entry)}\n재고용 비용 {entry.Data.RehireCost:N0}"
                : $"{key} · 빈 슬롯";
            bool ready = exists && (entry.State == CompanionHireState.Ready ||
                entry.State == CompanionHireState.Waiting && Time.timeAsDouble >= entry.ReadyTime);
            _rehireButtons[i].interactable = ready && !_requesting &&
                _currency.CanSpend(CurrencyType.Stage, entry.Data.RehireCost);
        }
        RefreshNavigation();
    }

    private void RefreshNavigation()
    {
        Button[] buttons = { _hireButtons[0], _hireButtons[1], _hireButtons[2],
            _rehireButtons[0], _rehireButtons[1], _backButton };
        var available = Array.FindAll(buttons, button => button != null && button.interactable);
        for (int i = 0; i < available.Length; i++)
        {
            Button previous = available[(i + available.Length - 1) % available.Length];
            Button next = available[(i + 1) % available.Length];
            available[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnLeft = previous, selectOnUp = previous, selectOnRight = next, selectOnDown = next };
        }
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem != null && eventSystem.currentSelectedGameObject != null)
        {
            Button selected = eventSystem.currentSelectedGameObject.GetComponent<Button>();
            if (selected != null && !selected.interactable) _backButton.Select();
        }
    }

    private static string GetState(CompanionHireEntry entry)
    {
        switch (entry.State)
        {
            case CompanionHireState.Hired: return "고용 완료 · 배치 대기";
            case CompanionHireState.Prepared: return "배치 완료 · 전투 대기";
            case CompanionHireState.Alive: return "생존";
            case CompanionHireState.Dying: return "사망 · 연출 종료 대기";
            case CompanionHireState.Waiting:
                return $"재고용 대기 {Math.Max(0, entry.ReadyTime - Time.timeAsDouble):0.0}초";
            case CompanionHireState.Ready: return "재고용 가능";
            default: return "상태 확인 필요";
        }
    }

    private static string GetName(int index) => index == 0 ? "검사" : index == 1 ? "궁수" : "마법사";

    private static string GetReason(CompanionHireResult result)
    {
        switch (result)
        {
            case CompanionHireResult.NotEnoughCurrency: return "스테이지 재화가 부족합니다.";
            case CompanionHireResult.LimitReached: return "동료는 최대 2명까지 고용할 수 있습니다.";
            case CompanionHireResult.NotReady: return "아직 재고용할 수 없습니다.";
            case CompanionHireResult.InvalidPosition: return "동료가 생성될 빈 위치를 확보하지 못했습니다.";
            case CompanionHireResult.InvalidData: return "고용 데이터가 연결되지 않았습니다.";
            case CompanionHireResult.InitializationFailed: return "동료 초기화에 실패했습니다. 재화는 차감되지 않았습니다.";
            case CompanionHireResult.PointOccupied: return "다른 동료가 점유한 배치점입니다.";
            case CompanionHireResult.AlreadyPlaced: return "이미 배치된 동료입니다.";
            default: return "현재는 고용할 수 없습니다.";
        }
    }

    private void CreatePanel()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        RectTransform overlay = MakeRect("CompanionHireScreen", canvas.rootCanvas.transform, Vector2.zero, Vector2.zero);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.sizeDelta = Vector2.zero;
        _panel = overlay.gameObject;
        overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.85f);
        Canvas modal = overlay.gameObject.AddComponent<Canvas>();
        modal.overrideSorting = true;
        modal.sortingOrder = canvas.rootCanvas.sortingOrder + 10;
        overlay.gameObject.AddComponent<GraphicRaycaster>();
        RectTransform body = MakeRect("Panel", overlay, new Vector2(940, 650), Vector2.zero);
        body.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.13f, 0.15f, 1);
        MakeText(body, "Title", "동료 고용", new Vector2(800, 50), new Vector2(0, 280));
        _balance = MakeText(body, "Currency", "", new Vector2(800, 40), new Vector2(0, 230));
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            float x = (i - 1) * 290;
            _details[i] = MakeText(body, "Companion" + i, "", new Vector2(275, 150), new Vector2(x, 110));
            _hireButtons[i] = MakeButton(body, "Hire" + i, "고용", new Vector2(x, 0), new Vector2(240, 50), () => RequestHire(index));
        }
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            float y = -75 - i * 75;
            _slotTexts[i] = MakeText(body, "Slot" + i, "", new Vector2(600, 65), new Vector2(-110, y));
            _rehireButtons[i] = MakeButton(body, "Rehire" + i, "재고용", new Vector2(330, y), new Vector2(160, 50), () => RequestRehire(index));
        }
        _message = MakeText(body, "Message", "", new Vector2(860, 65), new Vector2(0, -220));
        _backButton = MakeButton(body, "Back", "뒤로", new Vector2(0, -285), new Vector2(240, 48), Close);
        _backButton.Select();
    }

    private static RectTransform MakeRect(string objectName, Transform parent, Vector2 size, Vector2 position)
    {
        RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private TMP_Text MakeText(Transform parent, string objectName, string text, Vector2 size, Vector2 position)
    {
        TMP_Text label = MakeRect(objectName, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;
        label.text = text;
        label.fontSize = 26;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18;
        label.fontSizeMax = 26;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private Button MakeButton(Transform parent, string objectName, string text, Vector2 position, Vector2 size,
        UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = MakeRect(objectName, parent, size, position);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.3f, 0.32f, 0.36f, 1);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        MakeText(rect, "Label", text, size, Vector2.zero);
        return button;
    }
}
