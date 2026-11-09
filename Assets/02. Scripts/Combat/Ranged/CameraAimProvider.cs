using UnityEngine;

/// <summary>
/// 카메라 조준 제공자 - 화면 중앙의 적/지형 조준점 계산
/// </summary>
public class CameraAimProvider : AimProvider
{
    [Header ( "----- 조준 -----" )]
    [SerializeField] Camera _aimCamera;    //실제 플레이 카메라
    [SerializeField] float _maxDistance = 50f;    //최대 조준 거리
    [SerializeField] LayerMask _aimMask;    //적 피격 영역과 지형 레이어

    /// <summary>
    /// 현재 화면 중앙의 조준 정보 조회
    /// </summary>
    /// <param name="owner">발사 주체</param>
    /// <returns>월드 조준 정보</returns>
    public override AimInfo GetAim ( GameObject owner )
    {
        Ray ray = _aimCamera.ViewportPointToRay ( new Vector3 ( 0.5f , 0.5f , 0f ) );

        Vector3 point = ray.GetPoint ( _maxDistance );
        float nearestDistance = _maxDistance;

        RaycastHit [ ] hits = Physics.RaycastAll (
            ray , _maxDistance , _aimMask , QueryTriggerInteraction.Collide );

        foreach ( RaycastHit hit in hits )
        {
            Collider collider = hit.collider;

            if ( collider.transform.IsChildOf ( owner.transform ) == true )
                continue;

            //구역 감지용 Trigger는 조준을 가리지 않음
            if ( collider.isTrigger == true
                && collider.GetComponentInParent<IDamageable> ( ) == null )
                continue;

            if ( hit.distance > nearestDistance )
                continue;

            nearestDistance = hit.distance;
            point = hit.point;
        }

        return new AimInfo ( point , ray.direction );
    }
}
