using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EnemyAnimationEvents : MonoBehaviour
{
    [SerializeField] private AnimationClip _attackClip;
    [SerializeField] private AnimationClip _deathClip;
    [SerializeField] private AnimationClip _damageClip;

    private EnemyAnimation _enemyAnimation;

    private EnemyCombat _enemyCombat;

    private Animator _animator;
    private EnemyAttack _enemyAttack;

    private int _playbackAttackId;

    private int _attackStateHash =
        Animator.StringToHash("Base Layer.Attack");

    public bool CanStartAttack =>
        isActiveAndEnabled &&
        _animator != null &&
        _animator.isActiveAndEnabled &&
        _animator.runtimeAnimatorController != null &&
        _attackClip != null &&
        _enemyAttack != null &&
        _animator.HasState(0, _attackStateHash) &&
        _playbackAttackId == 0 &&
        !_animator.IsInTransition(0) &&
        _animator.GetCurrentAnimatorStateInfo(0).fullPathHash
            != _attackStateHash;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _enemyAttack = GetComponentInParent<EnemyAttack>();
        _enemyCombat = GetComponentInParent<EnemyCombat>();
        _enemyAnimation = GetComponentInParent<EnemyAnimation>();

        if (_attackClip == null ||
            _deathClip == null ||
            _damageClip == null ||
            _enemyAttack == null ||
            _enemyCombat == null ||
            _enemyAnimation == null)
        {
            Debug.LogError(
                "공격·사망·피격 클립과 부모 컴포넌트를 확인하세요.",
                this);

            enabled = false;
        }
    }

    public bool TryPlayAttack(int attackId)
    {
        if (!CanStartAttack)
        {
            return false;
        }

        // 재생할 때 공격 회차를 기록
        _playbackAttackId = attackId;

        _animator.SetInteger("animation", 3);
        _animator.Play(_attackStateHash, 0, 0f);

        return true;
    }

    public void OnAttackHit(AnimationEvent animationEvent)
    {
        if (!IsExpectedEvent(animationEvent))
        {
            return;
        }

        _enemyAttack.TryCommitHit(_playbackAttackId);
    }

    public void OnAttackEnd(AnimationEvent animationEvent)
    {
        if (!IsExpectedEvent(animationEvent))
        {
            return;
        }

        _enemyAttack.CompleteAttack(_playbackAttackId);
    }

    public void CancelPlayback(int attackId)
    {
        if (_playbackAttackId == attackId)
        {
            _playbackAttackId = 0;
        }
    }

    private bool IsExpectedEvent(AnimationEvent animationEvent)
    {
        return isActiveAndEnabled &&
            _playbackAttackId != 0 &&
            animationEvent.animatorClipInfo.clip == _attackClip &&
            animationEvent.animatorStateInfo.fullPathHash
                == _attackStateHash;
    }

    private void OnDisable()
    {
        if (_enemyAttack != null)
        {
            _enemyAttack.CancelAttack();
        }
    }

    public void OnDeathEnd(AnimationEvent animationEvent)
    {
        bool isExpectedEvent =
            isActiveAndEnabled &&
            _enemyCombat != null &&
            _enemyCombat.IsDead &&
            animationEvent.animatorClipInfo.clip == _deathClip &&
            animationEvent.animatorStateInfo.fullPathHash ==
                Animator.StringToHash("Base Layer.Die");

        if (!isExpectedEvent)
        {
            return;
        }

        _enemyCombat.CompleteDeathPresentation();
    }

    public void OnDamageEnd(AnimationEvent animationEvent)
    {
        bool isExpectedEvent =
            isActiveAndEnabled &&
            _enemyAnimation != null &&
            _enemyCombat != null &&
            !_enemyCombat.IsDead &&
            !_enemyAttack.IsAttacking &&
            animationEvent.animatorClipInfo.clip == _damageClip &&
            animationEvent.animatorStateInfo.fullPathHash ==
                Animator.StringToHash("Base Layer.Damage");

        if (!isExpectedEvent)
        {
            return;
        }

        _enemyAnimation.CompleteDamagePresentation();
    }
}