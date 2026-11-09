using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class BuildPlacementController : MonoBehaviour
{
    [SerializeField] private BuildPreviewController _previewController;
    [SerializeField] private Camera _placementCamera;
    [SerializeField] private LayerMask _installationMask;
    [SerializeField] private float _maxRayDistance = 500f;

    [SerializeField] private BuildPhaseGate _buildPhaseGate;

    [SerializeField] private BuildPlacementValidator _placementValidator;

    [SerializeField] private InstallationEconomyData _economyData;

    [SerializeField] private LJPlay.PlayScene _playerScene;

    private InputAction _placeAction;
    private InputAction _demolishAction;
    private KeyBindingSettings _keyBindings;

    private void Awake()
    {
        if (_economyData == null || !_economyData.IsValid)
        {
            Debug.LogError(
                "유효한 InstallationEconomyData를 연결하세요.",
                this);

            enabled = false;
            return;
        }

        bool hasRequiredReferences =
            _buildPhaseGate != null &&
            _previewController != null &&
            _placementCamera != null &&
            _installationMask.value != 0 &&
            _maxRayDistance > 0f &&
            !float.IsNaN(_maxRayDistance) &&
            !float.IsInfinity(_maxRayDistance) &&
            _placementValidator != null &&
            _playerScene != null &&
            _previewController.PlacementCurrency == _playerScene.Currency;

        if (!hasRequiredReferences)
        {
            Debug.LogError(
                "BuildPhaseGate, Preview, Camera, Mask와 Ray 거리를 확인하세요.",
                this);

            enabled = false;
            return;
        }

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

        _placeAction = actions.FindAction(
            "Player/PlaceInstallation",
            false);

        _demolishAction = actions.FindAction(
            "Player/DemolishInstallation",
            false);

        if (_placeAction == null || _demolishAction == null)
        {
            Debug.LogError(
                "PlaceInstallation과 DemolishInstallation 액션을 확인하세요.",
                this);

            enabled = false;
            return;
        }
    }

    private void LateUpdate()
    {
        // 키 재지정 중이거나 해당 입력을 소비한 프레임에는 실행하지 않음
        if (Mouse.current == null || _keyBindings.IsCapturingInput)
        {
            return;
        }

        bool wasPlacePressed =
            _placeAction.WasPressedThisFrame();

        bool wasDemolishPressed =
            _demolishAction.WasPressedThisFrame();

        if (!wasPlacePressed && !wasDemolishPressed)
        {
            return;
        }

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // 입력을 실행하는 시점에도 진행 상태를 다시 검사
        if (_buildPhaseGate == null || !_buildPhaseGate.CanBuild)
        {
            Debug.Log(
                "준비 시간에만 설치와 철거가 가능합니다.",
                this);

            return;
        }

        // 두 키를 동시에 누르면 철거만 처리
        if (wasDemolishPressed)
        {
            TryDemolishInstallation();
            return;
        }

        TryPlaceInstallation();
    }

    private void TryPlaceInstallation()
    {
        TrapData data = _previewController.CurrentTrapData;
        Currency currency = _previewController.PlacementCurrency;

        if (data == null ||
            !data.IsValid ||
            currency == null ||
            !currency.IsStageActive)
        {
            return;
        }

        // 실제 설치 전에 공용 시스템의 해금 여부 확인
        // 잠겨 있으면 셀 점유·비용 차감·생성을 모두 진행하지 않음
        if (!_playerScene.IsInstallationUnlocked(data.TrapId))
        {
            Debug.Log(
                $"해금되지 않은 설치물입니다: {data.name}",
                this);

            return;
        }

        bool canTryPlacement =
            _previewController.isActiveAndEnabled &&
            _previewController.HasCell &&
            _previewController.IsAreaAvailable;

        if (!canTryPlacement)
        {
            return;
        }

        BuildArea area = _previewController.CurrentArea;
        Vector2Int startCell = _previewController.CurrentCell;
        Vector2Int size = _previewController.CurrentSize;
        Vector2Int baseSize = _previewController.BaseSize;
        Vector3 position = _previewController.CurrentAreaCenter;

        // 이번 설치에서 사용할 가격과 도전 번호 보관
        int cost = data.PlacementCost;
        int stageId = currency.CurrentStageId;

        if (area == null)
        {
            return;
        }

        if (!_previewController.IsWithinPlacementDistance(position))
        {
            Debug.Log("설치 가능한 거리를 벗어났습니다.", this);
            return;
        }

        if (!_placementValidator.IsSpaceClear(
            area, position, size, data))
        {
            Debug.Log(
                "설치 범위에 장애물 또는 캐릭터가 있습니다.",
                this);

            return;
        }

        // 현재 선택한 동일 TrapData로 이번 도전의 확정 결과 조회
        // 조회 실패 시 점유·재화 차감 전에 설치 중단
        if (!_playerScene.TryGetStageInstallationStats(
            stageId,
            data,
            out InstallationUpgradeStats upgradeStats))
        {
            Debug.LogError(
                $"설치물 강화 결과 조회 실패: " +
                $"도전 {stageId}, 데이터 {data.name}",
                this);

            return;
        }

        Quaternion rotation =
            area.transform.rotation *
            Quaternion.Euler(
                0f,
                _previewController.RotationDegrees,
                0f);

        if (!area.TryOccupyArea(startCell, size))
        {
            Debug.Log("해당 범위에는 설치할 수 없습니다.", this);
            return;
        }

        PlacedInstallation installation = null;
        bool wasPaid = false;
        bool wasPlaced = false;

        try
        {
            // 실제 설치 시점에 소비 가능 여부 재검사 및 차감
            if (!currency.TrySpend(CurrencyType.Stage, cost))
            {
                Debug.Log("재화가 부족하거나 사용할 수 없습니다.", this);
                return;
            }

            wasPaid = true;

            installation = Instantiate(
                data.Prefab,
                position,
                rotation);

            if (!installation.TryInitialize(
                area,
                startCell,
                size,
                baseSize,
                cost,
                stageId))
            {
                return;
            }

            SpikeTrap spikeTrap =
                installation.GetComponent<SpikeTrap>();

            FrostTrap frostTrap = installation.GetComponent<FrostTrap>();

            if (frostTrap != null &&
            !frostTrap.TryInitialize(
            data,
            _buildPhaseGate,
            upgradeStats))
            {
                return;
            }

            if (spikeTrap != null &&
                !spikeTrap.TryInitialize(
                data,
                _buildPhaseGate,
                upgradeStats))
            {
                return;
            }

            ArrowTower arrowTower = installation.GetComponent<ArrowTower>();

            if (arrowTower != null &&
            !arrowTower.TryInitialize(
            data,
            _buildPhaseGate,
            upgradeStats))
            {
                return;
            }

            ShockTrap shockTrap = installation.GetComponent<ShockTrap>();

            if (shockTrap != null &&
            !shockTrap.TryInitialize(
            data,
            _buildPhaseGate,
            upgradeStats))
            {
                return;
            }

            FireTrap fireTrap = installation.GetComponent<FireTrap>();

            if (fireTrap != null &&
            !fireTrap.TryInitialize(
            data,
            _buildPhaseGate,
            upgradeStats))
            {
                return;
            }

            // 생성 도중 진행 상태가 바뀌었다면 설치 취소
            if (!currency.IsStageActive ||
                currency.CurrentStageId != stageId ||
                !_buildPhaseGate.CanBuild)
            {
                return;
            }

            wasPlaced = true;
        }
        finally
        {
            if (!wasPlaced)
            {
                if (installation != null)
                {
                    // 복구 알림 전에 생성물의 Collider도 비활성화
                    installation.gameObject.SetActive(false);
                    Destroy(installation.gameObject);
                }

                if (!area.TryReleaseArea(startCell, size))
                {
                    Debug.LogError(
                        "설치 실패 후 셀 점유 해제에 실패했습니다.",
                        this);
                }

                // 실제 차감한 경우에만 원래 도전으로 전액 복구
                if (wasPaid)
                {
                    bool isSameStage =
                        currency != null &&
                        currency.IsStageActive &&
                        currency.CurrentStageId == stageId;

                    if (!isSameStage ||
                        !currency.TryAdd(CurrencyType.Stage, cost))
                    {
                        Debug.LogError(
                            $"설치 실패 후 재화 {cost} 복구에 실패했습니다.",
                            this);
                    }
                    else
                    {
                        Debug.Log(
                            $"설치 실패: 재화 {cost} 전액 복구",
                            this);
                    }
                }
            }
        }

        Debug.Log(
            $"설치 완료: 비용 {cost}, " +
            $"남은 스테이지 재화 " +
            $"{currency.GetBalance(CurrencyType.Stage)}, " +
            $"점유 범위 {area.OccupiedAreaCount}개",
            installation);
    }

    private void TryDemolishInstallation()
    {
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (!_placementCamera.pixelRect.Contains(mousePosition))
        {
            return;
        }

        Ray ray = _placementCamera.ScreenPointToRay(mousePosition);

        // 설치물 선택용 Trigger Collider도 검사
        bool hasHit = Physics.Raycast(
            ray,
            out RaycastHit hit,
            _maxRayDistance,
            _installationMask,
            QueryTriggerInteraction.Collide);

        if (!hasHit)
        {
            return;
        }

        // 자식 Visual을 클릭해도 부모의 설치물 정보 조회
        PlacedInstallation installation =
            hit.collider.GetComponentInParent<PlacedInstallation>();

        if (installation == null)
        {
            return;
        }

        Currency currency = _previewController.PlacementCurrency;

        // 설치 당시 도전과 현재 도전이 같을 때만 철거 및 환불
        bool canRefund =
            currency != null &&
            currency.isActiveAndEnabled &&
            currency.IsStageActive &&
            currency.CurrentStageId == installation.PaidStageId;

        if (!canRefund)
        {
            Debug.LogWarning(
                "설치 당시 도전의 재화를 사용할 수 없어 철거를 중단했습니다.",
                this);

            return;
        }

        // 삭제 전에 설치 당시 정보와 환불액 보관
        int paidCost = installation.PaidCost;
        int paidStageId = installation.PaidStageId;

        // long으로 계산한 뒤 정수 나눗셈으로 소수점 버림
        int refundAmount = (int)(
            (long)paidCost * _economyData.RefundPercent / 100);

        int balance = currency.GetBalance(CurrencyType.Stage);

        // 지급 가능한 정수 범위를 넘으면 철거 전에 중단
        if (refundAmount > int.MaxValue - balance)
        {
            Debug.LogWarning(
                "환불 후 재화가 정수 범위를 초과하여 철거를 중단했습니다.",
                this);

            return;
        }

        // 셀 점유 해제와 철거가 성공한 경우에만 환불
        if (!installation.TryDemolish())
        {
            return;
        }

        // 원래 도전 번호로 지급하여 다른 도전에 환불되는 것을 방지
        if (!currency.TryAddStageReward(paidStageId, refundAmount))
        {
            Debug.LogError(
                $"철거는 완료됐지만 환불 {refundAmount} 지급에 실패했습니다. " +
                $"도전 번호: {paidStageId}",
                this);

            return;
        }

        Debug.Log(
            $"철거 환불 완료: 지불 비용 {paidCost}, " +
            $"환불 {refundAmount}, " +
            $"남은 스테이지 재화 {currency.GetBalance(CurrencyType.Stage)}",
            this);
    }
}