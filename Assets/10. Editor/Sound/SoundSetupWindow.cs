#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 사운드 설정 창 - 선택 결과 편집과 검사/대상 한정 적용
/// </summary>
public class SoundSetupWindow : EditorWindow
{
    [SerializeField] SoundSetupProfile _profile; //저장해 둔 편집 프로필
    [SerializeField] string _filter = string.Empty; //용도/ID/음원 검색어
    [SerializeField] bool _pendingOnly; //배정 필요 또는 등록 제외 항목만 표시 여부
    Vector2 _scroll; //설정 화면 스크롤
    Vector2 _errorScroll; //상단 검사 오류 목록 스크롤
    SoundSetupInspection _inspection; //최근 검사 결과
    string _result = string.Empty; //최근 가져오기/적용 결과

    /// <summary>
    /// 프로젝트 전용 사운드 설정 창 열기
    /// </summary>
    [MenuItem( "Tools/Sound Setup" )]
    public static void Open ()
    {
        GetWindow<SoundSetupWindow>( "Sound Setup" );
    }

    private void OnEnable ()
    {
        minSize = new Vector2( 660f, 420f );
        Undo.undoRedoPerformed += Invalidate;
        //프로젝트 변경은 표시만 갱신하고 실제 적용 직전에 현재 설정 재검사
        EditorApplication.projectChanged += Repaint;
    }

    private void OnGUI ()
    {
        EditorGUILayout.HelpBox(
            "편집 모드에서 음원 데이터와 지정한 참조를 구성합니다. 데이터 등록과 실제 재생 검증은 별개입니다.", MessageType.Info );
        Action request = null;
        using ( new EditorGUI.DisabledScope( SoundSetupUtility.CanEdit( ) == false ) )
        {
            EditorGUI.BeginChangeCheck( );
            _profile = ( SoundSetupProfile ) EditorGUILayout.ObjectField(
                "설정 프로필", _profile, typeof( SoundSetupProfile ), false );
            if ( EditorGUI.EndChangeCheck( ) == true )
                Invalidate( );
            if ( GUILayout.Button( "새 설정 프로필 만들기" ) == true )
                CreateProfile( );
            if ( _profile == null )
                return;

            //긴 배정표를 스크롤해도 검사와 적용 버튼을 항상 표시
            request = DrawActions( );
            DrawInspectionStatus( );
            _scroll = EditorGUILayout.BeginScrollView( _scroll );
            using ( var serialized = new SerializedObject( _profile ) )
            {
                serialized.Update( );
                EditorGUI.BeginChangeCheck( );
                Field( serialized, "_audioFolder", "음원 폴더" );
                Field( serialized, "_dataMap", "기존 데이터 맵" );
                if ( _profile.DataMap == null )
                    Field( serialized, "_newMapPath", "새 맵 경로" );
                Field( serialized, "_connectManager", "공통 관리자에 맵 연결" );
                if ( serialized.FindProperty( "_connectManager" ).boolValue == true )
                    Field( serialized, "_managerPrefab", "GameManager 프리팹" );
                Field( serialized, "_replaceExisting", "충돌하는 기존 값 교체 허용" );
                if ( EditorGUI.EndChangeCheck( ) == true )
                {
                    serialized.ApplyModifiedProperties( );
                    Invalidate( );
                }

                if ( GUILayout.Button( "선택 JSON 가져오기 / 미싱 음원 복구" ) == true )
                {
                    serialized.ApplyModifiedProperties( );
                    ImportSelections( );
                    serialized.Update( );
                }
                if ( GUILayout.Button( "사운드 연결 자동 구성 / 기존 수동 연결 유지" ) == true )
                {
                    serialized.ApplyModifiedProperties( );
                    _result = SoundSetupDiscovery.Discover( _profile );
                    Invalidate( );
                    serialized.Update( );
                }
                EditorGUILayout.Space( );
                DrawEntries( serialized );
                EditorGUILayout.Space( );
                DrawBindings( serialized );
                if ( serialized.ApplyModifiedProperties( ) == true )
                    Invalidate( );
            }
            EditorGUILayout.Space( );
            if ( _inspection != null )
            {
                foreach ( string note in _inspection.Notes )
                    EditorGUILayout.LabelField( note, EditorStyles.wordWrappedLabel );
            }
            EditorGUILayout.EndScrollView( );
        }
        //편집값과 GUI 레이아웃을 확정한 뒤 요청 실행
        request?.Invoke( );
    }

    /// <summary>
    /// 스크롤 위치와 관계없이 검사 결과와 적용 차단 사유 표시
    /// </summary>
    void DrawInspectionStatus ()
    {
        if ( string.IsNullOrEmpty( _result ) == false )
            EditorGUILayout.HelpBox( _result, _inspection != null && _inspection.Errors.Count > 0 ? MessageType.Error : MessageType.Info );
        if ( _inspection == null )
        {
            EditorGUILayout.LabelField( "적용/저장", "현재 설정을 검사해 주세요." );
            return;
        }
        if ( _inspection.Errors.Count == 0 )
        {
            EditorGUILayout.LabelField( "적용/저장", "검사 통과. 적용/저장할 수 있습니다." );
            return;
        }
        EditorGUILayout.LabelField( "적용/저장", $"검사 오류 {_inspection.Errors.Count}개로 비활성화" );
        _errorScroll = EditorGUILayout.BeginScrollView( _errorScroll, GUILayout.Height( 130f ) );
        foreach ( string error in _inspection.Errors.Distinct( ) )
            EditorGUILayout.HelpBox( error, MessageType.Error );
        EditorGUILayout.EndScrollView( );
    }

    /// <summary>
    /// 지정한 프로필 필드를 한국어 이름으로 표시
    /// </summary>
    /// <param name="serialized">프로필 직렬화 객체</param>
    /// <param name="name">필드 이름</param>
    /// <param name="label">화면 표시 이름</param>
    static void Field ( SerializedObject serialized, string name, string label )
    {
        EditorGUILayout.PropertyField( serialized.FindProperty( name ), new GUIContent( label ) );
    }

    /// <summary>
    /// 선택 용도와 실제 ID/종류/음원을 편집할 배정표 표시
    /// </summary>
    /// <param name="serialized">편집할 프로필</param>
    void DrawEntries ( SerializedObject serialized )
    {
        EditorGUILayout.LabelField( "음원 배정표", EditorStyles.boldLabel );
        EditorGUILayout.HelpBox( "이번 적용에 포함한 행만 기존 맵에 추가/교체합니다. 체크 해제나 프로필 행 삭제로 기존 맵 항목은 삭제하지 않습니다.", MessageType.Info );
        int reserveCount = _profile.Entries.Count( entry => entry.Register == false && entry.IsReserve == true );
        int pendingCount = _profile.Entries.Count( entry => ( entry.Register == true || entry.IsReserve == false ) && entry.Id == SoundId.None );
        EditorGUILayout.LabelField( $"사용 대상 ID 미배정 {pendingCount}행 / 예비 후보 {reserveCount}행" );
        _filter = EditorGUILayout.TextField( "검색", _filter );
        _pendingOnly = EditorGUILayout.Toggle( "배정 필요 / 이번 적용 제외만 표시", _pendingOnly );
        SerializedProperty entries = serialized.FindProperty( "_entries" );
        for ( int i = 0; i < entries.arraySize; i++ )
        {
            SerializedProperty row = entries.GetArrayElementAtIndex( i );
            string purpose = row.FindPropertyRelative( "_purpose" ).stringValue;
            SoundId id = ( SoundId ) row.FindPropertyRelative( "_id" ).intValue;
            AudioClip clip = row.FindPropertyRelative( "_clip" ).objectReferenceValue as AudioClip;
            string search = purpose + " " + id + " " + ( clip != null ? clip.name : string.Empty );
            if ( search.IndexOf( _filter, StringComparison.OrdinalIgnoreCase ) < 0 )
                continue;
            if ( _pendingOnly == true && row.FindPropertyRelative( "_register" ).boolValue == true
                && id != SoundId.None && clip != null )
                continue;

            EditorGUILayout.BeginVertical( EditorStyles.helpBox );
            EditorGUILayout.LabelField( purpose, EditorStyles.wordWrappedLabel );
            EditorGUILayout.BeginHorizontal( );
            EditorGUILayout.PropertyField( row.FindPropertyRelative( "_register" ), new GUIContent( "이번 적용", "기존 맵 항목은 체크를 해제해도 유지됩니다." ), GUILayout.Width( 100f ) );
            //enumValueIndex는 간격이 있는 SoundId의 실제 숫자와 다르므로 사용하지 않음
            SoundId selectedId = ( SoundId ) EditorGUILayout.EnumPopup( id );
            if ( selectedId != id )
                row.FindPropertyRelative( "_id" ).intValue = ( int ) selectedId;
            SoundType type = ( SoundType ) row.FindPropertyRelative( "_type" ).intValue;
            SoundType selectedType = ( SoundType ) EditorGUILayout.EnumPopup( type, GUILayout.Width( 95f ) );
            if ( selectedType != type )
                row.FindPropertyRelative( "_type" ).intValue = ( int ) selectedType;
            EditorGUILayout.EndHorizontal( );
            EditorGUILayout.PropertyField( row.FindPropertyRelative( "_clip" ), new GUIContent( "음원" ) );
            EditorGUILayout.Slider( row.FindPropertyRelative( "_volume" ), 0f, 1f, new GUIContent( "개별 음량" ) );
            EditorGUILayout.PropertyField( row.FindPropertyRelative( "_isReserve" ), new GUIContent( "예비 후보" ) );
            DrawEntryStatus( row );
            if ( row.FindPropertyRelative( "_type" ).intValue == ( int ) SoundType.Bgm )
                DrawBgmAssignment( row );
            EditorGUILayout.LabelField( "가져오기 당시 메모: " + row.FindPropertyRelative( "_note" ).stringValue, EditorStyles.wordWrappedLabel );
            EditorGUILayout.BeginHorizontal( );
            bool duplicate = GUILayout.Button( "같은 음원을 다른 ID에도 배정" );
            bool remove = GUILayout.Button( "프로필 행 삭제 / 기존 맵 유지" );
            EditorGUILayout.EndHorizontal( );
            EditorGUILayout.EndVertical( );
            if ( duplicate == true )
            {
                DuplicateEntry( entries, i );
                break;
            }
            if ( remove == true )
            {
                entries.DeleteArrayElementAtIndex( i );
                break;
            }
        }
        if ( GUILayout.Button( "빈 배정 행 추가" ) == true )
        {
            int index = entries.arraySize++;
            SerializedProperty row = entries.GetArrayElementAtIndex( index );
            row.FindPropertyRelative( "_register" ).boolValue = false;
            row.FindPropertyRelative( "_selectionKey" ).stringValue = string.Empty;
            row.FindPropertyRelative( "_purpose" ).stringValue = "수동 배정";
            row.FindPropertyRelative( "_id" ).intValue = 0;
            row.FindPropertyRelative( "_type" ).intValue = ( int ) SoundType.Sfx;
            row.FindPropertyRelative( "_clip" ).objectReferenceValue = null;
            row.FindPropertyRelative( "_volume" ).floatValue = 1f;
            row.FindPropertyRelative( "_note" ).stringValue = "ID와 음원을 지정하고 이번 적용을 체크하세요.";
            row.FindPropertyRelative( "_requiresLoopController" ).boolValue = false;
            row.FindPropertyRelative( "_isReserve" ).boolValue = false;
        }
    }

    /// <summary>
    /// 음원 배정과 등록 제외 및 지속 루프를 구분하여 표시
    /// </summary>
    /// <param name="row">편집 중인 배정 행</param>
    static void DrawEntryStatus ( SerializedProperty row )
    {
        string status;
        bool register = row.FindPropertyRelative( "_register" ).boolValue;
        if ( register == false && row.FindPropertyRelative( "_isReserve" ).boolValue == true )
            status = "예비 후보 / 현재 사용 대상 제외";
        else if ( row.FindPropertyRelative( "_requiresLoopController" ).boolValue == true
            && row.FindPropertyRelative( "_type" ).intValue != ( int ) SoundType.LoopSfx )
            status = "반복음 배정 필요 / LoopSfx 사용";
        else if ( row.FindPropertyRelative( "_id" ).intValue == ( int ) SoundId.None )
            status = "ID 배정 필요";
        else if ( row.FindPropertyRelative( "_clip" ).objectReferenceValue == null )
            status = "음원 지정 필요";
        else
            status = register ? "이번 적용에 포함" : "이번 적용 제외 / 기존 맵 유지";
        EditorGUILayout.LabelField( "배정 상태", status );
    }

    /// <summary>
    /// 배경음의 사용처를 선택하고 단일 클립 등록 조건 안내
    /// </summary>
    /// <param name="row">배경음 후보 행</param>
    static void DrawBgmAssignment ( SerializedProperty row )
    {
        EditorGUILayout.LabelField( "TitleBgm / PlayBgm은 각각 등록된 클립 하나만 사용할 수 있습니다.", EditorStyles.wordWrappedLabel );
        EditorGUILayout.BeginHorizontal( );
        bool title = GUILayout.Button( "타이틀 BGM으로 배정" );
        bool play = GUILayout.Button( "플레이 BGM으로 배정" );
        EditorGUILayout.EndHorizontal( );
        if ( title == false && play == false )
            return;
        row.FindPropertyRelative( "_id" ).intValue = ( int ) ( title ? SoundId.TitleBgm : SoundId.PlayBgm );
        row.FindPropertyRelative( "_register" ).boolValue = true;
        row.FindPropertyRelative( "_isReserve" ).boolValue = false;
    }

    /// <summary>
    /// Unity 배열 삽입의 초기값에 의존하지 않고 현재 편집 중인 행을 복제
    /// </summary>
    /// <param name="entries">배정 배열</param>
    /// <param name="index">복제할 행 번호</param>
    static void DuplicateEntry ( SerializedProperty entries, int index )
    {
        SerializedProperty row = entries.GetArrayElementAtIndex( index );
        var value = new SoundSetupEntry(
            string.Empty, row.FindPropertyRelative( "_purpose" ).stringValue,
            row.FindPropertyRelative( "_clip" ).objectReferenceValue as AudioClip,
            row.FindPropertyRelative( "_note" ).stringValue,
            row.FindPropertyRelative( "_requiresLoopController" ).boolValue,
            row.FindPropertyRelative( "_isReserve" ).boolValue );
        value.SetRegistration(
            ( SoundId ) row.FindPropertyRelative( "_id" ).intValue,
            ( SoundType ) row.FindPropertyRelative( "_type" ).intValue,
            row.FindPropertyRelative( "_register" ).boolValue,
            row.FindPropertyRelative( "_volume" ).floatValue );
        entries.InsertArrayElementAtIndex( index + 1 );
        SerializedProperty copy = entries.GetArrayElementAtIndex( index + 1 );
        copy.FindPropertyRelative( "_selectionKey" ).stringValue = string.Empty;
        copy.FindPropertyRelative( "_register" ).boolValue = value.Register;
        copy.FindPropertyRelative( "_purpose" ).stringValue = value.Purpose;
        copy.FindPropertyRelative( "_id" ).intValue = ( int ) value.Id;
        copy.FindPropertyRelative( "_type" ).intValue = ( int ) value.Type;
        copy.FindPropertyRelative( "_clip" ).objectReferenceValue = value.Clip;
        copy.FindPropertyRelative( "_volume" ).floatValue = value.Volume;
        copy.FindPropertyRelative( "_note" ).stringValue = value.Note;
        copy.FindPropertyRelative( "_requiresLoopController" ).boolValue = value.RequiresLoopController;
        copy.FindPropertyRelative( "_isReserve" ).boolValue = value.IsReserve;
    }

    /// <summary>
    /// 서비스 전달 확인과 공유 변경 정책을 포함한 명시 대상 편집
    /// </summary>
    /// <param name="serialized">편집할 프로필</param>
    void DrawBindings ( SerializedObject serialized )
    {
        EditorGUILayout.LabelField( "지정 대상 연결", EditorStyles.boldLabel );
        EditorGUILayout.HelpBox(
            "ProjectileData, 화살비/불기둥 영역 또는 화염 장판 프리팹을 지정합니다. "
            + "FireTrapLoop 연결은 필요한 FireTrapSound/SoundLoopPlayer 모듈도 추가합니다. "
            + "투사체/궁극기 영역의 서비스 전달과 실제 출력은 플레이에서 확인하세요. "
            + "스태프 스킬은 실행자의 전용 ID를 사용하므로 투사체 필드에 배정하지 않습니다.", MessageType.Info );
        SerializedProperty bindings = serialized.FindProperty( "_bindings" );
        for ( int i = 0; i < bindings.arraySize; i++ )
        {
            SerializedProperty row = bindings.GetArrayElementAtIndex( i );
            EditorGUILayout.BeginVertical( EditorStyles.helpBox );
            EditorGUI.BeginChangeCheck( );
            Object bindingTarget = row.FindPropertyRelative( "_target" ).objectReferenceValue;
            Object selectedTarget = EditorGUILayout.ObjectField( "연결 대상", bindingTarget, typeof( Object ), false );
            if ( selectedTarget != bindingTarget )
                row.FindPropertyRelative( "_target" ).objectReferenceValue = selectedTarget;
            SoundSetupField field = ( SoundSetupField ) row.FindPropertyRelative( "_field" ).intValue;
            if ( Enum.IsDefined( typeof( SoundSetupField ), field ) == false )
                EditorGUILayout.HelpBox( "지원하지 않는 연결 필드입니다. 지원하는 필드를 지정하거나 이 행을 삭제하세요.", MessageType.Warning );
            string[] fields = { "투사체 발사", "투사체 충돌", "영역 생성", "영역 주기", "화염 장판 루프" };
            int selected = EditorGUILayout.Popup( "필드 종류", ( int ) field >= 0 && ( int ) field < fields.Length ? ( int ) field : -1, fields );
            if ( selected >= 0 && selected != ( int ) field )
                row.FindPropertyRelative( "_field" ).intValue = selected;
            SoundId id = ( SoundId ) row.FindPropertyRelative( "_id" ).intValue;
            SoundId selectedId = ( SoundId ) EditorGUILayout.EnumPopup( "사운드 ID", id );
            if ( selectedId != id )
                row.FindPropertyRelative( "_id" ).intValue = ( int ) selectedId;
            if ( EditorGUI.EndChangeCheck( ) == true )
            {
                //대상/필드/ID를 바꾼 연결은 수동 편집으로 보존하며 이전 확인은 재사용하지 않음
                row.FindPropertyRelative( "_isAutomatic" ).boolValue = false;
                row.FindPropertyRelative( "_serviceConnected" ).boolValue = false;
                row.FindPropertyRelative( "_allowSharedChange" ).boolValue = false;
            }
            bool loop = row.FindPropertyRelative( "_field" ).intValue == ( int ) SoundSetupField.FireTrapLoop;
            if ( loop == true )
                EditorGUILayout.PropertyField( row.FindPropertyRelative( "_serviceConnected" ),
                    new GUIContent( "서비스/작동 요청 연결 확인" ) );
            EditorGUILayout.PropertyField( row.FindPropertyRelative( "_allowSharedChange" ), new GUIContent( "표시된 공유 사용처 변경 허용" ) );
            bool remove = GUILayout.Button( "연결 행 삭제" );
            EditorGUILayout.EndVertical( );
            if ( remove == true )
            {
                bindings.DeleteArrayElementAtIndex( i );
                break;
            }
        }
        if ( GUILayout.Button( "연결 대상 추가" ) == true )
        {
            int index = bindings.arraySize++;
            SerializedProperty row = bindings.GetArrayElementAtIndex( index );
            row.FindPropertyRelative( "_target" ).objectReferenceValue = null;
            row.FindPropertyRelative( "_field" ).intValue = 0;
            row.FindPropertyRelative( "_id" ).intValue = 0;
            row.FindPropertyRelative( "_serviceConnected" ).boolValue = false;
            row.FindPropertyRelative( "_allowSharedChange" ).boolValue = false;
            row.FindPropertyRelative( "_isAutomatic" ).boolValue = false;
        }
    }

    /// <summary>
    /// 편집값 반영 후 실행할 프로필 요청 선택
    /// </summary>
    /// <returns>선택한 요청 또는 null</returns>
    Action DrawActions ()
    {
        Action request = null;
        EditorGUILayout.BeginHorizontal( );
        if ( GUILayout.Button( "프로필만 저장" ) == true )
            request = SaveProfile;
        if ( GUILayout.Button( "현재 설정 검사" ) == true )
            request = InspectProfile;
        using ( new EditorGUI.DisabledScope( _inspection == null || _inspection.Errors.Count > 0 ) )
        {
            if ( GUILayout.Button( "검사한 설정 적용 / 저장" ) == true )
                request = ApplyProfile;
        }
        EditorGUILayout.EndHorizontal( );
        return request;
    }

    /// <summary>
    /// 현재 편집 프로필만 저장
    /// </summary>
    void SaveProfile ()
    {
        AssetDatabase.SaveAssetIfDirty( _profile );
        _result = "프로필 저장 요청 완료. 맵과 연결 대상은 변경하지 않았습니다.";
    }

    /// <summary>
    /// 현재 편집값으로 병합 결과와 지정 참조 재검사
    /// </summary>
    void InspectProfile ()
    {
        _inspection = SoundSetupUtility.Inspect( _profile );
        _result = $"데이터/지정 참조 검사: 오류 {_inspection.Errors.Count}개, 병합 결과 {_inspection.Sounds.Count}개 ID. 실제 재생은 미검증입니다.";
        LogInspectionErrors( );
        _errorScroll = Vector2.zero;
        Repaint( );
    }

    /// <summary>
    /// 검사 오류 전체를 한 번에 복사할 수 있도록 콘솔에 묶어서 출력
    /// </summary>
    void LogInspectionErrors ()
    {
        if ( _inspection.Errors.Count == 0 )
            return;
        Debug.LogError( $"[Sound Setup] {_profile.name}: 검사 오류 {_inspection.Errors.Count}개\n"
            + string.Join( "\n", _inspection.Errors.Distinct( ) ), _profile );
    }

    /// <summary>
    /// 현재 설정을 재검사하고 표시한 대상의 저장 요청
    /// </summary>
    void ApplyProfile ()
    {
        _inspection = SoundSetupUtility.Inspect( _profile );
        LogInspectionErrors( );
        _errorScroll = Vector2.zero;
        Repaint( );
        if ( _inspection.Errors.Count > 0 )
        {
            _result = "현재 설정에 오류가 있어 적용하지 않았습니다. 검사 결과를 확인하세요.";
            return;
        }
        if ( EditorUtility.DisplayDialog( "사운드 설정 저장",
            "맵과 지정 참조/사운드 모듈을 저장합니다. 에셋 파일 저장 전체는 Ctrl+Z로 되돌릴 수 없습니다.\n"
            + "대상: " + _inspection.MapPath + "\n변경 작업: " + _inspection.Changes.Count
            + "\n이번 적용 제외/프로필 행 삭제는 기존 맵 항목을 삭제하지 않습니다.", "적용", "취소" ) == false )
            return;
        _result = SoundSetupUtility.Apply( _profile );
        _inspection = SoundSetupUtility.Inspect( _profile );
        LogInspectionErrors( );
        Repaint( );
    }

    /// <summary>
    /// 사용자 지정 위치에 편집 프로필만 생성
    /// </summary>
    void CreateProfile ()
    {
        string path = EditorUtility.SaveFilePanelInProject( "사운드 프로필 생성", "SoundSetupProfile", "asset",
            "편집 프로필 저장 위치를 선택하세요." );
        if ( string.IsNullOrEmpty( path ) == true )
            return;
        if ( AssetDatabase.LoadMainAssetAtPath( path ) != null || File.Exists( path ) == true )
        {
            _result = "기존 파일은 덮어쓰지 않습니다. 다른 경로를 선택하세요.";
            return;
        }
        _profile = CreateInstance<SoundSetupProfile>( );
        _profile.InitializeDefaults(
            AssetDatabase.LoadAssetAtPath<DefaultAsset>( SoundSetupProfile.DefaultAudioFolderPath ),
            AssetDatabase.LoadAssetAtPath<GameObject>( "Assets/Resources/GameManager.prefab" ) );
        AssetDatabase.CreateAsset( _profile, path );
        AssetDatabase.SaveAssetIfDirty( _profile );
        Selection.activeObject = _profile;
        Invalidate( );
    }

    /// <summary>
    /// 선택 JSON 파일을 읽어 편집 프로필에 병합
    /// </summary>
    void ImportSelections ()
    {
        string path = EditorUtility.OpenFilePanel( "선택 경로 내보내기 JSON", string.Empty, "json" );
        if ( string.IsNullOrEmpty( path ) == true )
            return;
        try
        {
            _result = SoundSelectionImporter.Import( _profile, path );
            Invalidate( );
        }
        catch ( JsonException exception )
        {
            _result = "JSON 형식 확인 필요: " + exception.Message;
        }
        catch ( IOException exception )
        {
            _result = "파일 읽기 실패: " + exception.Message;
        }
        catch ( UnauthorizedAccessException exception )
        {
            _result = "파일 접근 실패: " + exception.Message;
        }
        catch ( ArgumentException exception )
        {
            _result = "선택 파일 확인 필요: " + exception.Message;
        }
    }

    /// <summary>
    /// 편집/프로젝트 변경 이후 이전 검사 결과를 적용에 재사용하지 않음
    /// </summary>
    void Invalidate ()
    {
        _inspection = null;
        Repaint( );
    }

    private void OnDisable ()
    {
        Undo.undoRedoPerformed -= Invalidate;
        EditorApplication.projectChanged -= Repaint;
    }
}
#endif
