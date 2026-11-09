using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 동료 생성 위치 탐색 - NavMesh와 공간 조건을 만족하는 생성/재배치 좌표 계산
/// </summary>
public static class CompanionSpawnPositionFinder
{
    /// <summary>
    /// 요청점 주변에서 동료가 들어갈 수 있는 위치 조회
    /// </summary>
    /// <param name="prefab">위치 검사에 사용할 동료 프리팹</param>
    /// <param name="requested">요청 위치</param>
    /// <param name="searchRadius">요청점 주변 NavMesh 탐색 거리</param>
    /// <param name="blockMask">생성을 방해하는 레이어</param>
    /// <param name="position">확정 위치</param>
    /// <param name="ignoredInstance">공간 검사에서 제외할 재배치 동료</param>
    /// <returns>위치 확보 성공 여부</returns>
    public static bool TryGetPosition (
        Companion prefab , Vector3 requested , float searchRadius , LayerMask blockMask ,
        out Vector3 position , Companion ignoredInstance = null )
    {
        NavMeshAgent agent = prefab.GetComponent<NavMeshAgent> ( );
        return TryGetPosition (
            agent , requested , searchRadius , blockMask , out position , ignoredInstance );
    }

    /// <summary>
    /// 플레이어 주변의 빈 위치를 가까운 후보부터 조회
    /// </summary>
    /// <param name="prefab">생성할 동료 프리팹</param>
    /// <param name="center">추종할 플레이어 위치</param>
    /// <param name="searchRadius">후보점 주변 NavMesh 탐색 거리</param>
    /// <param name="blockMask">생성을 방해하는 레이어</param>
    /// <param name="position">확정 위치</param>
    /// <returns>위치 확보 성공 여부</returns>
    public static bool TryGetFollowerPosition (
        Companion prefab , Vector3 center , float searchRadius , LayerMask blockMask ,
        out Vector3 position )
    {
        NavMeshAgent agent = prefab.GetComponent<NavMeshAgent> ( );
        float spacing = agent.radius * 2f + 1f;

        //기존 후보 순서 유지: 가까운 원부터 8방향씩 총 16개 검사
        for ( int ring = 1 ; ring <= 2 ; ring++ )
        {
            for ( int direction = 0 ; direction < 8 ; direction++ )
            {
                float angle = direction * Mathf.PI * 0.25f;
                Vector3 offset = new Vector3 ( Mathf.Sin ( angle ) , 0f , Mathf.Cos ( angle ) );
                Vector3 requested = center + offset * spacing * ring;

                if ( TryGetPosition (
                    agent , requested , searchRadius , blockMask , out position , null ) == true )
                    return true;
            }
        }

        position = default;
        return false;
    }

    /// <summary>
    /// 동료의 Agent 크기와 이동 영역을 기준으로 위치 검사
    /// </summary>
    /// <param name="agent">동료 프리팹의 이동 설정</param>
    /// <param name="requested">요청 위치</param>
    /// <param name="searchRadius">요청점 주변 NavMesh 탐색 거리</param>
    /// <param name="blockMask">생성을 방해하는 레이어</param>
    /// <param name="position">확정 위치</param>
    /// <param name="ignoredInstance">공간 검사에서 제외할 재배치 동료</param>
    /// <returns>위치 확보 성공 여부</returns>
    static bool TryGetPosition (
        NavMeshAgent agent , Vector3 requested , float searchRadius , LayerMask blockMask ,
        out Vector3 position , Companion ignoredInstance )
    {
        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID ,
            areaMask = agent.areaMask
        };

        if ( NavMesh.SamplePosition (
            requested , out NavMeshHit hit , searchRadius , filter ) == false )
        {
            position = default;
            return false;
        }

        position = hit.position;

        float radius = agent.radius;
        float height = Mathf.Max ( agent.height , radius * 2f );
        Vector3 bottom = position + Vector3.up * ( agent.baseOffset + radius );
        Vector3 top = position + Vector3.up * ( agent.baseOffset + height - radius );

        //같은 프레임에 생성하거나 이동한 다른 동료의 Collider 위치 반영
        Physics.SyncTransforms ( );

        if ( ignoredInstance == null )
        {
            return Physics.CheckCapsule ( bottom , top , radius ,
                blockMask , QueryTriggerInteraction.Ignore ) == false;
        }

        Collider [ ] colliders = Physics.OverlapCapsule ( bottom , top , radius ,
            blockMask , QueryTriggerInteraction.Ignore );
        foreach ( Collider collider in colliders )
        {
            if ( collider.transform.IsChildOf ( ignoredInstance.transform ) == false )
                return false;
        }

        return true;
    }
}
