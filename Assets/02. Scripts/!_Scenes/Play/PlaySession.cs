using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이 도전 연결부 - 도전 시작/구간/사건/종료 조율
/// </summary>
public class PlaySession
{
    MonoBehaviour _owner;		//현재 실행을 소유한 씬
    Hero _hero;		//플레이어
    Currency _currency;		//현재 재화
    StageRewardController _rewards;		//처치 보상과 도전 번호
    CompanionHireController _hireController;		//동료 고용 관리
    EquipmentUnlock _equipmentUnlock;		//현재 슬롯의 해금 상태
    SaveService _saveService;		//현재 진행의 저장 서비스
    VfxManager _vfx;		//도전 시작과 종료의 효과 정리

    ItemInventory _inventory;		//아이템 사용 기록
    HeroItemUse _itemUse;		//사용 성공 사건
    WeaponDataMap _weaponDataMap;		//무기 설정
    UpgradeDataMap _upgradeDataMap;		//강화 설정
    UpgradeProgress _upgradeProgress;		//현재 강화 진행

    MissionController _missions;		//미션 처리
    EnemyCollectionDataMap _collectionDataMap;		//도감 설정
    CollectionUnlock _collectionUnlock;		//도감 해금 처리
    Action<bool> _setCursorLocked;		//씬 커서와 입력 정리
    Action _clearInstallationSelection;		//씬 설치 선택 해제

    InstallationUpgradeSnapshot _stageInstallationUpgrades;		//도전별 설치물 강화 결과
    int _lastStageUsedItemCount;		//마지막 정산 도전의 아이템 사용 횟수
    List<EnemyCollectionData> _endCollectionTargets;		//종료 접수에서 매핑한 도감 대상
    MissionStageResult _endResult;		//접수한 최종 결과

    int _endScore;		//이번 클리어의 확정 점수
    ClearRewardData _clearRewardData;    //종료 보상 설정
    int _stageTargetScore;    //도전 시작 때 확보한 기준 점수
    int _stagePaidStars;    //도입 전 정책을 적용한 별 지급 단계

    string _settlementId;    //저장용 정산 식별자
    ClearRewardPlan _clearRewardPlan;    //재시도에서 유지할 계산 결과
    ClearRewardResult _clearRewardResult;    //원래 도전의 지급 상태
    PlayEndFailure _lastEndFailure;		//마지막 종료 실패 위치

    bool _endEventsCompleted;		//마지막 사건 전달 완료 여부
    bool _endProcessRequested;		//종료 처리/재시도 요청 여부
    bool _isStarting;		//도전 시작 처리 중 여부
    bool _isStageReady;		//도전 준비 완료 여부

    int _endingStageId;		//종료 예약 도전 번호
    int _cleanupStageId;		//정산 후 정리 대기 도전 번호
    bool _abandonRequested;		//미지급 스테이지 보상 폐기 여부

    int _savedWave;      //마지막 저장 완료 일반 웨이브
    int _pendingWave;    //저장 재시도가 필요한 일반 웨이브

    #region ----- 프로퍼티/이벤트 -----

    /// <summary>
    /// 도전 시작 처리 중 여부
    /// </summary>
    public bool IsStarting => _isStarting;

    /// <summary>
    /// 도전 준비 완료 여부
    /// </summary>
    public bool IsStageReady => _isStageReady;

    /// <summary>
    /// 종료 예약 또는 정리 대기 여부
    /// </summary>
    public bool IsEndingStage => _endingStageId != 0 || _cleanupStageId != 0;

    /// <summary>
    /// 정산 후 정리 대기 여부
    /// </summary>
    public bool IsStageCleanupPending => _cleanupStageId != 0;

    /// <summary>
    /// 마지막 사건 전달 대기 여부
    /// </summary>
    public bool IsAwaitingEndEvents => _endingStageId != 0 && _endEventsCompleted == false;

    /// <summary>
    /// 마지막 정산 도전의 아이템 사용 횟수
    /// </summary>
    public int LastStageUsedItemCount => _lastStageUsedItemCount;

    /// <summary>
    /// 진행 중이거나 종료 처리 중인 도전 번호
    /// </summary>
    public int CurrentStageId
    {
        get
        {
            if ( _endingStageId != 0 )
                return _endingStageId;

            if ( _cleanupStageId != 0 )
                return _cleanupStageId;

            return _isStageReady ? _rewards.CurrentStageId : 0;
        }
    }

    /// <summary>
    /// 마지막 종료 실패 위치
    /// </summary>
    public PlayEndFailure LastEndFailure => _lastEndFailure;

    /// <summary>
    /// 저장 대기 중인 일반 웨이브, 없으면 0
    /// </summary>
    public int PendingWave => _pendingWave;

    /// <summary>
    /// 종료 처리 결과(도전 번호, 미지급 재화 폐기 여부, 정리 완료 여부)
    /// </summary>
    public event Action<int , bool , bool> OnStageEndProcessed;

    #endregion

    #region ----- 생성/해제 -----

    /// <summary>
    /// 도전 모듈 연결과 사건 구독
    /// </summary>
    /// <param name="owner">실행 씬</param>
    /// <param name="hero">플레이어</param>
    /// <param name="currency">재화</param>
    /// <param name="rewards">처치 보상</param>
    /// <param name="hireController">동료 고용</param>
    /// <param name="inventory">아이템 인벤토리</param>
    /// <param name="itemUse">아이템 사용 처리</param>
    /// <param name="weaponDataMap">무기 설정</param>
    /// <param name="upgradeDataMap">강화 설정</param>
    /// <param name="upgradeProgress">강화 진행도</param>
    /// <param name="missions">미션 처리</param>
    /// <param name="collectionDataMap">도감 설정</param>
    /// <param name="collectionUnlock">도감 해금 처리</param>
    /// <param name="setCursorLocked">커서 잠금 콜백</param>
    /// <param name="clearInstallationSelection">설치물 선택 해제 콜백</param>
    /// <param name="lastStageUsedItemCount">마지막 정산 아이템 사용 횟수</param>
    /// <param name="equipmentUnlock">해금 상태</param>
    /// <param name="saveService">공유 저장 서비스</param>
    /// <param name="vfx">씬 VFX 서비스</param>
    /// <param name="clearRewardData">종료 보상 설정</param>
    public PlaySession (
        MonoBehaviour owner , Hero hero , Currency currency ,
        StageRewardController rewards , CompanionHireController hireController ,
        ItemInventory inventory , HeroItemUse itemUse ,
        WeaponDataMap weaponDataMap , UpgradeDataMap upgradeDataMap ,
        UpgradeProgress upgradeProgress , MissionController missions ,
        EnemyCollectionDataMap collectionDataMap , CollectionUnlock collectionUnlock ,
        Action<bool> setCursorLocked , Action clearInstallationSelection ,
        int lastStageUsedItemCount , EquipmentUnlock equipmentUnlock ,
        SaveService saveService , VfxManager vfx , ClearRewardData clearRewardData )
    {
        _owner = owner;
        _hero = hero;
        _currency = currency;
        _rewards = rewards;
        _hireController = hireController;
        _inventory = inventory;
        _itemUse = itemUse;
        _weaponDataMap = weaponDataMap;
        _upgradeDataMap = upgradeDataMap;
        _upgradeProgress = upgradeProgress;
        _missions = missions;
        _collectionDataMap = collectionDataMap;
        _collectionUnlock = collectionUnlock;
        _setCursorLocked = setCursorLocked;
        _clearInstallationSelection = clearInstallationSelection;
        _lastStageUsedItemCount = lastStageUsedItemCount;
        _equipmentUnlock = equipmentUnlock;
        _saveService = saveService;
        _vfx = vfx;
        _clearRewardData = clearRewardData;

        BindMission ( );
    }

    /// <summary>
    /// 미션 사건 구독 해제
    /// </summary>
    public void Dispose ( )
    {
        UnbindMission ( );
    }

    #endregion

    #region ----- 도전 준비/진행 -----

    /// <summary>
    /// 도전 시작
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
        //요청 가능 불가, 시작할 수 없는 상태면
        if ( CanProcessLobbyRequest ( ) == false || _missions.CanBeginStage == false )
            return false;

        //무기가 해금이 안 되어 있으면
        if ( _equipmentUnlock.IsWeaponUnlocked ( weaponType ) == false )
            return false;

        //맵 아이디, 난이도, 맵 방어 조건 확인
        if ( string.IsNullOrWhiteSpace ( mapId ) == true
            || difficulty < MissionDifficulty.Easy
            || difficulty > MissionDifficulty.Hard
            || ( defenseRule & ~( MissionDefenseRule.NoPass | MissionDefenseRule.NoDamage ) ) != 0 )
            return false;

        _isStarting = true;

        bool started = BeginStageCore (
            weaponType , initialStageCurrency ,
            mapId , difficulty , defenseRule ,
            companionPriorityPosition );

        _isStarting = false;

        if ( started == true )
        {
            //전체 시작 처리가 끝난 뒤에만 요청과 가능 상태 알림 허용
            _hireController.SetHireEnabled ( true );
            _inventory.NotifyChanged ( );
        }

        return started;
    }

    /// <summary>
    /// 도전 준비
    /// </summary>
    /// <param name="weaponType">선택 무기</param>
    /// <param name="initialStageCurrency">시작 스테이지 재화</param>
    /// <param name="mapId">맵 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="defenseRule">완전 방어 조건</param>
    /// <param name="companionPriorityPosition">현재 맵의 동료 공격 우선순위 기준</param>
    /// <returns>준비 성공 여부</returns>
    bool BeginStageCore (
        WeaponType weaponType ,
        int initialStageCurrency ,
        string mapId ,
        MissionDifficulty difficulty ,
        MissionDefenseRule defenseRule ,
        Vector3? companionPriorityPosition )
    {
        //플레이어와 재화 변경 전에 이번 도전의 보상 설정 확인
        if ( _clearRewardData.TryGetTargetScore ( mapId , difficulty , out int targetScore ) == false )
        {
            Debug.LogError ( $"보상 기준 점수가 없습니다: 맵={mapId}, 난이도={difficulty}" , _owner );
            return false;
        }

        //보상 지급 단계 확인
        int paidStars = _saveService.GetPaidStars ( mapId , difficulty , _clearRewardData.LegacyStars );

        //무기 가져오기
        if ( _weaponDataMap.TryGet ( weaponType , out WeaponData weaponData ) == false )
        {
            Debug.LogError (
                $"무기 데이터가 없거나 목록 구성이 잘못됐습니다: {weaponType}" , _owner );
            return false;
        }

        //이전 도전의 단발 잔여 효과도 새 초기화 전에 정리
        _vfx.StopAll ( VfxScope.Gameplay );

        //플레이어 초기화 전에 도전별 강화 결과 계산
        HeroUpgradeStats heroStats = CalculateHeroUpgrades ( weaponData.WeaponType );
        CompanionUpgradeSnapshot companionUpgrades =
            new CompanionUpgradeSnapshot ( _upgradeDataMap , _upgradeProgress );
        InstallationUpgradeSnapshot installationUpgrades =
            new InstallationUpgradeSnapshot ( _upgradeDataMap , _upgradeProgress );

        //원본 대신 도전별 강화 결과 적용
        if ( _hero.Init ( weaponData , heroStats ) == false
            || _hero.ResetStageResources ( ) == false )
        {
            _hero.StopStageActions ( );
            return false;
        }

        if ( _rewards.BeginStage ( initialStageCurrency ) == false )
        {
            _hero.StopStageActions ( );
            return false;
        }

        int stageId = _rewards.CurrentStageId;

        if ( _hireController.BeginStage (
            stageId , companionUpgrades , companionPriorityPosition ) == false )
        {
            _hero.StopStageActions ( );

            //고용 연결에 실패한 이번 도전만 정리
            if ( _rewards.AbandonStage ( stageId ) == false )
                Debug.LogError ( "고용 연결 실패 후 재화 도전 정리에 실패했습니다." , _owner );

            return false;
        }

        _missions.BeginStage ( new MissionStageContext ( stageId , mapId , difficulty , defenseRule ) );

        //입장 준비 성공 후 이전 결과 초기화
        _collectionUnlock.BeginStage ( stageId );
        _savedWave = 0;
        _pendingWave = 0;
        _endResult = default;
        _endScore = 0;
        _stageTargetScore = targetScore;
        _stagePaidStars = paidStars;
        _settlementId = Guid.NewGuid ( ).ToString ( "N" );
        _clearRewardPlan = null;
        _clearRewardResult = new ClearRewardResult ( stageId , false , 0 , ClearRewardState.Pending );
        _lastEndFailure = PlayEndFailure.None;
        _endCollectionTargets = null;
        _endEventsCompleted = false;
        _endProcessRequested = false;

        //새 도전의 사용 기록/쿨타임 초기화
        _inventory.ResetStageState ( );

        _hero.ResetReviveCount ( );
        _stageInstallationUpgrades = installationUpgrades;

        //요청 허용 전에 이전 선택 해제
        _clearInstallationSelection ( );
        _isStageReady = true;
        _setCursorLocked ( true );

        return true;
    }

    /// <summary>
    /// 플레이어/무기 강화 증가량 계산
    /// </summary>
    /// <param name="type">무기 타입</param>
    /// <returns>플레이어/무기 강화 증가량</returns>
    public HeroUpgradeStats CalculateHeroUpgrades ( WeaponType type )
    {
        PlayerUpgradeCalculator.Calculate (
            _upgradeDataMap , _upgradeProgress ,
            out float maxHpBonus , out float moveSpeedBonus );

        WeaponUpgradeCalculator.Calculate (
            _upgradeDataMap , _upgradeProgress , type ,
            out float damageBonus , out float attackSpeedBonus );

        return new HeroUpgradeStats (
            maxHpBonus , moveSpeedBonus , damageBonus , attackSpeedBonus );
    }

    /// <summary>
    /// 첫 전투 시작
    /// </summary>
    /// <param name="result">전투 시작 결과</param>
    /// <returns>전투 시작 성공 여부</returns>
    public bool BeginCombat ( out CompanionHireResult result )
    {
        result = CompanionHireResult.Unavailable;

        if ( CanProcessStageRequest ( ) == false
            || _missions.CanChangePhase ( _rewards.CurrentStageId ) == false )
            return false;

        //동료 전투 시작
        if ( _hireController.BeginCombat ( _rewards.CurrentStageId ,
            out result , out IReadOnlyList<int> participantIds ) == false )
            return false;

        //표시 알림 전에 구간/참여 확정
        _missions.ApplyPlayPhase ( participantIds );
        _hireController.NotifyEntriesChanged ( );
        return true;
    }

    /// <summary>
    /// 준비/전투 구간 전환
    /// </summary>
    /// <param name="stageId">도전 번호</param>
    /// <param name="isPlaying">전투 구간 여부</param>
    /// <returns>구간 전환 성공 여부</returns>
    public bool SetPlayPhase ( int stageId , bool isPlaying )
    {
        if ( CanProcessStageRequest ( ) == false
            || _hireController.IsPreparing == true
            || _missions.CanChangePhase ( stageId ) == false )
            return false;

        //준비 전환은 기존 참여 기록 유지
        if ( isPlaying == true )
            _missions.ApplyPlayPhase ( _hireController.GetActiveCompanionIds ( ) );
        else
            _missions.ApplyPreparationPhase ( );

        return true;
    }

    /// <summary>
    /// 도전별 설치물 강화 결과 조회
    /// </summary>
    /// <param name="stageId">도전 번호</param>
    /// <param name="data">설치물 설정</param>
    /// <param name="stats">강화 증가량</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetStageInstallationStats (
        int stageId ,
        TrapData data ,
        out InstallationUpgradeStats stats )
    {
        stats = default;

        if ( CanProcessStageRequest ( ) == false
            || stageId != _rewards.CurrentStageId )
            return false;

        stats = _stageInstallationUpgrades.GetStats ( data );
        return true;
    }

    #endregion

    #region ----- 미션 사건 -----

    /// <summary>
    /// 미션 처치 등록
    /// </summary>
    /// <param name="kill">처치 정보</param>
    /// <returns>처치 처리 결과</returns>
    public MissionKillResult RegisterMissionKill ( MissionKillContext kill )
    {
        //비활성화 중에도 확정 사건 수신
        if ( _isStarting == true || _endEventsCompleted == true )
            return MissionKillResult.Rejected;

        return _missions.RegisterKill ( kill );
    }

    /// <summary>
    /// 미션 기록 사건 구독
    /// </summary>
    void BindMission ( )
    {
        _itemUse.OnUseCommitted += HandleItemUseCommitted;
        _hero.OnDied += HandleMissionPlayerDied;
        _hireController.OnParticipationConfirmed += HandleMissionParticipation;
    }

    /// <summary>
    /// 미션 기록 사건 구독 해제
    /// </summary>
    void UnbindMission ( )
    {
        _itemUse.OnUseCommitted -= HandleItemUseCommitted;
        _hero.OnDied -= HandleMissionPlayerDied;
        _hireController.OnParticipationConfirmed -= HandleMissionParticipation;
    }

    /// <summary>
    /// 아이템 사용 횟수 기록
    /// </summary>
    /// <param name="itemId">아이템 ID</param>
    void HandleItemUseCommitted ( string itemId )
    {
        if ( _endEventsCompleted == false )
            _missions.RecordItemUse ( _rewards.CurrentStageId );
    }

    /// <summary>
    /// 플레이어 사망 기록
    /// </summary>
    /// <param name="damageInfo">사망 피해 정보</param>
    void HandleMissionPlayerDied ( DamageInfo damageInfo )
    {
        if ( _endEventsCompleted == false )
            _missions.RecordPlayerDeath ( _rewards.CurrentStageId );
    }

    /// <summary>
    /// 동료 전투 참여 기록
    /// </summary>
    /// <param name="stageId">도전 번호</param>
    /// <param name="hireId">고용 번호</param>
    void HandleMissionParticipation ( int stageId , int hireId )
    {
        if ( _endEventsCompleted == false )
            _missions.RecordCompanionParticipation ( stageId , hireId );
    }

    #endregion

    #region ----- 진행 저장 -----

    /// <summary>
    /// 미저장 미션을 우선 처리하고 진행 저장
    /// </summary>
    /// <param name="saveConfirmedData">변경이 없어도 저장할지 여부</param>
    /// <returns>저장 성공 여부</returns>
    public bool TrySaveProgress ( bool saveConfirmedData )
    {
        //호출 조건과 요청 차단은 PlayScene에서 처리
        bool saved;

        if ( _missions.HasPendingSave == true )
        {
            //미션 저장 후보에 최신 확정 진행도 포함
            saved = _missions.TrySaveProgress ( );
        }
        else
        {
            saved = saveConfirmedData == false
                || _saveService.TrySaveCurrent ( ) == SaveResult.Success;
        }

        if ( saved == false )
            return false;

        //일반 저장으로 복구한 경우에도 웨이브 대기 해제
        if ( _pendingWave != 0 )
        {
            _savedWave = _pendingWave;
            _pendingWave = 0;
        }

        return true;
    }

    /// <summary>
    /// 일반 웨이브 완료 진행 저장
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    /// <param name="waveNumber">완료 웨이브 번호</param>
    /// <returns>저장 완료 여부</returns>
    public bool TrySaveWave ( int stageId , int waveNumber )
    {
        if ( CanProcessStageRequest ( ) == false
            || stageId != _rewards.CurrentStageId
            || _missions.IsPlaying == true
            || waveNumber <= 0 )
            return false;

        //이미 저장한 완료 요청은 파일을 다시 쓰지 않음
        if ( waveNumber <= _savedWave )
            return true;

        //이전 저장을 건너뛰지 않고 실패한 같은 번호는 재시도
        if ( waveNumber != _savedWave + 1 )
            return false;

        _pendingWave = waveNumber;

        //처치 재전달 없이 현재 진행만 저장
        return TrySaveProgress ( true );
    }

    #endregion

    #region ----- 도전 종료 -----

    /// <summary>
    /// 호환 도전 종료 요청
    /// </summary>
    /// <param name="stageId">도전 번호</param>
    /// <param name="abandon">미지급 재화 폐기 여부</param>
    /// <returns>요청 접수 여부</returns>
    public bool RequestEndStage ( int stageId , bool abandon )
    {
        //호환 종료 요청은 승리로 처리하지 않음
        MissionStageResult result = new MissionStageResult (
            stageId ,
            abandon == true ? MissionOutcome.Abandoned : MissionOutcome.Defeat ,
            0 , false , false , "legacy_end_request" , 0 );

        return RequestEndStage ( result , abandon ) && CompleteEndEvents ( stageId );
    }

    /// <summary>
    /// 패배/포기 종료 예약
    /// </summary>
    /// <param name="result">종료 결과</param>
    /// <param name="discardPendingCurrency">미지급 재화 폐기 여부</param>
    /// <returns>예약 접수 여부</returns>
    public bool RequestEndStage (
        MissionStageResult result ,
        bool discardPendingCurrency )
    {
        return RequestEndStage ( result , discardPendingCurrency , null );
    }

    /// <summary>
    /// 도전 종료 예약
    /// </summary>
    /// <param name="result">종료 결과</param>
    /// <param name="discardPendingCurrency">미지급 재화 폐기 여부</param>
    /// <param name="collectionEnemies">도감 대상 적 목록</param>
    /// <returns>예약 접수 여부</returns>
    public bool RequestEndStage (
        MissionStageResult result ,
        bool discardPendingCurrency ,
        IReadOnlyList<EnemyData> collectionEnemies )
    {
        //진행 중 도전의 첫 종료만 접수
        if ( _owner.isActiveAndEnabled == false || _isStageReady == false || _endingStageId != 0 )
            return false;

        if ( result.StageId != _rewards.CurrentStageId )
        {
            Debug.LogError ( $"종료 요청의 도전 번호가 다릅니다: 요청={result.StageId}"
                + $", 현재={_rewards.CurrentStageId}" , _owner );
            return false;
        }

        if ( result.PassedCount < 0 )
        {
            Debug.LogError ( $"최종 통과 수는 음수일 수 없습니다: {result.PassedCount}" , _owner );
            return false;
        }

        //승패/별 조합은 종료 요청 경계에서 한 번 검사
        bool validStars = result.Outcome switch
        {
            MissionOutcome.Victory =>
                result.StarCount >= 1 && result.StarCount <= 3,
            MissionOutcome.Defeat or MissionOutcome.Abandoned =>
                result.StarCount == 0,
            _ => false
        };

        if ( validStars == false )
        {
            Debug.LogError ( $"종료 결과의 승패/별 조합이 잘못됐습니다:"
                + $" 승패={result.Outcome}, 별={result.StarCount}" , _owner );
            return false;
        }

        List<EnemyCollectionData> collectionTargets = null;

        if ( result.Outcome == MissionOutcome.Victory
            && TryPrepareCollectionTargets ( collectionEnemies , out collectionTargets ) == false )
            return false;

        //도감 대상 확인 후 종료 예약
        _endCollectionTargets = collectionTargets;
        _endingStageId = result.StageId;
        _endResult = result;
        _abandonRequested = discardPendingCurrency;

        //새 고용 차단, 마지막 사건 수신 유지
        _hireController.SetHireEnabled ( false );

        return true;
    }

    /// <summary>
    /// 도감 대상 매핑
    /// </summary>
    /// <param name="enemies">적 목록</param>
    /// <param name="targets">도감 항목 목록</param>
    /// <returns>매핑 성공 여부</returns>
    bool TryPrepareCollectionTargets (
        IReadOnlyList<EnemyData> enemies ,
        out List<EnemyCollectionData> targets )
    {
        targets = null;

        if ( enemies == null )
        {
            Debug.LogError ( "승리 결과에 도감 대상 목록이 없습니다. 대상이 없는 승리는 빈 목록을 전달하세요." , _owner );
            return false;
        }

        List<EnemyCollectionData> mapped = new List<EnemyCollectionData> ( enemies.Count );

        for ( int i = 0 ; i < enemies.Count ; i++ )
        {
            EnemyData enemy = enemies [ i ];

            if ( enemy == null )
            {
                Debug.LogError ( $"도감 대상의 {i}번 원본 적이 비어 있습니다." , _owner );
                return false;
            }

            if ( _collectionDataMap.TryGet ( enemy , out EnemyCollectionData data ) == false )
            {
                Debug.LogError ( $"도감에 등록되지 않은 적입니다: 항목={i}, 적={enemy.name}" , _owner );
                return false;
            }

            //중복 해금은 CollectionUnlock에서 처리
            mapped.Add ( data );
        }

        targets = mapped;

        return true;
    }

    /// <summary>
    /// 사건 전달 완료 및 정산 예약
    /// </summary>
    /// <param name="stageId">도전 번호</param>
    /// <returns>예약 접수 여부</returns>
    public bool CompleteEndEvents ( int stageId )
    {
        return CompleteEndEventsCore ( stageId , false , 0 );
    }

    /// <summary>
    /// 클리어 사건 전달 완료
    /// </summary>
    /// <param name="stageId">도전</param>
    /// <param name="score">확정 점수</param>
    /// <returns>접수 여부</returns>
    public bool CompleteEndEvents ( int stageId , int score )
    {
        return CompleteEndEventsCore ( stageId , true , score );
    }

    /// <summary>
    /// 종료 기록 확정과 정산 예약
    /// </summary>
    /// <param name="stageId">도전</param>
    /// <param name="hasScore">점수 전달 여부</param>
    /// <param name="score">확정 점수</param>
    /// <returns>접수 여부</returns>
    bool CompleteEndEventsCore ( int stageId , bool hasScore , int score )
    {
        if ( _endingStageId == 0
            || stageId != _endingStageId
            || _endEventsCompleted == true
            || _missions.IsProcessing == true )
            return false;

        bool isVictory = _endResult.Outcome == MissionOutcome.Victory;
        if ( isVictory != hasScore )
            return false;

        if ( hasScore == true && score < 0 )
            return false;

        //마지막 사건 처리 이후 받은 점수를 저장 재시도에서도 유지
        _endScore = score;
        _clearRewardResult = new ClearRewardResult ( stageId , hasScore , score ,
            _endResult.Outcome == MissionOutcome.Abandoned
                ? ClearRewardState.NotEligible : ClearRewardState.Pending );
        _endEventsCompleted = true;
        _endProcessRequested = true;

        //행동 취소 알림 전에 사건 수신 차단
        //기록 고정/정산은 다음 종료 처리에서 수행
        _hero.StopStageActions ( );
        _hireController.StopStageActions ( );

        //실행 무효화 후 유지 효과만 멈추고 마지막 단발 효과는 자연 종료
        _vfx.StopMaintained ( VfxScope.Gameplay );
        _setCursorLocked ( false );

        return true;
    }

    /// <summary>
    /// 종료 재시도 예약
    /// </summary>
    /// <returns>예약 접수 여부</returns>
    public bool RetryEndStage ( )
    {
        if ( _owner.isActiveAndEnabled == false || _endingStageId == 0
            || _endEventsCompleted == false || _missions.IsProcessing == true )
            return false;

        _endProcessRequested = true;

        return true;
    }

    /// <summary>
    /// 예약된 도전 정산 및 정리
    /// </summary>
    public void ProcessPendingEnd ( )
    {
        if ( _endingStageId == 0 || _endProcessRequested == false )
            return;

        _lastEndFailure = PlayEndFailure.None;
        _endProcessRequested = false;
        int stageId = _endingStageId;
        bool abandon = _abandonRequested;

        //실패 시 종료 상태를 유지하고 명시적으로 재시도
        if ( _missions.TryFinalizeStage ( _endResult ) == false )
        {
            NotifyEndFailure ( stageId , PlayEndFailure.Mission );
            return;
        }

        //미션 확정 후 승리 도감 처리, 이어서 최고 기록과 보상 저장
        if ( _endResult.Outcome == MissionOutcome.Victory )
        {
            if ( _collectionUnlock.TryUnlock ( _endCollectionTargets ) == false )
            {
                NotifyEndFailure ( stageId , PlayEndFailure.Collection );
                return;
            }
        }

        //포기는 지급 제외, 저장 성공한 보상은 뒤쪽 재화 정산 재시도에서 생략
        if ( _endResult.Outcome != MissionOutcome.Abandoned
            && _clearRewardResult.State != ClearRewardState.Committed )
        {
            if ( TryPrepareClearReward ( ) == false )
            {
                NotifyEndFailure ( stageId , PlayEndFailure.RewardCalculation );
                return;
            }
            if ( _saveService.TrySaveClearReward ( _clearRewardPlan , out ClearRewardCommit commit ) == false )
            {
                NotifyEndFailure ( stageId , PlayEndFailure.RewardSave );
                return;
            }

            //잔액 변경 알림에서 조회할 지급 결과부터 확정
            _clearRewardResult = new ClearRewardResult ( stageId , commit.Record , ClearRewardState.Committed );
            _currency.ApplySavedPermanentBalance ( commit.SavedBalance );
        }

        //미션/도감/클리어 기록 확정 후 재화 정산
        bool settled = abandon == true
            ? _rewards.AbandonStage ( stageId )
            : _rewards.EndStage ( stageId );

        if ( settled == false )
        {
            NotifyEndFailure ( stageId , PlayEndFailure.Currency );
            return;
        }

        //정산 상태 확정 후 객체 정리
        _cleanupStageId = stageId;
        _isStageReady = false;
        _clearInstallationSelection ( );
        _lastStageUsedItemCount = _inventory.UsedItemCount;

        //정리 실패는 재정산 없이 재시도
        _endingStageId = 0;
        TryCompleteStageCleanup ( );
    }

    /// <summary>
    /// 재시도에서 유지할 보상 계획 준비
    /// </summary>
    /// <returns>계산 성공 여부</returns>
    bool TryPrepareClearReward ( )
    {
        if ( _clearRewardPlan != null )
            return true;

        //저장 실패 시 기존 계산 계획으로 재시도
        ClearRewardInput input = new ClearRewardInput (
            _missions.StageContext , _endResult , _endScore , _stageTargetScore , _stagePaidStars );
        if ( ClearRewardCalculator.TryCalculate ( _clearRewardData , input , out ClearRewardAmounts amounts ) == false )
        {
            Debug.LogError ( "종료 보상 계산 금액이 정수 범위를 넘습니다." , _owner );
            return false;
        }
        _clearRewardPlan = new ClearRewardPlan ( _settlementId , input , amounts );
        _clearRewardResult = new ClearRewardResult (
            _endResult.StageId , new ClearRewardRecord ( _clearRewardPlan ) , ClearRewardState.Prepared );
        return true;
    }

    /// <summary>
    /// 원래 도전의 보상 결과 조회
    /// </summary>
    /// <param name="stageId">도전 번호</param>
    /// <param name="result">계산/지급 결과</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetClearReward ( int stageId , out ClearRewardResult result )
    {
        result = _clearRewardResult;
        return stageId != 0 && result.StageId == stageId;
    }

    /// <summary>
    /// 정산 후 도전 정리
    /// </summary>
    /// <returns>정리 완료 여부</returns>
    public bool TryCompleteStageCleanup ( )
    {
        if ( _owner.isActiveAndEnabled == false
            || _endingStageId != 0
            || _cleanupStageId == 0 )
            return false;

        _lastEndFailure = PlayEndFailure.None;

        int stageId = _cleanupStageId;
        bool abandon = _abandonRequested;

        //이미 정리된 동료는 종료 생략
        bool cleaned = _hireController.CurrentStageId == 0
            || _hireController.EndStage ( stageId );

        if ( cleaned == false )
        {
            Debug.LogError (
                $"정산 후 동료 정리에 실패했습니다: 도전={stageId}"
                + $" / 고용 도전={_hireController.CurrentStageId}" , _owner );
            NotifyEndFailure ( stageId , PlayEndFailure.CompanionCleanup );
            return false;
        }

        if ( _missions.CompleteStageCleanup ( stageId ) == false )
        {
            Debug.LogError (
                $"정산 후 미션 도전 정리에 실패했습니다: 도전={stageId}" , _owner );
            NotifyEndFailure ( stageId , PlayEndFailure.MissionCleanup );

            return false;
        }

        //완료 알림 전에 정리 상태 해제
        _stageInstallationUpgrades = null;
        _cleanupStageId = 0;
        _abandonRequested = false;
        _endEventsCompleted = false;
        _endCollectionTargets = null;

        //최종 정산과 정리가 끝난 뒤 일반 웨이브 저장 대기 해제
        _pendingWave = 0;

        OnStageEndProcessed?.Invoke ( stageId , abandon , true );

        return true;
    }

    /// <summary>
    /// 종료 실패 기록 및 통지
    /// </summary>
    /// <param name="stageId">도전 번호</param>
    /// <param name="failure">실패 위치</param>
    void NotifyEndFailure (
        int stageId ,
        PlayEndFailure failure )
    {
        _lastEndFailure = failure;

        OnStageEndProcessed?.Invoke ( stageId , _abandonRequested , false );
    }

    /// <summary>
    /// 접수한 종료 결과 조회
    /// </summary>
    /// <param name="stageId">도전 번호</param>
    /// <param name="result">접수한 종료 결과</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetEndResult (
        int stageId ,
        out MissionStageResult result )
    {
        result = default;

        if ( _endResult.StageId == 0 || _endResult.StageId != stageId )
            return false;

        result = _endResult;
        return true;
    }

    #endregion

    #region ----- 요청 조건 -----

    /// <summary>
    /// 도전 중 요청 가능 여부 확인
    /// </summary>
    /// <returns>요청 가능 여부</returns>
    public bool CanProcessStageRequest ( )
    {
        return _owner.isActiveAndEnabled
            && _isStarting == false
            && _isStageReady
            && _endingStageId == 0
            && _missions.IsProcessing == false
            && HasCurrentStage ( );
    }

    /// <summary>
    /// 입장 전 요청 가능 여부 확인
    /// </summary>
    /// <returns>요청 가능 여부</returns>
    public bool CanProcessLobbyRequest ( )
    {
        return _owner.isActiveAndEnabled
            && _isStarting == false
            && IsStageIdle ( );
    }

    /// <summary>
    /// 도전 대기 상태 확인
    /// </summary>
    /// <returns>도전 대기 여부</returns>
    public bool IsStageIdle ( )
    {
        return _isStageReady == false
            && _endingStageId == 0
            && _cleanupStageId == 0
            && _currency.IsStageActive == false
            && _rewards.CurrentStageId == 0
            && _hireController.CurrentStageId == 0;
    }

    /// <summary>
    /// 도전 연결 확인
    /// </summary>
    /// <returns>도전 연결 일치 여부</returns>
    bool HasCurrentStage ( )
    {
        int stageId = _rewards.CurrentStageId;
        return _currency.IsStageActive
            && stageId != 0
            && _currency.CurrentStageId == stageId
            && _hireController.CurrentStageId == stageId;
    }

    #endregion
}
