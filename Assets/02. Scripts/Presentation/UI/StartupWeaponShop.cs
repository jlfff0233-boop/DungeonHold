using System.Reflection;
using LJTest;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>UI-MapTest 및 !_Play_Stage1 시작 상점. 씬과 게임 로직은 변경하지 않는다.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class StartupWeaponShop : MonoBehaviour
{
    private TestScene _scene;
    private TMP_Text _balance, _status;
    private Button _start;
    private readonly Button[] _choices = new Button[3];
    private readonly TMP_Text[] _labels = new TMP_Text[3];
    private TMP_FontAsset _font;
    private FieldInfo _autoStart;
    private bool _previousAutoStart, _ownsGate, _starting;
    private float _previousTimeScale;
    private int _initialCurrency;
    private readonly System.Collections.Generic.List<PauseMenuController> _pausedMenus =
        new System.Collections.Generic.List<PauseMenuController>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.path != "Assets/01. Scenes/UI-MapTest.unity" &&
            scene.path != "Assets/01. Scenes/!_Play_Stage1.unity") return;
        TestScene player = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<StartupWeaponShop>(true) != null) return;
            if (player == null) player = root.GetComponentInChildren<TestScene>();
        }
        if (player == null || !player.isActiveAndEnabled) return;
        var host = new GameObject("StartupWeaponShop");
        SceneManager.MoveGameObjectToScene(host, scene);
        host.AddComponent<StartupWeaponShop>().Initialize(player);
    }

    private void Initialize(TestScene player)
    {
        _scene = player;
        // There is no public auto-start switch. Borrow only the runtime setting;
        // never serialize it or modify the scene asset or TestScene source.
        _autoStart = typeof(TestScene).GetField("_autoBeginStage", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo currency = typeof(TestScene).GetField("_initialStageCurrency", BindingFlags.Instance | BindingFlags.NonPublic);
        if (_autoStart == null || _autoStart.FieldType != typeof(bool) || currency == null || currency.FieldType != typeof(int))
        {
            Debug.LogError("시작 상점: TestScene의 자동 시작 설정을 확인할 수 없습니다.", this);
            Destroy(gameObject);
            return;
        }
        _previousAutoStart = (bool)_autoStart.GetValue(_scene);
        _initialCurrency = (int)currency.GetValue(_scene);
        _autoStart.SetValue(_scene, false);
        _previousTimeScale = Time.timeScale;
        _ownsGate = true;
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            foreach (PauseMenuController menu in root.GetComponentsInChildren<PauseMenuController>(true))
                if (menu.enabled) { _pausedMenus.Add(menu); menu.enabled = false; }
        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (text.font != null) { _font = text.font; break; }
        Build();
        Hold();
    }

    private void Update()
    {
        if (!_ownsGate) return;
        Hold();
        bool ready = _scene != null && _scene.IsInitialized && !_scene.IsEndingStage;
        _balance.text = ready ? $"영구 재화  {_scene.Currency.GetBalance(CurrencyType.Permanent):N0}" : "플레이 데이터 준비 중";
        for (int i = 0; i < 3; i++)
        {
            WeaponType type = (WeaponType)i;
            int price = 0;
            ShopPurchaseResult state = ready
                ? _scene.GetProductPurchaseState(WeaponShopProduct.GetProductId(type), out price)
                : ShopPurchaseResult.Unavailable;
            bool owned = ready && _scene.IsWeaponUnlocked(type);
            bool selected = ready && _scene.SelectedWeapon == type;
            _labels[i].text = selected ? "선택됨" : owned ? "선택" : state == ShopPurchaseResult.Unavailable
                ? "준비 대기" : price == 0 ? "무료 구매·선택" : $"{price:N0} 골드 구매·선택";
            _choices[i].interactable = !_starting && !selected && ready &&
                (owned || state == ShopPurchaseResult.Success);
        }
        _start.interactable = !_starting && ready && _scene.SelectedWeapon.HasValue;
    }

    private void LateUpdate() { if (_ownsGate) Hold(); }
    private void Hold()
    {
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Choose(int index)
    {
        if (_starting || _scene == null || !_scene.IsInitialized) return;
        WeaponType type = (WeaponType)index;
        ShopPurchaseResult result = _scene.IsWeaponUnlocked(type) ? ShopPurchaseResult.Success
            : _scene.TryPurchaseProduct(WeaponShopProduct.GetProductId(type));
        if (result == ShopPurchaseResult.Success) result = _scene.TrySelectWeapon(type);
        _status.text = result == ShopPurchaseResult.Success ? "무기를 선택했습니다. 플레이 시작을 누르세요."
            : result == ShopPurchaseResult.SaveFailed ? "저장에 실패했습니다. Console을 확인하고 다시 시도하세요."
            : result == ShopPurchaseResult.CannotSpend ? "영구 재화가 부족합니다." : $"무기를 선택할 수 없습니다: {result}";
    }

    private void Begin()
    {
        if (_starting || _scene == null || !_scene.IsInitialized || !_scene.SelectedWeapon.HasValue) return;
        _starting = true;
        if (!_scene.BeginStage(_initialCurrency))
        {
            _starting = false;
            _status.text = "도전 시작에 실패했습니다. Console의 초기화·참조 오류를 확인하세요.";
            return;
        }
        Release();
        Destroy(gameObject);
    }

    private void Release()
    {
        if (!_ownsGate) return;
        _ownsGate = false;
        if (_scene != null) _autoStart.SetValue(_scene, _previousAutoStart);
        Time.timeScale = _previousTimeScale;
        foreach (PauseMenuController menu in _pausedMenus) if (menu != null) menu.enabled = true;
        _pausedMenus.Clear();
        if (_scene != null && _scene.IsStageReady) _scene.SetCursorLocked(true);
    }
    private void OnDestroy() { Release(); }

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
            new GameObject("StartupWeaponShopEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).transform.SetParent(transform);
        RectTransform panel = Box("WeaponSelection", transform, Vector2.zero, new Vector2(1280,720));
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.sizeDelta = Vector2.zero;
        panel.gameObject.AddComponent<Image>().color = new Color(0.035f,0.05f,0.09f,1f);
        Text("Title",panel,"전투에 사용할 무기를 선택하세요",new Vector2(0,270),new Vector2(1120,70),38);
        Text("Info",panel,"첫 무기는 무료입니다. 구매한 무기와 선택은 저장됩니다.",new Vector2(0,208),new Vector2(1120,45),23);
        _balance = Text("Balance",panel,"",new Vector2(0,150),new Vector2(1000,45),25);
        string[] names = {"검", "활", "스태프"};
        string[] descriptions = {"근접 전투", "원거리 전투", "마법 전투"};
        for (int i=0;i<3;i++)
        {
            float x=(i-1)*345;
            Text("Weapon"+i,panel,names[i],new Vector2(x,65),new Vector2(310,60),36);
            Text("Description"+i,panel,descriptions[i],new Vector2(x,7),new Vector2(310,40),23);
            int index=i;
            _choices[i]=Button("Choose"+i,panel,new Vector2(x,-75),new Vector2(310,64),()=>Choose(index),out _labels[i]);
        }
        _status=Text("Status",panel,"무기를 구매·선택한 다음 플레이를 시작하세요.",new Vector2(0,-165),new Vector2(1120,65),23);
        _start=Button("Start",panel,new Vector2(0,-262),new Vector2(390,68),Begin,out TMP_Text startLabel);
        startLabel.text="플레이 시작";
        _start.interactable=false;
    }

    private static RectTransform Box(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
        rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    private TMP_Text Text(string name,Transform parent,string value,Vector2 position,Vector2 size,float fontSize)
    {
        var label=Box(name,parent,position,size).gameObject.AddComponent<TextMeshProUGUI>();
        if(_font!=null) label.font=_font;
        label.text=value;label.fontSize=fontSize;label.alignment=TextAlignmentOptions.Center;
        label.color=Color.white;label.raycastTarget=false;return label;
    }
    private Button Button(string name,Transform parent,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action,out TMP_Text label)
    {
        var rect=Box(name,parent,position,size);
        var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.12f,.36f,.40f);
        var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);
        label=Text("Label",rect,"",Vector2.zero,size-Vector2.one*12,22);return button;
    }
}
