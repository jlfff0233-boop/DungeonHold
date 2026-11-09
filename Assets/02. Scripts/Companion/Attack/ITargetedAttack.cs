using UnityEngine;

/// <summary>
/// 타겟 조준 공격 - 월드 조준 위치 전달 계약
/// </summary>
public interface ITargetedAttack
{
    /// <summary>
    /// 이번 공격의 월드 조준 위치 설정
    /// </summary>
    /// <param name="position">타겟의 월드 조준 위치</param>
    void SetTargetPosition ( Vector3 position );
}
