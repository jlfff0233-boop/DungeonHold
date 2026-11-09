using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LJPlay;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 플레이 HUD 연결 창 - 현재 씬의 표시 대상 검사와 편집 중 참조 연결
/// </summary>
public class PlayHudBindingWindow : EditorWindow
{
    /// <summary>
    /// 컴포넌트 계획 - 기존 바인더 또는 추가할 바인더 보관
    /// </summary>
    class ComponentPlan
    {
        public Type Type;    //연결할 컴포넌트 타입
        public GameObject Host;    //바인더를 부착할 대상
        public Component Instance;    //기존 또는 이번에 추가한 컴포넌트
    }

    /// <summary>
    /// 필드 계획 - 검사에서 확정한 빈 참조의 할당 정보
    /// </summary>
    class FieldPlan
    {
        public ComponentPlan Component;    //할당 대상 바인더
        public string Name;    //직렬화 필드 이름
        public Object Value;    //연결할 실제 대상
    }

    [SerializeField] Transform _hud;    //현재 씬의 HUD 루트
    [SerializeField] PlayScene _sceneLink;    //게임 대상의 기준 연결부
    [SerializeField] Hero _hero;    //플레이어 모듈 소유자
    [SerializeField] Currency _currency;    //현재 재화
    [SerializeField] StageRewardController _rewards;    //콤보와 보상
    [SerializeField] WaveController _wave;    //웨이브 진행
    [SerializeField] BuildPreviewController _preview;    //설치 미리보기
    [SerializeField] BuildPlacementController _placement;    //설치 실행
    [SerializeField] BuildPhaseGate _phaseGate;    //설치 구간 조건

    readonly List<ComponentPlan> _components = new List<ComponentPlan> ( );    //검사한 컴포넌트
    readonly List<FieldPlan> _fields = new List<FieldPlan> ( );    //이번에 채울 빈 참조
    readonly List<string> _results = new List<string> ( );    //검사와 적용 결과
    Vector2 _scroll;    //결과 목록 스크롤
    int _sceneHandle;    //대상 씬 변경 확인
    bool _hasErrors;    //적용 전에 해결해야 할 누락 또는 중복
    bool _hasInspection;    //현재 선택에 대한 검사 여부
    Button _startButton;    //준비 시작 버튼
    ComponentPlan _preparation;    //시작 버튼의 호출 대상
    bool _addStartListener;    //중복 없는 시작 호출 추가 여부
    int _repairStartListener = -1;    //호출 대상만 끊긴 준비 시작 항목

    /// <summary>
    /// 메뉴에서 HUD 연결 창 열기
    /// </summary>
    [MenuItem ( "Tools/Play HUD Binding" )]
    public static void Open ( )
    {
        GetWindow<PlayHudBindingWindow> ( "Play HUD Binding" ).Show ( );
    }

    private void OnEnable ( )
    {
        minSize = new Vector2 ( 560f , 420f );
        Undo.undoRedoPerformed += InvalidateInspection;
        EditorApplication.hierarchyChanged += InvalidateInspection;
        FindTargets ( );
    }

    private void OnGUI ( )
    {
        Scene scene = SceneManager.GetActiveScene ( );
        if ( _sceneHandle != scene.handle )
            FindTargets ( );

        EditorGUILayout.LabelField ( "대상 씬" , scene.name );
        bool unavailable = EditorApplication.isPlayingOrWillChangePlaymode
            || scene.IsValid ( ) == false || scene.isLoaded == false
            || PrefabStageUtility.GetCurrentPrefabStage ( ) != null;

        if ( unavailable == true )
            EditorGUILayout.HelpBox ( "플레이를 종료하고 일반 씬 편집 상태에서 사용하세요." , MessageType.Info );

        using ( new EditorGUI.DisabledScope ( unavailable ) )
        {
            EditorGUI.BeginChangeCheck ( );
            _hud = DrawTarget ( "HUD 루트" , _hud );
            _sceneLink = DrawTarget ( "씬 연결부" , _sceneLink );
            _hero = DrawTarget ( "플레이어" , _hero );
            _currency = DrawTarget ( "재화" , _currency );
            _rewards = DrawTarget ( "보상/콤보" , _rewards );
            _wave = DrawTarget ( "웨이브" , _wave );
            _preview = DrawTarget ( "설치 미리보기" , _preview );
            _placement = DrawTarget ( "설치 실행" , _placement );
            _phaseGate = DrawTarget ( "설치 구간" , _phaseGate );
            if ( EditorGUI.EndChangeCheck ( ) == true )
                InvalidateInspection ( );

            if ( _hasInspection == true && _hasErrors == true )
            {
                string firstError = _results.FirstOrDefault ( result => result.StartsWith ( "[해결 필요]" , StringComparison.Ordinal ) );
                EditorGUILayout.HelpBox ( "연결을 막는 항목이 있습니다.\n" + firstError , MessageType.Error );
            }

            using ( new EditorGUILayout.HorizontalScope ( ) )
            {
                if ( GUILayout.Button ( "대상 자동 탐색" ) == true )
                    FindTargets ( );
                if ( GUILayout.Button ( "연결 검사" ) == true )
                    Inspect ( );
                using ( new EditorGUI.DisabledScope ( _hasInspection == false || _hasErrors ) )
                {
                    if ( GUILayout.Button ( "빈 참조 자동 연결" ) == true )
                        ApplyBindings ( );
                }
            }
        }

        EditorGUILayout.HelpBox (
            "정상 연결은 유지하고 빈 참조만 채웁니다. 다른 대상이 연결된 필드는 충돌로 표시하며 덮어쓰지 않습니다."
            + "\n컴포넌트 추가와 버튼 연결은 Undo로 되돌릴 수 있습니다. 씬 저장은 직접 수행하세요." , MessageType.Info );
        if ( _hasInspection == false && _results.Count > 0 )
            EditorGUILayout.HelpBox ( "아래는 이전 결과입니다. 대상 또는 계층이 변경되어 다시 검사해야 합니다." , MessageType.Info );
        using ( new EditorGUI.DisabledScope ( _results.Count == 0 ) )
        {
            if ( GUILayout.Button ( "검사 결과 전체 복사" ) == true )
                GUIUtility.systemCopyBuffer = string.Join ( "\n" , _results );
        }
        _scroll = EditorGUILayout.BeginScrollView ( _scroll );
        foreach ( string result in _results )
            EditorGUILayout.LabelField ( result , EditorStyles.wordWrappedLabel );
        EditorGUILayout.EndScrollView ( );
        EditorGUILayout.HelpBox (
            "미연계: 생성 동료의 자동 추적, 무기/공용 슬롯 정보 갱신, 적 머리 위 체력바."
            + "\nPause/화면 활성 상태/레이아웃/웨이브 실행/종료 처리는 변경하지 않습니다." , MessageType.Info );
    }

    /// <summary>
    /// 직접 지정할 씬 대상 표시
    /// </summary>
    /// <param name="label">필드 표시명</param>
    /// <param name="value">현재 대상</param>
    /// <returns>선택한 대상</returns>
    T DrawTarget<T> ( string label , T value ) where T : Object
    {
        return ( T ) EditorGUILayout.ObjectField ( label , value , typeof ( T ) , true );
    }

    /// <summary>
    /// 현재 씬의 단일 대상과 씬 연결부가 소유한 실제 모듈 탐색
    /// </summary>
    void FindTargets ( )
    {
        Scene scene = SceneManager.GetActiveScene ( );
        _sceneHandle = scene.handle;
        _hud = Unique ( All<Transform> ( scene ).Where ( item => item.name == "Canvas_HUD" ) );
        _sceneLink = Unique ( All<PlayScene> ( scene ) );
        _hero = _sceneLink != null ? _sceneLink.Hero : Unique ( All<Hero> ( scene ) );
        _currency = _sceneLink != null ? _sceneLink.Currency : Unique ( All<Currency> ( scene ) );
        _rewards = _sceneLink != null ? _sceneLink.Rewards : Unique ( All<StageRewardController> ( scene ) );
        _wave = Unique ( All<WaveController> ( scene ) );
        _preview = Unique ( All<BuildPreviewController> ( scene ) );
        _placement = Unique ( All<BuildPlacementController> ( scene ) );
        _phaseGate = Unique ( All<BuildPhaseGate> ( scene ) );
        _results.Clear ( );
        InvalidateInspection ( );
    }

    /// <summary>
    /// 비활성 오브젝트를 포함한 해당 씬의 컴포넌트 조회
    /// </summary>
    /// <param name="scene">조회할 씬</param>
    /// <returns>씬 내부 컴포넌트</returns>
    static IEnumerable<T> All<T> ( Scene scene ) where T : Component
    {
        return scene.IsValid ( ) && scene.isLoaded
            ? scene.GetRootGameObjects ( ).SelectMany ( root => root.GetComponentsInChildren<T> ( true ) )
            : Enumerable.Empty<T> ( );
    }

    /// <summary>
    /// 후보가 정확히 하나인 경우만 자동 선택
    /// </summary>
    /// <param name="items">탐색 후보</param>
    /// <returns>단일 대상 또는 null</returns>
    static T Unique<T> ( IEnumerable<T> items ) where T : Object
    {
        T [ ] candidates = items.Take ( 2 ).ToArray ( );
        return candidates.Length == 1 ? candidates [ 0 ] : null;
    }

    /// <summary>
    /// 대상 변경 후 이전 검사 결과 무효화
    /// </summary>
    void InvalidateInspection ( )
    {
        _hasInspection = false;
        Repaint ( );
    }

    /// <summary>
    /// 실제 변경 없이 모든 연결 대상과 기존 참조 검사
    /// </summary>
    void Inspect ( )
    {
        InspectCore ( );
        //사용자가 경로를 입력하지 않아도 Unity 로그에서 검사 원인 확인
        Debug.Log ( "[Play HUD 연결 검사]\n" + string.Join ( "\n" , _results ) );
    }

    /// <summary>
    /// 대상 검사와 연결 계획 생성, 적용 전 재검사는 별도 로그 생략
    /// </summary>
    void InspectCore ( )
    {
        _components.Clear ( );
        _fields.Clear ( );
        _results.Clear ( );
        _hasErrors = false;
        _hasInspection = true;
        _addStartListener = false;
        _repairStartListener = -1;
        _preparation = null;
        _startButton = null;

        Scene scene = SceneManager.GetActiveScene ( );
        if ( EditorApplication.isPlayingOrWillChangePlaymode
            || scene.IsValid ( ) == false || scene.isLoaded == false
            || PrefabStageUtility.GetCurrentPrefabStage ( ) != null )
        {
            Error ( "일반 씬의 편집 상태에서 검사하세요." );
            return;
        }

        foreach ( Component target in new Component [ ]
        {
            _hud , _sceneLink , _hero , _currency , _rewards , _wave , _preview , _placement , _phaseGate
        } )
        {
            if ( target == null || EditorUtility.IsPersistent ( target )
                || target.gameObject.scene != scene )
                Error ( "상단 대상을 모두 현재 씬의 인스턴스로 지정하세요. 자동 탐색은 없거나 중복된 대상을 비워둡니다." );
        }
        if ( _hasErrors == true )
            return;

        if ( _sceneLink.Hero != _hero || _sceneLink.Currency != _currency || _sceneLink.Rewards != _rewards )
        {
            Error ( "플레이어/재화/보상 대상이 TestScene의 실제 참조와 다릅니다. 씬 연결부터 확인하세요." );
            return;
        }

        Health health = ReadReference<Health> ( _hero , "_health" );
        Energy energy = ReadReference<Energy> ( _hero , "_energy" );
        SkillCaster skill = ReadReference<SkillCaster> ( _hero , "_skillCaster" );
        PlayerReviveData revive = ReadReference<PlayerReviveData> ( _hero , "_reviveData" );
        Transform stage = At<Transform> ( "StageStatus" );
        Transform skillSlot = At<Transform> ( "StageStatus/SkillRoot/SkillSlot" );
        Transform ultimateSlot = At<Transform> ( "StageStatus/SkillRoot/SkillSlot (1)" );
        Transform currencyRoot = At<Transform> ( "StageStatus/Currency" );
        if ( _hasErrors == true )
            return;

        ComponentPlan view = Binder<PlayerHudView> ( _hud.gameObject );
        Bind ( view , "_healthFill" , At<Image> ( "PlayerStatus/HealthFrame/Health" ) );
        Bind ( view , "_energyFill" , At<Image> ( "PlayerStatus/EnergyFrame/Energy" ) );

        ComponentPlan player = Binder<PlayerHudBinder> ( _hud.gameObject );
        Bind ( player , "_view" , view != null ? view.Instance : null );
        Bind ( player , "_health" , health );
        Bind ( player , "_energy" , energy );

        ComponentPlan basicSkill = Binder<PlayerSkillHudBinder> ( _hud.gameObject );
        Bind ( basicSkill , "_hero" , _hero );
        Bind ( basicSkill , "_skillCaster" , skill );
        Bind ( basicSkill , "_icon" , skillSlot.Find ( "Icon" )?.GetComponent<Image> ( ) );
        Bind ( basicSkill , "_cooldownText" , At<TMP_Text> ( "StageStatus/SkillRoot/SkillSlot/CooldownText" ) );

        ComponentPlan ultimate = Binder<PlayerUltimateHudBinder> ( ultimateSlot.gameObject );
        Bind ( ultimate , "_hero" , _hero );
        Bind ( ultimate , "_icon" , At<Image> ( "StageStatus/SkillRoot/SkillSlot (1)/Icon" ) );
        Bind ( ultimate , "_cooldownText" , At<TMP_Text> ( "StageStatus/SkillRoot/SkillSlot (1)/CooldownText" ) );

        ComponentPlan life = Binder<PlayerLifeHudBinder> ( _hud.gameObject );
        Bind ( life , "_hero" , _hero );
        Bind ( life , "_health" , health );
        Bind ( life , "_reviveData" , revive );
        Bind ( life , "_noticeRoot" , At<Transform> ( "StageStatus/ImportantNoticeRoot" )?.gameObject );
        Bind ( life , "_noticeText" , At<TMP_Text> ( "StageStatus/ImportantNoticeRoot/NoticeText" ) );

        ComponentPlan money = Binder<CurrencyHudBinder> ( currencyRoot.gameObject );
        Bind ( money , "_currency" , _currency );
        Bind ( money , "_amountText" , At<TMP_Text> ( "StageStatus/Currency/CurrencyText" ) );

        ComponentPlan combo = Binder<ComboHudBinder> ( _hud.gameObject );
        Bind ( combo , "_rewards" , _rewards );
        Bind ( combo , "_comboRoot" , At<Transform> ( "StageStatus/ComboNoticeRoot/ComboDisplayRoot" )?.gameObject );
        Bind ( combo , "_comboText" , At<TMP_Text> ( "StageStatus/ComboNoticeRoot/ComboDisplayRoot/ComboText" ) );
        Bind ( combo , "_remainingTimeText" , At<TMP_Text> ( "StageStatus/ComboNoticeRoot/ComboDisplayRoot/RemaingingTimeText" ) );
        Bind ( combo , "_rewardRoot" , At<Transform> ( "StageStatus/ComboNoticeRoot/CoinGainText" )?.gameObject );
        Bind ( combo , "_rewardText" , At<TMP_Text> ( "StageStatus/ComboNoticeRoot/CoinGainText" ) );

        ComponentPlan wave = Binder<WaveHudBinder> ( stage.gameObject , true );
        Bind ( wave , "_waveController" , _wave );
        Bind ( wave , "_waveText" , At<TMP_Text> ( "StageStatus/Wave/WaveText" ) );
        Bind ( wave , "_timerText" , At<TMP_Text> ( "StageStatus/Wave/Timer" ) );
        Bind ( wave , "_passedEnemyText" , At<TMP_Text> ( "StageStatus/Objective/PassedEnemy" ) );
        Bind ( wave , "_activeEnemyText" , At<TMP_Text> ( "StageStatus/RemainingEnemyRoot/RemainingEnemyText" ) );

        ComponentPlan placement = Binder<PlacementHudBinder> ( stage.gameObject , true );
        Bind ( placement , "_preview" , _preview );
        Bind ( placement , "_placement" , _placement );
        Bind ( placement , "_buildPhaseGate" , _phaseGate );
        Bind ( placement , "_stateText" , At<TMP_Text> ( "StageStatus/PreparationHintRoot/PlacementGuideRoot/StateText" ) );
        Bind ( placement , "_reasonText" , At<TMP_Text> ( "StageStatus/PreparationHintRoot/PlacementGuideRoot/ReasonText" ) );
        Bind ( placement , "_forbiddenIcon" , At<Transform> ( "StageStatus/PreparationHintRoot/PlacementGuideRoot/ForbiddenIcon" )?.gameObject );

        _preparation = Binder<PreparationHudBinder> ( stage.gameObject , true );
        _startButton = At<Button> ( "StageStatus/PreparationHintRoot/StarWaveHint" );
        Bind ( _preparation , "_waveController" , _wave );
        Bind ( _preparation , "_hintRoot" , At<Transform> ( "StageStatus/PreparationHintRoot" )?.gameObject );
        Bind ( _preparation , "_startButton" , _startButton );
        InspectStartButton ( );
        _results.Add ( $"검사 결과: 빈 참조 {_fields.Count}개, 추가 컴포넌트 {_components.Count ( item => item.Instance == null )}개." );
        if ( _hud.gameObject.activeInHierarchy == false )
            _results.Add ( "[확인 필요] HUD 또는 상위 오브젝트가 꺼져 있습니다. 활성 상태는 도구가 변경하지 않습니다." );
    }

    /// <summary>
    /// 주체가 실제로 보유한 직렬화 참조 조회
    /// </summary>
    /// <param name="owner">참조를 소유한 컴포넌트</param>
    /// <param name="field">필드 이름</param>
    /// <returns>현재 참조 또는 null</returns>
    T ReadReference<T> ( Component owner , string field ) where T : Object
    {
        using ( SerializedObject serialized = new SerializedObject ( owner ) )
        {
            SerializedProperty property = serialized.FindProperty ( field );
            T value = property != null ? property.objectReferenceValue as T : null;
            if ( value == null )
                Error ( $"{Describe ( owner )}.{field}: 실제 참조가 없습니다." );
            else if ( value is Component component && component.gameObject.scene != owner.gameObject.scene )
            {
                Error ( $"{Describe ( owner )}.{field}: 다른 씬의 컴포넌트가 연결되어 있습니다." );
                return null;
            }
            return value;
        }
    }

    /// <summary>
    /// HUD 내부의 정확한 경로와 중복 이름 검사
    /// </summary>
    /// <param name="path">HUD 기준 상대 경로</param>
    /// <returns>지정 경로의 단일 컴포넌트 또는 null</returns>
    T At<T> ( string path ) where T : Component
    {
        Transform current = _hud;
        foreach ( string name in path.Split ( '/' ) )
        {
            Transform [ ] children = current.Cast<Transform> ( ).Where ( child => child.name == name ).ToArray ( );
            if ( children.Length != 1 )
            {
                Error ( $"{Describe ( _hud )}/{path}: 경로가 없거나 같은 이름의 자식이 중복됩니다." );
                return null;
            }
            current = children [ 0 ];
        }
        T [ ] components = current.GetComponents<T> ( );
        if ( components.Length != 1 )
        {
            Error ( $"{Describe ( current )}: {typeof ( T ).Name} 컴포넌트가 없거나 중복됩니다." );
            return null;
        }
        return components [ 0 ];
    }

    /// <summary>
    /// 바인더의 단일 소유 확인과 필요한 추가 계획 작성
    /// </summary>
    /// <param name="host">기본 부착 위치</param>
    /// <param name="canAdd">누락 시 기존 스크립트 추가 여부</param>
    /// <returns>검사한 컴포넌트 계획</returns>
    ComponentPlan Binder<T> ( GameObject host , bool canAdd = false ) where T : Component
    {
        T [ ] candidates = All<T> ( host.scene ).ToArray ( );
        if ( candidates.Length > 1 )
        {
            Error ( $"{typeof ( T ).Name} 중복: {string.Join ( ", " , candidates.Select ( item => Describe ( item ) ) )}" );
            return null;
        }
        T instance = candidates.Length == 1 ? candidates [ 0 ] : null;
        if ( instance != null && instance.transform.IsChildOf ( _hud ) == false )
        {
            Error ( $"{Describe ( instance )}: 대상 HUD 밖의 바인더입니다. 중복 추가하지 않습니다." );
            return null;
        }
        if ( instance == null && canAdd == false )
        {
            Error ( $"{Describe ( host )}: 기존 {typeof ( T ).Name}이 없습니다." );
            return null;
        }
        if ( instance != null && canAdd == true && instance.gameObject != host )
        {
            Error ( $"{Describe ( instance )}: 계속 갱신할 바인더를 {Describe ( host )}에 배치하세요." );
            return null;
        }
        ComponentPlan plan = new ComponentPlan { Type = typeof ( T ) , Host = host , Instance = instance };
        _components.Add ( plan );
        if ( instance == null )
            _results.Add ( $"[추가 예정] {Describe ( host )}: {typeof ( T ).Name}" );
        else if ( instance is Behaviour behaviour && behaviour.enabled == false )
            _results.Add ( $"[확인 필요] {Describe ( instance )}의 {typeof ( T ).Name}이 꺼져 있습니다. 활성 상태는 유지합니다." );
        return plan;
    }

    /// <summary>
    /// 필드 계약 확인 후 정상/빈 참조/충돌 분류
    /// </summary>
    /// <param name="component">연결할 컴포넌트 계획</param>
    /// <param name="field">직렬화 필드 이름</param>
    /// <param name="value">예상 연결 대상</param>
    void Bind ( ComponentPlan component , string field , Object value )
    {
        if ( component == null )
            return;
        string label = $"{Describe ( component.Instance != null ? component.Instance : component.Host )} [{component.Type.Name}].{field}";
        FieldInfo contract = component.Type.GetField ( field , BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public );
        if ( value == null || contract == null || contract.FieldType.IsInstanceOfType ( value ) == false )
        {
            Error ( $"{label}: 대상 누락 또는 필드 타입 불일치." );
            return;
        }
        Object current = null;
        if ( component.Instance != null )
        {
            using ( SerializedObject serialized = new SerializedObject ( component.Instance ) )
            {
                SerializedProperty property = serialized.FindProperty ( field );
                if ( property == null || property.propertyType != SerializedPropertyType.ObjectReference )
                {
                    Error ( $"{label}: 직렬화 참조 필드가 없습니다." );
                    return;
                }
                current = property.objectReferenceValue;
            }
        }
        if ( current == value )
            _results.Add ( $"[정상] {label} → {Describe ( value )}" );
        else if ( current != null )
            _results.Add ( $"[충돌/유지] {label}: 현재 {Describe ( current )} / 예상 {Describe ( value )}" );
        else
        {
            _fields.Add ( new FieldPlan { Component = component , Name = field , Value = value } );
            _results.Add ( $"[연결 예정] {label} → {Describe ( value )}" );
        }
    }

    /// <summary>
    /// 기존 준비 시작 호출의 대상과 중복 여부 검사
    /// </summary>
    void InspectStartButton ( )
    {
        if ( _startButton == null || _preparation == null )
            return;
        int matches = 0;
        for ( int i = 0 ; i < _startButton.onClick.GetPersistentEventCount ( ) ; i++ )
        {
            if ( _startButton.onClick.GetPersistentMethodName ( i ) != nameof ( PreparationHudBinder.RequestWaveStart ) )
                continue;
            matches++;
            Object target = _startButton.onClick.GetPersistentTarget ( i );
            if ( _startButton.onClick.GetPersistentListenerState ( i ) == UnityEventCallState.Off )
            {
                Error ( $"{Describe ( _startButton )}: 기존 RequestWaveStart 호출이 꺼져 있습니다. 활성 상태는 유지합니다." );
            }
            else if ( target == null )
                _repairStartListener = i;
            else if ( target != _preparation.Instance )
                Error ( $"{Describe ( _startButton )}: RequestWaveStart가 다른 대상 {Describe ( target )}에 연결되어 있습니다. 덮어쓰지 않습니다." );
        }
        if ( matches > 1 )
            Error ( $"{Describe ( _startButton )}: RequestWaveStart가 중복 등록되어 있습니다." );
        _addStartListener = matches == 0;
        if ( _repairStartListener >= 0 )
            _results.Add ( "[복구 예정] 준비 시작 버튼의 기존 RequestWaveStart 호출에 빠진 대상만 연결" );
        else
            _results.Add ( _addStartListener ? "[연결 예정] 준비 시작 버튼 → RequestWaveStart" : "[기존 호출 유지] 준비 시작 버튼" );
    }

    /// <summary>
    /// 최신 검사 후 빈 필드와 필요한 바인더/호출만 하나의 Undo로 적용
    /// </summary>
    void ApplyBindings ( )
    {
        //검사 이후 Inspector에서 바뀐 연결도 다시 대조
        InspectCore ( );
        if ( _hasErrors == true )
            return;
        int additions = _components.Count ( item => item.Instance == null );
        if ( additions == 0 && _fields.Count == 0
            && _addStartListener == false && _repairStartListener < 0 )
        {
            _results.Insert ( 0 , "적용할 변경이 없습니다. 충돌로 표시된 기존 연결은 유지했습니다." );
            return;
        }

        Undo.IncrementCurrentGroup ( );
        int group = Undo.GetCurrentGroup ( );
        Undo.SetCurrentGroupName ( "Play HUD 자동 연결" );
        try
        {
            foreach ( ComponentPlan component in _components )
            {
                if ( component.Instance == null )
                    component.Instance = Undo.AddComponent ( component.Host , component.Type );
            }
            foreach ( FieldPlan field in _fields )
            {
                using ( SerializedObject serialized = new SerializedObject ( field.Component.Instance ) )
                {
                    serialized.FindProperty ( field.Name ).objectReferenceValue = field.Value;
                    serialized.ApplyModifiedProperties ( );
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications ( field.Component.Instance );
            }
            if ( _addStartListener == true || _repairStartListener >= 0 )
            {
                Undo.RecordObject ( _startButton , "준비 시작 버튼 연결" );
                PreparationHudBinder preparation = ( PreparationHudBinder ) _preparation.Instance;
                if ( _repairStartListener >= 0 )
                    UnityEventTools.RegisterPersistentListener ( _startButton.onClick , _repairStartListener , preparation.RequestWaveStart );
                else
                    UnityEventTools.AddPersistentListener ( _startButton.onClick , preparation.RequestWaveStart );
                PrefabUtility.RecordPrefabInstancePropertyModifications ( _startButton );
            }
            EditorSceneManager.MarkSceneDirty ( SceneManager.GetActiveScene ( ) );
            Undo.CollapseUndoOperations ( group );
            int fields = _fields.Count;
            InspectCore ( );
            _results.Insert ( 0 , $"적용 완료: 참조 {fields}개, 컴포넌트 {additions}개. 씬은 저장하지 않았습니다." );
        }
        catch ( Exception exception )
        {
            //이번 편집에서 추가/할당한 대상만 복구
            Undo.RevertAllDownToGroup ( group );
            InspectCore ( );
            Error ( $"적용 실패로 이번 작업을 되돌렸습니다: {exception.Message}" );
        }
    }

    /// <summary>
    /// 적용을 막는 확인 항목 기록
    /// </summary>
    /// <param name="message">누락 또는 계약 오류</param>
    void Error ( string message )
    {
        _hasErrors = true;
        _results.Add ( "[해결 필요] " + message );
    }

    /// <summary>
    /// 씬 오브젝트는 전체 경로, 설정 에셋은 프로젝트 경로로 표시
    /// </summary>
    /// <param name="target">표시할 대상</param>
    /// <returns>대상 경로</returns>
    static string Describe ( Object target )
    {
        if ( target == null )
            return "없음";
        Transform transform = target is Component component ? component.transform
            : target is GameObject gameObject ? gameObject.transform : null;
        if ( transform == null )
            return AssetDatabase.GetAssetPath ( target );
        string path = transform.name;
        while ( transform.parent != null )
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    private void OnDisable ( )
    {
        Undo.undoRedoPerformed -= InvalidateInspection;
        EditorApplication.hierarchyChanged -= InvalidateInspection;
    }
}
