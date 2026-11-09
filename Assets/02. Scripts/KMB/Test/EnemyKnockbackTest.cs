using UnityEngine;

public class EnemyKnockbackTest : MonoBehaviour
{
    [SerializeField] private EnemyCombat _targetEnemy;
    [SerializeField] private Hero _player;
    [SerializeField] private KnockbackData _knockbackData;

    [ContextMenu("테스트/적 넉백 적용")]
    private void ApplyKnockbackForTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Play 모드에서 실행하세요.", this);
            return;
        }

        if (_targetEnemy == null ||
            !_targetEnemy.CanReceiveDamage ||
            _player == null ||
            _knockbackData == null)
        {
            Debug.LogWarning(
                "생존 적, 플레이어와 넉백 데이터를 연결하세요.",
                this);

            return;
        }

        KnockbackController knockback =
            _targetEnemy.GetComponent<KnockbackController>();

        if (knockback == null || !knockback.isActiveAndEnabled)
        {
            Debug.LogWarning("적의 KnockbackController를 확인하세요.", this);
            return;
        }

        Vector3 direction =
            _targetEnemy.transform.position - _player.transform.position;

        EffectContext context = new EffectContext(
            _player.gameObject,
            gameObject,
            ActorType.Player,
            Faction.Ally,
            AttackType.Skill,
            0);

        bool applied = knockback.TryApply(
            new KnockbackInfo(
                context,
                _knockbackData,
                direction));

        Debug.Log(
            $"넉백 요청: 대상 {_targetEnemy.name}, 적용 {applied}, " +
            $"진행 중 {knockback.IsKnockback}",
            this);
    }
}