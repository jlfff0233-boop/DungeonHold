using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화면의 Canvas 효과 생성과 개별 수명 관리
/// </summary>
[DisallowMultipleComponent]
public class UiVfxPlayer : MonoBehaviour
{
    [SerializeField] UiVfxEffect _effect;		//재생할 UI 효과
    [SerializeField] RectTransform _point;		//효과를 둘 UI 부모
    [SerializeField] bool _useUnscaledTime = true;		//일시정지 중에도 재생
    List<UiVfxEffect> _effects = new( );		//이 화면이 소유한 출력

    /// <summary>
    /// 화면 구성 완료 후 Canvas 아래 명시적으로 재생
    /// </summary>
    public UiVfxEffect Play ()
    {
        if ( isActiveAndEnabled == false || _effect == null )
            return null;
        RectTransform point = _point != null ? _point : transform as RectTransform;
        Canvas canvas = point != null ? point.GetComponentInParent<Canvas>( ) : null;
        if ( point == null || point.gameObject.activeInHierarchy == false
            || canvas == null || canvas.isActiveAndEnabled == false )
        {
            Debug.LogError( "화면을 준비한 뒤 활성 Canvas 아래의 UI 효과 기준점에서 재생하세요.", this );
            return null;
        }
        if ( _effect.Validate( ) == false )
            return null;

        UiVfxEffect effect = Instantiate( _effect, point, false );
        effect.Init( _useUnscaledTime );
        _effects.Add( effect );
        effect.gameObject.SetActive( true );
        return effect;
    }

    private void LateUpdate ()
    {
        for ( int i = _effects.Count - 1; i >= 0; i-- )
        {
            UiVfxEffect effect = _effects[i];
            if ( effect != null && effect.UpdateEffect(
                effect.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime ) )
                continue;
            _effects.RemoveAt( i );
            Remove( effect );
        }
    }

    /// <summary>
    /// 이 화면의 출력만 즉시 정리
    /// </summary>
    public void Clear ()
    {
        UiVfxEffect[] effects = _effects.ToArray( );
        _effects.Clear( );
        foreach ( UiVfxEffect effect in effects )
            Remove( effect );
    }

    /// <summary>
    /// 입력과 갱신을 먼저 막고 출력 제거
    /// </summary>
    void Remove ( UiVfxEffect effect )
    {
        if ( effect == null )
            return;
        effect.gameObject.SetActive( false );
        Destroy( effect.gameObject );
    }

    private void OnDisable ()
    {
        Clear( );
    }
}
