using System.Collections.Generic;
using LJPlay;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 히어로 편집 창 - 역할별 전투 설정과 참조 탐색 제공
/// </summary>
public class HeroEditorWindow : EditorWindow
{
    [SerializeField] Hero _hero;    //현재 편집 대상
    [SerializeField] bool _isLocked;    //선택 변경에도 대상 유지
    [SerializeField] int _tab;    //상위 메뉴
    [SerializeField] int _combatTab;    //전투 역할 메뉴
    Vector2 _scroll;    //본문 스크롤 위치
    readonly Dictionary<string, bool> _foldouts = new( );    //참조별 펼침 상태
    readonly HashSet<int> _drawingObjects = new HashSet<int>( );    //현재 참조 경로의 순환 방지
    readonly string[] _tabs = { "개요", "전투", "사용 방법" };    //상위 메뉴 이름
    readonly string[] _combatTabs = { "기본 공격", "스킬", "궁극기", "에너지" };    //전투 역할 이름

    /// <summary>
    /// 메뉴에서 편집 창 열기
    /// </summary>
    [MenuItem ( "Tools/Hero Editor" )]
    public static void Open ( )
    {
        GetWindow<HeroEditorWindow>( "Hero Editor" ).Show( );
    }

    /// <summary>
    /// 컴포넌트 메뉴의 히어로를 고정하여 열기
    /// </summary>
    /// <param name="command">선택한 히어로 컴포넌트</param>
    [MenuItem ( "CONTEXT/Hero/Open Hero Editor" )]
    static void OpenFromComponent ( MenuCommand command )
    {
        HeroEditorWindow window = GetWindow<HeroEditorWindow>( "Hero Editor" );
        window._hero = (Hero) command.context;
        window._isLocked = true;
        window.Show( );
    }

    private void OnEnable ( )
    {
        minSize = new Vector2( 340f, 300f );
        Selection.selectionChanged += HandleSelectionChanged;
        EditorApplication.hierarchyChanged += Repaint;
        Undo.undoRedoPerformed += Repaint;
        HandleSelectionChanged( );
    }

    private void OnInspectorUpdate ( )
    {
        if ( EditorApplication.isPlaying == true )
            Repaint( );
    }

    private void OnGUI ( )
    {
        DrawTarget( );
        _tab = GUILayout.Toolbar( _tab, _tabs );
        _scroll = EditorGUILayout.BeginScrollView( _scroll );
        _drawingObjects.Clear( );

        if ( _tab == 2 )
            DrawHelp( );
        else if ( _hero == null )
            EditorGUILayout.HelpBox( "Hierarchy에서 Hero 또는 그 자식을 선택하거나, 위 대상 칸에 Hero를 지정하세요.", MessageType.Info );
        else if ( _tab == 0 )
            DrawOverview( );
        else
            DrawCombat( );

        EditorGUILayout.EndScrollView( );
    }

    /// <summary>
    /// 선택한 오브젝트의 히어로를 따라가기
    /// </summary>
    void HandleSelectionChanged ( )
    {
        if ( _isLocked == false )
        {
            GameObject selected = Selection.activeGameObject;
            _hero = selected != null ? selected.GetComponentInParent<Hero>( true ) : null;
        }

        Repaint( );
    }

    /// <summary>
    /// 대상 지정과 고정 조작 표시
    /// </summary>
    void DrawTarget ( )
    {
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            EditorGUI.BeginChangeCheck( );
            Hero selected = (Hero) EditorGUILayout.ObjectField( "대상", _hero, typeof( Hero ), true );
            if ( EditorGUI.EndChangeCheck( ) == true )
            {
                _hero = selected;
                _isLocked = true;
            }

            bool locked = GUILayout.Toggle( _isLocked, "대상 고정", "Button", GUILayout.Width( 76f ) );
            if ( locked != _isLocked )
            {
                _isLocked = locked;
                HandleSelectionChanged( );
            }
        }

        if ( _hero != null )
            EditorGUILayout.LabelField( _hero.gameObject.scene.IsValid( ) == true
                ? _hero.gameObject.scene.name + " / " + _hero.name
                : "프리팹 에셋 / " + _hero.name, EditorStyles.miniLabel );

        if ( EditorApplication.isPlaying == true )
            EditorGUILayout.HelpBox( "플레이 중에는 설정을 읽기 전용으로 표시합니다. 펼치기와 찾기는 사용할 수 있습니다.", MessageType.Info );
    }

    /// <summary>
    /// 실제 역할 참조와 초기화 출처 요약 표시
    /// </summary>
    void DrawOverview ( )
    {
        EditorGUILayout.LabelField( "현재 구성", EditorStyles.boldLabel );
        EditorGUILayout.LabelField( "루트 컴포넌트", _hero.GetComponents<Component>( ).Length + "개 (Transform 포함)" );
        DrawRole( "기본 공격", 0 );
        DrawRole( "기본 스킬", 1 );
        DrawRole( "궁극기", 2 );
        DrawReferenceField( _hero, "_energy", "에너지 모듈" );

        EditorGUILayout.Space( );
        DrawInitialSettings( );
        EditorGUILayout.Space( );
        if ( Foldout( "heroReferences", "Hero 전체 연결", false ) == true )
            DrawObject( _hero, "hero", 0 );

        if ( Foldout( "rootComponents", "루트 컴포넌트 목록", false ) == true )
        {
            EditorGUILayout.HelpBox( "부착 목록입니다. 역할 연결 여부와 컴포넌트 활성 상태는 서로 다르며, 목록만으로 미사용을 판정하지 않습니다.", MessageType.None );
            foreach ( Component component in _hero.GetComponents<Component>( ) )
            {
                if ( component == null )
                    EditorGUILayout.HelpBox( "Missing Script가 있습니다. 기본 Inspector에서 확인하세요.", MessageType.Warning );
                else
                    DrawReference( component, component.GetType( ).Name, "root/" + component.GetInstanceID( ), 0 );
            }
        }
    }

    /// <summary>
    /// 역할별 실행자 요약과 전투 메뉴 이동 표시
    /// </summary>
    /// <param name="label">역할 표시 이름</param>
    /// <param name="role">기본 공격/스킬/궁극기 메뉴 번호</param>
    void DrawRole ( string label, int role )
    {
        Object executor = TryGetExecutorField( role, out Object owner, out string path ) == true
            ? GetReference( owner, path ) : null;
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            EditorGUILayout.LabelField( label, executor != null ? executor.GetType( ).Name : "연결 확인 필요" );
            if ( GUILayout.Button( "열기", GUILayout.Width( 45f ) ) == true )
            {
                _tab = 1;
                _combatTab = role;
                _scroll = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// 기본 공격과 두 캐스터를 실제 참조 기준으로 분리 표시
    /// </summary>
    void DrawCombat ( )
    {
        _combatTab = GUILayout.Toolbar( _combatTab, _combatTabs );
        EditorGUILayout.Space( );
        if ( _combatTab == 3 )
        {
            DrawReferenceField( _hero, "_energyData", "에너지 설정" );
            DrawReferenceField( _hero, "_energy", "에너지 모듈 / 현재 값" );
            EditorGUILayout.HelpBox( "에너지는 스킬과 궁극기가 공유합니다. 모듈의 현재 값은 읽기 전용이며 편집 모드 값은 초기화 후 능력치를 뜻하지 않습니다.", MessageType.Info );
            return;
        }

        bool basic = _combatTab == 0;
        string moduleField = basic == true ? "_attacker" : _combatTab == 1 ? "_skillCaster" : "_ultimateCaster";
        Object module = GetReference( _hero, moduleField );
        WeaponData data = GetSelectedWeaponData( );

        if ( basic == true )
            DrawInitialSettings( );
        else if ( data != null )
        {
            EditorGUILayout.LabelField( "무기", data.WeaponType.ToString( ) );
            DrawReferenceField( data, _combatTab == 1 ? "_skillData" : "_ultimateData", "설정 데이터" );
        }
        else
            EditorGUILayout.HelpBox( "무기 데이터를 확인할 수 없습니다. 초기화 출처와 선택 무기를 확인하세요.", MessageType.Info );

        if ( TryGetExecutorField( _combatTab, out Object owner, out string path ) == true )
        {
            EditorGUILayout.LabelField( EditorApplication.isPlaying == true ? "현재 적용 실행자" : "선택 무기의 연결", EditorStyles.boldLabel );
            DrawReferenceField( owner, path, "실행자 / 발사/조준 설정" );
        }
        else
            EditorGUILayout.HelpBox( "무기 연결을 확인할 수 없습니다. 선택 무기와 Hero의 무기별 연결 누락/중복을 확인하세요.", MessageType.Info );

        //편집 모드의 관리 모듈 참조는 무기 초기화 전 값이므로 여기서 실행자를 수정하지 않음
        if ( module != null )
        {
            using ( new EditorGUI.DisabledScope( true ) )
                EditorGUILayout.ObjectField( "관리 모듈", module, module.GetType( ), true );
        }

        if ( EditorApplication.isPlaying == true && module != null )
        {
            EditorGUILayout.Space( );
            EditorGUILayout.LabelField( "실행 상태 (읽기 전용)", EditorStyles.boldLabel );
            if ( module is Attacker attacker )
            {
                EditorGUILayout.LabelField( "공격 진행", attacker.IsAttacking.ToString( ) );
                EditorGUILayout.LabelField( "공격 회차", attacker.CurrentAttackId.ToString( ) );
            }
            else if ( module is SkillCaster caster )
            {
                EditorGUILayout.LabelField( "시전 진행", caster.IsCasting.ToString( ) );
                EditorGUILayout.LabelField( "남은 쿨타임", caster.RemainingCooldown.ToString( "F2" ) );
            }
        }
    }

    /// <summary>
    /// 선택 또는 실제 적용된 무기 데이터의 초기 능력치와 기본 공격 표시
    /// </summary>
    void DrawInitialSettings ( )
    {
        EditorGUILayout.LabelField( "무기 데이터 설정", EditorStyles.boldLabel );
        WeaponData data = GetSelectedWeaponData( );
        if ( data == null )
        {
            EditorGUILayout.HelpBox( "편집 중에는 같은 Hero를 참조하는 단일 TestScene의 무기 맵/선택 무기를, 플레이 중에는 Hero 초기화 상태를 확인하세요.", MessageType.Info );
            return;
        }

        EditorGUILayout.LabelField( EditorApplication.isPlaying == true ? "적용 무기" : "선택 무기", data.WeaponType.ToString( ) );
        DrawReference( data, "WeaponData 펼치기", "weapon/" + data.GetInstanceID( ), 0 );
        DrawReferenceField( data, "_combatStatsData", "전투 능력치" );
        DrawReferenceField( data, "_attackData", "기본 공격 데이터" );
    }

    /// <summary>
    /// 편집 중에는 초기화 예정 무기를, 플레이 중에는 실제 적용된 무기를 조회
    /// </summary>
    /// <returns>확정할 수 있는 무기 데이터 또는 null</returns>
    WeaponData GetSelectedWeaponData ( )
    {
        if ( EditorApplication.isPlaying == true )
            return _hero.WeaponData;

        PlayScene source = null;
        foreach ( PlayScene scene in Resources.FindObjectsOfTypeAll<PlayScene>( ) )
        {
            if ( scene.gameObject.scene.IsValid( ) == false || scene.gameObject.scene != _hero.gameObject.scene
                || GetReference( scene, "_hero" ) != _hero )
                continue;

            //초기화 주체가 여럿이면 탐색 순서로 하나를 임의 선택하지 않음
            if ( source != null )
                return null;
            source = scene;
        }

        if ( source == null )
            return null;

        using ( SerializedObject serialized = new SerializedObject( source ) )
        {
            WeaponDataMap map = (WeaponDataMap) serialized.FindProperty( "_weaponDataMap" ).objectReferenceValue;
            WeaponType type = (WeaponType) serialized.FindProperty( "_selectedWeaponType" ).intValue;
            if ( map == null )
                return null;
            return map.TryGet( type, out WeaponData data ) == true ? data : null;
        }
    }

    /// <summary>
    /// 역할별 실행자의 실제 편집 소유 객체와 직렬화 경로 조회
    /// </summary>
    /// <param name="role">기본 공격/스킬/궁극기 메뉴 번호</param>
    /// <param name="owner">참조 필드 소유 객체</param>
    /// <param name="path">참조 필드 직렬화 경로</param>
    /// <returns>중복 없이 실행자 필드를 찾았는지 여부</returns>
    bool TryGetExecutorField ( int role, out Object owner, out string path )
    {
        owner = null;
        path = null;
        if ( EditorApplication.isPlaying == true )
        {
            string moduleField = role == 0 ? "_attacker" : role == 1 ? "_skillCaster" : "_ultimateCaster";
            owner = GetReference( _hero, moduleField );
            path = role == 0 ? "_attackExecutor" : "_skillExecutor";
            return owner != null;
        }

        WeaponData data = GetSelectedWeaponData( );
        if ( data == null )
            return false;

        string executorField = role == 0 ? "_attackExecutor" : role == 1 ? "_skillExecutor" : "_ultimateExecutor";
        using ( SerializedObject serialized = new SerializedObject( _hero ) )
        {
            SerializedProperty bindings = serialized.FindProperty( "_weaponBindings" );
            for ( int i = 0; i < bindings.arraySize; i++ )
            {
                SerializedProperty binding = bindings.GetArrayElementAtIndex( i );
                WeaponType type = (WeaponType) binding.FindPropertyRelative( "_weaponType" ).intValue;
                if ( type != data.WeaponType )
                    continue;

                if ( path != null )
                {
                    path = null;
                    return false;
                }

                //SerializedObject를 해제한 뒤에는 Property 대신 경로로 다시 조회
                path = binding.FindPropertyRelative( executorField ).propertyPath;
            }
        }

        owner = _hero;
        return path != null;
    }

    /// <summary>
    /// 직렬화된 참조 조회
    /// </summary>
    /// <param name="owner">참조 소유 객체</param>
    /// <param name="field">필드 이름</param>
    /// <returns>연결된 객체 또는 null</returns>
    Object GetReference ( Object owner, string field )
    {
        if ( owner == null )
            return null;

        using ( SerializedObject serialized = new SerializedObject( owner ) )
        {
            SerializedProperty property = serialized.FindProperty( field );
            return property != null ? property.objectReferenceValue : null;
        }
    }

    /// <summary>
    /// 실제 소유 객체의 참조를 편집하고 내용을 펼쳐 표시
    /// </summary>
    /// <param name="owner">필드 소유 객체</param>
    /// <param name="field">필드 이름</param>
    /// <param name="label">표시 이름</param>
    void DrawReferenceField ( Object owner, string field, string label )
    {
        using ( SerializedObject serialized = new SerializedObject( owner ) )
        {
            serialized.Update( );
            SerializedProperty property = serialized.FindProperty( field );
            if ( property == null )
            {
                EditorGUILayout.HelpBox( label + ": 필드를 찾을 수 없습니다 (" + field + ").", MessageType.Warning );
                return;
            }

            using ( new EditorGUI.DisabledScope( EditorApplication.isPlaying ) )
                EditorGUILayout.PropertyField( property, new GUIContent( label ) );
            Object reference = property.objectReferenceValue;
            serialized.ApplyModifiedProperties( );
            if ( reference != null )
                DrawReference( reference, "내용: " + reference.GetType( ).Name, owner.GetInstanceID( ) + "/" + field, 0 );
            else
                EditorGUILayout.HelpBox( label + " 연결 없음", MessageType.Info );
        }
    }

    /// <summary>
    /// 참조 내용 펼치기와 원본 찾기 제공
    /// </summary>
    /// <param name="reference">표시할 원본 객체</param>
    /// <param name="label">표시 제목</param>
    /// <param name="key">펼침 상태 키</param>
    /// <param name="depth">현재 펼침 깊이</param>
    void DrawReference ( Object reference, string label, string key, int depth )
    {
        bool expanded;
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            expanded = Foldout( key, label, false );
            if ( GUILayout.Button( "찾기", GUILayout.Width( 42f ) ) == true )
                EditorGUIUtility.PingObject( reference );
            if ( GUILayout.Button( "선택", GUILayout.Width( 42f ) ) == true )
            {
                //원본을 선택해도 살펴보던 히어로 편집 대상은 유지
                _isLocked = true;
                Selection.activeObject = reference;
            }
        }

        if ( expanded == false )
            return;
        if ( reference is GameObject )
        {
            EditorGUILayout.HelpBox( "GameObject의 구성은 선택 버튼으로 기본 Inspector에서 확인하세요.", MessageType.None );
            return;
        }
        if ( _drawingObjects.Contains( reference.GetInstanceID( ) ) == true || depth >= 5 )
        {
            EditorGUILayout.HelpBox( "순환 참조 또는 최대 펼침 깊이입니다. 선택 버튼으로 원본을 확인하세요.", MessageType.None );
            return;
        }

        using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
        {
            if ( reference is ScriptableObject )
                EditorGUILayout.HelpBox( "공유 SO 에셋: 변경하면 이 에셋을 사용하는 다른 대상에도 반영됩니다.", MessageType.None );
            DrawObject( reference, key, depth );
        }
    }

    /// <summary>
    /// 원본 직렬화 필드를 표시하여 Undo와 프리팹 오버라이드 유지
    /// </summary>
    /// <param name="targetObject">표시할 객체</param>
    /// <param name="key">현재 참조 경로</param>
    /// <param name="depth">현재 펼침 깊이</param>
    void DrawObject ( Object targetObject, string key, int depth )
    {
        _drawingObjects.Add( targetObject.GetInstanceID( ) );
        using ( SerializedObject serialized = new SerializedObject( targetObject ) )
        {
            serialized.Update( );
            SerializedProperty property = serialized.GetIterator( );
            bool enterChildren = true;
            while ( property.NextVisible( enterChildren ) == true )
            {
                enterChildren = false;
                bool readOnly = property.propertyPath == "m_Script" || targetObject is Health || targetObject is Energy;
                using ( new EditorGUI.DisabledScope( readOnly == true || EditorApplication.isPlaying == true ) )
                    EditorGUILayout.PropertyField( property, true );

                if ( property.propertyType != SerializedPropertyType.ObjectReference || property.propertyPath == "m_Script" )
                    continue;

                Object reference = property.objectReferenceValue;
                if ( reference != null )
                {
                    //중첩 편집 전에 현재 소유 객체의 변경을 확정
                    serialized.ApplyModifiedProperties( );
                    DrawReference( reference, property.displayName + " 펼치기", key + "/" + property.propertyPath, depth + 1 );
                }
            }
            serialized.ApplyModifiedProperties( );
        }
        _drawingObjects.Remove( targetObject.GetInstanceID( ) );
    }

    /// <summary>
    /// 경로별 펼침 상태 유지
    /// </summary>
    /// <param name="key">상태 키</param>
    /// <param name="label">표시 제목</param>
    /// <param name="initial">최초 펼침 여부</param>
    /// <returns>현재 펼침 여부</returns>
    bool Foldout ( string key, string label, bool initial )
    {
        bool expanded = _foldouts.TryGetValue( key, out bool stored ) == true ? stored : initial;
        expanded = EditorGUILayout.Foldout( expanded, label, true );
        _foldouts[key] = expanded;
        return expanded;
    }

    /// <summary>
    /// 다른 작업자도 사용할 수 있는 사용 순서와 편집 범위 안내
    /// </summary>
    void DrawHelp ( )
    {
        EditorGUILayout.LabelField( "Hero Editor 사용 방법", EditorStyles.boldLabel );
        EditorGUILayout.HelpBox(
            "1. 상단 메뉴 Tools > Hero Editor로 엽니다. 창 제목을 드래그해 Inspector 옆에 도킹할 수 있습니다.\n\n"
            + "2. Hierarchy에서 Hero 또는 그 자식을 선택합니다. 대상 칸에 직접 지정하면 자동으로 고정됩니다. Hero 컴포넌트의 메뉴 > Open Hero Editor도 사용할 수 있습니다.\n\n"
            + "3. 대상 고정을 켜면 다른 오브젝트나 SO를 선택해도 현재 Hero를 유지합니다. 고정을 해제하면 현재 선택을 따라갑니다.\n\n"
            + "4. 개요에서 역할별 실행자와 초기화 출처를 확인합니다. 열기를 누르면 해당 전투 메뉴로 이동합니다.\n\n"
            + "5. 전투에서 기본 공격 / 스킬 / 궁극기 / 에너지를 선택합니다. 내용 또는 펼치기를 열면 참조된 원본 설정을 같은 화면에서 편집합니다.\n\n"
            + "6. 찾기는 원본 위치를 강조합니다. 선택은 대상을 고정한 뒤 원본을 일반 Inspector에 표시합니다.", MessageType.None );
        EditorGUILayout.LabelField( "값을 변경할 때", EditorStyles.boldLabel );
        EditorGUILayout.HelpBox(
            "변경은 원래 컴포넌트와 SO에 적용되며 Undo/Redo를 사용할 수 있습니다. 공유 SO 변경은 다른 사용자에게도 영향을 줍니다. 씬/프로젝트 저장과 프리팹 Apply/Revert는 Unity 기본 기능으로 처리하세요.\n\n"
            + "플레이 중에는 읽기 전용입니다. Health와 Energy의 현재 값은 편집 모드에서도 읽기 전용이며, 초기 능력치는 설정 데이터를 수정하세요.\n\n"
            + "편집 중에는 같은 Hero를 참조하는 단일 TestScene의 선택 무기를 무기 맵에서 조회합니다. 데이터는 WeaponData, 실행자는 Hero의 해당 무기별 연결을 편집합니다. 프리팹이나 다른 초기화 경로는 자동 추정하지 않습니다.\n\n"
            + "플레이 중에는 Hero에 실제 적용된 WeaponData와 관리 모듈의 현재 실행자를 표시합니다. 컴포넌트 순서를 바꾸지 않으며 생성/삭제/자동 연결도 하지 않습니다.\n\n"
            + "이 창은 단일 Hero용입니다. 기본 직렬화 필드를 표시하므로 별도 커스텀 Inspector의 전용 버튼은 원본 Inspector에서 사용하세요. 펼침 상태는 창이 열린 동안 유지됩니다.", MessageType.Info );
    }

    private void OnDisable ( )
    {
        Selection.selectionChanged -= HandleSelectionChanged;
        EditorApplication.hierarchyChanged -= Repaint;
        Undo.undoRedoPerformed -= Repaint;
    }
}
