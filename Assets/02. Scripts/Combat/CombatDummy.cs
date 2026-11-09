using UnityEngine;

/// <summary>
/// 전투 더미 - 피해 수신 초기화와 상태이상, 넉백, 중력 갱신
/// </summary>
[RequireComponent ( typeof ( Health ), typeof ( DamageReceiver ), typeof ( Mover ) )]
[RequireComponent ( typeof ( StatusEffectController ), typeof ( KnockbackController ) )]
public class CombatDummy : MonoBehaviour
{
    [Header ( "----- 전투 설정 -----" )]
    [SerializeField] CombatStatsData _combatStatsData;    //체력과 방어 설정
    [SerializeField] StatusResistanceData _statusResistance;    //상태이상 저항 설정

    Health _health;    //같은 더미의 체력
    DamageReceiver _damageReceiver;    //같은 더미의 피해 수신
    Mover _mover;    //중력과 강제 이동
    StatusEffectController _statusEffects;    //상태이상 갱신
    KnockbackController _knockback;    //넉백 갱신
    bool _isInitialized;    //전투 초기화 완료 여부

    /// <summary>
    /// 피해 수신과 프레임 갱신 준비 완료 여부
    /// </summary>
    public bool IsInitialized => _isInitialized;

    private void Awake ( )
    {
        _health = GetComponent<Health> ( );
        _damageReceiver = GetComponent<DamageReceiver> ( );
        _mover = GetComponent<Mover> ( );
        _statusEffects = GetComponent<StatusEffectController> ( );
        _knockback = GetComponent<KnockbackController> ( );
    }

    private void Start ( )
    {
        //구독자의 OnEnable 이후 준비하여 최초 HP 알림도 전달
        if ( Init ( ) == false )
            Debug.LogError ( "전투 더미 초기화에 실패했습니다.", this );
    }

    /// <summary>
    /// 공통 피해와 효과 모듈 준비 후 더미 체력 초기화
    /// </summary>
    /// <returns>전체 초기화 성공 여부</returns>
    public bool Init ( )
    {
        _isInitialized = false;
        _mover.SetMoveEnabled ( false );

        if ( _damageReceiver.Init (
            gameObject, _health, Faction.Enemy,
            _combatStatsData.DamageReductionRate ) == false )
            return false;

        if ( _statusEffects.Init (
            this, _health, _damageReceiver, _mover, _statusResistance ) == false )
            return false;

        if ( _knockback.Init (
            this, _health, _damageReceiver, _mover ) == false )
            return false;

        //효과와 강제 이동의 기존 상태를 정리한 뒤 HP 변경 알림 발행
        _health.Init ( _combatStatsData.MaxHp );
        _isInitialized = _health.IsAlive
            && _health.MaxHp == _combatStatsData.MaxHp;
        return _isInitialized;
    }

    private void Update ( )
    {
        if ( _isInitialized == false )
            return;

        float deltaTime = Time.deltaTime;
        _statusEffects.UpdateEffects ( deltaTime );

        if ( _knockback.IsKnockback == true )
        {
            //넉백 종료 프레임도 중력을 이미 처리하므로 일반 이동 생략
            _knockback.UpdateKnockback ( deltaTime );
            return;
        }

        _mover.UpdateMove ( deltaTime );
    }

    private void OnDisable ( )
    {
        _knockback.CancelKnockback ( );
    }
}
