using System.Collections;
using UnityEngine;

namespace LJTest
{
    /// <summary>
    /// 공격 타이밍 테스트 - 회차별 타격/종료 신호 전달
    /// </summary>
    public class AttackTimingTest : MonoBehaviour
    {
        [Header( "----- 컴포넌트 -----" )]
        [SerializeField] Attacker _attacker;    //검증할 공격 모듈

        [Header( "----- 검증 시간 -----" )]
        [SerializeField] float _hitTime;    //공격 시작부터 타격 신호까지의 시간
        [SerializeField] float _endTime;    //공격 시작부터 종료 신호까지의 시간

        Coroutine _attackRoutine;    //현재 회차의 검증 신호 전달 작업

        private void OnEnable ()
        {
            //유효하지 않은 시간으로 종료 신호를 누락하지 않도록 검사
            if ( _hitTime >= 0f == false ||
                float.IsInfinity( _hitTime ) == true ||
                _endTime >= _hitTime == false ||
                float.IsInfinity( _endTime ) == true )
            {
                Debug.LogError(
                    "검증 시간은 유한한 0 이상의 값이며 종료 시간이 타격 시간 이상이어야 합니다.",
                    this );

                enabled = false;
                return;
            }

            _attacker.OnAttackStarted += HandleAttackStarted;
            _attacker.OnAttackEnded += HandleAttackEnded;
        }

        /// <summary>
        /// 공격 시작 신호 수신
        /// </summary>
        /// <param name="context">공격 실행 정보</param>
        void HandleAttackStarted ( AttackContext context )
        {
            //이번 재생에 해당하는 회차와 시간을 전달
            _attackRoutine = StartCoroutine(
                SendAttackSignals(
                    context.AttackId, _hitTime, _endTime ) );
        }

        /// <summary>
        /// 원래 공격 회차의 타격/종료 신호 전달
        /// </summary>
        /// <param name="attackId">공격 회차</param>
        /// <param name="hitTime">타격 신호까지의 시간</param>
        /// <param name="endTime">종료 신호까지의 시간</param>
        /// <returns>신호 전달 대기 작업</returns>
        IEnumerator SendAttackSignals (
            int attackId, float hitTime, float endTime )
        {
            yield return new WaitForSeconds( hitTime );

            //최신 회차를 조회하지 않고 시작 시 받은 원래 회차 사용
            _attacker.TryCommitHit( attackId );

            yield return new WaitForSeconds( endTime - hitTime );

            _attacker.CompleteAttack( attackId );
        }

        /// <summary>
        /// 공격 종료 신호 수신
        /// </summary>
        /// <param name="attackId">공격 회차</param>
        /// <param name="reason">종료 사유</param>
        void HandleAttackEnded ( int attackId, AttackEndReason reason )
        {
            //취소/사망/정상 종료 후 남은 검증 신호 전달 중단
            StopAttackRoutine( );
        }

        /// <summary>
        /// 남은 검증 신호 전달 중단
        /// </summary>
        void StopAttackRoutine ()
        {
            //공격 전이나 이미 정리된 경우에는 작업이 없음
            if ( _attackRoutine == null )
                return;

            StopCoroutine( _attackRoutine );
            _attackRoutine = null;
        }

        private void OnDisable ()
        {
            _attacker.OnAttackStarted -= HandleAttackStarted;
            _attacker.OnAttackEnded -= HandleAttackEnded;

            if ( _attackRoutine == null )
                return;

            //검증 연결을 끌 때 종료 신호를 기다리는 공격도 취소
            StopAttackRoutine( );
            _attacker.CancelAttack( AttackEndReason.Canceled );
        }
    }
}
