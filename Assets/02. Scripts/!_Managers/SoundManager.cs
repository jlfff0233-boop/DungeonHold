using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 사운드 관리자 - 공통 볼륨, BGM 전환과 효과음 재생 관리
/// </summary>
public class SoundManager : MonoBehaviour
{
    const string MasterParameter = "MasterVolume";      //마스터 볼륨 노출 이름
    const string BgmParameter = "BgmVolume";        //BGM 볼륨 노출 이름
    const string SfxParameter = "SfxVolume";        //효과음 볼륨 노출 이름
    const float MinDecibel = -80f;      //음소거 볼륨

    /// <summary>
    /// BGM 전환 단계
    /// </summary>
    enum BgmFadePhase
    {
        None,       //전환 없음
        FadeOut,    //현재 BGM 감소
        FadeIn      //새 BGM 증가
    }


    [Header( "----- 출력 연결 -----" )]
    [SerializeField] AudioMixer _mixer;     //공통 볼륨 적용 대상
    [SerializeField] AudioSource _bgm;      //BGM 전용 출력
    [SerializeField] AudioSource _uiSfx;        //일시정지 중에도 재생할 UI 효과음
    [SerializeField] AudioSource _gameplaySfx;      //일시정지에 따르는 플레이 효과음

    [Header( "----- 사운드 설정 -----" )]
    [SerializeField] SoundDataMap _soundDataMap; //식별자별 음원 설정
    [SerializeField, Min( 1f )] float _worldDistance = 30f; //위치 출력 감쇠 거리
    [SerializeField, Min( 0f )] float _bgmTransition = 0.3f; //ID 요청의 BGM 전환 시간

    Dictionary<SoundId, SoundData> _sounds; //현재 초기화의 조회표
    WorldSoundPlayer _worldSounds; //위치 출력 관리
    float _bgmGain = 1f; //현재 곡의 개별 음량
    float _nextBgmGain = 1f; //전환할 곡의 개별 음량

    bool _isInitialized;        //출력 초기화 완료 여부
    bool _hasAppliedVolumes;        //최초 볼륨 적용 성공 여부

    float _masterDecibel;       //마지막 적용에 성공한 마스터 볼륨
    float _bgmDecibel;      //마지막 적용에 성공한 BGM 볼륨
    float _sfxDecibel;      //마지막 적용에 성공한 효과음 볼륨

    BgmFadePhase _fadePhase;        //현재 BGM 전환 단계
    AudioClip _nextBgm;     //감소 완료 후 재생할 BGM
    bool _nextBgmLoop;      //다음 BGM 반복 여부
    float _fadeDuration;        //각 전환 단계의 진행 시간
    float _fadeElapsed;     //현재 단계의 경과 시간
    float _fadeStartVolume;     //현재 단계 시작 시 BGM 출력 음량

    bool _ownsListenerPause;        //이 관리자가 적용한 일시정지 여부
    bool _previousListenerPause;        //일시정지 적용 전 전역 상태

#if UNITY_EDITOR
    Dictionary<SoundId, string> _debugWorldRequests = new( ); //위치 출력의 ID별 마지막 실제 요청
    Dictionary<SoundId, string> _debugCommonRequests = new( ); //BGM/UI/공통 효과음의 ID별 마지막 요청

    /// <summary>
    /// ID별 마지막 BGM/UI/공통 효과음 요청 결과
    /// </summary>
    public IReadOnlyDictionary<SoundId, string> DebugCommonRequests => _debugCommonRequests;

    /// <summary>
    /// 현재 UI 출력의 실제 설정
    /// </summary>
    public AudioSource DebugUiSource => _uiSfx;

    /// <summary>
    /// ID별 마지막 위치 재생 요청 결과
    /// </summary>
    public IReadOnlyDictionary<SoundId, string> DebugWorldRequests => _debugWorldRequests;

    /// <summary>
    /// 현재 남아 있는 위치 출력의 실제 설정
    /// </summary>
    public IReadOnlyList<AudioSource> DebugWorldSources => _worldSounds?.DebugSources;
#endif

    /// <summary>
    /// 공통 효과음 믹서
    /// </summary>
    public AudioMixerGroup SfxOutput => _gameplaySfx.outputAudioMixerGroup;

    /// <summary>
    /// 준비 상태 변경 이벤트(재생 가능 여부)
    /// </summary>
    public event Action<bool> OnReadyChanged;

    /// <summary>
    /// 플레이 효과음 전체 중단 이벤트
    /// </summary>
    public event Action OnGameplayStopped;

    /// <summary>
    /// 출력 초기화 완료 여부
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// 재생 가능 여부
    /// </summary>
    public bool IsReady => CanApplyVolumes && _hasAppliedVolumes;

    /// <summary>
    /// BGM 전환 진행 여부
    /// </summary>
    public bool IsBgmFading => _fadePhase != BgmFadePhase.None;

    /// <summary>
    /// 사운드 일시정지 여부
    /// </summary>
    public bool IsGameplayPaused => _ownsListenerPause;

    /// <summary>
    /// 볼륨 적용 가능 여부
    /// </summary>
    public bool CanApplyVolumes =>
        isActiveAndEnabled
        && _isInitialized;

    /// <summary>
    /// 사운드 설정과 출력 초기화
    /// </summary>
    /// <param name="reason">초기화 실패 사유</param>
    /// <returns>출력 초기화 성공 여부</returns>
    public bool Init ( out string reason )
    {
        reason = string.Empty;

        if ( isActiveAndEnabled == false )
        {
            reason = "활성화된 사운드 관리자에서 초기화해야 합니다.";
            return false;
        }

        if ( _isInitialized == true )
            return true;

        if ( _soundDataMap == null )
        {
            reason = "SoundManager의 사운드 데이터 맵을 연결해야 합니다.";
            return false;
        }

        if ( _soundDataMap.TryBuild( out Dictionary<SoundId, SoundData> sounds ) == false )
        {
            reason = "사운드 데이터 맵 설정을 확인하세요.";
            return false;
        }

        //최초 볼륨 적용 실패 시 되돌릴 현재 믹서 값 보관
        if ( _mixer.GetFloat( MasterParameter, out float masterDecibel ) == false
            || _mixer.GetFloat( BgmParameter, out float bgmDecibel ) == false
            || _mixer.GetFloat( SfxParameter, out float sfxDecibel ) == false )
        {
            reason = "믹서의 MasterVolume, BgmVolume, SfxVolume 노출 설정이 필요합니다.";
            return false;
        }

        _masterDecibel = masterDecibel;
        _bgmDecibel = bgmDecibel;
        _sfxDecibel = sfxDecibel;

        //Inspector에 저장되지 않는 일시정지 제외 설정만 코드에서 적용
        _bgm.ignoreListenerPause = true;
        _uiSfx.ignoreListenerPause = true;
        _gameplaySfx.ignoreListenerPause = false;

        _sounds = sounds;
        _worldSounds = new WorldSoundPlayer( transform, SfxOutput, _worldDistance );
#if UNITY_EDITOR
        _debugWorldRequests.Clear( );
        _debugCommonRequests.Clear( );
#endif

        //설정 볼륨을 적용하기 전에는 재생 요청을 받지 않음
        _hasAppliedVolumes = false;
        _isInitialized = true;
        return true;
    }

    private void Update ()
    {
        if ( _isInitialized == false )
            return;

        _worldSounds.Update( );

        if ( _fadePhase == BgmFadePhase.None )
            return;

        //메뉴 일시정지 중에도 BGM 전환은 계속 진행
        _fadeElapsed += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01( _fadeElapsed / _fadeDuration );
        float targetVolume =
            _fadePhase == BgmFadePhase.FadeOut ? 0f : _bgmGain;

        _bgm.volume = Mathf.Lerp(
            _fadeStartVolume, targetVolume, progress );

        if ( progress < 1f )
            return;

        if ( _fadePhase == BgmFadePhase.FadeOut )
        {
            //현재 곡을 정리한 뒤 다음 곡을 무음에서 시작
            _bgm.Stop( );
            _bgm.clip = _nextBgm;
            _bgm.loop = _nextBgmLoop;
            _bgmGain = _nextBgmGain;
            _bgm.volume = 0f;
            _bgm.Play( );

            _nextBgm = null;
            BeginFade( BgmFadePhase.FadeIn, _fadeDuration );
            return;
        }

        ClearFade( );
    }

    #region ----- 볼륨 -----

    /// <summary>
    /// 공통 볼륨 적용
    /// </summary>
    /// <param name="masterVolume">마스터 볼륨</param>
    /// <param name="bgmVolume">BGM 볼륨</param>
    /// <param name="sfxVolume">효과음 볼륨</param>
    /// <param name="reason">적용 또는 복구 실패 사유</param>
    /// <returns>전체 볼륨 적용 성공 여부</returns>
    public bool TrySetVolumes (
        float masterVolume,
        float bgmVolume,
        float sfxVolume,
        out string reason )
    {
        reason = string.Empty;

        if ( CanApplyVolumes == false )
        {
            reason = "사운드 초기화 후 볼륨을 적용해야 합니다.";
            return false;
        }

        if ( ValidateVolumes( masterVolume, bgmVolume, sfxVolume, out reason ) == false )
            return false;

        return TryApplyPreparedVolumes( masterVolume, bgmVolume, sfxVolume, out reason );
    }

    /// <summary>
    /// 검증된 볼륨을 믹서에 적용
    /// </summary>
    /// <param name="masterVolume">마스터 볼륨</param>
    /// <param name="bgmVolume">BGM 볼륨</param>
    /// <param name="sfxVolume">효과음 볼륨</param>
    /// <param name="reason">믹서 적용 또는 복구 실패 사유</param>
    /// <returns>전체 볼륨 적용 성공 여부</returns>
    public bool TryApplyPreparedVolumes (
        float masterVolume,
        float bgmVolume,
        float sfxVolume,
        out string reason )
    {
        //일반 외부 요청은 TrySetVolumes, 설정 요청은 VolumeSettings에서 검사
        reason = string.Empty;

        float masterDecibel = ToDecibel( masterVolume );
        float bgmDecibel = ToDecibel( bgmVolume );
        float sfxDecibel = ToDecibel( sfxVolume );

        if ( TryWriteMixerValues(
            masterDecibel, bgmDecibel, sfxDecibel ) == false )
        {
            //부분 적용 시 마지막 성공 값으로 복구
            bool restored = TryWriteMixerValues(
                _masterDecibel, _bgmDecibel, _sfxDecibel );

            if ( restored == false )
                SetVolumeReady( false );

            reason = restored == true
                ? "볼륨 적용에 실패하여 이전 믹서 값으로 복구했습니다."
                : "볼륨 적용과 이전 믹서 값 복구에 실패했습니다.";

            return false;
        }

        //전체 적용 성공 후 볼륨 값과 재생 준비 상태 반영
        _masterDecibel = masterDecibel;
        _bgmDecibel = bgmDecibel;
        _sfxDecibel = sfxDecibel;
        SetVolumeReady( true );
        return true;
    }

    /// <summary>
    /// 볼륨 준비 상태 반영과 알림
    /// </summary>
    /// <param name="isReady">재생 준비 여부</param>
    void SetVolumeReady ( bool isReady )
    {
        if ( _hasAppliedVolumes == isReady )
            return;

        _hasAppliedVolumes = isReady;
        if ( isReady == false )
            StopAllCore( );

        OnReadyChanged?.Invoke( isReady );
    }

    /// <summary>
    /// 세 볼륨 채널 적용 결과 확인
    /// </summary>
    /// <param name="masterDecibel">마스터 볼륨</param>
    /// <param name="bgmDecibel">BGM 볼륨</param>
    /// <param name="sfxDecibel">효과음 볼륨</param>
    /// <returns>전체 채널 적용 성공 여부</returns>
    bool TryWriteMixerValues (
        float masterDecibel,
        float bgmDecibel,
        float sfxDecibel )
    {
        //한 채널이 실패해도
        //나머지 채널의 적용 또는 복구는 모두 시도
        bool masterApplied = _mixer.SetFloat( MasterParameter, masterDecibel );
        bool bgmApplied = _mixer.SetFloat( BgmParameter, bgmDecibel );
        bool sfxApplied = _mixer.SetFloat( SfxParameter, sfxDecibel );

        return masterApplied && bgmApplied && sfxApplied;
    }

    /// <summary>
    /// 볼륨 입력 범위 확인
    /// </summary>
    /// <param name="volume">검사할 볼륨</param>
    /// <returns>0부터 1 사이의 값인지 여부</returns>
    static bool IsValidVolume ( float volume )
    {
        return volume >= 0f && volume <= 1f;
    }

    /// <summary>
    /// 공통 볼륨 범위 검사
    /// </summary>
    /// <param name="masterVolume">마스터 볼륨</param>
    /// <param name="bgmVolume">BGM 볼륨</param>
    /// <param name="sfxVolume">효과음 볼륨</param>
    /// <param name="reason">범위 오류 사유</param>
    /// <returns>모든 볼륨이 0부터 1 사이인지 여부</returns>
    public static bool ValidateVolumes (
        float masterVolume,
        float bgmVolume,
        float sfxVolume,
        out string reason )
    {
        reason = string.Empty;

        if ( IsValidVolume( masterVolume ) == false
            || IsValidVolume( bgmVolume ) == false
            || IsValidVolume( sfxVolume ) == false )
        {
            reason = "볼륨은 0부터 1 사이의 값이어야 합니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 선형 볼륨을 믹서 데시벨로 변환
    /// </summary>
    /// <param name="volume">0부터 1 사이의 볼륨</param>
    /// <returns>믹서에 적용할 데시벨</returns>
    float ToDecibel ( float volume )
    {
        if ( volume <= 0f )
            return MinDecibel;

        return Mathf.Max( MinDecibel, Mathf.Log10( volume ) * 20f );
    }

    #endregion

    #region ----- BGM -----

    /// <summary>
    /// BGM 즉시 재생 또는 같은 곡의 재생 위치 유지
    /// </summary>
    /// <param name="clip">재생할 BGM</param>
    /// <param name="reason">재생 실패 사유</param>
    /// <param name="loop">반복 재생 여부</param>
    /// <param name="restart">처음부터 재생할지 여부</param>
    /// <param name="volumeScale">곡의 개별 음량</param>
    /// <returns>재생 요청 성공 여부</returns>
    public bool TryPlayBgm (
        AudioClip clip,
        out string reason,
        bool loop = true,
        bool restart = false,
        float volumeScale = 1f )
    {
        if ( CanPlayClip( clip, out reason ) == false )
            return false;

        if ( IsValidVolume( volumeScale ) == false )
        {
            reason = "BGM 개별 음량은 0부터 1 사이의 값이어야 합니다.";
            return false;
        }

        bool keepPosition =
            restart == false
            && _bgm.clip == clip
            && _bgm.isPlaying;

        //진행 중인 전환을 끝내고 기본 출력 음량 사용
        ClearFade( );
        _bgmGain = volumeScale;
        _bgm.volume = _bgmGain;
        _bgm.loop = loop;

        if ( keepPosition == true )
            return true;

        _bgm.Stop( );
        _bgm.clip = clip;
        _bgm.Play( );
        return true;
    }

    /// <summary>
    /// 현재 BGM 감소 후 다음 BGM 증가 전환
    /// </summary>
    /// <param name="clip">전환할 BGM</param>
    /// <param name="duration">감소와 증가에 각각 사용할 시간</param>
    /// <param name="reason">전환 실패 사유</param>
    /// <param name="loop">반복 재생 여부</param>
    /// <param name="restart">처음부터 전환할지 여부</param>
    /// <param name="volumeScale">전환할 곡의 개별 음량</param>
    /// <returns>전환 요청 성공 여부</returns>
    public bool TryFadeBgm (
        AudioClip clip,
        float duration,
        out string reason,
        bool loop = true,
        bool restart = false,
        float volumeScale = 1f )
    {
        if ( CanPlayClip( clip, out reason ) == false )
            return false;

        if ( IsValidVolume( volumeScale ) == false )
        {
            reason = "BGM 개별 음량은 0부터 1 사이의 값이어야 합니다.";
            return false;
        }

        if ( ( duration >= 0f ) == false )
        {
            reason = "BGM 전환 시간은 0 이상이어야 합니다.";
            return false;
        }

        if ( duration == 0f )
            return TryPlayBgm( clip, out reason, loop, restart, volumeScale );

        //같은 대기 곡의 반복 요청으로 감소 시간을 다시 시작하지 않음
        if ( restart == false
            && _fadePhase == BgmFadePhase.FadeOut
            && _nextBgm == clip
            && _nextBgmLoop == loop )
        {
            _nextBgmGain = volumeScale;
            return true;
        }

        //현재 곡 유지 요청은 재생 위치를 보존하고 출력 음량만 복귀
        if ( restart == false
            && _bgm.clip == clip
            && _bgm.isPlaying == true )
        {
            ClearFade( );
            _bgm.loop = loop;

            _bgmGain = volumeScale;

            if ( _bgm.volume != _bgmGain )
                BeginFade( BgmFadePhase.FadeIn, duration );

            return true;
        }

        ClearFade( );

        if ( _bgm.isPlaying == false )
        {
            _bgm.Stop( );
            _bgm.clip = clip;
            _bgm.loop = loop;
            _bgmGain = volumeScale;
            _bgm.volume = 0f;
            _bgm.Play( );

            BeginFade( BgmFadePhase.FadeIn, duration );
            return true;
        }

        _nextBgm = clip;
        _nextBgmLoop = loop;
        _nextBgmGain = volumeScale;
        BeginFade( BgmFadePhase.FadeOut, duration );
        return true;
    }

    /// <summary>
    /// 현재 출력 음량을 기준으로 BGM 전환 단계 시작
    /// </summary>
    /// <param name="phase">시작할 전환 단계</param>
    /// <param name="duration">단계 진행 시간</param>
    void BeginFade ( BgmFadePhase phase, float duration )
    {
        _fadePhase = phase;
        _fadeDuration = duration;
        _fadeElapsed = 0f;
        _fadeStartVolume = _bgm.volume;
    }

    /// <summary>
    /// BGM 전환 상태와 대기 곡 정리
    /// </summary>
    void ClearFade ()
    {
        _fadePhase = BgmFadePhase.None;
        _nextBgm = null;
        _nextBgmLoop = false;
        _nextBgmGain = 1f;
        _fadeDuration = 0f;
        _fadeElapsed = 0f;
        _fadeStartVolume = 0f;
    }

    /// <summary>
    /// BGM 재생과 전환 중단
    /// </summary>
    public void StopBgm ()
    {
        if ( _isInitialized == false )
            return;

        StopBgmCore( );
    }

    /// <summary>
    /// BGM 출력 정리와 전환 음량 복구
    /// </summary>
    void StopBgmCore ()
    {
        ClearFade( );
        StopSource( _bgm );
        _bgmGain = 1f;

        if ( _bgm != null )
            _bgm.volume = 1f;
    }

    #endregion

    #region ----- 효과음 -----

    /// <summary>
    /// 식별자에 대응하는 공통 출력 재생
    /// </summary>
    /// <param name="id">사운드 식별자</param>
    /// <returns>재생 요청 성공 여부</returns>
    public bool Play ( SoundId id )
    {
        if ( TryGetSound( id, out SoundData data ) == false )
        {
            RecordCommon( id, "요청 거절: 관리자 초기화 전 또는 미사용/미등록 ID" );
            return false;
        }

        AudioClip clip = data.PickClip( );
        bool accepted;
        string reason;
        switch ( data.Type )
        {
            case SoundType.Bgm:
                accepted = TryFadeBgm( clip, _bgmTransition, out reason, volumeScale: data.Volume );
                break;
            case SoundType.Ui:
                accepted = TryPlayUiSfx( clip, out reason, data.Volume );
                break;
            case SoundType.LoopSfx:
                reason = $"반복 효과음은 SoundLoopPlayer로 시작/중단해야 합니다: {id}";
                Debug.LogError( reason, this );
                accepted = false;
                break;
            default:
                accepted = TryPlaySfx( clip, out reason, data.Volume );
                break;
        }
        RecordCommon( id, $"{( accepted ? "요청 접수" : "요청 거절: " + reason )}: {clip.name}"
            + $" / 종류={data.Type} / 개별 음량={data.Volume:0.##} / 로딩={clip.loadState}" );
        return accepted;
    }

    /// <summary>
    /// 에디터에서만 공통 ID 요청 결과를 보관
    /// </summary>
    /// <param name="id">요청한 ID</param>
    /// <param name="result">접수 또는 거절 사유</param>
    [System.Diagnostics.Conditional( "UNITY_EDITOR" )]
    void RecordCommon ( SoundId id, string result )
    {
#if UNITY_EDITOR
        _debugCommonRequests[id] = $"프레임 {Time.frameCount} / {Time.time:0.00}초 / {result}";
#endif
    }

    /// <summary>
    /// 식별자에 대응하는 위치 효과음 재생
    /// </summary>
    /// <param name="id">사운드 식별자</param>
    /// <param name="position">발생 위치</param>
    /// <returns>재생 요청 성공 여부</returns>
    public bool PlayAt ( SoundId id, Vector3 position )
    {
        return PlayAt( id, position, false );
    }

    /// <summary>
    /// 발생 위치를 보존하고 본인 효과음은 거리 감쇠 없이 재생
    /// </summary>
    /// <param name="id">사운드 식별자</param>
    /// <param name="position">발생 위치</param>
    /// <param name="ignoreDistance">거리와 방향에 관계없이 2D로 재생할지 여부</param>
    /// <returns>재생 요청 성공 여부</returns>
    public bool PlayAt ( SoundId id, Vector3 position, bool ignoreDistance )
    {
        AudioClip clip = GetSfx( id, out SoundData data, out string reason );
        if ( clip == null )
        {
            RecordWorld( id, $"요청 거절: {reason} / {( ignoreDistance ? "2D" : "3D" )} / 위치={position}" );
            return false;
        }

        _worldSounds.Play( clip, position, data.Volume, ignoreDistance );
        RecordWorld( id, $"요청 접수: {clip.name} / {( ignoreDistance ? "2D" : "3D" )} / 위치={position}"
            + $" / 개별 음량={data.Volume:0.##} / 로딩={clip.loadState}" );
        return true;
    }

    /// <summary>
    /// 에디터에서만 ID별 위치 출력 요청을 보관
    /// </summary>
    /// <param name="id">요청한 ID</param>
    /// <param name="result">접수 또는 거절 사유</param>
    [System.Diagnostics.Conditional( "UNITY_EDITOR" )]
    void RecordWorld ( SoundId id, string result )
    {
#if UNITY_EDITOR
        _debugWorldRequests[id] = $"프레임 {Time.frameCount} / {Time.time:0.00}초 / {result}";
#endif
    }

    /// <summary>
    /// 로컬 또는 위치 출력의 효과음 준비
    /// </summary>
    /// <param name="id">사운드 식별자</param>
    /// <param name="data">조회한 설정</param>
    /// <param name="clip">선택 음원</param>
    /// <returns>재생 준비 성공 여부</returns>
    public bool TryPrepareSfx ( SoundId id, out SoundData data, out AudioClip clip )
    {
        clip = GetSfx( id, out data, out _ );
        return clip != null;
    }

    /// <summary>
    /// 효과음 설정을 확인하고 재생할 클립 반환
    /// </summary>
    /// <param name="id">요청 ID</param>
    /// <param name="data">출력 설정</param>
    /// <param name="reason">거절 사유</param>
    /// <returns>재생할 클립, 실패하면 null</returns>
    public AudioClip GetSfx ( SoundId id, out SoundData data, out string reason )
    {
        data = null;
        reason = string.Empty;
        if ( IsReady == false )
        {
            reason = "사운드 초기화 또는 볼륨 적용 미완료/관리자 비활성";
            return null;
        }
        if ( AudioListener.pause == true )
        {
            reason = "플레이 오디오 일시정지";
            return null;
        }

        if ( TryGetSound( id, out data ) == false )
        {
            reason = id == SoundId.None ? "None ID는 미사용" : "사운드 맵에 등록되지 않은 ID: " + id;
            return null;
        }

        if ( data.Type != SoundType.Sfx && data.Type != SoundType.Footstep )
        {
            reason = "로컬/위치 효과음에 사용할 수 없는 종류: " + id;
            Debug.LogError( $"로컬/위치 효과음에 사용할 수 없는 종류: {id}", this );
            return null;
        }

        return data.PickClip( );
    }

    /// <summary>
    /// 소유 오브젝트에서 재생할 반복 효과음 준비
    /// </summary>
    /// <param name="id">반복 효과음 식별자</param>
    /// <param name="data">출력과 음량 설정</param>
    /// <returns>반복 재생 가능한 데이터인지 여부</returns>
    public bool TryPrepareLoop ( SoundId id, out SoundData data )
    {
        data = null;
        if ( IsReady == false || AudioListener.pause == true )
            return false;
        if ( TryGetSound( id, out data ) == false )
            return false;
        if ( data.Type != SoundType.LoopSfx )
        {
            Debug.LogError( $"반복 출력에는 LoopSfx가 필요합니다: {id}", this );
            return false;
        }
        return true;
    }

    /// <summary>
    /// 현재 조회표의 사운드 설정 조회
    /// </summary>
    /// <param name="id">사운드 식별자</param>
    /// <param name="data">조회한 설정</param>
    /// <returns>조회 성공 여부</returns>
    bool TryGetSound ( SoundId id, out SoundData data )
    {
        data = null;
        if ( id == SoundId.None || _isInitialized == false )
            return false;

        if ( _sounds.TryGetValue( id, out data ) == true )
            return true;

        Debug.LogError( $"사운드 맵에 등록되지 않은 ID: {id}", this );
        return false;
    }

    /// <summary>
    /// 일시정지와 관계없이 UI 효과음 재생
    /// </summary>
    /// <param name="clip">재생할 효과음</param>
    /// <param name="reason">재생 실패 사유</param>
    /// <param name="volumeScale">개별 효과음 음량, 0부터 1</param>
    /// <returns>재생 요청 성공 여부</returns>
    public bool TryPlayUiSfx (
        AudioClip clip,
        out string reason,
        float volumeScale = 1f )
    {
        return TryPlayOneShot(
            _uiSfx, clip, volumeScale, out reason );
    }

    /// <summary>
    /// 플레이 일시정지에 따르는 효과음 재생
    /// </summary>
    /// <param name="clip">재생할 효과음</param>
    /// <param name="reason">재생 실패 사유</param>
    /// <param name="volumeScale">개별 효과음 음량, 0부터 1</param>
    /// <returns>재생 요청 성공 여부</returns>
    public bool TryPlaySfx (
        AudioClip clip,
        out string reason,
        float volumeScale = 1f )
    {
        return TryPlayOneShot(
            _gameplaySfx, clip, volumeScale, out reason );
    }

    /// <summary>
    /// 개별 음량을 적용한 효과음 중첩 재생
    /// </summary>
    /// <param name="source">재생할 출력</param>
    /// <param name="clip">재생할 효과음</param>
    /// <param name="volumeScale">개별 효과음 음량</param>
    /// <param name="reason">재생 실패 사유</param>
    /// <returns>재생 요청 성공 여부</returns>
    bool TryPlayOneShot (
        AudioSource source,
        AudioClip clip,
        float volumeScale,
        out string reason )
    {
        if ( CanPlayClip( clip, out reason ) == false )
            return false;

        if ( IsValidVolume( volumeScale ) == false )
        {
            reason = "효과음 음량은 0부터 1 사이의 값이어야 합니다.";
            return false;
        }

        //정지 중 요청을 쌓아 복귀 직후 한꺼번에 재생하지 않음
        if ( AudioListener.pause == true
            && source.ignoreListenerPause == false )
        {
            reason = "플레이 사운드 일시정지 중에는 효과음을 재생할 수 없습니다.";
            return false;
        }

        source.PlayOneShot( clip, volumeScale );
        return true;
    }

    /// <summary>
    /// UI 효과음 전체 중단
    /// </summary>
    public void StopUiSfx ()
    {
        if ( _isInitialized == false )
            return;

        StopSource( _uiSfx );
    }

    /// <summary>
    /// 플레이 효과음 전체 중단
    /// </summary>
    public void StopSfx ()
    {
        if ( _isInitialized == false )
            return;

        StopGameplayCore( );
    }

    /// <summary>
    /// 전역, 위치와 로컬 플레이 효과음 정리
    /// </summary>
    void StopGameplayCore ()
    {
        StopSource( _gameplaySfx );
        _worldSounds.Clear( );
        OnGameplayStopped?.Invoke( );
    }

    #endregion

    #region ----- 일시정지/종료 -----

    /// <summary>
    /// 플레이 사운드 일시정지 또는 이전 전역 상태 복구
    /// </summary>
    /// <param name="isPaused">일시정지 요청 여부</param>
    /// <param name="reason">일시정지 적용 실패 사유</param>
    /// <returns>요청 처리 성공 여부</returns>
    public bool TrySetGameplayPaused (
        bool isPaused,
        out string reason )
    {
        reason = string.Empty;

        //재생 준비가 해제된 경우에도 이미 적용한 일시정지는 복구
        if ( isPaused == false )
        {
            RestoreListenerPause( );
            return true;
        }

        if ( _ownsListenerPause == true )
            return true;

        if ( IsReady == false )
        {
            reason = "초기화와 볼륨 적용 후 일시정지를 요청해야 합니다.";
            return false;
        }

        _previousListenerPause = AudioListener.pause;
        AudioListener.pause = true;
        _ownsListenerPause = true;
        return true;
    }

    /// <summary>
    /// 이 관리자가 변경하기 전의 전역 일시정지 상태 복구
    /// </summary>
    void RestoreListenerPause ()
    {
        if ( _ownsListenerPause == false )
            return;

        AudioListener.pause = _previousListenerPause;
        _ownsListenerPause = false;
        _previousListenerPause = false;
    }

    /// <summary>
    /// BGM과 모든 효과음 중단
    /// </summary>
    public void StopAll ()
    {
        if ( _isInitialized == false )
            return;

        StopAllCore( );
    }

    /// <summary>
    /// 모든 출력과 BGM 전환 상태 정리
    /// </summary>
    void StopAllCore ()
    {
        StopBgmCore( );
        StopSource( _uiSfx );
        StopGameplayCore( );
    }

    /// <summary>
    /// 출력 재생 중단과 연결된 클립 해제
    /// </summary>
    /// <param name="source">정리할 출력</param>
    void StopSource ( AudioSource source )
    {
        //파괴 순서에 따라 먼저 제거된 출력은 제외
        if ( source == null )
            return;

        source.Stop( );
        source.clip = null;
    }

    /// <summary>
    /// 재생 요청 차단 후 출력과 일시정지 상태 정리
    /// </summary>
    public void Shutdown ()
    {
        if ( _isInitialized == false )
            return;

        bool wasReady = _hasAppliedVolumes;
        _isInitialized = false;
        _hasAppliedVolumes = false;

        StopAllCore( );
        if ( wasReady == true )
            OnReadyChanged?.Invoke( false );
        RestoreListenerPause( );
    }

    #endregion

    /// <summary>
    /// 재생 준비 상태와 요청 클립 확인
    /// </summary>
    /// <param name="clip">재생할 클립</param>
    /// <param name="reason">재생 불가 사유</param>
    /// <returns>재생 요청 가능 여부</returns>
    bool CanPlayClip ( AudioClip clip, out string reason )
    {
        reason = string.Empty;

        if ( IsReady == false )
        {
            reason = "사운드 초기화와 볼륨 적용 후 재생해야 합니다.";
            return false;
        }

        if ( clip == null )
        {
            reason = "재생할 오디오 클립이 필요합니다.";
            return false;
        }

        return true;
    }

    private void OnDisable ()
    {
        //비활성화 중 전역 일시정지가 남지 않도록 종료 경로와 함께 정리
        Shutdown( );
    }

    private void OnDestroy ()
    {
        Shutdown( );
    }
}
