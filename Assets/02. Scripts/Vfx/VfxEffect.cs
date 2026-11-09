using UnityEngine;

/// <summary>
/// VFX 효과 - 파티클/트레일 재생과 대상 추적
/// </summary>
[RequireComponent( typeof( ParticleSystem ) )]
public class VfxEffect : MonoBehaviour
{
    ParticleSystem _particles;		//루트 파티클
    TrailRenderer[] _trails;     //관리할 트레일
    VfxOrbit _orbit;		//선택적으로 사용하는 공전 표현
    Transform _anchor;		//추적 기준점
    bool _isFollowing;		//기준점 추적 여부
    bool _isLooping;		//유지 효과 여부
    VfxOptions _options;		//요청의 시간과 정리 범위

    /// <summary>
    /// 기준점 추적 여부
    /// </summary>
    public bool IsFollowing => _isFollowing;
    public bool IsLooping => _isLooping;
    public VfxScope Scope => _options.Scope;
    public bool UseUnscaledTime => _options.UseUnscaledTime;

    /// <summary>
    /// 활성화 전에 재생과 추적 정보 준비
    /// </summary>
    /// <param name="anchor">추적 기준점, 없으면 위치 고정</param>
    public void Init ( Transform anchor )
    {
        Init( anchor, default, IsMaintained( gameObject ) );
    }

    /// <summary>
    /// 활성화 전에 추적과 출력 설정 준비
    /// </summary>
    /// <param name="anchor">추적 기준점</param>
    /// <param name="options">시간과 정리 범위</param>
    /// <param name="looping">유지 효과 여부</param>
    public void Init ( Transform anchor, VfxOptions options, bool looping )
    {
        _particles = GetComponent<ParticleSystem>( );
        _trails = GetComponentsInChildren<TrailRenderer>( true );
        _orbit = GetComponentInChildren<VfxOrbit>( true );
        _anchor = anchor;
        _isFollowing = anchor != null;
        _options = options;
        _isLooping = looping;

        ParticleSystem [ ] systems = GetComponentsInChildren<ParticleSystem>( true );
        foreach ( ParticleSystem system in systems )
        {
            //재생과 제거는 공용 모듈이 관리하며 요청의 시간을 사용
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.useUnscaledTime = options.UseUnscaledTime;
        }

        foreach ( TrailRenderer trail in _trails )
        {
            //활성화 후 Play에서 방출을 시작하고 제거는 매니저가 담당
            trail.autodestruct = false;
            trail.emitting = false;
            trail.Clear( );
        }
    }

    /// <summary>
    /// 파티클과 트레일 재생
    /// </summary>
    public void Play ()
    {
        _particles.Play( true );

        foreach ( TrailRenderer trail in _trails )
            trail.emitting = true;
    }

    /// <summary>
    /// 추적과 공전 반영 후 출력 수명 확인
    /// </summary>
    /// <param name="deltaTime">요청한 시간 기준의 경과 시간</param>
    /// <returns>재생 유지 여부</returns>
    public bool UpdateEffect ( float deltaTime )
    {
        if ( _isFollowing == true )
        {
            if ( _anchor == null || _anchor.gameObject.activeInHierarchy == false )
            {
                Stop( );
            }
            else if ( deltaTime > 0f )
            {
                transform.SetPositionAndRotation( _anchor.position, _anchor.rotation );
            }
        }

        //기준점 위치를 반영한 뒤 자식 효과를 회전
        if ( _orbit != null && deltaTime > 0f )
            _orbit.UpdateOrbit( deltaTime );

        if ( _particles.IsAlive( true ) == true )
            return true;

        foreach ( TrailRenderer trail in _trails )
        {
            //방출 중에는 대기하고 중단 후에는 남은 선이 사라질 때까지 유지
            if ( trail.emitting == true || trail.positionCount > 1 )
                return true;
        }

        return false;
    }

    /// <summary>
    /// 방출과 공전 중단 후 기존 추적 유지
    /// </summary>
    public void StopEmission ()
    {
        _particles.Stop( true, ParticleSystemStopBehavior.StopEmitting );

        foreach ( TrailRenderer trail in _trails )
        {
            //이미 중단된 트레일에 끝점을 다시 추가하지 않음
            if ( trail.emitting == false )
                continue;

            //자동 갱신 전에 종료되더라도 마지막 위치까지 연결
            if ( trail.positionCount > 0 )
                trail.AddPosition( trail.transform.position );

            trail.emitting = false;
        }

        _orbit = null;
    }

    /// <summary>
    /// 방출과 추적 중단 후 잔상 분리
    /// </summary>
    public void Stop ()
    {
        //투사체가 LateUpdate 전에 종료돼도 마지막 위치 반영
        if ( _isFollowing == true && _anchor != null )
            transform.SetPositionAndRotation( _anchor.position, _anchor.rotation );

        StopEmission( );

        _isFollowing = false;
        _anchor = null;

        //소유자가 제거돼도 잔상은 현재 월드 위치에서 유지
        transform.SetParent( null, true );
    }

    /// <summary>
    /// 추적과 공전, 남은 출력 즉시 정리
    /// </summary>
    public void Clear ()
    {
        _isFollowing = false;
        _anchor = null;
        _orbit = null;
        _particles.Stop( true, ParticleSystemStopBehavior.StopEmittingAndClear );

        foreach ( TrailRenderer trail in _trails )
        {
            trail.emitting = false;
            trail.Clear( );
        }
    }

    /// <summary>
    /// 부착 효과의 초기화 조건과 유지 여부 확인
    /// </summary>
    /// <param name="looping">유지 효과 여부</param>
    /// <returns>사용 가능 여부</returns>
    public bool Validate ( out bool looping )
    {
        looping = false;

        if ( gameObject.activeSelf == true )
        {
            Debug.LogError( "부착 VFX는 재생 요청 전에 비활성 상태여야 합니다.", this );
            return false;
        }

        looping = IsMaintained( gameObject );
        return true;
    }

    /// <summary>
    /// 소유자가 중단해야 하는 유지 효과인지 확인
    /// </summary>
    /// <param name="root">효과 루트</param>
    /// <returns>반복 파티클 또는 트레일 포함 여부</returns>
    public static bool IsMaintained ( GameObject root )
    {
        foreach ( ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>( true ) )
        {
            if ( system.main.loop == true )
                return true;
        }

        return root.GetComponentInChildren<TrailRenderer>( true ) != null;
    }
}
