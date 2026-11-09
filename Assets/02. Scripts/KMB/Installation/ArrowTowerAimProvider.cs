using UnityEngine;

// 타워가 선택한 조준점을 공용 투사체 실행자에게 전달
public class ArrowTowerAimProvider : AimProvider
{
    [SerializeField] private Transform _firePoint;

    private Vector3 _targetPoint;

    public void SetTargetPoint(Vector3 targetPoint)
    {
        _targetPoint = targetPoint;
    }

    public override AimInfo GetAim(GameObject owner)
    {
        return new AimInfo(
            _targetPoint,
            _firePoint.forward);
    }
}