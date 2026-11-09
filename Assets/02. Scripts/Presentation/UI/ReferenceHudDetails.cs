using LJPlay;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Presentation only. The existing quick-slot and combat controllers own all actions.
[DefaultExecutionOrder(450)]
public class ReferenceHudDetails : MonoBehaviour
{
    private readonly TrapData[] _installations = new TrapData[5];
    [SerializeField] private Sprite[] _statusIcons = new Sprite[3];
    [SerializeField] private Sprite _keyboardFrame;
    private PlayScene _scene;
    private BuildPreviewController _preview;
    private BuildPhaseGate _phaseGate;
    private InputHandler _input;
    private InputHandler _boundInput;
    private int _selectedSlotIndex = -1;
    private int _observedStageId;
    private readonly List<int> _pendingSlots = new List<int>();
    private TMP_Text _health, _energy;
    private readonly TMP_Text[] _costs = new TMP_Text[5];
    [SerializeField, Min(0f)] private float _installationFeeIconGap = 4f;
    private readonly List<FeeIconBinding> _feeIcons = new List<FeeIconBinding>();

    private sealed class FeeIconBinding
    {
        public TMP_Text Text;
        public RectTransform Icon;
        public bool OnLeft;
    }
    private readonly GameObject[] _selections = new GameObject[5];
    private Image _installationFeedback;
    private Sprite _cancelIcon, _rotateIcon;
    private TrapData _feedbackTrap;
    private int _feedbackRotation, _feedbackStage;
    private float _rotateVisibleUntil;
    private bool _feedbackWasActive;

    public bool HandlesInstallationFeedback => isActiveAndEnabled &&
        _installationFeedback != null && _cancelIcon != null;

    private void OnEnable() => ResolveScene();

    private void OnDisable()
    {
        if (_boundInput != null) _boundInput.OnQuickSlotInput -= ObserveSlotInput;
        _boundInput = null;
        _pendingSlots.Clear();
        ResetInstallationFeedback();
    }

    private void Start()
    {
        foreach (TMP_Text amount in GetComponentsInChildren<TMP_Text>(true))
        {
            if (amount.name != "CurrencyText" && !amount.name.StartsWith("InstallationFee")) continue;
            // Monetary digits keep their normal advance, regardless of the pair.
            var features = new List<UnityEngine.TextCore.OTL_FeatureTag>(amount.fontFeatures);
            features.Remove(UnityEngine.TextCore.OTL_FeatureTag.kern);
            amount.fontFeatures = features;
        }
        CreateInstallationFeedback();
        Transform player = Find(transform, "PlayerStatus");
        if (player == null) return;
        TMP_Text sample = player.Find("NameText").GetComponent<TMP_Text>();
        _health = Label(player.Find("HealthFrame"), "HealthValue", sample.font, Vector2.zero,
            new Vector2(220, 24), new Vector2(0.5f, 0.5f), 20);
        _energy = Label(player.Find("EnergyFrame"), "EnergyValue", sample.font, Vector2.zero,
            new Vector2(190, 22), new Vector2(0.5f, 0.5f), 18);
        Transform bar = player.Find("Loadout/ItemBarRoot");
        for (int i = 0; i < 5; i++)
        {
            Transform cell = bar.Find(i == 0 ? "WeaponSlots" : "InstallationSlots" + (i == 1 ? "" : " (" + (i - 1) + ")"));
            if (cell == null) continue;
            Transform selected = cell.Find("Selection");
            _selections[i] = selected != null ? selected.gameObject : null;
            foreach (TMP_Text label in cell.GetComponentsInChildren<TMP_Text>(true))
                if (label.name.StartsWith("InstallationFee")) _costs[i] = label;
            if (_costs[i] == null)
                _costs[i] = Label(cell, "InstallationFee", sample.font, new Vector2(0, -98),
                    new Vector2(94, 26), new Vector2(0, 1), 22);
        }
        // Prices share a single-line policy across installation and support slots.
        foreach (TMP_Text price in player.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!price.name.StartsWith("InstallationFee")) continue;
            price.enableWordWrapping = false;
            price.enableAutoSizing = false;
            price.characterWidthAdjustment = 0f;
            price.overflowMode = TextOverflowModes.Overflow;
            Transform icon = price.transform.Find("Icon");
            if (icon == null && price.transform.parent != null &&
                price.transform.parent.name.StartsWith("InstallationFee"))
                icon = price.transform.parent.Find("Icon");
            if (icon is RectTransform iconRect)
            {
                Vector3 center = price.transform.InverseTransformPoint(
                    iconRect.TransformPoint(iconRect.rect.center));
                _feeIcons.Add(new FeeIconBinding
                {
                    Text = price,
                    Icon = iconRect,
                    OnLeft = center.x < price.rectTransform.rect.center.x
                });
            }
        }
        ResolveScene();
    }

    private void CreateInstallationFeedback()
    {
        // Reuse sprites already referenced by the HUD, including its hidden hints.
        // No asset loading, prefab edits, or gameplay input execution is needed.
        foreach (Image image in GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null) continue;
            if (image.sprite.name == "icon_cancel") _cancelIcon = image.sprite;
            if (image.sprite.name == "icon_rotate") _rotateIcon = image.sprite;
        }
        RectTransform rect = Rect(transform, "InstallationFeedback", Vector2.zero,
            new Vector2(64, 64), new Vector2(0.5f, 0.5f));
        _installationFeedback = rect.gameObject.AddComponent<Image>();
        _installationFeedback.preserveAspect = true;
        _installationFeedback.raycastTarget = false;
        _installationFeedback.gameObject.SetActive(false);
    }

    private void ResetInstallationFeedback()
    {
        _feedbackWasActive = false;
        _feedbackTrap = null;
        _rotateVisibleUntil = 0f;
        if (_installationFeedback != null) _installationFeedback.gameObject.SetActive(false);
    }

    private void RefreshInstallationFeedback()
    {
        if (_installationFeedback == null) return;
        bool active = CanSelectInstallation() && _preview != null &&
            _preview.isActiveAndEnabled && _preview.HasSelection &&
            Application.isFocused && Time.timeScale > 0f;
        if (!active)
        {
            ResetInstallationFeedback();
            return;
        }

        int stage = _scene.CurrentStageId;
        TrapData trap = _preview.CurrentTrapData;
        int rotation = _preview.RotationDegrees;
        bool sameSelection = _feedbackWasActive && _feedbackStage == stage && _feedbackTrap == trap;
        if (!sameSelection) _rotateVisibleUntil = 0f;
        // Checking the actual angle as well as R prevents selection resets from
        // being shown as rotations. This never forwards or repeats the R input.
        if (sameSelection && rotation != _feedbackRotation && Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
            _rotateVisibleUntil = Time.unscaledTime + 0.4f;

        _feedbackWasActive = true;
        _feedbackStage = stage;
        _feedbackTrap = trap;
        _feedbackRotation = rotation;

        Sprite icon = Time.unscaledTime < _rotateVisibleUntil ? _rotateIcon :
            (!_preview.HasCell || !_preview.IsAreaAvailable ? _cancelIcon : null);
        _installationFeedback.sprite = icon;
        _installationFeedback.gameObject.SetActive(icon != null);
    }

    private void ResolveScene()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (_scene == null)
                foreach (PlayScene candidate in root.GetComponentsInChildren<PlayScene>(true))
                    if (candidate.isActiveAndEnabled) { _scene = candidate; break; }
            if (_preview == null) _preview = root.GetComponentInChildren<BuildPreviewController>(true);
            if (_phaseGate == null) _phaseGate = root.GetComponentInChildren<BuildPhaseGate>(true);
            if (_input == null) _input = root.GetComponentInChildren<InputHandler>(true);
        }
        if (_boundInput == _input) return;
        if (_boundInput != null) _boundInput.OnQuickSlotInput -= ObserveSlotInput;
        _boundInput = _input;
        if (_boundInput != null) _boundInput.OnQuickSlotInput += ObserveSlotInput;
    }

    private void ObserveSlotInput(int index)
    {
        // Observe the existing request only; never execute the slot a second time.
        // Its final preview state is inspected in LateUpdate, after every subscriber.
        if (index >= 0 && index < _selections.Length && CanSelectInstallation())
            _pendingSlots.Add(index);
    }

    private bool CanSelectInstallation() => _scene != null && _scene.isActiveAndEnabled &&
        _scene.IsInitialized && _scene.IsStageReady && !_scene.IsEndingStage &&
        _phaseGate != null && _phaseGate.CanBuild && _input != null && _input.isActiveAndEnabled;

    private void RefreshSelectedSlot()
    {
        int stage = _scene != null && _scene.IsInitialized ? _scene.CurrentStageId : 0;
        if (stage != _observedStageId)
        {
            _observedStageId = stage;
            _selectedSlotIndex = -1;
        }
        if (_preview == null || !_preview.isActiveAndEnabled || !_preview.HasSelection)
            _selectedSlotIndex = -1;
        else
        {
            if (_selectedSlotIndex >= 0 && (_selectedSlotIndex >= _installations.Length ||
                _installations[_selectedSlotIndex] != _preview.CurrentTrapData))
                _selectedSlotIndex = -1;
            if (CanSelectInstallation())
                for (int i = _pendingSlots.Count - 1; i >= 0; i--)
                {
                    int index = _pendingSlots[i];
                    TrapData data = index < _installations.Length ? _installations[index] : null;
                    if (data == null || !data.IsValid || data != _preview.CurrentTrapData) continue;
                    _selectedSlotIndex = index;
                    break;
                }
        }
        _pendingSlots.Clear();
    }

    private void LateUpdate()
    {
        if (_scene == null || !_scene.isActiveAndEnabled || _preview == null || _input == null || _phaseGate == null || _boundInput != _input)
        {
            if (_scene != null && !_scene.isActiveAndEnabled) _scene = null;
            ResolveScene();
        }
        // Prices and selection feedback use the same registrations as gameplay.
        for (int i = 0; i < _installations.Length; i++)
        {
            _installations[i] = null;
            if (_scene != null && _scene.TryGetQuickSlot(i, out QuickSlotData slot) &&
                slot is InstallationQuickSlotData installation)
                _installations[i] = installation.Installation;
        }
        RefreshSelectedSlot();
        RefreshInstallationFeedback();
        Hero hero = _scene != null && _scene.IsInitialized ? _scene.Hero : null;
        Health health = hero != null ? hero.GetComponent<Health>() : null;
        Energy energy = hero != null ? hero.GetComponentInChildren<Energy>(true) : null;
        if (_health != null) _health.text = health != null ? $"{health.CurrentHp:0} / {health.MaxHp:0}" : "";
        if (_energy != null) _energy.text = energy != null ? $"{energy.CurrentEnergy:0} / {energy.MaxEnergy:0}" : "";
        for (int i = 0; i < 5; i++)
        {
            TrapData data = i < _installations.Length ? _installations[i] : null;
            bool selected = data != null && _preview != null && _preview.isActiveAndEnabled &&
                _preview.HasSelection && i == _selectedSlotIndex && _preview.CurrentTrapData == data;
            if (_selections[i] != null) _selections[i].SetActive(selected);
            if (_costs[i] == null) continue;
            _costs[i].text = data != null ? data.PlacementCost.ToString("N0") : "";
            bool affordable = data != null && _scene != null && _scene.Currency != null &&
                _scene.Currency.CanSpend(CurrencyType.Stage, data.PlacementCost);
            _costs[i].color = affordable ? new Color32(77, 232, 209, 255) : new Color32(232, 90, 90, 255);
        }
        RefreshFeeIconPositions();
    }

    private void RefreshFeeIconPositions()
    {
        foreach (FeeIconBinding binding in _feeIcons)
        {
            TMP_Text text = binding.Text;
            RectTransform icon = binding.Icon;
            if (text == null || icon == null || !text.isActiveAndEnabled ||
                string.IsNullOrEmpty(text.text)) continue;
            text.ForceMeshUpdate();
            if (text.textInfo.characterCount == 0) continue;
            Bounds bounds = text.textBounds;
            // Keep the authored side and vertical position; follow the visible text edge.
            float iconEdge = binding.OnLeft ? icon.rect.xMax : icon.rect.xMin;
            Vector3 edge = text.transform.InverseTransformPoint(
                icon.TransformPoint(new Vector3(iconEdge, icon.rect.center.y, 0f)));
            float target = binding.OnLeft
                ? bounds.min.x - _installationFeeIconGap
                : bounds.max.x + _installationFeeIconGap;
            icon.position += text.transform.TransformVector(new Vector3(target - edge.x, 0f, 0f));
        }
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }

    private static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size, Vector2 anchor)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, Vector2 position,
        Vector2 size, Vector2 anchor, float fontSize)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out TMP_Text authored)) return authored;
        TMP_Text label = Rect(parent, name, position, size, anchor).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        return label;
    }
}
