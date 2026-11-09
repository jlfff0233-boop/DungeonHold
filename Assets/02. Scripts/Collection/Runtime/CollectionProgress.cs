using System;
using System.Collections.Generic;

/// <summary>
/// 도감 진행 - 슬롯별 해금/신규 기록과 잠금 상태별 표시 조회
/// </summary>
public class CollectionProgress
{
    EnemyCollectionDataMap _map;    //검사된 도감 설정
    HashSet<string> _unlockedIds = new HashSet<string>( );    //저장된 해금 ID
    HashSet<string> _newIds = new HashSet<string>( );    //저장된 미확인 ID

    /// <summary>
    /// 확정 상태 변경 이벤트
    /// </summary>
    public event Action OnChanged;

    /// <summary>
    /// 검사된 저장 기록을 복원하고 조회 설정 연결
    /// </summary>
    /// <param name="unlockedIds">해금 ID</param>
    /// <param name="newIds">미확인 신규 ID</param>
    /// <param name="map">검사된 도감 맵</param>
    public void Init (
        IReadOnlyList<string> unlockedIds,
        IReadOnlyList<string> newIds,
        EnemyCollectionDataMap map )
    {
        _map = map;
        //파일 형식은 SaveService가 검사
        //현재 맵에서 빠진 ID도 기록에는 보존
        _unlockedIds = new HashSet<string>( unlockedIds );
        _newIds = new HashSet<string>( newIds );
    }

    /// <summary>
    /// 고정 적 ID의 해금 여부 조회
    /// </summary>
    /// <param name="enemyId">고정 적 ID</param>
    /// <returns>해금 여부</returns>
    public bool IsUnlocked ( string enemyId )
    {
        return _unlockedIds.Contains( enemyId );
    }

    /// <summary>
    /// 고정 적 ID의 미확인 여부 조회
    /// </summary>
    /// <param name="enemyId">고정 적 ID</param>
    /// <returns>신규 여부</returns>
    public bool IsNew ( string enemyId )
    {
        return _newIds.Contains( enemyId );
    }

    /// <summary>
    /// 저장에 성공한 해금/신규 기록 반영
    /// </summary>
    /// <param name="enemyIds">이번에 저장한 최초 해금 ID</param>
    public void ApplySavedUnlocks ( IReadOnlyList<string> enemyIds )
    {
        foreach ( string enemyId in enemyIds )
        {
            _unlockedIds.Add( enemyId );
            _newIds.Add( enemyId );
        }
    }

    /// <summary>
    /// 저장에 성공한 확인 대상만 신규 해제
    /// </summary>
    /// <param name="enemyIds">확인 저장한 ID</param>
    public void ApplySavedSeen ( IReadOnlyList<string> enemyIds )
    {
        foreach ( string enemyId in enemyIds )
            _newIds.Remove( enemyId );
    }

    /// <summary>
    /// 등록 순서의 도감 표시 목록 생성
    /// </summary>
    /// <returns>현재 해금 상태에 맞는 읽기 전용 목록</returns>
    public IReadOnlyList<CollectionEntryInfo> GetEntries ()
    {
        List<CollectionEntryInfo> entries = new List<CollectionEntryInfo>( );

        foreach ( EnemyCollectionData data in _map.Entries )
            entries.Add( new CollectionEntryInfo(
                data, IsUnlocked( data.EnemyId ), IsNew( data.EnemyId ) ) );

        return entries.AsReadOnly( );
    }

    /// <summary>
    /// 등록된 항목의 표시 정보 조회
    /// </summary>
    /// <param name="enemyId">고정 적 ID</param>
    /// <param name="info">잠금 상태에 맞는 표시 정보</param>
    /// <returns>등록 여부</returns>
    public bool TryGetEntryInfo ( string enemyId, out CollectionEntryInfo info )
    {
        info = null;
        if ( _map.TryGet( enemyId,
            out EnemyCollectionData data ) == false )
            return false;

        info = new CollectionEntryInfo(
            data, IsUnlocked( enemyId ), IsNew( enemyId ) );

        return true;
    }

    /// <summary>
    /// 상태 적용을 끝낸 뒤 최초/변경 표시 요청
    /// </summary>
    public void NotifyChanged ()
    {
        OnChanged?.Invoke( );
    }
}
