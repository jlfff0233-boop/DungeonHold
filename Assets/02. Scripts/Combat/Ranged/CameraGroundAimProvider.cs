using UnityEngine;

/// <summary>
/// 지면 조준 제공자 - 화면 중앙의 차폐를 반영한 지면 위치 조회
/// </summary>
public class CameraGroundAimProvider : MonoBehaviour
{
    [Header ( "----- 조준 -----" )]
    [SerializeField] Camera _aimCamera;    //실제 플레이 카메라
    [SerializeField] float _maxDistance = 50f;    //카메라 탐색 거리
    [SerializeField] LayerMask _groundMask;    //조준 가능한 지면 레이어
    [SerializeField] LayerMask _obstacleMask;    //지면 선택을 막는 벽 레이어

    /// <summary>
    /// 가장 가까운 충돌이 지면인 경우 목표 위치 반환
    /// </summary>
    /// <param name="owner">사용 주체</param>
    /// <param name="point">선택한 지면 위치</param>
    /// <returns>유효 지면을 찾았는지 여부</returns>
    public bool TryGetPoint ( GameObject owner , out Vector3 point )
    {
        point = default;

        Ray ray = _aimCamera.ViewportPointToRay (
            new Vector3 ( 0.5f , 0.5f , 0f ) );

        int mask = _groundMask.value | _obstacleMask.value;

        RaycastHit [ ] hits = Physics.RaycastAll (
            ray , _maxDistance , mask , QueryTriggerInteraction.Ignore );

        Collider nearestCollider = null;
        float nearestDistance = float.PositiveInfinity;

        foreach ( RaycastHit hit in hits )
        {
            Collider collider = hit.collider;

            if ( collider.transform.IsChildOf ( owner.transform ) == true )
                continue;

            if ( hit.distance >= nearestDistance )
                continue;

            nearestCollider = collider;
            nearestDistance = hit.distance;
            point = hit.point;
        }

        if ( nearestCollider == null )
            return false;

        //벽 너머 지면이 아니라 가장 먼저 충돌한 면을 판정
        int layerBit = 1 << nearestCollider.gameObject.layer;
        return ( _groundMask.value & layerBit ) != 0;
    }
}
