using System;
using UnityEngine;

[RequireComponent(typeof(Health), typeof(DamageReceiver), typeof(EnemyMovement))]
[RequireComponent(typeof(StatusEffectController))]
[RequireComponent(typeof(KnockbackController))]
public class EnemyCombat : MonoBehaviour
{
    [SerializeField] private EnemyData _enemyData;

    public EnemyData Data => _enemyData;

    private Health _health;
    private DamageReceiver _damageReceiver;

    private bool _isInitialized;

    private EnemyMovement _enemyMovement;
    private bool _isDead;

    private StatusEffectController _statusEffects;

    private KnockbackController _knockback;

    // KMB 적의 사망 결과. 공통 처치 보상 계약은 이후 팀과 합의
    public event Action<EnemyCombat, DamageInfo> OnDied;

    private bool _hasCompletedDeathPresentation;

    public bool IsDead => _isDead;

    public bool CanReceiveDamage =>
        isActiveAndEnabled &&
        _isInitialized &&
        !_isDead &&
        !_enemyMovement.HasPassed;

    // 적 생성 당시의 보상 정보
    public int RewardStageId { get; private set; }
    public long RewardLifeId { get; private set; }
    public int KillReward { get; private set; }

    private void Awake()
    {
        // 같은 적 부모에 붙은 공통 전투 모듈 확보
        _health = GetComponent<Health>();
        _damageReceiver = GetComponent<DamageReceiver>();
        _enemyMovement = GetComponent<EnemyMovement>();
        _statusEffects = GetComponent<StatusEffectController>();
        _knockback = GetComponent<KnockbackController>();
    }

    private void Update()
    {
        if (!_isInitialized || _isDead || _enemyMovement.HasPassed)
        {
            return;
        }

        _statusEffects.UpdateEffects(Time.deltaTime);

        // 화상 갱신 도중 사망했을 수도 있으므로 다시 검사
        if (_isDead || _enemyMovement.HasPassed)
        {
            return;
        }

        if (_knockback.IsKnockback)
        {
            _knockback.UpdateKnockback(Time.deltaTime);
        }
    }

    private void OnEnable()
    {
        _damageReceiver.OnDamaged += HandleDamaged;
        _damageReceiver.OnDied += HandleDied;
        _enemyMovement.OnPassed += HandlePassed;
    }

    private void OnDisable()
    {
        _damageReceiver.OnDamaged -= HandleDamaged;
        _damageReceiver.OnDied -= HandleDied;
        _enemyMovement.OnPassed -= HandlePassed;

        if (_knockback != null)
        {
            _knockback.CancelKnockback();
        }
    }

    // 실제 피해가 적용된 뒤 공통 모듈에서 전달되는 알림
    private void HandleDamaged(DamageInfo damageInfo, float appliedDamage)
    {
        if (!_isInitialized || _isDead || _enemyMovement.HasPassed)
        {
            return;
        }

        Debug.Log(
            $"적 피격: 실제 피해 {appliedDamage}, HP {_health.CurrentHp}/{_health.MaxHp}",
            this);
    }

    // HP를 다시 판정하지 않고 공통 사망 알림을 처리
    private void HandleDied(DamageInfo damageInfo)
    {
        if (!_isInitialized || _isDead || _enemyMovement.HasPassed)
        {
            return;
        }

        // 외부 알림 전에 사망 상태와 행동 중단 확정
        _isDead = true;
        _enemyMovement.StopMovement();
        _damageReceiver.SetInvulnerable(true);

        EnemyAttack enemyAttack = GetComponent<EnemyAttack>();

        if (enemyAttack != null)
        {
            enemyAttack.CancelAttack();
        }

        // 사망한 적이 플레이어·설치 공간을 막지 않도록 처리
        Collider[] colliders = GetComponentsInChildren<Collider>();

        foreach (Collider collider in colliders)
        {
            collider.enabled = false;
        }

        Debug.Log("적 사망: 사망 연출 시작", this);

        // 처치 집계와 사망 모션 연결
        OnDied?.Invoke(this, damageInfo);

        // 여기서는 Destroy하지 않음
    }

    // 통과한 적이 제거되기 전 같은 프레임에 피해받는 것을 방지
    private void HandlePassed(EnemyMovement enemy)
    {
        _damageReceiver.SetInvulnerable(true);
    }

    // 새로 생성한 적의 체력과 피해 수신 정보 초기화
    public bool TryInitialize()
    {
        if (!isActiveAndEnabled || _isInitialized)
        {
            Debug.LogError("현재 상태에서는 적 전투를 초기화할 수 없습니다.", this);
            return false;
        }

        if (_enemyData == null || !_enemyData.IsValid)
        {
            Debug.LogError(
                "적에 유효한 EnemyData를 연결하세요.",
                this);

            return false;
        }

        CombatStatsData combatStats = _enemyData.CombatStatsData;

        float maxHp = combatStats.MaxHp;
        float damageReductionRate = combatStats.DamageReductionRate;

        // 잘못된 설정으로 초기화를 시작하지 않도록 확인
        bool isValidSetting =
            maxHp > 0f &&
            !float.IsInfinity(maxHp) &&
            !float.IsNaN(damageReductionRate) &&
            !float.IsInfinity(damageReductionRate);

        if (!isValidSetting)
        {
            Debug.LogError("적의 최대 HP와 피해 감소율 설정을 확인해야 합니다.", this);
            return false;
        }

        // 피해 수신 모듈을 먼저 준비하고 같은 Health를 전달
        bool hasInitializedReceiver = _damageReceiver.Init(
            gameObject,
            _health,
            Faction.Enemy,
            damageReductionRate);

        if (!hasInitializedReceiver)
        {
            return false;
        }

        if (!_statusEffects.isActiveAndEnabled)
        {
            Debug.LogError(
                "적의 StatusEffectController를 활성화하세요.",
                this);

            return false;
        }

        // 효과 모듈도 피해 수신과 같은 Health를 사용
        if (!_statusEffects.Init(
            this,
            _health,
            _damageReceiver,
            _enemyMovement,
            _enemyData.StatusResistance))
        {
            return false;
        }

        if (!_knockback.isActiveAndEnabled || !_knockback.Init(
        this,
        _health,
        _damageReceiver,
        _enemyMovement))
        {
            Debug.LogError("적의 넉백 초기화에 실패했습니다.", this);
            return false;
        }

        // 공통 Health의 API로 새 생명 시작
        _health.Init(maxHp);

        _isInitialized = true;

        Debug.Log(
            $"적 전투 초기화 완료: HP {_health.CurrentHp}/{_health.MaxHp}",
            this);

        return true;
    }

    public void CompleteDeathPresentation()
    {
        if (!_isDead || _hasCompletedDeathPresentation)
        {
            return;
        }

        _hasCompletedDeathPresentation = true;

        Debug.Log("적 사망 연출 완료: 오브젝트 제거", this);
        Destroy(gameObject);
    }

    // 전투 초기화 이후, 이동 시작 전에 생성 당시 보상 정보 기록
    public bool TryInitializeReward(int stageId, long lifeId)
    {
        bool canInitialize =
            _isInitialized &&
            !_isDead &&
            !_enemyMovement.HasPassed &&
            RewardLifeId == 0 &&
            stageId > 0 &&
            lifeId > 0;

        if (!canInitialize)
        {
            Debug.LogError(
                "적의 보상 정보를 초기화할 수 없습니다.",
                this);

            return false;
        }

        RewardStageId = stageId;
        RewardLifeId = lifeId;
        KillReward = _enemyData.KillReward;

        return true;
    }
}