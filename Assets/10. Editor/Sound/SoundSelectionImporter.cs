#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 사운드 선택 가져오기 - 선택 JSON을 프로젝트 음원과 배정 초안으로 변환
/// </summary>
public static class SoundSelectionImporter
{
    /// <summary>
    /// 원본 경로 대신 지정한 프로젝트 폴더에서 선택 음원을 찾아 병합
    /// </summary>
    /// <param name="profile">반영할 편집 프로필</param>
    /// <param name="jsonPath">선택 JSON 경로</param>
    /// <returns>추가 항목과 수동 확인 대상 안내</returns>
    public static string Import ( SoundSetupProfile profile, string jsonPath )
    {
        if ( profile == null || EditorUtility.IsPersistent( profile ) == false )
            throw new ArgumentException( "프로젝트에 저장한 사운드 설정 프로필이 필요합니다." );
        if ( SoundSetupUtility.CanEdit( ) == false )
            return "가져오지 않았습니다. 플레이/컴파일/임포트가 끝난 뒤 다시 요청하세요.";
        string folder = AssetDatabase.GetAssetPath( profile.AudioFolder );
        if ( folder.StartsWith( "Assets/", StringComparison.Ordinal ) == false
            || AssetDatabase.IsValidFolder( folder ) == false )
            throw new ArgumentException( "프로필의 음원 폴더 참조가 없거나 유효하지 않습니다. "
                + "음원 파일이 들어 있는 폴더를 프로젝트 창에서 음원 폴더 필드에 다시 지정하세요." );

        JObject root = JObject.Parse( File.ReadAllText( jsonPath ) );
        JArray selectionArray = root["selections"] as JArray;
        if ( selectionArray == null || selectionArray.Count == 0 )
            throw new ArgumentException( "selections가 있는 선택 경로 내보내기 JSON이 필요합니다." );
        SoundSelection[] selections = selectionArray.Select( token => new SoundSelection( token ) ).ToArray( );

        var clips = AssetDatabase.FindAssets( "t:AudioClip", new[] { folder } )
            .Select( AssetDatabase.GUIDToAssetPath )
            .GroupBy( Path.GetFileName, StringComparer.OrdinalIgnoreCase )
            .ToDictionary( group => group.Key, group => group.ToArray( ), StringComparer.OrdinalIgnoreCase );
        //이전 키의 마지막 ID 부분을 제외하고 같은 선택의 모든 배정을 먼저 보존
        var existing = profile.Entries.Where( entry => string.IsNullOrEmpty( entry.SelectionKey ) == false
                && entry.SelectionKey.LastIndexOf( '|' ) > 0 )
            .GroupBy( entry => entry.SelectionKey.Substring( 0, entry.SelectionKey.LastIndexOf( '|' ) + 1 ),
                StringComparer.OrdinalIgnoreCase )
            .ToDictionary( group => group.Key, group => group.ToArray( ), StringComparer.OrdinalIgnoreCase );
        var selected = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
        var additions = new List<SoundSetupEntry>( );
        var restorations = new Dictionary<SoundSetupEntry, AudioClip>( );
        int preserved = 0;
        int unassigned = 0;
        int missing = 0;

        foreach ( SoundSelection selection in selections )
        {
            if ( string.IsNullOrWhiteSpace( selection.Group ) || string.IsNullOrWhiteSpace( selection.RelativePath ) )
                throw new ArgumentException( "선택 항목의 group 또는 relativePath가 비어 있습니다." );

            string name = Path.GetFileName( selection.RelativePath.Replace( '\\', '/' ) );
            string prefix = selection.Group + "|" + name + "|";
            AudioClip clip = clips.TryGetValue( name, out string[] matches ) && matches.Length == 1
                ? AssetDatabase.LoadAssetAtPath<AudioClip>( matches[0] ) : null;
            if ( existing.TryGetValue( prefix, out SoundSetupEntry[] previous ) )
            {
                //수동 ID/클립/음량/적용 선택과 같은 음원의 복수 역할 배정을 그대로 유지
                if ( selected.Add( prefix ) == true )
                {
                    preserved += previous.Length;
                    foreach ( SoundSetupEntry missingEntry in previous.Where( candidate => candidate.Clip == null ) )
                    {
                        if ( clip != null )
                            restorations.Add( missingEntry, clip );
                        else
                            missing++;
                    }
                }
                continue;
            }

            string note = clip != null ? string.Empty
                : $"음원 누락 또는 동명 파일 중복: {name}. AudioClip을 직접 지정하세요. ";
            bool assigned = Enum.TryParse( selection.SoundId, out SoundId id )
                && Enum.IsDefined( typeof( SoundId ), id ) && id != SoundId.None;
            if ( assigned == false )
            {
                id = SoundId.None;
            }
            string key = prefix + ( int ) id;
            if ( selected.Add( key ) == false )
                continue;
            if ( clip == null )
                missing++;
            if ( assigned == false )
                unassigned++;
            SoundType type = GetSoundType( id );
            note += assigned
                ? "JSON의 soundId로 배정했습니다. 실제 호출/출력은 별도로 확인하세요."
                : "유효한 soundId가 없습니다. 파일명/그룹으로 추정하지 않으므로 ID와 종류를 직접 지정하세요.";
            var newEntry = new SoundSetupEntry(
                key, selection.Group + " / " + selection.Purpose + " / " + name,
                clip, note, type == SoundType.LoopSfx );
            newEntry.SetRegistration( id, type, assigned );
            additions.Add( newEntry );
        }

        //전체 JSON 검증 후 한 번에 반영하여 실패 시 부분 복구 방지
        if ( additions.Count > 0 || restorations.Count > 0 )
        {
            Undo.RecordObject( profile, "사운드 선택 가져오기" );
            foreach ( var restoration in restorations )
                restoration.Key.RestoreMissingClip( restoration.Value );
            profile.AddEntries( additions );
            EditorUtility.SetDirty( profile );
        }
        return $"음원 폴더: {folder}. 선택 {selections.Length}건: 신규 {additions.Count}행, 기존 배정 유지 {preserved}행, "
            + $"미싱 복구 {restorations.Count}행, 음원 누락/동명 중복 {missing}행, 신규 ID 미배정 {unassigned}행. "
            + "그룹/파일명 기반 자동 배정은 사용하지 않습니다. 프로필을 저장하고 검사하세요.";
    }

    /// <summary>
    /// 재생 경로에 맞는 초기 출력 종류 선택
    /// </summary>
    /// <param name="id">등록할 식별자</param>
    /// <returns>초기 출력 종류</returns>
    public static SoundType GetSoundType ( SoundId id )
    {
        if ( id == SoundId.TitleBgm || id == SoundId.PlayBgm )
            return SoundType.Bgm;
        if ( id == SoundId.FireTrapLoop )
            return SoundType.LoopSfx;
        if ( id == SoundId.HeroFootstep || id == SoundId.CompanionFootstep )
            return SoundType.Footstep;
        switch ( id )
        {
            case SoundId.UiClick:
            case SoundId.UiBack:
            case SoundId.UiNotice:
            case SoundId.UiReject:
            case SoundId.UiEquip:
            case SoundId.UiPage:
            case SoundId.ShopPurchase:
            case SoundId.UpgradeSuccess:
            case SoundId.MissionReward:
                return SoundType.Ui;
            default:
                return SoundType.Sfx;
        }
    }


    /// <summary>
    /// 선택 JSON 항목 - 외부 키를 읽어 변환한 조회 전용 값
    /// </summary>
    class SoundSelection
    {
        string _group; //선택 그룹
        string _purpose; //원래 용도
        string _soundId; //실제 ID 또는 설명 문자열
        string _relativePath; //파일 이름 확인에만 사용하는 원본 상대 경로

        /// <summary>
        /// 선택 그룹
        /// </summary>
        public string Group => _group;

        /// <summary>
        /// 원래 용도
        /// </summary>
        public string Purpose => _purpose;

        /// <summary>
        /// 실제 ID 또는 설명 문자열
        /// </summary>
        public string SoundId => _soundId;

        /// <summary>
        /// 파일 이름 확인에만 사용하는 원본 상대 경로
        /// </summary>
        public string RelativePath => _relativePath;

        /// <summary>
        /// 기존 내보내기 키를 유지하면서 JSON 객체를 조회 값으로 변환
        /// </summary>
        /// <param name="token">선택 배열의 JSON 항목</param>
        public SoundSelection ( JToken token )
        {
            JObject item = token as JObject;
            if ( item == null )
                throw new ArgumentException( "선택 배열에는 JSON 객체만 지정할 수 있습니다." );
            foreach ( string key in new[] { "group", "purpose", "soundId", "relativePath" } )
            {
                JToken value;
                if ( item.TryGetValue( key, out value ) == true
                    && value.Type != JTokenType.String && value.Type != JTokenType.Null )
                    throw new ArgumentException( "선택 항목은 문자열이어야 합니다: " + key );
            }
            _group = item.Value<string>( "group" );
            _purpose = item.Value<string>( "purpose" );
            _soundId = item.Value<string>( "soundId" );
            _relativePath = item.Value<string>( "relativePath" );
        }
    }
}
#endif
