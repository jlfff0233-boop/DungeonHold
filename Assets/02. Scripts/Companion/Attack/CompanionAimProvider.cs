using UnityEngine;

/// <summary>
/// 동료 조준 제공자 - 전달받은 피격 중심으로 투사체 조준
/// </summary>
public class CompanionAimProvider : AimProvider
{
    Vector3 _targetPosition;    //발사 직전에 전달받은 피격 중심

    /// <summary>
    /// 이번 발사의 월드 조준 위치 설정
    /// </summary>
    /// <param name="position">피격 콜라이더 중심의 월드 위치</param>
    public void SetTargetPosition ( Vector3 position )
    {
        _targetPosition = position;
    }

    /// <summary>
    /// 조준 위치와 공격자 전방 기준 방향 조회
    /// </summary>
    /// <param name="owner">발사 주체</param>
    /// <returns>월드 조준 정보</returns>
    public override AimInfo GetAim ( GameObject owner )
    {
        return new AimInfo ( _targetPosition , owner.transform.forward );
    }
}
