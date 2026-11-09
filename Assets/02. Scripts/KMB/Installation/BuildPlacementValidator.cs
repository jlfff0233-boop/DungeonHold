using UnityEngine;

public class BuildPlacementValidator : MonoBehaviour
{
    [SerializeField] private LayerMask _blockingActorMask;

    // 경계가 닿기만 하는 인접 배치를 허용하기 위한 작은 여유
    [SerializeField] private float _horizontalPadding = 0.01f;

    public bool IsSpaceClear(
        BuildArea area,
        Vector3 areaCenter,
        Vector2Int size,
        TrapData data)
    {
        bool hasValidSettings =
            isActiveAndEnabled &&
            area != null &&
            data != null &&
            data.IsValid &&
            size.x > 0 &&
            size.y > 0 &&
            _blockingActorMask.value != 0 &&
            _horizontalPadding >= 0f &&
            !float.IsInfinity(_horizontalPadding);

        if (!hasValidSettings)
        {
            return false;
        }

        float height = data.PlacementHeight;

        Vector3 halfExtents = new Vector3(
            size.x * area.CellSize * 0.5f - _horizontalPadding,
            height * 0.5f,
            size.y * area.CellSize * 0.5f - _horizontalPadding);

        if (halfExtents.x <= 0f || halfExtents.z <= 0f)
        {
            return false;
        }

        // 표시용 Preview 높이가 아닌 실제 설치 위치 기준
        Vector3 center =
            areaCenter + area.transform.up * (height * 0.5f);

        // 직접 이동한 Collider의 위치도 검사에 반영
        Physics.SyncTransforms();

        // Default의 벽도 찾은 뒤 표시 컴포넌트로 구분
        // size에는 이미 90도 회전이 반영되어 있음
        Collider[] colliders = Physics.OverlapBox(
            center,
            halfExtents,
            area.transform.rotation,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        foreach (Collider collider in colliders)
        {
            BuildPlacementBlocker blocker =
                collider.GetComponentInParent<BuildPlacementBlocker>();

            if (blocker != null && blocker.isActiveAndEnabled)
            {
                return false;
            }

            int colliderLayer = 1 << collider.gameObject.layer;

            bool isBlockingActor =
                (_blockingActorMask.value & colliderLayer) != 0;

            if (isBlockingActor)
            {
                return false;
            }
        }

        return true;
    }
}