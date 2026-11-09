using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(EnemyMovement))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyAttack))]
[RequireComponent(typeof(EnemyCombat))]
public class EnemyAnimation : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private float _moveThreshold = 0.05f;

    private EnemyMovement _enemyMovement;
    private NavMeshAgent _navMeshAgent;

    private EnemyAttack _enemyAttack;
    private EnemyCombat _enemyCombat;

    private DamageReceiver _damageReceiver;
    private Health _health;
    private bool _isPlayingDamage;

    private void Awake()
    {
        _enemyMovement = GetComponent<EnemyMovement>();
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _enemyAttack = GetComponent<EnemyAttack>();
        _enemyCombat = GetComponent<EnemyCombat>();
        _damageReceiver = GetComponent<DamageReceiver>();
        _health = GetComponent<Health>();

        bool hasValidSettings =
            _animator != null &&
            _animator.runtimeAnimatorController != null &&
            _moveThreshold > 0f &&
            !float.IsInfinity(_moveThreshold);

        if (!hasValidSettings)
        {
            Debug.LogError(
                "적 Animator, Controller와 이동 기준 속도를 확인하세요.",
                this);

            enabled = false;
            return;
        }

        bool hasAnimationParameter = false;

        foreach (AnimatorControllerParameter parameter
                 in _animator.parameters)
        {
            if (parameter.name == "animation" &&
                parameter.type == AnimatorControllerParameterType.Int)
            {
                hasAnimationParameter = true;
                break;
            }
        }

        if (!hasAnimationParameter)
        {
            Debug.LogError(
                "Animator에 Int 타입 animation 파라미터가 필요합니다.",
                this);

            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!_animator.isActiveAndEnabled)
        {
            return;
        }

        if (_enemyCombat.IsDead)
        {
            _isPlayingDamage = false;
            return;
        }

        if (_enemyAttack.IsAttacking)
        {
            // 공격이 피격 모션을 대체한 경우 피격 상태 정리
            _isPlayingDamage = false;
            return;
        }

        if (_isPlayingDamage)
        {
            return;
        }

        bool canMove =
            _enemyMovement.isActiveAndEnabled &&
            !_enemyMovement.IsMovementPaused &&
            !_enemyMovement.HasPassed;

        bool isMoving =
            canMove &&
            _enemyMovement.Velocity.sqrMagnitude >
            _moveThreshold * _moveThreshold;

        int animationValue = isMoving ? 2 : 1;

        if (_animator.GetInteger("animation") != animationValue)
        {
            _animator.SetInteger("animation", animationValue);
        }
    }

    private void OnEnable()
    {
        _enemyCombat.OnDied += HandleEnemyDied;
        _damageReceiver.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        if (_enemyCombat != null)
        {
            _enemyCombat.OnDied -= HandleEnemyDied;
        }

        if (_damageReceiver != null)
        {
            _damageReceiver.OnDamaged -= HandleDamaged;
        }

        _isPlayingDamage = false;
    }

    private void HandleEnemyDied(
        EnemyCombat enemy,
        DamageInfo damageInfo)
    {
        _isPlayingDamage = false;

        if (_animator == null || !_animator.isActiveAndEnabled)
        {
            return;
        }

        // Gazer 컨트롤러: 5는 Die
        _animator.SetInteger("animation", 5);
        _animator.Play("Base Layer.Die", 0, 0f);
    }

    private void HandleDamaged(
    DamageInfo damageInfo,
    float appliedDamage)
    {
        bool canPlayDamage =
            _enemyCombat.CanReceiveDamage &&
            _health.IsAlive &&
            !_enemyAttack.IsAttacking &&
            !_isPlayingDamage &&
            _animator != null &&
            _animator.isActiveAndEnabled;

        if (!canPlayDamage)
        {
            return;
        }

        // 연속 피격으로 같은 모션을 계속 처음부터 재생하지 않음
        _isPlayingDamage = true;

        _animator.SetInteger("animation", 4);
        _animator.Play("Base Layer.Damage", 0, 0f);
    }

    public void CompleteDamagePresentation()
    {
        if (!_isPlayingDamage)
        {
            return;
        }

        _isPlayingDamage = false;
    }
}