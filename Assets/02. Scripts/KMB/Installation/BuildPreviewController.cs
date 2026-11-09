using UnityEngine;
using UnityEngine.InputSystem;

public class BuildPreviewController : MonoBehaviour
{
    [SerializeField] private Camera _placementCamera;
    [SerializeField] private LayerMask _buildAreaMask;
    [SerializeField] private GameObject _previewObject;

    [SerializeField] private Material _availableMaterial;
    [SerializeField] private Material _occupiedMaterial;

    [SerializeField] private float _maxRayDistance = 500f;
    [SerializeField] private float _previewHeightOffset = 0.05f;

    [SerializeField] private BuildPhaseGate _buildPhaseGate;

    [SerializeField] private Transform _player;
    [SerializeField] private float _maxPlacementDistance = 8f;

    [SerializeField] private BuildPlacementValidator _placementValidator;

    [SerializeField] private Currency _currency;

    private InputAction _rotateAction;
    private KeyBindingSettings _keyBindings;

    // 공용 슬롯에서 전달받은 현재 선택
    // 시작 시에는 아무 설치물도 선택하지 않음
    private TrapData _trapData;

    public bool HasSelection => _trapData != null;

    // 실제 설치도 Preview와 같은 재화 모듈 사용
    public Currency PlacementCurrency => _currency;


    private Renderer _previewRenderer;

    // 90도 회전 횟수: 0~3
    private int _quarterTurns;

    public BuildArea CurrentArea { get; private set; }
    public Vector2Int CurrentCell { get; private set; }
    public Vector3 CurrentCellCenter { get; private set; }

    public bool HasCell { get; private set; }
    public bool IsCellAvailable { get; private set; }
    public int RotationDegrees => _quarterTurns * 90;

    public TrapData CurrentTrapData => _trapData;

    // 회전하기 전의 기본 점유 크기
    public Vector2Int BaseSize =>
        _trapData != null
            ? _trapData.FootprintSize
            : Vector2Int.zero;

    // 점유 검사에는 회전된 크기 사용
    public Vector2Int CurrentSize =>
        _quarterTurns % 2 == 0
            ? BaseSize
            : new Vector2Int(BaseSize.y, BaseSize.x);
    public Vector3 CurrentAreaCenter { get; private set; }
    public bool IsAreaAvailable { get; private set; }

    private void Awake()
    {
        bool hasRequiredReferences =
            _player != null &&
            _buildPhaseGate != null &&
            _placementCamera != null &&
            _previewObject != null &&
            _availableMaterial != null &&
            _occupiedMaterial != null &&
            _buildAreaMask.value != 0 &&
            _placementValidator != null &&
            _currency != null;

        if (!hasRequiredReferences)
        {
            Debug.LogError("Player, BuildPhaseGate, Preview 참조와 Mask를 확인하세요.",
            this);
            enabled = false;
            return;
        }

        // Preview를 숨길 때 컨트롤러까지 꺼지지 않도록 분리
        if (_previewObject == gameObject ||
            transform.IsChildOf(_previewObject.transform))
        {
            Debug.LogError("Preview 큐브와 컨트롤러를 별도 오브젝트로 구성하세요.", this);
            enabled = false;
            return;
        }

        _previewRenderer = _previewObject.GetComponent<Renderer>();

        if (_previewRenderer == null ||
            _maxRayDistance <= 0f ||
            float.IsNaN(_maxRayDistance) ||
            float.IsInfinity(_maxRayDistance))
        {
            Debug.LogError("Preview의 Renderer와 Ray 거리를 확인하세요.", this);
            enabled = false;
            return;
        }

        _previewRenderer.shadowCastingMode =
            UnityEngine.Rendering.ShadowCastingMode.Off;

        _previewRenderer.receiveShadows = false;

        InputActionAsset actions = InputSystem.actions;
        _keyBindings = GameManager.Instance.KeyBindings;

        if (actions == null || _keyBindings == null)
        {
            Debug.LogError(
                "프로젝트 공용 입력과 키 설정을 확인하세요.",
                this);

            enabled = false;
            return;
        }

        _rotateAction = actions.FindAction(
            "Player/RotateInstallation",
            false);

        if (_rotateAction == null)
        {
            Debug.LogError(
                "RotateInstallation 액션을 확인하세요.",
                this);

            enabled = false;
            return;
        }

        ClearSelection();   
    }

    private void Update()
    {
        // 도전이 준비되지 않았거나 종료된 상태에서는 선택까지 해제
        if (!_buildPhaseGate.HasReadyPlayerStage)
        {
            ClearSelection();
            return;
        }

        // 전투 중이거나 종료 예약 중에는 표시만 숨김
        // 정산 실패로 예약이 취소되면 기존 선택을 유지할 수 있음
        if (!_buildPhaseGate.CanBuild)
        {
            ClearPreview();
            return;
        }

        // 명시적으로 해제된 선택은 자동으로 복구하지 않음
        if (_trapData == null)
        {
            ClearPreview();
            return;
        }

        if (Mouse.current == null)
        {
            ClearPreview();
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (!_placementCamera.pixelRect.Contains(mousePosition))
        {
            ClearPreview();
            return;
        }

        // 키 재지정 중에는 회전하지 않고 프리뷰 갱신은 유지
        if (!_keyBindings.IsCapturingInput &&
            _rotateAction.WasPressedThisFrame())
        {
            _quarterTurns = (_quarterTurns + 1) % 4;

            Debug.Log(
                $"Preview 회전: {RotationDegrees}도, 점유 크기: {CurrentSize}",
                this);
        }

        Vector2Int size = CurrentSize;

        Ray ray = _placementCamera.ScreenPointToRay(mousePosition);

        bool hasHit = Physics.Raycast(
            ray,
            out RaycastHit hit,
            _maxRayDistance,
            _buildAreaMask,
            QueryTriggerInteraction.Collide);

        if (!hasHit)
        {
            ClearPreview();
            return;
        }

        BuildArea area = hit.collider.GetComponentInParent<BuildArea>();

        if (area == null ||
            !area.TryGetCell(hit.point, out Vector2Int cell) ||
            !area.TryGetCellCenter(cell, out Vector3 cellCenter) ||
            !area.TryGetAreaCenter(
                cell, size, out Vector3 areaCenter))
        {
            ClearPreview();
            return;
        }

        // 플레이어 기준 설치 거리 밖에서는 Preview 숨김
        if (!IsWithinPlacementDistance(areaCenter))
        {
            ClearPreview();
            return;
        }

        // 시작 셀 정보와 전체 범위 정보를 구분
        CurrentArea = area;
        CurrentCell = cell;
        CurrentCellCenter = cellCenter;
        CurrentAreaCenter = areaCenter;
        HasCell = true;

        IsCellAvailable = area.CanOccupyArea(cell, Vector2Int.one);

        // 셀 전체가 비어 있고 설치 거리 안에 있어야 설치 가능
        bool isWithinDistance =
            IsWithinPlacementDistance(areaCenter);

        IsAreaAvailable =
        area.CanOccupyArea(cell, size) &&
        isWithinDistance &&
        _placementValidator.IsSpaceClear(
        area,
        areaCenter,
        size,
        _trapData) &&
        _currency.CanSpend(
        CurrencyType.Stage,
        _trapData.PlacementCost);

        // BuildArea 방향을 기준으로 Preview 회전
        Quaternion previewRotation =
            area.transform.rotation *
            Quaternion.Euler(0f, RotationDegrees, 0f);

        _previewObject.transform.SetPositionAndRotation(
            areaCenter + area.transform.up * _previewHeightOffset,
            previewRotation);

        // 큐브는 기본 크기로 설정하고 회전으로 방향 변경
        Vector2Int baseSize = BaseSize;

        _previewObject.transform.localScale = new Vector3(
            baseSize.x * area.CellSize,
            0.1f,
            baseSize.y * area.CellSize);

        // 시작 셀이 아닌 전체 범위 검사 결과로 색 결정
        _previewRenderer.sharedMaterial =
            IsAreaAvailable ? _availableMaterial : _occupiedMaterial;

        if (!_previewObject.activeSelf)
        {
            _previewObject.SetActive(true);
        }
    }

    // 공용 슬롯이 전달한 설치물 종류를 실제 선택에 반영
    // 선택 성공은 설치 완료나 비용 지불을 의미하지 않음
    public bool TrySelectInstallation(TrapData data)
    {
        bool canSelect =
            isActiveAndEnabled &&
            _buildPhaseGate != null &&
            _buildPhaseGate.CanBuild &&
            data != null &&
            data.IsValid;

        // 거절된 요청은 기존 선택과 프리뷰를 변경하지 않음
        if (!canSelect)
        {
            return false;
        }

        // 이전 종류로 계산한 위치·범위·설치 가능 결과부터 무효화
        ClearPreview();

        _trapData = data;
        _quarterTurns = 0;

        // 배치 가능 여부는 다음 프리뷰 계산에서 판단
        return true;
    }

    // 도전 종료나 씬 비활성화 중에도 해제할 수 있도록
    // 준비 시간 조건을 요구하지 않음
    public void ClearSelection()
    {
        ClearPreview();

        _trapData = null;
        _quarterTurns = 0;
    }

    // 표시와 실제 설치가 같은 거리 규칙을 사용
    public bool IsWithinPlacementDistance(Vector3 worldPosition)
    {
        bool hasValidSettings =
            _player != null &&
            _player.gameObject.activeInHierarchy &&
            _maxPlacementDistance > 0f &&
            !float.IsInfinity(_maxPlacementDistance);

        if (!hasValidSettings)
        {
            return false;
        }

        float distance = Vector3.Distance(
            _player.position,
            worldPosition);

        return distance <= _maxPlacementDistance;
    }

    private void ClearPreview()
    {
        CurrentArea = null;
        CurrentCell = default;
        CurrentCellCenter = Vector3.zero;
        HasCell = false;
        IsCellAvailable = false;
        CurrentAreaCenter = Vector3.zero;
        IsAreaAvailable = false;

        if (_previewObject != null &&
            _previewObject != gameObject &&
            !transform.IsChildOf(_previewObject.transform))
        {
            _previewObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        ClearSelection();
    }
}
