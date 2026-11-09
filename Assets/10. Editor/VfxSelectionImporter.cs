#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 선택 JSON의 원본과 사용처를 선택 범위에 배정
/// </summary>
public static class VfxSelectionImporter
{
    /// <summary>
    /// 전체 배정 성공 후 프로필에 반영
    /// </summary>
    /// <param name="profile">저장된 프로필</param>
    /// <param name="jsonPath">선택 JSON 경로</param>
    public static string Import ( VfxSetupProfile profile, string jsonPath )
    {
        if ( VfxSetupUtility.CanEdit( profile ) == false )
            return "저장된 프로필을 편집 모드에서 지정하세요.";

        string folder = AssetDatabase.GetAssetPath( profile.SourceFolder );
        if ( folder.StartsWith( "Assets/", StringComparison.Ordinal ) == false
            || AssetDatabase.IsValidFolder( folder ) == false )
            throw new ArgumentException( "선별 원본 폴더를 지정하세요." );

        Selection[] selections = ReadSelections( jsonPath );
        List<VfxSetupBinding> uses = VfxSetupUtility.DiscoverUses( );
        VfxSetupProfile draft = Object.Instantiate( profile );
        draft.name = profile.name;
        try
        {
            string report = FillDraft( draft, folder, selections, uses );

            //초안 처리가 모두 성공한 뒤 원본 프로필만 변경
            Undo.RecordObject( profile, "VFX 선택 JSON 가져오기" );
            EditorUtility.CopySerialized( draft, profile );
            EditorUtility.SetDirty( profile );
            return report + "\n프로필 저장 후 검사와 생성 / 연결 / 저장을 실행하세요.";
        }
        finally
        {
            Object.DestroyImmediate( draft );
        }
    }

    /// <summary>
    /// JSON 형식과 선택 키 중복 확인
    /// </summary>
    /// <param name="path">JSON 경로</param>
    static Selection[] ReadSelections ( string path )
    {
        var settings = new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error };
        SelectionFile file = JsonConvert.DeserializeObject<SelectionFile>( File.ReadAllText( path ), settings );
        if ( file == null || file.Version != 1 || file.Selections.Length == 0 )
            throw new ArgumentException( "version 1과 비어 있지 않은 selections가 필요합니다." );

        var keys = new HashSet<string>( StringComparer.Ordinal );
        var usages = new HashSet<string>( StringComparer.Ordinal );
        foreach ( Selection selection in file.Selections )
        {
            if ( selection == null || string.IsNullOrWhiteSpace( selection.Key )
                || keys.Add( selection.Key ) == false )
                throw new ArgumentException( "선택 key가 비어 있거나 중복되었습니다." );
            foreach ( string usage in selection.Usages )
            {
                if ( string.IsNullOrWhiteSpace( usage ) || usages.Add( usage ) == false )
                    throw new ArgumentException( "사용처가 비어 있거나 중복되었습니다: " + usage );
            }
        }
        return file.Selections;
    }

    /// <summary>
    /// 실제 사용처에 배정하며 기존 선택 보존
    /// </summary>
    /// <param name="draft">작업용 프로필</param>
    /// <param name="folder">원본 폴더</param>
    /// <param name="selections">선택 항목</param>
    /// <param name="uses">탐색한 사용처</param>
    static string FillDraft ( VfxSetupProfile draft, string folder, Selection[] selections,
        List<VfxSetupBinding> uses )
    {
        //선택 범위의 이전 자동 탐색 빈 항목만 제외
        foreach ( VfxSetupBinding binding in draft.Bindings )
        {
            if ( VfxSetupUtility.Includes( draft.Scope, binding ) == false
                || string.IsNullOrEmpty( binding.SuggestionKey ) )
                continue;
            VfxSetupEffect effect = draft.Effects.FirstOrDefault( item => item.Key == binding.EffectKey );
            if ( ( effect == null || ( effect.Source == null && effect.Result == null ) )
                && selections.Any( item => item.Key == binding.EffectKey ) == false )
                binding.SetIncluded( false );
        }

        var notes = new List<string>( );
        int assigned = 0;
        int preserved = 0;
        int pending = 0;
        int outside = 0;
        foreach ( Selection selection in selections )
        {
            //오타는 범위와 관계없이 알리고, 실제 배정만 선택 범위로 제한
            var targets = new List<VfxSetupBinding>( );
            foreach ( string usage in selection.Usages )
            {
                VfxSetupBinding[] matches = uses.Where( item => item.SuggestionKey == usage ).ToArray( );
                if ( matches.Length == 0 )
                    throw new ArgumentException( "탐색되지 않은 사용처입니다: " + usage );
                targets.AddRange( matches.Where( item => VfxSetupUtility.Includes( draft.Scope, item ) ) );
            }
            if ( selection.Usages.Length > 0 && targets.Count == 0 )
            {
                outside++;
                continue;
            }
            GameObject source = LoadSource( folder, selection.RelativePath );
            bool looping = VfxEffect.IsMaintained( source );
            draft.EnsureEffect( selection.Key, looping );
            VfxSetupEffect entry = draft.Effects.First( item => item.Key == selection.Key );
            if ( entry.Source != null && entry.Source != source )
                throw new ArgumentException( "기존 선택과 JSON 원본이 다릅니다: "
                    + selection.Key + ". 원본 교체는 프로필에서 처리하세요." );
            if ( entry.Source == null && entry.Result == null )
                entry.SetSource( source, looping );

            //이전 툴이 단발로 판정한 트레일만 보정하고 실제 원본 선택은 보존
            GameObject selected = entry.Source != null ? entry.Source : entry.Prefab != null ? entry.Prefab.gameObject : null;
            if ( entry.Output == VfxOutput.World && selected != null
                && selected.GetComponentInChildren<TrailRenderer>( true ) != null )
                entry.SetLooping( true );

            entry.ImportNote( selection.Note );
            if ( entry.Note != entry.ImportedNote )
                notes.Add( "수동/기존 메모 보존: " + source.name + " / 필요 시 'JSON 메모 사용'을 선택하세요." );

            if ( selection.Usages.Length == 0 )
            {
                //빈 사용처는 수동 연결로 전환하며 다른 효과와 수동 배정은 보존
                int removed = draft.RemoveAutomaticBindings( entry.Key );
                if ( removed > 0 )
                    notes.Add( "수동 연결 전환으로 자동 배정 " + removed + "건 제거: " + source.name
                        + " / 기존 데이터의 효과 참조는 Inspector에서 해제하세요." );

                pending++;
                notes.Add( "수동 연결/연계 대기: " + source.name + " / " + entry.Note );
                continue;
            }

            foreach ( VfxSetupBinding target in targets )
            {
                string usage = target.SuggestionKey;
                VfxSetupBinding binding = draft.FindBinding( target );
                if ( binding != null && VfxSetupUtility.Includes( draft.Scope, binding ) == false )
                {
                    notes.Add( "기존 배정의 담당 범위가 달라 보존: " + usage );
                    continue;
                }
                if ( binding != null && string.IsNullOrEmpty( binding.SuggestionKey ) )
                {
                    preserved++;
                    notes.Add( "수동 배정 보존: " + target.Target.name + " / " + usage );
                    continue;
                }
                if ( binding == null )
                {
                    draft.AddBinding( target.Target, target.Field, usage, target.Looping );
                    binding = draft.FindBinding( target );
                }

                VfxSetupEffect previous = draft.Effects.FirstOrDefault( item => item.Key == binding.EffectKey );
                if ( binding.EffectKey == entry.Key )
                {
                    preserved++;
                    continue;
                }
                if ( previous != null && ( previous.Source != null || previous.Result != null ) )
                {
                    preserved++;
                    notes.Add( "기존 배정 보존: " + usage + " / " + previous.Key );
                    continue;
                }
                binding.Assign( entry.Key );
                assigned++;
            }
        }
        return $"범위 {draft.Scope} / 선택 {selections.Length}개 / 신규 배정 {assigned}건 / 기존 유지 {preserved}건 / 배정 대기 {pending}개 / 범위 밖 {outside}개\n"
            + string.Join( "\n", notes );
    }

    /// <summary>
    /// 지정 폴더 안의 프리팹 조회
    /// </summary>
    /// <param name="folder">원본 폴더</param>
    /// <param name="relativePath">프리팹 상대 경로</param>
    static GameObject LoadSource ( string folder, string relativePath )
    {
        if ( string.IsNullOrWhiteSpace( relativePath ) )
            throw new ArgumentException( "relativePath가 필요합니다." );
        string relative = relativePath.Replace( '\\', '/' );
        if ( Path.IsPathRooted( relative ) || relative.Split( '/' ).Any( part => part == ".." || part == "." )
            || relative.EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase ) == false )
            throw new ArgumentException( "폴더 내부의 프리팹 상대 경로를 지정하세요: " + relative );

        string path = folder.TrimEnd( '/' ) + "/" + relative;
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>( path );
        if ( source == null )
            throw new ArgumentException( "원본 프리팹을 찾을 수 없습니다: " + path );
        return source;
    }

    /// <summary>
    /// JSON 버전과 선택 목록
    /// </summary>
    class SelectionFile
    {
        [JsonProperty( "version", Required = Required.Always )]
        public int Version;		//형식 버전
        [JsonProperty( "selections", Required = Required.Always )]
        public Selection[] Selections;		//선택 목록
    }

    /// <summary>
    /// 원본과 배정할 사용처
    /// </summary>
    class Selection
    {
        [JsonProperty( "key", Required = Required.Always )]
        public string Key;		//선택 식별자
        [JsonProperty( "relativePath", Required = Required.Always )]
        public string RelativePath;		//프리팹 상대 경로
        [JsonProperty( "usages", Required = Required.Always )]
        public string[] Usages;		//자동 탐색 용도 키
        [JsonProperty( "note" )]
        public string Note;		//배정 참고 사항
    }
}
#endif
