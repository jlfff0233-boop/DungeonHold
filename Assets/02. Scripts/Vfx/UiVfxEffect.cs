using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Linq;

/// <summary>
/// Canvas 효과의 재생 준비와 완료 시간 관리
/// </summary>
[RequireComponent( typeof( RectTransform ) )]
public class UiVfxEffect : MonoBehaviour
{
    [SerializeField] bool _looping;		//화면에서 직접 정리할 반복 효과
    [SerializeField, Min( 0.01f )] float _duration = 1f;		//단발 효과의 완료 시간
    float _elapsed;		//현재 재생 시간
    bool _useUnscaledTime;		//이번 출력의 시간 기준

    public bool Looping => _looping;
    public float Duration => _duration;
    public bool UseUnscaledTime => _useUnscaledTime;

    /// <summary>
    /// 활성화 전에 애니메이션 시간과 입력 차단 해제 설정
    /// </summary>
    public void Init ( bool useUnscaledTime )
    {
        _elapsed = 0f;
        _useUnscaledTime = useUnscaledTime;
        foreach ( Animator animator in GetComponentsInChildren<Animator>( true ) )
        {
            animator.updateMode = useUnscaledTime ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        foreach ( Graphic graphic in GetComponentsInChildren<Graphic>( true ) )
            graphic.raycastTarget = false;
        foreach ( CanvasGroup group in GetComponentsInChildren<CanvasGroup>( true ) )
        {
            group.blocksRaycasts = false;
            group.interactable = false;
        }
    }

    /// <summary>
    /// 지정한 시간 기준으로 재생 유지 여부 확인
    /// </summary>
    public bool UpdateEffect ( float deltaTime )
    {
        _elapsed += deltaTime;
        return _looping || _elapsed < _duration;
    }

    /// <summary>
    /// 비활성 Canvas 효과와 명시적인 완료 조건 확인
    /// </summary>
    public bool Validate ()
    {
        if ( gameObject.activeSelf || transform is not RectTransform || ( _looping == false && _duration <= 0f ) )
        {
            Debug.LogError( "UI 효과는 비활성 RectTransform 루트와 양수 단발 시간을 사용하세요.", this );
            return false;
        }
        if ( GetComponentInChildren<Graphic>( true ) == null || GetComponentInChildren<ParticleSystem>( true ) != null
            || GetComponentInChildren<Renderer>( true ) != null
            || GetComponentInChildren<Animation>( true ) != null
            || GetComponentsInChildren<Canvas>( true ).Length > 0
            || GetComponentsInChildren<MonoBehaviour>( true ).Any( item => item is IEventSystemHandler ) )
        {
            Debug.LogError( "UI 효과는 Graphic/Animator로 구성하고 Canvas, 월드 렌더러와 입력 모듈은 제외하세요.", this );
            return false;
        }
        return true;
    }
}
