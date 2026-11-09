using UnityEngine;

namespace LJTest
{
    /// <summary>
    /// 처치 보상 테스트 - 더미 사망과 생명 식별값 전달
    /// </summary>
    public class EnemyRewardTest : MonoBehaviour
    {
        [Header ( "----- 더미 -----" )]
        [SerializeField] Health _health;                   //더미 체력
        [SerializeField] DamageReceiver _damageReceiver;   //더미 사망 알림

        [Header ( "----- 처치 보상 -----" )]
        [SerializeField] int _killAmount = 20;             //테스트 처치 보상

        StageRewardController _rewards;                    //보상 연결부
        int _stageId;                                     //해당 생명의 원래 도전 번호
        long _lifeId;                                     //현재 생명 식별값
        bool _isBound;                                    //생명 연결 완료 여부

        private void OnEnable ( )
        {
            _damageReceiver.OnDied += HandleDied;
            _health.OnRevived += HandleRevived;
        }

        /// <summary>
        /// 살아 있는 더미를 새 도전에 연결
        /// </summary>
        /// <param name="test">더미 연결을 요청한 재화 테스트</param>
        /// <param name="rewards">보상 연결부</param>
        /// <param name="stageId">새 도전 번호</param>
        /// <returns>연결 성공 여부</returns>
        public bool BindStageForTest (
            CurrencyTest test, StageRewardController rewards, int stageId )
        {
            //기존 재화 테스트 호출은 유지하고 실제 연결에는 테스트 참조를 전달하지 않음
            return test != null && BindStageForTest ( rewards, stageId );
        }

        /// <summary>
        /// 재화 테스트 컴포넌트 없이 살아 있는 더미를 새 도전에 연결
        /// </summary>
        /// <param name="rewards">보상 연결부</param>
        /// <param name="stageId">새 도전 번호</param>
        /// <returns>연결 성공 여부</returns>
        public bool BindStageForTest ( StageRewardController rewards, int stageId )
        {
            if ( isActiveAndEnabled == false
                || rewards == null
                || _health.IsAlive == false
                || stageId <= 0
                || stageId != rewards.CurrentStageId )
                return false;

            if ( _isBound == true && _stageId == stageId )
                return false;

            if ( rewards.TryIssueLifeId ( stageId, out long lifeId ) == false )
                return false;

            _rewards = rewards;
            _stageId = stageId;
            _lifeId = lifeId;
            _isBound = true;
            return true;
        }

        /// <summary>
        /// 사망한 보상 더미를 최대 체력으로 부활
        /// </summary>
        [ContextMenu ( "보상 더미 부활" )]
        public void ReviveForTest ( )
        {
            if ( Application.isPlaying == false || isActiveAndEnabled == false )
                return;

            //새 생명 식별값은 실제 부활 알림에서 발급
            bool revived = _health.Revive ( _health.MaxHp );

            Debug.Log (
                $"{name} 보상 더미 부활: 성공={revived}"
                + $" / 체력={_health.CurrentHp} / {_health.MaxHp}"
                + $" / 도전 번호={_stageId}"
                + $" / 생명 식별값={_lifeId}", this );
        }

        /// <summary>
        /// 사망 확정 정보를 보상 연결부로 전달
        /// </summary>
        /// <param name="damageInfo">사망 피해 정보</param>
        void HandleDied ( DamageInfo damageInfo )
        {
            if ( _isBound == false
                || _rewards == null
                || IsTeamKill ( damageInfo ) == false )
                return;

            bool accepted = _rewards.RegisterKill (
                _stageId, _lifeId, _killAmount, Time.timeAsDouble );

            Debug.Log (
                $"{name} 처치 보상: 접수={accepted}"
                + $" / 도전 번호={_stageId}"
                + $" / 생명 식별값={_lifeId}"
                + $" / 콤보 처치 수={_rewards.ComboKillCount}"
                + $" / 다음 보상 기준={_rewards.NextRewardKillCount}"
                + $" / 남은 시간={_rewards.GetComboRemainingTime ( Time.timeAsDouble ):F2}"
                + $" / 스테이지 재화 잔액={_rewards.StageCurrencyBalance}"
                + $" / 미지급 요청={_rewards.PendingRewardCount}", this );
        }

        /// <summary>
        /// 부활한 더미의 새 생명 식별값 발급
        /// </summary>
        void HandleRevived ( )
        {
            if ( _isBound == false || _rewards == null )
                return;

            if ( _rewards.TryIssueLifeId ( _stageId, out long lifeId ) == false )
            {
                _isBound = false;
                return;
            }

            //부활해도 사망 알림에 사용할 원래 도전 번호는 유지
            _lifeId = lifeId;
        }

        /// <summary>
        /// 플레이어, 동료와 설치물의 팀 처치 여부 확인
        /// </summary>
        /// <param name="damageInfo">사망 피해 정보</param>
        /// <returns>팀 처치 여부</returns>
        bool IsTeamKill ( DamageInfo damageInfo )
        {
            return damageInfo.Faction == Faction.Ally
                && ( damageInfo.ActorType == ActorType.Player
                    || damageInfo.ActorType == ActorType.Companion
                    || damageInfo.ActorType == ActorType.Installation );
        }

        private void OnDisable ( )
        {
            _damageReceiver.OnDied -= HandleDied;
            _health.OnRevived -= HandleRevived;
        }
    }
}
