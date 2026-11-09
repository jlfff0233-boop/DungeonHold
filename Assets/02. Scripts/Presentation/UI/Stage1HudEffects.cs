using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using LJTest;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(500)]
public sealed class Stage1HudEffects : MonoBehaviour
{
    [SerializeField] private RectTransform _earningTemplate;

    private const float CurrencyPulseSeconds = 0.22f;
    private const float EarningSeconds = 0.95f;
    private static readonly Color32 CountdownColor = new Color32(111, 255, 207, 255);

    private TestScene _scene;
    private WaveController _wave;
    private DynamicTargetHudBinder _targetHud;
    private StageRewardController _rewards;
    private TMP_Text _currencyText;
    private TMP_Text _startNotice;
    private Transform _enemyHud;
    private Camera _camera;
    private Vector3 _currencyScale;
    private Vector3 _noticeScale;
    private int _balance;
    private bool _hasBalance;
    private Tween _currencyPulse;
    private float _nextHudLookup;
    private float _reveal;
    private int _countdownNumber;
    private readonly List<Earning> _earnings = new List<Earning>();

    private sealed class Earning
    {
        public CanvasGroup View;
        public Vector3 Position;
        public int StageId;
        public float Age;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= Install;
        SceneManager.sceneLoaded += Install;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Install(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    private static void Install(Scene scene, LoadSceneMode mode)
    {
        if (!scene.isLoaded || scene.name != "!_Play_Stage1") return;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.TryGetComponent<Stage1HudEffects>(out _)) return;

        GameObject template = Resources.Load<GameObject>("Stage1HudEffects");
        GameObject instance = template != null ? Instantiate(template) : new GameObject("Stage1HudEffects");
        SceneManager.MoveGameObjectToScene(instance, scene);
        if (!instance.TryGetComponent<Stage1HudEffects>(out _))
            instance.AddComponent<Stage1HudEffects>();
    }

    private void Start()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (_scene == null) _scene = root.GetComponentInChildren<TestScene>(true);
            if (_wave == null) _wave = root.GetComponentInChildren<WaveController>(true);
            if (_targetHud == null) _targetHud = root.GetComponentInChildren<DynamicTargetHudBinder>(true);
        }
        ResolveHud();
    }

    private void ResolveHud()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (!label.gameObject.activeInHierarchy) continue;
                if (_currencyText == null && label.name == "CurrencyText" && label.transform.parent.name == "Currency")
                {
                    _currencyText = label;
                    _currencyScale = label.rectTransform.localScale;
                    _hasBalance = false;
                }
                if (_startNotice == null && label.name == "StartNotice")
                {
                    _startNotice = label;
                    PrepareNotice();
                }
            }
        }
    }

    private void PrepareNotice()
    {
        RectTransform rect = _startNotice.rectTransform;
        _noticeScale = rect.localScale;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-20f, 220f);
        rect.sizeDelta = new Vector2(520f, 160f);
        _startNotice.alignment = TextAlignmentOptions.Center;
        _startNotice.enableAutoSizing = false;
        _startNotice.fontStyle |= FontStyles.Bold;
        _startNotice.raycastTarget = false;
        _startNotice.enabled = false;
    }

    private void LateUpdate()
    {
        if ((_currencyText == null || _startNotice == null) && Time.unscaledTime >= _nextHudLookup)
        {
            _nextHudLookup = Time.unscaledTime + 0.5f;
            ResolveHud();
        }
        StageRewardController rewards = _scene != null && _scene.IsInitialized ? _scene.Rewards : null;
        if (_rewards != rewards)
        {
            if (_rewards != null) _rewards.OnRewardPaid -= HandleRewardPaid;
            _rewards = rewards;
            if (_rewards != null) _rewards.OnRewardPaid += HandleRewardPaid;
        }
        UpdateCurrency();
        UpdateCountdown();
        HideEarningTemplates();
        UpdateEarnings();
    }

    private void UpdateCurrency()
    {
        if (_currencyText == null ||
            !int.TryParse(_currencyText.text, NumberStyles.Integer | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture, out int balance))
            return;

        if (!_hasBalance || balance != _balance)
        {
            bool increase = _hasBalance && balance > _balance;
            bool revealing = _currencyText.maxVisibleCharacters != int.MaxValue;
            _balance = balance;
            _hasBalance = true;
            _currencyPulse?.Kill();
            RectTransform rect = _currencyText.rectTransform;
            rect.localScale = _currencyScale * (increase ? 1.25f : 1f);
            if (increase)
                _currencyPulse = rect.DOScale(_currencyScale, CurrencyPulseSeconds)
                    .SetEase(Ease.OutCubic).SetUpdate(true);
            if (!increase || _currencyText.text.Length <= 1)
                _currencyText.maxVisibleCharacters = int.MaxValue;
            else if (!revealing)
            {
                _reveal = 0f;
                _currencyText.maxVisibleCharacters = 1;
            }
        }

        if (_currencyText.maxVisibleCharacters == int.MaxValue) return;
        int visible = 1 + Mathf.FloorToInt(_reveal / 0.055f);
        _currencyText.maxVisibleCharacters = visible >= _currencyText.text.Length ? int.MaxValue : visible;
        _reveal += Time.unscaledDeltaTime;
    }

    private void UpdateCountdown()
    {
        if (_startNotice == null) return;
        float remaining = _wave != null ? _wave.RemainingPreparationTime : 0f;
        bool visible = _wave != null && _wave.isActiveAndEnabled && _wave.IsPreparing &&
            !_wave.HasStoppedForStageResult && remaining > 0f && remaining <= 3f;
        _startNotice.enabled = visible;
        if (!visible) { _countdownNumber = 0; return; }

        int number = Mathf.CeilToInt(remaining);
        if (number != _countdownNumber)
        {
            _countdownNumber = number;
            _startNotice.text = number.ToString();
            _startNotice.fontSize = 84f + 18f * (3 - number);
        }
        float progress = number - remaining;
        float scale = Mathf.SmoothStep(1.9f, 1f, Mathf.Clamp01(progress / 0.2f));
        Color color = CountdownColor;
        color.a = Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(progress / 0.12f)) *
            (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 1f, progress)));
        _startNotice.color = color;
        _startNotice.rectTransform.localScale = _noticeScale * scale;
    }

    private void HideEarningTemplates()
    {
        if (_enemyHud == null && _targetHud != null)
            _enemyHud = _targetHud.transform.Find("DynamicEnemyHealthUI");
        if (_enemyHud == null) return;
        foreach (Transform healthUi in _enemyHud)
        {
            Transform earning = healthUi.Find("Earning");
            if (earning == null) continue;
            if (_earningTemplate == null) _earningTemplate = earning as RectTransform;
            if (earning.gameObject.activeSelf) earning.gameObject.SetActive(false);
        }
    }

    private void HandleRewardPaid(RewardRequest reward)
    {
        if (_earningTemplate == null || reward.KillAmount <= 0 ||
            reward.StageId != _rewards.CurrentStageId)
            return;
        foreach (EnemyCombat enemy in FindObjectsByType<EnemyCombat>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (enemy.gameObject.scene != gameObject.scene || enemy.RewardStageId != reward.StageId ||
                enemy.RewardLifeId != reward.LifeId)
                continue;

            Vector3 position = enemy.transform.position + Vector3.up * 2.2f;
            Renderer renderer = enemy.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                position = new Vector3(bounds.center.x, bounds.max.y + 0.3f, bounds.center.z);
            }
            ShowEarning(reward, position);
            return;
        }
    }

    private void ShowEarning(RewardRequest reward, Vector3 position)
    {
        var popup = new GameObject("Enemy Earning", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        popup.SetActive(false);
        popup.layer = _earningTemplate.gameObject.layer;
        popup.transform.SetParent(transform, false);
        Canvas sourceCanvas = _earningTemplate.GetComponentInParent<Canvas>(true);
        popup.transform.localScale = sourceCanvas != null ? sourceCanvas.transform.lossyScale : Vector3.one * 0.01f;
        popup.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        RectTransform earning = Instantiate(_earningTemplate, popup.transform, false);
        earning.gameObject.SetActive(true);
        foreach (Graphic graphic in earning.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        TMP_Text label = earning.Find("Text")?.GetComponent<TMP_Text>();
        if (label == null) { Destroy(popup); return; }
        label.text = $"+{reward.KillAmount:N0}";
        label.margin = Vector4.zero;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        float textWidth = label.GetPreferredValues(label.text).x;
        RectTransform text = label.rectTransform;
        RectTransform icon = earning.Find("Icon") as RectTransform;
        float iconWidth = icon != null ? icon.rect.width : 0f;
        float gap = icon != null ? 3f : 0f;
        float y = (icon != null ? icon.rect.height : text.rect.height) * 0.5f;
        text.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);
        AlignEarningPart(text, (iconWidth + gap) * 0.5f, y);
        if (icon != null) AlignEarningPart(icon, -(textWidth + gap) * 0.5f, y);

        CanvasGroup view = popup.GetComponent<CanvasGroup>();
        view.blocksRaycasts = false;
        view.interactable = false;
        _earnings.Add(new Earning { View = view, Position = position, StageId = reward.StageId });
    }

    private static void AlignEarningPart(RectTransform rect, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
    }

    private void UpdateEarnings()
    {
        if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
        int stageId = _rewards != null ? _rewards.CurrentStageId : 0;
        for (int i = _earnings.Count - 1; i >= 0; i--)
        {
            Earning earning = _earnings[i];
            earning.Age += Time.deltaTime;
            if (earning.Age >= EarningSeconds || earning.StageId != stageId)
            {
                earning.View.gameObject.SetActive(false);
                Destroy(earning.View.gameObject);
                _earnings.RemoveAt(i);
                continue;
            }
            float progress = earning.Age / EarningSeconds;
            Vector3 position = earning.Position + Vector3.up * (0.55f * progress);
            earning.View.alpha = 1f - Mathf.InverseLerp(0.55f, 1f, progress);
            if (_camera != null)
                earning.View.transform.SetPositionAndRotation(position, _camera.transform.rotation);
            earning.View.gameObject.SetActive(_camera != null && _camera.WorldToViewportPoint(position).z > 0f);
        }
    }

    private void OnDestroy()
    {
        _currencyPulse?.Kill();
        if (_rewards != null) _rewards.OnRewardPaid -= HandleRewardPaid;
        if (_currencyText != null)
        {
            _currencyText.maxVisibleCharacters = int.MaxValue;
            _currencyText.rectTransform.localScale = _currencyScale;
        }
        if (_startNotice != null) _startNotice.enabled = false;
    }
}
