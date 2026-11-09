using System.Collections.Generic;
using LJTest;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
public class PauseMenuController : MonoBehaviour
{
    [Header("화면 — Controller는 이 화면들 밖에 배치")]
    [SerializeField] private GameObject _pauseMenuRoot;
    [SerializeField] private GameObject _optionsRoot;
    [SerializeField] private GameObject _helpRoot;
    [SerializeField] private GameObject _dimRoot;
    [SerializeField] private GameObject _resumeButton;
    [SerializeField] private GameObject _optionsFirstSelected;
    [SerializeField] private GameObject _helpFirstSelected;

    [Header("T 인벤토리")]
    [SerializeField] private GameObject _inventoryRoot;
    private UnityEngine.UI.Button _inventoryCloseButton;
    private bool _inventoryOpen;

    [Header("결과 화면")]
    [SerializeField] private GameObject _resultScreen;
    [SerializeField] private GameObject _resultStatistics;
    [SerializeField] private GameObject _spellbookScreen;
    [SerializeField] private GameObject _spellbookDefaultReturn;
    [SerializeField] private UpgradeDetailPreview _upgradeDetail;
    [SerializeField] private GameObject _campaignScreen;

    [Header("옵션 하위 화면 — 게임플레이·소리·그래픽·조작")]
    [SerializeField] private GameObject[] _settingsRoots = new GameObject[0];

    [Header("재시도·타이틀 이동 확인")]
    [SerializeField] private GameObject _confirmRoot;
    [SerializeField] private TMP_Text _confirmTitleText;
    [SerializeField] private TMP_Text _confirmDescriptionText;
    [SerializeField] private GameObject _confirmCancelButton;

    [Header("기존 플레이어 커서 API")]
    [SerializeField] private TestScene _playerTestScene;

    [Header("Pause 중 정지할 입력 컴포넌트 — GameObject가 아닌 컴포넌트")]
    [SerializeField] private Behaviour[] _inputBehaviours = new Behaviour[0];

    [Header("시간")]
    [SerializeField] private bool _freezeTime = true;

    [Header("씬 이동 — Build Profiles의 Scene List에 등록 필요")]
    [SerializeField] private string _titleScenePath = "Assets/01. Scenes/!_Title.unity";

    private readonly List<Behaviour> _disabledInputs = new List<Behaviour>();
    private readonly List<GameObject> _activatedPopupParents = new List<GameObject>();
    private readonly List<UnityEngine.UI.Button> _campaignCloseButtons = new List<UnityEngine.UI.Button>();
    private readonly List<UnityEngine.UI.Button> _pauseRetryButtons = new List<UnityEngine.UI.Button>();
    private readonly List<UnityEngine.UI.Button> _pauseExitButtons = new List<UnityEngine.UI.Button>();
    private readonly List<UnityEngine.UI.Button> _confirmButtons = new List<UnityEngine.UI.Button>();
    private readonly List<UnityEngine.UI.Button> _cancelButtons = new List<UnityEngine.UI.Button>();
    private readonly List<UnityEngine.UI.Button> _storeEntryButtons = new List<UnityEngine.UI.Button>();
    private readonly List<UnityEngine.UI.Button> _storeBackButtons = new List<UnityEngine.UI.Button>();
    private bool _isPaused;
    private bool _optionsOpen;
    private bool _helpOpen;
    private bool _timeFrozen;
    private bool _isSceneChanging;
    private bool _confirmationFromResult;
    private bool _spellbookFromResult;
    private bool _campaignFromResult;
    private float _previousTimeScale;
    private CursorLockMode _previousCursorLock;
    private bool _previousCursorVisible;
    private GameObject _previousSelection;

    private enum ConfirmAction { None, Retry, ExitToTitle }
    private ConfirmAction _pendingAction;

    public bool IsPaused => _isPaused;

    private bool IsResultVisible =>
        (_resultScreen != null && _resultScreen.activeInHierarchy) ||
        (_resultStatistics != null && _resultStatistics.activeInHierarchy);

    private void Awake()
    {
        // The template dim belongs to PopupRoot, which also contains a back button.
        // Keep pause shading separate so opening it never activates that popup.
        if (_dimRoot != null && _pauseMenuRoot != null)
        {
            GameObject pauseDim = Instantiate(_dimRoot, _pauseMenuRoot.transform.parent, false);
            pauseDim.name = "PauseDimmedBackground";
            pauseDim.transform.SetSiblingIndex(_pauseMenuRoot.transform.GetSiblingIndex());
            pauseDim.SetActive(false);
            _dimRoot = pauseDim;
        }
        if (_pauseMenuRoot != null)
            foreach (var button in _pauseMenuRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                bool retry = button.name == "RetryButton";
                bool exit = button.name == "ExitButton";
                if (!retry && !exit) continue;
                string method = retry ? nameof(RequestRetry) : nameof(RequestExitToTitle);
                bool connected = false;
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    connected |= button.onClick.GetPersistentTarget(i) == this &&
                        button.onClick.GetPersistentMethodName(i) == method &&
                        button.onClick.GetPersistentListenerState(i) != UnityEngine.Events.UnityEventCallState.Off;
                if (connected) continue;
                if (retry)
                {
                    button.onClick.AddListener(RequestRetry);
                    _pauseRetryButtons.Add(button);
                }
                else
                {
                    button.onClick.AddListener(RequestExitToTitle);
                    _pauseExitButtons.Add(button);
                }
            }
        ResolveUpgradeDetail();
        ResolveStoreScreen();
        ResolveInventory();
        if (_spellbookScreen != null)
            foreach (var button in _spellbookScreen.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                string text = label != null ? label.text.Trim() : string.Empty;
                if (button.name != "BackButton" && text != "뒤로" && text != "닫기") continue;
                button.onClick.AddListener(CloseSpellbook);
                _storeBackButtons.Add(button);
            }
        foreach (GameObject sceneRoot in gameObject.scene.GetRootGameObjects())
            foreach (var button in sceneRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label == null || label.text.Replace(" ", "").Replace("\n", "").Trim() != "업그레이드") continue;
                button.onClick.AddListener(OpenStore);
                _storeEntryButtons.Add(button);
            }
        if (_resultScreen == null)
        {
            WaveSummaryHudBinder summary = FindFirstObjectByType<WaveSummaryHudBinder>();
            if (summary != null) _resultScreen = summary.ResultPanel;
        }

        foreach (GameObject root in new[] { _pauseMenuRoot, _optionsRoot, _helpRoot, _dimRoot, _confirmRoot })
        {
            if (root != null && transform.IsChildOf(root.transform))
            {
                Debug.LogError("PauseMenuController는 숨겨지는 메뉴·옵션·암막 밖에 배치하세요.", this);
                enabled = false;
                return;
            }
        }
        Show(_pauseMenuRoot, false);
        Show(_optionsRoot, false);
        Show(_helpRoot, false);
        Show(_dimRoot, false);
        HideSettings();
        ResolveConfirmation();
        HideConfirmation();
        if (_confirmRoot != null)
        {
            foreach (var button in _confirmRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                string text = label != null ? label.text.Trim() : string.Empty;
                bool confirm = button.name == "ConfirmButton" || text == "확인";
                bool cancel = button.name == "CancelButton" || text == "취소";
                if (!confirm && !cancel) continue;
                string method = confirm ? nameof(ConfirmPendingAction) : nameof(CancelConfirmation);
                bool connected = false;
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    connected |= button.onClick.GetPersistentTarget(i) == this &&
                        button.onClick.GetPersistentMethodName(i) == method;
                if (connected || (confirm ? _confirmButtons.Contains(button) : _cancelButtons.Contains(button))) continue;
                if (confirm)
                {
                    button.onClick.AddListener(ConfirmPendingAction);
                    _confirmButtons.Add(button);
                }
                else
                {
                    button.onClick.AddListener(CancelConfirmation);
                    _cancelButtons.Add(button);
                }
            }
        }
        if (_campaignScreen != null)
        {
            foreach (var button in _campaignScreen.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                string text = label != null ? label.text.Trim() : string.Empty;
                if (button.name != "BackButton" && text != "닫기" && text != "뒤로") continue;
                button.onClick.AddListener(CloseCampaignToResult);
                _campaignCloseButtons.Add(button);
            }
        }
    }

    private void LateUpdate()
    {
        if (_inventoryOpen && (_inventoryRoot == null || !_inventoryRoot.activeInHierarchy))
            CloseInventory();
        if (!_isSceneChanging && _spellbookFromResult && _pendingAction == ConfirmAction.None &&
            _spellbookScreen != null && !_spellbookScreen.activeInHierarchy &&
            (_upgradeDetail == null || !_upgradeDetail.gameObject.activeInHierarchy))
            CloseSpellbook();
        // A legacy close listener may hide the campaign directly.
        if (!_isSceneChanging && _campaignFromResult && _campaignScreen != null &&
            !_campaignScreen.activeInHierarchy)
            CloseCampaignToResult();
    }

    private void OnDestroy()
    {
        foreach (var button in _pauseRetryButtons)
            if (button != null) button.onClick.RemoveListener(RequestRetry);
        foreach (var button in _pauseExitButtons)
            if (button != null) button.onClick.RemoveListener(RequestExitToTitle);
        if (_inventoryCloseButton != null)
            _inventoryCloseButton.onClick.RemoveListener(CloseInventory);
        foreach (var button in _storeBackButtons)
            if (button != null) button.onClick.RemoveListener(CloseSpellbook);
        foreach (var button in _storeEntryButtons)
            if (button != null) button.onClick.RemoveListener(OpenStore);
        foreach (var button in _confirmButtons)
            if (button != null) button.onClick.RemoveListener(ConfirmPendingAction);
        foreach (var button in _cancelButtons)
            if (button != null) button.onClick.RemoveListener(CancelConfirmation);
        foreach (var button in _campaignCloseButtons)
            if (button != null) button.onClick.RemoveListener(CloseCampaignToResult);
    }

    private void Update()
    {
        if (_isSceneChanging) return;
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && (selected.GetComponent<TMP_InputField>() != null ||
                selected.GetComponent<UnityEngine.UI.InputField>() != null)) return;
            if (_inventoryOpen)
            {
                if (!TryCloseChildScreen()) CloseInventory();
            }
            else OpenInventory();
            return;
        }
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        ResolveUpgradeDetail();

        if (_pendingAction != ConfirmAction.None)
        {
            CancelConfirmation();
            return;
        }

        // Escape is handled here once. A child consumes it before any parent can close.
        if (TryCloseChildScreen()) return;

        if (_inventoryOpen)
        {
            CloseInventory();
            return;
        }

        if (HasOpenSettings())
        {
            if (!_isPaused) BeginPause();
            BackToOptions();
            return;
        }

        if (_campaignFromResult && _campaignScreen != null &&
            _campaignScreen.activeInHierarchy)
        {
            CloseCampaignToResult();
            return;
        }

        if (_upgradeDetail != null && _upgradeDetail.gameObject.activeInHierarchy)
        {
            _upgradeDetail.Close();
            return;
        }

        if ((_spellbookFromResult || HasWaveResultContext()) && _spellbookScreen != null &&
            _spellbookScreen.activeInHierarchy)
        {
            CloseSpellbook();
            return;
        }

        if (!_isPaused &&
            _resultStatistics != null &&
            _resultStatistics.activeInHierarchy)
        {
            _resultStatistics.SetActive(false);

            if (_resultScreen != null)
                _resultScreen.SetActive(true);

            return;
        }

        if (IsResultVisible) return;

        if (!_isPaused) OpenPause();
        else if (_optionsOpen || _helpOpen) BackToPause();
        else Resume();
    }

    private bool TryCloseChildScreen()
    {
        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        foreach (GameObject root in roots)
            foreach (SpellbookCardPreview card in root.GetComponentsInChildren<SpellbookCardPreview>(true))
                if (card.TryCancelPayment()) return true;
        foreach (GameObject root in roots)
            foreach (ConsumableScreenBinder inventory in root.GetComponentsInChildren<ConsumableScreenBinder>(true))
                if (inventory.TryCloseConsumables()) return true;
        foreach (GameObject root in roots)
            foreach (CompanionHirePanel hire in root.GetComponentsInChildren<CompanionHirePanel>(true))
                if (hire.TryCloseHirePanel()) return true;
        foreach (GameObject root in roots)
            foreach (StageResultUiBinder results in root.GetComponentsInChildren<StageResultUiBinder>(true))
                if (results.TryCloseStatistics()) return true;
        return false;
    }

    private void ResolveInventory()
    {
        if (_inventoryRoot == null)
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == "InventoryScreen")
                    {
                        _inventoryRoot = child.gameObject;
                        break;
                    }
        if (_inventoryRoot == null || _inventoryCloseButton != null) return;
        foreach (var button in _inventoryRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            if (button.name == "CloseButton")
            {
                _inventoryCloseButton = button;
                button.onClick.AddListener(CloseInventory);
                break;
            }
    }

    public void OpenInventory()
    {
        if (_isSceneChanging || _isPaused || IsResultVisible || Time.timeScale <= 0f ||
            _playerTestScene == null || !_playerTestScene.IsInitialized ||
            !_playerTestScene.IsStageReady || _playerTestScene.IsEndingStage ||
            (_spellbookScreen != null && _spellbookScreen.activeInHierarchy) ||
            (_upgradeDetail != null && _upgradeDetail.gameObject.activeInHierarchy)) return;
        ResolveInventory();
        if (_inventoryRoot == null || _inventoryRoot.activeInHierarchy) return;
        BeginPause();
        _inventoryOpen = true;
        ShowWithParents(_inventoryRoot);
        Select(_inventoryCloseButton != null ? _inventoryCloseButton.gameObject : null);
    }

    public void CloseInventory()
    {
        if (!_inventoryOpen) return;
        Resume();
    }

    public void OpenPause()
    {
        if (_isSceneChanging || _isPaused || _pauseMenuRoot == null || IsResultVisible ||
            (_upgradeDetail != null && _upgradeDetail.gameObject.activeInHierarchy) ||
            _spellbookFromResult || _campaignFromResult) return;

        BeginPause();
        BackToPause();
    }

    private void ResolveUpgradeDetail()
    {
        if (_upgradeDetail != null && _upgradeDetail.gameObject.scene == gameObject.scene) return;
        _upgradeDetail = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            _upgradeDetail = root.GetComponentInChildren<UpgradeDetailPreview>(true);
            if (_upgradeDetail != null) return;
        }
    }

    private void BeginPause()
    {
        _previousTimeScale = Time.timeScale;
        _previousCursorLock = Cursor.lockState;
        _previousCursorVisible = Cursor.visible;
        _previousSelection = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject : null;
        _isPaused = true;

        if (_playerTestScene != null)
            _playerTestScene.SetCursorLocked(false);

        _disabledInputs.Clear();
        foreach (Behaviour input in _inputBehaviours)
        {
            if (input == null || input == this || !input.enabled || _disabledInputs.Contains(input))
                continue;
            _disabledInputs.Add(input);
            input.enabled = false;
        }

        _timeFrozen = _freezeTime;
        if (_timeFrozen) Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        if (_isSceneChanging || !_isPaused) return;

        if (_inventoryOpen)
        {
            _inventoryOpen = false;
            Show(_inventoryRoot, false);
        }

        _isPaused = false;
        _confirmationFromResult = false;
        _optionsOpen = false;
        _helpOpen = false;
        HideConfirmation();
        HideSettings();
        Show(_pauseMenuRoot, false);
        Show(_optionsRoot, false);
        Show(_helpRoot, false);
        Show(_dimRoot, false);

        RestorePopupParents();

        foreach (Behaviour input in _disabledInputs)
        {
            if (input != null) input.enabled = true;
        }
        _disabledInputs.Clear();

        if (_playerTestScene != null)
            _playerTestScene.SetCursorLocked(_previousCursorLock == CursorLockMode.Locked);
        Cursor.lockState = _previousCursorLock;
        Cursor.visible = _previousCursorVisible;
        if (_timeFrozen) Time.timeScale = _previousTimeScale;
        _timeFrozen = false;
        Select(_previousSelection);
    }

    public void OpenOptions()
    {
        if (!_isPaused || _isSceneChanging || _pendingAction != ConfirmAction.None || _optionsRoot == null) return;
        BackToOptions();
    }

    public void BackToOptions()
    {
        if (!_isPaused || _isSceneChanging || _pendingAction != ConfirmAction.None || _optionsRoot == null) return;
        HideSettings();
        _optionsOpen = true;
        _helpOpen = false;
        Show(_helpRoot, false);
        Show(_pauseMenuRoot, false);
        Show(_dimRoot, false);
        Show(_optionsRoot, true);
        Select(_optionsFirstSelected);
    }

    public void OpenHelp()
    {
        if (!_isPaused || _isSceneChanging || _pendingAction != ConfirmAction.None || _helpRoot == null) return;
        HideSettings();
        _helpOpen = true;
        _optionsOpen = false;
        Show(_pauseMenuRoot, false);
        Show(_optionsRoot, false);
        Show(_dimRoot, false);
        Show(_helpRoot, true);
        Select(_helpFirstSelected);
    }

    public void BackToPause()
    {
        if (!_isPaused || _isSceneChanging) return;
        HideConfirmation();
        HideSettings();
        _optionsOpen = false;
        _helpOpen = false;
        Show(_optionsRoot, false);
        Show(_helpRoot, false);
        ShowWithParents(_dimRoot);
        ShowWithParents(_pauseMenuRoot);
        Select(_resumeButton);
    }

    private bool HasOpenSettings()
    {
        foreach (GameObject root in _settingsRoots)
        {
            if (root != null && root.activeInHierarchy) return true;
        }
        return false;
    }

    private void HideSettings()
    {
        foreach (GameObject root in _settingsRoots)
            Show(root, false);
    }

    public void Retry()
    {
        LoadScene(SceneManager.GetActiveScene().path, false);
    }

    public void ExitToTitle()
    {
        // Serialized components can retain the old default after the script changes.
        if (_titleScenePath == "Assets/01. Scenes/Title.unity")
            _titleScenePath = "Assets/01. Scenes/!_Title.unity";
        LoadScene(_titleScenePath, true);
    }

    public void RequestRetry()
    {
        _confirmationFromResult = false;
        OpenConfirmation(ConfirmAction.Retry);
    }

    private void ResolveStoreScreen()
    {
        if (_spellbookScreen != null && _spellbookScreen.scene == gameObject.scene &&
            (_spellbookScreen.name.StartsWith("StoreScreen") || _spellbookScreen.name.StartsWith("SpellbookScreen"))) return;
        _spellbookScreen = null;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith("StoreScreen") || child.name.StartsWith("SpellbookScreen"))
                {
                    _spellbookScreen = child.gameObject;
                    return;
                }
    }

    public void OpenStore()
    {
        if (_isSceneChanging || _pendingAction != ConfirmAction.None) return;
        ResolveStoreScreen();
        ResolveUpgradeDetail();
        if (_spellbookScreen == null) return;
        if ((_resultScreen != null && _resultScreen.activeInHierarchy) || HasWaveResultContext())
            _spellbookFromResult = true;
        if (_upgradeDetail != null) Show(_upgradeDetail.gameObject, false);
        Show(_spellbookScreen, true);
        if (_spellbookFromResult) Show(_resultScreen, false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OpenSpellbookFromResult()
    {
        ResolveStoreScreen();
        ResolveUpgradeDetail();
        if (_isSceneChanging || _pendingAction != ConfirmAction.None ||
            _spellbookScreen == null || _resultScreen == null) return;

        _spellbookFromResult = true;
        if (_upgradeDetail != null) Show(_upgradeDetail.gameObject, false);
        Show(_spellbookDefaultReturn, false);
        Show(_spellbookScreen, true);
        Show(_resultScreen, false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OpenCampaignFromResult()
    {
        if (_isSceneChanging || _pendingAction != ConfirmAction.None ||
            _campaignScreen == null || _resultScreen == null) return;

        _campaignFromResult = true;
        Show(_campaignScreen, true);
        Show(_resultScreen, false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseCampaignToResult()
    {
        if (_isSceneChanging || !_campaignFromResult) return;

        Show(_campaignScreen, false);
        Show(_resultScreen, true);
        _campaignFromResult = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseSpellbook()
    {
        if (_isSceneChanging) return;
        bool returnToResult = _spellbookFromResult || HasWaveResultContext();
        _spellbookFromResult = false;
        Show(_spellbookScreen, false);
        Show(returnToResult ? _resultScreen : _spellbookDefaultReturn, true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Select(null);
    }

    private bool HasWaveResultContext()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            var summary = root.GetComponentInChildren<WaveSummaryHudBinder>(true);
            if (summary != null && summary.IsOpen) return true;
        }
        return false;
    }

    public void RequestResultRetry()
    {
        RequestStageResultRetry();
    }

    // Final stage results restart the scene rather than a wave in an ended stage.
    public void RequestStageResultRetry()
    {
        if (_isSceneChanging || _pendingAction != ConfirmAction.None) return;
        if (!_isPaused) BeginPause();
        _confirmationFromResult = true;
        OpenConfirmation(ConfirmAction.Retry);
        if (_pendingAction == ConfirmAction.None) Resume();
    }

    public void RequestExitToTitle()
    {
        OpenConfirmation(ConfirmAction.ExitToTitle);
    }

    private void ResolveConfirmation()
    {
        if (_confirmRoot == null)
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == "ConfirmDialogRoot")
                    {
                        _confirmRoot = child.gameObject;
                        break;
                    }
        if (_confirmRoot == null) return;
        foreach (TMP_Text label in _confirmRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (_confirmTitleText == null && label.name == "TitleText") _confirmTitleText = label;
            if (_confirmDescriptionText == null && label.name == "DescriptionText") _confirmDescriptionText = label;
        }
        foreach (var button in _confirmRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            if (button.name == "ConfirmButton" && !_confirmButtons.Contains(button))
            {
                button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                button.onClick.AddListener(ConfirmPendingAction);
                _confirmButtons.Add(button);
            }
            if (button.name == "CancelButton")
            {
                if (_confirmCancelButton == null) _confirmCancelButton = button.gameObject;
                if (_cancelButtons.Contains(button)) continue;
                button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                button.onClick.AddListener(CancelConfirmation);
                _cancelButtons.Add(button);
            }
        }
    }

    private void OpenConfirmation(ConfirmAction action)
    {
        if (!_isPaused || _isSceneChanging || _pendingAction != ConfirmAction.None) return;
        ResolveConfirmation();
        if (_confirmRoot == null || _confirmTitleText == null || _confirmDescriptionText == null)
        {
            Debug.LogError("확인창 Root·제목·설명을 연결하세요.", this);
            return;
        }

        HideSettings();
        _optionsOpen = false;
        _helpOpen = false;
        _pendingAction = action;
        bool retry = action == ConfirmAction.Retry;
        _confirmTitleText.text = retry ? "1웨이브부터 다시 시작하시겠습니까?" : "타이틀로 돌아가시겠습니까?";
        _confirmDescriptionText.text = retry
            ? "현재 진행을 초기화하고 1웨이브부터 다시 시작합니다."
            : "현재 진행은 종료됩니다.";
        Show(_optionsRoot, false);
        Show(_helpRoot, false);
        Show(_pauseMenuRoot, false);
        ShowWithParents(_dimRoot);
        if (_pauseMenuRoot != null && _confirmRoot.transform.parent != _pauseMenuRoot.transform.parent)
            _confirmRoot.transform.SetParent(_pauseMenuRoot.transform.parent, false);
        // A hidden template may be serialized with zero scale. Activation alone
        // cannot make it visible; retain all non-zero authored scale values.
        Vector3 scale = _confirmRoot.transform.localScale;
        _confirmRoot.transform.localScale = new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : scale.z);
        ShowWithParents(_confirmRoot);
        _confirmRoot.transform.SetAsLastSibling();
        Select(_confirmCancelButton);
    }

    public void CancelConfirmation()
    {
        if (!_isPaused || _isSceneChanging || _pendingAction == ConfirmAction.None) return;
        if (_confirmationFromResult) Resume();
        else BackToPause();
    }

    public void ConfirmPendingAction()
    {
        if (!_isPaused || _isSceneChanging) return;
        ConfirmAction action = _pendingAction;
        if (action == ConfirmAction.Retry) Retry();
        else if (action == ConfirmAction.ExitToTitle) ExitToTitle();

        // 씬 로드 실패 시 확인창을 유지해 취소하거나 다시 시도할 수 있게 한다.
        if (_isSceneChanging) HideConfirmation();
    }

    private void HideConfirmation()
    {
        _pendingAction = ConfirmAction.None;
        Show(_confirmRoot, false);
    }

    private void LoadScene(string path, bool showCursor)
    {
        if (!_isPaused || _isSceneChanging) return;
        if (string.IsNullOrWhiteSpace(path) || !Application.CanStreamedLevelBeLoaded(path))
        {
            Debug.LogError("이동할 씬을 Build Profiles의 Scene List에 활성 상태로 등록하세요: " + path, this);
            return;
        }

        AsyncOperation operation;
        try
        {
            operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
        }
        catch (System.Exception error)
        {
            Debug.LogError("씬 이동을 시작하지 못했습니다: " + error.Message, this);
            return;
        }
        if (operation == null) return;

        // 이전 씬 입력은 차단한 채 제거하고 새 씬에서 초기화한다.
        _isSceneChanging = true;
        _isPaused = false;
        _timeFrozen = false;
        Time.timeScale = 1f;
        if (showCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            operation.completed += _ =>
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            };
        }
    }

    private void OnDisable()
    {
        Resume();
    }

    private static void Show(GameObject root, bool visible)
    {
        if (root != null && root.activeSelf != visible) root.SetActive(visible);
    }

    private void ShowWithParents(GameObject root)
    {
        if (root == null) return;

        List<GameObject> parents = new List<GameObject>();
        for (Transform parent = root.transform.parent; parent != null; parent = parent.parent)
        {
            if (!parent.gameObject.activeSelf) parents.Add(parent.gameObject);
        }

        for (int i = parents.Count - 1; i >= 0; i--)
        {
            _activatedPopupParents.Add(parents[i]);
            parents[i].SetActive(true);
        }

        Show(root, true);
    }

    private void RestorePopupParents()
    {
        for (int i = _activatedPopupParents.Count - 1; i >= 0; i--)
            Show(_activatedPopupParents[i], false);

        _activatedPopupParents.Clear();
    }

    private static void Select(GameObject target)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target);
    }
}
