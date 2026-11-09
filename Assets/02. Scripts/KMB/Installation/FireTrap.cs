using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlacedInstallation))]
public class FireTrap : MonoBehaviour, IInstallationUpgradeTarget
{
    [SerializeField] private LayerMask _enemyMask;
    [SerializeField] private BurnEffectData _burnData;

    // 직접 피해를 짧은 간격으로 나누어 적용
    [SerializeField, Min(0.01f)]
    private float _damageTickInterval = 0.2f;

    [SerializeField] private bool _showDebugLog = true;

    private PlacedInstallation _installation;
    private BuildPhaseGate _buildPhaseGate;

    private bool _isInitialized;
    private float _damagePerSecond;
    private float _detectionHeight;
    private Vector3 _halfExtents;
    private int _attackId;

    private HashSet<EnemyCombat> _detectedEnemies =
        new HashSet<EnemyCombat>();

    private List<EnemyCombat> _targets =
        new List<EnemyCombat>();

    // 적마다 장판 위에 머문 피해 누적 시간을 따로 보관
    private Dictionary<EnemyCombat, float> _damageTimes =
        new Dictionary<EnemyCombat, float>();

    private List<EnemyCombat> _exitedEnemies =
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
            _burnData != null &&
            _burnData.Duration > 0f &&
            _burnData.RawDamage > 0f &&
            _burnData.TickInterval > 0f &&
            _damageTickInterval > 0f;

        if (!hasValidSettings)
        {
            Debug.LogError(
                "화염 장판의 피해 설정, 화상 데이터와 초기화 순서를 확인하세요.",
                this);

            return false;
        }

        _buildPhaseGate = buildPhaseGate;
        _detectionHeight = data.DetectionHeight;

        // 강화된 피해와 기준 간격으로 장판의 초당 직접 피해 계산
        float damage =
            data.Damage * (1f + upgradeStats.DamageBonus);

        float attackInterval =
            data.AttackInterval * (1f - upgradeStats.IntervalReduction);

        _damagePerSecond = damage / attackInterval;

        float cellSize = _installation.Area.CellSize;

        _halfExtents = new Vector3(
            _installation.Size.x * cellSize * 0.5f,
            _detectionHeight * 0.5f,
            _installation.Size.y * cellSize * 0.5f);

        _isInitialized = true;

        if (_showDebugLog)
        {
            Debug.Log(
                $"화염 장판 강화 적용: 데이터 {data.name}, " +
                $"기준 피해 {damage:F1}, " +
                $"기준 간격 {attackInterval:F3}초, " +
                $"초당 직접 피해 {_damagePerSecond:F3}",
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
            _damageTimes.Clear();
            return;
        }

        CollectTargets();
        RemoveExitedEnemies();
        ApplyEffects(Time.deltaTime);
    }

    private void CollectTargets()
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

            if (enemy == null ||
                !enemy.CanReceiveDamage ||
                enemy.Data.IsFlying)
            {
                continue;
            }

            // Collider가 여러 개여도 같은 적은 한 번만 처리
            if (_detectedEnemies.Add(enemy))
            {
                _targets.Add(enemy);
            }
        }
    }

    private void RemoveExitedEnemies()
    {
        _exitedEnemies.Clear();

        foreach (EnemyCombat enemy in _damageTimes.Keys)
        {
            if (!_detectedEnemies.Contains(enemy))
            {
                _exitedEnemies.Add(enemy);
            }
        }

        foreach (EnemyCombat enemy in _exitedEnemies)
        {
            _damageTimes.Remove(enemy);
        }
    }

    private void ApplyEffects(float deltaTime)
    {
        foreach (EnemyCombat enemy in _targets)
        {
            if (enemy == null || !enemy.CanReceiveDamage)
            {
                continue;
            }

            StatusEffectController effects =
                enemy.GetComponent<StatusEffectController>();

            // 다른 공격의 화상도 포함해 기존 화상이 끝날 때까지 대기
            // 유지 중에는 재적용하거나 지속시간을 갱신하지 않음
            if (!effects.IsBurning)
            {
                _attackId++;

                EffectContext context = new EffectContext(
                    gameObject,
                    gameObject,
                    ActorType.Installation,
                    Faction.Ally,
                    AttackType.BasicAttack,
                    _attackId);

                bool wasApplied = effects.TryApply(
                    new BurnEffectInfo(context, _burnData));

                if (_showDebugLog && wasApplied)
                {
                    Debug.Log(
                        $"화염 장판 화상 적용: 대상 {enemy.name}",
                        this);
                }
            }

            // 화상과 별개로 장판 위에 머문 시간을 누적
            _damageTimes.TryGetValue(
                enemy,
                out float elapsedTime);

            elapsedTime += deltaTime;

            if (elapsedTime < _damageTickInterval)
            {
                _damageTimes[enemy] = elapsedTime;
                continue;
            }

            // 프레임 지연으로 간격을 조금 넘겨도
            // 누적 시간만큼 계산해 초당 피해량을 유지
            float damage = _damagePerSecond * elapsedTime;
            _damageTimes[enemy] = 0f;

            _attackId++;

            DamageInfo damageInfo = new DamageInfo(
                gameObject,
                gameObject,
                ActorType.Installation,
                Faction.Ally,
                damage,
                AttackType.DamageOverTime,
                _attackId);

            IDamageable damageable =
                enemy.GetComponent<DamageReceiver>();

            damageable.TakeDamage(damageInfo);
        }
    }

    private void OnDisable()
    {
        // 장판의 직접 피해 기록만 정리
        // 적에게 이미 적용된 화상은 공용 시스템에서 만료
        _damageTimes.Clear();
    }
}