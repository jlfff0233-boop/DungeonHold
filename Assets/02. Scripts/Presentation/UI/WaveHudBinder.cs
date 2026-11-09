using LJTest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WaveHudBinder : MonoBehaviour
{
    [SerializeField] private WaveController _waveController;
    [SerializeField] private Stage1Data _stageData;
    [SerializeField] private Stage1Data _waveCountData;
    [SerializeField] private Stage1Controller _stageController;
    [SerializeField] private TMP_Text _waveText;
    [SerializeField] private TMP_Text _timerText;
    [SerializeField] private TMP_Text _passedEnemyText;
    [SerializeField] private TMP_Text _activeEnemyText;
    [SerializeField] private TMP_Text _starText;
    [SerializeField, Range(0f, 1f)] private float _warningRatio = 0.8f;

    private Image _objectiveFill;
    private GameObject _portalLife;
    private Image _portalLifeSource;
    private RectTransform _portalSwirl;
    private Material _portalSwirlMaterial;
    private const float PortalSwirlDegreesPerSecond = 75f;
    [SerializeField] private GameObject _startBar;
    private Image _startEffectSource;
    private Image _startEffect;
    [SerializeField] private GameObject _star;
    private CanvasGroup _starFadeGroup;
    private float _starBaseAlpha;
    private const float StarFadeSeconds = 0.8f;
    [SerializeField] private TMP_Text _startWaveText;
    private float _startRemaining;
    private int _startedWave;
    private const float WaveStartSeconds = 2f;
    [SerializeField] private TMP_Text _scoreText;
    private int _score;
    private WaveController _boundWave;
    private TestScene _scene;
    private readonly Color _normalColor = Color.white;
    private readonly Color _warningColor = new Color32(255, 196, 70, 255);
    private readonly Color _failureColor = new Color32(232, 90, 90, 255);

    private void OnEnable()
    {
        ResolveWaveText();
        ResolveTimerText();
        ResolveRemainingEnemyText();
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.name == "PortalLife" || child.name == "Portal_Life") { _portalLife = child.gameObject; break; }
        EnsurePortalSwirl();
        EnsureStartBar();
        if (_star != null && _starFadeGroup == null)
        {
            if (!_star.TryGetComponent(out _starFadeGroup))
                _starFadeGroup = _star.AddComponent<CanvasGroup>();
            _starBaseAlpha = _starFadeGroup.alpha;
        }
        EnsureStartEffect();
        SetPortalLifeVisible(false);
        if (_passedEnemyText != null)
        {
            Transform fill = _passedEnemyText.transform.parent.Find("ObjectiveFill");
            _objectiveFill = fill != null ? fill.GetComponent<Image>() : null;
        }
        EnsureStarText();
        SetScore(_score);
        BindWave();
        Refresh();
    }

    private void LateUpdate()
    {
        BindWave();
        _startRemaining = Mathf.Max(0f, _startRemaining - Time.deltaTime);
        Refresh();
        if (_portalSwirl != null && _portalSwirl.gameObject.activeInHierarchy)
            _portalSwirl.Rotate(0f, 0f, PortalSwirlDegreesPerSecond * Time.deltaTime);
    }

    private void EnsurePortalSwirl()
    {
        if (_portalSwirl != null || _portalLife == null ||
            !_portalLife.TryGetComponent(out Image source) || !source.enabled)
            return;

        var swirl = new GameObject("PortalSwirl", typeof(RectTransform), typeof(Image));
        swirl.layer = _portalLife.layer;
        _portalSwirl = (RectTransform)swirl.transform;
        _portalSwirl.SetParent(source.rectTransform, false);
        _portalSwirl.SetAsFirstSibling();
        _portalSwirl.anchorMin = Vector2.zero;
        _portalSwirl.anchorMax = Vector2.one;
        _portalSwirl.offsetMin = Vector2.zero;
        _portalSwirl.offsetMax = Vector2.zero;

        Image image = swirl.GetComponent<Image>();
        image.sprite = source.overrideSprite;
        image.color = source.color;
        image.material = source.material;
        Shader glowShader = Resources.Load<Shader>("PortalSwirlGlow");
        if (glowShader != null)
        {
            _portalSwirlMaterial = new Material(glowShader);
            image.material = _portalSwirlMaterial;
        }
        image.type = source.type;
        image.preserveAspect = source.preserveAspect;
        image.maskable = source.maskable;
        image.raycastTarget = false;
        _portalLifeSource = source;
        source.enabled = false;
    }

    private void OnDestroy()
    {
        if (_portalLifeSource != null) _portalLifeSource.enabled = true;
        if (_portalSwirl != null) Destroy(_portalSwirl.gameObject);
        if (_portalSwirlMaterial != null) Destroy(_portalSwirlMaterial);
        if (_startEffectSource != null) _startEffectSource.enabled = true;
        if (_startEffect != null) Destroy(_startEffect.gameObject);
        if (_starFadeGroup != null) _starFadeGroup.alpha = _starBaseAlpha;
    }

    private void EnsureStartEffect()
    {
        if (_startEffect != null || _startBar == null ||
            !_startBar.TryGetComponent(out Image source) || !source.enabled)
            return;

        Sprite sprite = source.overrideSprite;
        if (sprite == null || sprite.name != "Wave_Start_Effect") return;

        var effect = new GameObject("Wave Start Effect Visual", typeof(RectTransform), typeof(Image));
        effect.layer = source.gameObject.layer;
        _startEffect = effect.GetComponent<Image>();
        RectTransform rect = _startEffect.rectTransform;
        rect.SetParent(source.rectTransform, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        _startEffect.sprite = sprite;
        _startEffect.material = source.material;
        _startEffect.type = source.type;
        _startEffect.preserveAspect = source.preserveAspect;
        _startEffect.maskable = source.maskable;
        _startEffect.raycastTarget = false;
        _startEffectSource = source;
        source.enabled = false;
    }

    private void AnimateStartEffect()
    {
        if (_startEffect == null) return;

        const float expandSeconds = 0.55f;
        const float collapseSeconds = 0.5f;
        float expand = Mathf.Clamp01((WaveStartSeconds - _startRemaining) / expandSeconds);
        float collapse = Mathf.Clamp01(_startRemaining / collapseSeconds);
        float width = Mathf.SmoothStep(0f, 1f, Mathf.Min(expand, collapse));
        _startEffect.rectTransform.localScale = new Vector3(
            Mathf.Lerp(0.005f, 1f, width), Mathf.Lerp(0.12f, 1f, width), 1f);

        Color color = _startEffectSource.color;
        color.a *= Mathf.SmoothStep(0f, 1f, collapse);
        _startEffect.color = color;
    }

    private void ResolveWaveText()
    {
        // Use WavePannel first; retain the existing prefab's WaveBanner as a fallback.
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (string panelName in new[] { "WavePannel", "WavePanel", "WaveBanner" })
        {
            foreach (Transform child in children)
            {
                if (child.name != panelName) continue;
                Transform label = child.Find("WaveText");
                TMP_Text text = label != null ? label.GetComponent<TMP_Text>() : child.GetComponentInChildren<TMP_Text>(true);
                if (text == null) continue;
                _waveText = text;
                return;
            }
        }
    }

    public void SetScore(int score)
    {
        _score = Mathf.Max(0, score);
        if (_scoreText != null) _scoreText.text = $"점수 {_score:N0}";
    }

    private void ResolveTimerText()
    {
        // Bind the label on this HUD instance, including inactive authored UI.
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name != "TimerBanner") continue;
            Transform label = child.Find("Timer");
            if (label != null && label.TryGetComponent(out TMP_Text text))
            {
                _timerText = text;
                return;
            }
        }
    }

    private void ShowTimer()
    {
        if (_timerText == null) return;
        Transform parent = _timerText.transform.parent;
        if (parent != null && parent.name == "TimerBanner" && !parent.gameObject.activeSelf)
            parent.gameObject.SetActive(true);
        if (!_timerText.gameObject.activeSelf) _timerText.gameObject.SetActive(true);
        _timerText.enabled = true;
    }

    private void ResolveRemainingEnemyText()
    {
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name != "RemainingEnemyText") continue;
            _activeEnemyText = text;
            return;
        }
    }

    private void BindWave()
    {
        if (_boundWave == _waveController) return;
        if (_boundWave != null) _boundWave.OnWaveStarted -= HandleWaveStarted;
        _boundWave = _waveController;
        Clear();
        if (_boundWave != null) _boundWave.OnWaveStarted += HandleWaveStarted;
    }

    private void HandleWaveStarted(int waveNumber)
    {
        _startedWave = waveNumber;
        _startRemaining = WaveStartSeconds;
        Refresh();
    }

    private void EnsureStartBar()
    {
        if (_star == null)
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (string.Equals(child.name, "Star", System.StringComparison.OrdinalIgnoreCase))
                { _star = child.gameObject; break; }
        if (_startWaveText == null && _star != null)
            _startWaveText = _star.GetComponentInChildren<TMP_Text>(true);
        if (_portalLife == null) return;
        if (_startBar == null)
            foreach (string name in new[] { "StarBar", "StartBar", "Start" })
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                    if (string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase))
                    { _startBar = child.gameObject; break; }
                if (_startBar != null) break;
            }
        if (_startBar != null)
        {
            // Intro objects must remain visible when PortalLife is disabled.
            if (_startBar.transform.IsChildOf(_portalLife.transform))
                _startBar.transform.SetParent(_portalLife.transform.parent, true);
            if (_star != null && _star.transform.IsChildOf(_portalLife.transform))
                _star.transform.SetParent(_portalLife.transform.parent, true);
            Image background = _startBar.GetComponent<Image>();
            if (background != null && background.sprite == null)
            {
                background.sprite = Resources.Load<Sprite>("WorldUI/Wave_Start_Frame");
                background.preserveAspect = true;
                background.raycastTarget = false;
            }
            if (_startWaveText == null)
                _startWaveText = _startBar.GetComponentInChildren<TMP_Text>(true);
            if (_startWaveText == null)
            {
                RectTransform labelRect = new GameObject("Star", typeof(RectTransform)).GetComponent<RectTransform>();
                labelRect.SetParent(_startBar.transform, false);
                labelRect.gameObject.layer = _startBar.layer;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.sizeDelta = Vector2.zero;
                _startWaveText = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
                if (_activeEnemyText != null) _startWaveText.font = _activeEnemyText.font;
                _startWaveText.fontSize = 24;
                _startWaveText.alignment = TextAlignmentOptions.Center;
                _startWaveText.raycastTarget = false;
                if (_star == null) _star = labelRect.gameObject;
            }
            return;
        }
        RectTransform portal = _portalLife.GetComponent<RectTransform>();
        RectTransform bar = new GameObject("StartBar", typeof(RectTransform)).GetComponent<RectTransform>();
        bar.SetParent(portal.parent, false);
        bar.gameObject.layer = _portalLife.layer;
        bar.anchorMin = portal.anchorMin;
        bar.anchorMax = portal.anchorMax;
        bar.pivot = portal.pivot;
        bar.anchoredPosition = portal.anchoredPosition;
        bar.sizeDelta = portal.sizeDelta;
        // The intro is independent of PortalLife, including its remaining-enemy text.
        bar.SetSiblingIndex(portal.GetSiblingIndex());
        RectTransform frame = new GameObject("Wave_Start", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        frame.SetParent(bar, false);
        frame.gameObject.layer = _portalLife.layer;
        frame.anchorMin = Vector2.zero;
        frame.anchorMax = Vector2.one;
        frame.sizeDelta = Vector2.zero;
        Image image = frame.GetComponent<Image>();
        image.sprite = Resources.Load<Sprite>("WorldUI/Wave_Start_Frame");
        image.preserveAspect = true;
        image.raycastTarget = false;
        RectTransform label = new GameObject("WaveStartText", typeof(RectTransform)).GetComponent<RectTransform>();
        label.SetParent(bar, false);
        label.gameObject.layer = _portalLife.layer;
        label.anchorMin = Vector2.zero;
        label.anchorMax = Vector2.one;
        label.sizeDelta = Vector2.zero;
        _startWaveText = label.gameObject.AddComponent<TextMeshProUGUI>();
        if (_activeEnemyText != null) _startWaveText.font = _activeEnemyText.font;
        _startWaveText.fontSize = 24;
        _startWaveText.alignment = TextAlignmentOptions.Center;
        _startWaveText.raycastTarget = false;
        _startBar = bar.gameObject;
        _startBar.SetActive(false);
    }

    private void Refresh()
    {
        if (_waveController == null || !_waveController.isActiveAndEnabled)
        {
            Clear();
            return;
        }
        if (_scene == null)
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                TestScene candidate = root.GetComponentInChildren<TestScene>(true);
                if (candidate != null && candidate.isActiveAndEnabled) { _scene = candidate; break; }
            }
        bool hasRules = _stageData != null && _stageData.IsValid;
        bool idle = _scene != null && _scene.IsInitialized && !_scene.IsStageReady && !_scene.IsEndingStage;
        int wave = idle ? 0 : Mathf.Max(0, _waveController.CurrentWaveNumber);
        int passed = idle ? 0 : Mathf.Max(0, _waveController.PassedEnemyCount);
        int active = idle ? 0 : Mathf.Max(0, _waveController.ActiveEnemyCount);
        bool finished = !idle && _stageController != null && _stageController.isActiveAndEnabled && _stageController.HasFinished;
        bool running = !idle && !finished && _waveController.IsWaveRunning &&
            !_waveController.HasStoppedForStageResult && !_waveController.HasCompletedAllWaves;
        if (!running) _startRemaining = 0f;
        bool starting = running && _startRemaining > 0f;
        if (starting) AnimateStartEffect();
        SetPortalLifeVisible(running && !starting);
        if (_startBar != null) _startBar.SetActive(starting);
        if (_starFadeGroup != null)
            _starFadeGroup.alpha = _starBaseAlpha * Mathf.SmoothStep(0f, 1f, _startRemaining / StarFadeSeconds);
        if (_star != null) _star.SetActive(starting);
        if (_startWaveText != null)
        {
            _startWaveText.gameObject.SetActive(starting);
            _startWaveText.text = starting ? $"웨이브 {_startedWave}" : "";
        }
        if (finished) passed = _stageController.FinalPassedCount;

        Stage1Data waveCountData = _waveCountData != null ? _waveCountData : _stageData;
        int totalWaves = waveCountData != null ? Mathf.Max(0, waveCountData.WaveCount) : 0;
        if (_waveText != null) _waveText.text = totalWaves > 0
            ? $"웨이브 {Mathf.Clamp(wave, 0, totalWaves)} / {totalWaves}"
            : "웨이브 정보 없음";
        if (_timerText != null)
        {
            ShowTimer();
            if (idle) _timerText.text = "입장 전";
            else if (finished)
            {
                _timerText.text = _stageController.HasFailed ? "실패!" : "스테이지 성공";
            }
            else if (_waveController.HasStoppedForStageResult) _timerText.text = "진행 종료";
            else if (_waveController.HasCompletedAllWaves) _timerText.text = "웨이브 종료";
            else if (_waveController.IsPreparing) _timerText.text = FormatTime(_waveController.RemainingPreparationTime);
            else if (_waveController.IsWaveRunning)
                _timerText.text = _stageController != null ? FormatTime(_stageController.RemainingCombatTime) : "--:--";
            else _timerText.text = "대기";
        }
        if (_passedEnemyText != null)
        {
            _passedEnemyText.text = hasRules ? Mathf.Max(0, _stageData.PassLimit - passed).ToString() : $"통과 {passed} · 한도 미연결";
            _passedEnemyText.color = !idle && hasRules && passed >= _stageData.PassLimit ? _failureColor :
                !idle && hasRules && passed >= Mathf.CeilToInt(_stageData.PassLimit * _warningRatio) ? _warningColor : _normalColor;
        }
        if (_objectiveFill != null)
            _objectiveFill.fillAmount = hasRules && _stageData.PassLimit > 0
                ? Mathf.Clamp01((float)(_stageData.PassLimit - passed) / _stageData.PassLimit) : 0f;
        int remaining = active;
        if (!idle && waveCountData != null && waveCountData.IsValid && wave > 0 && wave <= waveCountData.WaveCount)
        {
            int plannedThroughCurrentWave = 0;
            for (int i = 0; i < wave; i++)
            {
                WaveData data = waveCountData.GetWave(i);
                int total = 0;
                for (int group = 0; group < data.SpawnGroupCount; group++)
                    total += data.GetSpawnGroup(group).Count;
                plannedThroughCurrentWave += total;
            }
            // Includes enemies that have not spawned yet; restart restores these counters.
            remaining = _waveController.HasCompletedAllWaves ? 0 :
                Mathf.Clamp(plannedThroughCurrentWave - _waveController.DiedEnemyCount - passed, 0, plannedThroughCurrentWave);
        }
        if (_activeEnemyText != null) _activeEnemyText.text = remaining.ToString("N0");
        if (_starText != null && _starText != _startWaveText &&
            (_star == null || !_starText.transform.IsChildOf(_star.transform)))
        {
            _starText.gameObject.SetActive(false);
            _starText.color = _normalColor;
            if (!hasRules) _starText.text = "별점 조건 미연결";
            else if (idle) _starText.text = "";
            else if (finished) _starText.text = $"최종 {_stageController.StarCount}성\n" + Conditions();
            else
            {
                int potential = passed >= _stageData.PassLimit ? 0 : passed <= _stageData.ThreeStarMaxPassed ? 3 :
                    passed <= _stageData.TwoStarMaxPassed ? 2 : 1;
                _starText.text = $"현재 유지 가능 {potential}성\n" + Conditions();
                if (potential == 0) _starText.color = _failureColor;
                else if (potential < 3) _starText.color = _warningColor;
            }
        }
    }

    private string Conditions() => $"클리어 시: 3성 통과 {_stageData.ThreeStarMaxPassed}명 이하\n2성 통과 {_stageData.TwoStarMaxPassed}명 이하";

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private void EnsureStarText()
    {
        if (_starText != null || _passedEnemyText == null) return;
        RectTransform rect = new GameObject("StageStarConditions", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(_passedEnemyText.transform.parent, false);
        rect.gameObject.layer = _passedEnemyText.gameObject.layer;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0, -8);
        rect.sizeDelta = new Vector2(400, 90);
        _starText = rect.gameObject.AddComponent<TextMeshProUGUI>();
        _starText.font = _passedEnemyText.font;
        _starText.fontSize = 20;
        _starText.enableAutoSizing = true;
        _starText.fontSizeMin = 14;
        _starText.fontSizeMax = 20;
        _starText.alignment = TextAlignmentOptions.Center;
        _starText.raycastTarget = false;
    }

    private void SetPortalLifeVisible(bool visible)
    {
        if (_portalLife != null && _portalLife.activeSelf != visible) _portalLife.SetActive(visible);
    }

    private void Clear()
    {
        _startRemaining = 0f;
        if (_startBar != null) _startBar.SetActive(false);
        if (_star != null) _star.SetActive(false);
        if (_startWaveText != null) _startWaveText.text = "";
        SetPortalLifeVisible(false);
        foreach (TMP_Text text in new[] { _waveText, _timerText, _passedEnemyText, _activeEnemyText, _starText })
            if (text != null) text.text = "";
    }

    private void OnDisable()
    {
        if (_boundWave != null) _boundWave.OnWaveStarted -= HandleWaveStarted;
        _boundWave = null;
        Clear();
    }
}
