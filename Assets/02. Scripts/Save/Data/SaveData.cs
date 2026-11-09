using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 저장 처리 결과
/// </summary>
public enum SaveResult
{
    Success,        //처리 성공
    NotFound,       //저장 파일 없음
    InvalidData,        //필수 값 누락 또는 잘못된 값
    UnsupportedVersion,     //지원하지 않는 버전
    ReadFailed,     //파일 읽기 실패
    WriteFailed,     //파일 쓰기 실패
    Unavailable,    //현재 연결 상태에서 요청 불가
    DeleteFailed    //파일 삭제 실패
}

/// <summary>
/// 저장 데이터 - 슬롯별 영구 진행 보관
/// </summary>
[Serializable]
public class SaveData
{
    public const int CurrentVersion = 9;        //현재 저장 형식 버전

    [SerializeField] int _version;                       //저장 형식 버전
    [SerializeField] int _permanentCurrencyBalance = -1;  //영구 재화, 누락 시 -1
    [SerializeField] List<ItemSaveData> _items;           //아이템 수량
    [SerializeField] List<UpgradeSaveData> _upgrades;     //강화 단계
    [SerializeField] List<MissionKillCountSaveData> _missionCounters; //미션 처치 집계, 기존 JSON 필드명 유지
    [SerializeField] List<MissionSaveData> _missions;     //미션 완료와 지급
    [SerializeField] List<string> _collectionUnlockedIds; //도감 해금 ID
    [SerializeField] List<string> _collectionNewIds;      //도감 미확인 신규 ID
    [SerializeField] List<WeaponType> _unlockedWeapons;    //해금한 무기
    [SerializeField] List<string> _purchasedInstallationIds;    //구매한 설치물
    [SerializeField] bool _hasSelectedWeapon;    //무기 선택 여부
    [SerializeField] WeaponType _selectedWeapon;    //선택 무기
    [SerializeField] List<StageClearRecord> _stageRecords;    //맵/난이도별 클리어 기록
    [SerializeField] List<StarRewardRecord> _starRewards;    //최초 별 지급 이력
    [SerializeField] bool _hasClearReward;    //최근 종료 지급 기록 존재 여부
    [SerializeField] ClearRewardRecord _lastClearReward;    //최근 종료 지급 내역

    /// <summary>
    /// 저장 형식 버전
    /// </summary>
    public int Version => _version;

    /// <summary>
    /// 영구 재화 잔액
    /// </summary>
    public int PermanentCurrencyBalance => _permanentCurrencyBalance;

    /// <summary>
    /// 아이템별 보유 기록
    /// </summary>
    public IReadOnlyList<ItemSaveData> Items => _items;

    /// <summary>
    /// 강화별 구매 기록
    /// </summary>
    public IReadOnlyList<UpgradeSaveData> Upgrades => _upgrades;

    /// <summary>
    /// 난이도별 미션 처치 집계
    /// </summary>
    public IReadOnlyList<MissionKillCountSaveData> MissionKillCounts => _missionCounters;

    /// <summary>
    /// 최초 미션 완료와 지급 기록
    /// </summary>
    public IReadOnlyList<MissionSaveData> Missions => _missions;

    /// <summary>
    /// 해금한 도감 적 ID
    /// </summary>
    public IReadOnlyList<string> CollectionUnlockedIds => _collectionUnlockedIds;

    /// <summary>
    /// 확인하지 않은 도감 신규 ID
    /// </summary>
    public IReadOnlyList<string> CollectionNewIds => _collectionNewIds;

    /// <summary>
    /// 해금한 무기
    /// </summary>
    public IReadOnlyList<WeaponType> UnlockedWeapons => _unlockedWeapons;

    /// <summary>
    /// 구매한 설치물 ID
    /// </summary>
    public IReadOnlyList<string> PurchasedInstallationIds => _purchasedInstallationIds;

    /// <summary>
    /// 무기 선택 여부
    /// </summary>
    public bool HasSelectedWeapon => _hasSelectedWeapon;

    /// <summary>
    /// 선택 무기
    /// </summary>
    public WeaponType SelectedWeapon => _selectedWeapon;

    /// <summary>
    /// 맵/난이도별 클리어 기록
    /// </summary>
    public IReadOnlyList<StageClearRecord> StageRecords => _stageRecords;

    /// <summary>
    /// 맵/난이도별 최초 별 지급 이력
    /// </summary>
    public IReadOnlyList<StarRewardRecord> StarRewards => _starRewards;
    /// <summary>
    /// 최근 종료 보상 저장 여부
    /// </summary>
    public bool HasClearReward => _hasClearReward;
    /// <summary>
    /// 최근 종료 보상 기록
    /// </summary>
    public ClearRewardRecord LastClearReward => _hasClearReward ? _lastClearReward : null;

    /// <summary>
    /// 도입 전 별 기록의 정책 대기 목록 준비
    /// </summary>
    public void PrepareLegacyStarRewards ( )
    {
        _starRewards = new List<StarRewardRecord> ( );
        //기록이 손상된 파일의 판단은 기존 값 검사에 맡김
        if ( _stageRecords != null )
        {
            foreach ( StageClearRecord record in _stageRecords )
            {
                if ( record != null )
                    _starRewards.Add ( new StarRewardRecord (
                        record.MapId , record.Difficulty , 0 , true ) );
            }
        }
        _hasClearReward = false;
        _lastClearReward = null;
    }

    /// <summary>
    /// 별 지급 단계를 저장 후보에 반영
    /// </summary>
    /// <param name="record">교체할 지급 이력</param>
    public void SetStarReward ( StarRewardRecord record )
    {
        for ( int i = 0 ; i < _starRewards.Count ; i++ )
        {
            if ( _starRewards [ i ].MapId == record.MapId
                && _starRewards [ i ].Difficulty == record.Difficulty )
            {
                _starRewards [ i ] = record;
                return;
            }
        }
        _starRewards.Add ( record );
    }

    /// <summary>
    /// 최근 지급 내역 반영
    /// </summary>
    /// <param name="record">지급 내역</param>
    public void SetLastClearReward ( ClearRewardRecord record )
    {
        _lastClearReward = record;
        _hasClearReward = true;
    }

    /// <summary>
    /// 구형 클리어 목록 준비
    /// </summary>
    public void PrepareLegacyStageRecords ()
    {
        //구형 파일에 없던 목록만 생성하고 읽은 기록은 유지
        if ( _stageRecords == null )
            _stageRecords = new List<StageClearRecord> ( );
    }

    /// <summary>
    /// 현재 저장 버전 반영
    /// </summary>
    public void SetCurrentVersion ()
    {
        _version = CurrentVersion;
    }

    /// <summary>
    /// 빈 슬롯에 사용할 신규 저장 데이터 생성
    /// </summary>
    /// <param name="balance">최초 영구 재화</param>
    /// <returns>현재 형식의 신규 데이터</returns>
    public static SaveData CreateNew ( int balance )
    {
        //신규 플레이에만 기본값을 채우며 외부 JSON의 누락 보완에는 사용하지 않음
        return new SaveData
        {
            _version = CurrentVersion ,
            _permanentCurrencyBalance = balance ,
            _items = new List<ItemSaveData> ( ) ,
            _upgrades = new List<UpgradeSaveData> ( ) ,
            _missionCounters = new List<MissionKillCountSaveData> ( ) ,
            _missions = new List<MissionSaveData> ( ) ,
            _collectionUnlockedIds = new List<string> ( ) ,
            _collectionNewIds = new List<string> ( ) ,
            _unlockedWeapons = new List<WeaponType> ( ) ,
            _purchasedInstallationIds = new List<string> ( ) ,
            _hasSelectedWeapon = false ,
            _selectedWeapon = default ,
            _stageRecords = new List<StageClearRecord> ( ) ,
            _starRewards = new List<StarRewardRecord> ( )
        };
    }

    /// <summary>
    /// 저장 후보의 클리어 기록 추가/교체
    /// </summary>
    /// <param name="record">반영할 기록</param>
    public void SetStageClearRecord ( StageClearRecord record )
    {
        //같은 맵/난이도의 기록을 교체하며 최고 별 비교는 저장 서비스에서 수행
        for ( int i = 0 ; i < _stageRecords.Count ; i++ )
        {
            StageClearRecord current = _stageRecords [ i ];

            if ( current.MapId != record.MapId
                || current.Difficulty != record.Difficulty )
                continue;

            _stageRecords [ i ] = record;
            return;
        }

        _stageRecords.Add ( record );
    }

    /// <summary>
    /// 저장 후보의 영구 재화 설정
    /// </summary>
    /// <param name="balance">변경 후 잔액</param>
    public void SetPermanentBalance ( int balance )
    {
        _permanentCurrencyBalance = balance;
    }

    /// <summary>
    /// 무기 해금 기록 추가
    /// </summary>
    /// <param name="weaponType">무기 타입</param>
    public void AddWeaponUnlock ( WeaponType weaponType )
    {
        _unlockedWeapons.Add ( weaponType );
    }

    /// <summary>
    /// 설치물 구매 기록 추가
    /// </summary>
    /// <param name="installationId">설치물 ID</param>
    public void AddInstallationPurchase ( string installationId )
    {
        _purchasedInstallationIds.Add ( installationId );
    }

    /// <summary>
    /// 선택 무기 설정
    /// </summary>
    /// <param name="weaponType">무기 타입</param>
    public void SetSelectedWeapon ( WeaponType weaponType )
    {
        _hasSelectedWeapon = true;
        _selectedWeapon = weaponType;
    }

    /// <summary>
    /// 저장 후보의 아이템 기록 교체 또는 추가
    /// </summary>
    /// <param name="itemId">아이템 ID</param>
    /// <param name="count">변경 후 수량</param>
    public void SetItemCount ( string itemId , int count )
    {
        ItemSaveData replacement = new ItemSaveData ( itemId , count );

        for ( int i = 0 ; i < _items.Count ; i++ )
        {
            if ( _items [ i ].ItemId != itemId )
                continue;

            _items [ i ] = replacement;
            return;
        }

        _items.Add ( replacement );
    }

    /// <summary>
    /// 저장 후보의 강화 기록 교체 또는 추가
    /// </summary>
    /// <param name="upgradeId">강화 ID</param>
    /// <param name="level">변경 후 단계</param>
    public void SetUpgradeLevel ( string upgradeId , int level )
    {
        UpgradeSaveData replacement = new UpgradeSaveData ( upgradeId , level );

        for ( int i = 0 ; i < _upgrades.Count ; i++ )
        {
            if ( _upgrades [ i ].UpgradeId != upgradeId )
                continue;

            _upgrades [ i ] = replacement;
            return;
        }

        _upgrades.Add ( replacement );
    }

    /// <summary>
    /// 변경 후보로 사용할 데이터 복사
    /// </summary>
    /// <returns>목록을 분리한 변경 후보</returns>
    public SaveData Copy ( )
    {
        //기록 객체는 변경하지 않고 교체하므로 목록만 분리
        return new SaveData
        {
            _version = this._version ,
            _permanentCurrencyBalance = this._permanentCurrencyBalance ,
            _items = new List<ItemSaveData> ( _items ) ,
            _upgrades = new List<UpgradeSaveData> ( _upgrades ) ,
            _missionCounters = new List<MissionKillCountSaveData> ( _missionCounters ) ,
            _missions = new List<MissionSaveData> ( _missions ) ,
            _collectionUnlockedIds = new List<string> ( _collectionUnlockedIds ) ,
            _collectionNewIds = new List<string> ( _collectionNewIds ) ,
            _unlockedWeapons = new List<WeaponType> ( _unlockedWeapons ) ,
            _purchasedInstallationIds = new List<string> ( _purchasedInstallationIds ) ,
            _hasSelectedWeapon = _hasSelectedWeapon ,
            _selectedWeapon = _selectedWeapon ,
            _stageRecords = new List<StageClearRecord> ( _stageRecords ) ,
            _starRewards = new List<StarRewardRecord> ( _starRewards ) ,
            _hasClearReward = _hasClearReward ,
            _lastClearReward = _lastClearReward
        };
    }

    /// <summary>
    /// 후보의 처치 집계와 미션 완료 기록 교체
    /// </summary>
    /// <param name="killCounts">처치 집계</param>
    /// <param name="missions">완료/지급 기록</param>
    public void SetMissionProgress (
        IReadOnlyList<MissionKillCountSaveData> killCounts ,
        IReadOnlyList<MissionSaveData> missions )
    {
        //호출부의 진행 목록과 후보 목록을 분리해 저장 실패 시 원본 유지
        _missionCounters = new List<MissionKillCountSaveData> ( killCounts );
        _missions = new List<MissionSaveData> ( missions );
    }

    /// <summary>
    /// 저장 후보에 선정된 최초 해금/신규 기록 추가
    /// </summary>
    /// <param name="enemyIds">중복과 기존 해금을 제외한 ID</param>
    public void AddCollectionUnlocks ( IReadOnlyList<string> enemyIds )
    {
        //최초 해금한 적은 해금 목록과 미확인 신규 목록에 함께 반영
        _collectionUnlockedIds.AddRange ( enemyIds );
        _collectionNewIds.AddRange ( enemyIds );
    }

    /// <summary>
    /// 저장 후보에서 확인한 신규 표시만 제거
    /// </summary>
    /// <param name="enemyIds">확인 대상으로 선정한 ID</param>
    public void RemoveCollectionNew ( IReadOnlyList<string> enemyIds )
    {
        foreach ( string enemyId in enemyIds )
            _collectionNewIds.Remove ( enemyId );
    }
}
