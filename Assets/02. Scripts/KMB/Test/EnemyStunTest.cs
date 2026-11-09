using UnityEngine;

public class EnemyStunTest : MonoBehaviour
{
    [SerializeField] private EnemyCombat _targetEnemy;
    [SerializeField] private StunEffectData _stunData;

    [ContextMenu("테스트/적 기절 적용")]
    private void ApplyStunForTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Play 모드에서 실행하세요.", this);
            return;
        }

        if (_targetEnemy == null ||
            !_targetEnemy.CanReceiveDamage ||
            _stunData == null)
        {
            Debug.LogWarning(
                "초기화된 생존 적과 기절 데이터를 연결하세요.",
                this);

            return;
        }

        StatusEffectController effects =
            _targetEnemy.GetComponent<StatusEffectController>();

        if (effects == null || !effects.isActiveAndEnabled)
        {
            Debug.LogWarning(
                "적의 StatusEffectController를 확인하세요.",
                this);

            return;
        }

        // 설치물에서 발생한 효과를 흉내 내는 테스트 요청
        EffectContext context = new EffectContext(
            gameObject,
            gameObject,
            ActorType.Installation,
            Faction.Ally,
            AttackType.BasicAttack,
            0);

        bool changed = effects.TryApply(
            new StatusEffectInfo(context, _stunData));

        Debug.Log(
            $"기절 요청: 대상 {_targetEnemy.name}, " +
            $"기록 변경 {changed}, 기절 {effects.IsStunned}, " +
            $"남은 시간 {effects.RemainingStunTime:F2}초",
            this);
    }
}