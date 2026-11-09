using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LJTest
{
    /// <summary>
    /// 동료 테스트 - 검사 초기화와 테스트 타겟 연결
    /// </summary>
    public class CompanionTest : MonoBehaviour
    {
        [Header ( "----- 디버그 -----" )]
        [SerializeField] bool _showDebugLog = true;		//일반 디버그 로그 출력 여부

        [Header ( "----- 동료 -----" )]
        [SerializeField] Companion _companion;		//검증 대상
        [SerializeField] CompanionData _data;		//검증용 동료 설정
        [SerializeField] Transform _hero;		//추종형에서 사용할 플레이어
        [SerializeField] Transform _placementPoint;		//지정하면 배치형, 비워두면 추종형

        [Header ( "----- 테스트 적 -----" )]
        [SerializeField] CombatTarget [ ] _targets;		//기존 피해 테스트 대상

        [Header ( "----- 피격 테스트 -----" )]
        [SerializeField] float _testDamage = 30f;		//H키 입력 시 방어 적용 전 피해량

        DamageReceiver _damageReceiver;		//검증할 동료의 피해 수신 모듈
        Health _health;		//피격 결과로 조회할 동료 체력
        StatusEffectController _statusEffects;		//효과 요청과 조회 모듈

        /// <summary>
        /// 테스트 준비와 생존 조건 충족 여부
        /// </summary>
        bool CanTest =>
            isActiveAndEnabled
            && _companion.IsInitialized
            && _companion.isActiveAndEnabled
            && _health.IsAlive;

        /// <summary>
        /// 무작위 넉백 테스트의 활성, 초기화와 생존 조건 충족 여부
        /// </summary>
        public bool CanTestKnockback => CanTest;

        private void Awake ( )
        {
            _damageReceiver = _companion.GetComponent<DamageReceiver> ( );
            _health = _companion.GetComponent<Health> ( );
            _statusEffects = _companion.GetComponent<StatusEffectController> ( );
        }

        private void OnEnable ( )
        {
            _damageReceiver.OnDamaged += HandleDamaged;
        }

        private void Start ( )
        {
            //초기화 도중 실패해도 테스트 실행 여부와 배치점 연결을 확인할 수 있도록 먼저 출력
            if ( _showDebugLog == true )
                Debug.Log (
                    $"[동료 테스트:{name}] 시작"
                    + $" / 동료:{( _companion != null ? _companion.name : "미할당" )}"
                    + $" / 데이터:{( _data != null ? _data.name : "미할당" )}"
                    + $" / 플레이어:{( _hero != null ? _hero.name : "미할당" )}"
                    + $" / 배치점:{( _placementPoint != null ? _placementPoint.name : "미배치(추종)" )}" ,
                    this );

            //적의 체력과 진영은 적 초기화 주체가 설정하며,
            //동료 테스트는 자신의 배치 또는 추종 초기화만 담당
            Vector3? placementPoint = _placementPoint != null
                ? _placementPoint.position : ( Vector3? ) null;

            GameManager gameManager = GameManager.Instance;
            _companion.SetSound( gameManager.Sound );
            _companion.SetVfx( gameManager.Vfx );

            if ( _companion.PrepareHire (
                _data , _hero , placementPoint ) == false )
            {
                Debug.LogError ( $"[동료 테스트:{name}] 초기화 실패 / 앞선 초기화 오류 확인" , this );
                enabled = false;
                return;
            }

            //씬에 미리 배치한 기존 검사 테스트는 비용 처리 없이 바로 확정
            _companion.ConfirmHire ( );
        }

        private void Update ( )
        {
            //공개 요청 함수에서 테스트 준비와 생존을 확인
            //기절은 테스트 피해 입력의 차단 조건이 아님
            if ( Keyboard.current != null
                && Keyboard.current.hKey.wasPressedThisFrame == true )
            {
                HitCompanion ( _testDamage );
            }
        }

        /// <summary>
        /// 테스트 적을 피해 발생원으로 사용하여 동료 피격
        /// </summary>
        /// <param name="damage">방어 적용 전 피해량</param>
        public void HitCompanion ( float damage )
        {
            if ( CanTest == false )
                return;

            CombatTarget attacker = FindTestAttacker ( );

            if ( attacker == null )
                return;

            //테스트 컴포넌트가 동료에 붙어 있어도 자기 피해로 판정되지 않도록 적을 전달
            //수동 피해 주입이므로 적의 거리나 생존 상태는 검사하지 않음
            DamageInfo damageInfo = new DamageInfo (
                attacker.gameObject , attacker.gameObject ,
                ActorType.Enemy , attacker.Faction ,
                damage , AttackType.BasicAttack , 0 );

            float appliedDamage = _damageReceiver.TakeDamage ( damageInfo );

            //유효 피격은 OnDamaged에서 출력하고 피해가 적용되지 않은 요청만 여기서 알림
            if ( appliedDamage <= 0f )
            {
                if ( _showDebugLog == true )
                    Debug.Log (
                        $"[동료 테스트:{name}] 피해 적용 없음 / 요청 피해:{damage} / 생존:{_health.IsAlive}" ,
                        this );
            }
        }

        /// <summary>
        /// 수동 테스트 요청에 사용할 적 조회
        /// </summary>
        /// <returns>적 발생원 또는 null</returns>
        CombatTarget FindTestAttacker ( )
        {
            foreach ( CombatTarget target in _targets )
            {
                if ( target == null
                    || target.Faction != Faction.Enemy
                    || target.gameObject == _companion.gameObject )
                    continue;

                return target;
            }

            Debug.LogWarning (
                $"[동료 테스트:{name}] 요청 실패 / 발생원으로 사용할 테스트 적 없음" ,
                this );

            return null;
        }

        /// <summary>
        /// 테스트용 동료 상태이상 요청
        /// </summary>
        /// <param name="data">필수 상태이상 설정</param>
        public void ApplyCompanionStatus ( StatusEffectData data )
        {
            if ( CanTest == false )
                return;

            CombatTarget attacker = FindTestAttacker ( );

            if ( attacker == null )
                return;

            EffectContext context = new EffectContext (
                attacker.gameObject , attacker.gameObject ,
                ActorType.Enemy , attacker.Faction ,
                AttackType.Skill , 0 );

            if ( data is SlowEffectData slowData )
            {
                SlowEffectInfo effectInfo =
                    new SlowEffectInfo ( context , slowData );

                bool changed = _statusEffects.TryApply ( effectInfo );

                if ( _showDebugLog == true )
                {
                    _statusEffects.TryGetSlowState (
                        slowData , out float remainingTime , out float slowRate );

                    Debug.Log (
                        $"[동료 감속:{_companion.name}] 적용/변경={changed}"
                        + $" / 해당 기록 시간={remainingTime}"
                        + $" / 감소율={slowRate}"
                        + $" / 감속 기록={_statusEffects.SlowCount}"
                        + $" / 최종 이동 배율={_statusEffects.MoveSpeedMultiplier}" ,
                        _companion );
                }

                return;
            }

            if ( data is StunEffectData stunData )
            {
                StatusEffectInfo effectInfo =
                    new StatusEffectInfo ( context , stunData );

                bool changed = _statusEffects.TryApply ( effectInfo );

                if ( _showDebugLog == true )
                {
                    Debug.Log (
                        $"[동료 기절:{_companion.name}] 적용/변경={changed}"
                        + $" / 남은 시간={_statusEffects.RemainingStunTime}"
                        + $" / 행동 가능={_companion.CanAct}" ,
                        _companion );
                }

                return;
            }

            if ( data is BurnEffectData burnData )
            {
                BurnEffectInfo effectInfo =
                    new BurnEffectInfo ( context , burnData );

                bool changed = _statusEffects.TryApply ( effectInfo );

                if ( _showDebugLog == true )
                {
                    _statusEffects.TryGetBurnState (
                        out float remainingTime ,
                        out float remainingTickTime ,
                        out float rawDamage ,
                        out float tickInterval );

                    Debug.Log (
                        $"[동료 화상:{_companion.name}] 적용/변경={changed}"
                        + $" / 남은 시간={remainingTime}"
                        + $" / 다음 피해까지={remainingTickTime}"
                        + $" / 주기당 피해={rawDamage}"
                        + $" / 피해 간격={tickInterval}" ,
                        _companion );
                }

                return;
            }

            if ( _showDebugLog == true )
            {
                Debug.Log (
                    $"[동료 효과:{_companion.name}] 적용/변경=False"
                    + $" / 지원하지 않는 설정={data}" ,
                    _companion );
            }
        }

        /// <summary>
        /// 테스트 적 위치에서 동료를 밀어내는 넉백 요청
        /// </summary>
        /// <param name="data">필수 넉백 설정</param>
        public void ApplyCompanionKnockback ( KnockbackData data )
        {
            if ( CanTest == false )
                return;

            CombatTarget attacker = FindTestAttacker ( );

            if ( attacker == null )
                return;

            //타겟 탐색/적 AI를 변경하지 않고 선택한 테스트 적의 위치만 방향에 사용
            EffectContext context = new EffectContext (
                attacker.gameObject , attacker.gameObject ,
                ActorType.Enemy , attacker.Faction ,
                AttackType.Skill , 0 );

            Vector3 direction = _companion.transform.position - attacker.Position;
            KnockbackController knockback =
                _companion.GetComponent<KnockbackController> ( );

            bool applied = knockback.TryApply (
                new KnockbackInfo ( context , data , direction ) );

            if ( _showDebugLog == true )
                Debug.Log (
                    $"[동료 넉백:{_companion.name}] 적용={applied}"
                    + $" / 요청 거리={data.Distance} / 요청 시간={data.Duration}"
                    + $" / 남은 시간={knockback.RemainingTime}"
                    + $" / 행동 가능={_companion.CanAct}" , _companion );
        }

        /// <summary>
        /// 실제 피격 피해량과 남은 체력 출력
        /// </summary>
        /// <param name="damageInfo">피격 원인 정보</param>
        /// <param name="damage">실제 감소한 체력</param>
        void HandleDamaged ( DamageInfo damageInfo , float damage )
        {
            if ( _showDebugLog == true )
                Debug.Log (
                    $"[동료 피격:{_companion.name}] " +
                    $"공격자:{damageInfo.Attacker.name} / " +
                    $"실제 피해:{damage} / " +
                    $"HP:{_health.CurrentHp} / {_health.MaxHp}" ,
                    _companion );
        }

        /// <summary>
        /// 배치형의 현재 위치에서 배치점까지 복귀 경로 진단
        /// </summary>
        public void LogReturnPath ( )
        {
            if ( _companion.Mode != CompanionMode.Defend )
                return;

            NavMeshAgent agent =
                _companion.GetComponent<NavMeshAgent> ( );

            Vector3 currentPosition = _companion.transform.position;
            Vector3 placementPoint = _companion.ActivityCenter;

            if ( agent.isOnNavMesh == false )
            {
                Debug.LogWarning (
                    $"[동료 테스트:{_companion.name}] 복귀 경로 진단 " +
                    $"/ 현재 위치:{currentPosition} / 배치점:{placementPoint} " +
                    "/ 계산 불가: Agent가 NavMesh에 연결되지 않음" ,
                    _companion );
                return;
            }

            //임시 진단용 계산만 수행하며 실제 Agent의 경로는 변경하지 않음
            NavMeshPath path = new NavMeshPath ( );
            bool calculated = agent.CalculatePath ( placementPoint , path );
            Vector3 [ ] corners = path.corners;

            string endPositionText = "없음";
            string errorText = "계산 불가";

            if ( calculated == true && corners.Length > 0 )
            {
                Vector3 endPosition = corners [ corners.Length - 1 ];
                Vector3 offset = endPosition - placementPoint;
                offset.y = 0f;

                endPositionText = endPosition.ToString ( );
                errorText = offset.magnitude.ToString ( "F2" );
            }

            if ( _showDebugLog == true )
                Debug.Log (
                    $"[동료 테스트:{_companion.name}] 복귀 경로 진단 " +
                    $"/ 현재 위치:{currentPosition} / 배치점:{placementPoint} " +
                    $"/ 계산 성공:{calculated} / 경로 상태:{path.status} " +
                    $"/ 끝점:{endPositionText} / 수평 오차:{errorText}" ,
                    _companion );
        }

        private void OnDisable ( )
        {
            _damageReceiver.OnDamaged -= HandleDamaged;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos ( )
        {
            if ( _companion == null || _data == null )
                return;

            CompanionMode mode = Application.isPlaying == true
                ? _companion.Mode
                : ( _placementPoint != null ? CompanionMode.Defend : CompanionMode.Follow );

            if ( Application.isPlaying == false
                && _placementPoint == null && _hero == null )
                return;

            Vector3 activityCenter = Application.isPlaying == true
                ? _companion.ActivityCenter
                : ( _placementPoint != null ? _placementPoint.position : _hero.position );

            Color previousColor = Handles.color;
            Handles.color = Color.cyan;
            Handles.DrawWireDisc ( activityCenter , Vector3.up , _data.ActivityRadius );
            Handles.Label ( activityCenter + Vector3.right * _data.ActivityRadius ,
                $"활동 반경 {_data.ActivityRadius}" );

            CombatTarget target = _companion.CurrentTarget;
            Handles.color = Color.white;
            Handles.Label ( _companion.transform.position + Vector3.up * 1.5f ,
                $"모드: {( mode == CompanionMode.Follow ? "추종형" : "배치형" )}"
                + $"\n현재 타겟: {( target != null ? target.name : "없음" )}" );
            Handles.color = previousColor;
        }
#endif
    }
}
