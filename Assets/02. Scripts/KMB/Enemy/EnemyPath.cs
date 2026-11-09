using UnityEngine;

public class EnemyPath : MonoBehaviour
{
    public int WaypointCount => transform.childCount;

    // 지정한 순서의 웨이포인트 위치 가져오기
    public bool TryGetWaypointPosition(int waypointIndex, out Vector3 waypointPosition)
    {
        waypointPosition = Vector3.zero;

        bool isValidIndex =
            waypointIndex >= 0 &&
            waypointIndex < WaypointCount;

        if (!isValidIndex)
        {
            return false;
        }

        waypointPosition = transform.GetChild(waypointIndex).position;
        return true;
    }
}