using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 로비 상태
/// </summary>
public enum LobbyState
{
    Closed,         //연결 없음
    Initializing,   //초기화 중
    Ready,          //요청 가능
    Suspended,      //요청 차단
    Closing         //해제 중
}

/// <summary>
/// 로비 연결부 - 진행 복원과 로비 기능 연결
/// </summary>
public class LobbyController : MonoBehaviour
{
    [Header ( "----- 스테이지 목록 -----" )]
    [SerializeField] StageSelectionData _stageSelectionData;       //스테이지 목록 설정

    [Header ( "----- 데이터 -----" )]
    [SerializeField] Currency _currency;        //로비 재화
    [SerializeField] ShopData _shopData;        //상품 설정
    [SerializeField] ItemDataMap _itemDataMap;      //아이템 설정
    [SerializeField] WeaponDataMap _weaponDataMap;      //무기 설정
    [SerializeField] UpgradeDataMap _upgradeDataMap;        //강화 설정
    [SerializeField] MissionDataMap _missionDataMap;        //미션 설정
    [SerializeField] EnemyCollectionDataMap _collectionDataMap;     //도감 설정

    [Header ( "----- 등장 적 -----" )]
    [SerializeField] LobbyEnemySource _enemySource; //구성 목록 제공자
    [SerializeField] LobbyEnemyPolicy _enemyPolicy; //미해금 표시 정책

    LobbyEnemyQuery _enemyQuery; //현재 진행의 등장 적 조회

    SaveManager _saveManager;       //진행 사용권 소유자
    SaveService _saveService;       //공유 저장 서비스
    ItemInventory _inventory;       //보유 아이템
    EquipmentUnlock _equipmentUnlock;       //장비 보유/선택

    ShopPurchase _shopPurchase;     //상품 구매
    UpgradeProgress _upgradeProgress;       //강화 단계
    UpgradePurchase _upgradePurchase;       //강화 구매

    MissionController _missions;        //미션 조회/저장
    CollectionProgress _collections;        //도감 기록
    CollectionUnlock _collectionUnlock;     //신규 확인 저장

    LobbyState _state;      //현재 상태
    int _selectedStageIndex = -1;       //선택 맵 번호
    MissionDifficulty _selectedDifficulty;      //선택 난이도

    #region ----- 프로퍼티/이벤트 -----

    /// <summary>
    /// 현재 상태
    /// </summary>
    public LobbyState State => _state;

    /// <summary>
    /// 초기화 완료 여부
    /// </summary>
    public bool IsInitialized =>
        _state == LobbyState.Ready
        || _state == LobbyState.Suspended;

    /// <summary>
    /// 조회 가능 여부
    /// </summary>
    public bool CanQuery => IsInitialized && isActiveAndEnabled;

    /// <summary>
    /// 요청 가능 여부
    /// </summary>
    public bool CanProcessRequest =>
        CanQuery
        && _state == LobbyState.Ready
        && _missions.IsProcessing == false;

    /// <summary>
    /// 선택 맵 목록
    /// </summary>
    public IReadOnlyList<LobbyStageInfo> Stages => _stageSelectionData.Stages;

    /// <summary>
    /// 선택 맵
    /// </summary>
    public LobbyStageInfo SelectedStage =>
        _selectedStageIndex >= 0 ? Stages [ _selectedStageIndex ] : null;

    /// <summary>
    /// 선택 난이도
    /// </summary>
    public MissionDifficulty SelectedDifficulty => _selectedDifficulty;

    /// <summary>
    /// 선택 무기
    /// </summary>
    public WeaponType? SelectedWeapon => _equipmentUnlock?.SelectedWeapon;

    /// <summary>
    /// 재화 모듈
    /// </summary>
    public Currency Currency => _currency;

    /// <summary>
    /// 보유 아이템
    /// </summary>
    public ItemInventory Inventory => _inventory;

    /// <summary>
    /// 상품 목록
    /// </summary>
    public IReadOnlyList<IShopProduct> ShopProducts => _shopData.Products;

    /// <summary>
    /// 강화 목록
    /// </summary>
    public IReadOnlyList<UpgradeData> Upgrades => _upgradeDataMap.Upgrades;

    /// <summary>
    /// 미션 목록
    /// </summary>
    public IReadOnlyList<MissionData> Missions => _missionDataMap.OrderedMissions;

    /// <summary>
    /// 도감 기록
    /// </summary>
    public CollectionProgress Collections => _collections;

    /// <summary>
    /// 탐험 레벨
    /// </summary>
    public int ExplorationLevel => _missions.ExplorationLevel;

    /// <summary>
    /// 초기화 완료 이벤트
    /// </summary>
    public event Action OnInitialized;

    /// <summary>
    /// 로비 상태 알림
    /// </summary>
    public event Action OnStateChanged;

    /// <summary>
    /// 모델 해제 전 이벤트
    /// </summary>
    public event Action OnReleasing;

    /// <summary>
    /// 맵/난이도 변경 이벤트
    /// </summary>
    public event Action OnSelectionChanged;

    /// <summary>
    /// 장비 변경 이벤트
    /// </summary>
    public event Action OnEquipmentChanged;

    /// <summary>
    /// 강화 변경 이벤트
    /// </summary>
    public event Action OnUpgradeChanged;

    /// <summary>
    /// 미션 변경 이벤트
    /// </summary>
    public event Action OnMissionChanged;

    /// <summary>
    /// 도감 변경 이벤트
    /// </summary>
    public event Action OnCollectionChanged;

    /// <summary>
    /// 미션 보상 확정 이벤트(도전 번호, 보상 목록, 합계)
    /// </summary>
    public event Action<int , IReadOnlyList<MissionReward> , int> OnMissionRewardsCommitted;

    #endregion

    #region ----- 로비 수명 -----

    /// <summary>
    /// 준비된 진행으로 로비 모듈 구성
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>구성 여부</returns>
    bool Init ( out string reason )
    {
        if ( ValidateSetup ( out reason ) == false )
            return false;

        GameManager game = GameManager.Instance;

        if ( game == null )
        {
            reason = "공통 관리자가 준비되지 않았습니다.";
            return false;
        }

        if ( game.StartupProgress != StartupProgressState.Ready )
        {
            reason = game.StartupProgressError ?? "영구 진행을 준비 중입니다.";
            return false;
        }

        SaveManager manager = game.Save;

        if ( manager.TryAttachScene (
            this , out SaveService service , out SaveData data ) == false )
        {
            reason = "진행 사용권을 연결하지 못했습니다.";
            return false;
        }

        //이후 실패에서는 실제 연결한 관리자에 사용권 반환
        _saveManager = manager;
        _saveService = service;

        _upgradeProgress = new UpgradeProgress ( );
        MissionProgress missionProgress = new MissionProgress ( );

        if ( _upgradeProgress.Init ( data.Upgrades , _upgradeDataMap ) == false )
        {
            reason = "저장된 강화 기록을 확인하세요.";
            return false;
        }

        if ( missionProgress.Init (
            data.MissionKillCounts , data.Missions , _missionDataMap ) == false )
        {
            reason = "저장된 미션 기록을 확인하세요.";
            return false;
        }

        _inventory = new ItemInventory ( );
        _inventory.Init ( data.Items );

        _collections = new CollectionProgress ( );
        _collections.Init (
            data.CollectionUnlockedIds , data.CollectionNewIds , _collectionDataMap );

        //현재 도감 복원 이후, 최초 상태 알림 전에 조회 연결
        if ( _enemySource != null
            && ( _enemyPolicy == LobbyEnemyPolicy.HideLocked
                || _enemyPolicy == LobbyEnemyPolicy.RevealLocked ) )
        {
            _enemyQuery = new LobbyEnemyQuery (
                _enemySource , _collectionDataMap , _collections , _enemyPolicy );
        }

        _missions = new MissionController (
            _missionDataMap , missionProgress , service , _currency );

        _collectionUnlock = new CollectionUnlock ( _collections , service , _currency );
        _equipmentUnlock = new EquipmentUnlock ( _shopData , _currency , service , data );

        _shopPurchase = new ShopPurchase (
            _shopData , _currency , _inventory , service , _equipmentUnlock );
        _upgradePurchase = new UpgradePurchase (
            _upgradeDataMap , _upgradeProgress , _currency , service );

        _selectedStageIndex = 0;
        _selectedDifficulty = GetDefaultDifficulty ( Stages [ 0 ] );

        //재화 최초 알림 전에 나머지 조회 객체 복원
        if ( _currency.Init ( data.PermanentCurrencyBalance ) == false )
        {
            reason = "로비 재화를 초기화하지 못했습니다.";
            return false;
        }

        //실패 가능한 준비를 마친 뒤 구독
        BindModules ( );
        return true;
    }

    /// <summary>
    /// 로비 진행 연결
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>초기화 여부</returns>
    public bool TryOpen ( out string reason )
    {
        reason = null;

        if ( _state != LobbyState.Closed )
        {
            reason = "이미 로비가 연결되어 있습니다.";
            return false;
        }

        if ( isActiveAndEnabled == false )
        {
            reason = "활성화된 로비에서 초기화하세요.";
            return false;
        }

        _state = LobbyState.Initializing;

        if ( Init ( out reason ) == false )
        {
            //실패 반환은 내부 구독 전이므로 부분 연결만 정리
            ClearProgress ( false );
            OnStateChanged?.Invoke ( );
            return false;
        }

        _state = LobbyState.Ready;
        OnInitialized?.Invoke ( );
        OnStateChanged?.Invoke ( );
        return true;
    }

    /// <summary>
    /// 초기 설정 검사
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>사용 가능 여부</returns>
    bool ValidateSetup ( out string reason )
    {
        reason = null;

        if ( _stageSelectionData == null
            || _currency == null
            || _shopData == null
            || _itemDataMap == null
            || _weaponDataMap == null
            || _upgradeDataMap == null
            || _missionDataMap == null
            || _collectionDataMap == null )
        {
            reason = "로비 모듈과 데이터 참조를 연결하세요.";
            return false;
        }

        if ( _stageSelectionData.Validate ( out reason ) == false )
            return false;

        if ( _itemDataMap.Validate ( ) == false
            || _shopData.Validate ( _itemDataMap , _weaponDataMap ) == false
            || _upgradeDataMap.Validate ( ) == false
            || _missionDataMap.Validate ( ) == false
            || _collectionDataMap.Validate ( ) == false )
        {
            reason = "로비 데이터 검사에 실패했습니다.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 내부 알림 연결
    /// </summary>
    void BindModules ( )
    {
        _currency.OnPermanentSaveRequested += _saveService.TrySavePermanentBalance;
        _equipmentUnlock.OnChanged += HandleEquipmentChanged;
        _upgradeProgress.OnChanged += HandleUpgradeChanged;
        _missions.OnChanged += HandleMissionChanged;
        _missions.OnRewardsCommitted += HandleMissionRewardsCommitted;
        _collections.OnChanged += HandleCollectionChanged;
    }

    /// <summary>
    /// 내부 알림 해제
    /// </summary>
    void UnbindModules ( )
    {
        _currency.OnPermanentSaveRequested -= _saveService.TrySavePermanentBalance;
        _equipmentUnlock.OnChanged -= HandleEquipmentChanged;
        _upgradeProgress.OnChanged -= HandleUpgradeChanged;
        _missions.OnChanged -= HandleMissionChanged;
        _missions.OnRewardsCommitted -= HandleMissionRewardsCommitted;
        _collections.OnChanged -= HandleCollectionChanged;
    }

    /// <summary>
    /// 로비 연결 해제
    /// </summary>
    /// <param name="reason">거절 사유</param>
    /// <returns>해제 여부</returns>
    public bool TryReleaseProgress ( out string reason )
    {
        reason = null;

        if ( _state == LobbyState.Closed )
            return true;

        if ( IsInitialized == false )
        {
            reason = "로비 초기화 또는 해제 중입니다.";
            return false;
        }

        if ( _missions.IsProcessing == true )
        {
            reason = "미션 처리 중에는 해제할 수 없습니다.";
            return false;
        }

        //해제 알림 중 새 요청과 재초기화 차단
        _state = LobbyState.Closing;
        OnReleasing?.Invoke ( );

        ClearProgress ( true );
        OnStateChanged?.Invoke ( );
        return true;
    }

    /// <summary>
    /// 모델과 진행 사용권 정리
    /// </summary>
    /// <param name="unbind">구독 해제 여부</param>
    void ClearProgress ( bool unbind )
    {
        if ( unbind == true )
            UnbindModules ( );

        _shopPurchase = null;
        _upgradePurchase = null;
        _equipmentUnlock = null;
        _upgradeProgress = null;
        _inventory = null;
        _missions = null;
        _enemyQuery = null;
        _collections = null;
        _collectionUnlock = null;
        _saveService = null;
        _selectedStageIndex = -1;

        if ( _saveManager != null )
            _saveManager.DetachScene ( this );

        _saveManager = null;
        _state = LobbyState.Closed;
    }

    private void OnDestroy ( )
    {
        bool unbind = IsInitialized;
        _state = LobbyState.Closing;

        //파괴 중에는 알림이나 새 저장을 발생시키지 않음
        ClearProgress ( unbind );
    }

    #endregion

    #region ----- 스테이지 선택/입장 -----

    /// <summary>
    /// 맵 선택
    /// </summary>
    /// <param name="index">목록 번호</param>
    /// <param name="reason">거절 사유</param>
    /// <returns>선택 여부</returns>
    public bool TrySelectStage ( int index , out string reason )
    {
        reason = null;

        if ( CanProcessRequest == false )
        {
            reason = "현재 맵을 선택할 수 없습니다.";
            return false;
        }

        if ( index < 0 || index >= Stages.Count )
        {
            reason = "등록되지 않은 맵입니다.";
            return false;
        }

        if ( _selectedStageIndex == index )
            return true;

        _selectedStageIndex = index;

        //기존 난이도를 지원하면 유지하고, 미지원일 때만 기본값 선택
        if ( SelectedStage.Supports ( _selectedDifficulty ) == false )
            _selectedDifficulty = GetDefaultDifficulty ( SelectedStage );

        OnSelectionChanged?.Invoke ( );
        return true;
    }

    /// <summary>
    /// 난이도 선택
    /// </summary>
    /// <param name="difficulty">난이도</param>
    /// <param name="reason">거절 사유</param>
    /// <returns>선택 여부</returns>
    public bool TrySelectDifficulty (
        MissionDifficulty difficulty , out string reason )
    {
        reason = null;

        if ( CanProcessRequest == false )
        {
            reason = "현재 난이도를 선택할 수 없습니다.";
            return false;
        }

        if ( SelectedStage.Supports ( difficulty ) == false )
        {
            reason = "지원하지 않는 난이도입니다.";
            return false;
        }

        if ( _selectedDifficulty == difficulty )
            return true;

        _selectedDifficulty = difficulty;
        OnSelectionChanged?.Invoke ( );
        return true;
    }

    /// <summary>
    /// 스테이지의 기본 선택 난이도 조회
    /// </summary>
    /// <param name="stage">선택 스테이지</param>
    /// <returns>기본 난이도</returns>
    MissionDifficulty GetDefaultDifficulty ( LobbyStageInfo stage )
    {
        //보통을 지원하지 않는 맵은 등록된 첫 난이도 사용
        return stage.Supports ( MissionDifficulty.Normal ) == true
            ? MissionDifficulty.Normal
            : stage.Difficulties [ 0 ];
    }

    /// <summary>
    /// 선택 맵 최고 별 조회
    /// </summary>
    /// <param name="stars">최고 별</param>
    /// <returns>조회 여부</returns>
    public bool TryGetBestStars ( out int stars )
    {
        stars = 0;

        if ( CanQuery == false )
            return false;

        return _saveManager.TryGetBestStars (
            SelectedStage.MapId , _selectedDifficulty , out stars );
    }

    /// <summary>
    /// 선택 맵 최고 점수 조회
    /// </summary>
    /// <param name="hasScore">기록 여부</param>
    /// <param name="score">최고 점수</param>
    /// <returns>조회 가능 여부</returns>
    public bool TryGetBestScore ( out bool hasScore , out int score )
    {
        hasScore = false;
        score = 0;
        if ( CanQuery == false )
            return false;

        return _saveManager.TryGetBestScore (
            SelectedStage.MapId , _selectedDifficulty , out hasScore , out score );
    }

    /// <summary>
    /// 선택 맵 등장 적 조회
    /// </summary>
    /// <param name="entries">표시 목록</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>조회 여부</returns>
    public bool TryGetStageEnemies (
        out IReadOnlyList<LobbyEnemyInfo> entries , out string reason )
    {
        entries = null;
        reason = null;
        if ( CanQuery == false )
        {
            reason = "현재 로비를 조회할 수 없습니다.";
            return false;
        }

        if ( _enemyQuery == null )
        {
            reason = "등장 적 제공자와 표시 정책을 연결하세요.";
            return false;
        }

        return _enemyQuery.TryGetEntries (
            SelectedStage.MapId , _selectedDifficulty , out entries , out reason );
    }

    /// <summary>
    /// 해금된 적의 출현 스테이지 조회
    /// </summary>
    /// <param name="enemyId">도감 적 ID</param>
    /// <param name="stages">확인된 출현 스테이지</param>
    /// <param name="isComplete">전체 구성 조회 여부</param>
    /// <returns>조회에 성공한 구성이 있는지 여부</returns>
    public bool TryGetEnemyStages (
        string enemyId ,
        out IReadOnlyList<LobbyStageInfo> stages ,
        out bool isComplete )
    {
        stages = Array.Empty<LobbyStageInfo> ( );
        isComplete = false;

        if ( CanQuery == false
            || _enemySource == null
            || string.IsNullOrEmpty ( enemyId ) == true )
            return false;

        if ( _collectionDataMap.TryGet ( enemyId , out EnemyCollectionData data ) == false
            || _collections.IsUnlocked ( enemyId ) == false )
            return false;

        List<LobbyStageInfo> matches = new List<LobbyStageInfo> ( );
        bool hasCheckedConfig = false;
        isComplete = true;

        foreach ( LobbyStageInfo stage in Stages )
        {
            bool appears = false;

            foreach ( MissionDifficulty difficulty in stage.Difficulties )
            {
                //출현을 찾은 뒤에도 나머지 난이도의 조회 가능 여부 확인
                if ( _enemySource.TryGetEnemies (
                    stage.MapId , difficulty ,
                    out IReadOnlyList<EnemyData> enemies , out _ ) == false
                    || enemies == null )
                {
                    isComplete = false;
                    continue;
                }

                hasCheckedConfig = true;

                if ( enemies.Contains ( data.Enemy ) == true )
                    appears = true;
            }

            if ( appears == true )
                matches.Add ( stage );
        }

        stages = matches.AsReadOnly ( );
        return hasCheckedConfig;
    }

    /// <summary>
    /// 입장 선택 확정
    /// </summary>
    /// <param name="selection">확정한 선택</param>
    /// <returns>준비 결과</returns>
    public EntryPreparationResult TryPrepareEntry ( out StageEntrySelection selection )
    {
        selection = default;

        if ( CanProcessRequest == false )
            return EntryPreparationResult.Unavailable;

        WeaponType? selectedWeapon = _equipmentUnlock.SelectedWeapon;

        if ( selectedWeapon.HasValue == false )
            return EntryPreparationResult.WeaponNotSelected;

        if ( SavePendingMissions ( ) == false )
        {
            OnStateChanged?.Invoke ( );
            return EntryPreparationResult.SaveFailed;
        }

        selection = new StageEntrySelection (
            SelectedStage.MapId , _selectedDifficulty , selectedWeapon.Value );

        _state = LobbyState.Suspended;
        OnStateChanged?.Invoke ( );
        return EntryPreparationResult.Success;
    }

    /// <summary>
    /// 요청 재개
    /// </summary>
    /// <returns>전환 여부</returns>
    public bool ResumeRequests ( )
    {
        if ( _state != LobbyState.Suspended )
            return false;

        _state = LobbyState.Ready;
        OnStateChanged?.Invoke ( );
        return true;
    }

    #endregion

    #region ----- 상점/강화 -----

    /// <summary>
    /// 상품 상태 조회
    /// </summary>
    /// <param name="productId">상품 ID</param>
    /// <param name="price">가격</param>
    /// <returns>구매 상태</returns>
    public ShopPurchaseResult GetProductPurchaseState (
        string productId , out int price )
    {
        price = 0;

        if ( CanQuery == false )
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
        if ( CanProcessRequest == false )
            return ShopPurchaseResult.Unavailable;

        return _shopPurchase.TryPurchaseProduct ( productId );
    }

    /// <summary>
    /// 무기 선택
    /// </summary>
    /// <param name="weaponType">무기 타입</param>
    /// <returns>선택 결과</returns>
    public ShopPurchaseResult TrySelectWeapon ( WeaponType weaponType )
    {
        if ( CanProcessRequest == false )
            return ShopPurchaseResult.Unavailable;

        return _equipmentUnlock.TrySelectWeapon ( weaponType );
    }

    /// <summary>
    /// 무기 보유 조회
    /// </summary>
    /// <param name="weaponType">무기 타입</param>
    /// <returns>보유 여부</returns>
    public bool IsWeaponUnlocked ( WeaponType weaponType )
    {
        return _equipmentUnlock.IsWeaponUnlocked ( weaponType );
    }

    /// <summary>
    /// 설치물 보유 조회
    /// </summary>
    /// <param name="installationId">설치물 ID</param>
    /// <returns>보유 여부</returns>
    public bool IsInstallationUnlocked ( string installationId )
    {
        return _equipmentUnlock.IsInstallationUnlocked ( installationId );
    }

    /// <summary>
    /// 강화 상태 조회
    /// </summary>
    /// <param name="upgradeId">강화 ID</param>
    /// <returns>현재 상태</returns>
    public UpgradeState GetUpgradeState ( string upgradeId )
    {
        if ( CanQuery == false )
            return new UpgradeState ( UpgradePurchaseResult.Unavailable );

        return _upgradePurchase.GetUpgradeState ( upgradeId );
    }

    /// <summary>
    /// 강화 구매
    /// </summary>
    /// <param name="upgradeId">강화 ID</param>
    /// <returns>구매 결과</returns>
    public UpgradePurchaseResult TryUpgrade ( string upgradeId )
    {
        if ( CanProcessRequest == false )
            return UpgradePurchaseResult.Unavailable;

        return _upgradePurchase.TryUpgrade ( upgradeId );
    }

    /// <summary>
    /// 플레이어 강화 미리보기
    /// </summary>
    /// <param name="type">무기 타입</param>
    /// <param name="stats">강화 증가량</param>
    /// <returns>조회 여부</returns>
    public bool TryGetHeroUpgradePreview (
        WeaponType type , out HeroUpgradeStats stats )
    {
        stats = default;

        if ( CanQuery == false )
            return false;

        PlayerUpgradeCalculator.Calculate (
            _upgradeDataMap , _upgradeProgress ,
            out float maxHpBonus , out float moveSpeedBonus );

        WeaponUpgradeCalculator.Calculate (
            _upgradeDataMap , _upgradeProgress , type ,
            out float damageBonus , out float attackSpeedBonus );

        stats = new HeroUpgradeStats (
            maxHpBonus , moveSpeedBonus , damageBonus , attackSpeedBonus );
        return true;
    }

    /// <summary>
    /// 설치물 강화 미리보기
    /// </summary>
    /// <param name="data">설치물 설정</param>
    /// <param name="stats">강화 증가량</param>
    /// <returns>조회 여부</returns>
    public bool TryGetInstallationUpgradePreview (
        TrapData data , out InstallationUpgradeStats stats )
    {
        stats = default;

        if ( CanQuery == false )
            return false;

        stats = InstallationUpgradeCalculator
            .Calculate ( _upgradeDataMap , _upgradeProgress , data );
        return true;
    }

    /// <summary>
    /// 장비 변경 전달
    /// </summary>
    void HandleEquipmentChanged ( )
    {
        OnEquipmentChanged?.Invoke ( );
    }

    /// <summary>
    /// 강화 변경 전달
    /// </summary>
    void HandleUpgradeChanged ( )
    {
        OnUpgradeChanged?.Invoke ( );
    }

    #endregion

    #region ----- 미션/도감 -----

    /// <summary>
    /// 미션 상태 조회
    /// </summary>
    /// <param name="missionId">미션 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="state">미션 상태</param>
    /// <returns>조회 여부</returns>
    public bool TryGetMissionState (
        string missionId ,
        MissionDifficulty difficulty ,
        out MissionState state )
    {
        state = default;

        if ( CanQuery == false )
            return false;

        return _missions.TryGetState ( missionId , difficulty , out state );
    }

    /// <summary>
    /// 도감 신규 표시 확인
    /// </summary>
    /// <param name="source">화면의 도감 기록</param>
    /// <param name="enemyIds">확인한 항목</param>
    /// <returns>저장 여부</returns>
    public bool TryMarkCollectionSeen (
        CollectionProgress source , IReadOnlyList<string> enemyIds )
    {
        //재연결 전 화면에서 보낸 요청 구분
        if ( CanProcessRequest == false || source != _collections )
            return false;

        return _collectionUnlock.TryMarkSeen ( enemyIds );
    }

    /// <summary>
    /// 미저장 진행 처리
    /// </summary>
    /// <returns>저장 여부</returns>
    public bool TrySavePendingProgress ( )
    {
        if ( CanProcessRequest == false )
            return false;

        bool saved = SavePendingMissions ( );

        //미션 처리 종료 후 요청 가능 상태 재조회
        OnStateChanged?.Invoke ( );
        return saved;
    }

    /// <summary>
    /// 미저장 미션 저장
    /// </summary>
    /// <returns>저장 여부</returns>
    bool SavePendingMissions ( )
    {
        return _missions.HasPendingSave == false || _missions.TrySaveProgress ( );
    }

    /// <summary>
    /// 미션 변경 전달
    /// </summary>
    void HandleMissionChanged ( )
    {
        OnMissionChanged?.Invoke ( );
    }

    /// <summary>
    /// 미션 보상 전달
    /// </summary>
    /// <param name="stageId">도전 번호</param>
    /// <param name="rewards">보상 목록</param>
    /// <param name="amount">보상 합계</param>
    void HandleMissionRewardsCommitted (
        int stageId , IReadOnlyList<MissionReward> rewards , int amount )
    {
        OnMissionRewardsCommitted?.Invoke ( stageId , rewards , amount );
    }

    /// <summary>
    /// 도감 변경 전달
    /// </summary>
    void HandleCollectionChanged ( )
    {
        OnCollectionChanged?.Invoke ( );
    }

    #endregion
}
