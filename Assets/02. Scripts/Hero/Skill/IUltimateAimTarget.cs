using UnityEngine;

/// <summary>
/// 궁극기 범위 지정 - 후보 조회와 확정 위치 전달
/// </summary>
public interface IUltimateAimTarget
{
    /// <summary>
    /// 피해 반경
    /// </summary>
    float AimRadius { get; }

    /// <summary>
    /// 지면과 시전 거리를 만족하는 후보 위치 조회
    /// </summary>
    /// <param name="owner">궁극기 사용자</param>
    /// <param name="point">후보 위치</param>
    /// <param name="hasGroundPoint">지면을 찾았는지 여부</param>
    /// <returns>유효한 위치인지 여부</returns>
    bool TryGetAimPoint (
        GameObject owner , out Vector3 point , out bool hasGroundPoint );

    /// <summary>
    /// 다음 사용 준비에서 소비할 위치 지정
    /// </summary>
    /// <param name="point">확정 위치</param>
    void SetAimPoint ( Vector3 point );

    /// <summary>
    /// 미사용 확정 위치 해제
    /// </summary>
    void ClearAimPoint ( );
}
