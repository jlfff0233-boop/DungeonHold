using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 VFX - 단발 표현과 상태 효과 연결
/// </summary>
public class CharacterVfx : MonoBehaviour
{
    [SerializeField] Transform _point;		//공통 기준점, 미지정이면 루트
    [SerializeField] Transform _hitPoint;		//피격 기준점, 미지정이면 공통 기준점
    [SerializeField] Transform _stunPoint;		//기절 기준점, 미지정이면 공통 기준점
    [SerializeField] VfxEffect _hit;		//피격 단발 효과
    [SerializeField] VfxEffect _death;		//사망 단발 효과
    [SerializeField] VfxEffect _revive;		//부활 단발 효과
    [SerializeField] VfxEffect _burn;		//화상 유지 효과
    [SerializeField] VfxEffect _slow;		//감속 유지 효과
    [SerializeField] VfxEffect _stun;		//기절 유지 효과

    VfxManager _manager;		//씬 재생 서비스
    VfxEffect _burnEffect;		//재생 중인 화상
    VfxEffect _slowEffect;		//재생 중인 감속
    VfxEffect _stunEffect;		//재생 중인 기절
    List<VfxEffect> _tails = new List<VfxEffect>( );		//자연 종료 중인 유지 효과

    /// <summary>
    /// 기준 위치
    /// </summary>
    Transform Point => _point != null ? _point : transform;

    /// <summary>
    /// 이전 유지 효과 정리 후 서비스 연결
    /// </summary>
    /// <param name="manager">씬 재생 서비스</param>
    public void Init ( VfxManager manager )
    {
        Clear ( );
        _manager = manager;
    }

    /// <summary>
    /// 공통 기준점에서 단발 효과 재생
    /// </summary>
    /// <param name="prefab">효과 프리팹</param>
    public void Play ( VfxEffect prefab )
    {
        Play ( prefab , Point );
    }

    /// <summary>
    /// 지정한 기준점에서 단발 효과 재생
    /// </summary>
    /// <param name="prefab">효과 프리팹</param>
    /// <param name="point">재생 기준점</param>
    void Play ( VfxEffect prefab , Transform point )
    {
        if ( isActiveAndEnabled == false )
            return;

        _manager.Play ( prefab , point.position , point.rotation );
    }

    /// <summary>
    /// 피격 기준점에서 효과 재생
    /// </summary>
    public void PlayHit ( )
    {
        //피격 기준점이 없으면 기존 출력 위치 유지
        Transform point = ( _hitPoint != null ) ? _hitPoint : Point;
        Play ( _hit , point );
    }

    /// <summary>
    /// 사망 위치 효과 요청
    /// </summary>
    public void PlayDeath ( )
    {
        Play ( _death );
    }

    /// <summary>
    /// 부활 위치 효과 요청
    /// </summary>
    public void PlayRevive ( )
    {
        Play ( _revive );
    }

    /// <summary>
    /// 현재 상태에 맞게 유지 효과 반영
    /// </summary>
    /// <param name="effects">상태이상 모듈</param>
    public void RefreshEffects ( StatusEffectController effects )
    {
        if ( isActiveAndEnabled == false )
            return;

        //자연 종료가 완료된 효과의 참조 제거
        _tails.RemoveAll( effect => effect == null );

        RefreshEffect ( ref _burnEffect , _burn , effects.IsBurning , Point );
        RefreshEffect ( ref _slowEffect , _slow , effects.SlowCount > 0 , Point );

        //기절만 캐릭터별 전용 기준점 사용
        Transform stunPoint = _stunPoint != null ? _stunPoint : Point;
        RefreshEffect ( ref _stunEffect , _stun , effects.IsStunned , stunPoint );
    }

    /// <summary>
    /// 현재 상태에 맞춰 유지 효과 생성/중단
    /// </summary>
    /// <param name="instance">현재 재생 객체</param>
    /// <param name="prefab">유지 효과 프리팹</param>
    /// <param name="active">현재 상태 활성 여부</param>
    /// <param name="point">추적 기준점</param>
    void RefreshEffect (
        ref VfxEffect instance ,
        VfxEffect prefab ,
        bool active ,
        Transform point )
    {
        if ( active == true )
        {
            if ( instance == null )
                instance = _manager.Follow ( prefab , point );
            return;
        }

        if ( instance == null )
            return;

        VfxEffect effect = instance;
        instance = null;
        //방출은 끝내되 사망 시 정리할 참조는 보관
        effect.StopEmission ( );
        _tails.Add( effect );
    }

    /// <summary>
    /// 현재 유지 효과와 잔여 출력 즉시 정리
    /// </summary>
    public void Clear ( )
    {
        ClearEffect ( ref _burnEffect );
        ClearEffect ( ref _slowEffect );
        ClearEffect ( ref _stunEffect );

        //상태 만료로 분리된 잔여 출력도 함께 정리
        foreach ( VfxEffect effect in _tails )
        {
            if ( effect != null )
                effect.Clear ( );
        }

        _tails.Clear ( );
    }

    /// <summary>
    /// 보관 참조 해제 후 파티클 정리
    /// </summary>
    /// <param name="instance">정리할 효과</param>
    void ClearEffect ( ref VfxEffect instance )
    {
        if ( instance == null )
            return;

        VfxEffect effect = instance;
        instance = null;
        effect.Clear ( );
    }

    private void OnDisable ( )
    {
        Clear ( );
    }
}
