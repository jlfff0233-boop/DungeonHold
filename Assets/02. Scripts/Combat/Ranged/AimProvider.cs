
using UnityEngine;

/// <summary>
/// 조준 정보 - 목표 위치와 시점 기준 방향 전달
/// </summary>
public struct AimInfo
{
    /// <summary>
    /// 월드 기준 조준 위치
    /// </summary>
    public Vector3 Point { get; }

    /// <summary>
    /// 조준 기준의 전방 방향
    /// </summary>
    public Vector3 Direction { get; }

    /// <summary>
    /// 조준 정보 생성
    /// </summary>
    /// <param name="point">월드 조준 위치</param>
    /// <param name="direction">조준 기준 전방</param>
    public AimInfo ( Vector3 point , Vector3 direction )
    {
        Point = point;
        Direction = direction;
    }
}

/// <summary>
/// 조준 제공자 - 발사에 사용할 월드 조준 정보 제공
/// </summary>
public abstract class AimProvider : MonoBehaviour
{
    /// <summary>
    /// 현재 조준 정보 조회
    /// </summary>
    /// <param name="owner">발사 주체</param>
    /// <returns>월드 조준 정보</returns>
    public abstract AimInfo GetAim ( GameObject owner );
}
