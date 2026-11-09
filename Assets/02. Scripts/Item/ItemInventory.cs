using System;
using System.Collections.Generic;

/// <summary>
/// 아이템 인벤토리 - 보유 수량, 재사용 상태와 도전 사용 기록 관리
/// </summary>
public class ItemInventory
{
    /// <summary>
    /// 아이템별 보유 수량(아이템 ID, 수량)
    /// </summary>
    Dictionary<string, int> _counts = new( );
    /// <summary>
    /// 아이템별 다음 사용 가능 시각(아이템 ID, 사용 가능 시각)
    /// </summary>
    Dictionary<string, double> _nextUseTimes = new( );
    int _usedItemCount;    //현재 도전에서 성공한 사용 횟수

    /// <summary>
    /// 현재 도전의 아이템 사용 횟수
    /// </summary>
    public int UsedItemCount => _usedItemCount;

    /// <summary>
    /// 아이템 상태 변경 이벤트
    /// </summary>
    public event Action OnChanged;

    /// <summary>
    /// 검사가 끝난 저장 수량 적용
    /// </summary>
    /// <param name="items">아이템 수량</param>
    public void Init ( IReadOnlyList<ItemSaveData> items )
    {
        _counts.Clear( );

        foreach ( ItemSaveData item in items )
            _counts.Add( item.ItemId, item.Count );

        ResetStageState( );
    }

    /// <summary>
    /// 보유 수량을 유지하고 새 도전의 사용 상태 초기화
    /// </summary>
    public void ResetStageState ()
    {
        _nextUseTimes.Clear( );
        _usedItemCount = 0;
    }

    /// <summary>
    /// 보유 수량 조회
    /// </summary>
    /// <param name="itemId">아이템 식별자</param>
    /// <returns>보유 수량, 기록이 없으면 0</returns>
    public int GetCount ( string itemId )
    {
        return _counts.TryGetValue( itemId, out int count ) == true
            ? count
            : 0;
    }

    /// <summary>
    /// 남은 재사용 대기시간 조회
    /// </summary>
    /// <param name="itemId">아이템 식별자</param>
    /// <param name="currentTime">현재 게임 시간</param>
    /// <returns>남은 시간, 사용 가능하면 0</returns>
    public double GetRemainingCooldown ( 
        string itemId, double currentTime )
    {
        if ( _nextUseTimes.TryGetValue( 
            itemId, out double nextUseTime ) == false )
            return 0d;

        double remainingTime = nextUseTime - currentTime;
        return remainingTime > 0d ? remainingTime : 0d;
    }

    /// <summary>
    /// 한 개 사용 후의 수량 계산
    /// </summary>
    /// <param name="itemId">사용할 아이템 식별자</param>
    /// <param name="nextCount">사용 후 수량</param>
    /// <returns>사용할 수량 보유 여부</returns>
    public bool TryGetCountAfterUse (
        string itemId, out int nextCount )
    {
        nextCount = 0;

        int count = GetCount( itemId );

        if ( count <= 0 )
            return false;

        nextCount = count - 1;
        return true;
    }

    /// <summary>
    /// 보유 상한 안에서 한 개 추가한 수량 계산
    /// </summary>
    /// <param name="item">추가할 아이템 설정</param>
    /// <param name="nextCount">추가 후 수량</param>
    /// <returns>한 개 추가 가능 여부</returns>
    public bool TryGetCountAfterAdd ( ItemData item, out int nextCount )
    {
        nextCount = 0;

        int count = GetCount( item.ItemId );

        if ( count >= item.MaxCount )
            return false;

        nextCount = count + 1;
        return true;
    }

    /// <summary>
    /// 저장에 성공한 수량 반영
    /// </summary>
    /// <param name="itemId">아이템 식별자</param>
    /// <param name="count">저장된 수량</param>
    public void ApplySavedCount ( string itemId, int count )
    {
        _counts [ itemId ] = count;
    }

    /// <summary>
    /// 저장에 성공한 사용의 런타임 상태 반영
    /// </summary>
    /// <param name="item">사용한 아이템 설정</param>
    /// <param name="nextCount">저장된 사용 후 수량</param>
    /// <param name="currentTime">사용 시점의 게임 시간</param>
    public void ApplyUse (
        ItemData item, int nextCount, double currentTime )
    {
        ApplySavedCount( item.ItemId, nextCount );
        _nextUseTimes [ item.ItemId ] = currentTime + item.Cooldown;
        _usedItemCount++;
    }

    /// <summary>
    /// 관련 상태 적용 후 재조회 알림
    /// </summary>
    public void NotifyChanged ()
    {
        OnChanged?.Invoke( );
    }
}
