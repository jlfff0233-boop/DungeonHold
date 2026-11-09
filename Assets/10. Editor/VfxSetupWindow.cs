#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 공용 효과 구성과 사용처 일괄 연결 창
/// </summary>
public class VfxSetupWindow : EditorWindow
{
    [SerializeField] VfxSetupProfile _profile;		//현재 배정 프로필
    Vector2 _scroll;		//창 스크롤
    bool _showEffects;		//고급 배정표 펼침
    bool _showSelections = true;		//원본별 배정 요약 펼침
    bool _showBindings = true;		//사용처 목록 펼침
    string _report;		//복사 가능한 최근 결과
    VfxSetupInspection _inspection;		//사용자에게 표시한 적용 계획
    int _category;		//사용처 분류
    string _search = string.Empty;		//대상과 역할 검색

    /// <summary>
    /// 공용 설정 창 열기
    /// </summary>
    [MenuItem( "Tools/VFX Setup" )]
    public static void Open ()
    {
        GetWindow<VfxSetupWindow>( "VFX Setup" );
    }

    private void OnGUI ()
    {
        EditorGUI.BeginChangeCheck( );
        _profile = ( VfxSetupProfile ) EditorGUILayout.ObjectField(
            "배정 파일", _profile, typeof( VfxSetupProfile ), false );
        if ( EditorGUI.EndChangeCheck( ) )
            _inspection = null;
        using ( new EditorGUI.DisabledScope( EditorApplication.isPlayingOrWillChangePlaymode
            || EditorApplication.isCompiling ) )
        {
            if ( GUILayout.Button( "새 배정 파일" ) )
                CreateProfile( );
        }
        if ( _profile == null )
            return;

        _scroll = EditorGUILayout.BeginScrollView( _scroll );
        using ( new EditorGUI.DisabledScope( VfxSetupUtility.CanEdit( _profile ) == false ) )
        {
            using ( var serialized = new SerializedObject( _profile ) )
            {
                serialized.Update( );
                EditorGUILayout.PropertyField( serialized.FindProperty( "_sourceFolder" ), new GUIContent( "선별 원본 폴더" ) );
                EditorGUILayout.PropertyField( serialized.FindProperty( "_outputFolder" ), new GUIContent( "공용 프리팹 생성 폴더" ) );
                SerializedProperty scope = serialized.FindProperty( "_scope" );
                scope.enumValueIndex = EditorGUILayout.Popup( "탐색 / 가져오기 / 적용 범위", scope.enumValueIndex,
                    new[] { "전체 담당", "LJ", "KMB", "LHJ", "직접 지정" } );
                if ( serialized.ApplyModifiedProperties( ) )
                    _inspection = null;
                EditorGUILayout.HelpBox( "아래 분류와 검색은 화면 필터입니다. 실제 작업은 위 담당 범위와 적용 체크를 따릅니다.", MessageType.Info );
                DrawSelections( serialized.FindProperty( "_effects" ) );
                _category = EditorGUILayout.Popup( "화면 분류", _category,
                    new[] { "전체", "플레이어 / 동료 (LJ)", "무기 (LJ)", "아이템 (LJ)",
                        "적 / 보스 (KMB)", "설치물 (KMB)", "UI / 연출 (LHJ)", "직접 지정" } );
                _search = EditorGUILayout.TextField( "대상 / 사용처 검색", _search );
                _showBindings = EditorGUILayout.Foldout( _showBindings, "사용처에 효과 선택", true );
                if ( _showBindings )
                    DrawBindings( serialized.FindProperty( "_bindings" ) );
                _showEffects = EditorGUILayout.Foldout( _showEffects, "고급: 공용 효과 목록", true );
                if ( _showEffects )
                {
                    DrawEffects( serialized.FindProperty( "_effects" ) );
                }
                if ( serialized.ApplyModifiedProperties( ) )
                    _inspection = null;
            }

            EditorGUILayout.HelpBox( "사용처에 원본 또는 준비된 효과를 선택한 뒤 검사하세요. "
                + "필요한 신규 효과 생성과 참조 연결을 함께 적용합니다. UI는 UGUI 원본을 사용합니다.", MessageType.Info );
            EditorGUILayout.HelpBox( "상속 필드는 기본적으로 베이스에 적용하며 명시적인 오버라이드는 유지합니다. "
                + "개별 변경은 '이 프리팹에만 적용'을 선택하세요. 공유 데이터는 프로필 밖 사용처에도 반영됩니다.", MessageType.Info );
            using ( new EditorGUILayout.HorizontalScope( ) )
            {
                if ( GUILayout.Button( "선택 JSON 가져오기 / 일괄 배정" ) )
                    ImportSelections( );
                if ( _showEffects && GUILayout.Button( "원본 폴더 목록 추가" ) )
                    ImportSources( );
                if ( GUILayout.Button( "선택 범위 사용처 불러오기" ) )
                {
                    _report = VfxSetupUtility.Discover( _profile );
                    _inspection = null;
                }
            }
            using ( new EditorGUILayout.HorizontalScope( ) )
            {
                if ( GUILayout.Button( "프로필 저장" ) )
                {
                    AssetDatabase.SaveAssetIfDirty( _profile );
                    _report = "배정 프로필 저장 완료. 사용처 적용과 실제 출력 확인은 별도입니다.";
                }
            }
            if ( GUILayout.Button( "선택 항목 검사" ) )
                Inspect( );
            using ( new EditorGUI.DisabledScope( _inspection == null
                || ( _inspection.Changes.Count == 0 && _inspection.Generations.Count == 0 ) ) )
            {
                string label = _inspection != null && _inspection.Errors.Count > 0
                    ? "검사 통과 항목만 적용"
                    : "선택 항목 생성 / 연결 / 저장";
                if ( GUILayout.Button( label ) )
                {
                    _report = VfxSetupUtility.Apply( _profile, _inspection );
                    Debug.Log( "[VFX Setup] " + _report, _profile );
                    _inspection = null;
                }
            }
        }

        if ( _inspection != null )
        {
            EditorGUILayout.LabelField( "적용 가능: 효과 " + _inspection.Generations.Count
                + "건 / 연결 " + _inspection.Changes.Count + "건", EditorStyles.boldLabel );
            foreach ( VfxGeneration generation in _inspection.Generations )
                    EditorGUILayout.LabelField( ( generation.Existing != null ? "기존 갱신: " : "신규 생성: " )
                        + generation.Path, EditorStyles.wordWrappedLabel );
            foreach ( VfxSetupChange change in _inspection.Changes )
                EditorGUILayout.LabelField( change.Description, EditorStyles.wordWrappedLabel );
        }
        if ( string.IsNullOrEmpty( _report ) == false )
        {
            EditorGUILayout.Space( );
            EditorGUILayout.SelectableLabel( _report, EditorStyles.textArea, GUILayout.MinHeight( 110 ) );
            using ( new EditorGUILayout.HorizontalScope( ) )
            {
                if ( GUILayout.Button( "결과 복사" ) )
                    EditorGUIUtility.systemCopyBuffer = _report;
                if ( GUILayout.Button( "Console 출력" ) )
                    Debug.Log( "[VFX Setup] " + _report, _profile );
            }
        }
        EditorGUILayout.EndScrollView( );
    }

    /// <summary>
    /// 저장 경로를 선택하여 프로필만 생성
    /// </summary>
    void CreateProfile ()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "VFX 배정 프로필", "VfxSetupProfile", "asset", "프로필 저장 경로" );
        if ( string.IsNullOrEmpty( path ) )
            return;
        _profile = CreateInstance<VfxSetupProfile>( );
        AssetDatabase.CreateAsset( _profile, path );
        _inspection = null;
        Selection.activeObject = _profile;
    }

    /// <summary>
    /// 선택 원본별 배정 수와 참고 사항 표시
    /// </summary>
    void DrawSelections ( SerializedProperty effects )
    {
        _showSelections = EditorGUILayout.Foldout( _showSelections, "선택 원본 / 배정 요약", true );
        if ( _showSelections == false )
            return;
        for ( int i = 0; i < _profile.Effects.Count; i++ )
        {
            VfxSetupEffect entry = _profile.Effects[i];
            if ( entry.Source == null && entry.Result == null && string.IsNullOrEmpty( entry.Note ) )
                continue;
            string name = entry.Source != null ? entry.Source.name
                : entry.Result != null ? entry.Result.name : entry.Key;
            VfxSetupBinding[] bindings = _profile.Bindings.Where( item => item.EffectKey == entry.Key
                && VfxSetupUtility.Includes( _profile.Scope, item ) ).ToArray( );
            if ( bindings.Length == 0 && _profile.Bindings.Any( item => item.EffectKey == entry.Key ) )
                continue;
            int included = bindings.Count( item => item.Apply );
            string status = bindings.Length == 0 ? "수동 연결 / 연계 대기" : "배정 후보 / 실제 참조는 사용처에서 확인";
            EditorGUILayout.LabelField( name, $"범위 내 배정 {bindings.Length}건 / 적용 선택 {included}건" );
            EditorGUILayout.LabelField( status, EditorStyles.wordWrappedLabel );
            if ( string.IsNullOrEmpty( entry.Note ) == false )
                EditorGUILayout.LabelField( entry.Note, EditorStyles.wordWrappedLabel );
            if ( entry.HasImportedNote == true && entry.Note != entry.ImportedNote )
            {
                EditorGUILayout.LabelField( "최신 JSON: " + entry.ImportedNote, EditorStyles.wordWrappedLabel );
                if ( GUILayout.Button( "JSON 메모 사용" ) )
                    effects.GetArrayElementAtIndex( i ).FindPropertyRelative( "_note" ).stringValue = entry.ImportedNote;
            }
        }
    }

    /// <summary>
    /// JSON 선택을 프로필의 배정 초안으로 가져오기
    /// </summary>
    void ImportSelections ()
    {
        string path = EditorUtility.OpenFilePanel( "VFX 선택 JSON", Application.dataPath + "/12. Vfx", "json" );
        if ( string.IsNullOrEmpty( path ) )
            return;
        try
        {
            _report = VfxSelectionImporter.Import( _profile, path );
            _inspection = null;
        }
        catch ( Exception exception ) when ( exception is Newtonsoft.Json.JsonException
            || exception is ArgumentException || exception is System.IO.IOException
            || exception is UnauthorizedAccessException )
        {
            _report = "JSON을 가져오지 않았습니다.\n" + exception.Message;
        }
    }

    /// <summary>
    /// 원본과 기존 공용 효과를 나누어 편집
    /// </summary>
    void DrawEffects ( SerializedProperty effects )
    {
        for ( int i = 0; i < effects.arraySize; i++ )
        {
            SerializedProperty row = effects.GetArrayElementAtIndex( i );
            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
            {
                EditorGUILayout.PropertyField( row.FindPropertyRelative( "_key" ), new GUIContent( "효과 키" ) );
                EditorGUILayout.PropertyField( row.FindPropertyRelative( "_note" ), new GUIContent( "참고 사항" ) );
                EditorGUILayout.PropertyField( row.FindPropertyRelative( "_output" ), new GUIContent( "출력 종류" ) );
                bool ui = row.FindPropertyRelative( "_output" ).enumValueIndex == ( int ) VfxOutput.Ui;
                EditorGUILayout.PropertyField( row.FindPropertyRelative( ui ? "_uiPrefab" : "_prefab" ), new GUIContent( "준비된 공용 효과" ) );
                EditorGUILayout.PropertyField( row.FindPropertyRelative( "_looping" ), new GUIContent( "유지 효과" ) );
                EditorGUILayout.PropertyField( row.FindPropertyRelative( "_create" ), new GUIContent( "원본으로 생성 / 갱신" ) );
                if ( row.FindPropertyRelative( "_create" ).boolValue )
                    EditorGUILayout.PropertyField( row.FindPropertyRelative( "_source" ), new GUIContent( "원본 프리팹" ) );
                if ( ui && row.FindPropertyRelative( "_looping" ).boolValue == false )
                    EditorGUILayout.PropertyField( row.FindPropertyRelative( "_duration" ), new GUIContent( "단발 완료 시간" ) );
                if ( GUILayout.Button( "배정 항목 제거" ) )
                {
                    effects.DeleteArrayElementAtIndex( i );
                    break;
                }
            }
        }
        if ( GUILayout.Button( "효과 항목 추가" ) )
        {
            int index = effects.arraySize;
            effects.InsertArrayElementAtIndex( index );
            SerializedProperty row = effects.GetArrayElementAtIndex( index );
            row.FindPropertyRelative( "_key" ).stringValue = string.Empty;
            row.FindPropertyRelative( "_note" ).stringValue = string.Empty;
            row.FindPropertyRelative( "_importedNote" ).stringValue = string.Empty;
            row.FindPropertyRelative( "_hasImportedNote" ).boolValue = false;
            row.FindPropertyRelative( "_prefab" ).objectReferenceValue = null;
            row.FindPropertyRelative( "_source" ).objectReferenceValue = null;
            row.FindPropertyRelative( "_uiPrefab" ).objectReferenceValue = null;
            row.FindPropertyRelative( "_output" ).enumValueIndex = 0;
            row.FindPropertyRelative( "_duration" ).floatValue = 1f;
            row.FindPropertyRelative( "_builtStamp" ).stringValue = string.Empty;
            row.FindPropertyRelative( "_looping" ).boolValue = false;
            row.FindPropertyRelative( "_create" ).boolValue = true;
        }
    }

    /// <summary>
    /// 공용 대상 선택과 명시적인 필드 배정 편집
    /// </summary>
    void DrawBindings ( SerializedProperty bindings )
    {
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            bool includeAll = GUILayout.Button( "담당 범위 전체 포함" );
            bool excludeAll = GUILayout.Button( "담당 범위 전체 제외" );
            if ( includeAll || excludeAll )
            {
                for ( int i = 0; i < bindings.arraySize; i++ )
                {
                    if ( VfxSetupUtility.Includes( _profile.Scope, _profile.Bindings[i] ) == false )
                        continue;
                    bindings.GetArrayElementAtIndex( i ).FindPropertyRelative( "_apply" ).boolValue = includeAll;
                }
            }
        }
        SerializedProperty effects = bindings.serializedObject.FindProperty( "_effects" );
        string[] keys = Enumerable.Range( 0, effects.arraySize )
            .Select( index => effects.GetArrayElementAtIndex( index ).FindPropertyRelative( "_key" ).stringValue ).ToArray( );
        for ( int i = 0; i < bindings.arraySize; i++ )
        {
            SerializedProperty row = bindings.GetArrayElementAtIndex( i );
            Object value = row.FindPropertyRelative( "_target" ).objectReferenceValue;
            VfxTargetModule module = ( VfxTargetModule ) row.FindPropertyRelative( "_module" ).enumValueIndex;
            string role = RoleLabel( row );
            VfxSetupCategory category = VfxSetupUtility.Category( value,
                row.FindPropertyRelative( "_suggestionKey" ).stringValue, module,
                ( VfxSetupCategory ) row.FindPropertyRelative( "_category" ).enumValueIndex );
            bool inScope = _profile.Scope == VfxSetupScope.All || VfxSetupUtility.Owner( category ) == _profile.Scope;
            string title = "[" + VfxSetupUtility.Owner( category ) + "] "
                + ( value != null ? value.name : "대상 선택" ) + " / " + role;
            if ( ( _category != 0 && _category != ( int ) category )
                || ( string.IsNullOrEmpty( _search ) == false && title.IndexOf( _search, StringComparison.OrdinalIgnoreCase ) < 0 ) )
                continue;
            using ( new EditorGUILayout.VerticalScope( EditorStyles.helpBox ) )
            {
                using ( new EditorGUILayout.HorizontalScope( ) )
                {
                    using ( new EditorGUI.DisabledScope( inScope == false ) )
                        EditorGUILayout.PropertyField( row.FindPropertyRelative( "_apply" ), GUIContent.none, GUILayout.Width( 18 ) );
                    EditorGUILayout.LabelField( title, EditorStyles.boldLabel );
                }
                SerializedProperty target = row.FindPropertyRelative( "_target" );
                if ( value == null )
                    EditorGUILayout.PropertyField( target, new GUIContent( "대상 프리팹 / 데이터" ) );
                DrawAssignment( row, effects, module );
                VfxSetupEffect assigned = _profile.Effects.FirstOrDefault(
                    item => item.Key == row.FindPropertyRelative( "_effectKey" ).stringValue );
                if ( inScope == false )
                    EditorGUILayout.LabelField( "담당 범위 밖: 검사 / 적용 제외" );
                if ( assigned != null && assigned.Result != null && value != null )
                {
                    Object destination = value is GameObject owner ? VfxSetupUtility.Module( owner, module ) : value;
                    if ( destination != null )
                    {
                        using ( var targetState = new SerializedObject( destination ) )
                        {
                            SerializedProperty reference = targetState.FindProperty( row.FindPropertyRelative( "_field" ).stringValue );
                            if ( reference != null && reference.objectReferenceValue == assigned.Result )
                                EditorGUILayout.LabelField( "참조 연결 완료 / 호출과 출력 확인은 별도" );
                        }
                    }
                }
                row.isExpanded = EditorGUILayout.Foldout( row.isExpanded, "사용처 / 공유 / 교체 설정", true );
                if ( row.isExpanded == false )
                    continue;
                EditorGUILayout.PropertyField( target, new GUIContent( "대상" ) );
                EditorGUILayout.PropertyField( row.FindPropertyRelative( "_category" ), new GUIContent( "기능 분류 (Auto는 자동)" ) );
                if ( target.objectReferenceValue is GameObject )
                    EditorGUILayout.PropertyField( row.FindPropertyRelative( "_module" ), new GUIContent( "사용할 표현 모듈" ) );
                module = ( VfxTargetModule ) row.FindPropertyRelative( "_module" ).enumValueIndex;
                DrawComponent( target, module );
                SerializedProperty field = row.FindPropertyRelative( "_field" );
                DrawChoice( "사용처", field, VfxSetupUtility.Fields( target.objectReferenceValue, module ), true );
                DrawChoice( "사용할 효과 키", row.FindPropertyRelative( "_effectKey" ), keys );
                bool? required = VfxSetupUtility.RequiredLoop( target.objectReferenceValue, field.stringValue, module );
                SerializedProperty looping = row.FindPropertyRelative( "_looping" );
                if ( required.HasValue )
                    looping.boolValue = required.Value;
                using ( new EditorGUI.DisabledScope( required.HasValue ) )
                    EditorGUILayout.PropertyField( looping, new GUIContent( "유지 효과 사용처" ) );
                EditorGUILayout.PropertyField( row.FindPropertyRelative( "_replaceExisting" ), new GUIContent( "기존 참조 교체 허용" ) );
                EditorGUILayout.PropertyField( row.FindPropertyRelative( "_localOverride" ), new GUIContent( "상속 필드도 이 프리팹에만 적용" ) );
                if ( GUILayout.Button( "사용처 제거" ) )
                {
                    bindings.DeleteArrayElementAtIndex( i );
                    break;
                }
            }
        }
        using ( new EditorGUILayout.HorizontalScope( ) )
        {
            if ( GUILayout.Button( "직접 지정 추가" ) )
                AddBinding( bindings, VfxTargetModule.Character );
            if ( GUILayout.Button( "월드 연출 추가" ) )
                AddBinding( bindings, VfxTargetModule.WorldPlayer );
            if ( GUILayout.Button( "UI 효과 추가" ) )
                AddBinding( bindings, VfxTargetModule.UiPlayer );
        }
    }

    /// <summary>
    /// 한 사용처에서 원본 또는 준비된 효과를 바로 선택
    /// </summary>
    void DrawAssignment ( SerializedProperty binding, SerializedProperty effects, VfxTargetModule module )
    {
        string key = binding.FindPropertyRelative( "_effectKey" ).stringValue;
        SerializedProperty entry = null;
        for ( int i = 0; i < effects.arraySize; i++ )
        {
            SerializedProperty item = effects.GetArrayElementAtIndex( i );
            if ( item.FindPropertyRelative( "_key" ).stringValue == key )
            {
                entry = item;
                break;
            }
        }
        if ( entry == null )
        {
            if ( GUILayout.Button( "이 사용처의 효과 선택 준비" ) )
            {
                if ( string.IsNullOrWhiteSpace( key ) )
                    key = "custom_" + Guid.NewGuid( ).ToString( "N" );
                binding.FindPropertyRelative( "_effectKey" ).stringValue = key;
                int index = effects.arraySize;
                effects.InsertArrayElementAtIndex( index );
                entry = effects.GetArrayElementAtIndex( index );
                entry.FindPropertyRelative( "_key" ).stringValue = key;
                entry.FindPropertyRelative( "_note" ).stringValue = string.Empty;
                entry.FindPropertyRelative( "_importedNote" ).stringValue = string.Empty;
                entry.FindPropertyRelative( "_hasImportedNote" ).boolValue = false;
                entry.FindPropertyRelative( "_note" ).stringValue = string.Empty;
                entry.FindPropertyRelative( "_source" ).objectReferenceValue = null;
                entry.FindPropertyRelative( "_prefab" ).objectReferenceValue = null;
                entry.FindPropertyRelative( "_uiPrefab" ).objectReferenceValue = null;
                entry.FindPropertyRelative( "_create" ).boolValue = true;
                entry.FindPropertyRelative( "_looping" ).boolValue = binding.FindPropertyRelative( "_looping" ).boolValue;
                entry.FindPropertyRelative( "_duration" ).floatValue = 1f;
                entry.FindPropertyRelative( "_builtStamp" ).stringValue = string.Empty;
                entry.FindPropertyRelative( "_output" ).enumValueIndex = module == VfxTargetModule.UiPlayer ? 1 : 0;
            }
            return;
        }

        bool ui = entry.FindPropertyRelative( "_output" ).enumValueIndex == ( int ) VfxOutput.Ui;
        SerializedProperty source = entry.FindPropertyRelative( "_source" );
        SerializedProperty prepared = entry.FindPropertyRelative( ui ? "_uiPrefab" : "_prefab" );
        GameObject selected = entry.FindPropertyRelative( "_create" ).boolValue
            ? source.objectReferenceValue as GameObject : null;
        if ( selected == null && prepared.objectReferenceValue is Component component )
            selected = component.gameObject;
        EditorGUI.BeginChangeCheck( );
        GameObject choice = ( GameObject ) EditorGUILayout.ObjectField(
            "사용할 효과", selected, typeof( GameObject ), false );
        if ( EditorGUI.EndChangeCheck( ) )
        {
            VfxEffect world = choice != null ? choice.GetComponent<VfxEffect>( ) : null;
            UiVfxEffect canvas = choice != null ? choice.GetComponent<UiVfxEffect>( ) : null;
            if ( world != null || canvas != null )
            {
                ui = canvas != null;
                entry.FindPropertyRelative( "_output" ).enumValueIndex = ui ? 1 : 0;
                entry.FindPropertyRelative( ui ? "_uiPrefab" : "_prefab" ).objectReferenceValue = ui ? ( Object ) canvas : world;
                entry.FindPropertyRelative( "_looping" ).boolValue = ui ? canvas.Looping
                    : VfxEffect.IsMaintained( choice );
                source.objectReferenceValue = null;
                entry.FindPropertyRelative( "_create" ).boolValue = false;
                entry.FindPropertyRelative( "_builtStamp" ).stringValue = string.Empty;
            }
            else
            {
                source.objectReferenceValue = choice;
                entry.FindPropertyRelative( "_create" ).boolValue = choice != null;
                entry.FindPropertyRelative( "_builtStamp" ).stringValue = "pending";
                if ( choice == null )
                {
                    entry.FindPropertyRelative( "_prefab" ).objectReferenceValue = null;
                    entry.FindPropertyRelative( "_uiPrefab" ).objectReferenceValue = null;
                }
                else
                {
                    ui = choice.transform is RectTransform
                        && choice.GetComponentInChildren<UnityEngine.UI.Graphic>( true ) != null;
                    entry.FindPropertyRelative( "_output" ).enumValueIndex = ui ? 1 : 0;
                    if ( ui == false )
                        entry.FindPropertyRelative( "_looping" ).boolValue =
                            VfxEffect.IsMaintained( choice );
                }
            }
            binding.FindPropertyRelative( "_looping" ).boolValue = entry.FindPropertyRelative( "_looping" ).boolValue;
        }

        Object target = binding.FindPropertyRelative( "_target" ).objectReferenceValue;
        string field = binding.FindPropertyRelative( "_field" ).stringValue;
        bool? required = VfxSetupUtility.RequiredLoop( target, field, module );
        if ( required.HasValue )
            binding.FindPropertyRelative( "_looping" ).boolValue = required.Value;
        if ( ui && source.objectReferenceValue != null && entry.FindPropertyRelative( "_create" ).boolValue )
        {
            EditorGUILayout.PropertyField( entry.FindPropertyRelative( "_looping" ), new GUIContent( "화면에서 중단할 반복 효과" ) );
            if ( entry.FindPropertyRelative( "_looping" ).boolValue == false )
                EditorGUILayout.PropertyField( entry.FindPropertyRelative( "_duration" ), new GUIContent( "단발 완료 시간" ) );
        }
        else if ( ui && entry.FindPropertyRelative( "_uiPrefab" ).objectReferenceValue is UiVfxEffect existing )
        {
            entry.FindPropertyRelative( "_looping" ).boolValue = existing.Looping;
            EditorGUILayout.LabelField( "재생", existing.Looping ? "화면에서 중단할 반복 효과"
                : existing.Duration + "초 (효과 프리팹 설정)" );
        }
        if ( module != VfxTargetModule.Character || target is VfxPlayer || target is UiVfxPlayer )
            binding.FindPropertyRelative( "_looping" ).boolValue = entry.FindPropertyRelative( "_looping" ).boolValue;
        string status = choice == null ? "미지정" : source.objectReferenceValue == null ? "준비된 효과 연결"
            : entry.FindPropertyRelative( "_builtStamp" ).stringValue == "pending" ? "원본 변경: 신규 구성 예정"
            : prepared.objectReferenceValue == null ? "신규 구성 후 연결" : "기존 생성 결과 검사 후 연결";
        EditorGUILayout.LabelField( "상태", status );
    }

    /// <summary>
    /// 신규 사용처에 빈 효과를 함께 준비
    /// </summary>
    void AddBinding ( SerializedProperty bindings, VfxTargetModule module )
    {
        bindings.serializedObject.ApplyModifiedProperties( );
        Undo.RecordObject( _profile, "VFX 사용처 추가" );
        _profile.AddManualBinding( module );
        EditorUtility.SetDirty( _profile );
        bindings.serializedObject.Update( );
        _inspection = null;
        _category = 0;
        _search = string.Empty;
    }

    /// <summary>
    /// 자동 추천의 원래 역할로 일반 공격과 스킬을 구분
    /// </summary>
    static string RoleLabel ( SerializedProperty row )
    {
        string key = row.FindPropertyRelative( "_suggestionKey" ).stringValue;
        string label = FieldLabel( row.FindPropertyRelative( "_field" ).stringValue );
        if ( string.IsNullOrEmpty( key ) )
            return label;
        string weapon = key.StartsWith( "sword.", StringComparison.Ordinal ) ? "검 "
            : key.StartsWith( "bow.", StringComparison.Ordinal ) ? "활 "
            : key.StartsWith( "staff.", StringComparison.Ordinal ) ? "스태프 " : string.Empty;
        string action = key.Contains( ".ultimate" ) ? "궁극기 " : key.Contains( ".skill" ) ? "스킬 "
            : key.Contains( ".attack" ) ? "일반 공격 " : string.Empty;
        return weapon + action + label;
    }

    /// <summary>
    /// 기본 화면에서는 직렬화 이름 대신 사용 역할 표시
    /// </summary>
    static string FieldLabel ( string field )
    {
        switch ( field )
        {
            case "_hit": return "피격";
            case "_death": return "사망";
            case "_revive": return "부활";
            case "_hireVfx": return "고용 등장";
            case "_burn": return "화상";
            case "_slow": return "감속";
            case "_stun": return "기절";
            case "_attackVfx": return "공격 실행";
            case "_ultimateVfx": return "궁극기 실행";
            case "_launchVfx": return "발사";
            case "_flightVfx": return "비행";
            case "_impactVfx": return "충돌";
            case "_spawnVfx": return "영역 생성";
            case "_useVfx": return "아이템 사용";
            case "_effect": return "효과 재생";
            default: return ObjectNames.NicifyVariableName( field );
        }
    }

    /// <summary>
    /// 프리팹의 기존 표현 컴포넌트 또는 캐릭터 표현 추가 선택
    /// </summary>
    static void DrawComponent ( SerializedProperty target, VfxTargetModule module )
    {
        Object value = target.objectReferenceValue;
        GameObject owner = value as GameObject;
        if ( value is Component component )
            owner = component.gameObject;
        if ( owner == null )
            return;
        var options = new List<Object> { owner };
        var labels = new List<string> { module + " 모듈" };
        foreach ( Component item in owner.GetComponents<Component>( ) )
        {
            if ( item == null || VfxSetupUtility.Fields( item ).Length == 0 )
                continue;
            options.Add( item );
            labels.Add( item.GetType( ).Name + " (" + ( options.Count - 1 ) + ")" );
        }
        int index = options.IndexOf( value );
        if ( index < 0 )
            return;
        int selected = EditorGUILayout.Popup( "연결 컴포넌트", index, labels.ToArray( ) );
        if ( selected != index )
            target.objectReferenceValue = options[selected];
    }

    /// <summary>
    /// 임의 필드 문자열 대신 지원 목록에서 선택
    /// </summary>
    static void DrawChoice ( string label, SerializedProperty property, string[] values, bool friendly = false )
    {
        string[] labels = new[] { "선택하세요" }.Concat( friendly ? values.Select( FieldLabel ) : values ).ToArray( );
        int index = Array.IndexOf( values, property.stringValue ) + 1;
        int selected = EditorGUILayout.Popup( label, index, labels );
        if ( selected != index )
            property.stringValue = selected == 0 ? string.Empty : values[selected - 1];
    }

    /// <summary>
    /// 지정 폴더의 파티클 프리팹을 기존 선택 보존하며 목록에 추가
    /// </summary>
    void ImportSources ()
    {
        string folder = AssetDatabase.GetAssetPath( _profile.SourceFolder );
        if ( AssetDatabase.IsValidFolder( folder ) == false )
        {
            _report = "선별 원본 폴더를 지정하세요.";
            return;
        }
        Undo.RecordObject( _profile, "VFX 원본 목록 추가" );
        foreach ( string guid in AssetDatabase.FindAssets( "t:Prefab", new[] { folder } ) )
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>( AssetDatabase.GUIDToAssetPath( guid ) );
            if ( source.GetComponentInChildren<ParticleSystem>( true ) != null
                || source.GetComponentInChildren<TrailRenderer>( true ) != null
                || source.GetComponentInChildren<UnityEngine.UI.Graphic>( true ) != null )
                _profile.AddSource( source );
        }
        EditorUtility.SetDirty( _profile );
        _inspection = null;
        _report = "원본 목록 추가 완료. 역할별 원본을 선택하거나 생성한 효과 키를 사용처에 배정하세요.";
    }

    /// <summary>
    /// 오류와 보존 항목을 복사 가능한 결과로 표시
    /// </summary>
    void Inspect ()
    {
        _inspection = VfxSetupUtility.Inspect( _profile );
        _report = "적용 범위 " + _profile.Scope + " / 검사 오류 " + _inspection.Errors.Count + "개 / 신규 효과 "
            + _inspection.Generations.Count( item => item.Existing == null )
            + "개 / 갱신 효과 " + _inspection.Generations.Count( item => item.Existing != null )
            + "개 / 연결 예정 " + _inspection.Changes.Count + "건\n"
            + string.Join( "\n", _inspection.Errors.Concat( _inspection.Notes ) );
        if ( _inspection.Errors.Count > 0 )
            Debug.LogError( "[VFX Setup] " + _report, _profile );
        else
            Debug.Log( "[VFX Setup] " + _report, _profile );
    }
}
#endif
