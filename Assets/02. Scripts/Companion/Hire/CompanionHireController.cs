using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 동료 고용 요청 결과
/// </summary>
public enum CompanionHireResult
{
    Success,                //고용 또는 재고용 성공
    Unavailable,            //현재 요청할 수 없는 상태 또는 없는 고용 번호
    InvalidData,            //고용 데이터가 없는 요청
    LimitReached,           //보유 고용 기록 한도 도달
    NotReady,               //재고용 대기 또는 사망 모션 진행 중
    InvalidPosition,        //생성 위치 확보 실패
    NotEnoughCurrency,      //비용 소비 불가
    InitializationFailed,   //동료 초기화 실패
    PointOccupied,          //다른 동료가 점유한 포인트
    AlreadyPlaced           //이미 생성 또는 배치한 동료
}

/// <summary>
/// 동료 고용 관리 - 비용, 배치, 전투 시작과 타입별 재고용 조율
/// </summary>
public class CompanionHireController : MonoBehaviour
{
    [Header ( "----- 디버그 -----" )]
    [SerializeField] bool _showDebugLog;    //고용 수명과 타입별 대기 전환 로그 출력 여부

    [Header ( "----- 고용 제한 -----" )]
    [SerializeField] int _maxHireCount = 2;    //타입과 무관한 보유 기록 한도, 대기 기록도 포함

    [Header ( "----- 생성 위치 -----" )]
    [SerializeField] float _spawnSearchRadius = 1f;    //요청점 주변 NavMesh 탐색 거리
    [SerializeField] LayerMask _spawnBlockMask;    //캐릭터와 장애물 등 생성 방해 레이어

    /// <summary>
    /// 현재 도전의 고용 기록
    /// </summary>
    List<CompanionHireEntry> _entries = new List<CompanionHireEntry> ( );
    /// <summary>
    /// 이번 갱신에서 재고용 가능으로 바뀐 기록
    /// </summary>
    List<CompanionHireEntry> _readyEntries = new List<CompanionHireEntry> ( );
    /// <summary>
    /// 타입별 재고용 가능 시각(동료 타입, 가능 시각)
    /// </summary>
    Dictionary<WeaponType , double> _rehireReadyTimes =
        new Dictionary<WeaponType , double> ( );
    /// <summary>
    /// 이번 갱신에 만료된 동료 타입 목록
    /// </summary>
    List<WeaponType> _expiredTypes = new List<WeaponType> ( );
    /// <summary>
    /// 씬 연결부의 요청 허용 조건
    /// </summary>
    Func<bool> _canRequest;
    Vector3? _priorityPosition;    //현재 도전의 공격 우선순위 기준 위치

    Currency _currency;    //스테이지 재화
    SoundManager _sound; //생성 동료에 전달할 서비스
    VfxManager _vfx; //생성 동료에 전달할 VFX 서비스
    Transform _hero;    //추종 기준 플레이어
    CompanionHireDataMap _hireDataMap;    //타입별 고용 설정
    int _stageId;    //현재 연결한 도전 번호
    int _nextHireId;    //마지막으로 발급한 고용 번호
    CompanionUpgradeSnapshot _stageUpgrades;    //현재 도전에 확정된 동료 강화 결과
    bool _isInitialized;    //상위 참조 초기화 여부
    bool _hireEnabled;    //진행 상태에 따른 고용 요청 허용
    bool _combatStarted;    //준비를 마치고 동료 행동을 시작했는지 여부

    /// <summary>
    /// 현재 고용 수명이 연결된 도전 번호
    /// </summary>
    public int CurrentStageId => _stageId;

    /// <summary>
    /// 고용 기록 수
    /// </summary>
    public int EntryCount => _entries.Count;

    /// <summary>
    /// 아직 첫 전투를 시작하지 않은 준비 단계 여부
    /// </summary>
    public bool IsPreparing => IsCurrentStage ( _stageId ) && _combatStarted == false;

    /// <summary>
    /// 현재 고용과 배치 요청 가능 여부
    /// </summary>
    public bool CanPlace => isActiveAndEnabled && _hireEnabled && _canRequest ( );

    /// <summary>
    /// 타입 대기 또는 요청 허용 상태 변경 이벤트
    /// </summary>
    public event Action OnHireAvailabilityChanged;

    /// <summary>
    /// 고용 상태 변경 이벤트(변경된 기록)
    /// </summary>
    public event Action<CompanionHireEntry> OnEntryChanged;

    /// <summary>
    /// 동료 활동 확정 이벤트(원래 도전 번호, 고용 번호)
    /// </summary>
    public event Action<int , int> OnParticipationConfirmed;

    /// <summary>
    /// 생성 동료의 사운드 서비스 연결
    /// </summary>
    /// <param name="sound">공통 관리자</param>
    public void SetSound ( SoundManager sound )
    {
        _sound = sound;
    }

    /// <summary>
    /// 생성 동료의 VFX 서비스 연결
    /// </summary>
    /// <param name="vfx">씬 재생 서비스</param>
    public void SetVfx ( VfxManager vfx )
    {
        _vfx = vfx;
    }

    /// <summary>
    /// 재화, 플레이어와 타입별 고용 설정 연결
    /// </summary>
    /// <param name="currency">재화 모듈</param>
    /// <param name="hero">플레이어</param>
    /// <param name="hireDataMap">타입별 고용 데이터 맵</param>
    /// <param name="canRequest">씬 연결부의 요청 허용 조건</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        Currency currency ,
        Transform hero ,
        CompanionHireDataMap hireDataMap ,
        Func<bool> canRequest )
    {
        if ( isActiveAndEnabled == false || _stageId != 0 )
            return false;

        _currency = currency;
        _hero = hero;
        _hireDataMap = hireDataMap;
        _canRequest = canRequest;
        _hireEnabled = false;
        _isInitialized = true;
        return true;
    }

    /// <summary>
    /// 이미 시작된 재화 도전에 고용 수명 연결
    /// </summary>
    /// <param name="stageId">현재 도전 번호</param>
    /// <returns>연결 성공 여부</returns>
    public bool BeginStage ( int stageId )
    {
        return BeginStage ( stageId , new CompanionUpgradeSnapshot ( ) );
    }

    /// <summary>
    /// 현재 도전의 고용 수명과 강화 결과 연결
    /// </summary>
    /// <param name="stageId">현재 도전 번호</param>
    /// <param name="upgrades">도전별 계산 결과</param>
    /// <param name="priorityPosition">공격 우선순위 기준</param>
    /// <returns>연결 성공 여부</returns>
    public bool BeginStage (
        int stageId ,
        CompanionUpgradeSnapshot upgrades ,
        Vector3? priorityPosition = null )
    {
        if ( isActiveAndEnabled == false
            || _isInitialized == false
            || _stageId != 0
            || _currency.IsStageActive == false
            || _currency.CurrentStageId != stageId )
            return false;

        _stageUpgrades = upgrades;
        _priorityPosition = priorityPosition;
        _stageId = stageId;
        _hireEnabled = false;
        _combatStarted = false;
        return true;
    }

    private void Update ( )
    {
        if ( _stageId == 0 || _hireEnabled == false )
            return;

        double now = Time.timeAsDouble;
        int stageId = _stageId;

        //사망 연출과 별개로 타입 대기 만료를 먼저 반영
        foreach ( KeyValuePair<WeaponType , double> cooldown in _rehireReadyTimes )
        {
            if ( now >= cooldown.Value )
                _expiredTypes.Add ( cooldown.Key );
        }
        bool expired = _expiredTypes.Count > 0;
        foreach ( WeaponType type in _expiredTypes )
        {
            _rehireReadyTimes.Remove ( type );
            if ( _showDebugLog == true )
                Debug.Log ( $"[동료 고용:{name}] 타입 대기 만료 / 도전:{stageId} / 타입:{type}" , this );
        }
        _expiredTypes.Clear ( );

        //먼저 기록 상태만 변경
        for ( int i = 0 ; i < _entries.Count ; i++ )
        {
            CompanionHireEntry entry = _entries [ i ];

            if ( entry.State != CompanionHireState.Waiting
                || now < entry.ReadyTime )
                continue;

            entry.SetReady ( );
            if ( _showDebugLog == true )
                Debug.Log ( $"[동료 고용:{name}] 개체 재고용 준비 완료 / 도전:{stageId}"
                    + $" / 고용 번호:{entry.Id} / 타입:{entry.Data.CompanionData.WeaponType}" , this );
            _readyEntries.Add ( entry );
        }

        //고용 목록 순회가 끝난 뒤 외부 통지
        for ( int i = 0 ; i < _readyEntries.Count ; i++ )
        {
            //앞선 알림에서 도전이 종료됐다면 이전 도전 통지 중단
            if ( _stageId != stageId || _hireEnabled == false )
                break;

            CompanionHireEntry entry = _readyEntries [ i ];

            //앞선 알림에서 이미 재고용된 기록에는 Ready 통지 생략
            if ( entry.State != CompanionHireState.Ready )
                continue;

            OnEntryChanged?.Invoke ( entry );
        }

        _readyEntries.Clear ( );

        //대기가 연출보다 먼저 끝나도 타입 슬롯은 현재 상태를 다시 조회
        if ( expired == true && _stageId == stageId && _hireEnabled == true )
            OnHireAvailabilityChanged?.Invoke ( );
    }

    /// <summary>
    /// 진행 상태에 따른 고용 요청 허용
    /// </summary>
    /// <param name="enabled">고용 허용 여부</param>
    public void SetHireEnabled ( bool enabled )
    {
        if ( _hireEnabled == enabled )
            return;

        _hireEnabled = enabled;
        OnHireAvailabilityChanged?.Invoke ( );
    }

    /// <summary>
    /// 타입의 재고용 가능 기록을 우선 사용하고 없으면 신규 고용
    /// </summary>
    /// <param name="stageId">요청한 도전 번호</param>
    /// <param name="weaponType">고용할 동료의 무기 타입</param>
    /// <param name="hireId">요청 대상 고용 번호</param>
    /// <param name="result">처리 결과</param>
    /// <returns>고용 또는 재고용 성공 여부</returns>
    public bool TryHireOrRehire (
        int stageId , WeaponType weaponType ,
        out int hireId , out CompanionHireResult result )
    {
        hireId = 0;
        result = CompanionHireResult.Unavailable;

        //초기화와 도전 상태가 유효할 때만 데이터 조회
        if ( CanRequest ( stageId ) == false )
            return false;

        if ( _hireDataMap.TryGet (
            weaponType , out CompanionHireData data ) == false )
        {
            result = CompanionHireResult.InvalidData;
            return false;
        }

        //신규 인원 제한보다 기존 기록의 재고용을 먼저 판단
        if ( TryGetRehireEntry (
            data , out CompanionHireEntry entry ) == true )
        {
            return ExecuteHire (
                stageId , entry , false , out hireId , out result );
        }

        if ( CanCreateHire ( data , out result ) == false )
            return false;

        entry = new CompanionHireEntry ( ++_nextHireId , data );
        return ExecuteHire ( stageId , entry , true , out hireId , out result );
    }

    /// <summary>
    /// 설정에 해당하는 동료 신규 고용
    /// </summary>
    /// <param name="stageId">요청한 도전 번호</param>
    /// <param name="data">고용 설정</param>
    /// <param name="hireId">성공한 고용 번호</param>
    /// <param name="result">처리 결과</param>
    /// <returns>고용 성공 여부</returns>
    public bool TryHire (
        int stageId , CompanionHireData data ,
        out int hireId , out CompanionHireResult result )
    {
        hireId = 0;
        result = CompanionHireResult.Unavailable;

        if ( CanRequest ( stageId ) == false )
            return false;

        //데이터 검사
        if ( data == null )
        {
            result = CompanionHireResult.InvalidData;
            return false;
        }

        if ( CanCreateHire ( data , out result ) == false )
            return false;

        CompanionHireEntry entry =
            new CompanionHireEntry ( ++_nextHireId , data );

        return ExecuteHire ( stageId , entry , true , out hireId , out result );
    }

    /// <summary>
    /// 사망한 개별 고용 기록의 비용 처리와 추종 동료 생성
    /// </summary>
    /// <param name="stageId">요청한 도전 번호</param>
    /// <param name="hireId">재고용할 기록 번호</param>
    /// <param name="result">처리 결과</param>
    /// <returns>재고용 성공 여부</returns>
    public bool TryRehire (
        int stageId , int hireId ,
        out CompanionHireResult result )
    {
        result = CompanionHireResult.Unavailable;

        if ( CanRequest ( stageId ) == false )
            return false;

        if ( TryGetEntry ( hireId , out CompanionHireEntry entry ) == false )
            return false;

        //Update보다 요청이 먼저 실행돼도 실제 시각으로 준비 여부 판단
        if ( CanRehire ( entry , Time.timeAsDouble ) == false )
        {
            result = CompanionHireResult.NotReady;
            return false;
        }

        return ExecuteHire ( stageId , entry , false , out _ , out result );
    }

    /// <summary>
    /// 신규 고용의 타입 대기와 인원 제한 확인
    /// </summary>
    /// <param name="data">확인된 고용 설정</param>
    /// <param name="result">판정 결과</param>
    /// <returns>신규 고용 가능 여부</returns>
    bool CanCreateHire ( CompanionHireData data , out CompanionHireResult result )
    {
        if ( GetRehireRemainingTime ( data.CompanionData.WeaponType ) > 0d )
        {
            result = CompanionHireResult.NotReady;
            return false;
        }

        if ( _entries.Count >= _maxHireCount )
        {
            result = CompanionHireResult.LimitReached;
            return false;
        }

        result = CompanionHireResult.Success;
        return true;
    }

    /// <summary>
    /// 확인된 고용 요청 실행 후 참여와 표시 변경 전달
    /// </summary>
    /// <param name="stageId">확인된 도전 번호</param>
    /// <param name="entry">고용할 기록</param>
    /// <param name="isNew">신규 기록 여부</param>
    /// <param name="hireId">고용 기록 번호</param>
    /// <param name="result">처리 결과</param>
    /// <returns>고용 성공 여부</returns>
    bool ExecuteHire (
        int stageId , CompanionHireEntry entry , bool isNew ,
        out int hireId , out CompanionHireResult result )
    {
        hireId = 0;

        if ( TryEmploy ( entry , isNew , out result ) == false )
            return false;

        //외부 알림 전에 반환 정보와 고용 기록까지 확정
        hireId = entry.Id;
        if ( entry.State == CompanionHireState.Alive )
            OnParticipationConfirmed?.Invoke ( stageId , entry.Id );

        if ( _stageId == stageId && _hireEnabled == true )
            OnEntryChanged?.Invoke ( entry );
        return true;
    }

    /// <summary>
    /// 사망 기록 확정 후 같은 타입의 재고용 대기 갱신
    /// </summary>
    /// <param name="instance">사망한 동료</param>
    /// <param name="damageInfo">사망 원인</param>
    void HandleCompanionDied (
        Companion instance , DamageInfo damageInfo )
    {
        CompanionHireEntry entry = FindEntry ( instance );

        if ( entry == null || entry.State != CompanionHireState.Alive )
            return;

        entry.SetDying ( );

        double now = Time.timeAsDouble;
        double readyTime = now + entry.Data.RehireDelay;
        WeaponType type = entry.Data.CompanionData.WeaponType;
        bool restarted = _showDebugLog == true
            && _rehireReadyTimes.TryGetValue ( type , out double previousReadyTime ) == true
            && now < previousReadyTime;
        _rehireReadyTimes [ type ] = readyTime;

        List<CompanionHireEntry> changed = new List<CompanionHireEntry> ( );
        foreach ( CompanionHireEntry current in _entries )
        {
            if ( current.Data.CompanionData.WeaponType != type )
                continue;

            //같은 타입의 생존 동료는 그대로 유지
            if ( current.State != CompanionHireState.Dying
                && current.State != CompanionHireState.Waiting
                && current.State != CompanionHireState.Ready )
                continue;

            current.SetRehireReadyTime ( readyTime , now );
            changed.Add ( current );
        }

        if ( _showDebugLog == true )
            Debug.Log ( $"[동료 고용:{name}] 타입 대기 {( restarted == true ? "갱신" : "시작" )}"
                + $" / 도전:{_stageId} / 타입:{type} / 사망 고용 번호:{entry.Id}"
                + $" / 대기:{entry.Data.RehireDelay}초 / 종료 시각:{readyTime:F2}"
                + $" / 반영 기록:{changed.Count}개" , this );

        //관련 기록을 모두 갱신한 뒤 외부에 알림
        NotifyEntriesChanged ( changed );
    }

    /// <summary>
    /// 사망 객체 제거 후 이미 시작한 대기의 남은 상태 반영
    /// </summary>
    /// <param name="instance">사망 연출이 끝난 동료</param>
    void HandleDeathPresentationCompleted ( Companion instance )
    {
        CompanionHireEntry entry = FindEntry ( instance );

        if ( entry == null || entry.State != CompanionHireState.Dying )
            return;

        //제거 콜백 전에 이전 객체와 기록의 연결 해제
        entry.CompleteDeathPresentation ( Time.timeAsDouble );

        RemoveCompanion ( instance );
        if ( _showDebugLog == true )
            Debug.Log ( $"[동료 고용:{name}] 사망 연출 완료 / 고용 번호:{entry.Id}"
                + $" / 객체/포인트 해제 / 상태:{entry.State}"
                + $" / 타입 대기:{GetRehireRemainingTime ( entry.Data.CompanionData.WeaponType ):F2}초" , this );
        OnEntryChanged?.Invoke ( entry );
    }

    /// <summary>
    /// 첫 전투 전에는 기록만, 이후에는 추종 동료까지 비용 처리 후 확정
    /// </summary>
    /// <param name="entry">대상 고용 기록</param>
    /// <param name="isNew">신규 기록 여부</param>
    /// <param name="result">처리 결과</param>
    /// <returns>고용 성공 여부</returns>
    bool TryEmploy (
        CompanionHireEntry entry ,
        bool isNew ,
        out CompanionHireResult result )
    {
        result = CompanionHireResult.NotEnoughCurrency;
        int cost = entry.Data.HireCost;

        Companion instance = null;

        if ( _combatStarted == true )
        {
            //첫 전투 이후에는 생성 전 잔액 확인, 이전에는 실제 소비로 판정
            if ( _currency.CanSpend ( CurrencyType.Stage , cost ) == false )
                return false;

            if ( TryPrepareFollower ( entry , out instance , out result ) == false )
                return false;
        }

        //재화 알림은 표시용, 도전 전환은 요청 반환 후 처리
        if ( _currency.TrySpend ( CurrencyType.Stage , cost ) == false )
        {
            if ( instance != null )
                RemoveCompanion ( instance );

            result = CompanionHireResult.NotEnoughCurrency;
            return false;
        }

        entry.SetHired ( );

        if ( instance != null )
        {
            entry.SetPrepared ( instance , null );
            entry.SetAlive ( );
        }

        if ( isNew == true )
            _entries.Add ( entry );

        //첫 전투 전은 Hired 유지
        //실제 생성과 비용 처리가 성공한 동료만 등장 효과 재생
        if ( instance != null )
        {
            instance.ConfirmHire ( );
            instance.PlayHireEffect ( );
        }

        result = CompanionHireResult.Success;
        return true;
    }

    /// <summary>
    /// 미생성 동료 배치 또는 기존 동료 재배치
    /// </summary>
    /// <param name="hireId">배치할 고용 번호</param>
    /// <param name="point">배치 컨트롤러가 선택한 포인트</param>
    /// <param name="result">처리 결과</param>
    /// <returns>배치 성공 여부</returns>
    public bool TryPlace (
        int hireId ,
        CompanionPlacementPoint point ,
        out CompanionHireResult result )
    {
        result = CompanionHireResult.Unavailable;
        if ( CanRequest ( _stageId ) == false )
            return false;

        if ( TryGetEntry ( hireId , out CompanionHireEntry entry ) == false )
            return false;

        if ( entry.State != CompanionHireState.Hired
            && entry.State != CompanionHireState.Prepared
            && entry.State != CompanionHireState.Alive )
        {
            result = CompanionHireResult.NotReady;
            return false;
        }

        if ( entry.PlacementPoint == point )
        {
            result = CompanionHireResult.AlreadyPlaced;
            return false;
        }

        foreach ( CompanionHireEntry current in _entries )
        {
            if ( current == entry || current.PlacementPoint != point )
                continue;

            result = CompanionHireResult.PointOccupied;
            return false;
        }

        if ( CompanionSpawnPositionFinder.TryGetPosition (
            entry.Data.Prefab , point.Position , _spawnSearchRadius , _spawnBlockMask ,
            out Vector3 position , entry.Instance ) == false )
        {
            result = CompanionHireResult.InvalidPosition;
            return false;
        }

        int stageId = _stageId;

        //점유 변경 전 값을 보관하여 공격 취소 콜백 전에 확정 결과 출력
        CompanionPlacementPoint previousPoint = entry.PlacementPoint;
        bool initialPlacement = entry.State == CompanionHireState.Hired;

        if ( initialPlacement == true )
        {
            if ( TryPrepareInstance ( entry , position , CompanionMode.Defend ,
                out Companion instance , out result ) == false )
                return false;

            //초기화 성공 후에만 점유, 첫 전투 전 행동 보류 유지
            entry.SetPrepared ( instance , point );
        }
        else
        {
            if ( entry.Instance.TrySetPlacement ( position ) == false )
            {
                result = CompanionHireResult.Unavailable;
                return false;
            }

            //공격 취소 콜백 전에 실제 위치와 점유 기록 일치
            entry.SetPlacementPoint ( point );
        }

        if ( _showDebugLog == true )
            Debug.Log ( $"[동료 고용:{name}] 배치 확정 / 도전:{stageId} / 고용 번호:{entry.Id}"
                + $" / 이전 포인트:{( previousPoint == null ? "없음" : previousPoint.name )}"
                + $" / 새 포인트:{point.name} / 방어 중심:{entry.Instance.ActivityCenter}"
                + $" / 모드:{entry.Instance.Mode} / 기록 상태:{entry.State}" , this );

        if ( initialPlacement == false )
        {
            entry.Instance.CompletePlacement ( );
        }
        else
        {
            //최초 배치로 생성된 동료만 등장 효과 재생
            entry.Instance.PlayHireEffect ( );
        }

        result = CompanionHireResult.Success;

        if ( _stageId == stageId && _hireEnabled == true )
            OnEntryChanged?.Invoke ( entry );
        return true;
    }

    /// <summary>
    /// 미배치 동료를 추종형으로 준비한 뒤 전체 동료 전투 시작
    /// </summary>
    /// <param name="stageId">현재 도전 번호</param>
    /// <param name="result">처리 결과</param>
    /// <returns>전체 준비와 전투 시작 성공 여부</returns>
    public bool BeginCombat ( int stageId , out CompanionHireResult result )
    {
        bool started = BeginCombat ( stageId , out result , out _ );
        if ( started == true )
            NotifyEntriesChanged ( );
        return started;
    }

    /// <summary>
    /// 표시 알림 전에 준비 동료의 전체 전투 상태 확정
    /// </summary>
    /// <param name="stageId">원래 도전 번호</param>
    /// <param name="result">시작 결과</param>
    /// <param name="participantIds">시작 성공 시 확정된 참여 고용 번호</param>
    /// <returns>전체 시작 성공 여부</returns>
    public bool BeginCombat (
        int stageId ,
        out CompanionHireResult result ,
        out IReadOnlyList<int> participantIds )
    {
        participantIds = Array.Empty<int> ( );
        result = CompanionHireResult.Unavailable;
        if ( CanRequest ( stageId ) == false || _combatStarted == true )
            return false;

        //실패 시 이번 자동 생성분만 되돌리고, 기존 포인트 배치는 유지
        List<CompanionHireEntry> followers = new List<CompanionHireEntry> ( );

        foreach ( CompanionHireEntry entry in _entries )
        {
            if ( entry.State != CompanionHireState.Hired )
                continue;

            if ( TryPrepareFollower ( entry , out Companion instance , out result ) == false )
            {
                foreach ( CompanionHireEntry follower in followers )
                {
                    Companion prepared = follower.Instance;
                    //비활성화 콜백 전에 기록을 원래 미배치 고용 상태로 복구
                    follower.SetHired ( );
                    RemoveCompanion ( prepared );
                }
                return false;
            }

            entry.SetPrepared ( instance , null );
            followers.Add ( entry );
        }

        //전체 생성이 끝나야 단계와 기록 확정, 부분 성공 상태로 전투를 시작하지 않음
        _combatStarted = true;
        List<int> confirmedIds = new List<int> ( );

        foreach ( CompanionHireEntry entry in _entries )
        {
            if ( entry.State == CompanionHireState.Prepared )
                entry.SetAlive ( );
            if ( entry.State == CompanionHireState.Alive )
                confirmedIds.Add ( entry.Id );
        }

        //행동 허용과 표시 알림보다 먼저 이번 성공의 참여 목록을 독립적으로 확정
        participantIds = confirmedIds.AsReadOnly ( );

        foreach ( CompanionHireEntry entry in _entries )
        {
            if ( entry.State == CompanionHireState.Alive )
                entry.Instance.ConfirmHire ( );
        }

        //전체 준비 성공 후 이번에 자동 생성한 동료만 재생
        foreach ( CompanionHireEntry follower in followers )
            follower.Instance.PlayHireEffect ( );

        result = CompanionHireResult.Success;
        return true;
    }

    /// <summary>
    /// 연결 상태 확정 이후 동료 표시 갱신
    /// </summary>
    public void NotifyEntriesChanged ( )
    {
        NotifyEntriesChanged ( _entries.ToArray ( ) );
    }

    /// <summary>
    /// 상태 반영이 끝난 기록의 변경 전달
    /// </summary>
    /// <param name="entries">변경된 독립 기록 목록</param>
    void NotifyEntriesChanged ( IReadOnlyList<CompanionHireEntry> entries )
    {
        int stageId = _stageId;
        foreach ( CompanionHireEntry entry in entries )
        {
            //앞선 알림에서 정리된 이전 도전의 알림은 중단
            if ( _stageId != stageId )
                return;

            OnEntryChanged?.Invoke ( entry );
        }
    }

    /// <summary>
    /// 확인된 NavMesh 위치에 생성하고 행동 보류 상태로 초기화
    /// </summary>
    /// <param name="entry">대상 고용 기록</param>
    /// <param name="position">위치 검사를 통과한 생성 좌표</param>
    /// <param name="mode">방어 또는 추종</param>
    /// <param name="instance">초기화된 동료</param>
    /// <param name="result">처리 결과</param>
    /// <returns>생성 및 초기화 성공 여부</returns>
    bool TryPrepareInstance (
        CompanionHireEntry entry , Vector3 position , CompanionMode mode ,
        out Companion instance , out CompanionHireResult result )
    {
        //활성 프리팹의 Awake/OnEnable 완료 후 모듈 초기화
        instance = Instantiate (
            entry.Data.Prefab , position , Quaternion.identity , transform );
        instance.SetSound( _sound );
        instance.SetVfx( _vfx );
        Vector3? placementPoint = mode == CompanionMode.Defend
            ? position : ( Vector3? ) null;

        CompanionUpgradeStats upgrades = _stageUpgrades.GetStats ( entry.Data.CompanionData );

        if ( instance.PrepareHire (
            entry.Data.CompanionData , _hero , placementPoint , upgrades ,
            _priorityPosition ) == false )
        {
            RemoveCompanion ( instance );
            instance = null;
            result = CompanionHireResult.InitializationFailed;
            return false;
        }

        instance.OnDied += HandleCompanionDied;
        instance.OnDeathPresentationCompleted += HandleDeathPresentationCompleted;
        result = CompanionHireResult.Success;
        return true;
    }

    /// <summary>
    /// 현재 플레이에 진입할 생존 동료의 고용 번호 복사
    /// </summary>
    /// <returns>독립된 참여 후보 목록</returns>
    public IReadOnlyList<int> GetActiveCompanionIds ( )
    {
        List<int> ids = new List<int> ( );

        foreach ( CompanionHireEntry entry in _entries )
        {
            if ( entry.State == CompanionHireState.Alive && entry.Instance != null )
                ids.Add ( entry.Id );
        }
        return ids.AsReadOnly ( );
    }

    /// <summary>
    /// 정산 전 객체를 유지한 채 모든 동료 행동 중단
    /// </summary>
    public void StopStageActions ( )
    {
        SetHireEnabled ( false );

        foreach ( CompanionHireEntry entry in _entries )
        {
            if ( entry.Instance != null )
                entry.Instance.StopStageActions ( );
        }
    }

    /// <summary>
    /// 플레이어 주변의 빈 위치에 추종형 동료 준비
    /// </summary>
    /// <param name="entry">대상 고용 기록</param>
    /// <param name="instance">초기화된 동료</param>
    /// <param name="result">처리 결과</param>
    /// <returns>생성 위치 확보와 초기화 성공 여부</returns>
    bool TryPrepareFollower (
        CompanionHireEntry entry ,
        out Companion instance ,
        out CompanionHireResult result )
    {
        instance = null;
        result = CompanionHireResult.InvalidPosition;

        //위치 확보 후에만 생성하며, 초기화 실패 시 다른 위치에서 재생성하지 않음
        if ( CompanionSpawnPositionFinder.TryGetFollowerPosition (
            entry.Data.Prefab , _hero.position , _spawnSearchRadius , _spawnBlockMask ,
            out Vector3 position ) == false )
            return false;

        return TryPrepareInstance ( entry , position , CompanionMode.Follow ,
            out instance , out result );
    }

    /// <summary>
    /// 이벤트 해제 후 생성한 동료 제거
    /// </summary>
    /// <param name="instance">제거할 동료</param>
    void RemoveCompanion ( Companion instance )
    {
        //생성에 성공한 객체만 전달받으며 상태별 객체 유무는 호출부에서 판단
        instance.OnDied -= HandleCompanionDied;
        instance.OnDeathPresentationCompleted -= HandleDeathPresentationCompleted;

        instance.gameObject.SetActive ( false );
        Destroy ( instance.gameObject );
    }

    /// <summary>
    /// 해당 도전의 고용 객체와 기록 정리
    /// </summary>
    /// <param name="stageId">종료할 도전 번호</param>
    /// <returns>정리 성공 여부</returns>
    public bool EndStage ( int stageId )
    {
        if ( _stageId == 0 || _stageId != stageId )
            return false;

        ClearStage ( );
        return true;
    }

    /// <summary>
    /// 요청 차단 후 전체 고용 객체와 기록 제거
    /// </summary>
    void ClearStage ( )
    {
        _hireEnabled = false;
        _stageId = 0;
        _combatStarted = false;

        for ( int i = _entries.Count - 1 ; i >= 0 ; i-- )
        {
            CompanionHireEntry entry = _entries [ i ];
            Companion instance = entry.Instance;

            //미배치 고용 기록과 사망 후 대기 기록에는 생성 객체가 없음
            if ( instance == null )
                continue;

            entry.ClearInstance ( );
            RemoveCompanion ( instance );
        }

        _entries.Clear ( );
        _readyEntries.Clear ( );
        _rehireReadyTimes.Clear ( );
        _expiredTypes.Clear ( );
        _stageUpgrades = null;
        _priorityPosition = null;
    }

    /// <summary>
    /// 고용 번호로 개별 기록 조회
    /// </summary>
    /// <param name="hireId">고용 번호</param>
    /// <param name="entry">조회한 기록</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetEntry (
        int hireId , out CompanionHireEntry entry )
    {
        foreach ( CompanionHireEntry current in _entries )
        {
            if ( current.Id != hireId )
                continue;

            entry = current;
            return true;
        }

        entry = null;
        return false;
    }

    /// <summary>
    /// 현재 도전의 고용 순번으로 개별 기록 조회
    /// </summary>
    /// <param name="index">0이면 첫 번째, 1이면 두 번째</param>
    /// <param name="entry">해당 순번의 고용 기록</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetEntryAt (
        int index , out CompanionHireEntry entry )
    {
        //아직 고용하지 않은 순번은 기록이 없음
        if ( index < 0 || index >= _entries.Count )
        {
            entry = null;
            return false;
        }

        //상태로 건너뛰지 않아 사망/재고용 후에도 같은 배치 순번 유지
        entry = _entries [ index ];
        return true;
    }

    /// <summary>
    /// 같은 고용 설정의 재고용 가능한 개별 기록을 고용 순서로 조회
    /// </summary>
    /// <param name="data">선택한 고용 설정</param>
    /// <param name="entry">재고용 가능한 기록</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetRehireEntry (
        CompanionHireData data , out CompanionHireEntry entry )
    {
        double now = Time.timeAsDouble;
        foreach ( CompanionHireEntry current in _entries )
        {
            if ( current.Data != data )
                continue;

            if ( CanRehire ( current , now ) == false )
                continue;

            entry = current;
            return true;
        }

        entry = null;
        return false;
    }

    /// <summary>
    /// 현재 전투 중인 동료 기록을 고용 순서로 조회
    /// </summary>
    /// <param name="entry">행동 중인 생존 동료 기록</param>
    /// <returns>조회 성공 여부</returns>
    public bool TryGetAliveEntry ( out CompanionHireEntry entry )
    {
        foreach ( CompanionHireEntry current in _entries )
        {
            if ( current.State != CompanionHireState.Alive )
                continue;

            entry = current;
            return true;
        }

        entry = null;
        return false;
    }

    /// <summary>
    /// 사망 객체 정리와 타입 대기가 끝났는지 확인
    /// </summary>
    /// <param name="entry">검사할 고용 기록</param>
    /// <param name="now">현재 게임 시각</param>
    /// <returns>재고용 가능 여부</returns>
    bool CanRehire ( CompanionHireEntry entry , double now )
    {
        if ( entry.State != CompanionHireState.Waiting
            && entry.State != CompanionHireState.Ready )
            return false;

        return _rehireReadyTimes.TryGetValue (
            entry.Data.CompanionData.WeaponType , out double readyTime ) == false
            || now >= readyTime;
    }

    /// <summary>
    /// 타입별 남은 재고용 대기시간 조회
    /// </summary>
    /// <param name="weaponType">동료 타입</param>
    /// <returns>남은 게임 시간</returns>
    public double GetRehireRemainingTime ( WeaponType weaponType )
    {
        if ( _rehireReadyTimes.TryGetValue ( weaponType , out double readyTime ) == false )
            return 0d;

        return Math.Max ( 0d , readyTime - Time.timeAsDouble );
    }

    /// <summary>
    /// 타입 슬롯의 고용 또는 재고용 가능 여부 조회
    /// </summary>
    /// <param name="weaponType">동료 타입</param>
    /// <param name="result">현재 불가 사유 또는 성공 상태</param>
    /// <returns>요청 가능 여부</returns>
    public bool CanHireOrRehire (
        WeaponType weaponType , out CompanionHireResult result )
    {
        result = CompanionHireResult.Unavailable;
        if ( CanRequest ( _stageId ) == false )
            return false;

        if ( _hireDataMap.TryGet ( weaponType , out CompanionHireData data ) == false )
        {
            result = CompanionHireResult.InvalidData;
            return false;
        }

        //실행과 같은 재고용 우선 순서와 신규 고용 제한 적용
        if ( TryGetRehireEntry ( data , out _ ) == false
            && CanCreateHire ( data , out result ) == false )
            return false;

        if ( _currency.CanSpend ( CurrencyType.Stage , data.HireCost ) == false )
        {
            result = CompanionHireResult.NotEnoughCurrency;
            return false;
        }

        result = CompanionHireResult.Success;
        return true;
    }

    /// <summary>
    /// 현재 객체에 대응하는 고용 기록 조회
    /// </summary>
    /// <param name="instance">동료 객체</param>
    /// <returns>해당 기록 또는 null</returns>
    CompanionHireEntry FindEntry ( Companion instance )
    {
        foreach ( CompanionHireEntry entry in _entries )
        {
            if ( entry.Instance == instance )
                return entry;
        }

        return null;
    }

    /// <summary>
    /// 현재 도전과 고용 허용 상태 확인
    /// </summary>
    /// <param name="stageId">요청한 도전 번호</param>
    /// <returns>요청 가능 여부</returns>
    bool CanRequest ( int stageId )
    {
        return stageId == _stageId && CanPlace;
    }

    /// <summary>
    /// 재화 도전과 연결한 현재 도전이 일치하는지 확인
    /// </summary>
    /// <param name="stageId">요청한 도전 번호</param>
    /// <returns>현재 도전 여부</returns>
    bool IsCurrentStage ( int stageId )
    {
        return isActiveAndEnabled
            && _isInitialized
            && _stageId != 0
            && _stageId == stageId
            && _currency.IsStageActive
            && _currency.CurrentStageId == stageId;
    }

    private void OnDisable ( )
    {
        ClearStage ( );
    }
}
