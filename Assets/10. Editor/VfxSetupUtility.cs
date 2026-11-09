#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// VFX 설정 검사 결과와 파일별 변경 계획
/// </summary>
public class VfxSetupInspection
{
    public List<string> Errors = new( );		//적용에서 제외한 이유
    public List<string> Notes = new( );		//공유 사용처와 보존 항목
    public List<VfxSetupChange> Changes = new( );		//중복을 합친 실제 변경
    public List<VfxGeneration> Generations = new( );		//검사한 신규 출력
    public string Signature;		//프로필과 선택 원본의 검사 상태
}

/// <summary>
/// 검사 시점의 참조 변경 한 건
/// </summary>
public class VfxSetupChange
{
    public Object Target;		//상속을 해석한 실제 대상
    public string Field;		//변경할 직렬화 필드
    public Object Before;		//검사 시점의 참조
    public Object After;		//기존 또는 생성 후 대입할 참조
    public string EffectKey;		//생성 결과를 찾는 배정 키
    public VfxTargetModule Module;		//오브젝트에 추가할 모듈
    public string OutputPath;		//기존 출력 또는 신규 생성 경로
    public string Path;		//검사 시점의 파일 경로
    public string Key;		//검사 시점의 대상과 필드 식별
    public string Description;		//에셋이 바뀌어도 유지할 표시 내용
}

/// <summary>
/// 공용 VFX 사용처 검사와 담당 범위별 탐색 및 적용
/// </summary>
public static class VfxSetupUtility
{
    static string[] _characterFields = { "_hit", "_death", "_revive", "_burn", "_slow", "_stun" };		//추가 가능한 캐릭터 필드

    /// <summary>
    /// 저장된 프로필의 편집 가능 여부
    /// </summary>
    public static bool CanEdit ( VfxSetupProfile profile )
    {
        return EditorApplication.isPlayingOrWillChangePlaymode == false
            && EditorApplication.isCompiling == false && profile != null
            && AssetDatabase.GetAssetPath( profile ).StartsWith( "Assets/", StringComparison.Ordinal );
    }

    /// <summary>
    /// 직접 지정할 수 있는 월드/UI 효과 참조 필드 조회
    /// </summary>
    public static string[] Fields ( Object target, VfxTargetModule module = VfxTargetModule.Character )
    {
        if ( target is GameObject )
            return module == VfxTargetModule.Character ? _characterFields : new[] { "_effect" };
        if ( target is not Component && target is not ScriptableObject )
            return Array.Empty<string>( );

        var names = new List<string>( );
        using ( var serialized = new SerializedObject( target ) )
        {
            SerializedProperty property = serialized.GetIterator( );
            while ( property.NextVisible( true ) )
            {
                if ( property.depth != 0 || property.propertyType != SerializedPropertyType.ObjectReference )
                    continue;
                for ( Type type = target.GetType( ); type != null; type = type.BaseType )
                {
                    FieldInfo field = type.GetField( property.name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
                    if ( field == null )
                        continue;
                    if ( field.FieldType == typeof( VfxEffect ) || field.FieldType == typeof( UiVfxEffect ) )
                        names.Add( property.propertyPath );
                    break;
                }
            }
        }
        return names.ToArray( );
    }

    /// <summary>
    /// 기존 모듈에서 정해진 필드의 재생 방식 조회
    /// </summary>
    public static bool? RequiredLoop ( Object target, string field, VfxTargetModule module = VfxTargetModule.Character )
    {
        if ( target is GameObject && module != VfxTargetModule.Character )
            return null;
        if ( target is GameObject || target is CharacterVfx )
            return field == "_burn" || field == "_slow" || field == "_stun";
        if ( target is ProjectileData )
            return field == "_flightVfx";
        if ( target is AreaAttackExecutor || target is SwordSkillExecutor || target is ItemData
            || target is ArrowRainArea || target is FirePillarArea )
            return false;
        return null;
    }

    /// <summary>
    /// 공용 배정에서 지원하는 두 출력의 필드 종류 확인
    /// </summary>
    public static VfxOutput OutputFor ( Object target, string field, VfxTargetModule module )
    {
        if ( target is GameObject )
            return module == VfxTargetModule.UiPlayer ? VfxOutput.Ui : VfxOutput.World;
        for ( Type type = target.GetType( ); type != null; type = type.BaseType )
        {
            FieldInfo info = type.GetField( field,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly );
            if ( info != null )
                return info.FieldType == typeof( UiVfxEffect ) ? VfxOutput.Ui : VfxOutput.World;
        }
        return VfxOutput.World;
    }

    /// <summary>
    /// 기존 사용처와 직접 지정의 기능 분류
    /// </summary>
    public static VfxSetupCategory Category ( Object target, string key, VfxTargetModule module,
        VfxSetupCategory category = VfxSetupCategory.Auto )
    {
        if ( category != VfxSetupCategory.Auto )
            return category;
        string path = AssetDatabase.GetAssetPath( target );
        if ( target is EnemyCombat || key.StartsWith( "enemy.", StringComparison.Ordinal ) || key.StartsWith( "boss.", StringComparison.Ordinal )
            || path.StartsWith( "Assets/04. Prefabs/KMB/Enemy/", StringComparison.Ordinal ) )
            return VfxSetupCategory.Enemy;
        if ( key.StartsWith( "installation.", StringComparison.Ordinal )
            || path.StartsWith( "Assets/04. Prefabs/KMB/Installation/", StringComparison.Ordinal )
            || target is TrapData || target is PlacedInstallation )
            return VfxSetupCategory.Installation;
        if ( module != VfxTargetModule.Character || target is VfxPlayer || target is UiVfxPlayer )
            return VfxSetupCategory.Presentation;
        if ( key.StartsWith( "item.", StringComparison.Ordinal ) || target is ItemData )
            return VfxSetupCategory.Item;
        if ( key.StartsWith( "sword.", StringComparison.Ordinal ) || key.StartsWith( "bow.", StringComparison.Ordinal )
            || key.StartsWith( "staff.", StringComparison.Ordinal ) || key.Contains( ".attack" )
            || target is ProjectileData || target is AttackExecutor || target is ArrowRainArea || target is FirePillarArea )
            return VfxSetupCategory.Weapon;
        if ( key.StartsWith( "hero.", StringComparison.Ordinal ) || key.StartsWith( "companion.", StringComparison.Ordinal )
            || target is Hero || target is Companion || target is CharacterVfx )
            return VfxSetupCategory.Character;
        return VfxSetupCategory.Manual;
    }

    /// <summary>
    /// 배정에 저장된 분류 또는 실제 대상의 분류 조회
    /// </summary>
    public static VfxSetupCategory Category ( VfxSetupBinding binding )
    {
        return Category( binding.Target, binding.SuggestionKey ?? string.Empty, binding.Module, binding.Category );
    }

    /// <summary>
    /// 기능 분류에 해당하는 작업 담당
    /// </summary>
    public static VfxSetupScope Owner ( VfxSetupCategory category )
    {
        switch ( category )
        {
            case VfxSetupCategory.Character:
            case VfxSetupCategory.Weapon:
            case VfxSetupCategory.Item: return VfxSetupScope.Lj;
            case VfxSetupCategory.Enemy:
            case VfxSetupCategory.Installation: return VfxSetupScope.Kmb;
            case VfxSetupCategory.Presentation: return VfxSetupScope.Lhj;
            default: return VfxSetupScope.Manual;
        }
    }

    /// <summary>
    /// 담당 범위에 포함된 배정인지 확인
    /// </summary>
    public static bool Includes ( VfxSetupScope scope, VfxSetupBinding binding )
    {
        return scope == VfxSetupScope.All || Owner( Category( binding ) ) == scope;
    }

    /// <summary>
    /// 선택한 배정을 읽고 공유 필드와 교체 여부를 검사
    /// </summary>
    public static VfxSetupInspection Inspect ( VfxSetupProfile profile )
    {
        var result = new VfxSetupInspection( );
        if ( CanEdit( profile ) == false )
        {
            result.Errors.Add( "저장된 프로필을 편집 모드에서 지정하세요." );
            return result;
        }
        var effects = new Dictionary<string, VfxSetupEffect>( );
        var rejectedEffects = new HashSet<string>( );
        VfxSetupBinding[] selected = profile.Bindings.Where( item => item.Apply && Includes( profile.Scope, item ) ).ToArray( );
        var selectedKeys = new HashSet<string>( selected.Select( item => item.EffectKey ) );
        result.Notes.Add( "실제 적용 범위: " + profile.Scope + " / 참조 배정과 런타임 호출 연결은 별도입니다." );
        foreach ( VfxSetupEffect entry in profile.Effects.Where( item => selectedKeys.Contains( item.Key ) ) )
        {
            if ( string.IsNullOrWhiteSpace( entry.Key ) || effects.TryAdd( entry.Key, entry ) == false )
            {
                result.Errors.Add( "비어 있거나 중복된 효과 키: " + entry.Key );
                rejectedEffects.Add( entry.Key );
            }
        }
        var checkedEffects = new HashSet<string>( );
        var generations = new Dictionary<string, VfxGeneration>( );
        var outputs = new Dictionary<string, Object>( );
        string signature = EditorJsonUtility.ToJson( profile );
        var grouped = new Dictionary<string, List<(VfxSetupBinding binding, VfxSetupChange change)>>( );
        var rejectedTargets = new HashSet<string>( );
        var rejectedPaths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
        var appliedOutputs = new HashSet<string>( );		//연결 또는 내용 갱신을 허용한 출력
        foreach ( VfxSetupBinding binding in selected )
        {
            if ( binding.Target == null || Fields( binding.Target, binding.Module ).Contains( binding.Field ) == false )
            {
                result.Errors.Add( "대상과 VfxEffect 필드를 지정하세요: " + binding.EffectKey );
                continue;
            }
            Object target = ResolveTarget( binding );
            string targetKey = GlobalObjectId.GetGlobalObjectIdSlow( target ) + "/"
                + ( target is GameObject ? binding.Module.ToString( ) : "field" ) + "/" + binding.Field;
            if ( effects.TryGetValue( binding.EffectKey ?? string.Empty, out VfxSetupEffect entry ) == false )
            {
                result.Errors.Add( "효과 키를 지정하세요: " + binding.EffectKey );
                rejectedTargets.Add( targetKey );
                continue;
            }
            if ( rejectedEffects.Contains( entry.Key ) )
            {
                rejectedTargets.Add( targetKey );
                continue;
            }
            if ( checkedEffects.Add( entry.Key ) )
            {
                var entryErrors = new List<string>( );
                VfxGeneration plan = VfxPrefabBuilder.Plan( entry, profile.OutputFolder, entryErrors, profile.Effects );
                Object output = entry.Result;
                VfxSetupEffect cached = plan != null ? profile.Effects.FirstOrDefault(
                    item => item.Result != null && item.BuiltStamp == plan.Stamp
                        && AssetDatabase.GetAssetPath( item.Result ) == plan.Path ) : null;
                if ( cached != null )
                {
                    //현재 생성 경로에서 같은 원본과 설정의 결과만 재사용
                    output = cached.Result;
                    var check = new VfxSetupEffect( entry.Key, entry.Looping );
                    check.SetOutputKind( entry.Output );
                    check.SetResult( output, cached.BuiltStamp );
                    entryErrors.Clear( );
                    VfxPrefabBuilder.InspectEffect( check, entryErrors );
                    plan = null;
                }
                else if ( plan == null )
                    VfxPrefabBuilder.InspectEffect( entry, entryErrors );

                if ( entryErrors.Count > 0 )
                {
                    result.Errors.AddRange( entryErrors.Select( error => entry.Key + ": " + error ) );
                    rejectedEffects.Add( entry.Key );
                    rejectedTargets.Add( targetKey );
                    if ( plan != null )
                        rejectedPaths.Add( plan.Path );
                    continue;
                }
                if ( plan != null )
                {
                    VfxGeneration shared = result.Generations.FirstOrDefault( item =>
                        item.Path == plan.Path && item.Stamp == plan.Stamp );
                    if ( shared != null )
                    {
                        shared.Aliases.Add( entry );
                        plan = shared;
                    }
                    else
                        result.Generations.Add( plan );
                    generations.Add( entry.Key, plan );
                    signature += "\n" + plan.Path + ":" + plan.Stamp + ":"
                        + AssetDatabase.AssetPathToGUID( plan.Path ) + ":"
                        + ( plan.Existing != null ? AssetDatabase.GetAssetDependencyHash( plan.Path ).ToString( ) : string.Empty );
                }
                else
                {
                    if ( output != null )
                        signature += "\n" + AssetDatabase.GetAssetDependencyHash( AssetDatabase.GetAssetPath( output ) );
                }
                outputs.Add( entry.Key, output );
            }
            bool pending = generations.ContainsKey( entry.Key );
            Object resultPrefab = outputs[entry.Key];
            if ( entry.Looping != binding.Looping )
            {
                result.Errors.Add( "사용처와 효과의 단발/유지 방식이 다릅니다: " + entry.Key );
                rejectedTargets.Add( targetKey );
                continue;
            }
            if ( OutputFor( binding.Target, binding.Field, binding.Module ) != entry.Output )
            {
                result.Errors.Add( "사용처와 효과의 월드/UI 출력 종류가 다릅니다: " + entry.Key );
                rejectedTargets.Add( targetKey );
                continue;
            }
            bool? required = RequiredLoop( binding.Target, binding.Field, binding.Module );
            if ( required.HasValue && required.Value != binding.Looping )
            {
                result.Errors.Add( "기존 호출부가 요구하는 재생 방식과 다릅니다: " + binding.Field );
                rejectedTargets.Add( targetKey );
                continue;
            }

            if ( target is GameObject uiOwner && binding.Module == VfxTargetModule.UiPlayer
                && uiOwner.transform is not RectTransform )
            {
                result.Errors.Add( "UI 효과 추가 대상은 RectTransform 오브젝트를 선택하세요: " + uiOwner.name );
                rejectedTargets.Add( targetKey );
                continue;
            }
            string path = AssetDatabase.GetAssetPath( target );
            if ( path.StartsWith( "Assets/", StringComparison.Ordinal ) == false
                || ( path.EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase ) == false
                    && path.EndsWith( ".asset", StringComparison.OrdinalIgnoreCase ) == false )
                || EditorUtility.IsPersistent( target ) == false || AssetDatabase.IsOpenForEdit( path ) == false )
            {
                result.Errors.Add( "편집 가능한 프로젝트 프리팹/데이터를 지정하세요: " + path );
                rejectedTargets.Add( targetKey );
                continue;
            }
            if ( PrefabUtility.GetPrefabAssetType( target ) == PrefabAssetType.Model )
            {
                result.Errors.Add( "모델 프리팹은 직접 변경하지 않습니다: " + path );
                rejectedTargets.Add( targetKey );
                continue;
            }
            Object before = null;
            if ( target is not GameObject )
            {
                using ( var serialized = new SerializedObject( target ) )
                    before = serialized.FindProperty( binding.Field ).objectReferenceValue;
            }
            string outputPath = pending ? generations[entry.Key].Path : AssetDatabase.GetAssetPath( resultPrefab );
            var change = new VfxSetupChange { Target = target, Field = binding.Field,
                Before = before, After = pending ? generations[entry.Key].Existing : resultPrefab, Path = path,
                EffectKey = entry.Key, Module = binding.Module, OutputPath = outputPath,
                Key = targetKey,
                Description = "[" + Owner( Category( binding ) ) + "/" + Category( binding ) + "] "
                    + path + "/" + target.name + "/" + binding.Field + ": "
                    + ( before != null ? before.name : "미지정" ) + " → "
                    + ( pending ? ( generations[entry.Key].Existing != null ? "갱신 예정: " : "생성 예정: " )
                        + outputPath : resultPrefab.name ) };
            if ( grouped.ContainsKey( change.Key ) == false )
                grouped.Add( change.Key, new( ) );
            grouped[change.Key].Add( (binding, change) );
        }

        //충돌하는 출력은 공유 필드를 판정하기 전에 모두 제외
        foreach ( var plans in result.Generations.GroupBy( plan => plan.Path, StringComparer.OrdinalIgnoreCase ) )
        {
            if ( plans.Count( ) <= 1 )
                continue;
            rejectedPaths.Add( plans.Key );
            result.Errors.Add( "생성 / 갱신 경로가 중복됩니다: " + plans.Key );
        }

        foreach ( var rows in grouped.Values )
        {
            VfxSetupChange change = rows[0].change;
            if ( rejectedTargets.Contains( change.Key )
                || rows.Any( row => rejectedPaths.Contains( row.change.OutputPath ) ) )
            {
                result.Notes.Add( "관련 검사 오류로 연결 제외: " + change.Description );
                continue;
            }
            if ( rows.Any( row => row.change.OutputPath != change.OutputPath ) )
            {
                result.Errors.Add( "공유 필드에 서로 다른 효과를 배정했습니다: " + change.Path + "/" + change.Field );
                continue;
            }
            if ( rows.Count > 1 )
                result.Notes.Add( "공유 배정 " + rows.Count + "건을 합침: " + change.Description
                    + "\n  " + string.Join( "\n  ", rows.Select( row => AssetDatabase.GetAssetPath( row.binding.Target )
                        + "/" + row.binding.Target.name + " [" + row.binding.EffectKey + "]" ).Distinct( ) ) );
            if ( rows.Any( row => row.binding.Target != change.Target ) )
                result.Notes.Add( "상속/캐릭터 대상 해석 결과: " + change.Description );
            if ( change.After != null && change.Before == change.After )
            {
                bool updating = result.Generations.Any( plan => plan.Path == change.OutputPath );
                result.Notes.Add( ( updating ? "연결 유지 / 효과 갱신: " : "이미 연결됨: " ) + change.Description );
                if ( updating )
                    appliedOutputs.Add( change.OutputPath );
                continue;
            }
            if ( change.Before != null && rows.Any( row => row.binding.ReplaceExisting == false ) )
            {
                result.Notes.Add( "기존 값 보존. 교체하려면 공유 배정 모두에서 허용: " + change.Description );
                continue;
            }
            if ( change.Target is GameObject )
                result.Notes.Add( change.Module + " 모듈 추가 예정. 재생 시점 연결은 담당자 작업: " + change.Path );
            result.Changes.Add( change );
            appliedOutputs.Add( change.OutputPath );
        }
        //기존 연결 자체를 유지하는 갱신도 저장. 교체를 허용하지 않은 출력은 제외
        result.Generations.RemoveAll( plan => appliedOutputs.Contains( plan.Path ) == false );
        result.Signature = signature;
        return result;
    }

    /// <summary>
    /// 베리언트의 명시적인 오버라이드를 유지하며 실제 저장 대상 확인
    /// </summary>
    static Object ResolveTarget ( VfxSetupBinding binding )
    {
        Object target = binding.Target;
        if ( target is GameObject owner )
            target = ( Object ) Module( owner, binding.Module ) ?? owner;
        if ( binding.LocalOverride || target is ScriptableObject )
            return target;

        Object source = PrefabUtility.GetCorrespondingObjectFromSource( target );
        while ( source != null )
        {
            if ( target is Component )
            {
                using ( var serialized = new SerializedObject( target ) )
                {
                    if ( serialized.FindProperty( binding.Field ).prefabOverride )
                        break;
                }
            }
            target = source;
            if ( target is GameObject sourceOwner )
                target = ( Object ) Module( sourceOwner, binding.Module ) ?? sourceOwner;
            source = PrefabUtility.GetCorrespondingObjectFromSource( target );
        }
        return target;
    }

    /// <summary>
    /// 오브젝트의 선택한 표현 모듈 조회
    /// </summary>
    public static Component Module ( GameObject owner, VfxTargetModule module )
    {
        if ( module == VfxTargetModule.UiPlayer )
            return owner.GetComponent<UiVfxPlayer>( );
        if ( module == VfxTargetModule.WorldPlayer )
            return owner.GetComponent<VfxPlayer>( );
        return owner.GetComponent<CharacterVfx>( );
    }

    /// <summary>
    /// 표시한 계획과 재검사 결과가 같을 때 파일별 적용
    /// </summary>
    public static string Apply ( VfxSetupProfile profile, VfxSetupInspection shown )
    {
        VfxSetupInspection current = Inspect( profile );
        if ( shown == null || SamePlan( shown, current ) == false )
            return "검사 이후 설정/대상이 변경됐습니다. 다시 검사하고 변경 목록을 확인하세요.";
        if ( current.Changes.Count == 0 && current.Generations.Count == 0 )
            return "변경 대상이 없습니다.";

        var saved = new List<string>( );
        var created = new List<string>( );
        string editing = string.Empty;
        try
        {
            foreach ( VfxGeneration generation in current.Generations )
            {
                editing = generation.Path;
                Object output = VfxPrefabBuilder.Generate( generation );
                created.Add( generation.Path );
                foreach ( VfxSetupEffect entry in generation.Aliases )
                    entry.SetResult( output, generation.Stamp );
                EditorUtility.SetDirty( profile );
                AssetDatabase.SaveAssetIfDirty( profile );
            }
            foreach ( VfxSetupChange change in current.Changes )
            {
                if ( current.Generations.Any( plan => plan.Path == change.OutputPath ) )
                    change.After = profile.Effects.First( entry => entry.Key == change.EffectKey ).Result;
            }
            //상속 대상의 기존 값을 보존하도록 베리언트부터 저장
            foreach ( var file in current.Changes.GroupBy( item => item.Path )
                .OrderByDescending( group => group.Max( item => InheritanceDepth( item.Target ) ) ) )
            {
                editing = file.Key;
                SaveFile( file.Key, file.ToArray( ) );
                saved.Add( file.Key );
            }
        }
        catch ( Exception exception )
        {
            string message = "적용 중단: " + editing + "\n" + exception.Message
                + "\n생성 / 갱신 완료:\n" + string.Join( "\n", created )
                + "\n저장 완료:\n" + string.Join( "\n", saved )
                + "\n실패 파일의 편집 상태를 확인하고 다시 검사하세요.";
            Debug.LogError( "[VFX Setup] " + message, profile );
            return message;
        }
        return "필드 " + current.Changes.Count + "건 / 파일 " + saved.Count
            + "개 저장 완료 / 효과 생성 및 갱신 " + created.Count + "개. 실제 재생 연결/검증은 별도입니다.\n"
            + string.Join( "\n", created.Concat( saved ) )
            + ( current.Errors.Count == 0 ? string.Empty : "\n검사 오류로 제외:\n" + string.Join( "\n", current.Errors ) );
    }

    /// <summary>
    /// 저장 대상, 기존 값과 적용 값 대조
    /// </summary>
    static bool SamePlan ( VfxSetupInspection left, VfxSetupInspection right )
    {
        return left.Signature == right.Signature && left.Errors.SequenceEqual( right.Errors )
            && left.Generations.Select( plan => ( plan.Path, plan.Stamp, plan.Existing ) )
                .SequenceEqual( right.Generations.Select( plan => ( plan.Path, plan.Stamp, plan.Existing ) ) )
            && left.Changes.Count == right.Changes.Count
            && left.Changes.All( before => right.Changes.Any( after => before.Key == after.Key
                && before.Before == after.Before && before.After == after.After && before.OutputPath == after.OutputPath ) );
    }

    /// <summary>
    /// 상속된 대상을 원본보다 먼저 저장하기 위한 깊이 조회
    /// </summary>
    static int InheritanceDepth ( Object target )
    {
        int depth = 0;
        if ( target is ScriptableObject )
            return depth;
        while ( ( target = PrefabUtility.GetCorrespondingObjectFromSource( target ) ) != null )
            depth++;
        return depth;
    }

    /// <summary>
    /// 한 파일의 필드를 편집하고 해당 파일만 저장
    /// </summary>
    static void SaveFile ( string path, VfxSetupChange[] changes )
    {
        bool prefab = path.EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase );
        GameObject contents = prefab ? PrefabUtility.LoadPrefabContents( path ) : null;
        try
        {
            foreach ( VfxSetupChange change in changes )
            {
                Object target = prefab ? ResolveContents( change.Target, contents ) : change.Target;
                if ( target is GameObject owner )
                {
                    Component module = Module( owner, change.Module );
                    if ( module == null )
                        module = change.Module == VfxTargetModule.UiPlayer ? owner.AddComponent<UiVfxPlayer>( )
                            : change.Module == VfxTargetModule.WorldPlayer ? owner.AddComponent<VfxPlayer>( )
                            : owner.AddComponent<CharacterVfx>( );
                    target = module;
                }
                using ( var serialized = new SerializedObject( target ) )
                {
                    SerializedProperty field = serialized.FindProperty( change.Field );
                    if ( field == null || field.propertyType != SerializedPropertyType.ObjectReference
                        || field.objectReferenceValue != change.Before )
                        throw new InvalidOperationException( "검사 이후 필드가 변경됐습니다: " + change.Field );
                    if ( prefab == false )
                        Undo.RecordObject( target, "VFX 참조 배정" );
                    field.objectReferenceValue = change.After;
                    serialized.ApplyModifiedPropertiesWithoutUndo( );
                }
                if ( prefab == false )
                {
                    EditorUtility.SetDirty( target );
                    AssetDatabase.SaveAssetIfDirty( target );
                }
            }
            if ( prefab )
            {
                PrefabUtility.SaveAsPrefabAsset( contents, path, out bool success );
                if ( success == false )
                    throw new IOException( "프리팹 저장 실패: " + path );
            }
        }
        finally
        {
            if ( contents != null )
                PrefabUtility.UnloadPrefabContents( contents );
        }

        //저장 후 실제 원본을 다시 읽어 확인
        foreach ( VfxSetupChange change in changes )
        {
            Object target = change.Target is GameObject owner ? Module( owner, change.Module ) : change.Target;
            using ( var serialized = new SerializedObject( target ) )
            {
                if ( serialized.FindProperty( change.Field ).objectReferenceValue != change.After )
                    throw new IOException( "저장 결과 불일치: " + change.Description );
            }
        }
    }

    /// <summary>
    /// 원본 계층 위치와 컴포넌트 순서로 편집본 대상 확인
    /// </summary>
    static Object ResolveContents ( Object original, GameObject contents )
    {
        Transform source = original is GameObject owner ? owner.transform : ( ( Component ) original ).transform;
        var indices = new Stack<int>( );
        while ( source.parent != null )
        {
            indices.Push( source.GetSiblingIndex( ) );
            source = source.parent;
        }
        Transform target = contents.transform;
        while ( indices.Count > 0 )
            target = target.GetChild( indices.Pop( ) );
        if ( original is GameObject )
            return target.gameObject;
        Component component = ( Component ) original;
        int index = Array.IndexOf( component.GetComponents<Component>( ), component );
        Component[] components = target.GetComponents<Component>( );
        if ( index < 0 || index >= components.Length || components[index] == null
            || components[index].GetType( ) != component.GetType( ) )
            throw new InvalidOperationException( "프리팹의 컴포넌트 구성이 변경됐습니다." );
        return components[index];
    }

    /// <summary>
    /// 선택 범위의 사용처를 기존 배정 보존 후 추가
    /// </summary>
    /// <param name="profile">배정 프로필</param>
    public static string Discover ( VfxSetupProfile profile )
    {
        if ( CanEdit( profile ) == false )
            return "저장된 프로필을 편집 모드에서 지정하세요.";
        var notes = new List<string>( );
        List<VfxSetupBinding> uses = DiscoverUses( notes );
        Undo.RecordObject( profile, "VFX 연결 추천" );
        foreach ( VfxSetupBinding usage in uses.Where( item => Includes( profile.Scope, item ) ) )
            profile.AddBinding( usage.Target, usage.Field, usage.SuggestionKey, usage.Looping );
        EditorUtility.SetDirty( profile );
        return "탐색 범위: " + profile.Scope + " / 초안 추가 완료. 기존 선택 보존. 공유 필드는 검사에서 확인하세요.\n"
            + string.Join( "\n", notes );
    }

    /// <summary>
    /// 실제 컴포넌트와 데이터에서 배정 가능한 사용처 수집
    /// </summary>
    internal static List<VfxSetupBinding> DiscoverUses ( List<string> notes = null )
    {
        var uses = new List<VfxSetupBinding>( );
        string[] folders = new[] { "Assets/04. Prefabs/Player", "Assets/04. Prefabs/Companion" }
            .Where( AssetDatabase.IsValidFolder ).ToArray( );
        foreach ( string guid in folders.Length > 0 ? AssetDatabase.FindAssets( "t:Prefab", folders ) : Array.Empty<string>( ) )
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>( AssetDatabase.GUIDToAssetPath( guid ) );
            foreach ( Hero hero in root.GetComponentsInChildren<Hero>( true ) )
            {
                AddCharacter( uses, hero.gameObject, "hero" );
                using ( var serialized = new SerializedObject( hero ) )
                {
                    SerializedProperty weapons = serialized.FindProperty( "_weaponBindings" );
                    for ( int i = 0; i < weapons.arraySize; i++ )
                    {
                        SerializedProperty row = weapons.GetArrayElementAtIndex( i );
                        string key = ( ( WeaponType ) row.FindPropertyRelative( "_weaponType" ).intValue ).ToString( ).ToLowerInvariant( );
                        AddExecutor( uses, row.FindPropertyRelative( "_attackExecutor" ).objectReferenceValue, key + ".attack" );
                        AddExecutor( uses, row.FindPropertyRelative( "_skillExecutor" ).objectReferenceValue, key + ".skill" );
                        AddExecutor( uses, row.FindPropertyRelative( "_ultimateExecutor" ).objectReferenceValue,
                            key + ".ultimate", "_ultimateVfx" );
                    }
                }
            }
            foreach ( Companion companion in root.GetComponentsInChildren<Companion>( true ) )
            {
                AddCharacter( uses, companion.gameObject, "companion" );
                uses.Add( new VfxSetupBinding(
                    companion, "_hireVfx", "companion.hire", false, true ) );
                Object attacker = Reference( companion, "_attacker" );
                AddExecutor( uses, Reference( attacker, "_attackExecutor" ), "companion." + root.name + ".attack" );
            }
        }
        const string configs = "Assets/03. Configs";
        foreach ( string guid in AssetDatabase.IsValidFolder( configs )
            ? AssetDatabase.FindAssets( "t:WeaponData t:ItemData", new[] { configs } ) : Array.Empty<string>( ) )
        {
            Object data = AssetDatabase.LoadMainAssetAtPath( AssetDatabase.GUIDToAssetPath( guid ) );
            if ( data is ItemData item )
                uses.Add( new VfxSetupBinding( item, "_useVfx", "item." + item.name, false, true ) );
            if ( data is not WeaponData weapon )
                continue;
            string key = weapon.WeaponType.ToString( ).ToLowerInvariant( );
            if ( weapon.SkillData is BowSkillData bow )
                AddProjectile( uses, bow.ProjectileData, key + ".skill" );
            else if ( weapon.SkillData is StaffSkillData staff )
                AddProjectile( uses, staff.ProjectileData, key + ".skill" );
            if ( weapon.UltimateData is BowUltimateData rain && rain.AreaPrefab != null )
                uses.Add( new VfxSetupBinding( rain.AreaPrefab, "_spawnVfx", key + ".ultimate.spawn", false, true ) );
            else if ( weapon.UltimateData is StaffUltimateData pillar && pillar.AreaPrefab != null )
                uses.Add( new VfxSetupBinding( pillar.AreaPrefab, "_spawnVfx", key + ".ultimate.spawn", false, true ) );
        }
        AddKmbUses( uses, notes );
        return uses;
    }

    /// <summary>
    /// 적 전투 타입과 설치물의 실제 필드 탐색
    /// </summary>
    static void AddKmbUses ( List<VfxSetupBinding> uses, List<string> notes )
    {
        const string folder = "Assets/04. Prefabs";
        if ( AssetDatabase.IsValidFolder( folder ) == false )
            return;
        int enemyCount = 0;
        int installationCount = 0;
        int installationUses = 0;
        foreach ( string guid in AssetDatabase.FindAssets( "t:Prefab", new[] { folder } ) )
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>( AssetDatabase.GUIDToAssetPath( guid ) );
            foreach ( EnemyCombat enemy in root.GetComponentsInChildren<EnemyCombat>( true ) )
            {
                foreach ( string field in _characterFields.Where( item => item != "_revive" ) )
                    uses.Add( new VfxSetupBinding( enemy.gameObject, field, "enemy." + field.TrimStart( '_' ),
                        RequiredLoop( enemy.gameObject, field ).Value, true ) );
                AddComponentUses( uses, enemy.gameObject, "enemy." + root.name );
                enemyCount++;
            }
            foreach ( PlacedInstallation installation in root.GetComponentsInChildren<PlacedInstallation>( true ) )
            {
                int before = uses.Count;
                AddComponentUses( uses, installation.gameObject, "installation." + root.name );
                installationUses += uses.Count - before;
                installationCount++;
            }
        }
        notes?.Add( "KMB 적/보스: EnemyCombat 기반 프리팹 " + enemyCount
            + "개 탐색. 독립 보스 타입은 직접 지정의 적/보스 분류로 연결하세요. 재생/초기화/정리는 담당자 연계입니다." );
        notes?.Add( "KMB 설치물: " + installationCount + "개 / 배정 가능 필드 " + installationUses
            + "개. 필드가 없으면 KMB 구현 후 다시 탐색하세요." );
    }

    /// <summary>
    /// 실제 효과 필드와 투사체 데이터 참조만 배정 후보로 추가
    /// </summary>
    static void AddComponentUses ( List<VfxSetupBinding> uses, GameObject owner, string key )
    {
        MonoBehaviour[] components = owner.GetComponentsInChildren<MonoBehaviour>( true );
        for ( int i = 0; i < components.Length; i++ )
        {
            MonoBehaviour component = components[i];
            if ( component == null || component is CharacterVfx )
                continue;
            //컴포넌트 추가나 순서 변경에도 사용처 식별 유지
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier( component, out string _, out long localId );
            string prefix = key + "." + component.GetType( ).Name + "." + localId;
            foreach ( string field in Fields( component ) )
            {
                bool looping = RequiredLoop( component, field ) ?? false;
                uses.Add( new VfxSetupBinding( component, field, prefix + "." + field.TrimStart( '_' ), looping, true ) );
            }
            using ( var serialized = new SerializedObject( component ) )
            {
                SerializedProperty property = serialized.GetIterator( );
                while ( property.NextVisible( true ) )
                {
                    if ( property.depth == 0 && property.propertyType == SerializedPropertyType.ObjectReference
                        && property.objectReferenceValue is ProjectileData projectile )
                        AddProjectile( uses, projectile, prefix + "." + property.name.TrimStart( '_' ) );
                }
            }
        }
    }

    /// <summary>
    /// 캐릭터의 공통 표현 필드 추천
    /// </summary>
    /// <param name="uses">수집할 사용처</param>
    /// <param name="owner">캐릭터 오브젝트</param>
    /// <param name="key">용도 접두사</param>
    static void AddCharacter ( List<VfxSetupBinding> uses, GameObject owner, string key )
    {
        foreach ( string field in _characterFields )
            uses.Add( new VfxSetupBinding( owner, field, key + "." + field.TrimStart( '_' ), RequiredLoop( owner, field ).Value, true ) );
    }

    /// <summary>
    /// 실제 실행자의 범위 효과 또는 투사체 참조 추천
    /// </summary>
    /// <param name="uses">수집할 사용처</param>
    /// <param name="executor">공격 실행자</param>
    /// <param name="key">용도 키</param>
    static void AddExecutor (
        List<VfxSetupBinding> uses, Object executor, string key, string areaVfxField = "_attackVfx" )
    {
        if ( executor is AreaAttackExecutor )
            uses.Add( new VfxSetupBinding( executor, areaVfxField, key, false, true ) );
        else if ( executor is SwordSkillExecutor )
            uses.Add( new VfxSetupBinding( executor, "_attackVfx", key, false, true ) );
        else if ( executor is ProjectileAttackExecutor )
            AddProjectile( uses, Reference( executor, "_projectileData" ) as ProjectileData, key );
    }

    /// <summary>
    /// 투사체의 발사, 비행과 충돌 효과 추천
    /// </summary>
    /// <param name="uses">수집할 사용처</param>
    /// <param name="data">투사체 데이터</param>
    /// <param name="key">용도 접두사</param>
    static void AddProjectile ( List<VfxSetupBinding> uses, ProjectileData data, string key )
    {
        if ( data == null )
            return;
        uses.Add( new VfxSetupBinding( data, "_launchVfx", key + ".launch", false, true ) );
        uses.Add( new VfxSetupBinding( data, "_flightVfx", key + ".flight", true, true ) );
        uses.Add( new VfxSetupBinding( data, "_impactVfx", key + ".impact", false, true ) );
    }

    /// <summary>
    /// 추천에 필요한 기존 참조 읽기
    /// </summary>
    static Object Reference ( Object target, string field )
    {
        if ( target == null )
            return null;
        using ( var serialized = new SerializedObject( target ) )
            return serialized.FindProperty( field ).objectReferenceValue;
    }
}
#endif
