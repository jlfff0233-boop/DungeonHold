using UnityEngine;

/// <summary>
/// 넉백 이동 계약 - 강제 이동 가능 여부와 실제 위치 적용
/// </summary>
public interface IKnockbackMover
{
    /// <summary>
    /// 현재 강제 이동 처리 가능 여부
    /// </summary>
    bool CanKnockback { get; }

    /// <summary>
    /// 이번 프레임의 강제 이동 적용
    /// </summary>
    /// <param name="displacement">요청 수평 이동량</param>
    /// <param name="deltaTime">중력 갱신에 사용할 프레임 시간</param>
    /// <returns>충돌 없이 넉백을 계속할 수 있는지 여부</returns>
    bool MoveKnockback ( Vector3 displacement, float deltaTime );
}
