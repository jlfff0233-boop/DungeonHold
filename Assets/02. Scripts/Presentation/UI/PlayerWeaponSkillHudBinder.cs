using System;
using System.Collections.Generic;
using LJTest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Only binds the existing HUD. It never initializes or executes combat modules.
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public class PlayerWeaponSkillHudBinder : MonoBehaviour
{
    [Serializable]
    private sealed class WeaponArtwork
    {
        public WeaponType Type;
        public Texture2D Weapon;
        public Texture2D Skill;
        public Texture2D Ultimate;
    }

    [SerializeField] private WeaponArtwork[] _artwork = Array.Empty<WeaponArtwork>();
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private Sprite _readyFrame;
    [SerializeField] private Sprite _idleFrame;
    [SerializeField] private Sprite _readyEffect;
    [SerializeField] private Texture2D _weaponFrame;
    [Serializable]
    private sealed class AttackEffectLoadout
    {
        public ScriptableObject Source;
        public StatusEffectData[] Effects = Array.Empty<StatusEffectData>();
    }
    [SerializeField] private AttackEffectLoadout[] _attackEffectLoadouts = Array.Empty<AttackEffectLoadout>();
    [SerializeField] private Sprite[] _attackEffectIcons = new Sprite[3];

    private TestScene _scene;
    private Hero _hero;
    private Image _skillIcon;
    private TMP_Text _skillText;
    private Image _ultimateIcon;
    private Transform _playerPortrait;
    private readonly Dictionary<Transform, Image> _weaponBadges = new Dictionary<Transform, Image>();
    private TMP_Text _ultimateText;
    private Image _skillFrame, _ultimateFrame, _skillEffect, _ultimateEffect;
    private readonly Dictionary<Texture2D, Sprite> _sprites = new Dictionary<Texture2D, Sprite>();
    private readonly List<Behaviour> _replacedBinders = new List<Behaviour>();

    private void OnEnable()
    {
        ResolveUI();
        // These UI-only legacy binders would otherwise write the same two slots.
        foreach (PlayerSkillHudBinder binder in GetComponentsInChildren<PlayerSkillHudBinder>(true))
            Suspend(binder);
        foreach (PlayerUltimateHudBinder binder in GetComponentsInChildren<PlayerUltimateHudBinder>(true))
            Suspend(binder);
        Refresh();
    }

    private void Suspend(Behaviour binder)
    {
        if (!binder.enabled) return;
        _replacedBinders.Add(binder);
        binder.enabled = false;
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (_scene == null)
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                _scene = root.GetComponentInChildren<TestScene>(true);
                if (_scene != null) break;
            }

        Hero selected = _scene != null && _scene.isActiveAndEnabled && _scene.IsInitialized
            ? _scene.Hero : null;
        _hero = selected;
        // WeaponData's getter depends on Hero initialization; do not query an uninitialized Hero.
        WeaponData weapon = _hero != null ? _hero.WeaponData : null;
        if (weapon == null)
        {
            Clear("");
            return;
        }
        WeaponArtwork art = Array.Find(_artwork, entry => entry != null && entry.Type == weapon.WeaponType);
        ShowWeaponBadge(_playerPortrait, weapon.WeaponType, 64f);
        if (_playerPortrait != null) ShowAttackEffects(_playerPortrait.parent, weapon);
        bool active = _scene.IsStageReady && !_scene.IsEndingStage && _hero.isActiveAndEnabled;
        DrawSkill(_hero.SkillCaster, weapon.SkillData, _skillIcon, _skillText,
            art != null ? art.Skill : null, active, false);
        DrawSkill(_hero.UltimateCaster, weapon.UltimateData, _ultimateIcon, _ultimateText,
            art != null ? art.Ultimate : null, active, true);
    }

    private void DrawSkill(SkillCaster caster, SkillData data, Image icon, TMP_Text label,
        Texture2D texture, bool active, bool ultimate)
    {
        string text;
        bool ready = false;
        Color color = new Color32(110, 110, 110, 255);
        if (caster == null || data == null) text = "연결 필요";
        else if (!active) text = "";
        else if (caster.IsBusy)
        {
            text = "";
            color = Color.white;
        }
        else if (caster.RemainingCooldown > 0f)
            text = "";
        // Readiness reflects the skill itself, independently of basic attacks.
        else if (caster.CanUse)
        {
            ready = true;
            text = "";
            color = Color.white;
        }
        else text = "";
        if (label != null)
        {
            label.text = text;
            label.rectTransform.anchoredPosition = new Vector2(0, -10);
        }
        SetIcon(icon, texture, color);
        SetReadiness(ultimate ? _ultimateFrame : _skillFrame, ultimate ? _ultimateEffect : _skillEffect, ready);
    }

    private void SetReadiness(Image frame, Image effect, bool ready)
    {
        if (frame != null)
        {
            frame.sprite = ready ? _readyFrame : _idleFrame;
            frame.color = ready ? Color.white : new Color32(110, 110, 110, 255);
        }
        if (effect != null)
        {
            effect.sprite = ready ? _readyEffect : null;
            effect.enabled = effect.sprite != null;
            if (effect.gameObject.activeSelf != effect.enabled)
                effect.gameObject.SetActive(effect.enabled);
        }
    }

    private void SetIcon(Image image, Texture2D texture, Color color)
    {
        if (image == null) return;
        Sprite sprite = null;
        if (texture != null && !_sprites.TryGetValue(texture, out sprite))
        {
            // Keep the other artist's texture import settings intact.
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name + " HUD";
            _sprites.Add(texture, sprite);
        }
        image.sprite = sprite;
        image.enabled = sprite != null;
        image.preserveAspect = true;
        image.color = color;
    }

    private void ResolveUI()
    {
        Transform player = FindRoot("PlayerStatus");
        _playerPortrait = player != null ? player.Find("PortraitFrame") : null;
        Transform stage = FindRoot("StageStatus");
        _skillIcon = GetAt<Image>(stage, "SkillRoot/SkillSlot/Icon");
        _skillText = GetAt<TMP_Text>(stage, "SkillRoot/SkillSlot/CooldownText");
        _ultimateIcon = GetAt<Image>(stage, "SkillRoot/SkillSlot (1)/Icon");
        _ultimateText = GetAt<TMP_Text>(stage, "SkillRoot/SkillSlot (1)/CooldownText");
        _skillFrame = GetAt<Image>(stage, "SkillRoot/SkillSlot/Background");
        _ultimateFrame = GetAt<Image>(stage, "SkillRoot/SkillSlot (1)/Background");
        _skillEffect = GetAt<Image>(stage, "SkillRoot/SkillSlot/ReadinessEffect");
        _ultimateEffect = GetAt<Image>(stage, "SkillRoot/SkillSlot (1)/ReadinessEffect");
        foreach (TMP_Text text in new[] { _skillText, _ultimateText })
        {
            if (text == null) continue;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12;
            text.fontSizeMax = 28;
        }
    }

    private Transform FindRoot(string prefix)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child != transform && child.name.StartsWith(prefix, StringComparison.Ordinal)) return child;
        return null;
    }

    private static T GetAt<T>(Transform root, string path) where T : Component
    {
        Transform child = root != null ? root.Find(path) : null;
        return child != null ? child.GetComponent<T>() : null;
    }

    private void Clear(string text)
    {
        ShowWeaponBadge(_playerPortrait, null, 64f);
        if (_playerPortrait != null) ShowAttackEffects(_playerPortrait.parent, null);
        SetIcon(_skillIcon, null, Color.gray);
        SetIcon(_ultimateIcon, null, Color.gray);
        SetReadiness(_skillFrame, _skillEffect, false);
        SetReadiness(_ultimateFrame, _ultimateEffect, false);
        if (_skillText != null) _skillText.text = text;
        if (_ultimateText != null) _ultimateText.text = text;
    }

    private void OnDisable()
    {
        Clear("");
        foreach (Image badge in _weaponBadges.Values)
            if (badge != null) badge.gameObject.SetActive(false);
        _hero = null;
        foreach (Behaviour binder in _replacedBinders)
            if (binder != null) binder.enabled = true;
        _replacedBinders.Clear();
    }

    internal void ShowWeaponBadge(Transform portrait, WeaponType? type, float size)
    {
        if (portrait == null) return;
        if (!_weaponBadges.TryGetValue(portrait, out Image frame))
        {
            if (!type.HasValue || _weaponFrame == null) return;
            Transform authored = portrait.parent.Find("WeaponBadge");
            if (authored != null && authored.TryGetComponent(out Image authoredFrame))
            {
                frame = authoredFrame;
                _weaponBadges.Add(portrait, frame);
            }
            else
            {
            frame = new GameObject("WeaponBadge", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image)).GetComponent<Image>();
            // A sibling avoids clipping by a portrait mask.
            frame.transform.SetParent(portrait.parent, false);
            frame.gameObject.layer = portrait.gameObject.layer;
            frame.raycastTarget = false;
            frame.rectTransform.sizeDelta = new Vector2(size, size);
            Image icon = new GameObject("WeaponIcon", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(frame.transform, false);
            icon.gameObject.layer = portrait.gameObject.layer;
            icon.rectTransform.sizeDelta = new Vector2(size * 0.6f, size * 0.6f);
            icon.raycastTarget = false;
            _weaponBadges.Add(portrait, frame);
            if (portrait is RectTransform portraitRect)
                frame.rectTransform.position = portraitRect.TransformPoint(new Vector3(
                    portraitRect.rect.xMin + size * 0.3f, portraitRect.rect.yMin + size * 0.4f, 0f));
            }
        }
        frame.gameObject.SetActive(type.HasValue && isActiveAndEnabled);
        if (!type.HasValue) return;
        WeaponArtwork art = Array.Find(_artwork, entry => entry != null && entry.Type == type.Value);
        SetIcon(frame, _weaponFrame, Color.white);
        SetIcon(frame.transform.GetChild(0).GetComponent<Image>(), art != null ? art.Weapon : null, Color.white);
    }

    internal void ShowAttackEffects(Transform owner, ScriptableObject source)
    {
        Transform root = owner != null ? owner.Find("AttackEffects") : null;
        if (root == null) return;
        AttackEffectLoadout loadout = source != null ?
            Array.Find(_attackEffectLoadouts, entry => entry != null && entry.Source == source) : null;
        int slot = 0;
        foreach (StatusEffectType type in new[] { StatusEffectType.Burn, StatusEffectType.Slow, StatusEffectType.Stun })
        {
            bool available = loadout != null && loadout.Effects != null &&
                Array.Exists(loadout.Effects, effect => effect != null && effect.Type == type && effect.Duration > 0f);
            if (!available || slot >= root.childCount) continue;
            int index = type == StatusEffectType.Burn ? 0 : type == StatusEffectType.Slow ? 1 : 2;
            Transform frame = root.GetChild(slot);
            Image icon = frame.Find("Icon").GetComponent<Image>();
            icon.sprite = index < _attackEffectIcons.Length ? _attackEffectIcons[index] : null;
            frame.gameObject.SetActive(icon.sprite != null && isActiveAndEnabled);
            slot++;
        }
        for (; slot < root.childCount; slot++) root.GetChild(slot).gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        foreach (Sprite sprite in _sprites.Values)
            if (sprite != null) Destroy(sprite);
        _sprites.Clear();
    }
}
