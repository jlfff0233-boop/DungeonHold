using System;
using System.Collections.Generic;
using LJTest;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-5000)]
public class StageResultUiBinder : MonoBehaviour
{
    private sealed class Snapshot
    {
        public int StageId, Kills, Passed, Items, MaxCombo, Stars;
        public bool? Succeeded;
        public double Seconds;
        public long Paid, ComboPaid;
        public bool Settled;
        public string Status;
    }

    private TestScene _scene;
    private StageRewardController _rewards;
    private Hero _hero;
    private WaveController _wave;
    private Stage1Controller _stage;
    private PauseMenuController _pause;
    private GameObject _result, _statistics, _hud;
    private TMP_Text _summary;
    private Snapshot _snapshot;
    private int _trackingStage;
    private int _maxCombo;
    private long _paid, _comboPaid;
    private double _startedAt;
    private bool _endRequested, _hudWasActive;
    private bool _settlementFailed;
    private Button _settlementRetryButton;
    private GameObject _statisticsReturnSelection;
    private readonly HashSet<long> _paidLives = new HashSet<long>();
    private readonly List<Behaviour> _suppressedSummaries = new List<Behaviour>();
    private readonly List<InputHandler> _disabledInputs = new List<InputHandler>();
    private readonly Dictionary<Button, Button.ButtonClickedEvent> _originalClicks = new Dictionary<Button, Button.ButtonClickedEvent>();
    private readonly Dictionary<Button, UnityEngine.Events.UnityAction> _ownedClicks = new Dictionary<Button, UnityEngine.Events.UnityAction>();
    private readonly List<Button> _settledActions = new List<Button>();

    private void Start()
    {
        _pause = GetComponent<PauseMenuController>();
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (_wave == null)
                foreach (WaveController candidate in root.GetComponentsInChildren<WaveController>(true))
                    if (candidate.isActiveAndEnabled) { _wave = candidate; break; }
            if (_stage == null)
                foreach (Stage1Controller candidate in root.GetComponentsInChildren<Stage1Controller>(true))
                    if (candidate.isActiveAndEnabled) { _stage = candidate; break; }
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "ResultScreen") _result = child.gameObject;
                if (child.name == "ResultStatistics") _statistics = child.gameObject;
                if (child.name == "Canvas_HUD") _hud = child.gameObject;
            }
        }
        if (_result == null) return;
        // Replace the legacy all-waves summary only when a real result screen exists.
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (WaveSummaryHudBinder summary in root.GetComponentsInChildren<WaveSummaryHudBinder>(true))
                if (summary.enabled)
                {
                    _suppressedSummaries.Add(summary);
                    summary.enabled = false;
                }
        BuildSummary();
        WireButtons();
        Bind();
    }

    private void LateUpdate()
    {
        if (_result == null) return;
        Bind();
        TrackStage();
        if (_scene == null || !_scene.IsInitialized) return;
        if (_snapshot == null && _trackingStage != 0)
        {
            if (_stage != null && _stage.isActiveAndEnabled && _stage.HasFinished)
                Capture(_stage.HasSucceeded, _stage.StarCount, _stage.FinalPassedCount);
            else if (_stage == null && _wave != null && _wave.HasCompletedAllWaves)
                Capture(null, 0, _wave.PassedEnemyCount);
        }
        if (_snapshot != null && !_snapshot.Settled)
        {
            // Include payouts that happened later in the same final-kill notification.
            _snapshot.Paid = _paid;
            _snapshot.ComboPaid = _comboPaid;
            if (_snapshot.Succeeded.HasValue && !_endRequested) RequestSettlement();
        }
        if (_snapshot != null && (_result.activeInHierarchy || _statistics != null && _statistics.activeInHierarchy))
        {
            Draw();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void Bind()
    {
        if (_scene == null)
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                TestScene candidate = root.GetComponentInChildren<TestScene>(true);
                if (candidate != null && candidate.isActiveAndEnabled) { _scene = candidate; break; }
            }
        if (_scene == null) return;
        StageRewardController rewards = _scene.Rewards;
        Hero hero = _scene.Hero;
        if (_rewards != rewards)
        {
            if (_rewards != null)
            {
                _rewards.OnComboChanged -= ComboChanged;
                _rewards.OnRewardPaid -= RewardPaid;
            }
            _scene.OnStageEndProcessed -= EndProcessed;
            _scene.OnStageEndProcessed += EndProcessed;
            _rewards = rewards;
            if (_rewards != null)
            {
                _rewards.OnComboChanged += ComboChanged;
                _rewards.OnRewardPaid += RewardPaid;
            }
        }
        if (_hero != hero)
        {
            if (_hero != null) _hero.OnReviveExhausted -= ReviveExhausted;
            _hero = hero;
            if (_hero != null) _hero.OnReviveExhausted += ReviveExhausted;
        }
    }

    private void TrackStage()
    {
        int stage = _rewards != null ? _rewards.CurrentStageId : 0;
        if (stage == 0) return;
        if (stage == _trackingStage)
        {
            _maxCombo = Mathf.Max(_maxCombo, _rewards.ComboKillCount);
            return;
        }
        ResetPresentation();
        _trackingStage = stage;
        _startedAt = Time.timeAsDouble;
    }

    private void ComboChanged(int count, int next)
    {
        TrackStage();
        _maxCombo = Mathf.Max(_maxCombo, count);
    }

    private void RewardPaid(RewardRequest request)
    {
        TrackStage();
        if (request.StageId != _trackingStage || !_paidLives.Add(request.LifeId)) return;
        _paid += request.TotalAmount;
        _comboPaid += request.ComboAmount;
        if (_snapshot != null) { _snapshot.Paid = _paid; _snapshot.ComboPaid = _comboPaid; }
    }

    private void ReviveExhausted(DamageInfo damage)
    {
        TrackStage();
        if (_trackingStage != 0 && _snapshot == null)
            Capture(false, 0, _wave != null ? _wave.PassedEnemyCount : 0);
    }

    private void Capture(bool? success, int stars, int passed)
    {
        _snapshot = new Snapshot { StageId = _trackingStage, Succeeded = success, Stars = stars,
            Kills = _wave != null ? _wave.DiedEnemyCount : 0, Passed = passed,
            Items = _scene.Inventory.UsedItemCount, MaxCombo = _maxCombo,
            Seconds = Math.Max(0, Time.timeAsDouble - _startedAt), Paid = _paid, ComboPaid = _comboPaid,
            Status = success.HasValue ? "종료·정산 처리 중" : "승패 판정 API 미연결 · 정산 미요청" };
        _snapshot.Status = success.HasValue ? "종료·정산 처리 중" : _snapshot.Status;
        _hudWasActive = _hud != null && _hud.activeSelf;
        if (_hud != null) _hud.SetActive(false);
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (InputHandler input in root.GetComponentsInChildren<InputHandler>(true))
                if (input.enabled) { _disabledInputs.Add(input); input.enabled = false; }
        if (_statistics != null) _statistics.SetActive(false);
        ShowWithParents(_result.transform);
        // Inactive screen components can add legacy listeners in their first Awake.
        foreach (var click in _ownedClicks)
            if (click.Key != null)
            {
                click.Key.onClick = new Button.ButtonClickedEvent();
                click.Key.onClick.AddListener(click.Value);
            }
        Draw();
    }

    private void RequestSettlement()
    {
        if (_scene.IsEndingStage) { _endRequested = true; return; }
        _endRequested = true;
        _settlementFailed = false;
        if (_scene.IsStageCleanupPending)
        {
            if (!_scene.TryCompleteStageCleanup())
            {
                _settlementFailed = true;
                _snapshot.Status = "정산 후 종료 정리에 실패했습니다. 다시 시도해 주세요.";
            }
            return;
        }
        if (!_scene.RequestEndStage(_snapshot.StageId, false))
        {
            _settlementFailed = true;
            _snapshot.Status = "종료 요청이 거절되었습니다. 정산 재시도로 다시 요청할 수 있습니다.";
        }
    }

    private void EndProcessed(int stageId, bool abandoned, bool succeeded)
    {
        if (_snapshot == null && stageId == _trackingStage)
            Capture(abandoned ? false : _stage != null && _stage.HasFinished ? (bool?)_stage.HasSucceeded : null,
                _stage != null ? _stage.StarCount : 0, _wave != null ? _wave.PassedEnemyCount : 0);
        if (_snapshot == null || _snapshot.StageId != stageId) return;
        _snapshot.Settled = succeeded;
        _settlementFailed = !succeeded;
        _snapshot.Items = succeeded ? _scene.LastStageUsedItemCount : _snapshot.Items;
        _snapshot.Paid = _paid;
        _snapshot.ComboPaid = _comboPaid;
        _snapshot.Status = abandoned ? "도전 포기 처리됨" : succeeded
            ? "종료·정산 완료 · 스테이지 재화는 종료 시 정리됩니다." : "종료·정산 실패 · 다시 시도해 주세요.";
        Draw();
    }

    private void RetrySettlement()
    {
        if (_snapshot == null || _snapshot.Settled || !_snapshot.Succeeded.HasValue || _scene.IsEndingStage) return;
        _snapshot.Status = "종료·정산 재시도 중";
        _endRequested = false;
        RequestSettlement();
    }

    private void Draw()
    {
        if (_snapshot == null) return;
        string outcome = _snapshot.Succeeded == true ? "승리!" : _snapshot.Succeeded == false ? "실패!" : "승패 미확인";
        SetText(_result, "OutcomeText", outcome);
        SetText(_result, "StageNameText", _stage != null ? "스테이지 1" : "도전 결과");
        SetText(_result, "CurrencyText", _scene.Currency.GetBalance(CurrencyType.Permanent).ToString("N0"));
        if (_summary != null) _summary.text = $"처치 {_snapshot.Kills:N0} · 통과 {_snapshot.Passed:N0}\n" +
            $"사용 아이템 {_snapshot.Items:N0} · 최대 콤보 {_snapshot.MaxCombo:N0}\n" +
            $"관측 지급 재화 {_snapshot.Paid:N0} (스테이지)\n콤보 보너스 {_snapshot.ComboPaid:N0}\n" +
            (_snapshot.Succeeded == false && _stage != null && _stage.HasFailed
                ? $"실패 사유: {_stage.FailureReason}\n" : "") + _snapshot.Status;
        foreach (string objectName in new[] { "BonusRoot", "RewardCard", "NewRecordText", "ScoreRoot" })
            SetActive(_result, objectName, false);
        SetActive(_result, "VictoryArtwork", _snapshot.Settled && _snapshot.Succeeded == true);
        Transform rating = Find(_result, "RatingRoot");
        if (rating != null)
            for (int i = 0; i < rating.childCount; i++) rating.GetChild(i).gameObject.SetActive(_snapshot.Settled && i < _snapshot.Stars);
        if (_statistics != null)
        {
            foreach (TMP_Text value in _statistics.GetComponentsInChildren<TMP_Text>(true))
                if (value.name == "ValueText") value.text = "미집계";
            foreach (TMP_Text label in _statistics.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name != "LabelText") continue;
                Transform valueRoot = label.transform.parent.Find("ValueText");
                TMP_Text value = valueRoot != null ? valueRoot.GetComponent<TMP_Text>() : null;
                if (value == null) continue;
                switch (Normalize(label.text))
                {
                    case "처치한적": value.text = _snapshot.Kills.ToString("N0"); break;
                    case "통과한적": value.text = _snapshot.Passed.ToString("N0"); break;
                    case "최대연속처치": case "최대콤보": value.text = _snapshot.MaxCombo.ToString("N0"); break;
                    case "완료시간": case "관측진행시간":
                        label.text = "관측 진행 시간";
                        value.text = TimeSpan.FromSeconds(_snapshot.Seconds).ToString(@"mm\:ss"); break;
                    case "획득한추가재화": case "관측콤보보너스":
                        label.text = "관측 콤보 보너스"; value.text = _snapshot.ComboPaid.ToString("N0"); break;
                    default: value.text = "미집계"; break;
                }
            }
            Transform stars = Find(_statistics, "RatingIcons");
            if (stars != null)
                for (int i = 0; i < stars.childCount; i++) stars.GetChild(i).gameObject.SetActive(_snapshot.Settled && i < _snapshot.Stars);
        }
        foreach (Button button in _settledActions) if (button != null) button.interactable = _snapshot.Settled;
        if (_settlementRetryButton != null) _settlementRetryButton.gameObject.SetActive(_settlementFailed);
    }

    private void WireButtons()
    {
        foreach (Button button in _result.GetComponentsInChildren<Button>(true))
        {
            string label = ButtonLabel(button);
            if (label == "다시시도") ReplaceClick(button, () => { if (_pause != null) _pause.RequestStageResultRetry(); });
            else if (label == "통계" || label == "상세통계") ReplaceClick(button, OpenStatistics);
            else if (label == "스토리캠페인")
            {
                ReplaceClick(button, () => { if (_pause != null) _pause.OpenCampaignFromResult(); });
                _settledActions.Add(button);
            }
            else if (label == "마법책" || label == "업그레이드" || label == "상점")
            {
                ReplaceClick(button, () => { if (_pause != null) _pause.OpenSpellbookFromResult(); });
                _settledActions.Add(button);
            }
        }
        if (_statistics != null)
            foreach (Button button in _statistics.GetComponentsInChildren<Button>(true))
                if (ButtonLabel(button) == "뒤로" || ButtonLabel(button) == "닫기") ReplaceClick(button, CloseStatistics);
        _settlementRetryButton = AddButton("SettlementRetry", "정산 재시도", new Vector2(0, -120), RetrySettlement);
        _settlementRetryButton.gameObject.SetActive(false);
    }

    private void ReplaceClick(Button button, UnityEngine.Events.UnityAction action)
    {
        if (!_originalClicks.ContainsKey(button)) _originalClicks[button] = button.onClick;
        _ownedClicks[button] = action;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
    }

    private void OpenStatistics()
    {
        if (_statistics == null || _snapshot == null) return;
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        _statisticsReturnSelection = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
        Draw();
        ShowWithParents(_statistics.transform);
        _result.SetActive(false);
        if (eventSystem != null)
        {
            Button first = Array.Find(_statistics.GetComponentsInChildren<Button>(true),
                button => button.isActiveAndEnabled && button.interactable);
            eventSystem.SetSelectedGameObject(first != null ? first.gameObject : null);
        }
    }

    public bool TryCloseStatistics()
    {
        if (_snapshot == null || _statistics == null || !_statistics.activeInHierarchy) return false;
        CloseStatistics();
        return true;
    }

    private void CloseStatistics()
    {
        if (_statistics != null) _statistics.SetActive(false);
        ShowWithParents(_result.transform);
        Draw();
        var eventSystem = UnityEngine.EventSystems.EventSystem.current;
        if (eventSystem != null)
        {
            GameObject selection = _statisticsReturnSelection != null && _statisticsReturnSelection.activeInHierarchy
                ? _statisticsReturnSelection : null;
            if (selection == null)
            {
                Button first = Array.Find(_result.GetComponentsInChildren<Button>(true), button => button.isActiveAndEnabled && button.interactable);
                if (first != null) selection = first.gameObject;
            }
            eventSystem.SetSelectedGameObject(selection);
        }
    }

    private void BuildSummary()
    {
        Transform score = Find(_result, "ScoreRoot");
        RectTransform source = score as RectTransform;
        RectTransform rect = new GameObject("LiveResultSummary", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(_result.transform, false);
        rect.gameObject.layer = _result.layer;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(850, 240);
        rect.anchoredPosition = new Vector2(0, 150);
        if (source != null)
        {
            rect.SetParent(source.parent, false);
            rect.anchorMin = source.anchorMin; rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot; rect.anchoredPosition = source.anchoredPosition; rect.sizeDelta = source.sizeDelta;
        }
        _summary = rect.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_Text style = _result.GetComponentInChildren<TMP_Text>(true);
        if (style != null) _summary.font = style.font;
        _summary.fontSize = 24; _summary.enableAutoSizing = true; _summary.fontSizeMin = 14; _summary.fontSizeMax = 24;
        _summary.color = Color.white; _summary.alignment = TextAlignmentOptions.Center; _summary.raycastTarget = false;
    }

    private Button AddButton(string objectName, string text, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(_result.transform, false); rect.gameObject.layer = _result.layer;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.sizeDelta = new Vector2(260, 50); rect.anchoredPosition = position;
        Image image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.25f, 0.27f, 0.3f, 1);
        Button button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
        RectTransform labelRect = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
        labelRect.SetParent(rect, false); labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one; labelRect.sizeDelta = Vector2.zero;
        TMP_Text label = labelRect.gameObject.AddComponent<TextMeshProUGUI>(); label.font = _summary.font; label.text = text;
        label.fontSize = 22; label.color = Color.white; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        return button;
    }

    private void ResetPresentation()
    {
        _snapshot = null; _trackingStage = 0; _maxCombo = 0; _paid = _comboPaid = 0;
        _endRequested = false; _settlementFailed = false; _paidLives.Clear();
        if (_result != null) _result.SetActive(false);
        if (_statistics != null) _statistics.SetActive(false);
        if (_hud != null && _hudWasActive) _hud.SetActive(true);
        _hudWasActive = false;
        foreach (InputHandler input in _disabledInputs) if (input != null) input.enabled = true;
        _disabledInputs.Clear();
    }

    private static string Normalize(string text) => text.Replace(" ", "").Replace("\n", "").Trim();
    private static string ButtonLabel(Button button)
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        return label != null ? Normalize(label.text) : "";
    }
    private static Transform Find(GameObject root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == objectName) return child;
        return null;
    }
    private static void SetText(GameObject root, string objectName, string text)
    {
        Transform target = Find(root, objectName); TMP_Text label = target != null ? target.GetComponent<TMP_Text>() : null;
        if (label != null) label.text = text;
    }
    private static void SetActive(GameObject root, string objectName, bool active)
    {
        Transform target = Find(root, objectName); if (target != null) target.gameObject.SetActive(active);
    }
    private static void ShowWithParents(Transform target)
    {
        if (target.parent != null && !target.parent.gameObject.activeInHierarchy) ShowWithParents(target.parent);
        target.gameObject.SetActive(true);
    }

    private void OnDestroy()
    {
        if (_scene != null) _scene.OnStageEndProcessed -= EndProcessed;
        if (_hero != null) _hero.OnReviveExhausted -= ReviveExhausted;
        if (_rewards != null) { _rewards.OnComboChanged -= ComboChanged; _rewards.OnRewardPaid -= RewardPaid; }
        foreach (var entry in _originalClicks) if (entry.Key != null) entry.Key.onClick = entry.Value;
        foreach (Behaviour summary in _suppressedSummaries) if (summary != null) summary.enabled = true;
        foreach (InputHandler input in _disabledInputs) if (input != null) input.enabled = true;
    }
}
