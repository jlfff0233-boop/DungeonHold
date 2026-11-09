using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 위치 사운드 재생기 - 단발 출력 생성과 정리
/// </summary>
public class WorldSoundPlayer
{
    Transform _root; //출력 생성 루트
    AudioMixerGroup _output; //SFX 믹서
    float _maxDistance; //감쇠 거리
    List<AudioSource> _sources = new List<AudioSource>( ); //재생 중 출력

#if UNITY_EDITOR
    /// <summary>
    /// 에디터에서 조회할 현재 위치 출력
    /// </summary>
    public IReadOnlyList<AudioSource> DebugSources => _sources;
#endif

    /// <summary>
    /// 위치 출력 설정 연결
    /// </summary>
    /// <param name="root">생성 루트</param>
    /// <param name="output">SFX 믹서</param>
    /// <param name="maxDistance">감쇠 거리</param>
    public WorldSoundPlayer ( Transform root, AudioMixerGroup output, float maxDistance )
    {
        _root = root;
        _output = output;
        _maxDistance = maxDistance;
    }

    /// <summary>
    /// 준비한 음원을 지정 위치에서 재생
    /// </summary>
    /// <param name="clip">재생 음원</param>
    /// <param name="position">발생 위치</param>
    /// <param name="volume">개별 음량</param>
    public void Play ( AudioClip clip, Vector3 position, float volume )
    {
        Play( clip, position, volume, false );
    }

    /// <summary>
    /// 캐릭터 수명과 독립된 출력에 이번 효과음의 거리 감쇠 여부를 적용
    /// </summary>
    /// <param name="clip">재생 음원</param>
    /// <param name="position">발생 위치</param>
    /// <param name="volume">개별 음량</param>
    /// <param name="ignoreDistance">거리와 방향에 관계없이 2D로 재생할지 여부</param>
    public void Play ( AudioClip clip, Vector3 position, float volume, bool ignoreDistance )
    {
        GameObject instance = new GameObject( "WorldSfx" );
        instance.transform.SetParent( _root, false );
        instance.transform.position = position;

        AudioSource source = instance.AddComponent<AudioSource>( );
        source.playOnAwake = false;
        source.loop = false;
        source.ignoreListenerPause = false;
        source.outputAudioMixerGroup = _output;
        source.spatialBlend = ignoreDistance ? 0f : 1f;
        source.dopplerLevel = 0f;
        source.minDistance = 1f;
        source.maxDistance = _maxDistance;
        source.clip = clip;
        source.volume = volume;
        source.Play( );
        _sources.Add( source );
    }

    /// <summary>
    /// 자연 종료한 출력 제거
    /// </summary>
    public void Update ()
    {
        if ( AudioListener.pause == true )
            return;

        for ( int i = _sources.Count - 1; i >= 0; i-- )
        {
            AudioSource source = _sources[i];
            if ( source.isPlaying == true )
                continue;

            _sources.RemoveAt( i );
            Object.Destroy( source.gameObject );
        }
    }

    /// <summary>
    /// 위치 출력 전체 정리
    /// </summary>
    public void Clear ()
    {
        for ( int i = _sources.Count - 1; i >= 0; i-- )
        {
            AudioSource source = _sources[i];
            _sources.RemoveAt( i );

            //관리자 계층 파괴 중 먼저 제거된 출력은 제외
            if ( source == null )
                continue;

            source.Stop( );
            Object.Destroy( source.gameObject );
        }
    }
}
