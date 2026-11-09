using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EnemyCombat))]
[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(EnemyPlayerDetector))]
[RequireComponent(typeof(HitDetector))]
public class EnemyAttack : MonoBehaviour
{
    [SerializeField] private LayerMask _playerMask;

    [SerializeField] private EnemyAnimationEvents _animationEvents;

    private bool _isAttacking;
    private bool _hasCommittedHit;
    private int _currentAttackId;
    private Hero _attackPlayer;

    // 현재 공격이 방어 목표 공격인지 구분
    private bool _isDefenseGoalAttack;

    // 공격 시작 시 확정한 방어 목표
    private Stage2DefenseGoal _attackDefenseGoal;

    public bool IsAttacking => _isAttacking;

    private EnemyCombat _enemyCombat;
    private EnemyMovement _enemyMovement;
    private EnemyPlayerDetector _playerDetector;
    private HitDetector _hitDetector;
    private StatusEffectController _statusEffects;
    private KnockbackController _knockback;

    private float _damage;
    private float _attackInterval;
    private float _attackRange;
    private float _attackAngle;
    private float _remainingAttackTime;

    private bool _isInitialized;

    private bool _isReactionAllowed;

    // 피해 성공 여부와 관계없이 타격 기회를 사용했을 때 알림
    public event Action OnAttackOpportunityUsed;

    public float AttackRange => _attackRange;

    public bool HasValidPlayer =>
        _playerDetector.isActiveAndEnabled &&
        IsPlayerAlive(_playerDetector.Player);

    // 공격 실행이나 횟수 소비 없이 현재 타격 범위만 조회
    public bool HasPlayerInAttackRange =>
        isActiveAndEnabled &&
        _isInitialized &&
        TryGetAttackTarget(_playerDetector.Player, out _);

    // 이동 중에도 조회할 수 있도록 정지 상태와 별도로 거리 검사
    public bool HasDefenseGoalInAttackRange
    {
        get
        {
            if (!_isInitialized)
            {
                return false;
            }

            Stage2DefenseGoal goal = _enemyMovement.DefenseGoal;

            return goal != null &&
                goal.CanReceiveDamage &&
                Vector3.Distance(
                    transform.position,
                    goal.transform.position) <= _attackRange;
        }
    }

    [ContextMenu("테스트/공격 정지 원인 확인")]
    private void PrintAttackStateForTest()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Hero player = _playerDetector.Player;

        bool hasAttackTarget =
            TryGetAttackTarget(player, out DamageReceiver receiver);

        bool canStartAnimation =
            _animationEvents != null &&
            _animationEvents.CanStartAttack;

        Debug.Log(
            $"공격 상태 확인: 대상 {name}\n" +
            $"컴포넌트 활성: {isActiveAndEnabled}, " +
            $"초기화: {_isInitialized}\n" +
            $"반격 허용: {_isReactionAllowed}, " +
            $"공격 조건 충족: {CanReact()}\n" +
            $"공격 진행 중: {_isAttacking}, " +
            $"타격 기회 사용: {_hasCommittedHit}\n" +
            $"공격 애니메이션 시작 가능: {canStartAnimation}\n" +
            $"공격 대상 탐색 성공: {hasAttackTarget}\n" +
            $"플레이어 거리: {_playerDetector.CurrentDistance:F3}, " +
            $"공격 거리: {_attackRange:F3}\n" +
            $"남은 공격 간격: {_remainingAttackTime:F3}, " +
            $"Player Mask: {_playerMask.value}",
            this);
    }

    private void Awake()
    {
        _enemyCombat = GetComponent<EnemyCombat>();
        _enemyMovement = GetComponent<EnemyMovement>();
        _playerDetector = GetComponent<EnemyPlayerDetector>();
        _hitDetector = GetComponent<HitDetector>();
        _statusEffects = GetComponent<StatusEffectController>();
        _knockback = GetComponent<KnockbackController>();

        if (_playerMask.value == 0 || _animationEvents == null)
        {
            Debug.LogError(
                "Player Mask와 Animation Events를 연결하세요.",
                this);

            enabled = false;
        }
    }

    public bool TryInitialize(AttackData data)
    {
        if (!isActiveAndEnabled || _isInitialized || data == null)
        {
            return false;
        }

        bool hasValidValues =
            IsPositiveFinite(data.RawDamage) &&
            IsPositiveFinite(data.AttackInterval) &&
            IsPositiveFinite(data.Radius) &&
            data.Angle >= 0f &&
            data.Angle <= 360f;

        if (!hasValidValues)
        {
            Debug.LogError("적의 공격 설정값을 확인하세요.", this);
            return false;
        }

        _damage = data.RawDamage;
        _attackInterval = data.AttackInterval;
        _attackRange = data.Radius;
        _attackAngle = data.Angle;

        _isInitialized = true;
        return true;
    }

    // EnemyReaction이 이동 상태를 적용한 뒤 한 번 호출
    public void UpdateAttack(float deltaTime)
    {
        if (!isActiveAndEnabled || !_isInitialized)
        {
            return;
        }

        if (!_enemyCombat.CanReceiveDamage)
        {
            CancelAttack();
            return;
        }

        if (!IsPositiveFinite(deltaTime))
        {
            return;
        }

        _remainingAttackTime = Mathf.Max(
            0f,
            _remainingAttackTime - deltaTime);

        // 방어 목표에 도착한 적은 플레이어 반격 대신 목표 공격
        if (_enemyMovement.HasReachedDefenseGoal)
        {
            UpdateDefenseGoalAttack();
            return;
        }

        if (!CanReact())
        {
            CancelAttack();
            return;
        }

        if (_isAttacking)
        {
            if (!IsPlayerAlive(_attackPlayer) ||
                _playerDetector.Player != _attackPlayer)
            {
                CancelAttack();
            }

            return;
        }

        if (_remainingAttackTime > 0f ||
            !_animationEvents.CanStartAttack)
        {
            return;
        }

        Hero player = _playerDetector.Player;

        if (!TryGetAttackTarget(
            player, out DamageReceiver receiver))
        {
            return;
        }

        _currentAttackId++;
        _attackPlayer = player;
        _hasCommittedHit = false;
        _isAttacking = true;

        if (!_animationEvents.TryPlayAttack(_currentAttackId))
        {
            CancelAttack();
            return;
        }

        // 취소되어도 공격 간격은 유지
        _remainingAttackTime = _attackInterval;

        Debug.Log($"적 공격 시작: 회차 {_currentAttackId}", this);
    }

    private bool IsPositiveFinite(float value)
    {
        return value > 0f &&
            !float.IsInfinity(value);
    }

    private bool CanReact()
    {
        return _isReactionAllowed &&
            !_statusEffects.IsStunned &&
            !_knockback.IsKnockback &&
            HasValidPlayer &&
            _enemyMovement.isActiveAndEnabled &&
            _enemyMovement.IsMovementPaused;
    }

    private bool IsPlayerAlive(Hero player)
    {
        if (player == null || !player.isActiveAndEnabled)
        {
            return false;
        }

        Health health = player.GetComponent<Health>();
        return health != null && health.IsAlive;
    }

    private bool TryGetAttackTarget(
        Hero player,
        out DamageReceiver receiver)
    {
        receiver = null;

        if (!IsPlayerAlive(player))
        {
            return false;
        }

        float distance = Vector3.Distance(
            transform.position,
            player.transform.position);

        if (distance > _attackRange)
        {
            return false;
        }

        receiver = player.GetComponent<DamageReceiver>();

        if (receiver == null || !receiver.isActiveAndEnabled)
        {
            return false;
        }

        List<IDamageable> targets = new List<IDamageable>();

        _hitDetector.Detect(
            transform.position,
            _attackRange,
            transform.forward,
            _attackAngle,
            _playerMask,
            targets);

        return targets.Contains(receiver);
    }

    // 애니메이션이 시작할 때 기록한 회차로 요청
    public bool TryCommitHit(int attackId)
    {
        bool canCommit =
            isActiveAndEnabled &&
            _isAttacking &&
            attackId == _currentAttackId &&
            !_hasCommittedHit &&
            _enemyCombat.CanReceiveDamage;

        if (!canCommit)
        {
            return false;
        }

        // 빗나가도 이번 타격 기회는 소비
        _hasCommittedHit = true;

        // 목표 공격은 플레이어 반격 횟수를 소비하지 않음
        if (_isDefenseGoalAttack)
        {
            return TryCommitDefenseGoalHit(attackId);
        }

        OnAttackOpportunityUsed?.Invoke();

        if (!CanReact() ||
            !TryGetAttackTarget(
                _attackPlayer, out DamageReceiver receiver))
        {
            Debug.Log($"적 공격 빗나감: 회차 {attackId}", this);
            return false;
        }

        DamageInfo damageInfo = new DamageInfo(
            gameObject,
            gameObject,
            ActorType.Enemy,
            Faction.Enemy,
            _damage,
            AttackType.BasicAttack,
            attackId);

        IDamageable target = receiver;
        float appliedDamage = target.TakeDamage(damageInfo);

        Debug.Log(
            $"적 타격: 회차 {attackId}, 실제 피해 {appliedDamage}",
            this);

        return appliedDamage > 0f;
    }

    public bool CompleteAttack(int attackId)
    {
        if (!_isAttacking || attackId != _currentAttackId)
        {
            return false;
        }

        CancelAttack();
        Debug.Log($"적 공격 종료: 회차 {attackId}", this);
        return true;
    }

    public void CancelAttack()
    {
        if (_animationEvents != null)
        {
            _animationEvents.CancelPlayback(_currentAttackId);
        }

        _isAttacking = false;
        _hasCommittedHit = false;
        _attackPlayer = null;
        _isDefenseGoalAttack = false;
        _attackDefenseGoal = null;
    }

    private void OnDisable()
    {
        CancelAttack();
    }

    // 반격 상태를 소유하는 EnemyReaction에서 호출
    public void SetReactionAllowed(bool isAllowed)
    {
        _isReactionAllowed = isAllowed;

        if (!isAllowed && _isAttacking)
        {
            CancelAttack();
        }
    }

    private bool CanAttackDefenseGoal()
    {
        Stage2DefenseGoal goal = _enemyMovement.DefenseGoal;

        return isActiveAndEnabled &&
            _isInitialized &&
            _enemyCombat.CanReceiveDamage &&
            _enemyMovement.isActiveAndEnabled &&
            _enemyMovement.HasReachedDefenseGoal &&
            _enemyMovement.IsMovementPaused &&
            !_statusEffects.IsStunned &&
            !_knockback.IsKnockback &&
            goal != null &&
            goal.CanReceiveDamage;
    }

    private bool TryGetDefenseGoalTarget(
        Stage2DefenseGoal goal,
        out DamageReceiver receiver)
    {
        receiver = null;

        if (goal == null || !goal.CanReceiveDamage)
        {
            return false;
        }

        Vector3 offset =
            goal.transform.position - transform.position;

        if (offset.magnitude > _attackRange)
        {
            return false;
        }

        // 목표를 향하는 수평 공격 각도 검사
        offset.y = 0f;

        if (offset.sqrMagnitude > 0.0001f &&
            Vector3.Angle(transform.forward, offset) > _attackAngle * 0.5f)
        {
            return false;
        }

        receiver = goal.Receiver;
        return receiver != null && receiver.isActiveAndEnabled;
    }

    private void UpdateDefenseGoalAttack()
    {
        if (!CanAttackDefenseGoal())
        {
            CancelAttack();
            return;
        }

        Stage2DefenseGoal goal = _enemyMovement.DefenseGoal;

        if (_isAttacking)
        {
            // 다른 공격 또는 다른 목표로 시작한 공격은 취소
            if (!_isDefenseGoalAttack || _attackDefenseGoal != goal)
            {
                CancelAttack();
            }

            return;
        }

        if (_remainingAttackTime > 0f ||
            !_animationEvents.CanStartAttack)
        {
            return;
        }

        if (!TryGetDefenseGoalTarget(goal, out _))
        {
            return;
        }

        _currentAttackId++;
        _attackPlayer = null;
        _attackDefenseGoal = goal;
        _isDefenseGoalAttack = true;
        _hasCommittedHit = false;
        _isAttacking = true;

        if (!_animationEvents.TryPlayAttack(_currentAttackId))
        {
            CancelAttack();
            return;
        }

        // 공격이 취소돼도 사용한 공격 간격은 유지
        _remainingAttackTime = _attackInterval;

        Debug.Log(
            $"방어 목표 공격 시작: 회차 {_currentAttackId}",
            this);
    }

    private bool TryCommitDefenseGoalHit(int attackId)
    {
        bool hasValidTarget =
            CanAttackDefenseGoal() &&
            _attackDefenseGoal == _enemyMovement.DefenseGoal;

        if (!hasValidTarget ||
            !TryGetDefenseGoalTarget(
                _attackDefenseGoal,
                out DamageReceiver receiver))
        {
            Debug.Log(
                $"방어 목표 공격 빗나감: 회차 {attackId}",
                this);

            return false;
        }

        DamageInfo damageInfo = new DamageInfo(
            gameObject,
            gameObject,
            ActorType.Enemy,
            Faction.Enemy,
            _damage,
            AttackType.BasicAttack,
            attackId);

        float appliedDamage = receiver.TakeDamage(damageInfo);

        Debug.Log(
            $"방어 목표 타격: 회차 {attackId}, " +
            $"실제 피해 {appliedDamage:F1}",
            this);

        return appliedDamage > 0f;
    }
}