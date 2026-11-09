using UnityEngine;
using UnityEngine.UI;
using TMPro;
using LJTest;

[RequireComponent(typeof(Button))]
public class SpellbookCardPreview : MonoBehaviour
{
    [SerializeField] private UpgradeDetailPreview _detailView;
    [SerializeField] private UpgradeDetailUIData _uiData;

    [Header("표시 확인용 정보")]
    [SerializeField] private string _itemName;
    [SerializeField, TextArea] private string _description;
    [SerializeField] private Sprite _artwork;

    [SerializeField] private UpgradeData[] _upgrades = new UpgradeData[0];

    private Button _button;
    [SerializeField, Min(0)] private int _confirmationCost;
    private GameObject _confirmation;
    private TMP_Text _confirmationMessage;
    private bool _isPaying;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OpenDetail);
    }

    private void OnEnable() => ResolveDetailView();

    private void ResolveDetailView()
    {
        if (_detailView != null && _detailView.gameObject.scene == gameObject.scene) return;
        _detailView = null;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            _detailView = canvas.rootCanvas.GetComponentInChildren<UpgradeDetailPreview>(true);
        if (_detailView != null) return;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            _detailView = root.GetComponentInChildren<UpgradeDetailPreview>(true);
            if (_detailView != null) return;
        }
    }

    private void OnDestroy()
    {
        CloseConfirmation();
        if (_button != null)
            _button.onClick.RemoveListener(OpenDetail);
    }

    public void OpenDetail()
    {
        ResolveDetailView();
        if (_detailView == null || _uiData == null)
            return;

        if (_confirmationCost > 0)
        {
            ShowConfirmation();
            return;
        }
        OpenSelectedDetail();
    }

    private void OpenSelectedDetail()
    {

        GameObject returnScreen = null;
        for (Transform parent = transform.parent; parent != null; parent = parent.parent)
        {
            if (!parent.name.StartsWith("StoreScreen") && !parent.name.StartsWith("SpellbookScreen"))
                continue;
            returnScreen = parent.gameObject;
            break;
        }
        _detailView.Open(_uiData, returnScreen);
    }

    private void OnDisable() => CloseConfirmation();

    public bool TryCancelPayment()
    {
        if (_confirmation == null || !_confirmation.activeInHierarchy) return false;
        CloseConfirmation();
        return true;
    }

    private void ShowConfirmation()
    {
        if (_confirmation != null) return;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        RectTransform overlay = CreateRect("ShopPaymentConfirmation", canvas.rootCanvas.transform,
            Vector2.zero, Vector2.zero);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.sizeDelta = Vector2.zero;
        _confirmation = overlay.gameObject;
        overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
        Canvas modalCanvas = overlay.gameObject.AddComponent<Canvas>();
        modalCanvas.overrideSorting = true;
        modalCanvas.sortingOrder = canvas.rootCanvas.sortingOrder + 100;
        overlay.gameObject.AddComponent<GraphicRaycaster>();
        RectTransform panel = CreateRect("Panel", overlay, new Vector2(620, 260), Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.16f, 0.18f, 1);
        _confirmationMessage = CreateLabel(panel, "Message",
            $"{_confirmationCost} 재화를 지불하시겠습니까?", new Vector2(560, 110), new Vector2(0, 40));
        Button confirm = CreateButton(panel, "Confirm", "확인", new Vector2(-130, -65), ConfirmPayment);
        Button cancel = CreateButton(panel, "Cancel", "취소", new Vector2(130, -65), CloseConfirmation);
        confirm.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnLeft = cancel, selectOnRight = cancel };
        cancel.navigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnLeft = confirm, selectOnRight = confirm };
        cancel.Select();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ConfirmPayment()
    {
        if (_isPaying || _confirmation == null) return;
        ResolveDetailView();
        if (_detailView == null || _uiData == null) return;
        TestScene scene = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            scene = root.GetComponentInChildren<TestScene>(true);
            if (scene != null) break;
        }
        if (scene == null || !scene.isActiveAndEnabled || !scene.IsInitialized ||
            scene.IsStageReady || scene.IsEndingStage || scene.Currency == null)
        {
            _confirmationMessage.text = "현재는 구매할 수 없습니다.";
            return;
        }
        _isPaying = true;
        try
        {
            if (!scene.Currency.CanSpend(CurrencyType.Permanent, _confirmationCost))
            {
                _confirmationMessage.text = "재화가 부족합니다.";
                return;
            }
            // Permanent currency is saved before the balance changes.
            if (!scene.Currency.TrySpend(CurrencyType.Permanent, _confirmationCost))
            {
                _confirmationMessage.text = "결제 저장에 실패했습니다. 다시 시도해 주세요.";
                return;
            }
            CloseConfirmation();
            OpenSelectedDetail();
        }
        finally { _isPaying = false; }
    }

    private void CloseConfirmation()
    {
        if (_confirmation == null) return;
        _confirmation.SetActive(false);
        Destroy(_confirmation);
        _confirmation = null;
        _confirmationMessage = null;
        if (_button != null && isActiveAndEnabled) _button.Select();
    }

    private static RectTransform CreateRect(string objectName, Transform parent, Vector2 size, Vector2 position)
    {
        RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private TMP_Text CreateLabel(Transform parent, string objectName, string text, Vector2 size, Vector2 position)
    {
        TMP_Text label = CreateRect(objectName, parent, size, position).gameObject.AddComponent<TextMeshProUGUI>();
        TMP_Text source = GetComponentInChildren<TMP_Text>(true);
        if (source != null) label.font = source.font;
        label.text = text;
        label.fontSize = 28;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    private Button CreateButton(Transform parent, string objectName, string text, Vector2 position,
        UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = CreateRect(objectName, parent, new Vector2(210, 64), position);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.35f, 0.35f, 0.39f, 1);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        CreateLabel(rect, "Label", text, rect.sizeDelta, Vector2.zero);
        return button;
    }
}
