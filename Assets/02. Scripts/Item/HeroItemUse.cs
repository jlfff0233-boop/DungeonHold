using System;
using UnityEngine;

/// <summary>
/// 아이템 사용 결과
/// </summary>
public enum ItemUseResult
{
    Success,        //사용 성공 또는 사용 조건 충족
    Unavailable,        //현재 씬 진행 상태에서 사용 불가
    UnknownItem,        //등록되지 않은 아이템
    OnCooldown,     //재사용 대기 중
    CannotUse,      //플레이어 상태 또는 효과 조건 불충족
    NoItem,     //보유 수량 없음
    SaveFailed      //감소 수량 저장 실패
}

/// <summary>
/// 플레이어 아이템 사용 - 검사, 저장과 효과 적용 순서 조율
/// </summary>
public class HeroItemUse
{
    Hero _hero;    //사용 대상
    ItemDataMap _dataMap;    //아이템 설정
    ItemInventory _inventory;    //아이템 런타임 상태
    SaveService _saveService;    //현재 슬롯 저장 서비스
    SoundManager _sound; //사용 성공 효과음

    /// <summary>
    /// 아이템 사용 확정 이벤트(아이템 ID)
    /// </summary>
    public event Action<string> OnUseCommitted;

    /// <summary>
    /// 아이템 사용에 필요한 모듈 연결
    /// </summary>
    /// <param name="hero">사용 대상</param>
    /// <param name="dataMap">아이템 설정</param>
    /// <param name="inventory">아이템 런타임 상태</param>
    /// <param name="saveService">현재 슬롯 저장 서비스</param>
    /// <param name="sound">공통 사운드 서비스</param>
    public HeroItemUse (
        Hero hero,
        ItemDataMap dataMap,
        ItemInventory inventory,
        SaveService saveService,
        SoundManager sound )
    {
        _hero = hero;
        _dataMap = dataMap;
        _inventory = inventory;
        _saveService = saveService;
        _sound = sound;
    }

    /// <summary>
    /// 사용 가능 여부 조회
    /// </summary>
    /// <param name="itemId">아이템 아이디</param>
    /// <param name="currentTime">현재 게임 시간</param>
    /// <returns>사용 조건 확인 결과</returns>
    public ItemUseResult GetUseState (
        string itemId, double currentTime )
    {
        return PrepareUse( itemId, currentTime, out _, out _ );
    }

    /// <summary>
    /// 아이템 사용 및 효과 적용
    /// </summary>
    /// <param name="itemId">사용할 아이템 아이디</param>
    /// <param name="currentTime">현재 게임 시간</param>
    /// <param name="appliedAmount">실제 효과 적용량</param>
    /// <returns>사용 결과</returns>
    public ItemUseResult TryUse (
        string itemId,
        double currentTime,
        out float appliedAmount )
    {
        appliedAmount = 0f;

        ItemUseResult result = PrepareUse(
            itemId, currentTime, 
            out ItemData item, out int nextCount );

        if ( result != ItemUseResult.Success )
        {
            if ( result == ItemUseResult.UnknownItem )
                Debug.LogError( $"등록되지 않은 아이템 사용 요청: {itemId}" );

            return result;
        }

        if ( _saveService.TrySaveItemCount( itemId, nextCount ) == false )
            return ItemUseResult.SaveFailed;

        //회복 이벤트 전에 수량, 쿨타임과 사용 기록을 모두 반영
        _inventory.ApplyUse( item, nextCount, currentTime );

        //회복 알림보다 먼저 사용 이력 전달
        OnUseCommitted?.Invoke( itemId );

        appliedAmount = _hero.ApplyItemEffect( item );

        //수량과 사용 기록을 유지하고 실제 적용 뒤 표현 요청
        _hero.PlayItemVfx( item.UseVfx );

        //수량 저장과 실제 효과 적용 이후에만 사용음 요청
        _sound.Play( SoundId.ItemUse );

        _inventory.NotifyChanged( );
        return ItemUseResult.Success;
    }

    /// <summary>
    /// 사용 가능 여부 확인
    /// </summary>
    /// <param name="itemId">아이템 아이디</param>
    /// <param name="currentTime">현재 게임 시간</param>
    /// <param name="item">조회한 설정</param>
    /// <param name="nextCount">사용 후 수량</param>
    /// <returns>사용 조건 확인 결과</returns>
    ItemUseResult PrepareUse (
        string itemId,
        double currentTime,
        out ItemData item,
        out int nextCount )
    {
        nextCount = 0;

        if ( _dataMap.TryGet( itemId, out item ) == false )
            return ItemUseResult.UnknownItem;

        if ( _inventory.GetRemainingCooldown( itemId, currentTime ) > 0d )
            return ItemUseResult.OnCooldown;

        if ( _hero.CanUseItem( item ) == false )
            return ItemUseResult.CannotUse;

        if ( _inventory.TryGetCountAfterUse( itemId, out nextCount ) == false )
            return ItemUseResult.NoItem;

        return ItemUseResult.Success;
    }
}
