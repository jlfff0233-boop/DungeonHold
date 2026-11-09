using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 폭발 투사체 - 최초 충돌 위치의 범위 피해와 화상 적용
/// </summary>
public class ExplosionProjectile : Projectile
{
    [Header ( "----- 폭발 -----" )]
    [SerializeField] HitDetector _hitDetector;    //중복 수신자를 제거하는 범위 탐색 모듈
    [SerializeField] LayerMask _targetMask;    //폭발 피해 대상 레이어

    [Header ( "----- 화상 -----" )]
    [SerializeField] BurnEffectData _burnData;    //발사 시 보존할 화상 설정

    BurnEffectInfo _burnInfo;    //이번 발사에 확정한 화상 정보

    /// <summary>
    /// 발사 시 화상 정보를 보존하여 최초 차폐 충돌에도 동일하게 적용
    /// </summary>
    protected override void PrepareHit ( )
    {
        AttackContext context = Execution.Context;
        EffectContext effectContext = new EffectContext (
            context.Attacker , gameObject , context.ActorType , context.Faction ,
            context.AttackType , context.AttackId );

        _burnInfo = new BurnEffectInfo ( effectContext , _burnData );
    }

    /// <summary>
    /// 첫 충돌에서 폭발 피해만 적용하고 비행 종료 요청
    /// </summary>
    /// <param name="collider">충돌 대상</param>
    /// <param name="point">폭발 중심이 되는 충돌 위치</param>
    /// <returns>폭발 후 투사체 종료 여부</returns>
    protected override bool ApplyHit ( Collider collider , Vector3 point )
    {
        //폭발 대상 수와 무관하게 발생 위치에서 한 번 재생
        PlayImpactSound( point );
        PlayImpactVfx( point );
        AttackExecution execution = Execution;
        AttackContext context = execution.Context;
        BurnEffectInfo burnInfo = _burnInfo;
        List<IDamageable> targets = new List<IDamageable> ( );

        //비행은 벽에 막히지만 폭발은 벽 차폐 없이 구형 범위로 탐색
        _hitDetector.Detect (
            point , context.Radius , Vector3.forward , 360f , _targetMask , targets );

        if ( ShowDebugLog == true )
        {
            Debug.Log (
                $"폭발 탐색: ID={GetInstanceID ( )}, " +
                $"분류={context.AttackType}, 회차={context.AttackId}, " +
                $"위치={point}, 반경={context.Radius}, " +
                $"원본 피해={context.RawDamage}, 대상 수={targets.Count}",
                this );
        }

        DamageInfo damageInfo = new DamageInfo (
            context.Attacker , gameObject , context.ActorType , context.Faction ,
            context.RawDamage , context.AttackType , context.AttackId );
        float appliedTotal = 0f;

        foreach ( IDamageable target in targets )
        {
            //피해 이벤트에서 실행이 무효화되면 후속 대상의 피해 중단
            if ( execution.IsActive == false )
                break;

            Component component = target as Component;
            if ( target is Component && ( component == null
                || component.gameObject.activeInHierarchy == false ) )
                continue;

            //피해 콜백에서 대상이 제거돼도 로그에 원래 대상 이름을 표시
            string targetName = component != null ? component.name : target.ToString ( );

            float appliedDamage = target.TakeDamage ( damageInfo );
            appliedTotal += appliedDamage;

            if ( ShowDebugLog == true )
            {
                Debug.Log (
                    $"폭발 피해: ID={GetInstanceID ( )}, " +
                    $"분류={context.AttackType}, 회차={context.AttackId}, " +
                    $"대상={targetName}, 원본 피해={context.RawDamage}, " +
                    $"실제 피해={appliedDamage}",
                    this );
            }

            //TakeDamage가 호출하는 사망/피해 이벤트를 마친 뒤 실행 상태 재확인
            if ( execution.IsActive == false )
                break;

            if ( appliedDamage <= 0f || component == null )
                continue;

            //상태 이상 모듈은 선택 사항이며
            //사망/면역 여부는 수신 모듈에서 판단
            StatusEffectController effects = component.GetComponent<StatusEffectController> ( );
            if ( effects != null )
            {
                bool applied = effects.TryApply ( burnInfo );

                if ( ShowDebugLog == true )
                {
                    Debug.Log (
                        $"폭발 화상 요청: ID={GetInstanceID ( )}, " +
                        $"회차={context.AttackId}, 대상={targetName}, " +
                        $"적용={applied}, 주기당 원본 피해={burnInfo.RawDamage}, " +
                        $"간격={burnInfo.TickInterval}",
                        this );
                }
            }
            else if ( ShowDebugLog == true )
            {
                Debug.Log (
                    $"폭발 화상 생략: ID={GetInstanceID ( )}, " +
                    $"회차={context.AttackId}, 대상={targetName}, " +
                    $"사유=상태이상 모듈 없음",
                    this );
            }
        }

        if ( ShowDebugLog == true )
        {
            Debug.Log (
                $"폭발 처리 결과: ID={GetInstanceID ( )}, " +
                $"분류={context.AttackType}, 회차={context.AttackId}, " +
                $"실제 피해 합계={appliedTotal}, 실행 유효={execution.IsActive}",
                this );
        }

        //기본 공격만 충전 콜백을 보유하며
        //폭발 대상 수와 무관하게 한 번 보고
        execution.ReportAppliedDamage ( appliedTotal );

        //Complete/Destroy는 Projectile이 담당하고
        //이미 부여한 화상은 대상이 관리
        return true;
    }
}
