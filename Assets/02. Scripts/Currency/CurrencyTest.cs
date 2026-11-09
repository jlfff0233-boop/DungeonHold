using System;
using UnityEngine;

namespace LJTest
{
    /// <summary>
    /// 재화 테스트 - 초기화, 지급, 소비와 도전 수명 확인
    /// </summary>
    public class CurrencyTest : MonoBehaviour
    {
        [Header ( "----- 재화 -----" )]
        [SerializeField] Currency _currency;        //검증할 재화 모듈

        [Header ( "----- 도전 시작 -----" )]
        [SerializeField] int _initialStageCurrency = 500;    //시작 스테이지 재화

        [Header ( "----- 지급/소비 -----" )]
        [SerializeField] CurrencyType _testCurrencyType = CurrencyType.Stage;    //요청할 재화 종류
        [SerializeField] int _testAmount = 150;    //지급 또는 소비할 금액

        [Header ( "----- 콤보/처치 보상 -----" )]
        [SerializeField] StageRewardController _rewards;    //추가 검증할 보상 연결부, 미연결 시 기존 재화 테스트만 실행
        [SerializeField] ComboData _comboData;              //콤보 유지시간과 보상 자료 맵
        [SerializeField] EnemyRewardTest[] _rewardDummies = new EnemyRewardTest[0];    //보상을 연결할 더미 목록

        [Header ( "----- 씬 진행 -----" )]
        [SerializeField] TestScene _scene;    //실제 초기화와 도전 수명 요청 대상

        int _previousStageId;       //마지막으로 종료한 도전 번호
        bool _hasPreviousStage;     //이전 도전 번호 보관 여부

        /// <summary>
        /// Play 중 테스트 함수 실행 가능 여부
        /// </summary>
        bool CanTest =>
            Application.isPlaying && isActiveAndEnabled;

        /// <summary>
        /// 테스트에서 준비한 실제 재화 모듈
        /// </summary>
        public Currency Currency => _currency;

        /// <summary>
        /// 테스트 요청으로 실제 씬 초기화
        /// </summary>
        public void Init ( )
        {
            InitializeForTest ( );
        }

        private void OnEnable ( )
        {
            //기존 재화 테스트에 씬 참조를 연결하기 전에는 결과 구독 생략
            if ( _scene != null )
                _scene.OnStageEndProcessed += HandleStageEndProcessed;
        }

        /// <summary>
        /// 콤보 설정과 보상 연결부 초기화
        /// </summary>
        public void InitializeRewardsForTest ( )
        {
            if ( CanTest == false || _rewards == null )
                return;

            bool initialized = _rewards.Init ( _currency, _comboData );

            Debug.Log (
                $"콤보/처치 보상 초기화: 성공={initialized}", this );
        }

        /// <summary>
        /// 실제 저장 슬롯을 사용하여 씬 초기화
        /// </summary>
        public void InitializeForTest ( )
        {
            if ( CanTest == false )
                return;

            bool initialized = _scene.Init ( );

            Debug.Log (
                $"재화 초기화: 성공={initialized}"
                + $" / 영구 재화 잔액="
                + $"{_currency.GetBalance ( CurrencyType.Permanent )}"
                + $" / 스테이지 재화 잔액="
                + $"{_currency.GetBalance ( CurrencyType.Stage )}" ,
                this );
        }

        /// <summary>
        /// 새 도전 시작과 시작 스테이지 재화 확인
        /// </summary>
        public void BeginStageForTest ( )
        {
            if ( CanTest == false )
                return;

            int previousStageId = _currency.CurrentStageId;

            //실제 씬이 플레이어, 재화와 고용을 같은 순서로 시작
            bool started = _scene.BeginStage ( _initialStageCurrency );

            if ( started == true )
                BindRewardDummiesForTest ( );

            Debug.Log (
                $"도전 시작: 성공={started}"
                + $" / 이전 도전 번호={previousStageId}"
                + $" / 현재 도전 번호={_currency.CurrentStageId}"
                + $" / 진행 중={_currency.IsStageActive}"
                + $" / 영구 재화 잔액={_currency.GetBalance ( CurrencyType.Permanent )}"
                + $" / 스테이지 재화 잔액={_currency.GetBalance ( CurrencyType.Stage )}", this );
        }

        /// <summary>
        /// 고용/배치 준비를 끝내고 동료 전투 시작 요청
        /// </summary>
        public void BeginCombatForTest ( )
        {
            if ( CanTest == false )
                return;

            //도전 시작과 분리하여 고용 및 Z/X 배치 이후에 호출
            bool started = _scene.BeginCombat (
                out CompanionHireResult result );
            Debug.Log ( $"동료 전투 시작: 성공={started} / 결과={result}" , this );
        }

        /// <summary>
        /// Inspector에서 지정한 재화와 금액 지급
        /// </summary>
        public void AddCurrencyForTest ( )
        {
            if ( CanTest == false )
                return;

            int previousCurrencyBalance =
                _currency.GetBalance ( _testCurrencyType );

            bool added = _currency.TryAdd ( _testCurrencyType , _testAmount );

            Debug.Log (
                $"재화 지급: 종류={_testCurrencyType}"
                + $" / 요청 금액={_testAmount}"
                + $" / 성공={added}"
                + $" / 변경 전 재화 잔액={previousCurrencyBalance}"
                + $" / 변경 후 재화 잔액="
                + $"{_currency.GetBalance ( _testCurrencyType )}" ,
                this );
        }

        /// <summary>
        /// Inspector에서 지정한 재화와 금액 소비
        /// </summary>
        public void SpendCurrencyForTest ( )
        {
            if ( CanTest == false )
                return;

            int previousCurrencyBalance =
                _currency.GetBalance ( _testCurrencyType );

            bool canSpend = _currency.CanSpend ( _testCurrencyType , _testAmount );

            bool spent = _currency.TrySpend ( _testCurrencyType , _testAmount );

            Debug.Log (
                $"재화 소비: 종류={_testCurrencyType}"
                + $" / 요청 금액={_testAmount}"
                + $" / 소비 가능={canSpend}"
                + $" / 성공={spent}"
                + $" / 변경 전 재화 잔액={previousCurrencyBalance}"
                + $" / 변경 후 재화 잔액="
                + $"{_currency.GetBalance ( _testCurrencyType )}" ,
                this );
        }

        /// <summary>
        /// 현재 도전 종료와 이전 도전 번호 보관
        /// </summary>
        public void EndStageForTest ( )
        {
            if ( CanTest == false )
                return;

            int stageId = _currency.CurrentStageId;
            bool accepted = _scene.RequestEndStage ( stageId, false );

            //접수는 실제 종료 완료와 구분하고 결과 이벤트에서 완료 상태 확인
            Debug.Log (
                $"도전 종료 예약: 접수={accepted} / 요청 도전 번호={stageId}", this );
        }

        /// <summary>
        /// 테스트 더미의 새 생명 식별값 발급
        /// </summary>
        /// <param name="lifeId">발급한 생명 식별값</param>
        /// <returns>발급 성공 여부</returns>
        public bool TryIssueLifeIdForTest ( out long lifeId )
        {
            lifeId = 0;

            if ( CanTest == false || _rewards == null )
                return false;

            return _rewards.TryIssueLifeId (
                _rewards.CurrentStageId, out lifeId );
        }

        /// <summary>
        /// 현재 도전의 살아 있는 보상 더미 연결
        /// </summary>
        public void BindRewardDummiesForTest ( )
        {
            if ( CanTest == false
                || _rewards == null
                || _currency.IsStageActive == false
                || _rewards.CurrentStageId != _currency.CurrentStageId )
                return;

            for ( int i = 0; i < _rewardDummies.Length; i++ )
            {
                EnemyRewardTest dummy = _rewardDummies[i];

                if ( dummy == null )
                    continue;

                bool bound = dummy.BindStageForTest (
                    this, _rewards, _currency.CurrentStageId );

                Debug.Log (
                    $"{dummy.name} 보상 연결: 성공={bound}"
                    + $" / 도전 번호={_currency.CurrentStageId}", dummy );
            }
        }

        /// <summary>
        /// 처치 집계를 변경하지 않고 미지급 보상 재시도
        /// </summary>
        public void RetryRewardsForTest ( )
        {
            if ( CanTest == false || _rewards == null )
                return;

            bool completed = _rewards.RetryPendingRewards ( );

            Debug.Log (
                $"미지급 보상 재시도: 모두 처리={completed}"
                + $" / 미지급 요청={_rewards.PendingRewardCount}"
                + $" / 콤보 처치 수={_rewards.ComboKillCount}"
                + $" / 스테이지 재화 잔액={_rewards.StageCurrencyBalance}", this );
        }

        /// <summary>
        /// 남은 스테이지 보상을 폐기하고 도전 종료
        /// </summary>
        public void AbandonStageForTest ( )
        {
            if ( CanTest == false )
                return;

            int stageId = _currency.CurrentStageId;
            bool accepted = _scene.RequestEndStage ( stageId, true );

            Debug.Log (
                $"도전 포기 예약: 접수={accepted} / 요청 도전 번호={stageId}", this );
        }

        /// <summary>
        /// 현재 콤보와 미지급 상태 조회
        /// </summary>
        public void CheckComboForTest ( )
        {
            if ( CanTest == false || _rewards == null )
                return;

            _rewards.UpdateCombo ( Time.timeAsDouble );

            Debug.Log (
                $"콤보 상태: 처치 수={_rewards.ComboKillCount}"
                + $" / 다음 보상 기준={_rewards.NextRewardKillCount}"
                + $" / 남은 시간={_rewards.GetComboRemainingTime ( Time.timeAsDouble ):F2}"
                + $" / 미지급 요청={_rewards.PendingRewardCount}"
                + $" / 스테이지 재화 잔액={_rewards.StageCurrencyBalance}", this );
        }

        /// <summary>
        /// 이전 도전에서 늦게 도착한 보상 요청
        /// </summary>
        public void AddPreviousStageRewardForTest ( )
        {
            if ( CanTest == false )
                return;

            if ( _hasPreviousStage == false )
            {
                Debug.LogWarning (
                    "도전 종료를 먼저 실행해 이전 도전 번호를 보관하세요." , this );
                return;
            }

            int previousStageCurrencyBalance =
                _currency.GetBalance ( CurrencyType.Stage );

            bool added = _currency.TryAddStageReward ( _previousStageId , _testAmount );

            Debug.Log (
                $"이전 도전 보상: 요청 도전 번호={_previousStageId}"
                + $" / 현재 도전 번호={_currency.CurrentStageId}"
                + $" / 요청 금액={_testAmount}"
                + $" / 성공={added}"
                + $" / 변경 전 스테이지 재화 잔액="
                + $"{previousStageCurrencyBalance}"
                + $" / 변경 후 스테이지 재화 잔액="
                + $"{_currency.GetBalance ( CurrencyType.Stage )}" ,
                this );
        }

        /// <summary>
        /// 이전 도전에서 늦게 도착한 종료 요청
        /// </summary>
        public void EndPreviousStageForTest ( )
        {
            if ( CanTest == false )
                return;

            if ( _hasPreviousStage == false )
            {
                Debug.LogWarning (
                    "도전 종료를 먼저 실행해 이전 도전 번호를 보관하세요." , this );
                return;
            }

            bool ended = _currency.EndStage ( _previousStageId );

            Debug.Log (
                $"이전 도전 종료: 요청 도전 번호={_previousStageId}"
                + $" / 현재 도전 번호={_currency.CurrentStageId}"
                + $" / 성공={ended}"
                + $" / 진행 중={_currency.IsStageActive}"
                + $" / 스테이지 재화 잔액="
                + $"{_currency.GetBalance ( CurrencyType.Stage )}" ,
                this );
        }

        /// <summary>
        /// 소비 후 실행 실패를 가정한 전액 복구 확인
        /// </summary>
        public void RestoreSpentCurrencyForTest ( )
        {
            if ( CanTest == false )
                return;

            int previousCurrencyBalance = _currency.GetBalance ( _testCurrencyType );

            bool spent = _currency.TrySpend ( _testCurrencyType , _testAmount );

            //실제로 차감했을 때만 같은 금액 복구
            bool restored = false;

            if ( spent == true )
            {
                restored = _currency.TryAdd ( _testCurrencyType , _testAmount );
            }

            int currentCurrencyBalance = _currency.GetBalance ( _testCurrencyType );

            Debug.Log (
                $"소비 후 전액 복구: 종류={_testCurrencyType}"
                + $" / 요청 금액={_testAmount}"
                + $" / 소비 성공={spent}"
                + $" / 복구 성공={restored}"
                + $" / 변경 전 재화 잔액={previousCurrencyBalance}"
                + $" / 변경 후 재화 잔액={currentCurrencyBalance}"
                + $" / 원래 재화 잔액과 일치="
                + $"{currentCurrencyBalance == previousCurrencyBalance}" ,
                this );
        }

        /// <summary>
        /// 0원 지급과 소비의 반환값, 변경 알림 없음 확인
        /// </summary>
        public void CheckZeroAmountForTest ( )
        {
            if ( CanTest == false )
                return;

            int eventCount = 0;
            int previousCurrencyBalance = _currency.GetBalance ( _testCurrencyType );

            //로그용 상시 구독이 아니라 이벤트 발생 횟수 검증
            Action<CurrencyType , int> countChanged =
                ( type , currencyBalance ) => eventCount++;

            bool added;
            bool spent;

            _currency.OnBalanceChanged += countChanged;

            try
            {
                added = _currency.TryAdd ( _testCurrencyType , 0 );
                spent = _currency.TrySpend ( _testCurrencyType , 0 );
            }
            finally
            {
                _currency.OnBalanceChanged -= countChanged;
            }

            Debug.Log (
                $"0원 요청: 종류={_testCurrencyType}"
                + $" / 지급 성공={added}"
                + $" / 소비 성공={spent}"
                + $" / 변경 이벤트 횟수={eventCount}"
                + $" / 변경 전 재화 잔액={previousCurrencyBalance}"
                + $" / 변경 후 재화 잔액="
                + $"{_currency.GetBalance ( _testCurrencyType )}" ,
                this );
        }
        /// <summary>
        /// 실제 종료 결과 확인과 이전 도전 번호 보관
        /// </summary>
        /// <param name="stageId">종료를 요청한 원래 도전 번호</param>
        /// <param name="abandon">보상 폐기 요청 여부</param>
        /// <param name="ended">실제 종료 성공 여부</param>
        void HandleStageEndProcessed ( int stageId, bool abandon, bool ended )
        {
            //정산 실패를 종료된 이전 도전으로 기록하지 않음
            if ( ended == true )
            {
                _previousStageId = stageId;
                _hasPreviousStage = true;
            }

            Debug.Log (
                $"도전 종료 결과: 도전={stageId} / 포기={abandon} / 성공={ended}"
                + $" / 진행 중={_currency.IsStageActive}"
                + $" / 스테이지 재화 잔액={_currency.GetBalance ( CurrencyType.Stage )}"
                + $" / 미지급 요청={_rewards.PendingRewardCount}", this );
        }

        private void OnDisable ( )
        {
            if ( _scene != null )
                _scene.OnStageEndProcessed -= HandleStageEndProcessed;
        }
    }
}
