#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 사운드 이벤트 일괄 설정 - 현재 무기 모션에 누락된 이벤트만 추가하는 일회성 도구
/// </summary>
public class SoundAnimationEventSetup : EditorWindow
{
    List<SoundAnimationEventEntry> _entries = new List<SoundAnimationEventEntry>( ); //조회한 클립별 추가 계획
    List<string> _errors = new List<string>( ); //조회 중 확인한 연결 오류
    bool _includeFootsteps; //달리기 발소리 이벤트 포함 여부
    Vector2 _scroll; //클립 목록 스크롤
    string _result = string.Empty; //실제 적용 진행과 결과

    /// <summary>
    /// 편집 모드에서 사용하는 일회성 이벤트 도구 열기
    /// </summary>
    [MenuItem( "Tools/Sound Animation Events (One-shot)" )]
    public static void Open ()
    {
        GetWindow<SoundAnimationEventSetup>( "사운드 이벤트 일괄 추가" );
    }

    private void OnEnable ()
    {
        minSize = new Vector2( 620f, 420f );
    }

    private void OnGUI ()
    {
        EditorGUILayout.HelpBox(
            "현재 WeaponData의 실제 Controller/Override에서 검 공격/스킬/궁극기 클립을 찾습니다. "
            + "기존 이벤트와 이미 조절한 시점은 유지하며 누락된 사운드 이벤트만 추가합니다. "
            + "동일한 클립을 쓰는 동료에도 반영됩니다. 활/스태프 발사음은 모션 이벤트를 추가하지 않습니다.", MessageType.Info );
        bool apply = false;
        using ( new EditorGUI.DisabledScope( CanEdit( ) == false ) )
        {
            EditorGUI.BeginChangeCheck( );
            _includeFootsteps = EditorGUILayout.ToggleLeft( "달리기 발소리도 추가 (음원/ID 등록은 별도)", _includeFootsteps );
            if ( EditorGUI.EndChangeCheck( ) == true )
            {
                _entries.Clear( );
                _errors.Clear( );
            }
            if ( GUILayout.Button( "현재 사용 클립 조회 / 추가 계획 만들기" ) == true )
                BuildPlan( );
            using ( new EditorGUI.DisabledScope( _entries.Count == 0 || _errors.Count > 0 ) )
                apply = GUILayout.Button( "선택한 누락 이벤트 일괄 추가 / 저장" );

            _scroll = EditorGUILayout.BeginScrollView( _scroll );
            foreach ( SoundAnimationEventEntry entry in _entries )
                DrawEntry( entry );
            foreach ( string error in _errors )
                EditorGUILayout.HelpBox( error, MessageType.Error );
            EditorGUILayout.EndScrollView( );
        }
        //GUI 레이아웃을 닫은 뒤 저장/재임포트 요청
        if ( apply == true )
            Apply( );
        if ( string.IsNullOrEmpty( _result ) == false )
            EditorGUILayout.HelpBox( _result, MessageType.Info );
    }

    /// <summary>
    /// 플레이/컴파일/임포트 중 이벤트 편집 제한
    /// </summary>
    /// <returns>편집 가능한 상태인지 여부</returns>
    static bool CanEdit ()
    {
        return EditorApplication.isPlayingOrWillChangePlaymode == false
            && EditorApplication.isCompiling == false && EditorApplication.isUpdating == false;
    }

    /// <summary>
    /// LJ 무기 데이터에서 실제 사용하는 클립만 조회
    /// </summary>
    void BuildPlan ()
    {
        _entries.Clear( );
        _errors.Clear( );
        foreach ( string guid in AssetDatabase.FindAssets( "t:WeaponData", new[] { "Assets/03. Configs/Combat/Player/Weapon" } ) )
        {
            WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>( AssetDatabase.GUIDToAssetPath( guid ) );
            RuntimeAnimatorController controller = weapon.AnimatorOverrideController;
            if ( controller == null )
            {
                _errors.Add( weapon.name + ": 모션 Controller가 없습니다." );
                continue;
            }
            if ( weapon.WeaponType == WeaponType.Sword )
            {
                AddEntry( weapon, "Attack.Attack", "AnimationSound", SoundId.SwordSwing, "AnimationAttackHit" );
                AddEntry( weapon, "Attack.Skill", "AnimationSound", SoundId.SwordSkill, "AnimationSkillHit" );
                AddEntry( weapon, "Base Layer.Ultimate", "AnimationSound", SoundId.SwordUltimate, "AnimationUltimateHit" );
            }
            if ( _includeFootsteps == true )
                AddEntry( weapon, "Base Layer.Run", "AnimationFootstep", SoundId.None, string.Empty );
        }
        if ( _entries.Count == 0 && _errors.Count == 0 )
            _errors.Add( "현재 무기 데이터에서 추가할 클립을 찾지 못했습니다." );
    }

    /// <summary>
    /// 같은 공유 클립의 중복 계획을 합치고 초기 위치를 기존 타격 시점으로 제안
    /// </summary>
    /// <param name="weapon">클립을 사용하는 무기</param>
    /// <param name="statePath">실제 Animator 상태 경로</param>
    /// <param name="function">추가할 이벤트 함수</param>
    /// <param name="id">행동음 식별자이며 발소리는 None</param>
    /// <param name="anchor">초기 위치로 사용할 기존 타격 이벤트</param>
    void AddEntry ( WeaponData weapon, string statePath, string function, SoundId id, string anchor )
    {
        AnimationClip clip = ResolveClip( weapon.AnimatorOverrideController, statePath );
        if ( clip == null )
        {
            _errors.Add( weapon.name + ": 지원하는 단일 클립이 없습니다: " + statePath );
            return;
        }
        string use = weapon.name + " / " + statePath;
        SoundAnimationEventEntry existing = _entries.Find( entry => entry.Clip == clip && entry.Function == function && entry.Id == id );
        if ( existing != null )
        {
            existing.Uses += "\n" + use;
            return;
        }
        var row = new SoundAnimationEventEntry
        {
            Clip = clip,
            Uses = use,
            Function = function,
            Id = id,
            FirstTime = function == "AnimationFootstep" ? 0.2f : 0.35f,
            SecondTime = 0.7f
        };
        if ( TryReadEvents( row, out AnimationEvent[] events, out string reason ) == false )
        {
            _errors.Add( clip.name + ": " + reason );
            return;
        }
        AnimationEvent hit = events.FirstOrDefault( item => item.functionName == anchor );
        if ( hit != null )
            row.FirstTime = IsModel( clip ) ? hit.time : hit.time / clip.length;
        _entries.Add( row );
    }

    /// <summary>
    /// Override를 반영한 실제 상태 클립 조회
    /// </summary>
    /// <param name="runtime">실제 사용 Controller</param>
    /// <param name="statePath">상태 전체 경로</param>
    /// <returns>현재 사용 클립 또는 미지원/누락 시 null</returns>
    static AnimationClip ResolveClip ( RuntimeAnimatorController runtime, string statePath )
    {
        if ( runtime is AnimatorOverrideController overrides )
        {
            AnimationClip original = ResolveClip( overrides.runtimeAnimatorController, statePath );
            return original != null ? overrides[original] ?? original : null;
        }
        if ( runtime is AnimatorController controller )
        {
            foreach ( AnimatorControllerLayer layer in controller.layers )
            {
                AnimationClip clip = FindMotion( layer.stateMachine, layer.name, statePath ) as AnimationClip;
                if ( clip != null )
                    return clip;
            }
        }
        return null;
    }

    /// <summary>
    /// 지정한 전체 상태 경로의 모션만 조회
    /// </summary>
    /// <param name="machine">조회할 상태 머신</param>
    /// <param name="prefix">상위 경로</param>
    /// <param name="statePath">요청한 전체 경로</param>
    /// <returns>해당 모션 또는 null</returns>
    static Motion FindMotion ( AnimatorStateMachine machine, string prefix, string statePath )
    {
        foreach ( ChildAnimatorState child in machine.states )
        {
            if ( prefix + "." + child.state.name == statePath )
                return child.state.motion;
        }
        foreach ( ChildAnimatorStateMachine child in machine.stateMachines )
        {
            Motion motion = FindMotion( child.stateMachine, prefix + "." + child.stateMachine.name, statePath );
            if ( motion != null )
                return motion;
        }
        return null;
    }

    /// <summary>
    /// 클립과 추가 시점 및 기존 이벤트 유지 상태 표시
    /// </summary>
    /// <param name="entry">표시할 추가 계획</param>
    void DrawEntry ( SoundAnimationEventEntry entry )
    {
        EditorGUILayout.BeginVertical( EditorStyles.helpBox );
        entry.Selected = EditorGUILayout.ToggleLeft( "이번 추가에 포함", entry.Selected );
        EditorGUILayout.LabelField( entry.Uses, EditorStyles.wordWrappedLabel );
        using ( new EditorGUI.DisabledScope( true ) )
            EditorGUILayout.ObjectField( "대상 클립", entry.Clip, typeof( AnimationClip ), false );
        EditorGUILayout.LabelField( entry.Function + ( entry.Function == "AnimationSound" ? " / Int=" + ( int ) entry.Id : " / 좌우 2회" ) );
        bool readable = TryReadEvents( entry, out AnimationEvent[] events, out string reason );
        int count = readable ? events.Count( item => Matches( item, entry ) ) : 0;
        bool conflict = readable && HasOtherSound( events, entry );
        if ( count > 0 )
        {
            string times = string.Join( ", ", events.Where( item => Matches( item, entry ) )
                .Select( item => ( IsModel( entry.Clip ) ? item.time : item.time / entry.Clip.length ).ToString( "0.###" ) ) );
            EditorGUILayout.LabelField( "기존 위치 (클립 비율)", times );
        }
        else
        {
            using ( new EditorGUI.DisabledScope( entry.Selected == false || conflict || readable == false ) )
            {
                entry.FirstTime = EditorGUILayout.Slider( "첫 위치 (클립 비율)", entry.FirstTime, 0f, 1f );
                if ( entry.Function == "AnimationFootstep" )
                    entry.SecondTime = EditorGUILayout.Slider( "두 번째 위치", entry.SecondTime, 0f, 1f );
            }
        }
        EditorGUILayout.LabelField( "상태", readable == false ? reason : conflict ? "다른 ID의 사운드 이벤트가 있어 자동 추가 불가"
            : count > 0 ? $"기존 {count}개 유지 / 재추가하지 않음" : "누락 이벤트 추가 예정" );
        if ( entry.Clip != null && GUILayout.Button( "원본 선택 / Unity에서 시점 조절" ) == true )
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath( AssetDatabase.GetAssetPath( entry.Clip ) );
        EditorGUILayout.EndVertical( );
    }

    /// <summary>
    /// FBX 임포트 설정 또는 독립 클립의 기존 이벤트 조회
    /// </summary>
    /// <param name="entry">대상 계획</param>
    /// <param name="events">기존 이벤트</param>
    /// <param name="reason">설정 오류</param>
    /// <returns>지원하는 원본에서 조회했는지 여부</returns>
    static bool TryReadEvents ( SoundAnimationEventEntry entry, out AnimationEvent[] events, out string reason )
    {
        events = Array.Empty<AnimationEvent>( );
        reason = string.Empty;
        if ( entry.Clip == null || entry.Clip.length <= 0f )
            reason = "클립이 없거나 길이가 0입니다. 다시 조회하세요.";
        else
        {
            string path = AssetDatabase.GetAssetPath( entry.Clip );
            if ( path.StartsWith( "Assets/", StringComparison.Ordinal ) == false )
                reason = "Assets 내부 클립만 편집합니다.";
            else if ( AssetImporter.GetAtPath( path ) is ModelImporter importer )
            {
                ModelImporterClipAnimation[] matches = ImportClips( importer ).Where( clip => clip.name == entry.Clip.name ).ToArray( );
                if ( matches.Length != 1 )
                    reason = "임포트 클립 이름이 없거나 중복됩니다. 원본에서 직접 확인하세요.";
                else
                    events = matches[0].events ?? Array.Empty<AnimationEvent>( );
            }
            else if ( path.EndsWith( ".anim", StringComparison.OrdinalIgnoreCase ) )
                events = AnimationUtility.GetAnimationEvents( entry.Clip );
            else
                reason = "모델 내장 클립 또는 .anim만 지원합니다.";
        }
        return string.IsNullOrEmpty( reason );
    }

    /// <summary>
    /// 사용자 지정 클립이 없는 모델은 기본 임포트 클립을 기준으로 사용
    /// </summary>
    /// <param name="importer">대상 모델 임포터</param>
    /// <returns>보존할 전체 임포트 클립 설정</returns>
    static ModelImporterClipAnimation[] ImportClips ( ModelImporter importer )
    {
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        return clips.Length > 0 ? clips : importer.defaultClipAnimations;
    }

    /// <summary>
    /// 모델 임포트의 정규화 시간 사용 여부 확인
    /// </summary>
    /// <param name="clip">대상 클립</param>
    /// <returns>모델 내장 클립인지 여부</returns>
    static bool IsModel ( AnimationClip clip )
    {
        return AssetImporter.GetAtPath( AssetDatabase.GetAssetPath( clip ) ) is ModelImporter;
    }

    /// <summary>
    /// 같은 역할의 기존 이벤트 판별
    /// </summary>
    /// <param name="item">기존 이벤트</param>
    /// <param name="entry">추가할 역할</param>
    /// <returns>이미 같은 역할이 존재하는지 여부</returns>
    static bool Matches ( AnimationEvent item, SoundAnimationEventEntry entry )
    {
        return item.functionName == entry.Function && ( entry.Function != "AnimationSound" || item.intParameter == ( int ) entry.Id );
    }

    /// <summary>
    /// 같은 클립에서 다른 사운드 ID를 겹쳐 추가하는 상황 확인
    /// </summary>
    /// <param name="events">기존 이벤트</param>
    /// <param name="entry">추가할 역할</param>
    /// <returns>다른 행동음이 이미 존재하는지 여부</returns>
    static bool HasOtherSound ( AnimationEvent[] events, SoundAnimationEventEntry entry )
    {
        return entry.Function == "AnimationSound"
            && events.Any( item => item.functionName == entry.Function && item.intParameter != ( int ) entry.Id );
    }

    /// <summary>
    /// 전체 선택을 먼저 검사한 뒤 누락된 이벤트만 원본별로 저장
    /// </summary>
    void Apply ()
    {
        if ( CanEdit( ) == false )
            return;
        var pending = new List<SoundAnimationEventEntry>( );
        foreach ( SoundAnimationEventEntry entry in _entries.Where( row => row.Selected ) )
        {
            if ( TryReadEvents( entry, out AnimationEvent[] events, out string reason ) == false )
            {
                _result = "적용하지 않았습니다: " + entry.Uses + " / " + reason;
                return;
            }
            if ( events.Any( item => Matches( item, entry ) ) )
                continue;
            if ( HasOtherSound( events, entry ) == true )
            {
                _result = "적용하지 않았습니다: " + entry.Uses + " / 다른 사운드 ID와 충돌합니다.";
                return;
            }
            if ( entry.Function == "AnimationFootstep" && entry.FirstTime >= entry.SecondTime )
            {
                _result = entry.Uses + ": 두 번째 발 디딤 위치를 첫 번째보다 뒤로 지정하세요.";
                return;
            }
            string path = AssetDatabase.GetAssetPath( entry.Clip );
            UnityEngine.Object owner = IsModel( entry.Clip ) ? ( UnityEngine.Object ) AssetImporter.GetAtPath( path ) : entry.Clip;
            if ( EditorUtility.IsDirty( owner ) == true )
            {
                _result = "원본의 미저장 편집을 먼저 저장하거나 되돌려 주세요: " + path;
                return;
            }
            pending.Add( entry );
        }
        bool sharedConflict = pending.Where( entry => entry.Function == "AnimationSound" )
            .GroupBy( entry => entry.Clip ).Any( group => group.Select( entry => entry.Id ).Distinct( ).Count( ) > 1 );
        if ( sharedConflict == true )
        {
            _result = "같은 공유 클립에 서로 다른 사운드 ID가 요청됐습니다. 적용할 역할 하나만 선택하거나 모션 공유 구성을 먼저 확인하세요.";
            return;
        }
        if ( pending.Count == 0 )
        {
            _result = "추가할 누락 이벤트가 없습니다. 기존 이벤트와 조절한 위치는 유지했습니다.";
            return;
        }
        if ( EditorUtility.DisplayDialog( "사운드 이벤트 일괄 추가",
            $"선택한 {pending.Count}개 역할의 누락 이벤트를 저장합니다.\n기존 타격/종료/사운드 이벤트는 유지합니다. "
            + "공유 클립의 모든 사용처에 반영되며 FBX 임포트 설정 저장은 Undo로 되돌릴 수 없습니다.\n시점은 적용 후 원본의 Events에서 조절하세요.", "추가 / 저장", "취소" ) == false )
            return;

        int saved = 0;
        foreach ( var group in pending.GroupBy( entry => AssetDatabase.GetAssetPath( entry.Clip ) ).ToArray( ) )
        {
            SoundAnimationEventEntry[] rows = group.ToArray( );
            string[] names = rows.Select( entry => entry.Clip.name ).ToArray( );
            //재임포트 예외가 발생해도 전체 성공으로 표시하지 않고 마지막 저장 요청과 완료 수 유지
            _result = $"완료 {saved}개 파일 / 저장 요청 중: {group.Key}. 중단되면 Console과 해당 원본을 확인하세요.";
            if ( AssetImporter.GetAtPath( group.Key ) is ModelImporter importer )
            {
                ModelImporterClipAnimation[] clips = ImportClips( importer );
                foreach ( SoundAnimationEventEntry entry in group )
                {
                    ModelImporterClipAnimation clip = clips.Single( item => item.name == entry.Clip.name );
                    clip.events = AppendEvents( clip.events ?? Array.Empty<AnimationEvent>( ), entry, 1f );
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport( );
            }
            else
            {
                AnimationClip clip = group.First( ).Clip;
                AnimationEvent[] events = AnimationUtility.GetAnimationEvents( clip );
                foreach ( SoundAnimationEventEntry entry in group )
                    events = AppendEvents( events, entry, clip.length );
                Undo.RecordObject( clip, "사운드 이벤트 일괄 추가" );
                AnimationUtility.SetAnimationEvents( clip, events );
                EditorUtility.SetDirty( clip );
                AssetDatabase.SaveAssetIfDirty( clip );
            }
            AnimationClip[] savedClips = AssetDatabase.LoadAllAssetsAtPath( group.Key ).OfType<AnimationClip>( ).ToArray( );
            for ( int i = 0; i < rows.Length; i++ )
            {
                AnimationClip[] matches = savedClips.Where( clip => clip.name == names[i] ).ToArray( );
                if ( matches.Length != 1 || HasSavedEvents( matches[0], rows[i] ) == false )
                {
                    _result = $"확인 완료 {saved}개 파일 / 저장 후 이벤트 확인 실패: {group.Key}, {names[i]}. 일부 저장됐을 수 있으니 Console과 원본을 확인하세요.";
                    return;
                }
            }
            saved++;
        }
        BuildPlan( );
        _result = $"{saved}개 원본 파일에 누락 이벤트를 추가했습니다. 원본의 Events에서 시점을 조절하세요. 실제 소리 출력은 별도 검증 대상입니다.";
    }

    /// <summary>
    /// 저장/재임포트한 실제 클립에서 추가된 역할과 초 단위 위치 확인
    /// </summary>
    /// <param name="clip">저장 후 다시 조회한 클립</param>
    /// <param name="entry">이번 추가 계획</param>
    /// <returns>새 이벤트의 개수와 위치가 일치하는지 여부</returns>
    static bool HasSavedEvents ( AnimationClip clip, SoundAnimationEventEntry entry )
    {
        AnimationEvent[] events = AnimationUtility.GetAnimationEvents( clip ).Where( item => Matches( item, entry ) ).ToArray( );
        int expected = entry.Function == "AnimationFootstep" ? 2 : 1;
        return events.Length == expected
            && events.Any( item => Mathf.Approximately( item.time, entry.FirstTime * clip.length ) )
            && ( expected == 1 || events.Any( item => Mathf.Approximately( item.time, entry.SecondTime * clip.length ) ) );
    }

    /// <summary>
    /// 기존 이벤트 배열을 유지하며 필요한 사운드 이벤트만 추가
    /// </summary>
    /// <param name="events">기존 이벤트</param>
    /// <param name="entry">추가 계획</param>
    /// <param name="duration">모델은 1, 독립 클립은 실제 초 단위 길이</param>
    /// <returns>추가한 이벤트 배열</returns>
    static AnimationEvent[] AppendEvents ( AnimationEvent[] events, SoundAnimationEventEntry entry, float duration )
    {
        if ( events.Any( item => Matches( item, entry ) ) )
            return events;
        var result = new List<AnimationEvent>( events );
        result.Add( new AnimationEvent { functionName = entry.Function, intParameter = ( int ) entry.Id, time = entry.FirstTime * duration } );
        if ( entry.Function == "AnimationFootstep" )
            result.Add( new AnimationEvent { functionName = entry.Function, time = entry.SecondTime * duration } );
        return result.OrderBy( item => item.time ).ToArray( );
    }
}

/// <summary>
/// 사운드 이벤트 계획 - 같은 공유 클립의 역할과 추가 위치 보관
/// </summary>
class SoundAnimationEventEntry
{
    public AnimationClip Clip; //변경할 원본 클립
    public string Uses; //현재 무기의 사용 상태 경로
    public string Function; //추가할 이벤트 함수
    public SoundId Id; //행동음 식별자
    public float FirstTime; //첫 번째 정규화 위치
    public float SecondTime; //두 번째 발 디딤 정규화 위치
    public bool Selected = true; //이번 적용에 포함할지 여부
}
#endif
