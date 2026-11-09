using System;
using System.Collections.Generic;
using LJTest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public class ConsumableScreenBinder : MonoBehaviour
{
    [SerializeField] private bool _isShop;
    [SerializeField] private ShopData _shopData;
    [SerializeField] private TMP_FontAsset _font;

    private sealed class ItemRow
    {
        public ShopProduct Product;
        public GameObject Root;
        public TMP_Text Name;
        public TMP_Text Description;
        public TMP_Text Count;
        public TMP_Text Price;
        public Image Icon;
        public Button Action;
        public TMP_Text ActionLabel;
        public UnityEngine.Events.UnityAction Listener;
    }

    private readonly List<ItemRow> _rows = new List<ItemRow>();
    private TestScene _scene;
    private ItemInventory _inventory;
    private Currency _currency;
    private bool _initialized;
    private bool _requesting;
    private bool _wasInitialized;
    private int _lastRequestFrame = -1;
    private TMP_Text _status;
    private TMP_Text _balance;
    private TMP_Text _empty;
    private GameObject _view;
    private Button _entryButton;
    private Button _backButton;
    private ConsumableScreenBinder _returnStore;
    private ConsumableScreenBinder _visitingInventory;
    private CanvasGroup _storeGroup;
    private float _previousAlpha;
    private bool _previousInteractable;
    private bool _previousBlocksRaycasts;

    private bool Ready => _scene != null && _scene.isActiveAndEnabled && _scene.IsInitialized;

    private void OnEnable()
    {
        Bind();
        if (_initialized) Refresh();
    }

    private void Start()
    {
        if (_font == null)
        {
            TMP_Text source = GetComponentInChildren<TMP_Text>(true);
            if (source != null) _font = source.font;
        }
        if (_isShop) BuildShop();
        else BuildInventory();
        _initialized = true;
        Refresh();
        if (_returnStore != null) ShowInventory();
    }

    private void Update()
    {
        if (_scene == null || _inventory != _scene.Inventory || _currency != _scene.Currency)
            Bind();
        if (!_initialized) return;
        // Purchase phase and cooldown can change without an inventory event.
        if (_isShop || _wasInitialized != Ready || _view != null && _view.activeSelf) Refresh();
        if (_isShop && _visitingInventory != null && !_visitingInventory.gameObject.activeInHierarchy)
            RestoreStore();
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
        _inventory = _scene.Inventory;
        _currency = _scene.Currency;
        if (_inventory != null) _inventory.OnChanged += Refresh;
        if (_currency != null) _currency.OnBalanceChanged += HandleBalanceChanged;
    }

    private void Unbind()
    {
        if (_inventory != null) _inventory.OnChanged -= Refresh;
        if (_currency != null) _currency.OnBalanceChanged -= HandleBalanceChanged;
        _inventory = null;
        _currency = null;
    }

    private void HandleBalanceChanged(CurrencyType type, int amount)
    {
        if (type == CurrencyType.Permanent) Refresh();
    }

    private void OnDisable()
    {
        Unbind();
        if (_isShop)
        {
            if (_visitingInventory != null) _visitingInventory.CloseInventory();
            RestoreStore();
        }
        else
        {
            if (_view != null) _view.SetActive(false);
            if (_returnStore != null) _returnStore.RestoreStore();
            _returnStore = null;
        }
    }

    private void OnDestroy()
    {
        Unbind();
        foreach (ItemRow row in _rows)
            if (row.Action != null && row.Listener != null)
                row.Action.onClick.RemoveListener(row.Listener);
    }

    private void BuildShop()
    {
        _balance = FindText(transform, "CurrencyText");
        _status = MakeText(transform, "ConsumablePurchaseStatus", "", new Vector2(1000, 50), Vector2.zero);
        Anchor(_status.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 120));
        _entryButton = MakeButton(transform, "OpenConsumableInventory", "소모품 인벤토리", new Vector2(290, 52), Vector2.zero, OpenInventoryFromStore);
        Anchor((RectTransform)_entryButton.transform, new Vector2(1, 1), new Vector2(-180, -65));
        if (_shopData == null) return;
        foreach (IShopProduct shopProduct in _shopData.Products)
        {
            if (shopProduct is not ShopProduct product) continue;
            if (product == null || product.Item == null) continue;
            string cardName = product.Item is HealthItemData ? "SpellbookItemCard02" :
                product.Item is EnergyItemData ? "SpellbookItemCard03" : null;
            Transform card = cardName == null ? null : FindChild(transform, cardName);
            if (card == null) continue;
            Button action = card.GetComponent<Button>();
            if (action == null) action = card.gameObject.AddComponent<Button>();
            // Remove only the obsolete upgrade preview on these two potion cards.
            foreach (SpellbookCardPreview preview in card.GetComponents<SpellbookCardPreview>())
            {
                action.onClick.RemoveListener(preview.OpenDetail);
                preview.enabled = false;
            }
            Image background = card.GetComponent<Image>();
            if (background != null) { background.raycastTarget = true; action.targetGraphic = background; }
            Transform locked = FindChild(card, "LockedGroup");
            if (locked != null) locked.gameObject.SetActive(false);
            ItemRow row = new ItemRow { Product = product, Root = card.gameObject, Action = action,
                Name = FindText(card, "NameText"), Count = FindText(card, "TypeText"), Price = FindText(card, "CostText") };
            row.Description = MakeText(card, "ConsumableDescription", "", new Vector2(180, 26), Vector2.zero);
            Anchor(row.Description.rectTransform, new Vector2(0, 1), new Vector2(98, -130));
            row.Description.fontSizeMax = 18;
            row.Description.fontSizeMin = 14;
            Transform art = FindChild(card, "Artwork");
            row.Icon = art != null ? art.GetComponent<Image>() : null;
            row.Listener = () => Purchase(row);
            action.onClick.AddListener(row.Listener);
            _rows.Add(row);
        }
        _status.text = "물약 카드를 누르면 1개 구매합니다. 비용은 영구 재화로 지불합니다.";
    }

    private void BuildInventory()
    {
        _entryButton = MakeButton(transform, "OpenConsumables", "소모품 보유 목록", new Vector2(290, 52), Vector2.zero, ShowInventory);
        Anchor((RectTransform)_entryButton.transform, new Vector2(1, 1), new Vector2(-180, -65));
        RectTransform overlay = MakeRect("ConsumableInventoryView", transform, Vector2.zero, Vector2.zero);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.sizeDelta = Vector2.zero;
        _view = overlay.gameObject;
        overlay.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 0.98f);
        Canvas canvas = overlay.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        canvas.sortingOrder = parentCanvas != null ? parentCanvas.rootCanvas.sortingOrder + 30 : 30;
        overlay.gameObject.AddComponent<GraphicRaycaster>();
        MakeText(overlay, "Title", "소모품 인벤토리", new Vector2(800, 55), new Vector2(0, 310));
        _balance = MakeText(overlay, "PermanentCurrency", "", new Vector2(800, 45), new Vector2(0, 250));
        _empty = MakeText(overlay, "Empty", "보유 중인 소모품이 없습니다. 상점에서 물약을 구매하세요.", new Vector2(900, 80), new Vector2(0, 150));
        _status = MakeText(overlay, "Status", "", new Vector2(1000, 75), new Vector2(0, -235));
        if (_shopData != null)
            for (int i = 0; i < _shopData.Products.Count; i++)
            {
                if (_shopData.Products[i] is not ShopProduct product) continue;
                if (product == null || product.Item == null) continue;
                RectTransform root = MakeRect("OwnedItem" + i, overlay, new Vector2(900, 150), new Vector2(0, 95 - i * 175));
                root.gameObject.AddComponent<Image>().color = new Color(0.18f, 0.19f, 0.22f, 1);
                ItemRow row = new ItemRow { Product = product, Root = root.gameObject };
                row.Name = MakeText(root, "Name", "", new Vector2(520, 40), new Vector2(-70, 45));
                row.Description = MakeText(root, "Description", "", new Vector2(520, 60), new Vector2(-70, -5));
                row.Count = MakeText(root, "Count", "", new Vector2(520, 30), new Vector2(-70, -55));
                row.Action = MakeButton(root, "Use", "사용", new Vector2(200, 52), new Vector2(320, 0), () => Use(row));
                row.ActionLabel = row.Action.GetComponentInChildren<TMP_Text>();
                row.Icon = MakeRect("Icon", root, new Vector2(80, 80), new Vector2(-380, 0)).gameObject.AddComponent<Image>();
                _rows.Add(row);
            }
        _backButton = MakeButton(overlay, "Back", "뒤로", new Vector2(260, 55), new Vector2(0, -310), CloseInventory);
        _view.SetActive(false);
    }

    private void Purchase(ItemRow row)
    {
        if (!Ready || _requesting || _lastRequestFrame == Time.frameCount) return;
        _requesting = true;
        _lastRequestFrame = Time.frameCount;
        try
        {
            ShopPurchaseResult result = _scene.TryPurchaseItem(row.Product.Item.ItemId);
            _status.text = result == ShopPurchaseResult.Success ? $"{row.Product.Item.ItemName} 1개 구매 완료" : PurchaseReason(result);
        }
        finally { _requesting = false; Refresh(); }
    }

    private void Use(ItemRow row)
    {
        if (!Ready || _requesting || _lastRequestFrame == Time.frameCount) return;
        _requesting = true;
        _lastRequestFrame = Time.frameCount;
        try
        {
            ItemUseResult result = _scene.TryUseItem(row.Product.Item.ItemId, out float amount);
            _status.text = result == ItemUseResult.Success ? $"{row.Product.Item.ItemName} 사용 완료 · 회복 {amount:0.##}" : UseReason(result);
        }
        finally { _requesting = false; Refresh(); }
    }

    private void Refresh()
    {
        if (!_initialized) return;
        _wasInitialized = Ready;
        if (_balance != null) _balance.text = Ready && _currency != null
            ? (_isShop ? "" : "영구 재화: ") + _currency.GetBalance(CurrencyType.Permanent).ToString("N0") : "—";
        int total = 0;
        foreach (ItemRow row in _rows)
        {
            ItemData item = row.Product.Item;
            int count = Ready && _inventory != null ? _inventory.GetCount(item.ItemId) : 0;
            total += count;
            if (row.Name != null) row.Name.text = item.ItemName;
            if (row.Icon != null) { row.Icon.sprite = item.Icon; row.Icon.enabled = item.Icon != null; row.Icon.preserveAspect = true; }
            if (row.Count != null) row.Count.text = $"보유 {count} / {item.MaxCount}";
            if (_isShop)
            {
                if (row.Description != null) row.Description.text = item is HealthItemData ? "체력 회복" : "에너지 회복";
                foreach (SpellbookCardPreview preview in row.Root.GetComponents<SpellbookCardPreview>())
                    row.Action.onClick.RemoveListener(preview.OpenDetail);
                ShopPurchaseResult state = Ready ? _scene.GetPurchaseState(item.ItemId) : ShopPurchaseResult.Unavailable;
                if (row.Price != null) row.Price.text = state == ShopPurchaseResult.Success
                    ? row.Product.Price.ToString("N0") : $"{row.Product.Price:N0} · {PurchaseShortReason(state)}";
                if (row.Price != null)
                {
                    row.Price.enableAutoSizing = true;
                    row.Price.fontSizeMin = 12;
                    row.Price.fontSizeMax = 22;
                }
                row.Action.interactable = state == ShopPurchaseResult.Success && !_requesting;
            }
            else
            {
                row.Root.SetActive(count > 0);
                if (row.Description != null) row.Description.text = Description(item);
                ItemUseResult state = Ready ? _scene.GetItemUseState(item.ItemId) : ItemUseResult.Unavailable;
                row.Action.interactable = state == ItemUseResult.Success && !_requesting;
                row.ActionLabel.text = state == ItemUseResult.OnCooldown
                    ? $"대기 {_scene.GetRemainingItemCooldown(item.ItemId):0.0}초"
                    : state == ItemUseResult.Success ? "사용" : UseShortReason(state);
            }
        }
        if (_empty != null) _empty.gameObject.SetActive(total == 0);
        if (!_isShop && _view != null && _view.activeSelf) RefreshNavigation();
    }

    private void RefreshNavigation()
    {
        var buttons = new List<Button>();
        foreach (ItemRow row in _rows)
            if (row.Root.activeSelf && row.Action.interactable) buttons.Add(row.Action);
        if (_backButton != null) buttons.Add(_backButton);
        for (int i = 0; i < buttons.Count; i++)
            buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[(i + buttons.Count - 1) % buttons.Count],
                selectOnLeft = buttons[(i + buttons.Count - 1) % buttons.Count],
                selectOnDown = buttons[(i + 1) % buttons.Count], selectOnRight = buttons[(i + 1) % buttons.Count] };
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem == null) return;
        GameObject selection = eventSystem.currentSelectedGameObject;
        Button selectedButton = selection != null ? selection.GetComponent<Button>() : null;
        if (selection == null || !selection.transform.IsChildOf(_view.transform) ||
            !selection.activeInHierarchy || selectedButton != null && !selectedButton.interactable)
            _backButton.Select();
    }

    private void OpenInventoryFromStore()
    {
        ConsumableScreenBinder target = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (ConsumableScreenBinder candidate in root.GetComponentsInChildren<ConsumableScreenBinder>(true))
                if (!candidate._isShop) { target = candidate; break; }
        if (target == null) { _status.text = "연결된 인벤토리 화면을 찾지 못했습니다."; return; }
        if (_visitingInventory != null) return;
        _visitingInventory = target;
        _storeGroup = GetComponent<CanvasGroup>();
        if (_storeGroup == null) _storeGroup = gameObject.AddComponent<CanvasGroup>();
        _previousAlpha = _storeGroup.alpha;
        _previousInteractable = _storeGroup.interactable;
        _previousBlocksRaycasts = _storeGroup.blocksRaycasts;
        _storeGroup.alpha = 0;
        _storeGroup.interactable = false;
        _storeGroup.blocksRaycasts = false;
        // Keep StoreScreen active so its existing result/pause return context is preserved.
        target._returnStore = this;
        ShowWithParents(target.transform);
        if (target._initialized) target.ShowInventory();
    }

    private void ShowInventory()
    {
        if (_view == null) return;
        _view.SetActive(true);
        _status.text = "물약은 도전 중 실제 사용 조건을 만족할 때 사용할 수 있습니다.";
        Refresh();
        _backButton.Select();
    }

    public bool TryCloseConsumables()
    {
        if (_isShop || _view == null || !_view.activeInHierarchy) return false;
        CloseInventory();
        return true;
    }

    private void CloseInventory()
    {
        if (_view != null) _view.SetActive(false);
        if (_returnStore != null)
        {
            ConsumableScreenBinder store = _returnStore;
            _returnStore = null;
            gameObject.SetActive(false);
            store.RestoreStore();
        }
        else if (_entryButton != null && isActiveAndEnabled) _entryButton.Select();
    }

    private void RestoreStore()
    {
        if (_storeGroup != null)
        {
            _storeGroup.alpha = _previousAlpha;
            _storeGroup.interactable = _previousInteractable;
            _storeGroup.blocksRaycasts = _previousBlocksRaycasts;
        }
        _storeGroup = null;
        _visitingInventory = null;
        if (isActiveAndEnabled) { Refresh(); _entryButton.Select(); }
    }

    private static string Description(ItemData item) =>
        (item is HealthItemData ? "플레이어의 체력을 회복합니다." : item is EnergyItemData ? "플레이어의 에너지를 회복합니다." : "소모품") +
        $"\n재사용 대기시간 {item.Cooldown:0.##}초";

    private static string PurchaseReason(ShopPurchaseResult result)
    {
        switch (result)
        {
            case ShopPurchaseResult.CannotSpend: return "영구 재화가 부족합니다.";
            case ShopPurchaseResult.LimitReached: return "최대 보유 수량에 도달했습니다.";
            case ShopPurchaseResult.SaveFailed: return "저장 실패 · 구매는 반영되지 않았습니다. 다시 시도하세요.";
            case ShopPurchaseResult.UnknownProduct: return "판매하지 않는 상품입니다.";
            default: return "구매는 도전 종료 정리를 마친 입장 전 상태에서 가능합니다.";
        }
    }

    private static string PurchaseShortReason(ShopPurchaseResult result) => result == ShopPurchaseResult.CannotSpend
        ? "재화 부족" : result == ShopPurchaseResult.LimitReached ? "보유 최대" : "구매 불가";

    private static string UseReason(ItemUseResult result)
    {
        switch (result)
        {
            case ItemUseResult.NoItem: return "보유한 물약이 없습니다.";
            case ItemUseResult.OnCooldown: return "재사용 대기 중입니다.";
            case ItemUseResult.CannotUse: return "회복할 수 없거나 플레이어가 아이템을 사용할 수 없는 상태입니다.";
            case ItemUseResult.SaveFailed: return "저장 실패 · 수량은 차감되지 않았습니다.";
            case ItemUseResult.UnknownItem: return "등록되지 않은 아이템입니다.";
            default: return "아이템은 도전 중에만 사용할 수 있습니다.";
        }
    }

    private static string UseShortReason(ItemUseResult result) => result == ItemUseResult.Unavailable
        ? "도전 중 사용" : result == ItemUseResult.CannotUse ? "사용 조건 불충족" : result == ItemUseResult.NoItem ? "수량 없음" : "사용 불가";

    private static Transform FindChild(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child != root && child.name == objectName) return child;
        return null;
    }

    private static TMP_Text FindText(Transform root, string objectName)
    {
        Transform child = FindChild(root, objectName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static void ShowWithParents(Transform target)
    {
        if (target.parent != null && !target.parent.gameObject.activeInHierarchy) ShowWithParents(target.parent);
        target.gameObject.SetActive(true);
    }

    private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
    }

    private static RectTransform MakeRect(string objectName, Transform parent, Vector2 size, Vector2 position)
    {
        RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.gameObject.layer = parent.gameObject.layer;
        Anchor(rect, new Vector2(0.5f, 0.5f), position);
        rect.sizeDelta = size;
        return rect;
    }

    private TMP_Text MakeText(Transform parent, string objectName, string text, Vector2 size, Vector2 position)
    {
        TMP_Text label = MakeRect(objectName, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font;
        label.text = text;
        label.fontSize = 24;
        label.enableAutoSizing = true;
        label.fontSizeMin = 16;
        label.fontSizeMax = 24;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }

    private Button MakeButton(Transform parent, string objectName, string text, Vector2 size, Vector2 position,
        UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = MakeRect(objectName, parent, size, position);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.28f, 0.3f, 0.35f, 1);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        MakeText(rect, "Label", text, size, Vector2.zero);
        return button;
    }
}
