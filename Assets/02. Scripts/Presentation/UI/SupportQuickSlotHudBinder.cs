using System;
using System.Collections.Generic;
using LJPlay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Read-only HUD for indices 5..9. InputHandler remains the sole key execution path.
[DefaultExecutionOrder(400)]
public class SupportQuickSlotHudBinder : MonoBehaviour
{
    [Serializable]
    private sealed class Slot
    {
        public QuickSlotData Source;
        public WeaponType CompanionType;
        public ItemData Item;
        [NonSerialized] public Image Image, HireStatus, EmploymentStatus;
        [NonSerialized] public TMP_Text CostOrCount, Quantity, State, Name, Key;
    }

    [SerializeField] private CompanionHireDataMap _companions;
    [SerializeField] private int _maxHireCount = 2;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private Texture2D[] _companionIcons = new Texture2D[3];
    [SerializeField] private Texture2D _healthIcon;
    [SerializeField] private Texture2D _energyIcon;
    [SerializeField] private Slot[] _slots = new Slot[5];
    private PlayScene _scene;
    private InputHandler _input;
    private CompanionHireController _hire;
    private ItemInventory _inventory;
    private Currency _currency;
    private readonly Dictionary<Texture2D, Sprite> _sprites = new Dictionary<Texture2D, Sprite>();
    private bool _built;

    private void Start()
    {
        Bind();
        Transform player = null;
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.name == "PlayerStatus") { player = child; break; }
        Transform bar = player != null ? player.Find("Loadout/ItemBarRoot") : null;
        if (bar == null || _slots.Length != 5) return;
        for (int i = 0; i < 5; i++)
        {
            // The first cell is named WeaponSlots but belongs to installation key 1.
            // InstallationSlots (4)..(8) are the existing cells for keys 6..0.
            string objectName = "InstallationSlots (" + (i + 4) + ")";
            Transform root = bar.Find(objectName);
            if (root == null || _slots[i] == null) continue;
            Slot slot = _slots[i];
            Transform icon = root.Find("Installation");
            slot.Image = icon != null ? icon.GetComponent<Image>() : null;
            foreach (Image status in root.GetComponentsInChildren<Image>(true))
            {
                if (status.name.StartsWith("HireStatusIcon", StringComparison.Ordinal)) slot.HireStatus = status;
                if (status.name.StartsWith("EmploymentStatusIcon", StringComparison.Ordinal)) slot.EmploymentStatus = status;
            }
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name.StartsWith("InstallationFee", StringComparison.Ordinal)) slot.CostOrCount = text;
                if (text.name == "Quantity") slot.Quantity = text;
                if (text.name.StartsWith("KeyText", StringComparison.Ordinal)) slot.Key = text;
            }
            if (slot.Key != null) slot.Key.text = i == 4 ? "0" : (i + 6).ToString();
            slot.Name = MakeText(root, "SupportSlotName", new Vector2(48, 15), new Vector2(108, 26));
            slot.Name.gameObject.SetActive(false);
            slot.State = MakeText(root, "SupportSlotState", new Vector2(48, -46), new Vector2(92, 38));
            // Passive HUD only: do not add Button or key-event handlers.
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        }
        _built = true;
        Refresh();
    }

    private void OnEnable()
    {
        Bind();
        if (_built) Refresh();
    }

    private void LateUpdate()
    {
        if (_scene == null || !_scene.isActiveAndEnabled || _hire != _scene.HireController || _inventory != _scene.Inventory || _currency != _scene.Currency)
            Bind();
        Refresh();
    }

    private void Bind()
    {
        Unbind();
        if (_scene == null || !_scene.isActiveAndEnabled)
        {
            _scene = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (PlayScene candidate in root.GetComponentsInChildren<PlayScene>(true))
                    if (candidate.isActiveAndEnabled) { _scene = candidate; break; }
                if (_scene != null) break;
            }
        }
        if (_scene == null) return;
#if UNITY_EDITOR
        SyncFromScene();
#endif
        if (_input == null)
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                _input = root.GetComponentInChildren<InputHandler>(true);
                if (_input != null) break;
            }
        _hire = _scene.HireController;
        _inventory = _scene.Inventory;
        _currency = _scene.Currency;
        if (_hire != null) _hire.OnEntryChanged += EntryChanged;
        if (_inventory != null) _inventory.OnChanged += Refresh;
        if (_currency != null) _currency.OnBalanceChanged += BalanceChanged;
    }

    private void Unbind()
    {
        if (_hire != null) _hire.OnEntryChanged -= EntryChanged;
        if (_inventory != null) _inventory.OnChanged -= Refresh;
        if (_currency != null) _currency.OnBalanceChanged -= BalanceChanged;
        _hire = null; _inventory = null; _currency = null;
    }

    private void EntryChanged(CompanionHireEntry entry) => Refresh();
    private void BalanceChanged(CurrencyType type, int amount)
    {
        if (type == CurrencyType.Stage) Refresh();
    }

    private void Refresh()
    {
        if (!_built) return;
        // Read the same slot data used by gameplay, including in standalone builds.
        if (_scene != null)
            for (int i = 0; i < _slots.Length; i++)
            {
                Slot slot = _slots[i];
                if (slot == null) continue;
                _scene.TryGetQuickSlot(i + 5, out QuickSlotData source);
                slot.Source = source;
                slot.Item = source is ItemQuickSlotData itemSlot ? itemSlot.Item : null;
                if (source is CompanionQuickSlotData companionSlot) slot.CompanionType = companionSlot.WeaponType;
            }
        bool inStage = _scene != null && _scene.isActiveAndEnabled && _scene.IsInitialized &&
            _scene.IsStageReady && !_scene.IsEndingStage && _currency != null && _currency.IsStageActive;
        bool inputEnabled = _input != null && _input.isActiveAndEnabled;
        foreach (Slot slot in _slots)
        {
            if (slot == null || slot.State == null) continue;
            string state;
            bool available = false;
            bool hasItems = false;
            if (slot.Source is CompanionQuickSlotData)
            {
                slot.Name.text = CompanionName(slot.CompanionType);
                state = CompanionState(slot, inStage, out available);
            }
            else if (slot.Source is ItemQuickSlotData && slot.Item != null)
            {
                slot.Name.text = slot.Item.ItemName;
                int count = _scene != null && _scene.isActiveAndEnabled && _scene.IsInitialized && _scene.Inventory != null
                    ? _scene.Inventory.GetCount(slot.Item.ItemId) : 0;
                hasItems = count > 0;
                if (slot.Quantity != null) slot.Quantity.text = count.ToString();
                if (slot.CostOrCount != null) slot.CostOrCount.text = "";
                ItemUseResult result = inStage ? _scene.GetItemUseState(slot.Item.ItemId) : ItemUseResult.Unavailable;
                available = result == ItemUseResult.Success;
                state = result == ItemUseResult.Success ? "사용 가능" : result == ItemUseResult.NoItem ? "" :
                    result == ItemUseResult.OnCooldown ? $"{_scene.GetRemainingItemCooldown(slot.Item.ItemId):0.0}초" :
                    result == ItemUseResult.CannotUse ?
                        (_scene.Hero != null && _scene.Hero.CanAct && (slot.Item is HealthItemData || slot.Item is EnergyItemData)
                            ? "회복 불필요" : "사용 불가") :
                    "사용 불가";
            }
            else
            {
                slot.Name.text = "설정 미연결";
                if (slot.CostOrCount != null) slot.CostOrCount.text = "—";
                state = "사용 불가";
            }
            if (inStage && !inputEnabled) { state = "입력 차단"; available = false; }
            slot.State.text = "";
            slot.State.gameObject.SetActive(false);
            if (slot.Source is CompanionQuickSlotData)
            {
                bool hired = IsHired(slot, inStage);
                if (slot.HireStatus != null) slot.HireStatus.gameObject.SetActive(hired);
                if (slot.EmploymentStatus != null) slot.EmploymentStatus.gameObject.SetActive(!hired);
            }
            // Item brightness indicates ownership, independently of cooldown, health or input state.
            ApplyIcon(slot, slot.Source is ItemQuickSlotData ? hasItems : available);
        }
    }

    private bool IsHired(Slot slot, bool inStage)
    {
        if (!inStage || _hire == null || _hire.CurrentStageId != _currency.CurrentStageId ||
            _companions == null || !_companions.TryGet(slot.CompanionType, out CompanionHireData data)) return false;
        for (int i = 0; _hire.TryGetEntryAt(i, out CompanionHireEntry entry); i++)
            if (entry.Data == data && (entry.State == CompanionHireState.Hired ||
                entry.State == CompanionHireState.Prepared || entry.State == CompanionHireState.Alive)) return true;
        return false;
    }

    private string CompanionState(Slot slot, bool inStage, out bool available)
    {
        available = false;
        if (_companions == null || !_companions.TryGet(slot.CompanionType, out CompanionHireData data))
        {
            if (slot.CostOrCount != null) slot.CostOrCount.text = "—";
            return "설정 미연결";
        }
        int cost = data.HireCost;
        string action = "고용";
        if (inStage && _hire != null && _hire.CurrentStageId == _currency.CurrentStageId)
        {
            if (_hire.TryGetRehireEntry(data, out _)) { cost = data.RehireCost; action = "재고용"; }
            else
            {
                int count = 0;
                CompanionHireEntry waiting = null;
                while (_hire.TryGetEntryAt(count, out CompanionHireEntry entry))
                {
                    if (entry.Data == data && (entry.State == CompanionHireState.Waiting || entry.State == CompanionHireState.Dying) &&
                        (waiting == null || entry.State == CompanionHireState.Waiting &&
                            (waiting.State == CompanionHireState.Dying || entry.ReadyTime < waiting.ReadyTime))) waiting = entry;
                    count++;
                }
                // The actual executor creates a new record if no rehire is ready and capacity remains.
                if (_maxHireCount > 0 && count >= _maxHireCount)
                {
                    if (slot.CostOrCount != null) slot.CostOrCount.text = waiting != null ? data.RehireCost.ToString("N0") : cost.ToString("N0");
                    return waiting == null ? "인원 최대" : waiting.State == CompanionHireState.Dying ? "사망 처리 중" :
                        $"재고용 {Math.Max(0, waiting.ReadyTime - Time.timeAsDouble):0.0}초";
                }
            }
            available = _currency.CanSpend(CurrencyType.Stage, cost);
        }
        if (slot.CostOrCount != null) slot.CostOrCount.text = cost.ToString("N0");
        if (!inStage) return "입장 전";
        if (_hire == null || _hire.CurrentStageId != _currency.CurrentStageId) return "고용 불가";
        return available ? action : "재화 부족";
    }

    private void ApplyIcon(Slot slot, bool available)
    {
        if (slot.Image == null) return;
        int type = (int)slot.CompanionType;
        Texture2D texture = slot.Source is CompanionQuickSlotData
            ? type >= 0 && type < _companionIcons.Length ? _companionIcons[type] : null
            : slot.Item is HealthItemData ? _healthIcon : slot.Item is EnergyItemData ? _energyIcon : null;
        Sprite sprite = null;
        if (texture != null && !_sprites.TryGetValue(texture, out sprite))
        {
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _sprites.Add(texture, sprite);
        }
        slot.Image.sprite = sprite;
        slot.Image.enabled = sprite != null;
        slot.Image.preserveAspect = true;
        slot.Image.color = available ? Color.white : new Color(0.35f, 0.35f, 0.35f, 1);
    }

    private TMP_Text MakeText(Transform parent, string objectName, Vector2 position, Vector2 size)
    {
        RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
        TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font; text.fontSize = 16; text.enableAutoSizing = true; text.fontSizeMin = 10; text.fontSizeMax = 16;
        text.alignment = TextAlignmentOptions.Center; text.color = Color.white; text.raycastTarget = false;
        return text;
    }

    private static string CompanionName(WeaponType type) => type == WeaponType.Sword ? "검사" : type == WeaponType.Bow ? "궁수" : "마법사";

    private void OnDisable()
    {
        Unbind();
        foreach (Slot slot in _slots)
        {
            if (slot == null) continue;
            if (slot.State != null) slot.State.text = "";
            if (slot.CostOrCount != null) slot.CostOrCount.text = "";
            if (slot.Quantity != null) slot.Quantity.text = "";
            if (slot.Image != null) { slot.Image.sprite = null; slot.Image.enabled = false; }
        }
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in _sprites.Values) if (sprite != null) Destroy(sprite);
    }

#if UNITY_EDITOR
    [ContextMenu("물약 HUD 실제 수량 확인")]
    private void LogItemCounts()
    {
        if (!Application.isPlaying) return;
        Bind();
        Refresh();
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_scene == null || !_scene.IsInitialized ||
                !_scene.TryGetQuickSlot(i + 5, out QuickSlotData source) ||
                !(source is ItemQuickSlotData itemSlot) || itemSlot.Item == null) continue;
            string id = itemSlot.Item.ItemId;
            Debug.Log($"물약 HUD: 씬={_scene.name}, 아이템={id}, "
                + $"실제 보유={_scene.Inventory.GetCount(id)}, "
                + $"UI={(_slots[i]?.Quantity != null ? _slots[i].Quantity.text : "미연결")}, "
                + $"사용 상태={_scene.GetItemUseState(id)}", this);
        }
    }

    private void OnValidate()
    {
        foreach (Slot slot in _slots) SyncSlot(slot);
    }

    private static void SyncSlot(Slot slot)
    {
        if (slot == null || slot.Source == null) return;
        var source = new UnityEditor.SerializedObject(slot.Source);
        if (slot.Source is CompanionQuickSlotData)
        {
            var type = source.FindProperty("_weaponType");
            if (type != null) slot.CompanionType = (WeaponType)type.intValue;
            slot.Item = null;
        }
        else if (slot.Source is ItemQuickSlotData)
        {
            var item = source.FindProperty("_item");
            if (item != null) slot.Item = item.objectReferenceValue as ItemData;
        }
    }

    private void SyncFromScene()
    {
        // Read serialized contracts in the Editor without changing their owners or exposing new game APIs.
        var source = new UnityEditor.SerializedObject(_scene);
        var slots = source.FindProperty("_quickSlots");
        var input = source.FindProperty("_inputHandler");
        if (input != null) _input = input.objectReferenceValue as InputHandler;
        var companions = source.FindProperty("_companionHireDataMap");
        if (companions != null) _companions = companions.objectReferenceValue as CompanionHireDataMap;
        var controller = source.FindProperty("_hireController");
        if (controller != null && controller.objectReferenceValue != null)
        {
            var hire = new UnityEditor.SerializedObject(controller.objectReferenceValue);
            var maximum = hire.FindProperty("_maxHireCount");
            if (maximum != null) _maxHireCount = maximum.intValue;
        }
        if (slots == null || slots.arraySize < 10 || _slots.Length != 5) return;
        for (int i = 0; i < 5; i++)
        {
            if (_slots[i] == null) continue;
            _slots[i].Source = slots.GetArrayElementAtIndex(i + 5).objectReferenceValue as QuickSlotData;
            SyncSlot(_slots[i]);
        }
    }
#endif
}
