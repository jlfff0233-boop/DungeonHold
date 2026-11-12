using System;
using System.Collections.Generic;
using UnityEngine;

namespace LJPlay
{
    /// <summary>
    /// 플레이 씬 연결부 - 초기화/입력/도전 요청 연결
    /// </summary>
    public class PlayScene : MonoBehaviour
    {
        [Header ( "----- 컴포넌트 -----" )]
        [SerializeField] Hero _hero;		//히어로
        [SerializeField] InputHandler _inputHandler;		//인풋 핸들러
        [SerializeField] CameraFocus _cameraFocus;		//카메라 포커스
        [SerializeField] Camera _playCamera;		//공용 플레이 카메라

        [Header ( "----- 재화 -----" )]
        [SerializeField] Currency _currency;		//실제 재화 모듈
        [SerializeField] StageRewardController _rewards;		//처치 보상과 콤보
        [SerializeField] ComboData _comboData;		//콤보 설정
        [SerializeField] ClearRewardData _clearRewardData;    //종료 보상 설정

        [Header ( "----- 동료 -----" )]
        [SerializeField] CompanionHireController _hireController;		//동료 고용 관리자
        [SerializeField] CompanionPlacementController _placementController;		//배치 상호작용
        [SerializeField] CompanionHireDataMap _companionHireDataMap;		//동료 타입별 고용 설정

        [Header ( "----- 무기 설정 -----" )]
        [SerializeField] WeaponDataMap _weaponDataMap;		//무기 타입별 설정
        [SerializeField] WeaponType _selectedWeaponType;		//편집 중 무기 범위 표시

        [Header ( "----- 아이템 -----" )]
        [SerializeField] ItemDataMap _itemDataMap;		//사용 가능한 아이템 설정

        [Header ( "----- 공용 슬롯 -----" )]
        [SerializeField] QuickSlotData [ ] _quickSlots = new QuickSlotData [ 10 ];		//번호 키 순서의 슬롯 설정

        [Header ( "----- 설치물 -----" )]
        [SerializeField] BuildPreviewController _buildPreviewController;		//설치물 선택과 해제

        [Header ( "----- 상점 -----" )]
        [SerializeField] ShopData _shopData;		//전체 상품 설정

        [Header ( "----- 강화 -----" )]
        [SerializeField] UpgradeDataMap _upgradeDataMap;		//전체 강화 설정

        [Header ( "----- 미션 -----" )]
        [SerializeField] MissionDataMap _missionDataMap;		//미션과 선행 조건 설정
        [SerializeField] string _missionMapId = "stage_1";		//현재 도전의 고정 맵 ID
        [SerializeField] MissionDifficulty _missionDifficulty = MissionDifficulty.Normal;		//현재 난이도
        [SerializeField] MissionDefenseRule _missionDefenseRule = MissionDefenseRule.NoPass;		//현재 맵의 완전 방어 규칙

        [Header ( "----- 도감 -----" )]
        [SerializeField] EnemyCollectionDataMap _collectionDataMap;		//원본 적별 도감 설정

        PlaySession _session;		//현재 초기화에 연결된 도전 수명
        VfxManager _vfx;		//현재 연결한 공용 VFX 서비스
        int _previousStageUsedItemCount;		//세션 재연결 중 보존할 마지막 정산 사용 횟수

        SaveManager _saveManager;		//현재 진행을 제공한 저장 관리자
        SaveService _saveService;		//현재 슬롯의 저장 서비스
        ItemInventory _inventory = new ItemInventory ( );		//현재 슬롯의 아이템 상태
        HeroItemUse _itemUse;		//아이템 사용 순서 조율

        ShopPurchase _shopPurchase;		//현재 슬롯의 상품 구매 처리
        EquipmentUnlock _equipmentUnlock;		//현재 슬롯의 해금/선택
        UpgradeProgress _upgradeProgress;		//현재 슬롯의 구매 단계
        UpgradePurchase _upgradePurchase;		//현재 슬롯의 강화 구매 처리

        QuickSlotContext _quickSlotContext;		//현재 초기화의 슬롯 실행 연결

        MissionController _missions;		//현재 슬롯의 미션 처리
        CollectionProgress _collectionProgress;		//현재 슬롯의 도감 조회 상태
        CollectionUnlock _collectionUnlock;		//도감 해금과 신규 확인 저장

        Vector2 _moveInput;		//현재 이동 입력
        CursorLockMode _previousCursorLock;		//이전 커서 잠금 상태
        bool _prevCursorVisible;		//이전 커서 표시 여부

        bool _isInitialized;		//모듈 연결 완료 여부
        bool _isPreparing;		//진행 준비/저장 처리 또는 사용권 일시 반환 중
        string _initError;		//초기화 오류

        /// <summary>
        /// 초기화 오류
        /// </summary>
        public string InitError => _initError;

        /// <summary>
        /// 도전 대기 여부
        /// </summary>
        public bool IsStageIdle => _isInitialized
            && _isPreparing == false && _session.IsStageIdle( );

        /// <summary>
        /// 초기화 실패
        /// </summary>
        public event Action<string> OnInitFailed;

        /// <summary>
        /// 도전 종료 완료 확인
        /// </summary>
        /// <param name="stageId">도전</param>
        /// <returns>완료 여부</returns>
        public bool IsStageCompleted ( int stageId )
        {
            return TryGetCompletedResult( stageId, out _ );
        }

        /// <summary>
        /// 정리까지 끝난 도전의 결과 조회
        /// </summary>
        /// <param name="stageId">도전 번호</param>
        /// <param name="result">완료 결과</param>
        /// <returns>조회 성공 여부</returns>
        public bool TryGetCompletedResult ( int stageId, out MissionStageResult result )
        {
            result = default;

            if ( IsStageIdle == false )
                return false;

            return _session.TryGetEndResult( stageId, out result );
        }

        #region ----- 프로퍼티/이벤트 -----

        /// <summary>
        /// 플레이어
        /// </summary>
        public Hero Hero => _hero;

        /// <summary>
        /// 공용 플레이 카메라
        /// </summary>
        public Camera PlayCamera => _playCamera;

        /// <summary>
        /// 재화 모듈
        /// </summary>
        public Currency Currency => _currency;

        /// <summary>
        /// 처치 보상 연결부
        /// </summary>
        public StageRewardController Rewards => _rewards;

        /// <summary>
        /// 동료 고용 관리자
        /// </summary>
        public CompanionHireController HireController => _hireController;

        /// <summary>
        /// 슬롯 아이템 인벤토리
        /// </summary>
        public ItemInventory Inventory => _inventory;

        /// <summary>
        /// 상점 상품
        /// </summary>
        public IReadOnlyList<IShopProduct> ShopProducts => _shopData.Products;

        /// <summary>
        /// 선택 무기
        /// </summary>
        public WeaponType? SelectedWeapon => _equipmentUnlock?.SelectedWeapon;

        /// <summary>
        /// 공용 슬롯 수
        /// </summary>
        public int QuickSlotCount => _quickSlots.Length;

        /// <summary>
        /// 공용 슬롯 요청 가능 여부
        /// </summary>
        public bool CanRequestQuickSlot => CanProcessQuickSlotRequest ( );

        /// <summary>
        /// 마지막 정산 도전의 아이템 사용 횟수
        /// </summary>
        public int LastStageUsedItemCount => _session != null ? _session.LastStageUsedItemCount : _previousStageUsedItemCount;

        /// <summary>
        /// 진행 중이거나 종료 처리 중인 도전 번호
        /// </summary>
        public int CurrentStageId => _session != null ? _session.CurrentStageId : 0;

        /// <summary>
        /// 마지막 종료 실패 위치
        /// </summary>
        public PlayEndFailure LastEndFailure => _session != null ? _session.LastEndFailure : PlayEndFailure.None;

        /// <summary>
        /// 슬롯 미션 진행
        /// </summary>
        public MissionController Missions => _missions;

        /// <summary>
        /// 슬롯 도감 상태
        /// </summary>
        public CollectionProgress Collections => _collectionProgress;

        /// <summary>
        /// 씬 초기화 완료 여부
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 도전 준비 완료 여부
        /// </summary>
        public bool IsStageReady => _session != null && _session.IsStageReady;

        /// <summary>
        /// 종료 예약 또는 정리 대기 여부
        /// </summary>
        public bool IsEndingStage => _session != null && _session.IsEndingStage;

        /// <summary>
        /// 정산 후 정리 대기 여부
        /// </summary>
        public bool IsStageCleanupPending => _session != null && _session.IsStageCleanupPending;

        /// <summary>
        /// 마지막 사건 전달 대기 여부
        /// </summary>
        public bool IsAwaitingEndEvents => _session != null && _session.IsAwaitingEndEvents;

        /// <summary>
        /// 저장 대기 중인 일반 웨이브, 없으면 0
        /// </summary>
        public int PendingWave => _session == null ? 0 : _session.PendingWave;

        /// <summary>
        /// 세션 요청 전달 가능 여부
        /// </summary>
        bool CanForwardSessionRequest => _isInitialized && _isPreparing == false;

        /// <summary>
        /// 저장 요청의 공통 처리 조건
        /// </summary>
        bool CanProcessSaveRequest => CanForwardSessionRequest
            && _session.IsStarting == false && _missions.IsProcessing == false;

        /// <summary>
        /// 도전 종료 결과 이벤트(도전 번호, 미지급 보상 폐기 여부, 정리까지 완료 여부)
        /// </summary>
        public event Action<int , bool , bool> OnStageEndProcessed;

        /// <summary>
        /// 씬 초기화 완료 이벤트
        /// </summary>
        public event Action OnInitialized;

        /// <summary>
        /// 진행 삭제 완료 이벤트, 타이틀 전환에 사용
        /// </summary>
        public event Action OnProgressDeleted;

        /// <summary>
        /// 해금/선택 변경 이벤트
        /// </summary>
        public event Action OnEquipmentChanged;

        /// <summary>
        /// 강화 변경 이벤트
        /// </summary>
        public event Action OnUpgradeChanged;

        /// <summary>
        /// 도감 변경 이벤트
        /// </summary>
        public event Action OnCollectionChanged;

        /// <summary>
        /// 도감 최초 해금 이벤트(해금/보상 결과)
        /// </summary>
        public event Action<CollectionUnlockInfo> OnCollectionUnlocked;

        #endregion

        #region ----- 초기화 -----

        /// <summary>
        /// 저장 슬롯과 씬 모듈 초기화
        /// </summary>
        /// <returns>초기화 성공 여부</returns>
        public bool Init ( )
        {
            return Init( PlayInitMode.CurrentProgress );
        }

        /// <summary>
        /// 경로별 초기화
        /// </summary>
        /// <param name="mode">실행 경로</param>
        /// <returns>성공 여부</returns>
        public bool Init ( PlayInitMode mode )
        {
            if ( CanInitializeScene( ) == false )
                return false;

            //진행 준비와 최초 알림까지 새 요청 차단
            _isPreparing = true;
            _initError = null;

            GameManager gameManager = GameManager.Instance;
            if ( gameManager == null || gameManager.IsInitialized == false )
                return FailInit( "공통 관리자가 준비되지 않았습니다." );

            SaveManager saveManager = gameManager.Save;
            if ( saveManager.HasProgress == false )
            {
                if ( mode != PlayInitMode.Direct )
                    return FailInit( "사용할 진행이 없습니다." );

                SaveResult result = saveManager.TryStartProgress( );
                if ( result != SaveResult.Success )
                    return FailInit( $"진행 준비 실패: {result} / {saveManager.LastError}" );
            }

            if ( mode != PlayInitMode.CurrentProgress )
                SetCursorLocked( false );

            bool isInitSucceeded = InitializeScene( saveManager, out string reason );
            if ( isInitSucceeded == false )
            {
                //부분 복원과 사용권을 정리한 뒤 실패 전달
                ReleaseProgress( );
                return FailInit( reason );
            }

            _inventory.NotifyChanged( );
            _upgradeProgress.NotifyChanged( );
            _missions.NotifyChanged( );
            _collectionProgress.NotifyChanged( );
            _isPreparing = false;

            gameManager.Scenes.SetBgm( SoundId.PlayBgm );
            OnInitialized?.Invoke( );
            return true;
        }

        /// <summary>
        /// 초기화 실패 전달
        /// </summary>
        /// <param name="reason">사유</param>
        /// <returns>실패</returns>
        bool FailInit ( string reason )
        {
            _initError = reason;
            _isPreparing = false;
            OnInitFailed?.Invoke( reason );
            return false;
        }

        /// <summary>
        /// 이전 연결 해제 후 슬롯과 모듈 준비
        /// </summary>
        /// <param name="saveManager">저장 관리자</param>
        /// <param name="reason">실패 사유</param>
        /// <returns>준비 성공 여부</returns>
        bool InitializeScene ( SaveManager saveManager, out string reason )
        {
            reason = null;
            ReleaseProgress ( );

            if ( _clearRewardData == null || _clearRewardData.Validate ( ) == false )
            {
                reason = "종료 보상 설정을 연결하고 금액/기준 점수 구성을 확인하세요.";
                return false;
            }

            if ( ValidateQuickSlotSetup ( ) == false
                || _shopData.Validate ( _itemDataMap , _weaponDataMap ) == false
                || _upgradeDataMap.Validate ( ) == false
                || _missionDataMap.Validate ( ) == false
                || _collectionDataMap.Validate ( ) == false
                || _inputHandler.InitQuickSlotInput ( ) == false )
            {
                reason = "씬 설정을 확인하세요.";
                return false;
            }

            _saveManager = saveManager;

            if ( _saveManager.TryAttachScene (
                this , out SaveService saveService , out SaveData saveData ) == false )
            {
                reason = "진행 사용권을 연결하지 못했습니다.";
                return false;
            }

            UpgradeProgress progress = new UpgradeProgress ( );

            if ( progress.Init ( saveData.Upgrades , _upgradeDataMap ) == false )
            {
                reason = "강화 기록을 복원하지 못했습니다.";
                return false;
            }

            MissionProgress missionProgress = new MissionProgress ( );

            //현재 설정과 저장 진행 대조
            if ( missionProgress.Init (
                saveData.MissionKillCounts , saveData.Missions , _missionDataMap ) == false )
            {
                reason = "미션 기록을 복원하지 못했습니다.";
                return false;
            }

            CollectionProgress collectionProgress = new CollectionProgress ( );
            collectionProgress.Init (
                saveData.CollectionUnlockedIds , saveData.CollectionNewIds , _collectionDataMap );

            //재화 초기 알림 전에 조회 객체 준비
            _upgradeProgress = progress;
            _missions = new MissionController (
                _missionDataMap , missionProgress , saveService , _currency );
            _collectionProgress = collectionProgress;
            _collectionUnlock = new CollectionUnlock (
                collectionProgress , saveService , _currency );

            _equipmentUnlock = new EquipmentUnlock (
                _shopData , _currency , saveService , saveData );

            if ( InitializeModules ( saveData ) == false )
            {
                reason = "씬 모듈을 초기화하지 못했습니다.";
                return false;
            }

            _itemUse = new HeroItemUse (
                _hero , _itemDataMap , _inventory , saveService, GameManager.Instance.Sound );

            _shopPurchase = new ShopPurchase (
                _shopData , _currency , _inventory , saveService , _equipmentUnlock );
            _upgradePurchase = new UpgradePurchase ( _upgradeDataMap , _upgradeProgress , _currency , saveService );

            _quickSlotContext = new QuickSlotContext (
                _hireController , _itemUse , _buildPreviewController , _equipmentUnlock );

            _saveService = saveService;
            BindSave ( );
            BindUpgrade ( );
            CreateSession ( );
            BindCollection ( );
            BindEquipment ( );

            _isInitialized = true;
            return true;
        }

        /// <summary>
        /// 씬 초기화 조건 확인
        /// </summary>
        /// <returns>초기화 가능 여부</returns>
        bool CanInitializeScene ( )
        {
            if ( isActiveAndEnabled == false || _isPreparing == true )
                return false;

            if ( _session != null )
            {
                if ( _session.IsStarting == true || _session.IsStageIdle ( ) == false )
                    return false;
            }
            else if ( _currency.IsStageActive == true
                || _rewards.CurrentStageId != 0 || _hireController.CurrentStageId != 0 )
            {
                //세션이 없어도 진행 중 도전 확인
                return false;
            }

            return _missions == null || _missions.CanBeginStage;
        }

        /// <summary>
        /// 도전 연결부 생성
        /// </summary>
        void CreateSession ( )
        {
            _session = new PlaySession (
                this , _hero , _currency , _rewards , _hireController ,
                _inventory , _itemUse , _weaponDataMap , _upgradeDataMap ,
                _upgradeProgress , _missions , _collectionDataMap , _collectionUnlock ,
                SetCursorLocked , ClearInstallationSelection , _previousStageUsedItemCount ,
                _equipmentUnlock , _saveService , _vfx , _clearRewardData );
            _session.OnStageEndProcessed += HandleStageEndProcessed;
        }

        /// <summary>
        /// 도전 연결부 구독 해제와 사용 기록 보존
        /// </summary>
        void ReleaseSession ( )
        {
            if ( _session == null )
                return;

            //재초기화 시 마지막 사용 기록 유지
            _previousStageUsedItemCount = _session.LastStageUsedItemCount;
            _session.OnStageEndProcessed -= HandleStageEndProcessed;
            _session.Dispose ( );
            _session = null;
        }

        /// <summary>
        /// 진행 사용과 저장 구독 전체 해제
        /// </summary>
        void ReleaseProgress ( )
        {
            _isInitialized = false;
            _initError = null;
            _quickSlotContext = null;
            UnbindSave ( );
            UnbindUpgrade ( );
            ReleaseSession ( );

            if ( _vfx != null )
            {
                //씬 파괴 중 먼저 제거된 캐릭터는 건너뜀
                if ( _hero != null )
                    _hero.StopStageActions( );

                if ( _hireController != null )
                    _hireController.StopStageActions( );

                //실행 중단 뒤 효과만 정리하고 공용 서비스는 유지
                _vfx.StopAll( VfxScope.Gameplay );
                _vfx = null;
            }

            UnbindCollection ( );
            UnbindEquipment ( );

            _missions = null;
            _saveService = null;
            _itemUse = null;
            _shopPurchase = null;
            _equipmentUnlock = null;
            _upgradePurchase = null;
            _upgradeProgress = null;
            _collectionProgress = null;
            _collectionUnlock = null;

            //전역 참조 해제와 관계없이 실제 연결했던 관리자에 반환
            if ( _saveManager != null )
                _saveManager.DetachScene ( this );

            _saveManager = null;
        }

        /// <summary>
        /// 미저장 미션을 우선 처리하고 현재 진행 저장
        /// </summary>
        /// <returns>저장 성공 여부</returns>
        public bool TrySaveProgress ( )
        {
            if ( CanProcessSaveRequest == false || IsEndingStage == true )
                return false;

            return SaveProgressCore ( true );
        }

        /// <summary>
        /// 도전 밖에서 이동 전 미저장 진행만 처리
        /// </summary>
        /// <returns>이동 전 저장 준비 완료 여부</returns>
        public bool TrySavePendingProgress ( )
        {
            if ( CanProcessSaveRequest == false || _session.IsStageIdle ( ) == false )
                return false;

            return SaveProgressCore ( false );
        }

        /// <summary>
        /// 백업 동안 결과 화면의 진행 사용권 반환
        /// </summary>
        /// <param name="save">연결한 저장 관리자</param>
        /// <returns>반환 성공 여부</returns>
        public bool TrySuspendProgressForBackup ( out SaveManager save )
        {
            save = null;
            if ( isActiveAndEnabled == false || TrySavePendingProgress( ) == false )
                return false;

            if ( _saveManager.TrySuspendScene( this ) == false )
                return false;

            //결과 세션은 보존하고 씬의 진행 쓰기만 차단
            _isPreparing = true;
            save = _saveManager;
            return true;
        }

        /// <summary>
        /// 백업 종료 후 기존 결과 화면에 진행 다시 연결
        /// </summary>
        /// <returns>재연결 성공 여부</returns>
        public bool TryResumeProgressAfterBackup ()
        {
            if ( _saveManager == null || _saveManager.TryResumeScene( this ) == false )
                return false;

            _isPreparing = false;
            return true;
        }

        /// <summary>
        /// 복원 성공 후 교체 전 진행과 결과 세션 해제
        /// </summary>
        internal void ReleaseProgressAfterRestore ()
        {
            ReleaseProgress( );
        }

        /// <summary>
        /// 일반 웨이브 저장 요청 전달
        /// </summary>
        /// <param name="stageId">원래 도전 번호</param>
        /// <param name="waveNumber">완료 웨이브 번호</param>
        /// <returns>저장 완료 여부</returns>
        public bool TrySaveWave ( int stageId , int waveNumber )
        {
            if ( CanForwardSessionRequest == false )
                return false;

            //저장 알림 중 새 요청이 들어오는 것을 차단
            _isPreparing = true;
            bool saved = _session.TrySaveWave ( stageId , waveNumber );
            _isPreparing = false;

            return saved;
        }

        /// <summary>
        /// 요청을 차단한 상태에서 진행 저장
        /// </summary>
        /// <param name="saveConfirmedData">변경이 없어도 파일을 저장할지 여부</param>
        /// <returns>저장 성공 여부</returns>
        bool SaveProgressCore ( bool saveConfirmedData )
        {
            _isPreparing = true;
            bool saved = _session.TrySaveProgress ( saveConfirmedData );
            _isPreparing = false;

            return saved;
        }

        /// <summary>
        /// 도전 밖에서 미저장 미션 정산 후 진행 삭제
        /// </summary>
        /// <returns>삭제 성공 여부</returns>
        public bool TryDeleteProgress ( )
        {
            if ( CanForwardSessionRequest == false || _session.IsStarting == true
                || _session.IsStageIdle ( ) == false || _missions.IsProcessing == true )
                return false;

            _isPreparing = true;

            //미션 저장 실패 시 연결과 원본 진행 유지
            if ( _missions.HasPendingSave == true && _missions.TrySaveProgress ( ) == false )
            {
                _isPreparing = false;
                return false;
            }

            SaveManager manager = _saveManager;

            ClearInstallationSelection ( );
            ReleaseProgress ( );
            SaveResult result = manager.TryDeleteProgress ( );
            _isPreparing = false;

            if ( result != SaveResult.Success )
            {
                Debug.LogError ( $"진행 삭제 실패: {result} / {manager.LastError}" , this );
                if ( Init ( ) == false )
                    Debug.LogError ( "진행 재연결 실패. 플레이를 중단합니다." , this );
                return false;
            }

            _previousStageUsedItemCount = 0;
            SetCursorLocked ( false );
            OnProgressDeleted?.Invoke ( );
            return true;
        }

        /// <summary>
        /// 도전 종료 결과 전달
        /// </summary>
        /// <param name="stageId">종료한 도전 번호</param>
        /// <param name="abandon">미지급 스테이지 보상 폐기 여부</param>
        /// <param name="succeeded">정리까지 완료 여부</param>
        void HandleStageEndProcessed ( int stageId , bool abandon , bool succeeded )
        {
            //정산/정리 재시도 실패에서는 결과음을 재생하지 않음
            if ( succeeded == true && abandon == false
                && _session.TryGetEndResult( stageId, out MissionStageResult result ) == true )
            {
                switch ( result.Outcome )
                {
                    case MissionOutcome.Victory:
                        GameManager.Instance.Sound.Play( SoundId.StageClear );
                        break;
                    case MissionOutcome.Defeat:
                        GameManager.Instance.Sound.Play( SoundId.StageFail );
                        break;
                }
            }

            OnStageEndProcessed?.Invoke ( stageId , abandon , succeeded );
        }

        protected virtual void Start ( )
        {
            GameManager gameManager = GameManager.Instance;
            if ( gameManager == null || gameManager.IsInitialized == false )
            {
                FailInit( "공통 관리자가 준비되지 않았습니다." );
                return;
            }

            if ( gameManager.Scenes.TryAttachPlay( this ) == false && _initError == null )
                FailInit( "로비에서 선택한 입장 정보와 씬 흐름을 확인하세요." );
        }

        #endregion

        protected virtual void Update ( )
        {
            if ( IsStageReady == false || IsEndingStage == true )
                return;

            float deltaTime = Time.deltaTime;
            _hero.UpdateRevive ( deltaTime );

            //피해/입력 처리 전에 무적 만료
            _hero.UpdateDodge ( deltaTime );

            //입력 전에 효과/쿨타임 갱신
            _hero.UpdateEffects ( deltaTime );
            _hero.UpdateAttack ( deltaTime );
            _hero.UpdateSkill ( deltaTime );
            _rewards.UpdateCombo ( Time.timeAsDouble );

            //커서 해제 중에도 시간/중력 갱신
            if ( Cursor.lockState == CursorLockMode.Locked && IsEndingStage == false )
                _inputHandler.UpdateInput ( );

            //이동 기준 시점 먼저 적용
            _cameraFocus.UpdateRotation ( deltaTime );
            _cameraFocus.ApplyRotation ( );

            Vector3 direction = CalculateMoveDirection ( _moveInput );
            _hero.SetAimDirection ( _cameraFocus.transform.forward );
            _hero.Move ( direction );
            _hero.UpdateMove ( deltaTime );

            //이동 회전 후 월드 시점만 복구
            _cameraFocus.ApplyRotation ( );

            _hero.UpdateUltimateAim ( );
        }

        protected virtual void LateUpdate ( )
        {
            _session?.ProcessPendingEnd ( );
        }

        #region ----- 핸들 -----

        /// <summary>
        /// 이동 입력 보관
        /// </summary>
        /// <param name="moveInput">수평 이동 입력</param>
        void HandleMoveInput ( Vector2 moveInput )
        {
            _moveInput = moveInput;
        }

        /// <summary>
        /// 이동 입력 방향으로 회피 요청
        /// </summary>
        void HandleDodgeInput ( )
        {
            //이번 입력 방향으로 회피
            _hero.TryDodge ( CalculateMoveDirection ( _moveInput ) );
        }

        /// <summary>
        /// 점프 요청
        /// </summary>
        void HandleJumpInput ( )
        {
            _hero.TryJump ( );
        }

        /// <summary>
        /// 공격 요청
        /// </summary>
        void HandleAttackInput ( )
        {
            if ( _hero.IsAimingUltimate == true )
            {
                _hero.TryRequestUltimate ( );
                return;
            }

            _hero.TryAttack ( );
        }

        /// <summary>
        /// 스킬 요청
        /// </summary>
        void HandleSkillInput ( )
        {
            _hero.CancelUltimateAim ( );
            _hero.TryUseSkill ( );
        }

        /// <summary>
        /// 궁극기 요청
        /// </summary>
        void HandleUltimateInput ( )
        {
            _hero.TryRequestUltimate ( );
        }

        #endregion

        #region ----- 스테이지 -----

        /// <summary>
        /// 씬 기본 설정으로 도전 시작
        /// </summary>
        /// <param name="initialStageCurrency">시작 스테이지 재화</param>
        /// <returns>시작 성공 여부</returns>
        public bool BeginStage ( int initialStageCurrency )
        {
            return BeginStage (
                initialStageCurrency , _missionMapId ,
                _missionDifficulty , _missionDefenseRule );
        }

        /// <summary>
        /// 저장된 선택 무기로 도전 시작
        /// </summary>
        /// <param name="initialStageCurrency">시작 스테이지 재화</param>
        /// <param name="mapId">맵 ID</param>
        /// <param name="difficulty">난이도</param>
        /// <param name="defenseRule">완전 방어 조건</param>
        /// <returns>시작 성공 여부</returns>
        public bool BeginStage (
            int initialStageCurrency ,
            string mapId ,
            MissionDifficulty difficulty ,
            MissionDefenseRule defenseRule )
        {
            WeaponType? weaponType = SelectedWeapon;
            return weaponType.HasValue && BeginStage (
                weaponType.Value , initialStageCurrency , mapId , difficulty , defenseRule );
        }

        /// <summary>
        /// 선택 무기와 전달된 설정으로 도전 시작
        /// </summary>
        /// <param name="weaponType">선택 무기</param>
        /// <param name="initialStageCurrency">시작 스테이지 재화</param>
        /// <param name="mapId">맵 ID</param>
        /// <param name="difficulty">난이도</param>
        /// <param name="defenseRule">완전 방어 조건</param>
        /// <param name="companionPriorityPosition">현재 맵의 동료 공격 우선순위 기준</param>
        /// <returns>시작 성공 여부</returns>
        public bool BeginStage (
            WeaponType weaponType ,
            int initialStageCurrency ,
            string mapId ,
            MissionDifficulty difficulty ,
            MissionDefenseRule defenseRule ,
            Vector3? companionPriorityPosition = null )
        {
            if ( CanForwardSessionRequest == false )
                return false;

            return _session.BeginStage (
                weaponType , initialStageCurrency , mapId , difficulty , defenseRule ,
                companionPriorityPosition );
        }

        /// <summary>
        /// 접수한 도전 종료 결과 조회
        /// </summary>
        /// <param name="stageId">조회할 도전 번호</param>
        /// <param name="result">접수한 종료 결과</param>
        /// <returns>조회 성공 여부</returns>
        public bool TryGetEndResult (
            int stageId ,
            out MissionStageResult result )
        {
            result = default;

            if ( _session == null )
                return false;

            return _session.TryGetEndResult ( stageId , out result );
        }

        /// <summary>
        /// 원래 도전의 종료 보상 조회
        /// </summary>
        /// <param name="stageId">도전 번호</param>
        /// <param name="result">계산/지급 결과</param>
        /// <returns>조회 성공 여부</returns>
        public bool TryGetClearReward ( int stageId , out ClearRewardResult result )
        {
            result = default;
            return _session != null && _session.TryGetClearReward ( stageId , out result );
        }

        /// <summary>
        /// 호환 종료 예약과 마지막 사건 전달 완료 처리
        /// </summary>
        /// <param name="stageId">종료할 도전 번호</param>
        /// <param name="abandon">미지급 보상 폐기 여부</param>
        /// <returns>종료 예약과 사건 전달 완료 접수 여부</returns>
        public bool RequestEndStage ( int stageId , bool abandon )
        {
            if ( CanForwardSessionRequest == false )
                return false;

            return _session.RequestEndStage ( stageId , abandon );
        }

        /// <summary>
        /// 패배/포기 종료 예약 후 마지막 사건 전달 대기
        /// </summary>
        /// <param name="result">도전 종료 결과</param>
        /// <param name="discardPendingCurrency">미지급 스테이지 보상 폐기 여부</param>
        /// <returns>종료 예약 접수 여부</returns>
        public bool RequestEndStage (
            MissionStageResult result ,
            bool discardPendingCurrency )
        {
            if ( CanForwardSessionRequest == false )
                return false;

            return _session.RequestEndStage ( result , discardPendingCurrency );
        }

        /// <summary>
        /// 종료 결과/도감 대상 보관 후 마지막 사건 전달 대기
        /// 승리 시 적 목록이 필요하며, 대상이 없으면 빈 목록 전달
        /// </summary>
        /// <param name="result">도전 종료 결과</param>
        /// <param name="discardPendingCurrency">미지급 스테이지 보상 폐기 여부</param>
        /// <param name="collectionEnemies">도감 대상 적 목록</param>
        /// <returns>종료 예약 접수 여부</returns>
        public bool RequestEndStage (
            MissionStageResult result ,
            bool discardPendingCurrency ,
            IReadOnlyList<EnemyData> collectionEnemies )
        {
            if ( CanForwardSessionRequest == false )
                return false;

            return _session.RequestEndStage ( result , discardPendingCurrency , collectionEnemies );
        }

        /// <summary>
        /// 마지막 사건 수신 종료와 정산 예약
        /// </summary>
        /// <param name="stageId">종료 예약된 도전 번호</param>
        /// <returns>정산 예약 접수 여부</returns>
        public bool CompleteEndEvents ( int stageId )
        {
            if ( CanForwardSessionRequest == false )
                return false;

            return _session.CompleteEndEvents ( stageId );
        }

        /// <summary>
        /// 클리어 사건 전달 완료
        /// </summary>
        /// <param name="stageId">도전</param>
        /// <param name="score">확정 점수</param>
        /// <returns>접수 여부</returns>
        public bool CompleteEndEvents ( int stageId, int score )
        {
            if ( CanForwardSessionRequest == false )
                return false;

            return _session.CompleteEndEvents( stageId, score );
        }

        /// <summary>
        /// 미완료 종료 단계 재시도 예약
        /// </summary>
        /// <returns>재시도 예약 접수 여부</returns>
        public bool RetryEndStage ( )
        {
            if ( CanForwardSessionRequest == false )
                return false;

            return _session.RetryEndStage ( );
        }

        /// <summary>
        /// 정산 후 남은 도전 정리와 결과 통지
        /// </summary>
        /// <returns>정리 완료 여부</returns>
        public bool TryCompleteStageCleanup ( )
        {
            if ( CanForwardSessionRequest == false )
                return false;

            return _session.TryCompleteStageCleanup ( );
        }

        /// <summary>
        /// 동료 전투 시작과 미션 전투 구간 전환
        /// </summary>
        /// <param name="result">동료 전투 시작 결과</param>
        /// <returns>전투 시작 성공 여부</returns>
        public bool BeginCombat ( out CompanionHireResult result )
        {
            result = CompanionHireResult.Unavailable;
            if ( CanForwardSessionRequest == false )
                return false;

            bool wasPlaying = _missions.IsPlaying;
            bool started = _session.BeginCombat ( out result );
            if ( started == true && wasPlaying == false )
                GameManager.Instance.Sound.Play( SoundId.WaveStart );
            return started;
        }

        /// <summary>
        /// 첫 전투 이후 준비/전투 구간 전환
        /// </summary>
        /// <param name="stageId">전환할 도전 번호</param>
        /// <param name="isPlaying">전투 구간 여부</param>
        /// <returns>구간 전환 성공 여부</returns>
        public bool SetPlayPhase ( int stageId , bool isPlaying )
        {
            if ( CanForwardSessionRequest == false )
                return false;

            bool wasPlaying = _missions.IsPlaying;
            bool changed = _session.SetPlayPhase ( stageId , isPlaying );
            if ( changed == true && isPlaying == true && wasPlaying == false )
                GameManager.Instance.Sound.Play( SoundId.WaveStart );
            return changed;
        }

        /// <summary>
        /// 미션 처치 등록
        /// </summary>
        /// <param name="kill">도전/생명 번호와 적 정보</param>
        /// <returns>처치 접수/저장 상태</returns>
        public MissionKillResult RegisterMissionKill ( MissionKillContext kill )
        {
            //비활성화 중 확정 사건 수신 유지
            if ( CanForwardSessionRequest == false )
                return MissionKillResult.Rejected;

            return _session.RegisterMissionKill ( kill );
        }

        #endregion

        #region ----- 도감 -----

        /// <summary>
        /// 플레이 중 상점에서 사용할 적 도감 데이터 조회
        /// </summary>
        /// <param name="stageId">요청 화면에 연결된 도전 번호</param>
        /// <param name="enemy">조회할 원본 적 설정</param>
        /// <param name="data">이름/특징/공격 설명 표시에 사용할 도감 설정</param>
        /// <returns>현재 도전 요청과 적 매핑이 유효한지 여부</returns>
        public bool TryGetEnemyBriefInfo (
            int stageId,
            EnemyData enemy,
            out EnemyCollectionData data )
        {
            data = null;

            //초기화 완료 후에만 세션을 조회하며 종료/이전 도전 요청 차단
            if ( CanForwardSessionRequest == false
                || _session.CanProcessStageRequest ( ) == false
                || stageId != CurrentStageId
                || enemy == null )
                return false;

            //초기화에서 검사한 맵을 사용하며 해금/신규/저장 상태는 변경하지 않음
            return _collectionDataMap.TryGet ( enemy, out data );
        }

        /// <summary>
        /// 도감 신규 표시 해제
        /// </summary>
        /// <param name="source">화면에 연결된 슬롯 도감 객체</param>
        /// <param name="enemyIds">화면에서 확인한 신규 항목 ID</param>
        /// <returns>변경 불필요 또는 저장 성공 여부</returns>
        public bool TryMarkCollectionSeen (
            CollectionProgress source ,
            IReadOnlyList<string> enemyIds )
        {
            //이전 슬롯 화면의 확인 요청 차단
            if ( isActiveAndEnabled == false || _isInitialized == false
                || _isPreparing == true || _session.IsStarting == true
                || source != _collectionProgress )
                return false;

            return _collectionUnlock.TryMarkSeen ( enemyIds );
        }

        /// <summary>
        /// 도전별 도감 해금 결과 조회
        /// </summary>
        /// <param name="stageId">조회할 도전 번호</param>
        /// <returns>해금/보상 결과, 결과가 없으면 null</returns>
        public CollectionUnlockInfo GetCollectionStageResult ( int stageId )
        {
            return _collectionUnlock?.GetStageResult ( stageId );
        }

        /// <summary>
        /// 도감 변경/해금 구독
        /// </summary>
        void BindCollection ( )
        {
            //최초 활성화는 슬롯 준비 전
            if ( _collectionProgress == null )
                return;

            _collectionProgress.OnChanged += HandleCollectionChanged;
            _collectionUnlock.OnUnlocked += HandleCollectionUnlocked;
        }

        /// <summary>
        /// 도감 변경/해금 구독 해제
        /// </summary>
        void UnbindCollection ( )
        {
            if ( _collectionProgress == null )
                return;

            _collectionProgress.OnChanged -= HandleCollectionChanged;
            _collectionUnlock.OnUnlocked -= HandleCollectionUnlocked;
        }

        /// <summary>
        /// 도감 변경 전달
        /// </summary>
        void HandleCollectionChanged ( )
        {
            OnCollectionChanged?.Invoke ( );
        }

        /// <summary>
        /// 도감 해금 결과 전달
        /// </summary>
        /// <param name="result">해금/보상 결과</param>
        void HandleCollectionUnlocked ( CollectionUnlockInfo result )
        {
            OnCollectionUnlocked?.Invoke ( result );
        }

        #endregion

        #region ----- 공용 슬롯 -----

        /// <summary>
        /// 공용 슬롯 설정 조회
        /// </summary>
        /// <param name="slotIndex">슬롯 번호</param>
        /// <param name="slot">슬롯 설정</param>
        /// <returns>조회 성공 여부</returns>
        public bool TryGetQuickSlot (
            int slotIndex ,
            out QuickSlotData slot )
        {
            slot = null;

            if ( slotIndex < 0 || slotIndex >= _quickSlots.Length )
                return false;

            slot = _quickSlots [ slotIndex ];
            return true;
        }

        /// <summary>
        /// 동료 고용 설정 조회
        /// </summary>
        /// <param name="weaponType">동료 무기 타입</param>
        /// <param name="data">고용 설정</param>
        /// <returns>조회 성공 여부</returns>
        public bool TryGetCompanionHireData (
            WeaponType weaponType ,
            out CompanionHireData data )
        {
            return _companionHireDataMap.TryGet ( weaponType , out data );
        }

        /// <summary>
        /// 동료 고용/재고용 요청
        /// </summary>
        /// <param name="weaponType">동료 무기 타입</param>
        /// <param name="hireId">고용 번호</param>
        /// <param name="result">처리 결과</param>
        /// <returns>고용 성공 여부</returns>
        public bool TryHireCompanion (
            WeaponType weaponType ,
            out int hireId ,
            out CompanionHireResult result )
        {
            hireId = 0;
            result = CompanionHireResult.Unavailable;

            if ( CanProcessQuickSlotRequest ( ) == false )
                return false;

            return _quickSlotContext.TryHireCompanion (
                weaponType , out hireId , out result );
        }

        /// <summary>
        /// 아이템과 공용 슬롯 설정 검사
        /// </summary>
        /// <returns>설정 유효 여부</returns>
        bool ValidateQuickSlotSetup ( )
        {
            if ( _itemDataMap.Validate ( ) == false )
                return false;

            if ( _quickSlots.Length != 10 )
            {
                Debug.LogError ( "공용 슬롯은 10칸이어야 합니다." , this );
                return false;
            }

            for ( int i = 0 ; i < _quickSlots.Length ; i++ )
            {
                QuickSlotData slot = _quickSlots [ i ];

                if ( slot == null )
                    continue;

                if ( slot.Validate ( _itemDataMap , _companionHireDataMap ) == true )
                    continue;

                Debug.LogError (
                    $"공용 슬롯 설정이 잘못됐습니다: 슬롯={i} / 설정={slot.name}" , this );
                return false;
            }

            return true;
        }

        /// <summary>
        /// 공용 슬롯 요청 조건 확인
        /// </summary>
        /// <returns>요청 가능 여부</returns>
        bool CanProcessQuickSlotRequest ( )
        {
            return CanForwardSessionRequest
                && _session.CanProcessStageRequest ( );
        }

        /// <summary>
        /// 공용 슬롯 입력 전달
        /// </summary>
        /// <param name="slotIndex">0부터 시작하는 슬롯 번호</param>
        void HandleQuickSlotInput ( int slotIndex )
        {
            TryExecuteQuickSlot ( slotIndex );
        }

        /// <summary>
        /// 공용 슬롯 실행
        /// </summary>
        /// <param name="slotIndex">0부터 시작하는 슬롯 번호</param>
        /// <returns>실행 성공 여부</returns>
        public bool TryExecuteQuickSlot ( int slotIndex )
        {
            if ( CanProcessQuickSlotRequest ( ) == false )
                return false;

            if ( TryGetQuickSlot ( slotIndex , out QuickSlotData slot ) == false )
            {
                Debug.LogError ( $"공용 슬롯 번호가 잘못됐습니다: {slotIndex}" , this );
                return false;
            }

            if ( slot == null )
            {
                Debug.Log ( $"빈 공용 슬롯입니다: 슬롯={slotIndex}" , this );
                return false;
            }

            return slot.TryExecute ( _quickSlotContext );
        }

        /// <summary>
        /// 맵의 설치 프리뷰와 동료 배치점 교체
        /// </summary>
        /// <param name="buildPreview">설치 프리뷰</param>
        /// <param name="placementPoints">동료 배치점</param>
        internal void SetMapReferences (
            BuildPreviewController buildPreview ,
            CompanionPlacementPoint [ ] placementPoints )
        {
            ClearInstallationSelection ( );
            _buildPreviewController = buildPreview;
            _quickSlotContext = new QuickSlotContext (
                _hireController , _itemUse , _buildPreviewController , _equipmentUnlock );
            _placementController.SetPoints ( placementPoints );
        }

        /// <summary>
        /// 설치물 선택 해제
        /// </summary>
        void ClearInstallationSelection ( )
        {
            if ( _buildPreviewController == null )
                return;

            _buildPreviewController.ClearSelection ( );
        }

        #endregion

        #region ----- 아이템 -----

        /// <summary>
        /// 아이템 사용 조건 조회
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>사용 가능 여부/제한 사유</returns>
        public ItemUseResult GetItemUseState ( string itemId )
        {
            if ( CanProcessQuickSlotRequest ( ) == false )
                return ItemUseResult.Unavailable;

            return _itemUse.GetUseState ( itemId , Time.timeAsDouble );
        }

        /// <summary>
        /// 아이템 재사용 대기시간 조회
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>남은 초, 대기시간이 없으면 0</returns>
        public double GetRemainingItemCooldown ( string itemId )
        {
            return _inventory.GetRemainingCooldown ( itemId , Time.timeAsDouble );
        }

        /// <summary>
        /// 아이템 사용
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <param name="appliedAmount">효과 적용량</param>
        /// <returns>사용 처리 결과</returns>
        public ItemUseResult TryUseItem ( string itemId , out float appliedAmount )
        {
            appliedAmount = 0f;

            if ( CanProcessQuickSlotRequest ( ) == false )
                return ItemUseResult.Unavailable;

            return _itemUse.TryUse (
                itemId , Time.timeAsDouble , out appliedAmount );
        }

        #endregion

        #region ----- 상점 -----

        /// <summary>
        /// 상품 구매 조건 조회
        /// </summary>
        /// <param name="productId">상품 ID</param>
        /// <param name="price">구매 가격</param>
        /// <returns>구매 조건</returns>
        public ShopPurchaseResult GetProductPurchaseState ( string productId , out int price )
        {
            price = 0;
            if ( CanProcessPurchaseRequest ( ) == false )
                return ShopPurchaseResult.Unavailable;

            return _shopPurchase.GetProductPurchaseState ( productId , out price );
        }

        /// <summary>
        /// 상품 구매
        /// </summary>
        /// <param name="productId">상품 ID</param>
        /// <returns>구매 결과</returns>
        public ShopPurchaseResult TryPurchaseProduct ( string productId )
        {
            if ( CanProcessPurchaseRequest ( ) == false )
                return ShopPurchaseResult.Unavailable;

            return _shopPurchase.TryPurchaseProduct ( productId );
        }

        /// <summary>
        /// 보유 무기 선택
        /// </summary>
        /// <param name="weaponType">무기 타입</param>
        /// <returns>선택 결과</returns>
        public ShopPurchaseResult TrySelectWeapon ( WeaponType weaponType )
        {
            if ( CanProcessPurchaseRequest ( ) == false )
                return ShopPurchaseResult.Unavailable;

            return _equipmentUnlock.TrySelectWeapon ( weaponType );
        }

        /// <summary>
        /// 무기 해금 여부 조회
        /// </summary>
        /// <param name="weaponType">무기 타입</param>
        /// <returns>해금 여부</returns>
        public bool IsWeaponUnlocked ( WeaponType weaponType )
        {
            return _equipmentUnlock != null && _equipmentUnlock.IsWeaponUnlocked ( weaponType );
        }

        /// <summary>
        /// 설치물 해금 여부 조회
        /// </summary>
        /// <param name="installationId">설치물 ID</param>
        /// <returns>해금 여부</returns>
        public bool IsInstallationUnlocked ( string installationId )
        {
            return _equipmentUnlock != null && _equipmentUnlock.IsInstallationUnlocked ( installationId );
        }

        /// <summary>
        /// 해금 변경 구독
        /// </summary>
        void BindEquipment ( )
        {
            if ( _equipmentUnlock != null )
                _equipmentUnlock.OnChanged += HandleEquipmentChanged;
        }

        /// <summary>
        /// 해금 변경 구독 해제
        /// </summary>
        void UnbindEquipment ( )
        {
            if ( _equipmentUnlock != null )
                _equipmentUnlock.OnChanged -= HandleEquipmentChanged;
        }

        /// <summary>
        /// 해금 변경 전달
        /// </summary>
        void HandleEquipmentChanged ( )
        {
            OnEquipmentChanged?.Invoke ( );
        }

        /// <summary>
        /// 입장 전 구매/강화 요청 조건 확인
        /// </summary>
        /// <returns>요청 가능 여부</returns>
        bool CanProcessPurchaseRequest ( )
        {
            return CanForwardSessionRequest
                && _session.CanProcessLobbyRequest ( );
        }

        /// <summary>
        /// 소모품 구매 조건 조회
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>구매 가능 여부/제한 사유</returns>
        public ShopPurchaseResult GetPurchaseState ( string itemId )
        {
            if ( CanProcessPurchaseRequest ( ) == false )
                return ShopPurchaseResult.Unavailable;

            return _shopPurchase.GetPurchaseState ( itemId );
        }

        /// <summary>
        /// 소모품 구매
        /// </summary>
        /// <param name="itemId">아이템 ID</param>
        /// <returns>구매 처리 결과</returns>
        public ShopPurchaseResult TryPurchaseItem ( string itemId )
        {
            if ( CanProcessPurchaseRequest ( ) == false )
                return ShopPurchaseResult.Unavailable;

            return _shopPurchase.TryPurchase ( itemId );
        }

        #endregion

        #region ----- 강화 -----

        /// <summary>
        /// 입장 전 강화 상태 조회
        /// </summary>
        /// <param name="upgradeId">강화 ID</param>
        /// <returns>강화 단계/효과와 구매 조건</returns>
        public UpgradeState GetUpgradeState ( string upgradeId )
        {
            if ( CanProcessPurchaseRequest ( ) == false )
                return new UpgradeState ( UpgradePurchaseResult.Unavailable );

            return _upgradePurchase.GetUpgradeState ( upgradeId );
        }

        /// <summary>
        /// 입장 전 다음 강화 단계 구매
        /// </summary>
        /// <param name="upgradeId">강화 ID</param>
        /// <returns>구매 처리 결과</returns>
        public UpgradePurchaseResult TryUpgrade ( string upgradeId )
        {
            if ( CanProcessPurchaseRequest ( ) == false )
                return UpgradePurchaseResult.Unavailable;

            return _upgradePurchase.TryUpgrade ( upgradeId );
        }

        /// <summary>
        /// 입장 전 플레이어 강화 미리보기
        /// </summary>
        /// <param name="type">무기 타입</param>
        /// <param name="stats">강화 증가량</param>
        /// <returns>조회 성공 여부</returns>
        public bool TryGetHeroUpgradePreview (
            WeaponType type ,
            out HeroUpgradeStats stats )
        {
            stats = default;

            if ( CanProcessPurchaseRequest ( ) == false )
                return false;

            stats = _session.CalculateHeroUpgrades ( type );
            return true;
        }

        /// <summary>
        /// 입장 전 설치물 강화 미리보기
        /// </summary>
        /// <param name="data">설치물 설정</param>
        /// <param name="stats">강화 증가량</param>
        /// <returns>조회 성공 여부</returns>
        public bool TryGetInstallationUpgradePreview (
            TrapData data ,
            out InstallationUpgradeStats stats )
        {
            stats = default;

            if ( CanProcessPurchaseRequest ( ) == false )
                return false;

            stats = InstallationUpgradeCalculator.Calculate (
                _upgradeDataMap , _upgradeProgress , data );
            return true;
        }

        /// <summary>
        /// 도전 시작 시 확정한 설치물 강화 결과 조회
        /// </summary>
        /// <param name="stageId">조회할 도전 번호</param>
        /// <param name="data">설치물 설정</param>
        /// <param name="stats">강화 증가량</param>
        /// <returns>조회 성공 여부</returns>
        public bool TryGetStageInstallationStats (
            int stageId ,
            TrapData data ,
            out InstallationUpgradeStats stats )
        {
            stats = default;
            if ( CanForwardSessionRequest == false )
                return false;

            return _session.TryGetStageInstallationStats ( stageId , data , out stats );
        }

        /// <summary>
        /// 강화 변경 구독
        /// </summary>
        void BindUpgrade ( )
        {
            if ( _upgradeProgress != null )
                _upgradeProgress.OnChanged += HandleUpgradeChanged;
        }

        /// <summary>
        /// 강화 변경 구독 해제
        /// </summary>
        void UnbindUpgrade ( )
        {
            if ( _upgradeProgress != null )
                _upgradeProgress.OnChanged -= HandleUpgradeChanged;
        }

        /// <summary>
        /// 강화 변경 전달
        /// </summary>
        void HandleUpgradeChanged ( )
        {
            OnUpgradeChanged?.Invoke ( );
        }

        #endregion

        #region ----- 조작 -----

        /// <summary>
        /// 이동 입력을 카메라 기준 월드 방향으로 변환
        /// </summary>
        /// <param name="moveInput">수평 이동 입력</param>
        /// <returns>입력 크기를 반영한 월드 이동 방향</returns>
        Vector3 CalculateMoveDirection ( Vector2 moveInput )
        {
            //입력 크기를 유지하며 대각선 속도 제한
            moveInput = Vector2.ClampMagnitude ( moveInput , 1f );

            //카메라 수평 방향 기준
            Quaternion yawRotation = Quaternion.Euler (
                0f , _cameraFocus.transform.eulerAngles.y , 0f );

            Vector3 direction = new Vector3 ( moveInput.x , 0f , moveInput.y );

            return yawRotation * direction;
        }

        /// <summary>
        /// 커서 잠금과 표시 설정
        /// </summary>
        /// <param name="locked">커서 잠금 여부</param>
        public void SetCursorLocked ( bool locked )
        {
            Cursor.lockState = locked
                ? CursorLockMode.Locked
                : CursorLockMode.None;

            Cursor.visible = locked == false;

            //커서 해제 시 남은 이동 입력 제거
            if ( locked == false )
            {
                _hero.CancelUltimateAim ( );
                _moveInput = Vector2.zero;
                _hero.Move ( Vector3.zero );
            }
        }

        #endregion

        #region ----- 저장 -----

        /// <summary>
        /// 저장 데이터 적용과 씬 모듈 초기화
        /// </summary>
        /// <param name="saveData">슬롯 저장 데이터</param>
        /// <returns>초기화 성공 여부</returns>
        bool InitializeModules ( SaveData saveData )
        {
            //공용 서비스의 초기화는 GameManager가 담당
            GameManager gameManager = GameManager.Instance;
            _vfx = gameManager.Vfx;
            SoundManager sound = gameManager.Sound;
            _hero.SetSound( sound, isLocalPlayer: true );
            _hero.SetVfx( _vfx );
            _hireController.SetSound( sound );
            _hireController.SetVfx( _vfx );

            //재화 알림 전에 아이템 수량 복원
            _inventory.Init ( saveData.Items );

            if ( _currency.Init ( saveData.PermanentCurrencyBalance ) == false
                || _rewards.Init ( _currency , _comboData ) == false
                || _hireController.Init (
                    _currency , _hero.transform , _companionHireDataMap ,
                    CanProcessQuickSlotRequest ) == false )
                return false;

            //고용 초기화 후 배치 연결
            _placementController.Init ( _hireController , _hero.transform );

            return true;
        }

        /// <summary>
        /// 영구 재화 저장 요청 구독
        /// </summary>
        void BindSave ( )
        {
            //최초 활성화는 저장 준비 전
            if ( _saveService == null )
                return;

            _currency.OnPermanentSaveRequested +=
                TrySavePermanentBalance;
        }

        /// <summary>
        /// 영구 재화 저장 요청 구독 해제
        /// </summary>
        void UnbindSave ( )
        {
            if ( _saveService == null )
                return;

            _currency.OnPermanentSaveRequested -=
                TrySavePermanentBalance;
        }

        /// <summary>
        /// 영구 재화 저장 요청 전달
        /// </summary>
        /// <param name="balance">새 잔액</param>
        /// <returns>저장 성공 여부</returns>
        bool TrySavePermanentBalance ( int balance )
        {
            return CanForwardSessionRequest && _saveService.TrySavePermanentBalance( balance );
        }

        #endregion

        #region ----- 기타 -----

        protected virtual void OnEnable ( )
        {
            _inputHandler.OnMoveInput += HandleMoveInput;
            _inputHandler.OnLookInput += _cameraFocus.Rotate;
            _inputHandler.OnJumpInput += HandleJumpInput;
            _inputHandler.OnDodgeInput += HandleDodgeInput;
            _inputHandler.OnAttackInput += HandleAttackInput;
            _inputHandler.OnSkillInput += HandleSkillInput;
            _inputHandler.OnUltimateInput += HandleUltimateInput;
            _inputHandler.OnQuickSlotInput += HandleQuickSlotInput;

            //비활성화 시 복구할 커서 상태
            _previousCursorLock = Cursor.lockState;
            _prevCursorVisible = Cursor.visible;

            BindSave ( );

            //재활성화는 구독만 복구
            BindUpgrade ( );
            BindCollection ( );
            BindEquipment ( );

            if ( IsStageReady == true && IsEndingStage == false )
                SetCursorLocked ( true );
            else if ( _isInitialized == true )
                SetCursorLocked ( false );
        }


        protected virtual void OnDisable ( )
        {
            _hero.CancelUltimateAim ( );
            ClearInstallationSelection ( );
            UnbindSave ( );
            UnbindUpgrade ( );

            //입력/표시 연결 해제, 도전 상태와 미션 사건 연결 유지
            UnbindCollection ( );
            UnbindEquipment ( );
            _inputHandler.OnMoveInput -= HandleMoveInput;
            _inputHandler.OnLookInput -= _cameraFocus.Rotate;
            _inputHandler.OnJumpInput -= HandleJumpInput;
            _inputHandler.OnDodgeInput -= HandleDodgeInput;
            _inputHandler.OnAttackInput -= HandleAttackInput;
            _inputHandler.OnSkillInput -= HandleSkillInput;
            _inputHandler.OnUltimateInput -= HandleUltimateInput;
            _inputHandler.OnQuickSlotInput -= HandleQuickSlotInput;

            _moveInput = Vector2.zero;
            _hero.Move ( Vector3.zero );
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _prevCursorVisible;
        }

        protected virtual void OnDestroy ( )
        {
            GameManager gameManager = GameManager.Instance;
            if ( gameManager != null )
                gameManager.Scenes.DetachPlay( this );

            ReleaseProgress ( );
        }

        protected virtual void OnDrawGizmos ( )
        {
            //플레이 중에는 실행 시 설정한 범위 사용
            if ( Application.isPlaying == true )
                return;

            //편집 중 미연결 참조 제외
            if ( _hero == null || _weaponDataMap == null )
                return;

            if ( _weaponDataMap.TryGet (
                _selectedWeaponType , out WeaponData data ) == false )
                return;

            _hero.SetWeaponGizmo ( data );
        }

        #endregion
    }
}
