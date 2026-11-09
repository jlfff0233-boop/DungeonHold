#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// 검사한 효과 생성과 갱신 계획
/// </summary>
public class VfxGeneration
{
    public VfxSetupEffect Entry;		//출력 정보를 받을 배정
    public string Path;		//검사한 출력 경로
    public string Stamp;		//검사한 원본과 설정
    public Object Existing;		//같은 경로에서 갱신할 기존 효과
    public List<VfxSetupEffect> Aliases = new( );		//같은 원본과 설정을 공유할 역할
}

/// <summary>
/// 선별한 원본을 월드 또는 UGUI 효과 프리팹으로 구성
/// </summary>
public static class VfxPrefabBuilder
{
    /// <summary>
    /// 생성과 갱신 대상 검사 후 저장
    /// </summary>
    public static string Build ( VfxSetupProfile profile )
    {
        if ( VfxSetupUtility.CanEdit( profile ) == false )
            return "저장된 프로필을 편집 모드에서 지정하세요.";

        var errors = new List<string>( );
        var plans = new List<VfxGeneration>( );
        foreach ( var group in profile.Effects.GroupBy( item => item.Key ) )
        {
            if ( string.IsNullOrWhiteSpace( group.Key ) || group.Count( ) > 1 )
                errors.Add( "비어 있거나 중복된 효과 키: " + group.Key );
        }
        foreach ( VfxSetupEffect entry in profile.Effects )
        {
            VfxGeneration plan = Plan( entry, profile.OutputFolder, errors, profile.Effects );
            if ( plan != null )
            {
                VfxGeneration shared = plans.FirstOrDefault( item => item.Path == plan.Path && item.Stamp == plan.Stamp );
                if ( shared != null )
                    shared.Aliases.Add( entry );
                else
                    plans.Add( plan );
            }
        }
        if ( plans.GroupBy( item => item.Path ).Any( group => group.Count( ) > 1 ) )
            errors.Add( "같은 생성 파일명에 서로 다른 효과 설정을 지정했습니다." );
        if ( errors.Count > 0 )
        {
            Debug.LogError( "[VFX Setup] 생성 검사 실패\n" + string.Join( "\n", errors ), profile );
            return "생성 검사 실패. Console 내용을 복사할 수 있습니다.\n" + string.Join( "\n", errors );
        }
        if ( plans.Count == 0 )
            return "생성 / 갱신 대상이 없습니다. 원본과 생성 체크를 확인하세요. 기존 효과는 재사용합니다.";

        var saved = new List<string>( );
        string editing = profile.OutputFolder;
        //여러 파일 저장 경계에서만 실패를 보고하며 성공한 파일은 보존
        try
        {
            foreach ( VfxGeneration plan in plans )
            {
                string path = plan.Path;
                editing = path;
                Object prefab = Generate( plan );
                saved.Add( path );
                foreach ( VfxSetupEffect entry in plan.Aliases )
                    entry.SetResult( prefab, plan.Stamp );
                EditorUtility.SetDirty( profile );
                AssetDatabase.SaveAssetIfDirty( profile );
            }
        }
        catch ( Exception exception )
        {
            string message = "생성 중단: " + editing + "\n" + exception.Message
                + "\n저장 완료:\n" + string.Join( "\n", saved );
            Debug.LogError( "[VFX Setup] " + message, profile );
            return message;
        }
        return "공용 효과 " + saved.Count + "개 생성 / 갱신. 사용처 연결은 별도 적용입니다.\n"
            + string.Join( "\n", saved );
    }

    /// <summary>
    /// 기존 프리팹의 런타임 재생 계약 검사
    /// </summary>
    public static void InspectEffect ( VfxSetupEffect entry, List<string> errors )
    {
        if ( entry.Output == VfxOutput.Ui )
        {
            if ( entry.UiPrefab == null || EditorUtility.IsPersistent( entry.UiPrefab ) == false
                || entry.UiPrefab.transform.parent != null )
                errors.Add( "루트 UiVfxEffect 프리팹을 지정하거나 생성하세요: " + entry.Key );
            else if ( entry.UiPrefab.Validate( ) == false || entry.UiPrefab.Looping != entry.Looping )
                errors.Add( "UI 효과 규격 또는 반복 설정 오류: " + entry.Key );
            return;
        }
        if ( entry.Prefab == null )
        {
            errors.Add( "공용 효과를 지정하거나 먼저 생성하세요: " + entry.Key );
            return;
        }
        if ( EditorUtility.IsPersistent( entry.Prefab ) == false
            || entry.Prefab.transform.parent != null )
        {
            errors.Add( "VfxEffect가 루트에 있는 프리팹 에셋을 지정하세요: " + entry.Key );
            return;
        }
        if ( VfxEffect.IsMaintained( entry.Prefab.gameObject ) != entry.Looping )
            errors.Add( "효과의 단발/유지 설정이 다릅니다: " + entry.Key );
        InspectVisuals( entry.Prefab.gameObject, errors );
    }

    /// <summary>
    /// 선택한 출력 종류로 자동 구성 가능한 원본인지 확인
    /// </summary>
    static void InspectSource ( VfxSetupEffect entry, List<string> errors )
    {
        GameObject source = entry.Source;
        if ( EditorUtility.IsPersistent( source ) == false
            || source.transform.parent != null || PrefabUtility.IsPartOfPrefabAsset( source ) == false )
        {
            errors.Add( "원본은 프로젝트의 루트 프리팹으로 지정하세요: " + entry.Key );
            return;
        }
        if ( entry.Output == VfxOutput.Ui )
        {
            if ( source.transform is not RectTransform || source.GetComponentInChildren<Graphic>( true ) == null
                || source.GetComponentInChildren<ParticleSystem>( true ) != null
                || source.GetComponentInChildren<Renderer>( true ) != null
                || source.GetComponentInChildren<Animation>( true ) != null
                || source.GetComponentsInChildren<Canvas>( true ).Length > 0
                || source.GetComponentsInChildren<MonoBehaviour>( true ).Any( item => item is IEventSystemHandler ) )
                errors.Add( "UI 원본은 입력 모듈/Canvas/파티클/구형 Animation 없는 UGUI 프리팹을 지정하세요: " + entry.Key );
            if ( source.GetComponentsInChildren<MonoBehaviour>( true ).Any(
                item => item == null || ( item is not UIBehaviour && item is not UiVfxEffect ) ) )
                errors.Add( "UI 원본의 사용자 스크립트는 수동 구성 후 기존 효과로 지정하세요: " + entry.Key );
            if ( entry.Looping == false && entry.Duration <= 0f )
                errors.Add( "UI 단발 완료 시간은 양수로 지정하세요: " + entry.Key );
            return;
        }
        if ( source.GetComponentsInChildren<MonoBehaviour>( true ).Length > 0
            || source.GetComponentInChildren<Animator>( true ) != null
            || source.GetComponentInChildren<Animation>( true ) != null )
        {
            errors.Add( "스크립트/애니메이션이 있는 원본은 수동 구성 후 기존 효과로 지정하세요: " + entry.Key );
        }
        ParticleSystem[] particles = source.GetComponentsInChildren<ParticleSystem>( true );
        if ( particles.Length == 0 && source.GetComponentInChildren<TrailRenderer>( true ) == null )
            errors.Add( "파티클/트레일이 없는 원본: " + entry.Key );
        else if ( VfxEffect.IsMaintained( source ) != entry.Looping )
            errors.Add( "원본의 반복 여부가 선택한 방식과 다릅니다: " + entry.Key );
        if ( source.GetComponentsInChildren<Renderer>( true ).Any(
            item => item is not ParticleSystemRenderer && item is not TrailRenderer ) )
            errors.Add( "파티클/트레일 이외의 렌더러가 있는 원본은 수동 구성하세요: " + entry.Key );
        InspectVisuals( source, errors );
    }

    /// <summary>
    /// 출력의 누락 스크립트와 머티리얼 참조 확인
    /// </summary>
    static void InspectVisuals ( GameObject root, List<string> errors )
    {
        if ( root.GetComponentsInChildren<Component>( true ).Any( item => item == null ) )
            errors.Add( "누락된 스크립트가 있습니다: " + root.name );
        foreach ( Renderer renderer in root.GetComponentsInChildren<Renderer>( true ) )
        {
            if ( renderer is not ParticleSystemRenderer && renderer is not TrailRenderer )
                continue;
            if ( renderer.enabled == false
                || ( renderer is ParticleSystemRenderer particles && particles.renderMode == ParticleSystemRenderMode.None ) )
                continue;
            if ( renderer.sharedMaterials.Length == 0
                || renderer.sharedMaterials.Any( material => material == null || material.shader == null ) )
                errors.Add( "출력 머티리얼/셰이더 참조 누락: " + root.name + "/" + renderer.name );
        }
    }

    /// <summary>
    /// 검사한 원본을 비활성 공용 루트 아래 구성하여 저장
    /// </summary>
    static VfxEffect CreateWorld ( GameObject source, string path )
    {
        GameObject root = new GameObject( Path.GetFileNameWithoutExtension( path ) );
        root.SetActive( false );
        try
        {
            root.AddComponent<VfxEffect>( );
            ParticleSystem particles = root.GetComponent<ParticleSystem>( );
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.useUnscaledTime = false;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            particles.GetComponent<ParticleSystemRenderer>( ).enabled = false;

            GameObject visual = ( GameObject ) PrefabUtility.InstantiatePrefab( source, root.transform );
            //원본 내장 오디오 대신 게임의 사운드 호출 경로 사용
            foreach ( AudioSource audio in visual.GetComponentsInChildren<AudioSource>( true ) )
                audio.enabled = false;
            visual.SetActive( true );
            GameObject saved = PrefabUtility.SaveAsPrefabAsset( root, path, out bool success );
            if ( success == false )
                throw new IOException( "프리팹 저장 실패: " + path );
            return saved.GetComponent<VfxEffect>( );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate( root );
        }
    }

    /// <summary>
    /// 원본 변경 여부와 출력 종류를 포함한 생성 상태 계산
    /// </summary>
    public static string SourceStamp ( VfxSetupEffect entry )
    {
        string path = AssetDatabase.GetAssetPath( entry.Source );
        return AssetDatabase.AssetPathToGUID( path ) + ":" + AssetDatabase.GetAssetDependencyHash( path )
            + ":" + entry.Output + ":" + entry.Looping + ":"
            + ( entry.Output == VfxOutput.Ui ? entry.Duration : 0f )
                .ToString( "R", System.Globalization.CultureInfo.InvariantCulture );
    }

    /// <summary>
    /// 원본 이름으로 생성 또는 기존 결과 갱신 계획
    /// </summary>
    /// <param name="entry">효과 배정</param>
    /// <param name="folder">생성 폴더</param>
    /// <param name="errors">검사 오류</param>
    /// <param name="effects">공유 결과 조회 대상</param>
    public static VfxGeneration Plan ( VfxSetupEffect entry, string folder, List<string> errors,
        IReadOnlyList<VfxSetupEffect> effects = null )
    {
        if ( entry.Create == false || entry.Source == null )
            return null;
        if ( EditorUtility.IsPersistent( entry.Source ) == false )
        {
            errors.Add( "프로젝트의 원본 프리팹을 지정하세요: " + entry.Key );
            return null;
        }
        if ( IsFolderPath( folder ) == false )
        {
            errors.Add( "Assets 아래의 생성 폴더를 지정하세요: " + entry.Key );
            return null;
        }
        string stamp = SourceStamp( entry );
        string path = OutputPath( entry, folder );
        if ( ValidKey( Path.GetFileNameWithoutExtension( path ) ) == false )
            errors.Add( "유효한 원본 파일명을 지정하세요: " + entry.Key );

        bool samePath = entry.Result != null && AssetDatabase.GetAssetPath( entry.Result ) == path;
        if ( samePath )
        {
            if ( entry.BuiltStamp == stamp )
                return null;
            //이전 툴로 만든 중첩 원본이 그대로면 기존 조정 결과를 유지
            if ( string.IsNullOrEmpty( entry.BuiltStamp ) && entry.Result is VfxEffect legacy
                && legacy.transform.childCount > 0
                && PrefabUtility.GetCorrespondingObjectFromSource( legacy.transform.GetChild( 0 ).gameObject ) == entry.Source )
                return null;
        }
        InspectSource( entry, errors );
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>( path );
        Object known = samePath ? entry.Result : effects?.FirstOrDefault(
            item => item.Result != null && AssetDatabase.GetAssetPath( item.Result ) == path )?.Result;
        //프로필이 가리키고 같은 원본을 포함한 결과만 갱신. 동명 파일은 덮어쓰지 않음
        bool canUpdate = known != null && existing != null && existing.transform.childCount > 0
            && PrefabUtility.GetCorrespondingObjectFromSource( existing.transform.GetChild( 0 ).gameObject ) == entry.Source;
        if ( ( File.Exists( path ) || AssetDatabase.LoadMainAssetAtPath( path ) != null ) && canUpdate == false )
            errors.Add( "생성 경로에 다른 파일이 있습니다. 원본 이름 또는 생성 폴더를 확인하세요: " + path );
        return new VfxGeneration { Entry = entry, Path = path, Stamp = stamp, Existing = canUpdate ? known : null,
            Aliases = new List<VfxSetupEffect> { entry } };
    }

    /// <summary>
    /// 원본 파일명으로 공용 효과 경로 구성
    /// </summary>
    /// <param name="entry">효과 배정</param>
    /// <param name="folder">생성 폴더</param>
    internal static string OutputPath ( VfxSetupEffect entry, string folder )
    {
        string name = Path.GetFileNameWithoutExtension( AssetDatabase.GetAssetPath( entry.Source ) );
        return folder.TrimEnd( '/' ) + "/Vfx_" + name + ".prefab";
    }

    /// <summary>
    /// 검사한 경로에 생성 또는 갱신하며 사용처는 변경하지 않음
    /// </summary>
    public static Object Generate ( VfxGeneration plan )
    {
        EnsureFolder( System.IO.Path.GetDirectoryName( plan.Path ).Replace( '\\', '/' ) );
        return plan.Entry.Output == VfxOutput.Ui ? CreateUi( plan.Entry, plan.Path )
            : CreateWorld( plan.Entry.Source, plan.Path );
    }

    /// <summary>
    /// 원본 UI 구성을 비활성 RectTransform 루트 아래 보존
    /// </summary>
    static UiVfxEffect CreateUi ( VfxSetupEffect entry, string path )
    {
        var root = new GameObject( System.IO.Path.GetFileNameWithoutExtension( path ), typeof( RectTransform ) );
        root.SetActive( false );
        try
        {
            UiVfxEffect effect = root.AddComponent<UiVfxEffect>( );
            using ( var serialized = new SerializedObject( effect ) )
            {
                serialized.FindProperty( "_looping" ).boolValue = entry.Looping;
                serialized.FindProperty( "_duration" ).floatValue = entry.Duration;
                serialized.ApplyModifiedPropertiesWithoutUndo( );
            }
            RectTransform rect = ( RectTransform ) root.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            GameObject visual = ( GameObject ) PrefabUtility.InstantiatePrefab( entry.Source, rect );
            visual.SetActive( true );
            GameObject saved = PrefabUtility.SaveAsPrefabAsset( root, path, out bool success );
            if ( success == false )
                throw new IOException( "UI 효과 저장 실패: " + path );
            return saved.GetComponent<UiVfxEffect>( );
        }
        finally
        {
            Object.DestroyImmediate( root );
        }
    }

    /// <summary>
    /// Assets 내부의 생성 폴더 경로 확인
    /// </summary>
    static bool IsFolderPath ( string path )
    {
        return string.IsNullOrWhiteSpace( path ) == false && path.StartsWith( "Assets/", StringComparison.Ordinal )
            && path.Split( '/' ).All( part => ValidKey( part ) );
    }

    /// <summary>
    /// 경로 이동 문자가 없는 파일명 확인
    /// </summary>
    static bool ValidKey ( string key )
    {
        return string.IsNullOrWhiteSpace( key ) == false && key != "." && key != ".."
            && key.IndexOfAny( Path.GetInvalidFileNameChars( ) ) < 0 && key.Contains( '/' ) == false;
    }

    /// <summary>
    /// 신규 출력 폴더만 순서대로 생성
    /// </summary>
    static void EnsureFolder ( string path )
    {
        string current = "Assets";
        foreach ( string part in path.Split( '/' ).Skip( 1 ) )
        {
            string next = current + "/" + part;
            if ( AssetDatabase.IsValidFolder( next ) == false )
            {
                if ( string.IsNullOrEmpty( AssetDatabase.CreateFolder( current, part ) ) )
                    throw new IOException( "폴더 생성 실패: " + next );
            }
            current = next;
        }
    }
}
#endif
