using UnityEngine;

public class BuildAreaTest : MonoBehaviour
{
    [SerializeField] private BuildArea _buildArea;
    [SerializeField] private Transform _testPoint;
    [SerializeField] private Transform _cellMarker;

    // X는 가로, Y는 Z 방향 점유 셀 수
    [SerializeField] private Vector2Int _testSize = new Vector2Int(2, 3);

    [ContextMenu("테스트 지점의 셀 확인")]
    private void CheckTestPoint()
    {
        if (_buildArea == null || _testPoint == null)
        {
            Debug.LogWarning("BuildArea와 Test Point를 연결하세요.", this);
            return;
        }

        bool hasCell =
            _buildArea.TryGetCell(_testPoint.position, out Vector2Int cell);

        if (!hasCell)
        {
            Debug.Log(
                $"유효한 셀 없음: 위치 {_testPoint.position}. 영역과 설정을 확인하세요.",
                this);
            return;
        }

        if (!_buildArea.TryGetCellCenter(cell, out Vector3 center))
        {
            return;
        }

        // 높이 0.1m인 확인용 큐브의 바닥을 영역 평면에 맞춤
        if (_cellMarker != null)
        {
            _cellMarker.position =
                center + _buildArea.transform.up * 0.05f;
        }

        Debug.Log(
            $"입력 위치: {_testPoint.position}, 셀: {cell}, 셀 중심: {center}",
            this);
    }

    // 점유 테스트에 사용할 현재 셀 확인
    private bool TryGetTestCell(out Vector2Int cell)
    {
        cell = default;

        // 점유 기록은 런타임 상태이므로 Play 중에 시험
        if (!Application.isPlaying)
        {
            Debug.LogWarning("점유와 해제 테스트는 Play 모드에서 실행하세요.", this);
            return false;
        }

        if (_buildArea == null || _testPoint == null)
        {
            Debug.LogWarning("BuildArea와 Test Point를 연결하세요.", this);
            return false;
        }

        if (!_buildArea.TryGetCell(_testPoint.position, out cell))
        {
            Debug.LogWarning("테스트 지점에 유효한 셀이 없습니다.", this);
            return false;
        }

        return true;
    }

    [ContextMenu("테스트 셀 점유")]
    private void OccupyTestCell()
    {
        if (!TryGetTestCell(out Vector2Int cell))
        {
            return;
        }

        bool hasOccupied = _buildArea.TryOccupyCell(cell);

        Debug.Log(
            $"셀 {cell} 점유 성공: {hasOccupied}, 현재 점유 수: {_buildArea.OccupiedCellCount}",
            this);
    }

    [ContextMenu("테스트 셀 해제")]
    private void ReleaseTestCell()
    {
        if (!TryGetTestCell(out Vector2Int cell))
        {
            return;
        }

        bool hasReleased = _buildArea.TryReleaseCell(cell);

        Debug.Log(
            $"셀 {cell} 해제 성공: {hasReleased}, 현재 점유 수: {_buildArea.OccupiedCellCount}",
            this);
    }

    [ContextMenu("테스트 영역 점유 가능 확인")]
    private void CheckTestArea()
    {
        if (!TryGetTestCell(out Vector2Int startCell))
        {
            return;
        }

        bool canOccupy = _buildArea.CanOccupyArea(startCell, _testSize);

        Debug.Log(
            $"시작 셀: {startCell}, 크기: {_testSize}, 점유 가능: {canOccupy}",
            this);
    }

    [ContextMenu("테스트 영역 점유")]
    private void OccupyTestArea()
    {
        if (!TryGetTestCell(out Vector2Int startCell))
        {
            return;
        }

        bool hasOccupied = _buildArea.TryOccupyArea(startCell, _testSize);

        Debug.Log(
            $"영역 점유 성공: {hasOccupied}, 셀 수: {_buildArea.OccupiedCellCount}, 영역 수: {_buildArea.OccupiedAreaCount}",
            this);
    }

    [ContextMenu("테스트 영역 해제")]
    private void ReleaseTestArea()
    {
        if (!TryGetTestCell(out Vector2Int startCell))
        {
            return;
        }

        bool hasReleased = _buildArea.TryReleaseArea(startCell, _testSize);

        Debug.Log(
            $"영역 해제 성공: {hasReleased}, 셀 수: {_buildArea.OccupiedCellCount}, 영역 수: {_buildArea.OccupiedAreaCount}",
            this);
    }
}