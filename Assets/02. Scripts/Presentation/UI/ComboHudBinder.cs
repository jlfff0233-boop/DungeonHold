using TMPro;
using LJTest;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public class ComboHudBinder : MonoBehaviour
{
    [Header("게임 데이터")]
    [SerializeField] private StageRewardController _rewards;
    [SerializeField] private ComboData _comboData;

    [Serializable]
    private sealed class TierArtwork
    {
        public Texture2D Bar;
        public Texture2D Enemy;
        public Texture2D BrokenRight;
        public Texture2D BrokenLeft;
        public Texture2D Effect;
    }
    [SerializeField] private TierArtwork[] _tiers = Array.Empty<TierArtwork>();
    [SerializeField] private Texture2D _barBackground;

    [Header("콤보 표시")]
    [SerializeField] private GameObject _comboRoot;
    [SerializeField] private TMP_Text _comboText;
    [SerializeField] private TMP_Text _remainingTimeText;

    [Header("획득 알림")]
    [SerializeField] private bool _showRewardNotice = false;
    [SerializeField] private GameObject _rewardRoot;
    [SerializeField] private TMP_Text _rewardText;
    [SerializeField, Min(0.1f)]
    private float _displaySeconds = 2f;

    private StageRewardController _boundRewards;
    private double _rewardHideTime;
    private TestScene _scene;
    private int _observedStageId;
    private bool _showingReward;
    private readonly Queue<RewardRequest> _rewardQueue = new Queue<RewardRequest>();
    private readonly HashSet<long> _shownLives = new HashSet<long>();
    private readonly Dictionary<Texture2D, Sprite> _sprites = new Dictionary<Texture2D, Sprite>();
    private Image _timeFill;
    private Image _enemyIcon;
    private Image _enemyLeftIcon;
    [SerializeField, Min(0f)] private float _comboBreakSeconds = 0.6f;
    private bool _hadActiveCombo;
    private int _lastComboTier;
    private double _comboBreakUntil;
    private Vector2 _iconPosition;
    private Vector2 _iconSize;
    private Quaternion _iconRotation;
    private Image _tierEffect;
    private string _authoredComboText;
    private Vector3 _comboTextScale;
    private int _lastDisplayedKillCount;
    private double _comboTextPulseStartedAt;
    private const float ComboTextPulseSeconds = 0.22f;
    [SerializeField, Range(0f, 1f)] private float _comboGlowOuter = 0.35f;
    private Material _comboGlowMaterial;
    private readonly Dictionary<Texture2D, Color> _effectColors = new Dictionary<Texture2D, Color>();

    private void Awake()
    {
        // Keep the wording, spacing and rich-text tags authored in the prefab.
        _authoredComboText = _comboText != null ? _comboText.text : "";
        if (_comboText != null)
            _comboTextScale = _comboText.rectTransform.localScale;
    }

    private void OnEnable()
    {
        EnsureVisuals();
        ResetRewardState();
        BindRewards();
        RefreshCombo();
    }

    private void LateUpdate()
    {
        BindRewards();
        CheckStage();
        RefreshCombo();
        if (_showingReward && Time.timeAsDouble >= _rewardHideTime) HideReward();
        ShowNextReward();
    }

    private void OnDisable()
    {
        if (_boundRewards != null)
        {
            _boundRewards.OnComboChanged -= HandleComboChanged;
            _boundRewards.OnRewardPaid -= HandleRewardPaid;
        }

        _boundRewards = null;
        ResetRewardState();
        SetCombo(0, 0d);
    }

    private void BindRewards()
    {
        if (_scene == null)
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                TestScene candidate = root.GetComponentInChildren<TestScene>(true);
                if (candidate != null && candidate.isActiveAndEnabled)
                {
                    _scene = candidate;
                    break;
                }
            }
        StageRewardController target = _rewards != null ? _rewards :
            _scene != null && _scene.IsInitialized ? _scene.Rewards : null;
        if (_boundRewards == target) return;
        if (_boundRewards != null)
        {
            _boundRewards.OnComboChanged -= HandleComboChanged;
            _boundRewards.OnRewardPaid -= HandleRewardPaid;
        }
        ResetRewardState();
        _boundRewards = target;
        if (_boundRewards != null)
        {
            _boundRewards.OnComboChanged += HandleComboChanged;
            _boundRewards.OnRewardPaid += HandleRewardPaid;
        }
        CheckStage();
    }

    private void CheckStage()
    {
        int stage = _boundRewards != null && _boundRewards.isActiveAndEnabled ? _boundRewards.CurrentStageId : 0;
        if (stage == _observedStageId) return;
        ResetRewardState();
        _observedStageId = stage;
    }

    private void ResetRewardState()
    {
        _lastDisplayedKillCount = 0;
        _hadActiveCombo = false;
        _comboBreakUntil = 0d;
        _rewardQueue.Clear();
        _shownLives.Clear();
        _observedStageId = 0;
        HideReward();
    }

    private void HandleComboChanged(int killCount, int nextRewardKillCount)
    {
        RefreshCombo();
    }

    private void RefreshCombo()
    {
        if (_boundRewards == null || !_boundRewards.isActiveAndEnabled || _boundRewards.CurrentStageId == 0)
        {
            _hadActiveCombo = false;
            _comboBreakUntil = 0d;
            SetCombo(0, 0d);
            return;
        }

        double remaining = _boundRewards.GetComboRemainingTime(
            Time.timeAsDouble);

        SetCombo(_boundRewards.ComboKillCount, remaining);
    }

    private void SetCombo(int killCount, double remaining)
    {
        bool visible = killCount > 0 && remaining > 0d;
        int tier = killCount <= 10 ? 0 : killCount <= 20 ? 1 : 2;
        if (visible)
        {
            _lastComboTier = tier;
            _comboBreakUntil = 0d;
        }
        else if (_hadActiveCombo)
        {
            _comboBreakUntil = Time.timeAsDouble + _comboBreakSeconds;
        }
        _hadActiveCombo = visible;
        bool breaking = !visible && Time.timeAsDouble < _comboBreakUntil;
        if (breaking) tier = _lastComboTier;
        bool showRoot = visible || breaking;

        if (_comboText != null)
        {
            if (visible) _comboText.text = FormatComboText(_authoredComboText, killCount);
            UpdateComboTextPulse(visible ? killCount : 0);
            _comboText.enabled = showRoot;
            if (showRoot)
            {
                _comboText.gameObject.SetActive(true);
            }
        }

        if (_remainingTimeText != null)
            _remainingTimeText.text = "";

        if (_comboRoot != null && _comboRoot.activeSelf != showRoot)
            _comboRoot.SetActive(showRoot);
        if (_timeFill != null)
        {
            // Hold the last displayed bar while the broken icon is shown.
            if (!breaking)
                _timeFill.fillAmount = visible && _comboData != null && _comboData.Duration > 0f
                    ? Mathf.Clamp01((float)(remaining / _comboData.Duration)) : 0f;
        }
        // Artwork uses inclusive 10/20/30-kill bands, independently of reward milestones.
        // Keep the final artwork for combos beyond 30 kills.
        TierArtwork art = showRoot && _tiers.Length > 0 ? _tiers[Mathf.Min(tier, _tiers.Length - 1)] : null;
        SetTexture(_timeFill, art != null ? art.Bar : null);
        if (_timeFill != null) _timeFill.enabled = showRoot && _comboData != null && art != null && art.Bar != null;
        SetTexture(_enemyIcon, art != null ? (breaking ? art.BrokenRight : art.Enemy) : null);
        SetTexture(_enemyLeftIcon, breaking && art != null ? art.BrokenLeft : null);
        if (_enemyLeftIcon != null)
            _enemyLeftIcon.gameObject.SetActive(breaking && art != null && art.BrokenLeft != null);
        UpdateIconLayout(art, breaking);
        SetTexture(_tierEffect, art != null ? art.Effect : null);
        if (_tierEffect != null) _tierEffect.enabled = showRoot && art != null && art.Effect != null;
        if (visible) UpdateComboGlow(art != null ? art.Effect : null, tier);
    }

    private void UpdateComboTextPulse(int killCount)
    {
        double now = Time.timeAsDouble;
        if (killCount > _lastDisplayedKillCount)
            _comboTextPulseStartedAt = now;
        _lastDisplayedKillCount = killCount;

        float scale = 1f;
        if (killCount > 0)
        {
            float progress = Mathf.Clamp01((float)((now - _comboTextPulseStartedAt) / ComboTextPulseSeconds));
            scale = Mathf.SmoothStep(1.25f, 1f, progress);
        }
        _comboText.rectTransform.localScale = _comboTextScale * scale;
    }

    private static Color GetTierGlowColor(int tier)
    {
        if (tier == 0) return new Color32(34, 104, 146, 255); // #226892
        if (tier == 1) return new Color32(71, 34, 99, 255); // #472263
        return new Color32(193, 163, 70, 255); // #C1A346
    }

    private void UpdateIconLayout(TierArtwork art, bool breaking)
    {
        if (_enemyIcon == null) return;
        RectTransform right = _enemyIcon.rectTransform;
        right.anchoredPosition = _iconPosition;
        right.sizeDelta = _iconSize;
        right.localRotation = _iconRotation;
        if (!breaking || art == null || art.Enemy == null) return;

        // Match the original image's pixel scale instead of stretching each narrow
        // fragment to the full icon width. Start with their outer edges aligned.
        float scale = Mathf.Min(right.rect.width / art.Enemy.width,
            right.rect.height / art.Enemy.height);
        float progress = _comboBreakSeconds > 0f
            ? Mathf.Clamp01(1f - (float)((_comboBreakUntil - Time.timeAsDouble) / _comboBreakSeconds)) : 1f;
        // Reference: a quick intact pop, a fast return, then a short diagonal split.
        const float splitStart = 0.72f;
        if (progress < splitStart)
        {
            float rise = Mathf.Clamp01(progress / 0.3f);
            float fall = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.48f, splitStart, progress));
            float growth = 1f + 1.6f * Mathf.SmoothStep(0f, 1f, rise) * (1f - fall);
            SetTexture(_enemyIcon, art.Enemy);
            right.sizeDelta = _iconSize;
            right.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, art.Enemy.width * scale * growth);
            right.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, art.Enemy.height * scale * growth);
            if (_enemyLeftIcon != null) _enemyLeftIcon.gameObject.SetActive(false);
            return;
        }
        float split = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(splitStart, 1f, progress));
        SetBrokenIconLayout(_enemyIcon, art.BrokenRight, art.Enemy, scale, 1f, split);
        SetBrokenIconLayout(_enemyLeftIcon, art.BrokenLeft, art.Enemy, scale, -1f, split);
    }

    private void SetBrokenIconLayout(Image image, Texture2D fragment, Texture2D original,
        float scale, float direction, float progress)
    {
        if (image == null || fragment == null) return;
        RectTransform rect = image.rectTransform;
        Vector2 size = new Vector2(fragment.width, fragment.height) * scale;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        float offset = (original.width - fragment.width) * scale * 0.5f;
        rect.anchoredPosition = _iconPosition + new Vector2(
            direction * (offset + original.width * scale * 0.06f * progress),
            -direction * original.height * scale * 0.09f * progress);
        rect.localRotation = _iconRotation;
    }

    private void UpdateComboGlow(Texture2D effect, int tier)
    {
        if (_comboText == null || effect == null) return;
        if (_comboGlowMaterial == null)
        {
            Material source = _comboText.fontSharedMaterial;
            if (source == null) return;
            _comboGlowMaterial = new Material(source);
            if (!_comboGlowMaterial.HasProperty("_GlowColor"))
            {
                Shader shader = Shader.Find("TextMeshPro/Distance Field");
                if (shader == null) { Destroy(_comboGlowMaterial); _comboGlowMaterial = null; return; }
                _comboGlowMaterial.shader = shader;
            }
            _comboGlowMaterial.EnableKeyword("GLOW_ON");
            // Use a solid contour for the glyphs and keep the glow outside it.
            _comboGlowMaterial.SetFloat("_OutlineWidth", 0.24f);
            _comboGlowMaterial.SetFloat("_OutlineSoftness", 0.04f);
            _comboGlowMaterial.DisableKeyword("UNDERLAY_INNER");
            _comboGlowMaterial.EnableKeyword("UNDERLAY_ON");
            _comboGlowMaterial.SetFloat("_UnderlayOffsetX", 0f);
            _comboGlowMaterial.SetFloat("_UnderlayOffsetY", 0f);
            _comboGlowMaterial.SetFloat("_UnderlayDilate", 0.6f);
            _comboGlowMaterial.SetFloat("_UnderlaySoftness", 0.25f);
            _comboGlowMaterial.SetFloat("_GlowOffset", 0f);
            _comboGlowMaterial.SetFloat("_GlowInner", 0.05f);
            _comboGlowMaterial.SetFloat("_GlowPower", 1f);
            _comboText.fontSharedMaterial = _comboGlowMaterial;
            _comboText.extraPadding = true;
        }
        if (!_effectColors.TryGetValue(effect, out Color color))
        {
            // GPU copy also supports textures imported with Read/Write disabled.
            RenderTexture previous = RenderTexture.active;
            RenderTexture sample = RenderTexture.GetTemporary(16, 16, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            Texture2D pixels = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(effect, sample);
                RenderTexture.active = sample;
                pixels.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
                pixels.Apply();
                Vector3 sum = Vector3.zero;
                float weight = 0f;
                foreach (Color pixel in pixels.GetPixels())
                {
                    sum += new Vector3(pixel.r, pixel.g, pixel.b) * pixel.a;
                    weight += pixel.a;
                }
                color = weight > 0f ? new Color(sum.x / weight, sum.y / weight, sum.z / weight, 1f) : Color.white;
                _effectColors.Add(effect, color);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(sample);
                Destroy(pixels);
            }
        }
        color = GetTierGlowColor(tier);
        _comboGlowMaterial.SetColor("_OutlineColor", color);
        _comboGlowMaterial.SetColor("_UnderlayColor", color);
        color.a = 2f;
        _comboGlowMaterial.SetColor("_GlowColor", color);
        _comboGlowMaterial.SetFloat("_GlowOuter", _comboGlowOuter);
        _comboText.UpdateMeshPadding();
    }

    private static string FormatComboText(string template, int count)
    {
        if (string.IsNullOrEmpty(template)) return count.ToString();
        if (template.Contains("{count}")) return template.Replace("{count}", count.ToString());
        bool replaced = false;
        return Regex.Replace(template, @"<[^>]*>|\d+", match =>
        {
            if (replaced || match.Value.StartsWith("<", StringComparison.Ordinal)) return match.Value;
            replaced = true;
            return count.ToString();
        });
    }

    private void HandleRewardPaid(RewardRequest request)
    {
        if (!_showRewardNotice) return;
        if (_boundRewards == null ||
            !_boundRewards.isActiveAndEnabled || request.StageId == 0 ||
            request.StageId != _boundRewards.CurrentStageId ||
            request.ComboAmount <= 0)
        {
            return;
        }

        CheckStage();
        if (!_shownLives.Add(request.LifeId)) return;
        _rewardQueue.Enqueue(request);
        ShowNextReward();
    }

    private void ShowNextReward()
    {
        if (!_showRewardNotice)
        {
            _rewardQueue.Clear();
            HideReward();
            return;
        }
        if (_showingReward || _rewardQueue.Count == 0 || _boundRewards == null) return;
        RewardRequest request = _rewardQueue.Dequeue();
        if (request.StageId != _boundRewards.CurrentStageId) return;
        _rewardHideTime = Time.timeAsDouble + _displaySeconds;
        _showingReward = true;
        if (_rewardText != null) _rewardText.text = $"연속 처치 보너스 +{request.ComboAmount:N0}";
        if (_rewardRoot != null) _rewardRoot.SetActive(true);
    }

    private void HideReward()
    {
        _showingReward = false;
        if (_rewardText != null)
            _rewardText.text = "";

        if (_rewardRoot != null && _rewardRoot.activeSelf)
            _rewardRoot.SetActive(false);
    }

    private void EnsureVisuals()
    {
        if (_comboRoot == null || _timeFill != null) return;
        if (_remainingTimeText == null)
            foreach (TMP_Text text in _comboRoot.GetComponentsInChildren<TMP_Text>(true))
                if (text.name == "RemaingingTimeText" || text.name == "RemainingTimeText")
                    _remainingTimeText = text;
        // Use the authored HUD without changing its layout or creating children.
        _timeFill = FindImage("ComboTimeFill");
        _tierEffect = FindImage("ComboTierEffect");
        _enemyIcon = FindImage("ComboTierIcon");
        _enemyLeftIcon = FindImage("ComboTierIcon Left");
        if (_enemyIcon != null)
        {
            _iconPosition = _enemyIcon.rectTransform.anchoredPosition;
            _iconSize = _enemyIcon.rectTransform.sizeDelta;
            _iconRotation = _enemyIcon.rectTransform.localRotation;
        }
        if (_timeFill != null)
        {
            _timeFill.type = Image.Type.Filled;
            _timeFill.fillMethod = Image.FillMethod.Horizontal;
            _timeFill.fillOrigin = 0;
        }
    }

    private Image FindImage(string objectName)
    {
        // The time fill is nested under ComboTimeBackground in the authored HUD.
        foreach (Image image in _comboRoot.GetComponentsInChildren<Image>(true))
            if (image.name == objectName) return image;
        return null;
    }

    private void SetTexture(Image image, Texture2D texture)
    {
        if (image == null) return;
        Sprite sprite = null;
        if (texture != null && !_sprites.TryGetValue(texture, out sprite))
        {
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            _sprites.Add(texture, sprite);
        }
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private void OnDestroy()
    {
        if (_comboGlowMaterial != null) Destroy(_comboGlowMaterial);
        foreach (Sprite sprite in _sprites.Values) if (sprite != null) Destroy(sprite);
        _sprites.Clear();
    }

    [ContextMenu("콤보 HUD 상태 확인")]
    private void LogHudState()
    {
        StageRewardController rewards = _boundRewards != null
            ? _boundRewards : _rewards;
        double remaining = rewards != null
            ? rewards.GetComboRemainingTime(Time.timeAsDouble) : 0d;

        Debug.Log(
            $"콤보 HUD: 실행 중={Application.isPlaying}"
            + $" / Binder 활성={isActiveAndEnabled}"
            + $" / 도전={ (rewards != null ? rewards.CurrentStageId : 0)}"
            + $" / 처치 수={(rewards != null ? rewards.ComboKillCount : 0)}"
            + $" / 남은 시간={remaining:F2}"
            + $" / Root 활성={(_comboRoot != null && _comboRoot.activeInHierarchy)}"
            + $" / Text 활성={(_comboText != null && _comboText.isActiveAndEnabled)}"
            + $" / 표시 문자열={(_comboText != null ? _comboText.text : "미연결")}",
            this);
    }
}
