using UnityEngine;

namespace LJTest
{
    /// <summary>
    /// 스킬 타이밍 테스트 - 원래 회차의 타격과 연출 완료 신호 전달
    /// </summary>
    public class SkillTimingTest : MonoBehaviour
    {
        [Header ( "----- 디버그 -----" )]
        [SerializeField] bool _showDebugLog = true;     //일반 디버그 로그 출력 여부

        [Header ( "----- 컴포넌트 -----" )]
        [SerializeField] SkillCaster _skillCaster;      //검증할 스킬 모듈

        [Header ( "----- 검증 시간 -----" )]
        [SerializeField] float _hitTime = 0.3f;     //시작부터 타격까지
        [SerializeField] float _presentationEndTime = 0.8f;     //시작부터 전체 연출 완료까지

        int _skillId;       //이번 테스트의 원래 회차
        float _elapsedTime;     //이번 재생의 경과 시간
        float _currentHitTime;      //시작 시 보존한 타격 시간
        float _currentEndTime;      //시작 시 보존한 연출 완료 시간
        bool _isRunning;        //테스트 신호 진행 여부
        bool _hasSentHit;       //타격 신호 전달 여부


        private void OnEnable ( )
        {
            //유효한 값인지 검사
            if ( _hitTime >= 0f == false
                || float.IsInfinity ( _hitTime ) == true
                || _presentationEndTime >= _hitTime == false
                || float.IsInfinity ( _presentationEndTime ) == true )
            {
                Debug.LogError (
                    "타격 시간은 유한한 0 이상이며 연출 완료 시간은 타격 시간 이상이어야 합니다." , this );
                enabled = false;
                return;
            }

            _skillCaster.OnSkillStarted += HandleSkillStarted;
            _skillCaster.OnSkillEnded += HandleSkillEnded;
        }

        private void Update ( )
        {
            if ( _isRunning == false || Time.deltaTime <= 0f )
                return;

            int skillId = _skillId;
            bool signalsProcessed = false;

            try
            {
                _elapsedTime += Time.deltaTime;

                if ( _hasSentHit == false
                    && _elapsedTime >= _currentHitTime )
                {
                    //타격 이벤트 재진입 전에 전달 기록
                    _hasSentHit = true;
                    _skillCaster.TryCommitHit ( skillId );
                }

                //타격 중 취소된 재생은 완료 신호를 보내지 않음
                if ( _isRunning == true
                    && _skillId == skillId
                    && _elapsedTime >= _currentEndTime )
                {
                    _isRunning = false;
                    _skillCaster.CompleteSkill ( skillId );
                }

                signalsProcessed = true;
            }
            finally
            {
                //신호 처리 오류가 나면 원래 회차만 취소
                if ( signalsProcessed == false )
                {
                    if ( _skillId == skillId )
                        _isRunning = false;

                    CancelTestSkill ( skillId );
                }
            }
        }

        /// <summary>
        /// 원래 스킬 회차의 테스트 시작
        /// </summary>
        /// <param name="context">스킬 실행 정보</param>
        void HandleSkillStarted ( AttackContext context )
        {
            _skillId = context.AttackId;
            _elapsedTime = 0f;
            _currentHitTime = _hitTime;
            _currentEndTime = _presentationEndTime;
            _hasSentHit = false;
            _isRunning = true;

            if ( _showDebugLog == true )
                Debug.Log (
                    $"스킬 시작: 회차={_skillId}, " +
                    $"쿨타임={_skillCaster.RemainingCooldown}" , this );
        }

        /// <summary>
        /// 종료된 테스트 회차 정리
        /// </summary>
        /// <param name="skillId">종료된 회차</param>
        /// <param name="reason">종료 사유</param>
        void HandleSkillEnded ( int skillId , AttackEndReason reason )
        {
            if ( skillId != _skillId )
                return;

            _isRunning = false;

            if ( _showDebugLog == true )
                Debug.Log (
                    $"스킬 종료: 회차={skillId}, 사유={reason}, " +
                    $"쿨타임={_skillCaster.RemainingCooldown}" , this );
        }

        /// <summary>
        /// 아직 진행 중인 원래 테스트 회차 취소
        /// </summary>
        /// <param name="skillId">취소할 테스트 회차</param>
        void CancelTestSkill ( int skillId )
        {
            if ( _skillCaster.IsCasting == true
                && _skillCaster.CurrentSkillId == skillId )
                _skillCaster.CancelSkill ( AttackEndReason.Canceled );
        }

        private void OnDisable ( )
        {
            _skillCaster.OnSkillStarted -= HandleSkillStarted;
            _skillCaster.OnSkillEnded -= HandleSkillEnded;

            if ( _isRunning == false )
                return;

            int skillId = _skillId;
            _isRunning = false;

            CancelTestSkill ( skillId );
        }
    }
}
