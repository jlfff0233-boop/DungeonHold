using UnityEngine;

/// <summary>
/// 반복 사운드 플레이어 - 소유 오브젝트의 지속음 시작과 중단 관리
/// </summary>
[DisallowMultipleComponent]
public class SoundLoopPlayer : MonoBehaviour
{
    [SerializeField, Min( 1f )] float _maxDistance = 30f; //공간 음향 감쇠 거리

    SoundManager _sound; //전달받은 공통 사운드 서비스
    AudioSource _source; //이 컴포넌트가 소유하는 반복 출력
    SoundId _requestedId; //현재 요청한 반복음
    SoundId _failedId; //설정 오류로 거절한 ID이며 명시적 중단/초기화에서 해제
    bool _startPending; //준비 또는 일시정지 해제를 기다리는 요청
    bool _hasStarted; //재생을 시작한 요청 여부

    /// <summary>
    /// 연결된 공통 사운드 서비스
    /// </summary>
    public SoundManager Service => _sound;

    /// <summary>
    /// 준비 대기 또는 재생을 시작한 요청 여부
    /// </summary>
    public bool HasRequest => _startPending || _hasStarted;

    /// <summary>
    /// 이전 요청과 구독을 정리한 뒤 공통 서비스 연결
    /// </summary>
    /// <param name="sound">공통 사운드 서비스</param>
    public void Init ( SoundManager sound )
    {
        Stop( );
        Unbind( );
        _sound = sound;
        if ( isActiveAndEnabled == true && _sound != null )
            _sound.OnGameplayStopped += Stop;
    }

    private void Update ()
    {
        if ( _startPending == true )
            TryStartPending( );
    }

    /// <summary>
    /// 같은 반복 요청을 유지하고 미준비 상태는 재생 시작까지 대기
    /// </summary>
    /// <param name="id">반복 효과음 식별자</param>
    /// <returns>요청 접수 여부이며 대기 상태도 포함</returns>
    public bool TryPlay ( SoundId id )
    {
        if ( isActiveAndEnabled == false || _sound == null || id == SoundId.None )
            return false;
        if ( _failedId == id )
            return false;
        if ( _requestedId == id && ( _startPending == true || _hasStarted == true ) )
            return true;

        Stop( );
        _requestedId = id;
        _startPending = true;
        return TryStartPending( );
    }

    /// <summary>
    /// 준비된 요청만 한 번 시작하고 잘못된 데이터는 반복 요청하지 않음
    /// </summary>
    /// <returns>대기 중이거나 재생 시작에 성공했는지 여부</returns>
    bool TryStartPending ()
    {
        if ( _sound == null )
        {
            Stop( );
            return false;
        }
        if ( _sound.IsReady == false || AudioListener.pause == true )
            return true;

        _startPending = false;
        if ( _sound.TryPrepareLoop( _requestedId, out SoundData data ) == false )
        {
            _failedId = _requestedId;
            _requestedId = SoundId.None;
            return false;
        }
        if ( _source == null )
        {
            var output = new GameObject( "LoopSfx" );
            output.transform.SetParent( transform, false );
            _source = output.AddComponent<AudioSource>( );
            _source.playOnAwake = false;
            _source.loop = true;
            _source.ignoreListenerPause = false;
            _source.spatialBlend = 1f;
            _source.dopplerLevel = 0f;
            _source.minDistance = 1f;
            _source.maxDistance = _maxDistance;
        }
        _source.outputAudioMixerGroup = _sound.SfxOutput;
        _source.clip = data.Clips[0];
        _source.volume = data.Volume;
        _source.Play( );
        _hasStarted = true;
        return true;
    }

    /// <summary>
    /// 출력과 대기 요청을 함께 중단하여 전체 중단 후 자동 재생 방지
    /// </summary>
    public void Stop ()
    {
        _startPending = false;
        _hasStarted = false;
        _requestedId = SoundId.None;
        _failedId = SoundId.None;
        if ( _source == null )
            return;
        _source.Stop( );
        _source.clip = null;
    }

    /// <summary>
    /// 이전 서비스의 중단 알림 구독 해제
    /// </summary>
    void Unbind ()
    {
        if ( _sound != null )
            _sound.OnGameplayStopped -= Stop;
    }

    private void OnEnable ()
    {
        if ( _sound != null )
            _sound.OnGameplayStopped += Stop;
    }

    private void OnDisable ()
    {
        Stop( );
        Unbind( );
    }

    private void OnDestroy ()
    {
        if ( _source != null )
            Destroy( _source.gameObject );
    }
}
