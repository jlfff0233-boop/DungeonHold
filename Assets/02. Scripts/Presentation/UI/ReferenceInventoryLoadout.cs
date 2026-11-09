using LJTest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reuses the live HUD presentation; this view never executes or reassigns slots.
[DefaultExecutionOrder(500)]
public class ReferenceInventoryLoadout : MonoBehaviour
{
    [SerializeField] private Sprite[] _itemIcons = new Sprite[2];
    [SerializeField] private ItemData[] _items = new ItemData[2];
    [SerializeField] private CompanionHireDataMap _companions;
    private TestScene _scene;
    private GameObject _focus;
    private bool _focusWasActive;
    private Button _potionCategory, _consumableEntry;
    private Transform _sourceBar, _sourceCurrency;
    private TMP_Text _currency;
    private readonly Image[] _icons = new Image[10];
    private readonly Image[] _sourceIcons = new Image[10];
    private readonly TMP_Text[] _values = new TMP_Text[10];
    private readonly TMP_Text[] _sourceValues = new TMP_Text[10];
    private readonly GameObject[] _selection = new GameObject[10];
    private readonly GameObject[] _sourceSelection = new GameObject[10];

    private void OnEnable()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "FocusPoint") { _focus = child.gameObject; break; }
        if (_focus != null)
        {
            _focusWasActive = _focus.activeSelf;
            _focus.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (_focus != null) _focus.SetActive(_focusWasActive);
    }

    private void Start()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (_scene == null) _scene = root.GetComponentInChildren<TestScene>(true);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == "Canvas_HUD")
                {
                    _sourceBar = child.Find("PlayerStatus/Loadout/ItemBarRoot");
                    _sourceCurrency = child.Find("StageStatus/Currency/CurrencyText");
                }
        }
        Transform target = transform.Find("EquipmentBarRoot");
        if (_sourceBar == null || target == null) return;
        Transform category = transform.Find("CategoryRoot/MenuTextButton (3)");
        Transform entry = transform.Find("OpenConsumables");
        _potionCategory = category != null ? category.GetComponent<Button>() : null;
        _consumableEntry = entry != null ? entry.GetComponent<Button>() : null;
        if (_potionCategory != null && _consumableEntry != null)
        {
            _potionCategory.onClick.AddListener(OpenConsumables);
            _consumableEntry.gameObject.SetActive(false);
        }
        for (int i = 0; i < 10; i++)
        {
            Transform source = _sourceBar.Find(i == 0 ? "WeaponSlots" : "InstallationSlots" + (i == 1 ? "" : " (" + (i - 1) + ")"));
            Transform cell = target.Find(i == 0 ? "EquipmentSlot" : "EquipmentSlot (" + i + ")");
            if (source == null || cell == null) continue;
            Transform icon = source.Find(i == 0 ? "Weapon" : "Installation");
            _sourceIcons[i] = icon != null ? icon.GetComponent<Image>() : null;
            Transform destination = cell.Find("SlotBackground/Icon");
            _icons[i] = destination != null ? destination.GetComponent<Image>() : null;
            foreach (TMP_Text label in source.GetComponentsInChildren<TMP_Text>(true))
                if (label.name.StartsWith("InstallationFee")) _sourceValues[i] = label;
            TMP_Text key = null;
            foreach (TMP_Text label in cell.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name.StartsWith("Key")) key = label;
                else label.enabled = false;
            }
            if (key != null) key.text = ((i + 1) % 10).ToString();
            Transform selected = cell.Find("SlotBackground/SelectedFrame");
            _selection[i] = selected != null ? selected.gameObject : null;
            Transform sourceSelected = source.Find("Selection");
            _sourceSelection[i] = sourceSelected != null ? sourceSelected.gameObject : null;
            if (key != null)
            {
                RectTransform valueRect = new GameObject("LoadoutCost", typeof(RectTransform)).GetComponent<RectTransform>();
                valueRect.SetParent(cell, false); valueRect.gameObject.layer = cell.gameObject.layer;
                valueRect.anchorMin = valueRect.anchorMax = valueRect.pivot = new Vector2(0, 1);
                valueRect.anchoredPosition = new Vector2(0, -98); valueRect.sizeDelta = new Vector2(94, 26);
                _values[i] = valueRect.gameObject.AddComponent<TextMeshProUGUI>();
                _values[i].font = key.font; _values[i].fontSize = 22;
                _values[i].alignment = TextAlignmentOptions.Center; _values[i].raycastTarget = false;
            }
        }
        TMP_Text sample = _sourceCurrency != null ? _sourceCurrency.GetComponent<TMP_Text>() : null;
        if (sample != null)
        {
            RectTransform rect = new GameObject("CurrencyAmount", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(transform, false); rect.gameObject.layer = gameObject.layer;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 0);
            rect.anchoredPosition = new Vector2(110, 38); rect.sizeDelta = new Vector2(280, 70);
            _currency = rect.gameObject.AddComponent<TextMeshProUGUI>();
            _currency.font = sample.font; _currency.fontSize = 54;
            _currency.color = new Color32(77, 232, 209, 255); _currency.raycastTarget = false;
        }
    }

    private void OpenConsumables() => _consumableEntry.onClick.Invoke();

    private void OnDestroy()
    {
        if (_potionCategory != null) _potionCategory.onClick.RemoveListener(OpenConsumables);
    }

    private void LateUpdate()
    {
        if (_sourceBar == null) return;
        for (int i = 0; i < 10; i++)
        {
            if (_icons[i] != null)
            {
                Image source = _sourceIcons[i];
                _icons[i].sprite = source != null ? source.sprite : null;
                _icons[i].enabled = source != null && source.enabled && source.sprite != null;
                _icons[i].color = source != null ? source.color : Color.white;
                _icons[i].preserveAspect = true;
                if (i >= 8 && i - 8 < _itemIcons.Length && _itemIcons[i - 8] != null)
                {
                    _icons[i].sprite = _itemIcons[i - 8];
                    _icons[i].enabled = true;
                    ItemData item = i - 8 < _items.Length ? _items[i - 8] : null;
                    bool available = item != null && _scene != null && _scene.IsInitialized &&
                        _scene.GetItemUseState(item.ItemId) == ItemUseResult.Success;
                    _icons[i].color = available ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1);
                }
            }
            if (_values[i] != null)
            {
                _values[i].text = _sourceValues[i] != null ? _sourceValues[i].text : "";
                if (_sourceValues[i] != null) _values[i].color = _sourceValues[i].color;
                if (i >= 8)
                {
                    ItemData item = i - 8 < _items.Length ? _items[i - 8] : null;
                    int count = item != null && _scene != null && _scene.IsInitialized && _scene.Inventory != null
                        ? _scene.Inventory.GetCount(item.ItemId) : 0;
                    _values[i].text = item != null ? $"×{count}" : "";
                }
                else if (i >= 5 && _companions != null && _companions.TryGet((WeaponType)(i - 5), out CompanionHireData data))
                {
                    bool rehire = _scene != null && _scene.HireController != null &&
                        _scene.HireController.TryGetRehireEntry(data, out _);
                    int cost = rehire ? data.RehireCost : data.HireCost;
                    _values[i].text = cost.ToString("N0");
                    bool affordable = _scene != null && _scene.Currency != null && _scene.Currency.CanSpend(CurrencyType.Stage, cost);
                    _values[i].color = affordable ? new Color32(77, 232, 209, 255) : new Color32(232, 90, 90, 255);
                }
            }
            if (_selection[i] != null)
                _selection[i].SetActive(_sourceSelection[i] != null && _sourceSelection[i].activeSelf);
        }
        if (_currency != null && _sourceCurrency != null)
            _currency.text = _sourceCurrency.GetComponent<TMP_Text>().text;
    }
}
