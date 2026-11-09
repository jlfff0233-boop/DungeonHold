using UnityEngine;

/// <summary>
/// 동료 배치 포인트 - 상호작용 영역과 생성 기준점
/// </summary>
public class CompanionPlacementPoint : MonoBehaviour
{
    [Header ( "----- 배치 -----" )]
    [SerializeField] BoxCollider _interactionArea;    //배치 상호작용 영역
    [SerializeField] Transform _spawnPoint;    //NavMesh 바닥의 생성 기준점

    /// <summary>
    /// 동료 생성 기준 월드 위치
    /// </summary>
    public Vector3 Position => _spawnPoint.position;

    /// <summary>
    /// 플레이어 루트가 상호작용 영역 안인지 확인
    /// </summary>
    /// <param name="playerPosition">플레이어 루트의 월드 위치</param>
    /// <returns>영역 내부 여부</returns>
    public bool Contains ( Vector3 playerPosition )
    {
        //로컬 공간에서 검사하여 영역의 회전과 크기 변경도 반영
        Vector3 localPosition =
            _interactionArea.transform.InverseTransformPoint ( playerPosition );
        Vector3 offset = localPosition - _interactionArea.center;
        Vector3 halfSize = _interactionArea.size * 0.5f;

        return Mathf.Abs ( offset.x ) <= halfSize.x
            && Mathf.Abs ( offset.y ) <= halfSize.y
            && Mathf.Abs ( offset.z ) <= halfSize.z;
    }
}
