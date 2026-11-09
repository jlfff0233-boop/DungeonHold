using UnityEngine;

namespace LJTest
{
    /// <summary>
    /// 피해 대상 테스트 - 상태이상과 넉백 요청, 전투 결과 확인
    /// </summary>
    [RequireComponent ( typeof ( CombatDummy ) )]
    public class DamageTargetTest : MonoBehaviour
    {
        [Header ( "----- 디버그 -----" )]
        [SerializeField] bool _showDebugLog = true;     //일반 디버그 로그 출력 여부

        [Header( "----- 컴포넌트 -----" )]
        [SerializeField] Health _health;                    //대상 체력 모듈
        [SerializeField] DamageReceiver _damageReceiver;    //대상 피해 수신 모듈
        [SerializeField] StatusEffectController _statusEffects;  //효과 모듈
        [SerializeField] KnockbackController _knockback;    //강제 이동 모듈

        [Header( "----- 테스트 공격자 -----" )]
        [SerializeField] Transform _hero;    //효과 발생원과 넉백 방향 기준

        CombatDummy _dummy;    //초기화와 갱신을 담당하는 같은 오브젝트의 실행부

        /// <summary>
        /// 무작위 넉백 테스트의 활성, 초기화와 생존 조건 충족 여부
        /// </summary>
        public bool CanTestKnockback =>
            isActiveAndEnabled && _dummy.IsInitialized && _health.IsAlive;

        private void Awake ( )
        {
            _dummy = GetComponent<CombatDummy> ( );
        }

        private void OnEnable ()
        {
            //초기 HP 알림부터 확인할 수 있도록 먼저 구독
            _health.OnHpChanged += HandleHpChanged;
            _health.OnRevived += HandleRevived;
            _damageReceiver.OnDamaged += HandleDamaged;
            _damageReceiver.OnDied += HandleDied;
        }

        /// <summary>
        /// 플레이어를 발생원으로 더미 상태이상 요청
        /// </summary>
        /// <param name="data">필수 상태이상 설정</param>
        public void ApplyStatus ( StatusEffectData data )
        {
            if ( isActiveAndEnabled == false
                || _dummy.IsInitialized == false || _health.IsAlive == false )
                return;

            EffectContext context = new EffectContext(
                _hero.gameObject, _hero.gameObject,
                ActorType.Player, Faction.Ally, AttackType.Skill, 0 );

            //피해 계산은 공통 수신 측에 맡기고 여기서는 효과별 요청만 생성
            bool changed;
            if ( data is BurnEffectData burnData )
                changed = _statusEffects.TryApply( new BurnEffectInfo( context, burnData ) );
            else if ( data is SlowEffectData slowData )
                changed = _statusEffects.TryApply( new SlowEffectInfo( context, slowData ) );
            else if ( data is StunEffectData stunData )
                changed = _statusEffects.TryApply( new StatusEffectInfo( context, stunData ) );
            else
                changed = false;

            if ( _showDebugLog == true )
                Debug.Log(
                    $"{name} 효과 요청: 설정={data.name} / 적용/변경={changed}"
                    + $" / 체력={_health.CurrentHp} / {_health.MaxHp}", this );
        }

        /// <summary>
        /// 플레이어 위치에서 더미를 밀어내는 넉백 요청
        /// </summary>
        /// <param name="data">필수 넉백 설정</param>
        public void ApplyKnockback ( KnockbackData data )
        {
            if ( CanTestKnockback == false )
                return;

            EffectContext context = new EffectContext(
                _hero.gameObject, _hero.gameObject,
                ActorType.Player, Faction.Ally, AttackType.Skill, 0 );

            //발동 시점의 상대 위치로 방향 확정, 이후 Hero 이동을 추적하지 않음
            Vector3 direction = transform.position - _hero.position;
            bool applied = _knockback.TryApply(
                new KnockbackInfo( context, data, direction ) );

            if ( _showDebugLog == true )
                Debug.Log(
                    $"{name} 넉백 요청: 적용={applied}"
                    + $" / 요청 거리={data.Distance} / 요청 시간={data.Duration}"
                    + $" / 시작 위치={transform.position}", this );
        }

        /// <summary>
        /// 체력 변경 결과 확인
        /// </summary>
        /// <param name="currentHp">현재 체력</param>
        /// <param name="maxHp">최대 체력</param>
        void HandleHpChanged ( float currentHp, float maxHp )
        {
            if ( _showDebugLog == true )
                Debug.Log( $"{_health.gameObject.name} 체력 변경: {currentHp} / {maxHp}", this );
        }

        /// <summary>
        /// 피격 결과 확인
        /// </summary>
        /// <param name="damageInfo">피해 정보</param>
        /// <param name="damage">실제 피해량</param>
        void HandleDamaged ( DamageInfo damageInfo, float damage )
        {
            if ( _showDebugLog == true )
                Debug.Log(
                    $"피격: 공격자={damageInfo.Attacker}, " +
                    $"회차={damageInfo.AttackId}, 피해량={damage}",
                    this );
        }

        /// <summary>
        /// 사망 결과 확인
        /// </summary>
        /// <param name="damageInfo">피해 정보</param>
        void HandleDied ( DamageInfo damageInfo )
        {
            //검증 중에는 비활성화하거나 Collider를 변경하지 않음
            if ( _showDebugLog == true )
                Debug.Log(
                    $"사망: 공격자={damageInfo.Attacker}, 회차={damageInfo.AttackId}",
                    this );
        }

        /// <summary>
        /// 부활 결과 확인
        /// </summary>
        void HandleRevived ()
        {
            if ( _showDebugLog == true )
                Debug.Log( "부활", this );
        }

        private void OnDisable ()
        {
            _health.OnHpChanged -= HandleHpChanged;
            _health.OnRevived -= HandleRevived;
            _damageReceiver.OnDamaged -= HandleDamaged;
            _damageReceiver.OnDied -= HandleDied;
        }
    }
}
