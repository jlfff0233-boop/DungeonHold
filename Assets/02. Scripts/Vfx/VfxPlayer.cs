using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 월드 연출 요청과 소유 효과 정리
/// </summary>
[DisallowMultipleComponent]
public class VfxPlayer : MonoBehaviour
{
    [SerializeField] VfxEffect _effect;		//재생할 월드 효과
    [SerializeField] Transform _point;		//미지정이면 이 오브젝트 기준
    [SerializeField] bool _follow;		//재생 후에도 기준점 추적
    [SerializeField] VfxOptions _options = new( VfxScope.Presentation, true );		//시간과 일괄 정리 범위

    VfxManager _manager;		//전달받은 공용 서비스
    List<VfxEffect> _effects = new( );		//이 요청자가 생성한 효과

    /// <summary>
    /// 이전 효과 정리 후 서비스 연결
    /// </summary>
    public void Init ( VfxManager manager )
    {
        Clear( );
        _manager = manager;
    }

    /// <summary>
    /// 화면이나 연출 주체가 명시적으로 재생 요청
    /// </summary>
    public VfxEffect Play ()
    {
        if ( isActiveAndEnabled == false || _effect == null )
            return null;
        if ( _manager == null )
        {
            Debug.LogError( "VfxPlayer.Init 이후 재생을 요청하세요.", this );
            return null;
        }
        _effects.RemoveAll( item => item == null );
        VfxEffect effect = _manager.Play( _effect, _point != null ? _point : transform, _follow, _options );
        if ( effect != null )
            _effects.Add( effect );
        return effect;
    }

    /// <summary>
    /// 이 요청자가 생성한 효과만 정리
    /// </summary>
    public void Clear ()
    {
        VfxEffect[] effects = _effects.ToArray( );
        _effects.Clear( );
        foreach ( VfxEffect effect in effects )
        {
            if ( effect != null )
                effect.Clear( );
        }
    }

    private void OnDisable ()
    {
        Clear( );
    }
}
