using System;
using UnityEngine;

/// <summary>
/// 피해 수신 모듈 - 피해 검증, 방어 계산, 결과 알림
/// </summary>
public class DamageReceiver : MonoBehaviour, IDamageable
{
    Health _health;    //초기화 시 전달받은 체력 모듈

    GameObject _owner;      //피해 수신 주체
    Faction _faction;       //소속 진영
    float _damageReductionRate;     //최종 피해 감소율
    bool _isInvulnerable;       //무적 여부

    /// <summary>
    /// 초기화 시 전달받은 소속 진영
    /// </summary>
    public Faction Faction => _faction;

    /// <summary>
    /// 연결된 체력 모듈이 활성 상태이며 생존했는지 여부
    /// </summary>
    public bool IsAlive =>
        _health != null
        && _health.isActiveAndEnabled
        && _health.IsAlive;

    /// <summary>
    /// 피격 이벤트(피해 정보, 피해량)
    /// </summary>
    public event Action<DamageInfo, float> OnDamaged;

    /// <summary>
    /// 사망 이벤트(피해 정보)
    /// </summary>
    public event Action<DamageInfo> OnDied;

    /// <summary>
    /// 피해 수신 정보 초기화
    /// </summary>
    /// <param name="owner">피해 대상 오브젝트</param>
    /// <param name="health">체력 모듈</param>
    /// <param name="faction">소속 진영</param>
    /// <param name="damageReductionRate">최종 피해 감소율</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        GameObject owner, Health health,
        Faction faction, float damageReductionRate )
    {
        //유한하지 않은 설정은 피해 계산에 사용하지 않음
        if ( float.IsNaN( damageReductionRate ) == true
            || float.IsInfinity( damageReductionRate ) == true )
        {
            Debug.LogError( "피해 감소율은 유한한 값이어야 합니다.", this );
            return false;
        }

        _owner = owner;
        _health = health;
        _faction = faction;

        //피해 감소율 범위 제한
        _damageReductionRate = Mathf.Clamp01( damageReductionRate );
        _isInvulnerable = false;

        return true;
    }

    /// <summary>
    /// 적대 효과 수신 가능 여부 확인
    /// </summary>
    /// <param name="context">효과 발생 정보</param>
    /// <returns>공통 수신 조건 충족 여부</returns>
    public bool CanReceiveEffect ( EffectContext context )
    {
        return _health != null
            && isActiveAndEnabled
            && _health.IsAlive
            && _isInvulnerable == false
            && context.Attacker != _owner
            && context.Faction != _faction;
    }

    /// <summary>
    /// 피해 요청을 처리
    /// </summary>
    /// <param name="damageInfo">피해 정보</param>
    /// <returns>실제 감소한 체력</returns>
    public float TakeDamage ( DamageInfo damageInfo )
    {
        //기존 IDamageable 호출은 피해량만 받고 사망 결과는 새 처리 함수에 위임
        return TakeDamage ( damageInfo, out _ );
    }

    /// <summary>
    /// 피해 요청 처리와 이번 피해의 사망 결과 반환
    /// </summary>
    /// <param name="damageInfo">피해 정보</param>
    /// <param name="killedTarget">이번 피해로 사망했는지 여부</param>
    /// <returns>실제 감소한 체력</returns>
    public float TakeDamage ( DamageInfo damageInfo, out bool killedTarget )
    {
        //피해 거절 경로에서도 이번 요청이 사망을 발생시키지 않았음을 반환
        killedTarget = false;

        float rawDamage = damageInfo.RawDamage;

        //잘못된 값이 피해량으로 들어왔으면 종료
        if ( rawDamage > 0f == false
            || float.IsInfinity( rawDamage ) == true )
            return 0f;

        //자기 자신에게 적용되는 피해는 거절
        if ( damageInfo.Attacker == _owner )
            return 0f;

        //같은 진영의 피해는 거절
        if ( damageInfo.Faction == _faction )
            return 0f;

        //무적 상태면 피해를 적용하지 않음
        if ( _isInvulnerable == true )
            return 0f;

        //실제 대미지 계산
        float damage = rawDamage * ( 1f - _damageReductionRate );

        //실제 체력 감소와 사망 판정은 Health에서 처리
        float appliedDamage = _health.TakeHit( damage, out killedTarget );

        //사망한 대상이나 체력 변화가 없는 요청은 알리지 않음
        if ( appliedDamage <= 0f )
            return 0f;

        //Health가 반환한 이번 피해 결과로 알림
        //이벤트 이후 체력을 다시 읽지 않고 같은 결과를 호출자에게도 전달
        OnDamaged?.Invoke( damageInfo, appliedDamage );

        if ( killedTarget == true )
            OnDied?.Invoke( damageInfo );

        return appliedDamage;
    }

    /// <summary>
    /// 무적 상태 설정
    /// </summary>
    /// <param name="isInvulnerable">무적 여부</param>
    public void SetInvulnerable ( bool isInvulnerable )
    {
        _isInvulnerable = isInvulnerable;
    }
}
