using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlacedInstallation))]
public class ShockTrap : MonoBehaviour, IInstallationUpgradeTarget
{
    [SerializeField] private LayerMask _enemyMask;
    [SerializeField] private StunEffectData _stunData;
    [SerializeField] private bool _showDebugLog = true;

    private PlacedInstallation _installation;
    private BuildPhaseGate _buildPhaseGate;

    private bool _isInitialized;
    private float _damage;
    private float _attackInterval;
    private float _detectionHeight;
    private float _remainingAttackTime;
    private int _attackId;

    private Vector3 _halfExtents;

    // 여러 Collider를 가진 적의 중복 처리 방지
    private HashSet<EnemyCombat> _detectedEnemies =
        new HashSet<EnemyCombat>();

    // 탐색을 마친 뒤 대상에게 순서대로 적용
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
            data.IsValid &&
            data.HasValidAttackSettings &&
            buildPhaseGate != null &&
            _installation.Area != null &&
            _enemyMask.value != 0 &&
            _stunData != null &&
            _stunData.Duration > 0f;

        if (!hasValidSettings)
        {
            Debug.LogError(
                "충격 장치의 공격 설정, 기절 데이터와 초기화 순서를 확인하세요.",
                this);

            return false;
        }

        _buildPhaseGate = buildPhaseGate;
        // 이번 도전의 강화 결과를 실행 값에만 적용
        _damage =
            data.Damage * (1f + upgradeStats.DamageBonus);

        _attackInterval =
            data.AttackInterval * (1f - upgradeStats.IntervalReduction);
        _detectionHeight = data.DetectionHeight;

        float cellSize = _installation.Area.CellSize;

        // 점유 크기에 이미 90도 회전이 반영되어 있음
        _halfExtents = new Vector3(
            _installation.Size.x * cellSize * 0.5f,
            _detectionHeight * 0.5f,
            _installation.Size.y * cellSize * 0.5f);

        // 준비 시간에는 작동하지 않으며
        // 다음 웨이브도 강화가 반영된 간격만큼 기다린 뒤 첫 발동
        _remainingAttackTime = _attackInterval;
        _isInitialized = true;

        if (_showDebugLog)
        {
            Debug.Log(
                $"충격 장치 강화 적용: 데이터 {data.name}, " +
                $"피해 {_damage:F1}, " +
                $"발동 간격 {_attackInterval:F3}초, " +
                $"기절 시간 {_stunData.Duration:F3}초",
                this);
        }

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
            // 준비 시간에는 작동하지 않으며
            // 다음 웨이브의 첫 발동도 5초 후로 맞춤
            _remainingAttackTime = _attackInterval;
            return;
        }

        _remainingAttackTime -= Time.deltaTime;

        if (_remainingAttackTime > 0f)
        {
            return;
        }

        // 적이 없어도 발동 주기는 계속 진행
        _remainingAttackTime = _attackInterval;
        ActivateShock();
    }

    private void ActivateShock()
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
        _targets.Clear();

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

            if (_detectedEnemies.Add(enemy))
            {
                _targets.Add(enemy);
            }
        }

        _attackId++;

        DamageInfo damageInfo = new DamageInfo(
            gameObject,
            gameObject,
            ActorType.Installation,
            Faction.Ally,
            _damage,
            AttackType.BasicAttack,
            _attackId);

        EffectContext effectContext = new EffectContext(
            gameObject,
            gameObject,
            ActorType.Installation,
            Faction.Ally,
            AttackType.BasicAttack,
            _attackId);

        if (_showDebugLog)
        {
            Debug.Log(
                $"충격 장치 발동: 회차 {_attackId}, " +
                $"범위 내 지상 적 {_targets.Count}마리",
                this);
        }

        foreach (EnemyCombat enemy in _targets)
        {
            if (enemy == null || !enemy.CanReceiveDamage)
            {
                continue;
            }

            IDamageable damageable =
                enemy.GetComponent<DamageReceiver>();

            // 공통 피해 계약으로 먼저 피해 적용
            damageable.TakeDamage(damageInfo);

            // 이번 피해로 사망한 적에게는 기절을 적용하지 않음
            if (enemy == null || !enemy.CanReceiveDamage)
            {
                continue;
            }

            StatusEffectController effects =
                enemy.GetComponent<StatusEffectController>();

            effects.TryApply(
                new StatusEffectInfo(effectContext, _stunData));
        }
    }
}