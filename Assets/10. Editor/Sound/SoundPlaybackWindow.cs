#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using LJPlay;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 사운드 디버그 인스펙터 - 전체 캐릭터 출력 조회와 선택 대상의 실제 행동 요청
/// </summary>
[CustomEditor( typeof( SoundDebug ) )]
public class SoundPlaybackWindow : Editor
{
    PlayScene[] _scenes = Array.Empty<PlayScene>( ); //현재 로드된 플레이 대상
    PlayScene _scene; //검증할 실제 플레이 씬 연결부
    Hero[] _heroes = Array.Empty<Hero>( ); //로드된 모든 플레이어
    Companion[] _companions = Array.Empty<Companion>( ); //로드된 모든 동료
    SoundPlayer[] _otherPlayers = Array.Empty<SoundPlayer>( ); //적 등을 포함한 나머지 캐릭터 출력
    SoundLoopPlayer[] _loops = Array.Empty<SoundLoopPlayer>( ); //로드된 반복음 출력
    Hero _hero; //현재 행동을 검증할 플레이어
    Companion _companion; //현재 연결을 검증할 동료
    SoundPlayer _otherPlayer; //현재 연결을 조회할 일반 출력
    SoundManager _manager; //현재 게임이 소유하는 공통 서비스
    SoundDataMap _map; //실행 중 관리자에 연결된 맵
    Dictionary<SoundId, SoundData> _sounds = new Dictionary<SoundId, SoundData>( ); //검증할 등록 데이터
    PauseMenuController[] _pauseMenus = Array.Empty<PauseMenuController>( ); //현재 로드된 메뉴의 정지 상태
    string _motionResult = string.Empty; //사용자가 요청한 모션 이벤트 검사 결과
    string _result = string.Empty; //마지막 요청의 접수 결과
    string _mapError = string.Empty; //실행 맵의 구성 오류
    double _nextRefresh; //전체 대상 탐색 간격
    VolumeSettings _volumeSettings; //현재 실행의 볼륨 설정
    float _masterVolume = 1f; //검증할 마스터 볼륨
    float _bgmVolume = 1f; //검증할 배경음 볼륨
    float _sfxVolume = 1f; //검증할 효과음 볼륨
    bool _masterMuted; //마스터 음소거 선택
    bool _bgmMuted; //배경음 음소거 선택
    bool _sfxMuted; //효과음 음소거 선택
    bool _showVolumes; //볼륨 편집 영역 표시
    bool _showRequests = true; //실제 재생 요청과 출력 상태 표시
    Vector2 _requestScroll; //복사 가능한 요청 기록 스크롤
    bool _isPlaySession; //플레이 종료 전환 중 조회 방지

    private void OnEnable ()
    {
        EditorApplication.playModeStateChanged += HandlePlayMode;
        _isPlaySession = Application.isPlaying;
        _nextRefresh = 0d;
    }

    /// <summary>
    /// 상위 관리자의 검증 대상을 조회하며 게임 초기화와 시간 진행은 호출하지 않음
    /// </summary>
    void Refresh ()
    {
        if ( EditorApplication.timeSinceStartup < _nextRefresh )
            return;
        _nextRefresh = EditorApplication.timeSinceStartup + 0.25d;

        SoundManager manager = ( ( SoundDebug ) target ).GetComponentInParent<SoundManager>( true );
        if ( _manager != manager )
        {
            ClearConnection( );
            _manager = manager;
        }
        _scenes = UnityEngine.Object.FindObjectsByType<PlayScene>( FindObjectsInactive.Exclude, FindObjectsSortMode.None )
            .Where( scene => scene.gameObject.scene.IsValid( ) && scene.gameObject.scene.isLoaded ).ToArray( );
        _heroes = UnityEngine.Object.FindObjectsByType<Hero>( FindObjectsInactive.Include, FindObjectsSortMode.InstanceID )
            .Where( hero => hero.gameObject.scene.IsValid( ) && hero.gameObject.scene.isLoaded ).ToArray( );
        _companions = UnityEngine.Object.FindObjectsByType<Companion>( FindObjectsInactive.Include, FindObjectsSortMode.InstanceID )
            .Where( companion => companion.gameObject.scene.IsValid( ) && companion.gameObject.scene.isLoaded ).ToArray( );
        _otherPlayers = UnityEngine.Object.FindObjectsByType<SoundPlayer>( FindObjectsInactive.Include, FindObjectsSortMode.InstanceID )
            .Where( player => player.gameObject.scene.IsValid( ) && player.gameObject.scene.isLoaded
                && player.GetComponent<Hero>( ) == null && player.GetComponent<Companion>( ) == null ).ToArray( );
        _loops = UnityEngine.Object.FindObjectsByType<SoundLoopPlayer>( FindObjectsInactive.Include, FindObjectsSortMode.InstanceID )
            .Where( loop => loop.gameObject.scene.IsValid( ) && loop.gameObject.scene.isLoaded ).ToArray( );
        _pauseMenus = UnityEngine.Object.FindObjectsByType<PauseMenuController>( FindObjectsInactive.Include, FindObjectsSortMode.None );
        Hero previous = _hero;
        Companion previousCompanion = _companion;
        SoundPlayer previousOtherPlayer = _otherPlayer;
        if ( _heroes.Contains( _hero ) == false )
            _hero = null;
        if ( _companions.Contains( _companion ) == false )
            _companion = null;
        if ( _otherPlayers.Contains( _otherPlayer ) == false )
            _otherPlayer = null;
        if ( _hero == null && _companion == null && _otherPlayer == null && _heroes.Length == 1 )
            _hero = _heroes[0];
        UpdateSelectedScene( );
        if ( previous != _hero || previousCompanion != _companion || previousOtherPlayer != _otherPlayer )
        {
            _result = string.Empty;
            _motionResult = string.Empty;
        }
        SoundDataMap map = _manager != null ? ReadReference( _manager, "_soundDataMap" ) as SoundDataMap : null;
        if ( _map != map )
        {
            _map = map;
            _sounds.Clear( );
            _mapError = string.Empty;
            if ( _map != null && _map.TryBuild( out _sounds ) == false )
            {
                _sounds.Clear( );
                _mapError = "맵 에셋 검사 실패. Console의 ID/음원 오류를 확인하세요.";
            }
        }
    }

    /// <summary>
    /// 선택한 플레이어를 실제로 소유하는 단일 플레이 씬만 연결
    /// </summary>
    void UpdateSelectedScene ()
    {
        PlayScene[] matches = _hero != null ? _scenes.Where( scene => scene != null && scene.Hero == _hero ).ToArray( )
            : Array.Empty<PlayScene>( );
        _scene = matches.Length == 1 ? matches[0] : null;
    }

    /// <summary>
    /// 전체 대상 목록과 선택 대상 조작 및 공통 오디오 제어 표시
    /// </summary>
    public override void OnInspectorGUI ()
    {
        DrawDefaultInspector( );
        SoundDebug debug = ( SoundDebug ) target;
        if ( _isPlaySession == false || Application.isPlaying == false
            || debug.gameObject.scene.IsValid( ) == false || debug.gameObject.scene.isLoaded == false )
        {
            SoundManager parent = debug.GetComponentInParent<SoundManager>( true );
            EditorGUILayout.HelpBox( parent != null
                ? "플레이 모드의 Hierarchy에서 이 오브젝트를 선택하면 전체 플레이어/동료/반복음 상태와 선택 대상의 실제 행동을 검증합니다."
                : "SoundManager 하위 오브젝트에 SoundDebug를 붙여 주세요.", MessageType.Info );
            return;
        }
        if ( GUILayout.Button( "대상과 맵 에셋 다시 조회" ) == true )
        {
            _map = null;
            _nextRefresh = 0d;
        }
        Refresh( );
        EditorGUILayout.LabelField( "상위 사운드 관리자", _manager != null ? _manager.name : "미연결" );
        if ( _manager == null )
        {
            EditorGUILayout.HelpBox( "상위 SoundManager가 없습니다. 관리자의 하위 오브젝트에 배치해 주세요.", MessageType.Warning );
            return;
        }
        DrawTargets( );
        if ( _hero != null )
            DrawActions( );
        else if ( _companion != null )
            DrawCompanionConnections( _companion );
        DrawAudioStatus( );
        DrawRequests( );
        DrawAudioControls( );
        DrawVolumeSettings( );
        if ( string.IsNullOrEmpty( _result ) == false )
            EditorGUILayout.HelpBox( _result, MessageType.Info );
    }

    /// <summary>
    /// 모든 플레이어와 동료의 상태를 한눈에 표시하며 선택은 인스펙터를 유지
    /// </summary>
    void DrawTargets ()
    {
        EditorGUILayout.LabelField( $"플레이어 {_heroes.Length}명", EditorStyles.boldLabel );
        foreach ( Hero hero in _heroes )
        {
            if ( hero == null )
                continue;
            EditorGUILayout.BeginHorizontal( );
            bool selected = GUILayout.Toggle( _hero == hero, "검증", "Button", GUILayout.Width( 45f ) );
            using ( new EditorGUI.DisabledScope( true ) )
                EditorGUILayout.ObjectField( hero, typeof( Hero ), true );
            EditorGUILayout.EndHorizontal( );
            SoundPlayer player = hero.GetComponent<SoundPlayer>( );
            EditorGUILayout.LabelField(
                $"{hero.gameObject.scene.name} / 활성={hero.isActiveAndEnabled} / 서비스={player != null && player.IsConnectedTo( _manager )}",
                EditorStyles.wordWrappedLabel );
            if ( selected == true && _hero != hero )
            {
                _hero = hero;
                _companion = null;
                _otherPlayer = null;
                UpdateSelectedScene( );
                _result = string.Empty;
                _motionResult = string.Empty;
            }
        }
        if ( _heroes.Length > 1 && _hero == null && _companion == null && _otherPlayer == null )
            EditorGUILayout.HelpBox( "조작할 플레이어의 검증 버튼을 선택하세요. 전체 대상에 일괄 요청하지 않습니다.", MessageType.Info );

        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( $"동료 {_companions.Length}명", EditorStyles.boldLabel );
        foreach ( Companion companion in _companions )
        {
            if ( companion == null )
                continue;
            EditorGUILayout.BeginHorizontal( );
            bool selected = GUILayout.Toggle( _companion == companion, "검증", "Button", GUILayout.Width( 45f ) );
            using ( new EditorGUI.DisabledScope( true ) )
                EditorGUILayout.ObjectField( companion, typeof( Companion ), true );
            EditorGUILayout.EndHorizontal( );
            SoundPlayer player = companion.GetComponent<SoundPlayer>( );
            EditorGUILayout.LabelField(
                $"{companion.gameObject.scene.name} / 활성={companion.isActiveAndEnabled} / 초기화={companion.IsInitialized} / 서비스={player != null && player.IsConnectedTo( _manager )}",
                EditorStyles.wordWrappedLabel );
            if ( selected == true && _companion != companion )
            {
                _hero = null;
                _companion = companion;
                _otherPlayer = null;
                _scene = null;
                _result = string.Empty;
                _motionResult = string.Empty;
            }
        }
        if ( _otherPlayers.Length > 0 )
        {
            EditorGUILayout.Space( );
            EditorGUILayout.LabelField( $"기타 사운드 출력 {_otherPlayers.Length}개 / 적 등", EditorStyles.boldLabel );
            foreach ( SoundPlayer player in _otherPlayers )
            {
                if ( player == null )
                    continue;
                EditorGUILayout.BeginHorizontal( );
                bool selected = GUILayout.Toggle( _otherPlayer == player, "조회", "Button", GUILayout.Width( 45f ) );
                using ( new EditorGUI.DisabledScope( true ) )
                    EditorGUILayout.ObjectField( player, typeof( SoundPlayer ), true );
                EditorGUILayout.EndHorizontal( );
                EditorGUILayout.LabelField( $"{player.gameObject.scene.name} / 활성={player.isActiveAndEnabled} / 서비스={player.IsConnectedTo( _manager )}",
                    EditorStyles.wordWrappedLabel );
                if ( selected == true && _otherPlayer != player )
                {
                    _otherPlayer = player;
                    _hero = null;
                    _companion = null;
                    _scene = null;
                    _result = string.Empty;
                    _motionResult = string.Empty;
                }
            }
        }
        if ( _loops.Length > 0 )
        {
            EditorGUILayout.Space( );
            EditorGUILayout.LabelField( $"반복음 출력 {_loops.Length}개", EditorStyles.boldLabel );
            foreach ( SoundLoopPlayer loop in _loops )
            {
                if ( loop == null )
                    continue;
                using ( new EditorGUI.DisabledScope( true ) )
                    EditorGUILayout.ObjectField( loop, typeof( SoundLoopPlayer ), true );
                EditorGUILayout.LabelField(
                    $"활성={loop.isActiveAndEnabled} / 서비스={loop.Service == _manager} / 요청 있음={loop.HasRequest}",
                    EditorStyles.wordWrappedLabel );
            }
        }
    }

    /// <summary>
    /// 관리자와 맵 에셋의 상태를 행동 요청과 별도로 표시
    /// </summary>
    void DrawAudioStatus ()
    {
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "오디오 상태", EditorStyles.boldLabel );
        if ( _manager == null )
            EditorGUILayout.HelpBox( "현재 게임의 사운드 서비스 연결을 기다립니다.", MessageType.Info );
        else
        {
            EditorGUILayout.LabelField( "관리자", $"초기화={_manager.IsInitialized} / 재생 준비={_manager.IsReady}" );
            EditorGUILayout.LabelField( "BGM 전환", _manager.IsBgmFading ? "진행 중" : "없음" );
        }
        GameManager game = GameManager.Instance;
        if ( game != null && game.Sound == _manager && game.Settings != null )
        {
            VolumeSetting volume = game.Settings.Volume.Settings;
            EditorGUILayout.LabelField( "복원/마지막 적용 볼륨", $"마스터 {volume.Master} / BGM {volume.Bgm} / SFX {volume.Sfx}" );
        }
        if ( _map == null )
            EditorGUILayout.HelpBox( "관리자의 사운드 데이터 맵이 연결되지 않았습니다.", MessageType.Warning );
        if ( string.IsNullOrEmpty( _mapError ) == false )
            EditorGUILayout.HelpBox( _mapError, MessageType.Warning );

        int listeners = UnityEngine.Object.FindObjectsByType<AudioListener>( FindObjectsInactive.Exclude, FindObjectsSortMode.None )
            .Count( listener => listener.isActiveAndEnabled );
        if ( listeners != 1 )
            EditorGUILayout.HelpBox( $"활성 AudioListener가 {listeners}개입니다. 실제 청취에 사용할 출력을 확인하세요.", MessageType.Warning );
        EditorGUILayout.LabelField( "정지 상태",
            $"에디터={EditorApplication.isPaused} / 게임 시간={Time.timeScale:0.##} / 오디오={AudioListener.pause}" );
        EditorGUILayout.HelpBox(
            "아래 연결 진단은 맵 에셋 기준입니다. 실행 중 관리자의 조회표를 갱신하지 않습니다. "
            + "설정을 변경했다면 플레이를 다시 시작하세요. 요청 승인은 실제 출력/청취 확인과 구분합니다.", MessageType.Info );
    }

    /// <summary>
    /// 선택 대상과 공통 출력의 보고서 구성
    /// </summary>
    /// <returns>현재 요청 기록과 출력 상태</returns>
    string BuildReport ()
    {
        var lines = new List<string>( );
        Component actor = _hero != null ? ( Component ) _hero : ( _companion != null ? ( Component ) _companion : _otherPlayer );
        SoundPlayer player = actor != null ? actor.GetComponent<SoundPlayer>( ) : null;
        if ( actor != null )
        {
            lines.Add( "[선택 캐릭터] " + actor.name );
            if ( player == null )
                lines.Add( "SoundPlayer 없음" );
            else
            {
                lines.Add( "현재 서비스 연결=" + player.IsConnectedTo( _manager ) );
                lines.Add( DescribeOutput( "현재 발소리 출력", player.DebugFootsteps ) );
                lines.Add( DescribeOutput( "현재 행동음 출력", player.DebugEffects ) );
                lines.AddRange( player.DebugRequests.OrderBy( item => ( int ) item.Key )
                    .Select( item => $"{item.Key} ({( int ) item.Key}): {item.Value}" ) );
                if ( player.DebugRequests.Count == 0 )
                    lines.Add( "기록 없음: 이번 연결 이후 SoundPlayer.Play에 도달한 요청이 없습니다." );
            }
        }
        else
            lines.Add( "캐릭터별 기록은 플레이어/동료 또는 기타 사운드 출력을 선택하세요." );
        lines.Add( "[공통 BGM/UI/효과음 요청 / 상위 관리자 전체]" );
        lines.Add( DescribeOutput( "현재 UI 출력", _manager.DebugUiSource ) );
        lines.AddRange( _manager.DebugCommonRequests.OrderBy( item => ( int ) item.Key )
            .Select( item => $"{item.Key} ({( int ) item.Key}): {item.Value}" ) );
        if ( _manager.DebugCommonRequests.Count == 0 )
            lines.Add( "기록 없음: SoundManager.Play에 도달한 요청이 없습니다." );
        lines.Add( "[월드 요청 / 상위 관리자 전체]" );
        lines.AddRange( _manager.DebugWorldRequests.OrderBy( item => ( int ) item.Key )
            .Select( item => $"{item.Key} ({( int ) item.Key}): {item.Value}" ) );
        if ( _manager.DebugWorldRequests.Count == 0 )
            lines.Add( "기록 없음: PlayAt에 도달한 요청이 없습니다." );
        IReadOnlyList<AudioSource> sources = _manager.DebugWorldSources;
        if ( sources != null )
        {
            lines.Add( "[현재 남아 있는 월드 출력] " + sources.Count + "개" );
            foreach ( AudioSource source in sources )
                lines.Add( DescribeOutput( source != null && source.clip != null ? source.clip.name : "월드 출력", source ) );
        }
        return string.Join( "\n", lines );
    }

    /// <summary>
    /// 요청 보고서 표시와 복사 및 콘솔 출력
    /// </summary>
    void DrawRequests ()
    {
        _showRequests = EditorGUILayout.Foldout( _showRequests, "실제 사운드 요청 / 출력 상태", true );
        if ( _showRequests == false )
            return;
        string report = BuildReport( );
        EditorGUILayout.BeginHorizontal( );
        if ( GUILayout.Button( "요청/출력 기록 복사" ) == true )
            EditorGUIUtility.systemCopyBuffer = report;
        if ( GUILayout.Button( "요청/출력 기록 콘솔 출력" ) == true )
            Debug.Log( "[Sound Debug]\n" + report, target );
        EditorGUILayout.EndHorizontal( );
        _requestScroll = EditorGUILayout.BeginScrollView( _requestScroll, GUILayout.Height( 180f ) );
        float height = EditorStyles.textArea.CalcHeight( new GUIContent( report ), Mathf.Max( 100f, EditorGUIUtility.currentViewWidth - 70f ) );
        EditorGUILayout.SelectableLabel( report, EditorStyles.textArea, GUILayout.Height( height ) );
        EditorGUILayout.EndScrollView( );
        EditorGUILayout.HelpBox( "ID별 마지막 요청을 보관합니다. 접수는 청취 성공이 아닙니다. "
            + "발소리 기록이 없으면 이동/접지 조건과 이벤트를 확인하세요. 종료된 짧은 음원의 요청 기록도 남습니다.", MessageType.Info );
    }

    /// <summary>
    /// 저장된 설정이 아닌 현재 AudioSource 상태를 요약
    /// </summary>
    /// <param name="label">출력 구분</param>
    /// <param name="source">실제 출력</param>
    /// <returns>현재 출력 상태</returns>
    static string DescribeOutput ( string label, AudioSource source )
    {
        if ( source == null )
            return label + ": 출력 없음";
        string result = $"{label}: Spatial Blend={source.spatialBlend:0.##} / 활성={source.isActiveAndEnabled}"
            + $" / 재생 중={source.isPlaying} / 음량={source.volume:0.##} / 음소거={source.mute}";
        if ( source.spatialBlend > 0f )
            result += $" / 위치={source.transform.position} / 감쇠={source.rolloffMode} / 거리={source.minDistance:0.##}~{source.maxDistance:0.##}";
        return result;
    }

    /// <summary>
    /// 초기화가 끝난 플레이어 상태를 조회하고 실제 행동 API만 호출
    /// </summary>
    void DrawActions ()
    {
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "플레이어 행동", EditorStyles.boldLabel );
        Hero hero = _hero;
        if ( _scene == null || _scene.isActiveAndEnabled == false || _scene.IsInitialized == false
            || _scene.IsStageReady == false || _scene.IsEndingStage == true
            || hero == null || hero.isActiveAndEnabled == false || _scene.Hero != hero )
        {
            EditorGUILayout.HelpBox( "행동 검증 대기: 선택 플레이어를 소유하는 활성 PlayScene과 도전 초기화 완료가 필요합니다.", MessageType.Info );
            return;
        }
        SoundPlayer player = hero.GetComponent<SoundPlayer>( );
        if ( player == null || player.IsConnectedTo( _manager ) == false )
        {
            EditorGUILayout.HelpBox( "선택 플레이어의 SoundPlayer가 상위 사운드 관리자에 연결되지 않았습니다.", MessageType.Warning );
            return;
        }

        Energy energy = ReadReference( hero, "_energy" ) as Energy;
        EditorGUILayout.LabelField( "무기", hero.WeaponData.WeaponType.ToString( ) );
        EditorGUILayout.LabelField( "에너지", $"{energy.CurrentEnergy} / {energy.MaxEnergy}" );
        EditorGUILayout.LabelField( "스킬",
            $"비용 {hero.SkillCaster.EnergyCost} / 쿨타임 {hero.SkillCaster.RemainingCooldown:0.00} / 사용 가능 {hero.CanUseSkill}" );
        EditorGUILayout.LabelField( "궁극기",
            $"비용 {hero.UltimateCaster.EnergyCost} / 쿨타임 {hero.UltimateCaster.RemainingCooldown:0.00} / 사용 가능 {hero.CanUseUltimate}" );
        HeroUltimateAim aim = hero.UltimateAim;
        EditorGUILayout.LabelField( "궁극기 조준",
            $"조준 중={aim.IsAiming} / 지면={aim.HasGroundPoint} / 유효 위치={aim.IsValid}" );

        string block = GetActionPause( );
        if ( string.IsNullOrEmpty( block ) == false )
            EditorGUILayout.HelpBox( block, MessageType.Info );
        using ( new EditorGUI.DisabledScope( string.IsNullOrEmpty( block ) == false ) )
        {
            if ( GUILayout.Button( "검증용 에너지 최대 충전" ) == true )
                _result = $"에너지 {energy.Add( energy.MaxEnergy - energy.CurrentEnergy )} 충전";
            EditorGUILayout.BeginHorizontal( );
            if ( GUILayout.Button( hero.IsAimingUltimate ? "궁극기 조준 확정" : "공격 요청" ) == true )
            {
                if ( hero.IsAimingUltimate == true )
                    RequestUltimate( hero );
                else
                    ReportAction( "공격", hero.TryAttack( ) );
            }
            if ( GUILayout.Button( "스킬 요청" ) == true )
            {
                //실제 스킬 입력과 같은 순서로 조준을 취소한 뒤 요청
                hero.CancelUltimateAim( );
                ReportAction( "스킬", hero.TryUseSkill( ) );
            }
            if ( GUILayout.Button( "궁극기 / 조준 확정" ) == true )
                RequestUltimate( hero );
            EditorGUILayout.EndHorizontal( );
            EditorGUILayout.BeginHorizontal( );
            if ( GUILayout.Button( "점프 요청" ) == true )
                ReportAction( "점프", hero.TryJump( ) );
            if ( GUILayout.Button( "전방 회피 요청" ) == true )
                ReportAction( "회피", hero.TryDodge( hero.transform.forward ) );
            EditorGUILayout.EndHorizontal( );
        }
        //시간이 멈춰도 미확정 조준을 취소할 수 있음
        if ( GUILayout.Button( "조준 취소" ) == true )
        {
            hero.CancelUltimateAim( );
            _result = "궁극기 조준 취소 요청 완료";
        }
        DrawConnections( hero );
    }

    /// <summary>
    /// 에디터와 실제 메뉴/게임/오디오 정지 중 행동 요청 차단
    /// </summary>
    /// <returns>정지 사유이며 빈 문자열이면 요청 가능</returns>
    string GetActionPause ()
    {
        if ( EditorApplication.isPaused == true )
            return "에디터 Pause 상태입니다.";
        if ( Time.timeScale <= 0f )
            return "게임 시간이 정지돼 있습니다.";
        if ( AudioListener.pause == true )
            return "플레이 오디오가 일시정지돼 있습니다.";
        if ( _pauseMenus.Any( menu => menu != null && menu.gameObject.scene == _scene.gameObject.scene && menu.IsPaused ) )
            return "현재 플레이 씬의 메뉴가 일시정지 상태입니다.";
        return string.Empty;
    }

    /// <summary>
    /// 궁극기 조준 시작과 실제 시전 승인 결과 구분
    /// </summary>
    /// <param name="hero">요청 대상 플레이어</param>
    void RequestUltimate ( Hero hero )
    {
        bool accepted = hero.TryRequestUltimate( );
        if ( accepted == false )
        {
            _result = "궁극기 요청 거절. 에너지/쿨타임/행동 상태와 조준 지면/거리를 확인하세요.";
            return;
        }
        _result = hero.IsAimingUltimate
            ? "궁극기 조준 시작. 플레이 카메라 중앙으로 위치를 정한 뒤 확정하세요."
            : "궁극기 시전 승인. 실제 실행과 소리/모션 타이밍은 확인 대상입니다.";
    }

    /// <summary>
    /// 실제 플레이어 데이터의 사운드 연결을 변경 없이 표시
    /// </summary>
    /// <param name="hero">초기화가 끝난 플레이어</param>
    void DrawConnections ( Hero hero )
    {
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "행동 사운드 연결 / 맵 에셋 기준", EditorStyles.boldLabel );
        SoundPlayer player = hero.GetComponent<SoundPlayer>( );
        EditorGUILayout.LabelField( "캐릭터 출력",
            player != null && player.IsConnectedTo( _manager ) ? "현재 서비스 연결" : "서비스 연결 누락" );
        using ( var serialized = new SerializedObject( hero ) )
            DrawSoundStatus( "발소리", ( SoundId ) serialized.FindProperty( "_footstepSound" ).intValue, SoundType.Footstep );
        DrawSoundStatus( "점프", SoundId.HeroJump, SoundType.Sfx );
        DrawSoundStatus( "회피", SoundId.HeroDodge, SoundType.Sfx );
        DrawSoundStatus( "피격", SoundId.HeroHurt, SoundType.Sfx );
        DrawSoundStatus( "사망", SoundId.HeroDeath, SoundType.Sfx );

        WeaponData weapon = hero.WeaponData;
        if ( weapon.WeaponType == WeaponType.Sword )
        {
            DrawSoundStatus( "검 공격", SoundId.SwordSwing, SoundType.Sfx );
            DrawSoundStatus( "검 스킬", SoundId.SwordSkill, SoundType.Sfx );
            DrawSoundStatus( "검 궁극기", SoundId.SwordUltimate, SoundType.Sfx );
        }
        else
        {
            ProjectileData basic = hero.BasicAttackExecutor is ProjectileAttackExecutor
                ? ReadReference( hero.BasicAttackExecutor, "_projectileData" ) as ProjectileData : null;
            DrawProjectileStatus( "기본 공격", basic );
            ProjectileData skill = weapon.SkillData is BowSkillData bow ? bow.ProjectileData
                : ( weapon.SkillData is StaffSkillData staff ? staff.ProjectileData : null );
            if ( weapon.SkillData is StaffSkillData && skill != null )
            {
                DrawSoundStatus( "스킬 발사 (실행자 지정)", StaffSkillExecutor.LaunchSound, SoundType.Sfx );
                DrawSoundStatus( "스킬 충돌 (실행자 지정)", StaffSkillExecutor.ImpactSound, SoundType.Sfx );
            }
            else
                DrawProjectileStatus( "스킬", skill );
            Component area = weapon.UltimateData is BowUltimateData rain ? rain.AreaPrefab
                : ( weapon.UltimateData is StaffUltimateData pillar ? ( Component ) pillar.AreaPrefab : null );
            if ( area == null )
                EditorGUILayout.HelpBox( "궁극기 영역 프리팹이 없습니다.", MessageType.Warning );
            else
            {
                using ( var serialized = new SerializedObject( area ) )
                {
                    DrawSoundStatus( "궁극기 생성", ( SoundId ) serialized.FindProperty( "_spawnSound" ).intValue, SoundType.Sfx, true );
                    DrawSoundStatus( "궁극기 주기", ( SoundId ) serialized.FindProperty( "_tickSound" ).intValue, SoundType.Sfx, true );
                }
            }
        }
        if ( GUILayout.Button( "현재 모션 이벤트 검사" ) == true )
            InspectMotionEvents( hero );
        if ( string.IsNullOrEmpty( _motionResult ) == false )
            EditorGUILayout.HelpBox( _motionResult, MessageType.Info );
    }

    /// <summary>
    /// 동료 AI를 강제 실행하지 않고 선택한 동료의 실제 연결만 표시
    /// </summary>
    /// <param name="companion">검사할 동료</param>
    void DrawCompanionConnections ( Companion companion )
    {
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "선택 동료 연결", EditorStyles.boldLabel );
        using ( var serialized = new SerializedObject( companion ) )
            DrawSoundStatus( "발소리", ( SoundId ) serialized.FindProperty( "_footstepSound" ).intValue, SoundType.Footstep );
        DrawSoundStatus( "피격", SoundId.CompanionHurt, SoundType.Sfx );
        DrawSoundStatus( "사망", SoundId.CompanionDeath, SoundType.Sfx );
        if ( companion.IsInitialized == true )
        {
            Attacker attacker = ReadReference( companion, "_attacker" ) as Attacker;
            if ( attacker.Executor is ProjectileAttackExecutor executor )
                DrawProjectileStatus( "동료 공격", ReadReference( executor, "_projectileData" ) as ProjectileData );
        }
        EditorGUILayout.HelpBox( "동료의 이동/공격은 기존 AI로 실행합니다. 여기서는 연결과 모션 이벤트를 확인합니다.", MessageType.Info );
        if ( GUILayout.Button( "동료 모션 이벤트 검사" ) == true )
            InspectMotionEvents( companion );
        if ( string.IsNullOrEmpty( _motionResult ) == false )
            EditorGUILayout.HelpBox( _motionResult, MessageType.Info );
    }

    /// <summary>
    /// 투사체의 발사음과 충돌음 진단
    /// </summary>
    /// <param name="label">투사체 사용처</param>
    /// <param name="data">실제 연결 데이터</param>
    void DrawProjectileStatus ( string label, ProjectileData data )
    {
        if ( data == null )
        {
            EditorGUILayout.HelpBox( label + ": 투사체 데이터가 없습니다.", MessageType.Warning );
            return;
        }
        DrawSoundStatus( label + " 발사", data.LaunchSound, SoundType.Sfx, true );
        DrawSoundStatus( label + " 충돌", data.ImpactSound, SoundType.Sfx, true );
    }

    /// <summary>
    /// 식별자와 출력 종류를 검사하되 행동 요청은 차단하지 않음
    /// </summary>
    /// <param name="label">사운드 용도</param>
    /// <param name="id">실제 호출 식별자</param>
    /// <param name="expected">호출에 필요한 출력 종류</param>
    /// <param name="optional">None을 미사용으로 허용하는지 여부</param>
    void DrawSoundStatus ( string label, SoundId id, SoundType expected, bool optional = false )
    {
        if ( id == SoundId.None )
        {
            if ( optional == true )
                EditorGUILayout.LabelField( label, "None / 소리 미사용" );
            else
                EditorGUILayout.HelpBox( label + ": 필수 호출 ID가 지정되지 않았습니다.", MessageType.Warning );
            return;
        }
        if ( string.IsNullOrEmpty( _mapError ) == false || _map == null )
        {
            EditorGUILayout.LabelField( label, id + " / 맵 검사 불가" );
            return;
        }
        if ( _sounds.TryGetValue( id, out SoundData data ) == false )
        {
            EditorGUILayout.HelpBox( $"{label}: {id}가 맵에 등록되지 않았습니다.", MessageType.Warning );
            return;
        }
        if ( data.Type != expected )
            EditorGUILayout.HelpBox( $"{label}: {id}의 종류는 {data.Type}입니다. 용도에 맞는 {expected}를 지정하세요.", MessageType.Warning );
        else
            EditorGUILayout.LabelField( label, id + " / 데이터 설정 확인, 실제 출력 미확인" );
    }

    /// <summary>
    /// 현재 컨트롤러의 이벤트 목록만 보고하며 행동별 연결 완료로 판정하지 않음
    /// </summary>
    /// <param name="actor">검사할 캐릭터</param>
    void InspectMotionEvents ( Component actor )
    {
        AnimatorHandler handler = ReadReference( actor, "_animatorHandler" ) as AnimatorHandler;
        Animator animator = handler != null ? handler.GetComponent<Animator>( ) : null;
        if ( animator == null || animator.runtimeAnimatorController == null )
        {
            _motionResult = "Animator 또는 컨트롤러가 연결되지 않았습니다.";
            return;
        }
        var events = new List<string>( );
        foreach ( AnimationClip clip in animator.runtimeAnimatorController.animationClips.Distinct( ) )
        {
            foreach ( AnimationEvent item in AnimationUtility.GetAnimationEvents( clip ) )
            {
                if ( item.functionName == "AnimationFootstep" )
                    events.Add( clip.name + ": AnimationFootstep" );
                else if ( item.functionName == "AnimationSound" )
                    events.Add( clip.name + ": AnimationSound(" + ( SoundId ) item.intParameter + ")" );
            }
        }
        _motionResult = "검사 시점 대상: " + actor.name + "\n"
            + ( events.Count == 0 ? "발소리/행동 사운드 이벤트가 발견되지 않았습니다." : string.Join( "\n", events ) )
            + "\n컨트롤러 전체의 참고 목록입니다. 실제 행동 클립/재생 시점은 플레이에서 확인하세요.";
    }

    /// <summary>
    /// 임시 출력 없이 실제 관리자의 정지/복귀와 UI/BGM 요청 제공
    /// </summary>
    void DrawAudioControls ()
    {
        EditorGUILayout.Space( );
        EditorGUILayout.LabelField( "실제 관리자 오디오 제어", EditorStyles.boldLabel );
        if ( _manager == null )
            return;
        using ( new EditorGUI.DisabledScope( _manager.IsReady == false || EditorApplication.isPaused == true ) )
        {
            EditorGUILayout.BeginHorizontal( );
            if ( GUILayout.Button( "플레이 오디오 일시정지" ) == true )
            {
                bool accepted = _manager.TrySetGameplayPaused( true, out string reason );
                _result = accepted ? "플레이 오디오 일시정지 적용" : reason;
            }
            if ( GUILayout.Button( "UI 클릭음 요청" ) == true )
                ReportAudioRequest( "UI 클릭음", _manager.Play( SoundId.UiClick ) );
            EditorGUILayout.EndHorizontal( );
            EditorGUILayout.BeginHorizontal( );
            if ( GUILayout.Button( "타이틀 BGM 요청" ) == true )
                ReportAudioRequest( "타이틀 BGM", _manager.Play( SoundId.TitleBgm ) );
            if ( GUILayout.Button( "플레이 BGM 요청" ) == true )
                ReportAudioRequest( "플레이 BGM", _manager.Play( SoundId.PlayBgm ) );
            EditorGUILayout.EndHorizontal( );
        }
        if ( GUILayout.Button( "플레이 오디오 복귀" ) == true )
        {
            bool accepted = _manager.TrySetGameplayPaused( false, out string reason );
            _result = accepted ? "이 관리자가 적용한 오디오 정지 복구 완료" : reason;
        }
        EditorGUILayout.BeginHorizontal( );
        if ( GUILayout.Button( "플레이 효과음 중단" ) == true )
        {
            _manager.StopSfx( );
            _result = "플레이 효과음 중단 요청 완료";
        }
        if ( GUILayout.Button( "전체 오디오 중단" ) == true )
        {
            _manager.StopAll( );
            _result = "전체 오디오 중단 요청 완료. 일시정지 복구와는 별개입니다.";
        }
        EditorGUILayout.EndHorizontal( );
    }

    /// <summary>
    /// 공통 관리자 요청 접수와 실제 청취를 구분하여 결과 표시
    /// </summary>
    /// <param name="label">오디오 요청 이름</param>
    /// <param name="accepted">관리자 접수 결과</param>
    void ReportAudioRequest ( string label, bool accepted )
    {
        _result = label + ( accepted ? ": 실제 관리자에 요청 접수. 출력은 청취로 확인하세요."
            : ": 요청 거절. 관리자 준비/맵 등록/Console을 확인하세요." );
    }

    /// <summary>
    /// 요청 접수와 실제 소리 확인을 구분하여 결과 표시
    /// </summary>
    /// <param name="action">요청한 행동</param>
    /// <param name="accepted">실제 게임 API의 승인 결과</param>
    void ReportAction ( string action, bool accepted )
    {
        _result = action + ( accepted ? ": 행동 요청 승인. 효과음과 모션 타이밍은 청취 확인 대상입니다."
            : ": 행동 요청 거절. 에너지/쿨타임/접지/진행 중인 행동 상태를 확인하세요." );
    }

    /// <summary>
    /// 같은 관리자에 연결된 현재 볼륨 설정의 검증 조작 표시
    /// </summary>
    void DrawVolumeSettings ()
    {
        _showVolumes = EditorGUILayout.Foldout( _showVolumes, "볼륨 설정 검증", true );
        if ( _showVolumes == false )
            return;
        GameManager game = GameManager.Instance;
        if ( game == null || game.Sound != _manager || game.Settings == null )
        {
            EditorGUILayout.HelpBox( "이 관리자를 소유하는 GameManager의 볼륨 설정이 준비되지 않았습니다.", MessageType.Info );
            return;
        }
        ConnectVolumeSettings( game.Settings.Volume );
        DrawVolumeControls( _manager );
    }

    /// <summary>
    /// 현재 실행의 설정과 검증 선택값 연결
    /// </summary>
    /// <param name="settings">현재 볼륨 설정</param>
    void ConnectVolumeSettings ( VolumeSettings settings )
    {
        if ( _volumeSettings == settings )
            return;

        _volumeSettings = settings;
        SelectVolumes ( settings.Settings );
    }

    /// <summary>
    /// 출력이나 파일 변경 없이 검증 선택값 반영
    /// </summary>
    /// <param name="setting">선택할 설정</param>
    void SelectVolumes ( VolumeSetting setting )
    {
        _masterVolume = setting.Master;
        _bgmVolume = setting.Bgm;
        _sfxVolume = setting.Sfx;
        _masterMuted = false;
        _bgmMuted = false;
        _sfxMuted = false;
    }

    /// <summary>
    /// 검증용 볼륨과 음소거 입력 및 적용 요청
    /// </summary>
    /// <param name="manager">볼륨을 적용할 관리자</param>
    void DrawVolumeControls ( SoundManager manager )
    {
        EditorGUILayout.LabelField (
            "볼륨" , EditorStyles.boldLabel );

        VolumeSetting current = _volumeSettings.Settings;
        EditorGUILayout.LabelField ( "복원/마지막 적용값" ,
            $"마스터 {current.Master} / BGM {current.Bgm} / SFX {current.Sfx}" );

        if ( string.IsNullOrEmpty ( _volumeSettings.LoadError ) == false )
            EditorGUILayout.HelpBox ( _volumeSettings.LoadError , MessageType.Warning );

        _masterVolume = EditorGUILayout.Slider (
            "마스터" , _masterVolume , 0f , 1f );
        _masterMuted = EditorGUILayout.Toggle (
            "전체 음소거" , _masterMuted );

        _bgmVolume = EditorGUILayout.Slider (
            "BGM" , _bgmVolume , 0f , 1f );
        _bgmMuted = EditorGUILayout.Toggle (
            "BGM 음소거" , _bgmMuted );

        _sfxVolume = EditorGUILayout.Slider (
            "효과음" , _sfxVolume , 0f , 1f );
        _sfxMuted = EditorGUILayout.Toggle (
            "효과음 음소거" , _sfxMuted );

        if ( GUILayout.Button ( "볼륨 기본값 선택" ) == true )
            SelectVolumes ( _volumeSettings.DefaultSettings );

        EditorGUILayout.HelpBox (
            "음소거는 적용할 값을 0으로 바꾸며 선택한 슬라이더 값은 유지합니다. "
            + "저장하면 0이 저장되며 음소거 토글과 이전 슬라이더 값은 별도 저장하지 않습니다. "
            + "저장 없는 적용은 이번 실행에만 반영합니다." , MessageType.Info );

        using ( new EditorGUI.DisabledScope ( manager.CanApplyVolumes == false ) )
        {
            bool apply = GUILayout.Button ( "볼륨 저장 없이 적용" );
            bool save = GUILayout.Button ( "볼륨 저장 후 적용" );

            if ( apply == true || save == true )
            {
                //음소거는 적용 값만 0으로 바꾸고 입력한 볼륨은 보존
                VolumeSetting setting = new (
                    _masterMuted == true ? 0f : _masterVolume ,
                    _bgmMuted == true ? 0f : _bgmVolume ,
                    _sfxMuted == true ? 0f : _sfxVolume );

                string reason;
                bool succeeded = save == true
                    ? _volumeSettings.TrySaveAndApply ( setting , out reason )
                    : _volumeSettings.TryApply ( setting , out reason );

                if ( succeeded == true )
                    reason = $"{( save == true ? "저장 후" : "저장 없이" )} 적용 완료: "
                        + $"마스터 {setting.Master} / BGM {setting.Bgm} / SFX {setting.Sfx}";

                Report ( succeeded , reason );

                if ( succeeded == true )
                    Debug.Log ( $"[볼륨 설정 검증] {reason}" , target );
                else
                    Debug.LogWarning ( $"[볼륨 설정 검증] {reason}" , target );
            }
        }
    }


    /// <summary>
    /// 마지막 검증 요청 결과 표시
    /// </summary>
    /// <param name="succeeded">요청 성공 여부</param>
    /// <param name="reason">실패 사유</param>
    void Report ( bool succeeded , string reason )
    {
        _result = succeeded == true
            ? ( string.IsNullOrEmpty ( reason ) == true ? "요청 성공" : reason )
            : $"요청 실패: {reason}";

    }

    /// <summary>
    /// 기존 실행 객체의 직렬화 참조를 변경 없이 조회
    /// </summary>
    /// <param name="target">조회할 실행 객체</param>
    /// <param name="field">참조 필드 이름</param>
    /// <returns>현재 연결된 객체</returns>
    static UnityEngine.Object ReadReference ( UnityEngine.Object target, string field )
    {
        using ( var serialized = new SerializedObject( target ) )
            return serialized.FindProperty( field ).objectReferenceValue;
    }

    /// <summary>
    /// 게임 상태를 변경하지 않고 인스펙터가 보유한 참조만 해제
    /// </summary>
    void ClearConnection ()
    {
        _manager = null;
        _map = null;
        _scene = null;
        _hero = null;
        _companion = null;
        _otherPlayer = null;
        _scenes = Array.Empty<PlayScene>( );
        _heroes = Array.Empty<Hero>( );
        _companions = Array.Empty<Companion>( );
        _otherPlayers = Array.Empty<SoundPlayer>( );
        _loops = Array.Empty<SoundLoopPlayer>( );
        _pauseMenus = Array.Empty<PauseMenuController>( );
        _sounds.Clear( );
        _volumeSettings = null;
        _result = string.Empty;
        _motionResult = string.Empty;
        _mapError = string.Empty;
    }

    /// <summary>
    /// 도메인 재로드 설정과 무관하게 이전 실행의 대상과 상태 정리
    /// </summary>
    /// <param name="state">현재 플레이 전환 단계</param>
    void HandlePlayMode ( PlayModeStateChange state )
    {
        _isPlaySession = state == PlayModeStateChange.EnteredPlayMode;
        ClearConnection( );
        _nextRefresh = 0d;
    }

    /// <summary>
    /// 플레이 중 인스펙터의 목록과 상태 갱신
    /// </summary>
    /// <returns>현재 플레이 세션의 재표시 필요 여부</returns>
    public override bool RequiresConstantRepaint ()
    {
        return _isPlaySession && Application.isPlaying;
    }

    private void OnDisable ()
    {
        EditorApplication.playModeStateChanged -= HandlePlayMode;
        ClearConnection( );
    }
}
#endif
