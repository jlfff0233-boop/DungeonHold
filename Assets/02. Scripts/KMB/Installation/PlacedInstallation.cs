using UnityEngine;

public class PlacedInstallation : MonoBehaviour
{
    [SerializeField] private Transform _visual;

    private bool _isInitialized;

    public BuildArea Area { get; private set; }
    public Vector2Int StartCell { get; private set; }
    public Vector2Int Size { get; private set; }
    public int PaidCost { get; private set; }
    public int PaidStageId { get; private set; }

    public bool TryInitialize(
        BuildArea area,
        Vector2Int startCell,
        Vector2Int size,
        Vector2Int baseSize,
        int paidCost,
        int paidStageId)
    {
        bool hasValidSettings =
            !_isInitialized &&
            area != null &&
            _visual != null &&
            _visual != transform &&
            _visual.IsChildOf(transform) &&
            size.x > 0 &&
            size.y > 0 &&
            baseSize.x > 0 &&
            baseSize.y > 0 &&
            paidCost >= 0 &&
            paidStageId > 0;

        if (!hasValidSettings)
        {
            Debug.LogError(
                "설치물의 초기화 정보와 Visual 연결을 확인하세요.",
                this);

            return false;
        }

        Area = area;
        StartCell = startCell;
        Size = size;
        PaidCost = paidCost;
        PaidStageId = paidStageId;

        // 테스트용 단위 큐브를 기본 크기로 설정
        // 회전은 부모 오브젝트에서 적용
        _visual.localScale = new Vector3(
            baseSize.x * area.CellSize,
            0.1f,
            baseSize.y * area.CellSize);

        _isInitialized = true;

        return true;
    }

    public bool TryDemolish()
    {
        if (!_isInitialized || Area == null)
        {
            return false;
        }

        // 설치 당시 기록한 전체 범위만 해제
        if (!Area.TryReleaseArea(StartCell, Size))
        {
            Debug.LogError(
                "점유 해제에 실패하여 철거를 중단했습니다.",
                this);

            return false;
        }

        // 같은 설치물이 중복으로 해제되지 않도록 처리
        _isInitialized = false;

        Debug.Log(
            $"철거 완료: 시작 셀 {StartCell}, 크기 {Size}, " +
            $"점유 셀 {Area.OccupiedCellCount}개, " +
            $"점유 범위 {Area.OccupiedAreaCount}개",
            this);

        // 삭제 전 클릭 대상에서 즉시 제외
        gameObject.SetActive(false);
        Destroy(gameObject);

        return true;
    }
}