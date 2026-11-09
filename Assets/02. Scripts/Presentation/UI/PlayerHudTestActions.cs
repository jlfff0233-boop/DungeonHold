using LJTest;
using UnityEngine;

// UI-MapTest 전용 버튼 어댑터. 초기화와 게임 판정은 각 실행부에 맡긴다.
[DisallowMultipleComponent]
public class PlayerHudTestActions : MonoBehaviour
{
    [SerializeField] private TestScene _scene;
    [SerializeField] private Hero _hero;
    [SerializeField] private CombatDummy _dummy;

    private bool CanTest => Application.isPlaying && isActiveAndEnabled &&
        _scene != null && _scene.isActiveAndEnabled &&
        _scene.IsStageReady && !_scene.IsEndingStage && _hero != null;

    public void HitPlayer(float damage)
    {
        if (!CanTest || damage <= 0f || float.IsNaN(damage) ||
            float.IsInfinity(damage))
            return;

        Health health = _hero.GetComponent<Health>();
        DamageReceiver receiver = _hero.GetComponent<DamageReceiver>();
        if (health == null || !health.IsAlive || receiver == null ||
            !receiver.isActiveAndEnabled)
            return;

        receiver.TakeDamage(new DamageInfo(gameObject, gameObject,
            ActorType.Enemy, Faction.Enemy, damage, AttackType.Skill, 0));
    }

    public void RevivePlayer(float hp)
    {
        if (!CanTest || hp <= 0f || float.IsNaN(hp) || float.IsInfinity(hp))
            return;

        Health health = _hero.GetComponent<Health>();
        if (health != null && !health.IsAlive)
            _hero.Revive(hp);
    }

    public void CompleteDeathPresentationForTest()
    {
        if (CanTest)
            _hero.CompleteDeathPresentation();
    }

    public void CompleteRevivePresentationForTest()
    {
        if (CanTest)
            _hero.CompleteRevivePresentation();
    }

    public void ApplyPlayerKnockback(KnockbackData data)
    {
        if (!CanTest || data == null)
            return;

        KnockbackController knockback = _hero.GetComponent<KnockbackController>();
        if (knockback == null || !knockback.isActiveAndEnabled)
            return;

        EffectContext context = new EffectContext(gameObject, gameObject,
            ActorType.Enemy, Faction.Enemy, AttackType.Skill, 0);
        knockback.TryApply(new KnockbackInfo(context, data, _hero.transform.forward));
    }

    public void ApplyStatus(StatusEffectData data)
    {
        if (!CanTest || _dummy == null || !_dummy.isActiveAndEnabled ||
            !_dummy.IsInitialized || data == null)
            return;

        StatusEffectController effects = _dummy.GetComponent<StatusEffectController>();
        if (effects == null || !effects.isActiveAndEnabled)
            return;

        EffectContext context = new EffectContext(_hero.gameObject, _hero.gameObject,
            ActorType.Player, Faction.Ally, AttackType.Skill, 0);
        if (data is BurnEffectData burn)
            effects.TryApply(new BurnEffectInfo(context, burn));
        else if (data is SlowEffectData slow)
            effects.TryApply(new SlowEffectInfo(context, slow));
        else if (data is StunEffectData stun)
            effects.TryApply(new StatusEffectInfo(context, stun));
    }
}
