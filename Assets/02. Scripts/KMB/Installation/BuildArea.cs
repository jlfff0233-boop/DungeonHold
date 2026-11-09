using System.Collections.Generic;
using UnityEngine;

public class BuildArea : MonoBehaviour
{
    [SerializeField] private int _cellCountX = 12;
    [SerializeField] private int _cellCountZ = 8;
    [SerializeField] private float _cellSize = 1f;
    [SerializeField] private float _heightTolerance = 0.1f;

    // 같은 그리드 안에서도 계단·구조물·바닥 밖은 설치 불가
    // 비어 있으면 기존 직사각형 BuildArea와 동일하게 동작
    [SerializeField] private Vector2Int[] _blockedCells = new Vector2Int[0];

    private HashSet<Vector2Int> _blockedCellSet =
        new HashSet<Vector2Int>();

    private bool _hasCachedBlockedCells;

    public int CellCountX => _cellCountX;
    public int CellCountZ => _cellCountZ;
    public float CellSize => _cellSize;

    // 현재 사용 중인 셀 기록
    private HashSet<Vector2Int> _occupiedCells =
        new HashSet<Vector2Int>();

    // 시작 셀과 해당 영역의 점유 크기 기록
    private Dictionary<Vector2Int, Vector2Int> _occupiedAreas =
        new Dictionary<Vector2Int, Vector2Int>();

    public int OccupiedAreaCount => _occupiedAreas.Count;

    public int OccupiedCellCount => _occupiedCells.Count;

    private void Awake()
    {
        CacheBlockedCells();
    }

    private void OnValidate()
    {
        CacheBlockedCells();
    }

    private void CacheBlockedCells()
    {
        _blockedCellSet.Clear();

        if (_blockedCells != null)
        {
            foreach (Vector2Int cell in _blockedCells)
            {
                _blockedCellSet.Add(cell);
            }
        }

        _hasCachedBlockedCells = true;
    }

    private bool IsCellBlocked(Vector2Int cell)
    {
        if (!_hasCachedBlockedCells)
        {
            CacheBlockedCells();
        }

        return _blockedCellSet.Contains(cell);
    }

    // 월드 위치가 영역 안에 있으면 해당 셀 좌표 반환
    public bool TryGetCell(Vector3 worldPosition, out Vector2Int cell)
    {
        cell = default;

        if (!HasValidSettings())
        {
            return false;
        }

        Vector3 localPosition =
            transform.InverseTransformPoint(worldPosition);

        float width = _cellCountX * _cellSize;
        float depth = _cellCountZ * _cellSize;

        // 오브젝트 중심 기준 좌표를 좌측 아래 원점 기준으로 변환
        float positionX = localPosition.x + width * 0.5f;
        float positionZ = localPosition.z + depth * 0.5f;

        // 오른쪽과 위쪽 끝 경계는 제외해 범위를 넘는 셀 번호 방지
        bool isInside =
            positionX >= 0f &&
            positionX < width &&
            positionZ >= 0f &&
            positionZ < depth &&
            Mathf.Abs(localPosition.y) <= _heightTolerance;

        if (!isInside)
        {
            return false;
        }

        int cellX = Mathf.FloorToInt(positionX / _cellSize);
        int cellZ = Mathf.FloorToInt(positionZ / _cellSize);

        cell = new Vector2Int(cellX, cellZ);
        return true;
    }

    // 유효한 셀의 중심을 월드 위치로 반환
    public bool TryGetCellCenter(Vector2Int cell, out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        if (!IsValidCell(cell))
        {
            return false;
        }

        float width = _cellCountX * _cellSize;
        float depth = _cellCountZ * _cellSize;

        Vector3 localCenter = new Vector3(
            -width * 0.5f + (cell.x + 0.5f) * _cellSize,
            0f,
            -depth * 0.5f + (cell.y + 0.5f) * _cellSize);

        worldPosition = transform.TransformPoint(localCenter);
        return true;
    }

    private bool HasValidSettings()
    {
        bool hasValidValues =
            _cellCountX > 0 &&
            _cellCountZ > 0 &&
            _cellSize > 0f &&
            !float.IsInfinity(_cellSize) &&
            _heightTolerance >= 0f &&
            !float.IsInfinity(_heightTolerance);

        // 초기 구현에서는 월드 기준 크기 1과 수평 영역만 지원
        bool hasUnitScale =
            Vector3.Distance(transform.lossyScale, Vector3.one) < 0.001f;

        bool isHorizontal =
            Vector3.Dot(transform.up, Vector3.up) > 0.9999f;

        return hasValidValues && hasUnitScale && isHorizontal;
    }

    private void OnDrawGizmosSelected()
    {
        if (!HasValidSettings())
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        try
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;

            float width = _cellCountX * _cellSize;
            float depth = _cellCountZ * _cellSize;

            for (int x = 0; x < _cellCountX; x++)
            {
                for (int z = 0; z < _cellCountZ; z++)
                {
                    Vector2Int cell = new Vector2Int(x, z);

                    if (IsCellBlocked(cell))
                    {
                        continue;
                    }

                    float positionX = -width * 0.5f + x * _cellSize;
                    float positionZ = -depth * 0.5f + z * _cellSize;
                    Vector3 corner = new Vector3(positionX, 0f, positionZ);
                    Vector3 right = corner + Vector3.right * _cellSize;
                    Vector3 top = corner + Vector3.forward * _cellSize;

                    Gizmos.DrawLine(corner, right);
                    Gizmos.DrawLine(corner, top);

                    // 인접 셀의 선은 중복해서 그리지 않고 외곽도 닫음
                    if (x == _cellCountX - 1 ||
                        IsCellBlocked(cell + Vector2Int.right))
                    {
                        Gizmos.DrawLine(right, top + Vector3.right * _cellSize);
                    }

                    if (z == _cellCountZ - 1 ||
                        IsCellBlocked(cell + Vector2Int.up))
                    {
                        Gizmos.DrawLine(top, top + Vector3.right * _cellSize);
                    }
                }
            }
        }
        finally
        {
            // 다른 오브젝트의 Gizmos 설정에 영향을 남기지 않음
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }

    // 영역 설정과 셀 번호가 유효한지 확인
    private bool IsValidCell(Vector2Int cell)
    {
        return HasValidSettings() &&
            cell.x >= 0 &&
            cell.x < _cellCountX &&
            cell.y >= 0 &&
            cell.y < _cellCountZ;
    }

    // 해당 셀이 사용 중인지 조회
    public bool IsCellOccupied(Vector2Int cell)
    {
        return _occupiedCells.Contains(cell);
    }

    // 단일 셀도 같은 영역 기록 방식 사용
    public bool TryOccupyCell(Vector2Int cell)
    {
        return TryOccupyArea(cell, Vector2Int.one);
    }

    public bool TryReleaseCell(Vector2Int cell)
    {
        return TryReleaseArea(cell, Vector2Int.one);
    }

    // 전체 점유 범위가 영역 안에 들어오는지 확인
    private bool IsAreaInside(Vector2Int startCell, Vector2Int size)
    {
        if (!HasValidSettings())
        {
            return false;
        }

        bool hasValidSize =
            size.x > 0 &&
            size.y > 0 &&
            size.x <= _cellCountX &&
            size.y <= _cellCountZ;

        if (!hasValidSize)
        {
            return false;
        }

        // 시작 셀 + 크기 계산 대신 뺄셈으로 범위 검사
        return startCell.x >= 0 &&
            startCell.y >= 0 &&
            startCell.x <= _cellCountX - size.x &&
            startCell.y <= _cellCountZ - size.y;
    }

    // 전체 셀이 유효하고 비어 있는지 확인
    public bool CanOccupyArea(Vector2Int startCell, Vector2Int size)
    {
        if (!IsAreaInside(startCell, size))
        {
            return false;
        }

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                Vector2Int cell = new Vector2Int(
                    startCell.x + x,
                    startCell.y + z);

                if (IsCellBlocked(cell) || _occupiedCells.Contains(cell))
                {
                    return false;
                }
            }
        }

        return true;
    }

    // 전체 검사에 성공한 경우에만 모든 셀 점유
    public bool TryOccupyArea(Vector2Int startCell, Vector2Int size)
    {
        if (!CanOccupyArea(startCell, size))
        {
            return false;
        }

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                Vector2Int cell = new Vector2Int(
                    startCell.x + x,
                    startCell.y + z);

                _occupiedCells.Add(cell);
            }
        }

        _occupiedAreas.Add(startCell, size);
        return true;
    }

    // 처음 점유한 범위와 일치하는 경우에만 전체 해제
    public bool TryReleaseArea(Vector2Int startCell, Vector2Int size)
    {
        bool hasRecordedArea =
            _occupiedAreas.TryGetValue(startCell, out Vector2Int recordedSize);

        if (!hasRecordedArea || recordedSize != size)
        {
            return false;
        }

        // 일부 셀이 없는 상태에서 나머지만 해제하지 않도록 먼저 검사
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                Vector2Int cell = new Vector2Int(
                    startCell.x + x,
                    startCell.y + z);

                if (!_occupiedCells.Contains(cell))
                {
                    return false;
                }
            }
        }

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                Vector2Int cell = new Vector2Int(
                    startCell.x + x,
                    startCell.y + z);

                _occupiedCells.Remove(cell);
            }
        }

        _occupiedAreas.Remove(startCell);
        return true;
    }

    // 시작 셀과 크기로 전체 범위의 중심 계산
    // 범위가 영역 밖으로 나가도 빨간 Preview를 표시할 수 있도록 계산
    public bool TryGetAreaCenter(
        Vector2Int startCell,
        Vector2Int size,
        out Vector3 worldPosition)
    {
        worldPosition = Vector3.zero;

        bool canCalculate =
            IsValidCell(startCell) &&
            size.x > 0 &&
            size.y > 0;

        if (!canCalculate)
        {
            return false;
        }

        float width = _cellCountX * _cellSize;
        float depth = _cellCountZ * _cellSize;

        Vector3 localCenter = new Vector3(
            -width * 0.5f + (startCell.x + size.x * 0.5f) * _cellSize,
            0f,
            -depth * 0.5f + (startCell.y + size.y * 0.5f) * _cellSize);

        worldPosition = transform.TransformPoint(localCenter);
        return true;
    }
}
