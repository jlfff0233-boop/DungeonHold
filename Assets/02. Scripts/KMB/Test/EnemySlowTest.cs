using UnityEngine;

public class EnemySlowTest : MonoBehaviour
{
    [SerializeField] private EnemyCombat _targetEnemy;
    [SerializeField] private SlowEffectData _slowData;

    [ContextMenu("테스트/적 감속 적용")]
    private void ApplySlowForTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Play 모드에서 실행하세요.", this);
            return;
        }

        if (_targetEnemy == null ||
            !_targetEnemy.CanReceiveDamage ||
            _slowData == null)
        {
            Debug.LogWarning(
                "초기화된 생존 적과 감속 데이터를 연결하세요.",
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
            new SlowEffectInfo(context, _slowData));

        Debug.Log(
            $"감속 요청: 대상 {_targetEnemy.name}, " +
            $"기록 변경 {changed}, " +
            $"최종 이동 배율 {effects.MoveSpeedMultiplier}",
            this);
    }
}