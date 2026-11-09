using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VFX 관리자 - 공용 재생 서비스와 씬 효과 수명 관리
/// </summary>
[DisallowMultipleComponent]
public class VfxManager : MonoBehaviour
{
    /// <summary>
    /// 재생 중인 효과 목록
    /// </summary>
    List<VfxEffect> _effects = new List<VfxEffect>( );
    /// <summary>
    /// 반복 여부 딕셔너리(프리팹, 반복 여부)
    /// </summary>
    Dictionary<VfxEffect, bool> _prefabLoops =
        new Dictionary<VfxEffect, bool>( );
    bool _isReady;		//재생 준비 여부
    Transform _inactiveRoot;    //활성 프리팹도 초기화 전까지 비활성으로 생성

    /// <summary>
    /// 이전 효과 정리 후 재생 준비
    /// </summary>
    public void Init ()
    {
        StopAll( );
        _prefabLoops.Clear( );
        if ( _inactiveRoot == null )
        {
            GameObject root = new GameObject( "VFX Spawn Root" );
            root.SetActive( false );
            root.transform.SetParent( transform, false );
            _inactiveRoot = root.transform;
        }

        _isReady = true;
    }

    private void LateUpdate ()
    {
        for ( int i = _effects.Count - 1; i >= 0; i-- )
        {
            VfxEffect effect = _effects [ i ];
            if ( effect != null && effect.UpdateEffect(
                effect.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime ) == true )
                continue;

            RemoveEffect( i );
        }
    }

    /// <summary>
    /// 지정 위치에 단발 효과 생성
    /// </summary>
    /// <param name="prefab">효과 프리팹</param>
    /// <param name="position">발생 위치</param>
    /// <param name="rotation">발생 회전</param>
    /// <returns>생성한 효과, 미사용 또는 실패면 null</returns>
    public VfxEffect Play (
        VfxEffect prefab, Vector3 position, Quaternion rotation )
    {
        return Play( prefab, position, rotation, default );
    }

    /// <summary>
    /// 시간과 정리 범위를 지정한 위치 고정 단발 재생
    /// </summary>
    public VfxEffect Play (
        VfxEffect prefab, Vector3 position, Quaternion rotation, VfxOptions options )
    {
        if ( PreparePrefab( prefab, false, out bool looping ) == false )
            return null;

        return CreateEffect( prefab, position, rotation, null, options, looping );
    }

    /// <summary>
    /// 기준점을 추적하는 유지 효과 생성
    /// </summary>
    /// <param name="prefab">효과 프리팹</param>
    /// <param name="anchor">추적 기준점</param>
    /// <returns>생성한 효과</returns>
    public VfxEffect Follow ( VfxEffect prefab, Transform anchor )
    {
        return Follow( prefab, anchor, default );
    }

    /// <summary>
    /// 시간과 정리 범위를 지정한 추적 반복 재생
    /// </summary>
    public VfxEffect Follow ( VfxEffect prefab, Transform anchor, VfxOptions options )
    {
        if ( HasAnchor( anchor ) == false || PreparePrefab( prefab, true, out bool looping ) == false )
            return null;

        return CreateEffect( prefab, anchor.position, anchor.rotation, anchor, options, looping );
    }

    /// <summary>
    /// 기준점을 추적하는 단발 효과 재생
    /// </summary>
    public VfxEffect PlayFollow ( VfxEffect prefab, Transform anchor, VfxOptions options = default )
    {
        if ( HasAnchor( anchor ) == false || PreparePrefab( prefab, false, out bool looping ) == false )
            return null;
        return CreateEffect( prefab, anchor.position, anchor.rotation, anchor, options, looping );
    }

    /// <summary>
    /// 소유자가 지정한 위치 방식으로 단발 또는 반복 효과 재생
    /// </summary>
    public VfxEffect Play ( VfxEffect prefab, Transform point, bool follow, VfxOptions options )
    {
        if ( HasAnchor( point ) == false || PreparePrefab( prefab, null, out bool looping ) == false )
            return null;
        return CreateEffect( prefab, point.position, point.rotation, follow ? point : null, options, looping );
    }

    /// <summary>
    /// 공개 요청에서 필요한 기준점 확인
    /// </summary>
    bool HasAnchor ( Transform anchor )
    {
        if ( anchor != null )
            return true;
        Debug.LogError( "VFX 재생 기준점이 없습니다.", this );
        return false;
    }

    /// <summary>
    /// 프리팹과 요청한 재생 방식 확인
    /// </summary>
    /// <param name="prefab">효과 프리팹</param>
    /// <param name="requiredLoop">지정한 경우 해당 재생 방식만 허용</param>
    /// <param name="looping">프리팹의 반복 여부</param>
    /// <returns>사용 가능 여부</returns>
    bool PreparePrefab ( VfxEffect prefab, bool? requiredLoop, out bool looping )
    {
        looping = false;
        //미지정 효과는 선택적으로 사용하지 않음
        if ( prefab == null )
            return false;

        if ( _isReady == false || isActiveAndEnabled == false )
        {
            Debug.LogError( "VFX 관리자가 준비되지 않았습니다.", this );
            return false;
        }

        if ( _prefabLoops.TryGetValue( prefab, out looping ) == false )
        {
            looping = VfxEffect.IsMaintained( prefab.gameObject );
            _prefabLoops.Add( prefab, looping );
        }

        if ( requiredLoop.HasValue && looping != requiredLoop.Value )
        {
            Debug.LogError(
                $"VFX 재생 방식 불일치: {prefab.name} / 요청 반복={requiredLoop.Value}", prefab );
            return false;
        }

        return true;
    }

    /// <summary>
    /// 효과 준비와 등록 후 재생
    /// </summary>
    /// <param name="prefab">검사한 프리팹</param>
    /// <param name="position">발생 위치</param>
    /// <param name="rotation">발생 회전</param>
    /// <param name="anchor">추적 기준점</param>
    /// <returns>생성한 효과</returns>
    VfxEffect CreateEffect (
        VfxEffect prefab,
        Vector3 position,
        Quaternion rotation,
        Transform anchor, VfxOptions options, bool looping )
    {
        if ( options.UseUnscaledTime == true
            && prefab.GetComponentInChildren<TrailRenderer>( true ) != null )
        {
            Debug.LogError( "트레일 효과는 게임 시간 재생만 지원합니다.", prefab );
            return null;
        }

        //활성 프리팹도 비활성 부모 아래에서 생성해 초기화 전 콜백을 막음
        VfxEffect effect = Instantiate( prefab, _inactiveRoot, false );
        effect.gameObject.SetActive( false );
        effect.transform.SetParent( null, false );
        effect.transform.SetPositionAndRotation( position, rotation );
        effect.Init( anchor, options, looping );

        //활성화 전에 관리 목록에 등록
        _effects.Add( effect );
        effect.gameObject.SetActive( true );
        effect.Play( );
        return effect;
    }

    /// <summary>
    /// 소유자에 부착된 효과를 복제 없이 재생
    /// </summary>
    /// <param name="owner">부착 효과를 가진 소유자</param>
    /// <returns>재생한 효과 목록</returns>
    public VfxEffect[] PlayAttached ( Transform owner )
    {
        if ( _isReady == false || isActiveAndEnabled == false )
        {
            Debug.LogError( "VFX 관리자가 준비되지 않았습니다.", this );
            return System.Array.Empty<VfxEffect>( );
        }

        VfxEffect[] effects = owner.GetComponentsInChildren<VfxEffect>( true );
        for ( int i = 0; i < effects.Length; i++ )
        {
            VfxEffect effect = effects[i];

            //부착 효과도 비활성 상태에서 초기화
            if ( effect.Validate( out bool looping ) == false )
            {
                effects[i] = null;
                continue;
            }

            //위치는 부모가 갱신하므로 별도 추적 기준점을 사용하지 않음
            effect.Init( null, default, looping );

            _effects.Add( effect );
            effect.gameObject.SetActive( true );
            effect.Play( );
        }

        return effects;
    }

    /// <summary>
    /// 유지 효과 방출 중단
    /// </summary>
    public void StopMaintained ()
    {
        StopMaintainedCore( null );
    }

    /// <summary>
    /// 지정 범위의 반복 효과 방출 중단
    /// </summary>
    public void StopMaintained ( VfxScope scope )
    {
        StopMaintainedCore( scope );
    }

    /// <summary>
    /// 단발 추적은 유지하고 반복 효과만 중단
    /// </summary>
    void StopMaintainedCore ( VfxScope? scope )
    {
        foreach ( VfxEffect effect in _effects )
        {
            if ( effect != null && effect.IsLooping
                && ( scope.HasValue == false || effect.Scope == scope.Value ) )
                effect.Stop( );
        }
    }

    /// <summary>
    /// 모든 효과 즉시 제거
    /// </summary>
    public void StopAll ()
    {
        while ( _effects.Count > 0 )
            RemoveEffect( _effects.Count - 1 );
    }

    /// <summary>
    /// 지정 범위의 효과만 즉시 제거
    /// </summary>
    public void StopAll ( VfxScope scope )
    {
        for ( int i = _effects.Count - 1; i >= 0; i-- )
        {
            VfxEffect effect = _effects[i];
            if ( effect == null || effect.Scope == scope )
                RemoveEffect( i );
        }
    }

    /// <summary>
    /// 목록에서 해제한 뒤 효과 제거
    /// </summary>
    /// <param name="index">제거할 효과 번호</param>
    void RemoveEffect ( int index )
    {
        VfxEffect effect = _effects [ index ];
        _effects.RemoveAt( index );

        //씬 파괴 과정에서 먼저 제거된 객체도 목록은 정리
        if ( effect == null )
            return;

        effect.Clear( );
        effect.gameObject.SetActive( false );
        Destroy( effect.gameObject );
    }

    /// <summary>
    /// 새 요청 차단과 전체 출력 정리
    /// </summary>
    public void Shutdown ()
    {
        _isReady = false;
        StopAll( );
        _prefabLoops.Clear( );
    }

    private void OnDisable ()
    {
        //재사용은 Init과 캐릭터 상태 재연결을 명시적으로 수행
        Shutdown( );
    }

    private void OnDestroy ()
    {
        Shutdown( );
    }
}
