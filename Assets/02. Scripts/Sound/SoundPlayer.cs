using UnityEngine;

/// <summary>
/// 사운드 플레이어 - 캐릭터 위치의 발소리와 행동음 출력
/// </summary>
public class SoundPlayer : MonoBehaviour
{
    [SerializeField, Range( 0f, 1f )] float _spatialBlend = 1f; //공간 음향 비율
    [SerializeField, Min( 1f )] float _maxDistance = 30f; //감쇠 거리

    SoundManager _sound; //공통 사운드 서비스
    GameObject _outputRoot; //런타임 출력 소유 객체
    AudioSource _footsteps; //발소리 출력
    AudioSource _effects; //행동 효과음 출력

    /// <summary>
    /// 현재 연결된 캐릭터 출력이 거리 감쇠 없는 2D 방식인지 여부
    /// </summary>
    public bool Is2DOutput => _effects != null && _effects.spatialBlend == 0f;

#if UNITY_EDITOR
    System.Collections.Generic.Dictionary<SoundId, string> _debugRequests = new( ); //ID별 마지막 실제 요청

    /// <summary>
    /// 이번 연결 이후 ID별 마지막 재생 요청 결과
    /// </summary>
    public System.Collections.Generic.IReadOnlyDictionary<SoundId, string> DebugRequests => _debugRequests;

    /// <summary>
    /// 현재 발소리 출력의 실제 설정
    /// </summary>
    public AudioSource DebugFootsteps => _footsteps;

    /// <summary>
    /// 현재 행동음 출력의 실제 설정
    /// </summary>
    public AudioSource DebugEffects => _effects;
#endif

    /// <summary>
    /// 지정한 서비스로 로컬 출력이 준비되어 있는지 확인
    /// </summary>
    /// <param name="sound">비교할 공통 서비스</param>
    /// <returns>서비스와 출력이 모두 연결됐는지 여부</returns>
    public bool IsConnectedTo ( SoundManager sound )
    {
        return sound != null && _sound == sound && _outputRoot != null;
    }

    /// <summary>
    /// 공통 서비스 연결과 로컬 출력 준비
    /// </summary>
    /// <param name="sound">공통 관리자</param>
    public void Init ( SoundManager sound )
    {
        Init( sound, _spatialBlend );
    }

    /// <summary>
    /// 캐릭터 연결부가 지정한 공간 음향 비율로 출력 준비 또는 재설정
    /// </summary>
    /// <param name="sound">공통 관리자</param>
    /// <param name="spatialBlend">0은 거리 감쇠 없는 본인 출력, 1은 위치 출력</param>
    public void Init ( SoundManager sound, float spatialBlend )
    {
        StopAll( );
        Unbind( );
        _sound = sound;
#if UNITY_EDITOR
        _debugRequests.Clear( );
#endif

        //기존 프리팹에 출력 참조를 추가하지 않고 최초 연결에서 준비
        if ( _outputRoot == null )
        {
            _outputRoot = new GameObject( "SoundOutput" );
            _outputRoot.transform.SetParent( transform, false );
            _footsteps = CreateSource( );
            _effects = CreateSource( );
        }

        _footsteps.outputAudioMixerGroup = sound.SfxOutput;
        _effects.outputAudioMixerGroup = sound.SfxOutput;
        //이미 생성된 출력도 재연결 시 지정한 방식으로 갱신
        _footsteps.spatialBlend = spatialBlend;
        _effects.spatialBlend = spatialBlend;

        if ( isActiveAndEnabled == true )
            _sound.OnGameplayStopped += StopAll;
    }

    /// <summary>
    /// 캐릭터에 부착된 출력 생성
    /// </summary>
    /// <returns>생성한 출력</returns>
    AudioSource CreateSource ()
    {
        AudioSource source = _outputRoot.AddComponent<AudioSource>( );
        source.playOnAwake = false;
        source.loop = false;
        source.ignoreListenerPause = false;
        source.spatialBlend = _spatialBlend;
        source.dopplerLevel = 0f;
        source.minDistance = 1f;
        source.maxDistance = _maxDistance;
        return source;
    }

    /// <summary>
    /// 식별자에 대응하는 로컬 효과음 재생
    /// </summary>
    /// <param name="id">사운드 식별자</param>
    /// <returns>재생 요청 성공 여부</returns>
    public bool Play ( SoundId id )
    {
        if ( isActiveAndEnabled == false || _sound == null || _outputRoot == null )
        {
            RecordRequest( id, "요청 거절: 캐릭터 비활성 또는 사운드 서비스/출력 미연결" );
            return false;
        }

        AudioClip clip = _sound.GetSfx( id, out SoundData data, out string reason );
        if ( clip == null )
        {
            RecordRequest( id, "요청 거절: " + reason );
            return false;
        }

        AudioSource source = data.Type == SoundType.Footstep ? _footsteps : _effects;
        if ( source.isActiveAndEnabled == false )
        {
            RecordRequest( id, "요청 거절: AudioSource 비활성" );
            return false;
        }
        source.PlayOneShot( clip, data.Volume );
        RecordRequest( id, $"요청 접수: {clip.name} / Spatial Blend={source.spatialBlend:0.##}"
            + $" / 개별 음량={data.Volume:0.##} / 출력 음량={source.volume:0.##} / 음소거={source.mute} / 로딩={clip.loadState}" );
        return true;
    }

    /// <summary>
    /// 에디터에서만 실제 요청 기록을 보관하며 청취 성공으로 판정하지 않음
    /// </summary>
    /// <param name="id">요청한 ID</param>
    /// <param name="result">접수 또는 거절 사유</param>
    [System.Diagnostics.Conditional( "UNITY_EDITOR" )]
    void RecordRequest ( SoundId id, string result )
    {
#if UNITY_EDITOR
        _debugRequests[id] = $"프레임 {Time.frameCount} / {Time.time:0.00}초 / {result}";
#endif
    }

    /// <summary>
    /// 로컬 출력 전체 중단
    /// </summary>
    public void StopAll ()
    {
        //최초 Init 전이나 출력 계층이 먼저 파괴된 종료 경로
        if ( _outputRoot == null )
            return;

        _footsteps.Stop( );
        _effects.Stop( );
    }

    /// <summary>
    /// 공통 중단 알림 해제
    /// </summary>
    void Unbind ()
    {
        if ( _sound != null )
            _sound.OnGameplayStopped -= StopAll;
    }

    private void OnEnable ()
    {
        if ( _sound != null )
            _sound.OnGameplayStopped += StopAll;
    }

    private void OnDisable ()
    {
        StopAll( );
        Unbind( );
    }

    private void OnDestroy ()
    {
        //컴포넌트만 제거한 경우에도 생성한 출력은 함께 정리
        if ( _outputRoot != null )
            Destroy( _outputRoot );
    }
}
