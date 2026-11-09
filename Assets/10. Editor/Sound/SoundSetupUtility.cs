#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

/// <summary>
/// 사운드 설정 검사 결과 - 외부에서 변경하지 않는 검사 결과 조회
/// </summary>
public class SoundSetupInspection
{
    IReadOnlyList<string> _errors; //적용을 막는 설정 오류
    IReadOnlyList<string> _notes; //변경 예정과 미완료 연계 안내
    IReadOnlyList<SoundSetupValue> _sounds; //기존 항목을 보존한 병합 결과
    IReadOnlyList<SoundSetupChange> _changes; //명시적으로 지정한 필드 변경
    SoundDataMap _map; //현재 데이터 맵
    string _mapPath; //출력 에셋 경로
    bool _writeMap; //맵 생성 또는 내용 변경 여부

    /// <summary>
    /// 적용을 막는 설정 오류
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// 변경 예정과 미완료 연계 안내
    /// </summary>
    public IReadOnlyList<string> Notes => _notes;

    /// <summary>
    /// 기존 항목을 보존한 병합 결과
    /// </summary>
    public IReadOnlyList<SoundSetupValue> Sounds => _sounds;

    /// <summary>
    /// 명시적으로 지정한 필드 변경
    /// </summary>
    public IReadOnlyList<SoundSetupChange> Changes => _changes;

    /// <summary>
    /// 현재 데이터 맵
    /// </summary>
    public SoundDataMap Map => _map;

    /// <summary>
    /// 출력 에셋 경로
    /// </summary>
    public string MapPath => _mapPath;

    /// <summary>
    /// 맵 생성 또는 내용 변경 여부
    /// </summary>
    public bool WriteMap => _writeMap;

    /// <summary>
    /// 검사가 끝난 작업의 결과와 변경 계획을 조회 대상으로 구성
    /// </summary>
    /// <param name="errors">설정 오류</param>
    /// <param name="notes">변경/연계 안내</param>
    /// <param name="sounds">병합한 사운드</param>
    /// <param name="changes">필드 변경</param>
    /// <param name="map">기존 맵</param>
    /// <param name="mapPath">출력 경로</param>
    /// <param name="writeMap">맵 변경 필요 여부</param>
    public SoundSetupInspection ( IReadOnlyList<string> errors, IReadOnlyList<string> notes,
        IReadOnlyList<SoundSetupValue> sounds, IReadOnlyList<SoundSetupChange> changes,
        SoundDataMap map, string mapPath, bool writeMap )
    {
        _errors = errors;
        _notes = notes;
        _sounds = sounds;
        _changes = changes;
        _map = map;
        _mapPath = mapPath;
        _writeMap = writeMap;
    }
}

/// <summary>
/// 사운드 맵 편집값 - 검사와 저장에 전달할 ID별 설정
/// </summary>
public class SoundSetupValue
{
    SoundId _id; //실제 숫자로 저장하는 식별자
    SoundType _type; //출력 종류
    float _volume; //개별 음량
    IReadOnlyList<AudioClip> _clips; //중복을 제거한 음원 후보

    /// <summary>
    /// 실제 숫자로 저장하는 식별자
    /// </summary>
    public SoundId Id => _id;

    /// <summary>
    /// 출력 종류
    /// </summary>
    public SoundType Type => _type;

    /// <summary>
    /// 개별 음량
    /// </summary>
    public float Volume => _volume;

    /// <summary>
    /// 중복을 제거한 음원 후보
    /// </summary>
    public IReadOnlyList<AudioClip> Clips => _clips;

    /// <summary>
    /// 식별자와 출력 및 음원 후보를 하나의 등록값으로 구성
    /// </summary>
    /// <param name="id">등록 식별자</param>
    /// <param name="type">출력 종류</param>
    /// <param name="volume">개별 음량</param>
    /// <param name="clips">음원 후보</param>
    public SoundSetupValue ( SoundId id, SoundType type, float volume, IReadOnlyList<AudioClip> clips )
    {
        _id = id;
        _type = type;
        _volume = volume;
        _clips = clips;
    }
}

/// <summary>
/// 사운드 필드 변경 - 적용과 복구에 사용할 값의 묶음
/// </summary>
public class SoundSetupChange
{
    Object _target; //설정 에셋 또는 프리팹 컴포넌트
    string _field; //변경할 기존 직렬화 필드
    int _beforeId; //기존 식별자 숫자
    SoundId _afterId; //적용할 식별자
    Object _beforeReference; //관리자의 기존 데이터 맵
    bool _isMapReference; //관리자의 맵 연결 여부
    bool _addLoopModule; //화염 장판 사운드 모듈 추가 여부
    bool _addLoopPlayer; //이번 작업에서 반복 출력 모듈도 추가할지 여부
    GlobalObjectId _objectId; //재저장 이후 원본을 다시 찾을 식별자

    /// <summary>
    /// 설정 에셋 또는 프리팹 컴포넌트
    /// </summary>
    public Object Target => _target;

    /// <summary>
    /// 변경할 기존 직렬화 필드
    /// </summary>
    public string Field => _field;

    /// <summary>
    /// 기존 식별자 숫자
    /// </summary>
    public int BeforeId => _beforeId;

    /// <summary>
    /// 적용할 식별자
    /// </summary>
    public SoundId AfterId => _afterId;

    /// <summary>
    /// 관리자의 기존 데이터 맵
    /// </summary>
    public Object BeforeReference => _beforeReference;

    /// <summary>
    /// 관리자의 맵 연결 여부
    /// </summary>
    public bool IsMapReference => _isMapReference;

    /// <summary>
    /// 화염 장판 사운드 모듈 추가 여부
    /// </summary>
    public bool AddLoopModule => _addLoopModule;

    /// <summary>
    /// 이번 작업에서 반복 출력 모듈도 추가할지 여부
    /// </summary>
    public bool AddLoopPlayer => _addLoopPlayer;

    /// <summary>
    /// 재저장 이후 원본을 다시 찾을 식별자
    /// </summary>
    public GlobalObjectId ObjectId => _objectId;

    /// <summary>
    /// 하나의 필드 변경과 원본 식별자를 함께 보관
    /// </summary>
    /// <param name="target">필드 소유 객체</param>
    /// <param name="field">필드 이름</param>
    SoundSetupChange ( Object target, string field )
    {
        _target = target;
        _field = field;
        _objectId = GlobalObjectId.GetGlobalObjectIdSlow( target );
    }

    /// <summary>
    /// 사운드 식별자의 이전 값과 변경값 기록
    /// </summary>
    /// <param name="target">필드 소유 객체</param>
    /// <param name="field">필드 이름</param>
    /// <param name="before">기존 식별자</param>
    /// <param name="after">적용할 식별자</param>
    /// <returns>식별자 변경 계획</returns>
    public static SoundSetupChange ForId ( Object target, string field, int before, SoundId after )
    {
        var change = new SoundSetupChange( target, field );
        change._beforeId = before;
        change._afterId = after;
        return change;
    }

    /// <summary>
    /// 관리자의 기존 데이터 맵 연결 기록
    /// </summary>
    /// <param name="target">사운드 관리자</param>
    /// <param name="before">기존 데이터 맵</param>
    /// <returns>맵 연결 변경 계획</returns>
    public static SoundSetupChange ForMap ( SoundManager target, Object before )
    {
        var change = new SoundSetupChange( target, "_soundDataMap" );
        change._beforeReference = before;
        change._isMapReference = true;
        return change;
    }

    /// <summary>
    /// 지정한 화염 장판에 추가할 사운드 모듈과 원래 출력 존재 여부 기록
    /// </summary>
    /// <param name="trap">프리팹 원본의 화염 장판</param>
    /// <param name="id">추가할 모듈의 지속음</param>
    /// <returns>모듈 추가와 실패 시 제거할 범위</returns>
    public static SoundSetupChange ForLoopModule ( FireTrap trap, SoundId id )
    {
        var change = new SoundSetupChange( trap, "_loopSound" );
        change._afterId = id;
        change._addLoopModule = true;
        change._addLoopPlayer = trap.GetComponent<SoundLoopPlayer>( ) == null;
        return change;
    }

}

/// <summary>
/// 사운드 설정 작업 - 편집 모드 검사와 대상 한정 저장/복구
/// </summary>
public class SoundSetupUtility
{
    List<string> _errors = new List<string>( ); //이번 검사에서 확인한 설정 오류
    List<string> _notes = new List<string>( ); //이번 검사에서 확인한 변경과 연계 안내
    List<SoundSetupValue> _sounds = new List<SoundSetupValue>( ); //기존 항목을 보존한 병합 결과
    List<SoundSetupChange> _changes = new List<SoundSetupChange>( ); //명시적인 필드 변경
    Dictionary<string, SoundId> _requestedIds = new Dictionary<string, SoundId>( ); //필드별 요청 충돌 검사
    Dictionary<string, string[]> _users = new Dictionary<string, string[]>( ); //이번 검사에 한정한 공유 사용처
    SoundDataMap _map; //검사 중인 기존 맵
    string _mapPath; //명시한 출력 경로
    bool _writeMap; //맵 생성/변경 필요 여부

    /// <summary>
    /// 공유 상태 없이 검사 작업 하나를 생성
    /// </summary>
    SoundSetupUtility ()
    {
    }

    /// <summary>
    /// 플레이/컴파일/임포트 중 에셋 변경 거절
    /// </summary>
    /// <returns>편집 가능한 상태인지 여부</returns>
    public static bool CanEdit ()
    {
        return EditorApplication.isPlayingOrWillChangePlaymode == false
            && EditorApplication.isCompiling == false && EditorApplication.isUpdating == false;
    }

    /// <summary>
    /// 현재 맵과 배정표 및 지정한 대상을 변경 없이 검사
    /// </summary>
    /// <param name="profile">검사할 편집 프로필</param>
    /// <returns>적용 직전에도 다시 구성할 검사 결과</returns>
    public static SoundSetupInspection Inspect ( SoundSetupProfile profile )
    {
        return new SoundSetupUtility( ).InspectCore( profile );
    }

    /// <summary>
    /// 이번 검사 인스턴스가 병합과 연결 계획을 소유
    /// </summary>
    /// <param name="profile">검사할 프로필</param>
    /// <returns>검사가 끝난 결과의 읽기 전용 조회</returns>
    SoundSetupInspection InspectCore ( SoundSetupProfile profile )
    {
        if ( CanEdit( ) == false )
            _errors.Add( "플레이/컴파일/임포트 중에는 적용할 수 없습니다." );
        if ( profile == null || EditorUtility.IsPersistent( profile ) == false )
        {
            _errors.Add( "프로젝트에 저장한 SoundSetupProfile을 지정하세요." );
            return CreateResult( );
        }
        _mapPath = profile.DataMap != null
            ? AssetDatabase.GetAssetPath( profile.DataMap ) : profile.NewMapPath;
        if ( ValidAssetPath( _mapPath, ".asset" ) == false )
        {
            _errors.Add( "기존 Assets 폴더 아래의 .asset 경로를 지정하세요. 상위 폴더는 먼저 생성해야 합니다." );
            return CreateResult( );
        }
        Object existing = AssetDatabase.LoadMainAssetAtPath( _mapPath );
        if ( existing != null && existing is SoundDataMap == false )
        {
            _errors.Add( "맵 경로에 다른 종류의 에셋이 있습니다. 덮어쓰지 않습니다." );
            return CreateResult( );
        }
        _map = existing as SoundDataMap;
        if ( _map == null && File.Exists( _mapPath ) == true )
            _errors.Add( "맵 경로의 파일을 Unity 에셋으로 읽을 수 없습니다." );
        if ( _map != null && EditorUtility.IsDirty( _map ) == true )
            _errors.Add( "기존 맵의 미저장 변경을 먼저 저장하거나 되돌리세요." );

        List<SoundSetupValue> original = _map != null ? ReadMap( _map ) : new List<SoundSetupValue>( );
        _sounds.AddRange( original );
        foreach ( SoundSetupValue value in BuildEntries( profile ) )
        {
            int index = _sounds.FindIndex( item => item.Id == value.Id );
            if ( index < 0 )
            {
                _sounds.Add( value );
                _notes.Add( $"[등록] {value.Id}: {Describe( value )}" );
            }
            else if ( SameValue( _sounds[index], value ) == false )
            {
                if ( profile.ReplaceExisting == false )
                    _errors.Add( $"기존 {value.Id}와 배정이 다릅니다. 교체 허용 전에는 유지합니다." );
                _notes.Add( $"[교체 예정] {value.Id}: {Describe( _sounds[index] )} → {Describe( value )}" );
                _sounds[index] = value;
            }
        }
        ValidateSounds( );
        _writeMap = _map == null || original.Count != _sounds.Count
            || original.Where( ( value, index ) => SameValue( value, _sounds[index] ) == false ).Any( );
        _notes.Add( _writeMap ? "[맵 저장 예정] " + _mapPath : "[맵 유지] " + _mapPath );
        if ( profile.ConnectManager == true )
            InspectManager( profile );
        foreach ( SoundSetupBinding binding in profile.Bindings )
            InspectBinding( profile, binding );
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage( );
        foreach ( SoundSetupChange change in _changes )
        {
            string path = AssetDatabase.GetAssetPath( change.Target );
            if ( stage != null && stage.assetPath == path )
                _errors.Add( "편집 중인 프리팹을 닫고 저장한 뒤 적용하세요: " + path );
            if ( EditorUtility.IsDirty( change.Target ) == true )
                _errors.Add( "대상의 미저장 변경을 먼저 저장하세요: " + path );
        }
        if ( _errors.Count == 0 )
            _notes.Add( $"[데이터/지정 참조 검사 통과] 병합 결과 {_sounds.Count}개 ID. 실제 재생은 플레이 검증 대상입니다." );
        return CreateResult( );
    }

    /// <summary>
    /// 등록한 행을 ID별로 묶고 종류/음량 충돌 검사
    /// </summary>
    /// <param name="profile">입력 프로필</param>
    /// <returns>이번에 반영할 ID별 설정</returns>
    List<SoundSetupValue> BuildEntries ( SoundSetupProfile profile )
    {
        foreach ( SoundSetupEntry entry in profile.Entries )
        {
            if ( entry.RequiresLoopController == true && entry.Register == true && entry.Type != SoundType.LoopSfx )
            {
                _errors.Add( entry.Purpose + ": 지속음은 LoopSfx로 등록해야 합니다." );
            }
            else if ( entry.Register == false )
            {
                if ( entry.IsReserve == true )
                    _notes.Add( "[예비 후보] " + entry.Purpose );
                else if ( entry.Id == SoundId.None )
                    _notes.Add( ( entry.Type == SoundType.Bgm ? "[배경음 사용처 배정 필요] " : "[ID 배정 필요] " ) + entry.Purpose );
                else
                    _notes.Add( "[이번 적용 제외 / 기존 맵 유지] " + entry.Purpose );
            }
        }

        var values = new List<SoundSetupValue>( );
        foreach ( var group in profile.Entries.Where( entry => entry.Register == true ).GroupBy( entry => entry.Id ) )
        {
            SoundSetupEntry first = group.First( );
            if ( group.Any( entry => entry.Type != first.Type || entry.Volume != first.Volume ) == true )
                _errors.Add( $"{group.Key}: 같은 ID의 출력 종류와 음량이 서로 다릅니다." );
            values.Add( new SoundSetupValue( group.Key, first.Type, first.Volume,
                group.Select( entry => entry.Clip ).Distinct( ).ToArray( ) ) );
        }
        return values;
    }

    /// <summary>
    /// 등록값의 유효성과 현재 호출에 필요한 항목 검사
    /// </summary>
    void ValidateSounds ()
    {
        if ( _sounds.Count == 0 )
            _errors.Add( "등록할 사운드를 하나 이상 지정하세요. 빈 맵은 생성하지 않습니다." );
        var ids = new HashSet<SoundId>( );
        foreach ( SoundSetupValue value in _sounds )
        {
            bool validId = value.Id != SoundId.None && Enum.IsDefined( typeof( SoundId ), value.Id );
            if ( validId == false || ids.Add( value.Id ) == false )
                _errors.Add( $"식별자 미지정/중복/정의 누락: {( int ) value.Id}" );
            if ( Enum.IsDefined( typeof( SoundType ), value.Type ) == false
                || ( value.Volume >= 0f && value.Volume <= 1f ) == false )
                _errors.Add( $"{value.Id}: 출력 종류 또는 음량이 유효하지 않습니다." );
            if ( value.Clips.Count == 0 || value.Clips.Any( clip => clip == null
                || AssetDatabase.GetAssetPath( clip ).StartsWith( "Assets/", StringComparison.Ordinal ) == false ) )
                _errors.Add( $"{value.Id}: Assets 내부 AudioClip이 필요합니다." );
            if ( ( value.Type == SoundType.Bgm || value.Type == SoundType.LoopSfx ) && value.Clips.Count != 1 )
                _errors.Add( $"{value.Id}: BGM/반복 효과음은 클립 하나만 지정해야 합니다." );

            if ( validId == true )
            {
                SoundType expected = SoundSelectionImporter.GetSoundType( value.Id );
                if ( value.Type != expected )
                    _errors.Add( $"{value.Id}: 기존 호출은 {expected} 출력을 사용해야 합니다." );
            }
        }
        foreach ( SoundId id in Enum.GetValues( typeof( SoundId ) ) )
        {
            if ( id != SoundId.None && ( int ) id < 210 && ids.Contains( id ) == false )
                _notes.Add( $"[현재 호출 ID 미등록] {id}. 게임 진행을 막지는 않지만 호출 시 소리 누락/오류 로그가 발생합니다." );
        }
    }

    /// <summary>
    /// 공통 관리자 프리팹의 기존 서비스와 믹서/출력 연결 검사
    /// </summary>
    /// <param name="profile">연결 설정</param>
    void InspectManager ( SoundSetupProfile profile )
    {
        GameObject root = profile.ManagerPrefab;
        if ( root == null || ValidAssetPath( AssetDatabase.GetAssetPath( root ), ".prefab" ) == false
            || PrefabUtility.GetPrefabAssetType( root ) != PrefabAssetType.Regular )
        {
            _errors.Add( "관리자는 Assets 내부의 일반 프리팹 원본을 지정하세요." );
            return;
        }
        SoundManager[] managers = root.GetComponentsInChildren<SoundManager>( true );
        GameManager[] games = root.GetComponentsInChildren<GameManager>( true );
        if ( managers.Length != 1 || games.Length != 1 )
        {
            _errors.Add( "관리자 프리팹에는 GameManager와 SoundManager가 각각 하나 있어야 합니다." );
            return;
        }
        SoundManager manager = managers[0];
        if ( manager.enabled == false || IsActiveInPrefab( manager.transform ) == false )
            _errors.Add( "SoundManager와 상위 오브젝트가 활성화되어야 합니다." );
        if ( ReadReference( games[0], "_sound" ) != manager )
            _errors.Add( "GameManager._sound가 같은 프리팹의 SoundManager와 일치하지 않습니다." );
        AudioMixer mixer = ReadReference( manager, "_mixer" ) as AudioMixer;
        AudioSource bgm = ReadReference( manager, "_bgm" ) as AudioSource;
        AudioSource ui = ReadReference( manager, "_uiSfx" ) as AudioSource;
        AudioSource sfx = ReadReference( manager, "_gameplaySfx" ) as AudioSource;
        if ( mixer == null || bgm == null || ui == null || sfx == null )
        {
            _errors.Add( "관리자의 믹서와 출력 3개를 먼저 연결하세요. 도구가 새 출력을 만들지 않습니다." );
            return;
        }
        if ( bgm == ui || bgm == sfx || ui == sfx )
            _errors.Add( "BGM/UI/플레이 효과음은 서로 다른 AudioSource여야 합니다." );
        foreach ( AudioSource source in new[] { bgm, ui, sfx } )
        {
            if ( source.transform.IsChildOf( root.transform ) == false
                || source.outputAudioMixerGroup == null || source.outputAudioMixerGroup.audioMixer != mixer
                || source.enabled == false || IsActiveInPrefab( source.transform ) == false )
                _errors.Add( $"{source.name}: 같은 프리팹의 활성 출력과 지정 믹서 그룹이 필요합니다." );
            if ( source.playOnAwake == true || source.mute == true || source.volume == 0f )
                _notes.Add( $"[출력 확인] {source.name}: 자동 재생/음소거/출력 음량 설정을 확인하세요." );
        }
        if ( ui.outputAudioMixerGroup != sfx.outputAudioMixerGroup || bgm.outputAudioMixerGroup == sfx.outputAudioMixerGroup )
            _errors.Add( "UI/플레이 효과음은 같은 SFX 그룹, BGM은 별도 그룹을 사용해야 합니다." );
        foreach ( string parameter in new[] { "MasterVolume", "BgmVolume", "SfxVolume" } )
        {
            float ignored;
            if ( mixer.GetFloat( parameter, out ignored ) == false )
                _errors.Add( "믹서 노출 파라미터 누락: " + parameter );
        }
        Object current = ReadReference( manager, "_soundDataMap" );
        if ( current != null && current != _map && profile.ReplaceExisting == false )
            _errors.Add( "관리자에 다른 맵이 연결돼 있습니다. 교체 허용 전에는 유지합니다." );
        if ( current != _map || _map == null )
        {
            _changes.Add( SoundSetupChange.ForMap( manager, current ) );
            _notes.Add( "[관리자 연결 예정] " + _mapPath );
        }
    }

    /// <summary>
    /// 지정한 투사체/영역 필드의 출력 종류와 서비스 및 공유 참조 검사
    /// </summary>
    /// <param name="profile">교체 정책</param>
    /// <param name="binding">사용자 지정 대상</param>
    void InspectBinding ( SoundSetupProfile profile, SoundSetupBinding binding )
    {
        if ( Enum.IsDefined( typeof( SoundSetupField ), binding.Field ) == false )
        {
            _errors.Add( "지원하지 않는 연결 필드입니다: " + binding.Field );
            return;
        }
        if ( binding.Field == SoundSetupField.FireTrapLoop )
        {
            InspectFireTrapBinding( profile, binding );
            return;
        }
        Object target = binding.Target;
        string field = GetField( binding.Field );
        if ( target is GameObject root )
        {
            Component[] areas = root.GetComponentsInChildren<MonoBehaviour>( true )
                .Where( component => component is ArrowRainArea || component is FirePillarArea ).ToArray( );
            target = areas.Length == 1 ? areas[0] : null;
        }
        bool projectile = binding.Field == SoundSetupField.ProjectileLaunch || binding.Field == SoundSetupField.ProjectileImpact;
        if ( target == null || EditorUtility.IsPersistent( target ) == false
            || ( projectile ? target is ProjectileData : target is ArrowRainArea || target is FirePillarArea ) == false )
        {
            _errors.Add( "연결 대상은 ProjectileData 또는 영역 컴포넌트 하나가 있는 프리팹 원본으로 지정하세요." );
            return;
        }
        string path = AssetDatabase.GetAssetPath( target );
        if ( ValidAssetPath( path, projectile ? ".asset" : ".prefab" ) == false )
        {
            _errors.Add( "Assets 내부 지원 대상만 연결할 수 있습니다: " + path );
            return;
        }
        if ( target is Component componentTarget && PrefabUtility.GetPrefabAssetType( componentTarget ) != PrefabAssetType.Regular )
            _errors.Add( "일반 프리팹만 지원합니다: " + path );
        if ( EditorUtility.IsDirty( target ) == true )
            _errors.Add( "대상의 미저장 변경을 먼저 저장하세요: " + path );
        using ( var serialized = new SerializedObject( target ) )
        {
            SerializedProperty property = serialized.FindProperty( field );
            if ( property == null || property.propertyType != SerializedPropertyType.Enum )
            {
                _errors.Add( "지원하는 사운드 필드가 없습니다: " + path + "/" + field );
                return;
            }
            int before = property.intValue;
            SoundSetupValue sound = _sounds.Find( item => item.Id == binding.Id );
            if ( binding.Id != SoundId.None && ( sound == null
                || ( sound.Type != SoundType.Sfx && sound.Type != SoundType.Footstep ) ) )
                _errors.Add( "위치 출력에는 등록된 Sfx/Footstep ID가 필요합니다: " + binding.Id );
            string requestKey = target.GetInstanceID( ) + "/" + field;
            SoundId requested;
            if ( _requestedIds.TryGetValue( requestKey, out requested ) == true )
            {
                if ( requested != binding.Id )
                    _errors.Add( "같은 필드에 서로 다른 ID를 요청했습니다: " + path + "/" + field );
                return;
            }
            _requestedIds.Add( requestKey, binding.Id );
            if ( binding.Id != SoundId.None )
                _notes.Add( "[실행 검증 필요] 최초 Init 전 SetSound 전달과 실제 출력을 확인하세요: " + path );
            if ( before == ( int ) binding.Id )
            {
                _notes.Add( "[연결 유지] " + path + "/" + field );
                return;
            }
            if ( before != ( int ) SoundId.None && profile.ReplaceExisting == false )
                _errors.Add( "기존 사운드 ID가 있습니다. 교체 허용이 필요합니다: " + path );
            InspectSharedTarget( path, binding );
            _changes.Add( SoundSetupChange.ForId( target, field, before, binding.Id ) );
            _notes.Add( $"[ID 변경 예정] {path}/{field}: {( SoundId ) before} → {binding.Id}" );
        }
    }


    /// <summary>
    /// 화염 장판 프리팹의 반복음 지정과 누락된 사운드 모듈 추가 검사
    /// </summary>
    /// <param name="profile">기존 값 교체 정책</param>
    /// <param name="binding">명시적으로 지정한 장판 대상</param>
    void InspectFireTrapBinding ( SoundSetupProfile profile, SoundSetupBinding binding )
    {
        FireTrap trap = binding.Target as FireTrap;
        if ( binding.Target is GameObject root )
        {
            FireTrap[] traps = root.GetComponentsInChildren<FireTrap>( true );
            trap = traps.Length == 1 ? traps[0] : null;
        }
        else if ( binding.Target is FireTrapSound module )
            trap = module.GetComponent<FireTrap>( );
        string path = trap != null ? AssetDatabase.GetAssetPath( trap ) : string.Empty;
        if ( trap == null || EditorUtility.IsPersistent( trap ) == false
            || ValidAssetPath( path, ".prefab" ) == false
            || PrefabUtility.GetPrefabAssetType( trap ) != PrefabAssetType.Regular )
        {
            _errors.Add( "화염 장판 연결은 FireTrap 하나가 있는 일반 프리팹 원본을 지정하세요." );
            return;
        }
        if ( EditorUtility.IsDirty( trap ) == true )
            _errors.Add( "대상의 미저장 변경을 먼저 저장하세요: " + path );

        string key = trap.GetInstanceID( ) + "/_loopSound";
        if ( _requestedIds.TryGetValue( key, out SoundId requested ) == true )
        {
            if ( requested != binding.Id )
                _errors.Add( "같은 화염 장판에 서로 다른 반복음을 요청했습니다: " + path );
            return;
        }
        _requestedIds.Add( key, binding.Id );
        SoundSetupValue sound = _sounds.Find( value => value.Id == binding.Id );
        if ( binding.Id != SoundId.None && ( sound == null || sound.Type != SoundType.LoopSfx ) )
            _errors.Add( "화염 장판 지속음에는 등록된 LoopSfx ID가 필요합니다: " + binding.Id );

        FireTrapSound existing = trap.GetComponent<FireTrapSound>( );
        SoundLoopPlayer loopPlayer = trap.GetComponent<SoundLoopPlayer>( );
        if ( loopPlayer != null && loopPlayer.enabled == false )
            _notes.Add( "[반복 출력 활성 상태 확인] " + path );
        if ( existing == null && binding.Id == SoundId.None )
        {
            _notes.Add( "[모듈 미사용 유지] " + path );
            return;
        }
        int before = existing != null ? ReadId( existing, "_loopSound" ) : 0;
        if ( existing != null )
        {
            SoundLoopPlayer player = existing.GetComponent<SoundLoopPlayer>( );
            if ( player == null )
                _errors.Add( "FireTrapSound와 같은 오브젝트에 SoundLoopPlayer가 필요합니다: " + path );
            else if ( player.enabled == false || existing.enabled == false || IsActiveInPrefab( trap.transform ) == false )
                _notes.Add( "[모듈 활성 상태 확인] " + path );
            if ( before == ( int ) binding.Id )
            {
                _notes.Add( "[반복음 유지] " + path );
                if ( binding.Id != SoundId.None && binding.ServiceConnected == false )
                    _notes.Add( "[서비스 확인 필요] " + path );
                return;
            }
        }
        if ( binding.Id != SoundId.None && binding.ServiceConnected == false )
            _errors.Add( "화염 장판의 SetSound/SetOperating 요청 연결 확인이 필요합니다: " + path );
        if ( existing != null && before != 0 && profile.ReplaceExisting == false )
            _errors.Add( "기존 화염 장판 지속음 교체 허용이 필요합니다: " + path );
        InspectSharedTarget( path, binding );
        SoundSetupChange change = existing != null
            ? SoundSetupChange.ForId( existing, "_loopSound", before, binding.Id )
            : SoundSetupChange.ForLoopModule( trap, binding.Id );
        _changes.Add( change );
        _notes.Add( existing != null ? $"[반복음 변경 예정] {path}: {( SoundId ) before} → {binding.Id}"
            : $"[모듈 추가 예정] {path}: FireTrapSound / SoundLoopPlayer "
                + ( change.AddLoopPlayer ? "추가" : "유지" ) + $", ID={binding.Id}" );
    }

    /// <summary>
    /// 같은 에셋의 공유 사용처를 한 번 조회하고 변경 허용 여부 검사
    /// </summary>
    /// <param name="path">변경할 에셋 경로</param>
    /// <param name="binding">공유 변경 정책</param>
    void InspectSharedTarget ( string path, SoundSetupBinding binding )
    {
        if ( _users.TryGetValue( path, out string[] users ) == false )
        {
            users = FindUsers( path );
            _users.Add( path, users );
        }
        bool ownedAutomatic = binding.IsAutomatic && users.All( SoundSetupDiscovery.IsOwnedPath );
        if ( users.Length > 1 && binding.AllowSharedChange == false && ownedAutomatic == false )
            _errors.Add( "공유 대상 변경 확인이 필요합니다: " + path );
        if ( users.Length > 0 )
            _notes.Add( "[사용처] " + path + " ← " + string.Join( ", ", users ) );
    }

    /// <summary>
    /// 직렬화된 사운드 식별자의 실제 숫자 조회
    /// </summary>
    /// <param name="target">필드 소유 객체</param>
    /// <param name="field">식별자 필드</param>
    /// <returns>현재 식별자 숫자</returns>
    static int ReadId ( Object target, string field )
    {
        using ( var serialized = new SerializedObject( target ) )
            return serialized.FindProperty( field ).intValue;
    }

    /// <summary>
    /// 검사 작업의 상태를 외부 조회용 결과로 전달
    /// </summary>
    /// <returns>외부에서 필드나 목록을 변경할 수 없는 검사 결과</returns>
    SoundSetupInspection CreateResult ()
    {
        return new SoundSetupInspection( _errors.Distinct( ).ToArray( ), _notes.Distinct( ).ToArray( ),
            _sounds, _changes, _map, _mapPath, _writeMap );
    }

    /// <summary>
    /// 필드 종류를 기존 직렬화 이름으로 변환
    /// </summary>
    /// <param name="field">선택한 필드 종류</param>
    /// <returns>직렬화 필드 이름</returns>
    static string GetField ( SoundSetupField field )
    {
        switch ( field )
        {
            case SoundSetupField.ProjectileLaunch: return "_launchSound";
            case SoundSetupField.ProjectileImpact: return "_impactSound";
            case SoundSetupField.AreaSpawn: return "_spawnSound";
            case SoundSetupField.AreaTick: return "_tickSound";
            default: throw new ArgumentOutOfRangeException( nameof( field ) );
        }
    }

    /// <summary>
    /// 데이터를 직접 참조하는 프로젝트 프리팹/설정 목록 조회
    /// </summary>
    /// <param name="path">공유 영향을 확인할 에셋 경로</param>
    /// <returns>같은 에셋을 사용하는 대상</returns>
    static string[] FindUsers ( string path )
    {
        return AssetDatabase.FindAssets( "t:Prefab t:ScriptableObject", new[] { "Assets" } )
            .Select( AssetDatabase.GUIDToAssetPath ).Distinct( )
            .Where( candidate => AssetDatabase.GetMainAssetTypeAtPath( candidate ) != typeof( SoundSetupProfile ) )
            .Where( candidate => candidate != path && AssetDatabase.GetDependencies( candidate, false ).Contains( path ) ).ToArray( );
    }

    /// <summary>
    /// 경로가 기존 Assets 폴더 안의 지원 확장자인지 검사
    /// </summary>
    /// <param name="path">프로젝트 상대 경로</param>
    /// <param name="extension">허용 확장자</param>
    /// <returns>부모 폴더가 존재하는 정상 경로인지 여부</returns>
    static bool ValidAssetPath ( string path, string extension )
    {
        return string.IsNullOrEmpty( path ) == false && path.StartsWith( "Assets/", StringComparison.Ordinal )
            && path.Contains( "\\" ) == false && path.Split( '/' ).Any( part => part == ".." || part == "." ) == false
            && path.EndsWith( extension, StringComparison.OrdinalIgnoreCase )
            && AssetDatabase.IsValidFolder( Path.GetDirectoryName( path ).Replace( '\\', '/' ) );
    }

    /// <summary>
    /// 씬에 없는 프리팹 원본도 각 상위 오브젝트의 활성 설정으로 검사
    /// </summary>
    /// <param name="target">검사할 프리팹 내부 위치</param>
    /// <returns>자신과 모든 상위 오브젝트가 활성 설정인지 여부</returns>
    static bool IsActiveInPrefab ( Transform target )
    {
        while ( target != null )
        {
            if ( target.gameObject.activeSelf == false )
                return false;
            target = target.parent;
        }
        return true;
    }

    /// <summary>
    /// 직렬화 객체 참조를 변경 없이 조회
    /// </summary>
    /// <param name="target">필드 소유 객체</param>
    /// <param name="field">필드 이름</param>
    /// <returns>기존 참조</returns>
    static Object ReadReference ( Object target, string field )
    {
        using ( var serialized = new SerializedObject( target ) )
            return serialized.FindProperty( field ).objectReferenceValue;
    }

    /// <summary>
    /// 같은 ID의 설정과 음원 후보 순서 비교
    /// </summary>
    /// <param name="left">기존 값</param>
    /// <param name="right">예상 값</param>
    /// <returns>저장값이 같은지 여부</returns>
    static bool SameValue ( SoundSetupValue left, SoundSetupValue right )
    {
        return left.Id == right.Id && left.Type == right.Type && left.Volume == right.Volume && left.Clips.SequenceEqual( right.Clips );
    }

    /// <summary>
    /// 종류/음량과 후보 이름을 검토용 문자열로 요약
    /// </summary>
    /// <param name="value">요약할 등록값</param>
    /// <returns>검토용 문자열</returns>
    static string Describe ( SoundSetupValue value )
    {
        return $"{value.Type}, {value.Volume:0.###}, " + string.Join( ", ", value.Clips.Select( clip => clip != null ? clip.name : "없음" ) );
    }

    /// <summary>
    /// 기존 맵을 편집용 값으로 복사
    /// </summary>
    /// <param name="map">읽을 데이터 맵</param>
    /// <returns>기존 항목 전체</returns>
    static List<SoundSetupValue> ReadMap ( SoundDataMap map )
    {
        var values = new List<SoundSetupValue>( );
        using ( var serialized = new SerializedObject( map ) )
        {
            SerializedProperty sounds = serialized.FindProperty( "_sounds" );
            for ( int i = 0; i < sounds.arraySize; i++ )
            {
                SerializedProperty row = sounds.GetArrayElementAtIndex( i );
                SerializedProperty clips = row.FindPropertyRelative( "_clips" );
                var candidates = new List<AudioClip>( );
                for ( int j = 0; j < clips.arraySize; j++ )
                    candidates.Add( clips.GetArrayElementAtIndex( j ).objectReferenceValue as AudioClip );
                values.Add( new SoundSetupValue(
                    ( SoundId ) row.FindPropertyRelative( "_id" ).intValue,
                    ( SoundType ) row.FindPropertyRelative( "_type" ).intValue,
                    row.FindPropertyRelative( "_volume" ).floatValue, candidates ) );
            }
        }
        return values;
    }

    /// <summary>
    /// 직전 검사 후 맵을 먼저 저장하고 지정 참조를 연결하며 실패 시 이번 변경 복구
    /// </summary>
    /// <param name="profile">적용할 편집 프로필</param>
    /// <returns>실제 저장/복구 결과</returns>
    public static string Apply ( SoundSetupProfile profile )
    {
        SoundSetupInspection plan = Inspect( profile );
        if ( plan.Errors.Count > 0 )
            return "적용하지 않았습니다.\n" + string.Join( "\n", plan.Errors );

        SoundDataMap map = plan.Map;
        SoundDataMap previousProfileMap = profile.DataMap;
        List<SoundSetupValue> previous = map != null ? ReadMap( map ) : null;
        var attempted = new List<IGrouping<string, SoundSetupChange>>( );
        bool created = false;
        try
        {
            if ( map == null )
            {
                map = ScriptableObject.CreateInstance<SoundDataMap>( );
                WriteMap( map, plan.Sounds );
                AssetDatabase.CreateAsset( map, plan.MapPath );
                created = AssetDatabase.GetAssetPath( map ) == plan.MapPath;
                if ( created == false )
                    throw new IOException( "맵 에셋을 생성하지 못했습니다: " + plan.MapPath );
            }
            else if ( plan.WriteMap == true )
                WriteMap( map, plan.Sounds );
            if ( plan.WriteMap == true )
                SaveAssetChecked( map );
            if ( map.TryBuild( out _ ) == false )
                throw new InvalidOperationException( "생성한 맵이 기존 런타임 데이터 검사에 실패했습니다." );
            if ( SameMaps( map, plan.Sounds ) == false )
                throw new InvalidOperationException( "맵 저장 후 설정이 검사 결과와 다릅니다." );

            foreach ( var group in plan.Changes.GroupBy( change => AssetDatabase.GetAssetPath( change.Target ) ) )
            {
                //저장 중 오류도 복구 대상으로 기록하고 관련 없는 에셋은 저장하지 않음
                attempted.Add( group );
                SaveChanges( group, map, false );
            }
            profile.SetDataMap( map );
            EditorUtility.SetDirty( profile );
            SaveAssetChecked( profile );
            return $"맵 {plan.Sounds.Count}개 ID 저장/확인, 필드 {plan.Changes.Count}개 연결 완료.\n"
                + "서비스 전달/실제 호출/모션 이벤트/게임 출력 검증 완료를 뜻하지 않습니다.";
        }
        catch ( Exception exception )
        {
            //여러 Unity 에셋 저장의 경계에서만 복구하며 원인과 복구 실패를 함께 보고
            var failures = new List<string>( );
            //CreateAsset 도중 예외가 나도 실제로 생성한 자신의 맵만 복구 대상으로 인정
            if ( plan.Map == null && map != null && AssetDatabase.GetAssetPath( map ) == plan.MapPath )
                created = true;
            foreach ( var group in attempted.AsEnumerable( ).Reverse( ) )
            {
                try
                {
                    SaveChanges( group, map, true );
                }
                catch ( Exception recovery )
                {
                    failures.Add( group.Key + ": " + recovery.Message );
                }
            }
            try
            {
                profile.SetDataMap( previousProfileMap );
                EditorUtility.SetDirty( profile );
                SaveAssetChecked( profile );
            }
            catch ( Exception recovery )
            {
                failures.Add( "프로필 복구: " + recovery.Message );
            }
            try
            {
                if ( created == true && failures.Count == 0 )
                {
                    if ( AssetDatabase.DeleteAsset( plan.MapPath ) == false )
                        failures.Add( "이번에 생성한 맵을 제거하지 못했습니다: " + plan.MapPath );
                }
                else if ( previous != null && plan.WriteMap == true )
                {
                    WriteMap( map, previous );
                    SaveAssetChecked( map );
                }
            }
            catch ( Exception recovery )
            {
                failures.Add( "맵 복구: " + recovery.Message );
            }
            if ( map != null && EditorUtility.IsPersistent( map ) == false )
                Object.DestroyImmediate( map );
            Debug.LogException( exception );
            return "적용 실패: " + exception.Message + "\n"
                + ( failures.Count == 0 ? "이번에 적용한 맵/필드 변경을 복구했습니다."
                    : "부분 저장/복구 확인 필요:\n" + string.Join( "\n", failures ) );
        }
    }

    /// <summary>
    /// 대상 에셋 하나의 지정 필드만 저장하거나 이전 값으로 복구
    /// </summary>
    /// <param name="changes">같은 에셋 경로의 필드 변경</param>
    /// <param name="map">새로 연결할 맵</param>
    /// <param name="restore">이전 값 복구 여부</param>
    static void SaveChanges ( IGrouping<string, SoundSetupChange> changes, SoundDataMap map, bool restore )
    {
        bool prefab = changes.Key.EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase );
        GameObject contents = prefab ? PrefabUtility.LoadPrefabContents( changes.Key ) : null;
        try
        {
            foreach ( SoundSetupChange change in changes )
            {
                Object original = GlobalObjectId.GlobalObjectIdentifierToObjectSlow( change.ObjectId );
                if ( original == null )
                    throw new InvalidOperationException( "원본 대상을 다시 찾을 수 없습니다: " + changes.Key );
                Object target = prefab ? ResolveComponent( ( Component ) original, contents ) : original;
                if ( change.AddLoopModule == true )
                {
                    target = ApplyLoopModule( ( FireTrap ) target, change, restore );
                    if ( restore == true )
                        continue;
                }
                using ( var serialized = new SerializedObject( target ) )
                {
                    SerializedProperty property = serialized.FindProperty( change.Field );
                    if ( change.IsMapReference == true )
                        property.objectReferenceValue = restore ? change.BeforeReference : map;
                    else
                        property.intValue = restore ? change.BeforeId : ( int ) change.AfterId;
                    serialized.ApplyModifiedPropertiesWithoutUndo( );
                }
                if ( prefab == false )
                {
                    EditorUtility.SetDirty( target );
                    SaveAssetChecked( target );
                }
            }
            if ( prefab == true )
            {
                bool success;
                PrefabUtility.SaveAsPrefabAsset( contents, changes.Key, out success );
                if ( success == false )
                    throw new IOException( "프리팹 저장 실패: " + changes.Key );
            }

            //원본 에셋도 다시 조회하여 저장된 필드 값이 예상과 일치하는지 확인
            foreach ( SoundSetupChange change in changes )
            {
                Object original = GlobalObjectId.GlobalObjectIdentifierToObjectSlow( change.ObjectId );
                if ( change.AddLoopModule == true )
                {
                    FireTrap trap = ( FireTrap ) original;
                    FireTrapSound module = trap.GetComponent<FireTrapSound>( );
                    bool hasPlayer = trap.GetComponent<SoundLoopPlayer>( ) != null;
                    bool matches = restore
                        ? module == null && hasPlayer == ( change.AddLoopPlayer == false )
                        : module != null && hasPlayer && ReadId( module, change.Field ) == ( int ) change.AfterId;
                    if ( matches == false )
                        throw new IOException( "모듈 저장 확인 실패: " + changes.Key );
                    continue;
                }
                using ( var serialized = new SerializedObject( original ) )
                {
                    SerializedProperty property = serialized.FindProperty( change.Field );
                    bool matches = change.IsMapReference
                        ? property.objectReferenceValue == ( restore ? change.BeforeReference : map )
                        : property.intValue == ( restore ? change.BeforeId : ( int ) change.AfterId );
                    if ( matches == false )
                        throw new IOException( "필드 저장 확인 실패: " + changes.Key + "/" + change.Field );
                }
            }
        }
        finally
        {
            if ( contents != null )
                PrefabUtility.UnloadPrefabContents( contents );
        }
    }


    /// <summary>
    /// 이번 계획의 사운드 모듈만 추가하거나 실패 시 추가한 범위만 제거
    /// </summary>
    /// <param name="trap">격리한 프리팹의 원래 장판</param>
    /// <param name="change">추가와 복구 범위</param>
    /// <param name="restore">이번에 추가한 모듈 제거 여부</param>
    /// <returns>새로 추가한 사운드 모듈이며 복구 시 null</returns>
    static FireTrapSound ApplyLoopModule ( FireTrap trap, SoundSetupChange change, bool restore )
    {
        FireTrapSound module = trap.GetComponent<FireTrapSound>( );
        if ( restore == true )
        {
            if ( module != null )
                Object.DestroyImmediate( module );
            SoundLoopPlayer player = trap.GetComponent<SoundLoopPlayer>( );
            if ( change.AddLoopPlayer == true && player != null )
                Object.DestroyImmediate( player );
            return null;
        }
        if ( module != null )
            throw new InvalidOperationException( "검사 후 화염 장판에 사운드 모듈이 추가됐습니다." );
        return trap.gameObject.AddComponent<FireTrapSound>( );
    }

    /// <summary>
    /// 동명 오브젝트도 구분하도록 자식/컴포넌트 순서로 편집 프리팹 대상 조회
    /// </summary>
    /// <param name="original">원본 프리팹의 컴포넌트</param>
    /// <param name="contents">격리해 연 프리팹 루트</param>
    /// <returns>같은 위치와 타입의 편집 컴포넌트</returns>
    static Component ResolveComponent ( Component original, GameObject contents )
    {
        var indices = new Stack<int>( );
        Transform source = original.transform;
        while ( source.parent != null )
        {
            indices.Push( source.GetSiblingIndex( ) );
            source = source.parent;
        }
        Transform target = contents.transform;
        while ( indices.Count > 0 )
            target = target.GetChild( indices.Pop( ) );
        int index = Array.IndexOf( original.GetComponents<Component>( ), original );
        Component[] components = target.GetComponents<Component>( );
        if ( index < 0 || index >= components.Length || components[index] == null
            || components[index].GetType( ) != original.GetType( ) )
            throw new InvalidOperationException( "프리팹 구조가 검사 이후 변경됐습니다." );
        return components[index];
    }

    /// <summary>
    /// 저장한 맵의 설정을 다시 읽어 검사 결과와 대조
    /// </summary>
    /// <param name="map">저장한 맵</param>
    /// <param name="expected">검사한 병합 결과</param>
    /// <returns>모든 값이 일치하는지 여부</returns>
    static bool SameMaps ( SoundDataMap map, IReadOnlyList<SoundSetupValue> expected )
    {
        List<SoundSetupValue> actual = ReadMap( map );
        return actual.Count == expected.Count
            && actual.Where( ( value, index ) => SameValue( value, expected[index] ) == false ).Any( ) == false;
    }

    /// <summary>
    /// 대상 에셋만 저장하고 미저장 상태가 남으면 실패로 보고
    /// </summary>
    /// <param name="target">이번 작업에서 변경한 에셋</param>
    static void SaveAssetChecked ( Object target )
    {
        AssetDatabase.SaveAssetIfDirty( target );
        string path = AssetDatabase.GetAssetPath( target );
        if ( EditorUtility.IsDirty( target ) == true || File.Exists( path ) == false )
            throw new IOException( "에셋 저장 확인 실패: " + path );
    }

    /// <summary>
    /// 런타임 API 변경 없이 맵의 직렬화 배열 기록
    /// </summary>
    /// <param name="map">기록할 맵</param>
    /// <param name="values">검증한 설정값</param>
    static void WriteMap ( SoundDataMap map, IReadOnlyList<SoundSetupValue> values )
    {
        using ( var serialized = new SerializedObject( map ) )
        {
            SerializedProperty sounds = serialized.FindProperty( "_sounds" );
            sounds.arraySize = values.Count;
            for ( int i = 0; i < values.Count; i++ )
            {
                SoundSetupValue value = values[i];
                SerializedProperty row = sounds.GetArrayElementAtIndex( i );
                row.FindPropertyRelative( "_id" ).intValue = ( int ) value.Id;
                row.FindPropertyRelative( "_type" ).intValue = ( int ) value.Type;
                row.FindPropertyRelative( "_volume" ).floatValue = value.Volume;
                SerializedProperty clips = row.FindPropertyRelative( "_clips" );
                clips.arraySize = value.Clips.Count;
                for ( int j = 0; j < value.Clips.Count; j++ )
                    clips.GetArrayElementAtIndex( j ).objectReferenceValue = value.Clips[j];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo( );
        }
        EditorUtility.SetDirty( map );
    }
}
#endif
