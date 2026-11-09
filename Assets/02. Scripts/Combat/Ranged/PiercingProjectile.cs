using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 관통 투사체 - 대상별 단발 피해와 유효 적중 수 관리
/// </summary>
public class PiercingProjectile : Projectile
{
    HashSet<IDamageable> _processedTargets =
        new HashSet<IDamageable> ( );    //이미 피해를 요청한 대상

    int _maxHitCount;    //최대 유효 적중 대상 수
    int _hitCount;    //실제 피해가 발생한 대상 수

    /// <summary>
    /// 관통 설정 준비 후 공통 비행 초기화
    /// </summary>
    /// <param name="execution">이번 스킬 실행</param>
    /// <param name="data">생성/비행 설정</param>
    /// <param name="launchOrigin">몸 쪽 발사 경로 시작점</param>
    /// <param name="firePoint">실제 발사점</param>
    /// <param name="direction">정규화된 발사 방향</param>
    /// <param name="maxHitCount">최대 유효 적중 대상 수</param>
    public void InitPiercing (
        AttackExecution execution , ProjectileData data ,
        Vector3 launchOrigin , Vector3 firePoint ,
        Vector3 direction , int maxHitCount )
    {
        //부모 Init에서 즉시 차폐 검사가 발생하므로 관통 설정을 먼저 준비
        _maxHitCount = maxHitCount;
        _hitCount = 0;
        _processedTargets.Clear ( );

        base.Init (
            execution , data , launchOrigin , firePoint , direction );
    }

    /// <summary>
    /// 공통 제외 규칙과 이미 처리한 대상의 충돌 제외
    /// </summary>
    /// <param name="collider">충돌 후보</param>
    /// <returns>이번 충돌 처리 대상 여부</returns>
    protected override bool CanCollide ( Collider collider )
    {
        if ( base.CanCollide ( collider ) == false )
            return false;

        IDamageable target =
            collider.GetComponentInParent<IDamageable> ( );

        //피해 수신자가 없는 지형은 충돌 처리 대상으로 유지
        if ( target == null )
            return true;

        return _processedTargets.Contains ( target ) == false;
    }

    /// <summary>
    /// 대상별 피해 요청과 관통 종료 여부 결정
    /// </summary>
    /// <param name="collider">충돌 대상</param>
    /// <param name="point">충돌 위치</param>
    /// <returns>이번 충돌로 종료할지 여부</returns>
    protected override bool ApplyHit ( Collider collider , Vector3 point )
    {
        IDamageable target =
            collider.GetComponentInParent<IDamageable> ( );

        if ( target == null )
        {
            PlayImpactSound( point );
            PlayImpactVfx( point );
            if ( ShowDebugLog == true )
            {
                Debug.Log (
                    $"관통 종료: ID={GetInstanceID ( )}, " +
                    $"회차={Execution.Context.AttackId}, " +
                    $"대상={collider.name}, 사유=지형 충돌" , this );
            }

            return true;
        }

        //피해 이벤트보다 먼저 기록하여 다중 Collider/재접촉 중복 차단
        if ( _processedTargets.Add ( target ) == false )
            return false;

        PlayImpactSound( point );
        PlayImpactVfx( point );

        AttackContext context = Execution.Context;
        string targetName = collider.name;

        DamageInfo damageInfo = new DamageInfo (
            context.Attacker , gameObject ,
            context.ActorType , context.Faction ,
            context.RawDamage , context.AttackType , context.AttackId );

        float appliedDamage = target.TakeDamage ( damageInfo );

        //피해가 거절된 대상도 처리 기록은 유지하지만 관통 수는 소비하지 않음
        if ( appliedDamage > 0f )
            _hitCount++;

        if ( ShowDebugLog == true )
        {
            Debug.Log (
                $"관통 피해 결과: ID={GetInstanceID ( )}, " +
                $"회차={context.AttackId}, 대상={targetName}, " +
                $"원본 피해={context.RawDamage}, 실제 피해={appliedDamage}, " +
                $"유효 적중={_hitCount}/{_maxHitCount}" , this );
        }

        //Skill 실행에는 기본 공격 에너지 충전 콜백이 연결되지 않음
        Execution.ReportAppliedDamage ( appliedDamage );

        bool reachedLimit = _hitCount >= _maxHitCount;

        if ( reachedLimit == true && ShowDebugLog == true )
        {
            Debug.Log (
                $"관통 종료: ID={GetInstanceID ( )}, " +
                $"회차={context.AttackId}, 사유=최대 대상 수 도달" , this );
        }

        return reachedLimit;
    }
}
