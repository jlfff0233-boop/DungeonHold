using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlacedInstallation))]
public class SpikeTrap : MonoBehaviour, IInstallationUpgradeTarget
{
    [SerializeField] private LayerMask _enemyMask;

    private PlacedInstallation _installation;
    private BuildPhaseGate _buildPhaseGate;

    private bool _isInitialized;

    private float _damage;
    private float _attackInterval;
    private float _detectionHeight;
    private float _remainingAttackTime;

    private Vector3 _halfExtents;

    // 한 번의 발동에서 같은 적에게 중복 피해 방지
    private HashSet<EnemyCombat> _detectedEnemies =
        new HashSet<EnemyCombat>();

    // 탐색을 끝낸 다음 피해를 순서대로 적용
    private List<EnemyCombat> _targets =
        new List<EnemyCombat>();

    // 이 설치물은 피해 증가와 공격 주기 단축을 지원
    public InstallationUpgradeSupport SupportedUpgrades =>
        InstallationUpgradeSupport.Damage |
        InstallationUpgradeSupport.Interval;

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
            data.HasValidAttackSettings &&
            buildPhaseGate != null &&
            _installation.Area != null &&
            _enemyMask.value != 0;

        if (!hasValidSettings)
        {
            Debug.LogError(
                "가시 함정의 공격 설정, Enemy Mask와 초기화 순서를 확인하세요.",
                this);

            return false;
        }

        _buildPhaseGate = buildPhaseGate;

        // 원본 SO는 유지하고 이번 도전의 강화 결과를 실행 값에만 적용
        _damage =
            data.Damage * (1f + upgradeStats.DamageBonus);

        _attackInterval =
            data.AttackInterval * (1f - upgradeStats.IntervalReduction);

        _detectionHeight = data.DetectionHeight;

        float cellSize = _installation.Area.CellSize;

        // Size는 이미 회전이 반영된 점유 크기
        _halfExtents = new Vector3(
            _installation.Size.x * cellSize * 0.5f,
            _detectionHeight * 0.5f,
            _installation.Size.y * cellSize * 0.5f);

        _remainingAttackTime = 0f;
        _isInitialized = true;

        Debug.Log(
            $"가시 함정 강화 적용: 데이터 {data.name}, " +
            $"기본 피해 {data.Damage:F2} → 적용 피해 {_damage:F2}, " +
            $"기본 간격 {data.AttackInterval:F2}초 → " +
            $"적용 간격 {_attackInterval:F2}초",
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
            // 다음 웨이브에서는 바로 탐색할 수 있도록 초기화
            _remainingAttackTime = 0f;
            return;
        }

        _remainingAttackTime = Mathf.Max(
            0f,
            _remainingAttackTime - Time.deltaTime);

        if (_remainingAttackTime > 0f)
        {
            return;
        }

        _remainingAttackTime = _attackInterval;
        AttackEnemies();
    }

    private void AttackEnemies()
    {
        BuildArea area = _installation.Area;

        // 바닥에서 위쪽으로 탐색 상자를 배치
        Vector3 center =
            transform.position +
            area.transform.up * (_detectionHeight * 0.5f);

        // 점유 크기에 이미 회전이 반영되어 있으므로
        // 탐색 방향에는 BuildArea 회전만 사용
        Collider[] colliders = Physics.OverlapBox(
            center,
            _halfExtents,
            area.transform.rotation,
            _enemyMask,
            QueryTriggerInteraction.Collide);

        _detectedEnemies.Clear();
        _targets.Clear();

        foreach (Collider collider in colliders)
        {
            EnemyCombat enemy =
                collider.GetComponentInParent<EnemyCombat>();

            // 바닥 가시 함정은 비행형을 공격하지 않음
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

            _targets.Add(enemy);
        }

        DamageInfo damageInfo = new DamageInfo(
            gameObject,
            gameObject,
            ActorType.Installation,
            Faction.Ally,
            _damage,
            AttackType.BasicAttack,
            0);

        foreach (EnemyCombat enemy in _targets)
        {
            // 바닥 가시 함정은 비행형을 공격하지 않음
            if (enemy == null ||
                !enemy.CanReceiveDamage ||
                enemy.Data.IsFlying)
            {
                continue;
            }

            IDamageable damageable =
                enemy.GetComponent<DamageReceiver>();

            if (damageable == null)
            {
                continue;
            }

            string targetName = enemy.name;

            // HP 직접 수정 없이 공통 피해 계약으로 요청
            float appliedDamage =
                damageable.TakeDamage(damageInfo);

            if (appliedDamage > 0f)
            {
                Debug.Log(
                    $"가시 함정 적중: 대상 {targetName}, " +
                    $"실제 피해 {appliedDamage}",
                    this);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!_isInitialized || _installation.Area == null)
        {
            return;
        }

        BuildArea area = _installation.Area;

        Vector3 center =
            transform.position +
            area.transform.up * (_detectionHeight * 0.5f);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        try
        {
            Gizmos.matrix = Matrix4x4.TRS(
                center,
                area.transform.rotation,
                Vector3.one);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(
                Vector3.zero,
                _halfExtents * 2f);
        }
        finally
        {
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}