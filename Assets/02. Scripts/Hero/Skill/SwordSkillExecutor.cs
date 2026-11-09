using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 검 스킬 실행 모듈 - 전방 범위 피해와 넉백 요청
/// </summary>
public class SwordSkillExecutor : SkillExecutor
{
    [Header ( "----- 컴포넌트 -----" )]
    [SerializeField] HitDetector _hitDetector;      //공통 타격 탐색 모듈

    [Header ( "----- 타격 기준 -----" )]
    [SerializeField] Transform _detectionCenter;        //검 스킬 탐색 중심
    [SerializeField] Transform _directionTarget;        //캐릭터 전방 기준
    [SerializeField] LayerMask _layerMask;      //탐색 대상 레이어
    [SerializeField] VfxEffect _attackVfx; //실제 스킬 실행의 단발 효과

    float _knockbackDistance;       //이번 스킬 시작 시 보존한 넉백 거리
    float _knockbackDuration;       //이번 스킬 시작 시 보존한 넉백 진행 시간

    /// <summary>
    /// 검 스킬 설정을 검사하고 넉백 수치 보존
    /// </summary>
    /// <param name="skillData">검 스킬 설정</param>
    /// <returns>실행 준비 성공 여부</returns>
    public override bool TryPrepare ( SkillData skillData )
    {
        SwordSkillData swordSkillData = skillData as SwordSkillData;

        //검 전용 설정의 식별은 여기서만 처리하여 공통 SkillCaster와 분리
        if ( swordSkillData == null )
        {
            Debug.LogError (
                "검 스킬 실행 모듈에는 SwordSkillData를 연결해야 합니다." , this );

            return false;
        }

        KnockbackData knockbackData = swordSkillData.KnockbackData;

        if ( knockbackData == null )
        {
            Debug.LogError ( "검 스킬의 넉백 설정이 연결되지 않았습니다." , this );

            return false;
        }

        float distance = knockbackData.Distance;
        float duration = knockbackData.Duration;

        //설정 오류는 에너지 소비 전에 거절하며
        //이전 실행값을 일부만 덮어쓰지 않음
        if ( distance <= 0f || duration <= 0f )
        {
            Debug.LogError (
                "검 스킬 넉백 거리와 시간은 양수여야 합니다." , this );

            return false;
        }

        //시작 이후 SO 값이 바뀌어도 이번 회차는 보존한 수치를 사용
        _knockbackDistance = distance;
        _knockbackDuration = duration;

        return true;
    }

    /// <summary>
    /// 승인된 검 스킬의 단발 범위 피해와 넉백 실행
    /// </summary>
    /// <param name="execution">이번 검 스킬 실행</param>
    protected override void ExecuteAttack ( AttackExecution execution )
    {
        AttackContext context = execution.Context;
        //실행별 목록을 사용해 피해 이벤트 중 다른 탐색이 발생해도 유지
        //동일 수신자의 여러 Collider는 기존 HitDetector가 한 번으로 합침
        List<IDamageable> targets = new List<IDamageable> ( );

        //거리/시간은 시작 때, 탐색 위치/전방은 실제 타격 때 확정
        //첫 대상의 피해 이벤트에서 공격자가 회전해도 나머지 대상의 방향은 유지
        Vector3 center = _detectionCenter.position;
        Vector3 forward = _directionTarget.forward;

        if ( _attackVfx != null )
            Vfx.Play( _attackVfx, center, Quaternion.LookRotation( forward, Vector3.up ) );

        _hitDetector.Detect (
            center , context.Radius ,
            forward , context.Angle ,
            _layerMask , targets );

        //피해와 넉백에 동일한 공격 주체/발생원/분류/원래 회차를 전달
        DamageInfo damageInfo = new DamageInfo (
            context.Attacker , context.Source ,
            context.ActorType , context.Faction ,
            context.RawDamage , context.AttackType , context.AttackId );

        EffectContext effectContext = new EffectContext (
            context.Attacker , context.Source ,
            context.ActorType , context.Faction ,
            context.AttackType , context.AttackId );

        //대상별 이벤트가 발생하기 전에 이번 넉백 요청값도 확정
        //모든 대상은 같은 전방으로 밀며 수평화/정규화는 수신 모듈에서 처리
        KnockbackInfo knockbackInfo = new KnockbackInfo (
            effectContext ,
            _knockbackDistance ,
            _knockbackDuration ,
            forward );

        float totalDamage = 0f;

        foreach ( IDamageable target in targets )
        {
            //행동 취소나 사망과 별개로 실행 자체가 정리된 경우 중단
            if ( execution.IsActive == false )
                break;

            //현재 HP를 나중에 읽지 않고 이번 피해의 실제 사망 결과를 받음
            bool killedTarget = false;
            float appliedDamage;

            if ( target is DamageReceiver receiver )
            {
                appliedDamage = receiver.TakeDamage ( damageInfo, out killedTarget );
            }
            else
            {
                //다른 IDamageable은 기존 피해 계약 유지, 치명타 넉백은 공통 수신자만 지원
                appliedDamage = target.TakeDamage ( damageInfo );
            }

            totalDamage += appliedDamage;

            //피해 이벤트에서 실행이 정리됐다면 후속 넉백도 중단
            if ( execution.IsActive == false )
                break;

            //실제 피해가 발생한 대상만 넉백 요청
            if ( appliedDamage <= 0f )
                continue;

            Component component = target as Component;

            //피해 이벤트에서 파괴된 Unity 객체도 제외
            if ( component == null )
                continue;

            //피해 수신자와 넉백 모듈은 같은 주체 오브젝트에 배치
            //넉백 모듈이 없는 대상은 피해 처리만 완료
            KnockbackController knockback =
                component.GetComponent<KnockbackController> ( );

            if ( knockback == null )
                continue;

            //사망 알림까지 완료된 같은 타격의 결과를 즉시 전달
            //생존 대상은 일반 넉백, 이번 치명타의 대상은 사망 후 이동으로 처리
            knockback.TryApplyFromHit ( knockbackInfo, appliedDamage, killedTarget );
        }

        execution.ReportAppliedDamage ( totalDamage );
        execution.Complete ( );
    }
}
