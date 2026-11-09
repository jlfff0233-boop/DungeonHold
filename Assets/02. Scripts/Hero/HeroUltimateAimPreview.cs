using UnityEngine;

/// <summary>
/// 궁극기 조준 표시 - 후보 위치와 시전 가능 여부를 원형 선으로 표시
/// </summary>
public class HeroUltimateAimPreview : MonoBehaviour
{
    const int SegmentCount = 64;    //원형 선의 분할 수

    [Header ( "----- 표시 -----" )]
    [SerializeField] LineRenderer _lineRenderer;    //범위 테두리
    [SerializeField] float _heightOffset = 0.04f;    //바닥 겹침 방지 높이
    [SerializeField] Color _validColor = Color.green;    //시전 가능 색상
    [SerializeField] Color _invalidColor = Color.red;    //시전 불가 색상

    HeroUltimateAim _aim;    //표시할 조준 상태
    Vector3 [ ] _positions;    //재사용할 원형 선 좌표
    MaterialPropertyBlock _properties;    //이 표시 객체의 색상
    int _baseColorId;    //Unlit 머티리얼의 기본 색상 프로퍼티

    /// <summary>
    /// 조준 상태 연결과 표시 구성 초기화
    /// </summary>
    /// <param name="aim">히어로의 궁극기 조준 상태</param>
    public void Init ( HeroUltimateAim aim )
    {
        _aim = aim;
        _positions = new Vector3 [ SegmentCount ];
        _properties = new MaterialPropertyBlock ( );
        _baseColorId = Shader.PropertyToID ( "_BaseColor" );

        _lineRenderer.useWorldSpace = true;
        _lineRenderer.loop = true;
        _lineRenderer.positionCount = SegmentCount;
        _lineRenderer.enabled = false;

        //활성 프리팹의 최초 OnEnable은 Init 이전이므로 여기서 처음 구독
        _aim.OnChanged += Refresh;
        Refresh ( );
    }

    private void OnEnable ( )
    {
        //최초 Instantiate의 OnEnable은 Init 이전에 호출됨
        if ( _aim == null )
            return;

        _aim.OnChanged += Refresh;
        Refresh ( );
    }

    /// <summary>
    /// 현재 조준 정보로 범위 위치와 색상 갱신
    /// </summary>
    void Refresh ( )
    {
        bool visible = _aim.IsAiming && _aim.HasGroundPoint;
        _lineRenderer.enabled = visible;

        if ( visible == false )
            return;

        //높이 보정은 표현에만 적용하고 실제 시전 위치는 유지
        Vector3 center = _aim.Point + Vector3.up * _heightOffset;
        float radius = _aim.Radius;

        for ( int i = 0; i < SegmentCount; i++ )
        {
            float angle = i * Mathf.PI * 2f / SegmentCount;
            _positions [ i ] = center + new Vector3 (
                Mathf.Cos ( angle ) * radius, 0f, Mathf.Sin ( angle ) * radius );
        }

        _lineRenderer.SetPositions ( _positions );

        //공유 머티리얼을 변경하거나 객체별 머티리얼을 생성하지 않음
        Color color = _aim.IsValid ? _validColor : _invalidColor;
        _properties.SetColor ( _baseColorId, color );
        _lineRenderer.SetPropertyBlock ( _properties );
    }

    private void OnDisable ( )
    {
        _lineRenderer.enabled = false;

        if ( _aim != null )
            _aim.OnChanged -= Refresh;
    }
}
