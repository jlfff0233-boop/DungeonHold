using UnityEngine;

/// <summary>
/// 동료 타겟 탐색 - 우선 공격 대상과 가까운 위협 선택
/// </summary>
public class CompanionTargetDetector : MonoBehaviour
{
    [Header ( "----- 탐색 -----" )]
    [SerializeField] LayerMask _layerMask;      //동료가 탐색할 적 레이어

    /// <summary>
    /// 우선 공격 대상과 가장 가까운 위협 탐색
    /// </summary>
    /// <param name="center">동료 현재 위치</param>
    /// <param name="searchRadius">전체 후보 수집 반경</param>
    /// <param name="attackDetectionRadius">공격 대상 탐색 반경</param>
    /// <param name="priorityPosition">공격 우선순위 기준 위치</param>
    /// <param name="targetCollider">선택한 적의 조준 기준 콜라이더</param>
    /// <param name="nearestTarget">동료에게 가장 가까운 위협</param>
    /// <param name="excludedTarget">이번 탐색에서 제외할 추적 불가 타겟</param>
    /// <returns>선택한 적, 없으면 null</returns>
    public CombatTarget FindTargets (
        Vector3 center , float searchRadius ,
        float attackDetectionRadius , Vector3 priorityPosition ,
        out Collider targetCollider ,
        out CombatTarget nearestTarget ,
        CombatTarget excludedTarget = null )
    {
        //한 번 수집한 후보에서 공격 우선순위와 가까운 위협을 따로 판단
        targetCollider = null;
        nearestTarget = null;

        Collider [ ] colliders = Physics.OverlapSphere (
            center , searchRadius , _layerMask , QueryTriggerInteraction.Collide );

        CombatTarget selected = null;
        float selectedDistance = 0f;
        float nearestDistance = 0f;

        foreach ( Collider collider in colliders )
        {
            //자식의 피격 Collider에서도
            //부모 CombatTarget을 찾아 하나의 적으로 판단
            CombatTarget target =
                collider.GetComponentInParent<CombatTarget> ( );

            if ( IsValidTarget ( target , center , searchRadius ) == false )
                continue;

            float distance = ( target.Position - center ).sqrMagnitude;

            //접근 경로가 없는 적도 가까운 위협에서는 제외하지 않음
            if ( nearestTarget == null || distance < nearestDistance )
            {
                nearestTarget = target;
                nearestDistance = distance;
            }

            //제외 목표거나 멀리 있으면
            if ( target == excludedTarget
                || distance > attackDetectionRadius * attackDetectionRadius )
                continue;

            distance = ( target.Position - priorityPosition ).sqrMagnitude;

            if ( selected != null && distance >= selectedDistance )
                continue;

            selected = target;
            targetCollider = collider;
            selectedDistance = distance;
        }

        return selected;
    }

    /// <summary>
    /// 타겟 생존과 동료 기준 탐색 범위 확인
    /// </summary>
    /// <param name="target">검사할 타겟</param>
    /// <param name="center">동료 현재 위치</param>
    /// <param name="detectionRadius">탐색 반경</param>
    /// <returns>현재 추적 가능한 타겟 여부</returns>
    public bool IsValidTarget (
        CombatTarget target , Vector3 center , float detectionRadius )
    {
        //신규 선택과 기존 타겟 유지에 같은 기준을 적용
        if ( target == null || target.IsValid == false )
            return false;

        if ( target.Faction != Faction.Enemy )
            return false;

        //동료의 이동 반경과 적 후보 반경은 별개로 적용
        return ( target.Position - center ).sqrMagnitude
            <= detectionRadius * detectionRadius;
    }
}
