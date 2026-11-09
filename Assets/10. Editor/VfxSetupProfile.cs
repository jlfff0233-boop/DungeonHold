#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 공용 효과의 출력 종류
/// </summary>
public enum VfxOutput
{
    World = 0,
    Ui = 1
}

/// <summary>
/// 오브젝트에 추가할 표현 모듈
/// </summary>
public enum VfxTargetModule
{
    Character = 0,
    WorldPlayer = 1,
    UiPlayer = 2
}

/// <summary>
/// 탐색과 적용에 사용할 담당 범위
/// </summary>
public enum VfxSetupScope
{
    All, Lj, Kmb, Lhj, Manual
}

/// <summary>
/// 사용처의 기능 분류
/// </summary>
public enum VfxSetupCategory
{
    Auto, Character, Weapon, Item, Enemy, Installation, Presentation, Manual
}

/// <summary>
/// 공용 VFX의 원본 선택과 사용처 배정 보관
/// </summary>
[CreateAssetMenu( fileName = "VfxSetupProfile", menuName = "VFX/Setup Profile" )]
public class VfxSetupProfile : ScriptableObject
{
    [SerializeField] DefaultAsset _sourceFolder;		//선별한 원본 프리팹 폴더
    [SerializeField] string _outputFolder = "Assets/12. Vfx";		//신규 효과 생성 폴더
    [SerializeField] List<VfxSetupEffect> _effects = new( );		//공용 효과 배정표
    [SerializeField] List<VfxSetupBinding> _bindings = new( );		//명시적인 사용처
    [SerializeField] VfxSetupScope _scope = VfxSetupScope.Lj;		//화면 필터와 별개인 실제 작업 범위

    public DefaultAsset SourceFolder => _sourceFolder;
    public string OutputFolder => _outputFolder;
    public IReadOnlyList<VfxSetupEffect> Effects => _effects;
    public IReadOnlyList<VfxSetupBinding> Bindings => _bindings;
    public VfxSetupScope Scope => _scope;

    /// <summary>
    /// 기존 배정을 보존하고 신규 사용처만 추가
    /// </summary>
    public void AddBinding ( Object target, string field, string key, bool looping )
    {
        if ( _bindings.Exists( item => SameTarget( item.Target, target ) && item.Field == field
            && ( string.IsNullOrEmpty( item.SuggestionKey ) || item.SuggestionKey == key ) ) )
            return;

        EnsureEffect( key, looping );
        _bindings.Add( new VfxSetupBinding( target, field, key, looping, true ) );
    }

    /// <summary>
    /// 수동 연결로 전환한 효과의 자동 배정 제거
    /// </summary>
    /// <param name="effectKey">전환할 효과 키</param>
    /// <returns>제거한 배정 수</returns>
    internal int RemoveAutomaticBindings ( string effectKey )
    {
        return _bindings.RemoveAll( item => item.EffectKey == effectKey
            && string.IsNullOrEmpty( item.SuggestionKey ) == false
            && VfxSetupUtility.Includes( _scope, item ) );
    }

    /// <summary>
    /// 같은 사용처의 수동 배정을 우선 조회
    /// </summary>
    /// <param name="usage">탐색한 사용처</param>
    internal VfxSetupBinding FindBinding ( VfxSetupBinding usage )
    {
        return _bindings.Find( item => SameTarget( item.Target, usage.Target ) && item.Field == usage.Field
                && string.IsNullOrEmpty( item.SuggestionKey ) )
            ?? _bindings.Find( item => SameTarget( item.Target, usage.Target ) && item.Field == usage.Field
                && item.SuggestionKey == usage.SuggestionKey );
    }

    /// <summary>
    /// 오브젝트와 그 캐릭터 표현 컴포넌트의 수동 배정을 같은 대상으로 취급
    /// </summary>
    static bool SameTarget ( Object left, Object right )
    {
        if ( left is CharacterVfx leftCharacter )
            left = leftCharacter.gameObject;
        if ( right is CharacterVfx rightCharacter )
            right = rightCharacter.gameObject;
        return left == right;
    }

    /// <summary>
    /// 같은 키의 원본 선택은 유지
    /// </summary>
    public void EnsureEffect ( string key, bool looping )
    {
        if ( _effects.Exists( item => item.Key == key ) == false )
            _effects.Add( new VfxSetupEffect( key, looping ) );
    }

    /// <summary>
    /// 선택 폴더에서 가져온 원본을 예비 항목으로 추가
    /// </summary>
    public void AddSource ( GameObject source )
    {
        VfxEffect prefab = source.GetComponent<VfxEffect>( );
        UiVfxEffect uiPrefab = source.GetComponent<UiVfxEffect>( );
        if ( _effects.Exists( item => item.Source == source || ( prefab != null && item.Prefab == prefab )
            || ( uiPrefab != null && item.UiPrefab == uiPrefab ) ) )
            return;

        string key = "source_" + UnityEditor.AssetDatabase.AssetPathToGUID(
            UnityEditor.AssetDatabase.GetAssetPath( source ) );
        bool ui = uiPrefab != null || ( source.transform is RectTransform
            && source.GetComponentInChildren<UnityEngine.UI.Graphic>( true ) != null );
        bool looping = uiPrefab != null ? uiPrefab.Looping
            : VfxEffect.IsMaintained( source );
        var entry = new VfxSetupEffect( key, looping, prefab == null && uiPrefab == null ? source : null );
        entry.SetOutputKind( ui ? VfxOutput.Ui : VfxOutput.World );
        if ( uiPrefab != null )
            entry.SetResult( uiPrefab, string.Empty );
        else if ( prefab != null )
            entry.SetPrefab( prefab );
        _effects.Add( entry );
    }

    /// <summary>
    /// 담당자가 직접 지정할 빈 사용처 추가
    /// </summary>
    public void AddManualBinding ( VfxTargetModule module = VfxTargetModule.Character )
    {
        string key = "custom_" + Guid.NewGuid( ).ToString( "N" );
        var entry = new VfxSetupEffect( key, false );
        entry.SetOutputKind( module == VfxTargetModule.UiPlayer ? VfxOutput.Ui : VfxOutput.World );
        _effects.Add( entry );
        _bindings.Add( new VfxSetupBinding( null, module == VfxTargetModule.Character ? "_hit" : "_effect",
            key, false, false, module ) );
    }
}

/// <summary>
/// 선택 원본과 실제 연결할 공용 효과
/// </summary>
[Serializable]
public class VfxSetupEffect
{
    [SerializeField] string _key;		//역할별 고정 배정 키
    [SerializeField] bool _create;		//원본으로 신규 효과 생성
    [SerializeField] GameObject _source;		//수용한 원본 프리팹
    [SerializeField] VfxEffect _prefab;		//기존 또는 생성한 공용 효과
    [SerializeField] bool _looping;		//단발 또는 유지 방식
    [SerializeField] VfxOutput _output;		//기존 프로필은 월드 출력 유지
    [SerializeField] UiVfxEffect _uiPrefab;		//UGUI 공용 효과
    [SerializeField, Min( 0.01f )] float _duration = 1f;		//UI 단발 완료 시간
    [SerializeField, HideInInspector] string _builtStamp;		//생성 시점의 원본과 설정
    [SerializeField] string _note;		//배정 참고 사항
    [SerializeField, HideInInspector] string _importedNote;		//최근 JSON 메모와 수동 편집 구분
    [SerializeField, HideInInspector] bool _hasImportedNote;		//빈 JSON 메모와 미수신 구분

    public string Key => _key;
    public bool Create => _create;
    public GameObject Source => _source;
    public VfxEffect Prefab => _prefab;
    public bool Looping => _looping;
    public VfxOutput Output => _output;
    public UiVfxEffect UiPrefab => _uiPrefab;
    public float Duration => _duration;
    public string BuiltStamp => _builtStamp;
    public string Note => _note;
    public string ImportedNote => _importedNote;
    public bool HasImportedNote => _hasImportedNote;
    public Object Result => _output == VfxOutput.Ui ? _uiPrefab : _prefab;

    /// <summary>
    /// 역할과 재생 방식을 보관하고 생성 초안 준비
    /// </summary>
    public VfxSetupEffect ( string key, bool looping, GameObject source = null )
    {
        _key = key;
        _looping = looping;
        _source = source;
        _create = true;
    }

    /// <summary>
    /// 빈 항목의 원본과 재생 방식 설정
    /// </summary>
    /// <param name="source">원본 프리팹</param>
    /// <param name="looping">반복 여부</param>
    internal void SetSource ( GameObject source, bool looping )
    {
        _source = source;
        _looping = looping;
        _output = VfxOutput.World;
        _create = true;
        _builtStamp = string.Empty;
    }

    /// <summary>
    /// 효과의 유지 방식 설정
    /// </summary>
    /// <param name="looping">유지 효과 여부</param>
    internal void SetLooping ( bool looping )
    {
        _looping = looping;
    }

    /// <summary>
    /// 배정 참고 사항 설정
    /// </summary>
    /// <param name="note">참고 내용</param>
    internal void SetNote ( string note )
    {
        _note = note;
    }

    /// <summary>
    /// 수동 메모를 보존하고 JSON 메모 갱신
    /// </summary>
    /// <param name="note">새 JSON 메모</param>
    internal void ImportNote ( string note )
    {
        if ( string.IsNullOrEmpty( _note ) || ( _hasImportedNote == true && _note == _importedNote ) )
            _note = note;
        _importedNote = note;
        _hasImportedNote = true;
    }

    /// <summary>
    /// 생성 결과를 보관하여 재실행 시 중복 생성 방지
    /// </summary>
    public void SetPrefab ( VfxEffect prefab )
    {
        _prefab = prefab;
    }

    /// <summary>
    /// 신규 역할의 출력 종류 지정
    /// </summary>
    public void SetOutputKind ( VfxOutput output )
    {
        _output = output;
    }

    /// <summary>
    /// 생성 결과와 원본 상태를 함께 보관
    /// </summary>
    public void SetResult ( Object result, string stamp )
    {
        if ( _output == VfxOutput.Ui )
            _uiPrefab = ( UiVfxEffect ) result;
        else
            _prefab = ( VfxEffect ) result;
        _builtStamp = stamp;
    }
}

/// <summary>
/// 프리팹 컴포넌트 또는 데이터 필드의 효과 배정
/// </summary>
[Serializable]
public class VfxSetupBinding
{
    [SerializeField] bool _apply = true;		//이번 적용에 포함
    [SerializeField] Object _target;		//컴포넌트, 데이터 또는 캐릭터 표현 추가 대상
    [SerializeField] string _field;		//선택한 VfxEffect 참조 필드
    [SerializeField] string _effectKey;		//사용할 효과 배정 키
    [SerializeField] bool _looping;		//이 사용처가 요구하는 재생 방식
    [SerializeField] bool _replaceExisting;		//기존 참조 교체 허용
    [SerializeField] bool _localOverride;		//상속 필드도 이 프리팹에만 적용
    [SerializeField, HideInInspector] string _suggestionKey;		//재추천 시 기존 선택을 찾는 원래 역할
    [SerializeField] VfxTargetModule _module;		//기존 오브젝트 배정은 캐릭터 표현 유지
    [SerializeField] VfxSetupCategory _category;		//자동 분류 또는 직접 지정한 기능

    public bool Apply => _apply;
    public Object Target => _target;
    public string Field => _field;
    public string EffectKey => _effectKey;
    public bool Looping => _looping;
    public bool ReplaceExisting => _replaceExisting;
    public bool LocalOverride => _localOverride;
    public string SuggestionKey => _suggestionKey;
    public VfxTargetModule Module => _module;
    public VfxSetupCategory Category => _category;

    /// <summary>
    /// 효과 배정 후 적용 대상으로 포함
    /// </summary>
    /// <param name="effectKey">효과 키</param>
    internal void Assign ( string effectKey )
    {
        _effectKey = effectKey;
        _apply = true;
    }

    /// <summary>
    /// 이번 적용에 포함할지 설정
    /// </summary>
    /// <param name="include">포함 여부</param>
    internal void SetIncluded ( bool include )
    {
        _apply = include;
    }

    /// <summary>
    /// 역할과 실제 적용 선택을 분리하여 배정 생성
    /// </summary>
    public VfxSetupBinding ( Object target, string field, string key, bool looping, bool automatic = false,
        VfxTargetModule module = VfxTargetModule.Character )
    {
        _target = target;
        _field = field;
        _effectKey = key;
        _looping = looping;
        _suggestionKey = automatic ? key : string.Empty;
        _module = module;
    }
}
#endif
