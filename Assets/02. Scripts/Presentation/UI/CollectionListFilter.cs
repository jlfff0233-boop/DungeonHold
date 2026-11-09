using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CollectionListFilter : MonoBehaviour
{
    [Serializable]
    private sealed class FilterOption
    {
        public string label;
        public GameObject[] entries;
    }

    [SerializeField] private Button _iconButton;
    [SerializeField] private TMP_Text _caption;
    [SerializeField] private ScrollRect _enemyList;
    [SerializeField] private RectTransform _bookImage;
    [SerializeField] private RectTransform _normalTab;
    [SerializeField] private RectTransform _bossTab;
    [SerializeField] private RectTransform _tabHighlight;
    [SerializeField] private FilterOption[] _options = Array.Empty<FilterOption>();

    private const string AllLabel = "전부";
    private const int Normal = 1;
    private const int Boss = 2;
    private const int All = Normal | Boss;

    private GameObject _menu;
    private RectTransform _bossHighlight;
    private int _visibleTypes;

    private void OnEnable()
    {
        if (_iconButton == null || _caption == null || _enemyList == null ||
            _enemyList.content == null || _options.Length != 2)
        {
            Debug.LogError("Collection filter requires Icon, Caption, Enemy List, and filter options.", this);
            return;
        }

        if (_menu == null)
        {
            CreateMenu();
            SetVisibleTypes(All);
        }

        _iconButton.onClick.AddListener(ToggleMenu);
    }

    private void OnDisable()
    {
        if (_iconButton != null)
            _iconButton.onClick.RemoveListener(ToggleMenu);

        if (_menu != null)
            _menu.SetActive(false);
    }

    private void ToggleMenu()
    {
        bool open = !_menu.activeSelf;
        if (open)
        {
            transform.SetAsLastSibling();
            _menu.transform.SetAsLastSibling();
        }

        _menu.SetActive(open);
    }

    private void CreateMenu()
    {
        const float rowHeight = 42f;
        _menu = new GameObject("FilterOptions", typeof(RectTransform));
        _menu.layer = gameObject.layer;
        _menu.SetActive(false);
        var menuRect = (RectTransform)_menu.transform;
        menuRect.SetParent(transform, false);
        menuRect.anchorMin = Vector2.zero;
        menuRect.anchorMax = Vector2.right;
        menuRect.pivot = Vector2.up;
        menuRect.anchoredPosition = new Vector2(0f, -4f);
        menuRect.sizeDelta = new Vector2(0f, (_options.Length + 1) * rowHeight);

        for (int row = 0; row <= _options.Length; row++)
        {
            int index = row - 1;
            string label = index < 0 ? AllLabel : _options[index].label;
            var item = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            item.layer = gameObject.layer;
            var rect = (RectTransform)item.transform;
            rect.SetParent(menuRect, false);
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(0f, -row * rowHeight);
            rect.sizeDelta = new Vector2(0f, rowHeight);

            var background = item.GetComponent<Image>();
            background.color = new Color32(229, 198, 146, 255);
            var button = item.GetComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() => SetVisibleTypes(index < 0 ? All : 1 << index));

            var text = Instantiate(_caption, rect, false);
            text.name = "Label";
            text.text = label;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(14f, 0f);
            text.rectTransform.offsetMax = new Vector2(-14f, 0f);
        }
    }

    private void SetVisibleTypes(int visibleTypes)
    {
        if (visibleTypes != _visibleTypes)
        {
            _visibleTypes = visibleTypes;
            _caption.text = visibleTypes switch
            {
                All => AllLabel,
                Normal => _options[0].label,
                Boss => _options[1].label,
                _ => "선택 없음"
            };

            bool showNormal = (visibleTypes & Normal) != 0;
            bool showBoss = (visibleTypes & Boss) != 0;
            foreach (Transform entry in _enemyList.content)
            {
                bool visible = visibleTypes == All ||
                    (showNormal && Array.IndexOf(_options[0].entries, entry.gameObject) >= 0) ||
                    (showBoss && Array.IndexOf(_options[1].entries, entry.gameObject) >= 0);
                entry.gameObject.SetActive(visible);
            }

            UpdateTabs(showNormal, showBoss);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_enemyList.content);
            _enemyList.StopMovement();
            _enemyList.verticalNormalizedPosition = 1f;
        }

        _menu.SetActive(false);
    }

    private void UpdateTabs(bool showNormal, bool showBoss)
    {
        if (_bookImage == null || _normalTab == null || _bossTab == null || _tabHighlight == null)
            return;

        if (_bossHighlight == null)
        {
            _bossHighlight = Instantiate(_tabHighlight, _tabHighlight.parent, false);
            _bossHighlight.name = "BossTabHighlight";
            _bossHighlight.anchoredPosition += _bossTab.anchoredPosition - _normalTab.anchoredPosition;
            var label = _bossHighlight.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = "보스";

            BindTabButton(_normalTab, Normal);
            BindTabButton(_tabHighlight, Normal);
            BindTabButton(_bossTab, Boss);
            BindTabButton(_bossHighlight, Boss);
        }

        SetTabLayer(_normalTab, _tabHighlight, showNormal);
        SetTabLayer(_bossTab, _bossHighlight, showBoss);
    }

    private void BindTabButton(RectTransform tab, int type)
    {
        if (!tab.TryGetComponent(out Button button))
        {
            button = tab.gameObject.AddComponent<Button>();
            button.targetGraphic = tab.GetComponent<Graphic>();
            button.transition = Selectable.Transition.None;
        }

        button.onClick.AddListener(() =>
        {
            if (isActiveAndEnabled)
                SetVisibleTypes(_visibleTypes ^ type);
        });
    }

    private void SetTabLayer(RectTransform tab, RectTransform highlight, bool selected)
    {
        if (selected)
        {
            tab.SetAsLastSibling();
            highlight.SetAsLastSibling();
        }
        else
        {
            int pageIndex = _bookImage.GetSiblingIndex();
            if (tab.GetSiblingIndex() < pageIndex)
                pageIndex--;
            tab.SetSiblingIndex(pageIndex);
        }

        highlight.gameObject.SetActive(selected);
    }
}
