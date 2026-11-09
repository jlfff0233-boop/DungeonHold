#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 사운드 구성 탐색 - LJ 원본 참조에서 투사체/영역 연결 초안 생성
/// </summary>
public static class SoundSetupDiscovery
{
    /// <summary>
    /// 탐색할 LJ 캐릭터 원본 폴더 확인
    /// </summary>
    /// <param name="path">에셋 경로</param>
    /// <returns>LJ 플레이어/동료 프리팹 경로인지 여부</returns>
    public static bool IsActorPath ( string path )
    {
        return path.StartsWith( "Assets/04. Prefabs/Player/", StringComparison.Ordinal )
            || path.StartsWith( "Assets/04. Prefabs/Companion/", StringComparison.Ordinal );
    }

    /// <summary>
    /// 자동 공유 변경을 허용할 LJ 게임 데이터와 프리팹 경로 확인
    /// </summary>
    /// <param name="path">직접 사용처의 에셋 경로</param>
    /// <returns>LJ 소유로 한정된 경로인지 여부</returns>
    public static bool IsOwnedPath ( string path )
    {
        return IsActorPath( path ) || path.StartsWith( "Assets/04. Prefabs/Weapon/", StringComparison.Ordinal )
            || path.StartsWith( "Assets/03. Configs/Combat/Player/", StringComparison.Ordinal )
            || path.StartsWith( "Assets/03. Configs/Combat/Companion/", StringComparison.Ordinal )
            || path.StartsWith( "Assets/03. Configs/Combat/Projectile/", StringComparison.Ordinal );
    }

    /// <summary>
    /// 기존 수동 연결을 유지하며 현재 프로젝트의 LJ 자동 연결 초안 갱신
    /// </summary>
    /// <param name="profile">편집 중인 구성 프로필</param>
    /// <returns>초안 생성 결과이며 에셋 적용 결과와 구분</returns>
    public static string Discover ( SoundSetupProfile profile )
    {
        if ( SoundSetupUtility.CanEdit( ) == false || profile == null )
            return "편집 가능한 프로필을 지정하세요.";
        var bindings = new List<SoundSetupBinding>( );
        foreach ( string guid in AssetDatabase.FindAssets( "t:Prefab", new[] { "Assets/04. Prefabs/Player", "Assets/04. Prefabs/Companion" } ) )
        {
            string path = AssetDatabase.GUIDToAssetPath( guid );
            if ( IsActorPath( path ) == false )
                continue;
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>( path );
            foreach ( Hero hero in root.GetComponentsInChildren<Hero>( true ) )
            {
                using ( var serialized = new SerializedObject( hero ) )
                {
                    SerializedProperty weapons = serialized.FindProperty( "_weaponBindings" );
                    for ( int i = 0; i < weapons.arraySize; i++ )
                    {
                        SerializedProperty row = weapons.GetArrayElementAtIndex( i );
                        WeaponType type = ( WeaponType ) row.FindPropertyRelative( "_weaponType" ).intValue;
                        Object executor = row.FindPropertyRelative( "_attackExecutor" ).objectReferenceValue;
                        if ( executor is ProjectileAttackExecutor )
                            AddProjectile( bindings, Reference( executor, "_projectileData" ) as ProjectileData, type, false );
                    }
                }
            }
        }
        foreach ( string guid in AssetDatabase.FindAssets( "t:WeaponData t:CompanionHireData", new[] { "Assets/03. Configs/Combat" } ) )
        {
            Object data = AssetDatabase.LoadMainAssetAtPath( AssetDatabase.GUIDToAssetPath( guid ) );
            if ( data is WeaponData weapon )
            {
                if ( weapon.SkillData is BowSkillData bow )
                    AddProjectile( bindings, bow.ProjectileData, WeaponType.Bow, true );
                //스태프 스킬은 실행자에서 전용 ID를 전달하므로 공유 투사체 필드에 제안하지 않음
                if ( weapon.UltimateData is BowUltimateData rain && rain.AreaPrefab != null )
                    AddArea( bindings, rain.AreaPrefab.gameObject, SoundId.BowUltimate, SoundId.BowUltimateTick );
                else if ( weapon.UltimateData is StaffUltimateData pillar && pillar.AreaPrefab != null )
                    AddArea( bindings, pillar.AreaPrefab.gameObject, SoundId.StaffUltimate, SoundId.StaffUltimateTick );
            }
            else if ( data is CompanionHireData hire && hire.Prefab != null && hire.CompanionData != null
                && IsActorPath( AssetDatabase.GetAssetPath( hire.Prefab ) ) )
            {
                Object attacker = Reference( hire.Prefab, "_attacker" );
                Object executor = attacker != null ? Reference( attacker, "_attackExecutor" ) : null;
                if ( executor is ProjectileAttackExecutor )
                    AddProjectile( bindings, Reference( executor, "_projectileData" ) as ProjectileData, hire.CompanionData.WeaponType, false );
            }
        }
        var unique = bindings.GroupBy( binding => binding.Target.GetInstanceID( ) + "/" + binding.Field + "/" + binding.Id )
            .Select( group => group.First( ) ).ToArray( );
        Undo.RecordObject( profile, "사운드 연결 자동 탐색" );
        profile.ReplaceAutomaticBindings( unique );
        EditorUtility.SetDirty( profile );
        return $"연결 초안 {unique.Length}건 생성. 수동 연결과 동일한 자동 연결의 공유 변경 선택은 유지했습니다. "
            + "스태프 스킬 발사/충돌음은 실행자의 StaffSkillLaunch/StaffSkillImpact를 사용합니다. "
            + "공유 사용처와 실제 재생은 별도로 확인하세요. 모듈 추가/데이터 복제는 수행하지 않습니다.";
    }


    /// <summary>
    /// 실제 사용 중인 LJ 투사체 데이터에 역할별 식별자 제안
    /// </summary>
    /// <param name="bindings">초안 목록</param>
    /// <param name="data">실행자가 사용하는 데이터</param>
    /// <param name="type">무기 타입</param>
    /// <param name="skill">기본 스킬 여부</param>
    static void AddProjectile ( List<SoundSetupBinding> bindings, ProjectileData data, WeaponType type, bool skill )
    {
        if ( data == null || AssetDatabase.GetAssetPath( data ).StartsWith( "Assets/03. Configs/Combat/", StringComparison.Ordinal ) == false )
            return;
        SoundId launch;
        SoundId impact;
        if ( type == WeaponType.Bow )
        {
            launch = skill ? SoundId.BowSkillLaunch : SoundId.BowLaunch;
            impact = skill ? SoundId.BowSkillImpact : SoundId.BowImpact;
        }
        else if ( type == WeaponType.Staff )
        {
            launch = SoundId.StaffLaunch;
            impact = SoundId.StaffImpact;
        }
        else
            return;
        bindings.Add( new SoundSetupBinding( data, SoundSetupField.ProjectileLaunch, data.LaunchSound != SoundId.None ? data.LaunchSound : launch ) );
        bindings.Add( new SoundSetupBinding( data, SoundSetupField.ProjectileImpact, data.ImpactSound != SoundId.None ? data.ImpactSound : impact ) );
    }

    /// <summary>
    /// LJ 궁극기에서 사용하는 영역 생성음과 주기음 제안
    /// </summary>
    /// <param name="bindings">초안 목록</param>
    /// <param name="prefab">실제 영역 프리팹</param>
    /// <param name="spawn">생성음</param>
    /// <param name="tick">주기음</param>
    static void AddArea ( List<SoundSetupBinding> bindings, GameObject prefab, SoundId spawn, SoundId tick )
    {
        if ( AssetDatabase.GetAssetPath( prefab ).StartsWith( "Assets/04. Prefabs/Weapon/", StringComparison.Ordinal ) == false )
            return;
        bindings.Add( new SoundSetupBinding( prefab, SoundSetupField.AreaSpawn, spawn ) );
        bindings.Add( new SoundSetupBinding( prefab, SoundSetupField.AreaTick, tick ) );
    }

    /// <summary>
    /// 원본 참조를 변경 없이 조회
    /// </summary>
    /// <param name="target">참조 소유 객체</param>
    /// <param name="field">직렬화 참조 필드</param>
    /// <returns>실제 참조 객체</returns>
    static Object Reference ( Object target, string field )
    {
        using ( var serialized = new SerializedObject( target ) )
            return serialized.FindProperty( field ).objectReferenceValue;
    }
}
#endif
