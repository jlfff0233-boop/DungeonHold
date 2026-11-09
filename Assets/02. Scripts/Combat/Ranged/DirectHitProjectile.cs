using UnityEngine;

/// <summary>
/// 직격 투사체 - 첫 충돌 대상의 단발 피해 처리
/// </summary>
public class DirectHitProjectile : Projectile
{
    /// <summary>
    /// 충돌한 피해 수신자에 직격 피해 적용
    /// </summary>
    /// <param name="collider">충돌 대상</param>
    /// <param name="point">충돌 위치</param>
    /// <returns>첫 충돌 종료 여부</returns>
    protected override bool ApplyHit ( Collider collider , Vector3 point )
    {
        PlayImpactSound( point );
        PlayImpactVfx( point );
        IDamageable target =
            collider.GetComponentInParent<IDamageable> ( );

        if ( target == null )
        {
            if ( ShowDebugLog == true )
            {
                Debug.Log (
                    $"투사체 피해 없음: ID={GetInstanceID ( )}, " +
                    $"회차={Execution.Context.AttackId}, 대상={collider.name}, " +
                    $"사유=피해 수신자 없음",
                    this );
            }

            return true;
        }

        AttackContext context = Execution.Context;

        DamageInfo damageInfo = new DamageInfo (
            context.Attacker ,
            gameObject ,
            context.ActorType ,
            context.Faction ,
            context.RawDamage ,
            context.AttackType ,
            context.AttackId );

        float appliedDamage = target.TakeDamage ( damageInfo );

        if ( ShowDebugLog == true )
        {
            Debug.Log (
                $"투사체 피해 결과: ID={GetInstanceID ( )}, " +
                $"회차={context.AttackId}, 대상={collider.name}, " +
                $"원본 피해={context.RawDamage}, 실제 피해={appliedDamage}",
                this );
        }

        Execution.ReportAppliedDamage ( appliedDamage );

        //일반 화살은 기존처럼 첫 충돌에서 종료
        return true;
    }
}
