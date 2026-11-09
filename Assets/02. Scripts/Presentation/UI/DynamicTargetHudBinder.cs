using System;
using System.Collections.Generic;
using LJTest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(300)]
public class DynamicTargetHudBinder : MonoBehaviour
{
    [SerializeField] private GameObject _enemyHealthPrefab;
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private Texture2D _burnIcon;
    [SerializeField] private Texture2D _slowIcon;
    [SerializeField] private Texture2D _stunIcon;
    [SerializeField, Min(0.05f)] private float _scanSeconds = 0.2f;

    private sealed class TargetBinding
    {
        public Health Health;
        public StatusEffectController Effects;
        public WorldHealthView View;
        public Image Background;
        public Image Fill;
        private readonly HealthFillTween _fillTween = new HealthFillTween();
        public TMP_Text Value;
        public GameObject HealthRoot;
        public Image[] Indicators;
        public bool CenterIndicators;
        private bool _hasIndicatorLayout;
        private float _indicatorCenter;
        private float _indicatorSpacing;
        public Action<float, float> HealthHandler;
        public Action<StatusEffectType> EffectHandler;

        public void Bind(Health health, StatusEffectController effects)
        {
            if (Health == health && Effects == effects) return;
            Unbind();
            Health = health;
            Effects = effects;
            HealthHandler = (current, max) => Refresh();
            EffectHandler = type => Refresh();
            if (Health != null) Health.OnHpChanged += HealthHandler;
            if (Effects != null) Effects.OnEffectChanged += EffectHandler;
            Refresh();
        }

        public void Refresh()
        {
            bool valid = Health != null && Health.isActiveAndEnabled && Health.gameObject.activeInHierarchy;
            bool alive = valid && Health.IsAlive;
            if (View != null)
            {
                if (valid) View.SetHealth(Health.CurrentHp, Health.MaxHp);
                else View.ResetView();
            }
            // WorldHealthView owns this same fill when present; do not overwrite its tween.
            if (View == null)
                _fillTween.Set(Fill, valid && Health.MaxHp > 0f ? Health.CurrentHp / Health.MaxHp : 0f);
            if (Background != null) Background.enabled = alive;
            if (Fill != null) Fill.enabled = alive;
            if (Value != null) Value.text = valid ? $"{Health.CurrentHp:0} / {Health.MaxHp:0}" : "—";
            if (HealthRoot != null) HealthRoot.SetActive(alive);
            StatusEffectType[] types = { StatusEffectType.Burn, StatusEffectType.Slow, StatusEffectType.Stun };
            for (int i = 0; i < Indicators.Length; i++)
                if (Indicators[i] != null)
                    Indicators[i].gameObject.SetActive(alive && Effects != null && Effects.isActiveAndEnabled && Effects.HasEffect(types[i]));
            if (CenterIndicators) LayoutIndicators();
        }

        private void LayoutIndicators()
        {
            if (!_hasIndicatorLayout)
            {
                float left = float.PositiveInfinity, right = float.NegativeInfinity;
                int total = 0;
                foreach (Image indicator in Indicators)
                {
                    if (indicator == null) continue;
                    float x = indicator.rectTransform.anchoredPosition.x;
                    left = Mathf.Min(left, x);
                    right = Mathf.Max(right, x);
                    total++;
                }
                if (total == 0) return;
                _indicatorCenter = (left + right) * 0.5f;
                _indicatorSpacing = total > 1 ? (right - left) / (total - 1) : 0f;
                _hasIndicatorLayout = true;
            }

            int visible = 0;
            foreach (Image indicator in Indicators)
                if (indicator != null && indicator.gameObject.activeSelf) visible++;
            int index = 0;
            foreach (Image indicator in Indicators)
            {
                if (indicator == null || !indicator.gameObject.activeSelf) continue;
                Vector2 position = indicator.rectTransform.anchoredPosition;
                position.x = _indicatorCenter + (index++ - (visible - 1) * 0.5f) * _indicatorSpacing;
                indicator.rectTransform.anchoredPosition = position;
            }
        }

        public void Unbind()
        {
            _fillTween.Reset();
            if (Health != null && HealthHandler != null) Health.OnHpChanged -= HealthHandler;
            if (Effects != null && EffectHandler != null) Effects.OnEffectChanged -= EffectHandler;
            Health = null;
            Effects = null;
            if (View != null) View.ResetView();
            if (Fill != null) Fill.fillAmount = 0;
            if (Value != null) Value.text = "";
            if (Indicators != null)
                foreach (Image image in Indicators) if (image != null) image.gameObject.SetActive(false);
        }
    }

    private sealed class EnemyBinding
    {
        public EnemyCombat Enemy;
        public long LifeId;
        public GameObject Root;
        public TargetBinding Binding;
        public Renderer Renderer;
        public Collider Collider;
    }

    private sealed class CompanionSlot
    {
        public GameObject Root;
        public TargetBinding Binding;
    }

    private TestScene _scene;
    private CompanionHireController _hire;
    [SerializeField] private GameObject _hud;
    private PlayerWeaponSkillHudBinder _weaponHud;
    private GameObject _enemyUiRoot;
    private Camera _camera;
    private int _stageId;
    private double _nextScan;
    private readonly Dictionary<int, EnemyBinding> _enemies = new Dictionary<int, EnemyBinding>();
    private readonly List<int> _remove = new List<int>();
    private readonly List<CompanionSlot> _slots = new List<CompanionSlot>();
    private readonly List<Behaviour> _legacyCompanionBindings = new List<Behaviour>();
    private readonly Dictionary<Texture2D, Sprite> _sprites = new Dictionary<Texture2D, Sprite>();

    private void Start()
    {
        BindActiveHud();
        _enemyUiRoot = new GameObject("DynamicEnemyHealthUI");
        _enemyUiRoot.transform.SetParent(transform, false);
        BindScene();
    }

    private void BindActiveHud()
    {
        GameObject activeHud = _hud != null && _hud.activeInHierarchy ? _hud : null;
        if (activeHud == null)
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (canvas.gameObject.activeInHierarchy &&
                        canvas.name.StartsWith("Canvas_HUD", StringComparison.Ordinal) &&
                        Find(canvas.transform, "CampanionStatusRoot") != null)
                    { activeHud = canvas.gameObject; break; }
                if (activeHud != null) break;
            }
        if (activeHud == null || (activeHud == _hud && _slots.Count > 0)) return;

        foreach (CompanionSlot slot in _slots)
        {
            slot.Binding.Unbind();
            if (slot.Root != null) slot.Root.SetActive(false);
        }
        _slots.Clear();
        foreach (Behaviour binder in _legacyCompanionBindings) if (binder != null) binder.enabled = true;
        _legacyCompanionBindings.Clear();
        _hud = activeHud;
        _weaponHud = _hud.GetComponent<PlayerWeaponSkillHudBinder>();
        BuildCompanionSlots();
    }

    private void OnEnable()
    {
        _nextScan = 0;
        if (_enemyUiRoot != null) BindScene();
        foreach (Behaviour binder in _legacyCompanionBindings) if (binder != null) binder.enabled = false;
    }

    private void BindScene()
    {
        if (_scene == null)
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                TestScene candidate = root.GetComponentInChildren<TestScene>(true);
                if (candidate != null && candidate.isActiveAndEnabled) { _scene = candidate; break; }
            }
        CompanionHireController hire = _scene != null && _scene.IsInitialized ? _scene.HireController : null;
        if (hire == _hire) return;
        if (_hire != null) _hire.OnEntryChanged -= HandleEntryChanged;
        _hire = hire;
        if (_hire != null) _hire.OnEntryChanged += HandleEntryChanged;
        ClearTargets();
    }

    private void LateUpdate()
    {
        if (_enemyUiRoot == null) return;
        if (_hud == null || !_hud.activeInHierarchy || _slots.Count == 0) BindActiveHud();
        BindScene();
        int stage = _scene != null && _scene.isActiveAndEnabled && _scene.IsInitialized && _scene.IsStageReady &&
            !_scene.IsEndingStage && _scene.Rewards != null ? _scene.Rewards.CurrentStageId : 0;
        if (_stageId != stage) { ClearTargets(); _stageId = stage; _nextScan = 0; }
        bool visible = _hud != null && _hud.activeInHierarchy && stage != 0;
        if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
        if (stage != 0 && Time.unscaledTimeAsDouble >= _nextScan)
        {
            ScanEnemies();
            _nextScan = Time.unscaledTimeAsDouble + _scanSeconds;
        }
        RefreshEnemies(visible);
        RefreshCompanions();
    }

    private void ScanEnemies()
    {
        if (_enemyHealthPrefab == null || _hud == null) return;
        foreach (EnemyCombat enemy in FindObjectsByType<EnemyCombat>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (enemy.gameObject.scene != gameObject.scene || !enemy.CanReceiveDamage || enemy.RewardStageId != _stageId) continue;
            int key = enemy.GetInstanceID();
            if (_enemies.ContainsKey(key)) continue;
            Health health = enemy.GetComponent<Health>();
            if (health == null || !health.IsAlive) continue;
            GameObject ui = Instantiate(_enemyHealthPrefab, _enemyUiRoot.transform);
            ui.name = "EnemyHealth_" + key;
            // Disable the unbound template updater only on this UI clone.
            foreach (TargetHealthHudBinder legacy in ui.GetComponentsInChildren<TargetHealthHudBinder>(true)) legacy.enabled = false;
            foreach (GraphicRaycaster raycaster in ui.GetComponentsInChildren<GraphicRaycaster>(true)) raycaster.enabled = false;
            foreach (Graphic graphic in ui.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            WorldHealthView view = ui.GetComponentInChildren<WorldHealthView>(true);
            Transform frame = Find(ui.transform, "HpBarFrame");
            Image background = frame != null ? frame.GetComponent<Image>() : null;
            Image fill = GetImage(ui.transform, "HpBar");
            // Preserve the authored bar_Background and Hp_Bar sprites. Both belong
            // to the same visibility root; only Hp_Bar is filled by health ratio.
            if (background != null)
            {
                background.type = Image.Type.Simple;
                background.color = Color.white;
            }
            if (fill != null)
            {
                fill.rectTransform.anchorMin = Vector2.zero;
                fill.rectTransform.anchorMax = Vector2.one;
                fill.rectTransform.offsetMin = new Vector2(2f, 2f);
                fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
                fill.gameObject.SetActive(true);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.fillOrigin = 0;
            }
            TargetBinding binding = new TargetBinding { View = view, HealthRoot = frame != null ? frame.gameObject : null,
                Background = background, Fill = fill,
                Value = MakeText(ui.transform, "TargetHealthValue", new Vector2(0, 24), new Vector2(160, 22), 14),
                CenterIndicators = true,
                Indicators = new[] { GetImage(ui.transform, "BurnIndicator"), GetImage(ui.transform, "SlowIndicator"), GetImage(ui.transform, "StunIndicator") } };
            StyleIndicators(binding.Indicators);
            binding.Bind(health, enemy.GetComponent<StatusEffectController>());
            _enemies.Add(key, new EnemyBinding { Enemy = enemy, LifeId = enemy.RewardLifeId, Root = ui, Binding = binding,
                Renderer = enemy.GetComponentInChildren<Renderer>(), Collider = enemy.GetComponent<Collider>() });
        }
    }

    private void RefreshEnemies(bool hudVisible)
    {
        _remove.Clear();
        foreach (var pair in _enemies)
        {
            EnemyBinding target = pair.Value;
            if (target.Root == null || target.Enemy == null || !target.Enemy.CanReceiveDamage || target.Enemy.RewardStageId != _stageId ||
                target.Enemy.RewardLifeId != target.LifeId || target.Binding.Health == null || !target.Binding.Health.IsAlive)
            { _remove.Add(pair.Key); continue; }
            Vector3 position = target.Enemy.transform.position + Vector3.up * 2.2f;
            if (target.Collider != null) position = new Vector3(target.Collider.bounds.center.x, target.Collider.bounds.max.y + 0.3f, target.Collider.bounds.center.z);
            else if (target.Renderer != null) position = new Vector3(target.Renderer.bounds.center.x, target.Renderer.bounds.max.y + 0.3f, target.Renderer.bounds.center.z);
            bool onscreen = false;
            if (_camera != null)
            {
                Vector3 viewport = _camera.WorldToViewportPoint(position);
                onscreen = viewport.z > 0 && viewport.x >= -0.05f && viewport.x <= 1.05f && viewport.y >= -0.05f && viewport.y <= 1.05f;
                target.Root.transform.SetPositionAndRotation(position, _camera.transform.rotation);
            }
            target.Root.SetActive(hudVisible && onscreen);
            target.Binding.Refresh();
        }
        foreach (int key in _remove) RemoveEnemy(key);
    }

    private void RemoveEnemy(int key)
    {
        EnemyBinding target = _enemies[key];
        target.Binding.Unbind();
        if (target.Root != null) { target.Root.SetActive(false); Destroy(target.Root); }
        _enemies.Remove(key);
    }

    private void BuildCompanionSlots()
    {
        Transform container = Find(_hud.transform, "CampanionStatusRoot");
        if (container == null) return;
        Transform legacyRoot = Find(_hud.transform, "CompanionHudBindings");
        if (legacyRoot != null)
            foreach (TargetHealthHudBinder binder in legacyRoot.GetComponentsInChildren<TargetHealthHudBinder>(true))
                if (binder.enabled) { _legacyCompanionBindings.Add(binder); binder.enabled = false; }
        for (int i = 0; i < container.childCount; i++)
        {
            Transform row = container.GetChild(i);
            if (!row.name.StartsWith("CampanionRow", StringComparison.Ordinal)) continue;
            if (_slots.Count >= 2) { row.gameObject.SetActive(false); continue; }
            Transform energy = Find(row, "EnergyBackground");
            if (energy != null) energy.gameObject.SetActive(false); // Companion exposes no energy resource.
            Image[] indicators = new Image[3];
            for (int icon = 0; icon < 3; icon++)
                indicators[icon] = MakeImage(row, "BoundStatus" + icon, new Vector2(126 + icon * 32, -112), new Vector2(22, 22));
            StyleIndicators(indicators);
            TargetBinding binding = new TargetBinding { View = row.GetComponent<WorldHealthView>(), Fill = GetImage(row, "HealthFill") ?? GetImage(row, "Health"),
                Value = MakeText(row, "BoundHealth", new Vector2(206, -86), new Vector2(174, 20), 18), Indicators = indicators };
            _slots.Add(new CompanionSlot { Root = row.gameObject, Binding = binding });
            row.gameObject.SetActive(false);
        }
    }

    private void HandleEntryChanged(CompanionHireEntry entry) => RefreshCompanions();

    private void RefreshCompanions()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            CompanionSlot slot = _slots[i];
            if (slot.Root == null) continue;
            CompanionHireEntry entry = null;
            bool exists = _stageId != 0 && _hire != null && _hire.TryGetEntryAt(i, out entry);
            slot.Root.SetActive(exists);
            if (!exists) { slot.Binding.Unbind(); continue; }
            Companion companion = entry.Instance;
            Health health = companion != null && companion.IsInitialized ? companion.GetComponent<Health>() : null;
            StatusEffectController effects = companion != null ? companion.GetComponent<StatusEffectController>() : null;
            slot.Binding.Bind(health, effects);
            slot.Binding.Refresh();
            WeaponType type = entry.Data.CompanionData.WeaponType;
            if (_weaponHud != null)
            {
                _weaponHud.ShowWeaponBadge(slot.Root.transform.Find("Portrait"), type, 48f);
                _weaponHud.ShowAttackEffects(slot.Root.transform, entry.Data.CompanionData);
            }
        }
    }

    private void ClearTargets()
    {
        foreach (EnemyBinding enemy in _enemies.Values)
        {
            enemy.Binding.Unbind();
            if (enemy.Root != null) { enemy.Root.SetActive(false); Destroy(enemy.Root); }
        }
        _enemies.Clear();
        foreach (CompanionSlot slot in _slots) { slot.Binding.Unbind(); if (slot.Root != null) slot.Root.SetActive(false); }
    }

    private void OnDisable()
    {
        if (_hire != null) _hire.OnEntryChanged -= HandleEntryChanged;
        _hire = null;
        _stageId = 0;
        ClearTargets();
        foreach (Behaviour binder in _legacyCompanionBindings) if (binder != null) binder.enabled = true;
    }

    private void OnDestroy()
    {
        ClearTargets();
        if (_enemyUiRoot != null) Destroy(_enemyUiRoot);
        foreach (Sprite sprite in _sprites.Values) if (sprite != null) Destroy(sprite);
    }

    private void StyleIndicators(Image[] images)
    {
        Texture2D[] textures = { _burnIcon, _slowIcon, _stunIcon };
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null) continue;
            Texture2D texture = textures[i];
            Sprite sprite = null;
            if (texture != null && !_sprites.TryGetValue(texture, out sprite))
            {
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                _sprites.Add(texture, sprite);
            }
            images[i].sprite = sprite;
            images[i].enabled = sprite != null;
            images[i].preserveAspect = true;
            images[i].color = Color.white;
        }
    }

    private TMP_Text MakeText(Transform parent, string objectName, Vector2 position, Vector2 size, float fontSize)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null && existing.TryGetComponent(out TMP_Text authored)) return authored;
        RectTransform rect = MakeRect(parent, objectName, position, size);
        TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font; text.fontSize = fontSize; text.enableAutoSizing = true;
        text.fontSizeMin = 10; text.fontSizeMax = fontSize; text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
        return text;
    }

    private Image MakeImage(Transform parent, string objectName, Vector2 position, Vector2 size)
    {
        Image image = MakeRect(parent, objectName, position, size).gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform MakeRect(Transform parent, string objectName, Vector2 position, Vector2 size)
    {
        RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = parent.name.StartsWith("CampanionRow", StringComparison.Ordinal) ? new Vector2(0, 1) : new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    private static Transform Find(Transform parent, string objectName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true)) if (child.name == objectName) return child;
        return null;
    }

    private static Image GetImage(Transform parent, string objectName)
    {
        Transform child = Find(parent, objectName);
        return child != null ? child.GetComponent<Image>() : null;
    }
}
