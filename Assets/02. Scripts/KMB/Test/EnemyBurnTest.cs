using UnityEngine;

public class EnemyBurnTest : MonoBehaviour
{
    [SerializeField] private EnemyCombat _targetEnemy;
    [SerializeField] private Hero _player;
    [SerializeField] private BurnEffectData _burnData;

    [ContextMenu("테스트/적 화상 적용")]
    private void ApplyBurnForTest()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Play 모드에서 실행하세요.", this);
            return;
        }

        if (_targetEnemy == null ||
            !_targetEnemy.CanReceiveDamage ||
            _player == null ||
            _burnData == null)
        {
            Debug.LogWarning(
                "초기화된 생존 적, 플레이어와 화상 데이터를 연결하세요.",
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

        // 플레이어 스킬의 효과 요청을 흉내 내는 테스트
        // 공격 주체는 Hero, 요청 발생원은 이 테스트 오브젝트
        EffectContext context = new EffectContext(
            _player.gameObject,
            gameObject,
            ActorType.Player,
            Faction.Ally,
            AttackType.Skill,
            0);

        bool changed = effects.TryApply(
            new BurnEffectInfo(context, _burnData));

        Debug.Log(
            $"화상 요청: 대상 {_targetEnemy.name}, " +
            $"기록 변경 {changed}, 화상 {effects.IsBurning}, " +
            $"설정 피해 {_burnData.RawDamage}, " +
            $"간격 {_burnData.TickInterval}초",
            this);
    }
}