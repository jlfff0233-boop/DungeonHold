using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlacedInstallation))]
public class FrostTrap : MonoBehaviour,
    IInstallationUpgradeTarget,
    IInstallationSlowSource
{
    [SerializeField] private LayerMask _enemyMask;
    [SerializeField] private SlowEffectData _slowData;

    // 장판 위에 있는 적의 감속을 주기적으로 갱신
    [SerializeField, Min(0.01f)]
    private float _refreshInterval = 0.2f;

    private PlacedInstallation _installation;
    private BuildPhaseGate _buildPhaseGate;

    private bool _isInitialized;
    private float _detectionHeight;
    private float _remainingRefreshTime;
    // 이번 도전의 강화 결과를 반영한 요청 감속률
    private float _slowRate;
    private Vector3 _halfExtents;

    // 적에게 Collider가 여러 개 있어도 한 번만 적용
    private HashSet<EnemyCombat> _detectedEnemies =
        new HashSet<EnemyCombat>();

    // 냉기 장판은 감속률 강화를 지원
    public InstallationUpgradeSupport SupportedUpgrades =>
        InstallationUpgradeSupport.Slow;

    // 호환성 검사는 실제 사용 중인 기본 감속 데이터를 조회
    public SlowEffectData SlowData => _slowData;

    private void Awake()
    {
        _installation = GetComponent<PlacedInstallation>();
    }

    public bool TryInitialize(
    TrapData data,
    BuildPhaseGate buildPhaseGate,
    InstallationUpgradeStats upgradeStats)
    {
        bool hasValidSettings =
            !_isInitialized &&
            isActiveAndEnabled &&
            data != null &&
            data.IsValid &&
            data.DetectionHeight > 0f &&
            buildPhaseGate != null &&
            _installation.Area != null &&
            _enemyMask.value != 0 &&
            _slowData != null &&
            _slowData.Duration > 0f &&
            _slowData.SlowRate >= 0f &&
            _slowData.SlowRate <= 1f &&
            upgradeStats.SlowRateBonus >= 0f &&
            upgradeStats.SlowRateBonus <= 1f &&
            _refreshInterval > 0f &&
            _refreshInterval < _slowData.Duration;

        if (!hasValidSettings)
        {
            Debug.LogError(
                "냉기 장판의 감속 데이터, 강화 결과, " +
                "탐색 설정과 초기화 순서를 확인하세요.",
                this);

            return false;
        }

        float slowRate =
            upgradeStats.CalculateSlowRate(_slowData.SlowRate);

        // 잘못된 강화 설정을 임의로 보정하지 않고 설치 중단
        if (!(slowRate >= 0f && slowRate <= 1f))
        {
            Debug.LogError(
                $"냉기 장판의 요청 감속률은 0~1이어야 합니다. " +
                $"기본 {_slowData.SlowRate}, " +
                $"강화 추가 {upgradeStats.SlowRateBonus}, " +
                $"계산 결과 {slowRate}",
                this);

            return false;
        }

        _buildPhaseGate = buildPhaseGate;
        _detectionHeight = data.DetectionHeight;
        _slowRate = slowRate;

        float cellSize = _installation.Area.CellSize;

        // 회전된 점유 크기와 BuildArea 방향으로 탐색 범위 설정
        _halfExtents = new Vector3(
            _installation.Size.x * cellSize * 0.5f,
            _detectionHeight * 0.5f,
            _installation.Size.y * cellSize * 0.5f);

        _remainingRefreshTime = 0f;
        _isInitialized = true;

        Debug.Log(
            $"냉기 장판 강화 적용: 데이터 {data.name}, " +
            $"기본 감속 {_slowData.SlowRate:P0}, " +
            $"강화 추가 {upgradeStats.SlowRateBonus:P0}, " +
            $"요청 감속 {_slowRate:P0}",
            this);

        return true;
    }

    private void Update()
    {
        if (!_isInitialized ||
            _installation.Area == null ||
            _buildPhaseGate == null)
        {
            return;
        }

        if (!_buildPhaseGate.CanActivateTraps)
        {
            _remainingRefreshTime = 0f;
            return;
        }

        _remainingRefreshTime = Mathf.Max(
            0f,
            _remainingRefreshTime - Time.deltaTime);

        if (_remainingRefreshTime > 0f)
        {
            return;
        }

        _remainingRefreshTime = _refreshInterval;
        ApplySlowToEnemies();
    }

    private void ApplySlowToEnemies()
    {
        BuildArea area = _installation.Area;

        Vector3 center =
            transform.position +
            area.transform.up * (_detectionHeight * 0.5f);

        Collider[] colliders = Physics.OverlapBox(
            center,
            _halfExtents,
            area.transform.rotation,
            _enemyMask,
            QueryTriggerInteraction.Collide);

        _detectedEnemies.Clear();

        EffectContext context = new EffectContext(
            gameObject,
            gameObject,
            ActorType.Installation,
            Faction.Ally,
            AttackType.BasicAttack,
            0);

        foreach (Collider collider in colliders)
        {
            EnemyCombat enemy =
                collider.GetComponentInParent<EnemyCombat>();

            // 바닥 장판은 생존한 지상 적에게만 적용
            if (enemy == null ||
                !enemy.CanReceiveDamage ||
                enemy.Data.IsFlying)
            {
                continue;
            }

            if (!_detectedEnemies.Add(enemy))
            {
                continue;
            }

            StatusEffectController effects =
                enemy.GetComponent<StatusEffectController>();

            if (effects == null || !effects.isActiveAndEnabled)
            {
                continue;
            }

            // 적의 이동속도를 직접 변경하지 않고
            // 공용 상태이상 API로 감속 요청
            effects.TryApply(
            new SlowEffectInfo(
            context,
            _slowData,
            _slowRate));
        }
    }
}