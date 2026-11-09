using UnityEngine;

/// <summary>
/// 공용 슬롯 실행 연결 - 설치물 선택 요청, 동료 고용과 아이템 사용 전달
/// </summary>
public class QuickSlotContext
{
    CompanionHireController _hireController;    //동료 고용 처리
    HeroItemUse _itemUse;    //아이템 사용 처리
    BuildPreviewController _buildPreviewController;    //설치물 선택 처리
    EquipmentUnlock _equipmentUnlock;    //해금 상태

    /// <summary>
    /// 슬롯 실행에 필요한 기존 모듈 연결
    /// </summary>
    /// <param name="hireController">동료 고용 처리</param>
    /// <param name="itemUse">아이템 사용 처리</param>
    /// <param name="buildPreviewController">설치물 선택 처리</param>
    /// <param name="equipmentUnlock">해금 상태</param>
    public QuickSlotContext (
        CompanionHireController hireController,
        HeroItemUse itemUse,
        BuildPreviewController buildPreviewController ,
        EquipmentUnlock equipmentUnlock )
    {
        _hireController = hireController;
        _itemUse = itemUse;
        _buildPreviewController = buildPreviewController;
        _equipmentUnlock = equipmentUnlock;
    }

    /// <summary>
    /// 슬롯에 등록된 설치물 선택 요청
    /// </summary>
    /// <param name="data">선택을 요청할 설치물 설정</param>
    /// <returns>설치물 선택 성공 여부</returns>
    public bool TrySelectInstallation ( TrapData data )
    {
        if ( _buildPreviewController == null )
            return false;

        if ( _equipmentUnlock.IsInstallationUnlocked ( data.TrapId ) == false )
            return false;

        return _buildPreviewController.TrySelectInstallation( data );
    }

    /// <summary>
    /// 타입별 동료 고용 또는 재고용 요청
    /// </summary>
    /// <param name="weaponType">동료 무기 타입</param>
    /// <returns>고용 성공 여부</returns>
    public bool TryHireCompanion ( WeaponType weaponType )
    {
        return TryHireCompanion ( weaponType , out _ , out _ );
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
        bool hired = _hireController.TryHireOrRehire (
            _hireController.CurrentStageId , weaponType ,
            out hireId , out result );

        Debug.Log (
            $"동료 고용/재고용: 타입={weaponType}"
            + $" / 결과={result} / 고용 번호={hireId}" );
        return hired;
    }

    /// <summary>
    /// 기존 저장과 효과 적용 경로로 아이템 사용
    /// </summary>
    /// <param name="itemId">아이템 식별자</param>
    /// <returns>사용 성공 여부</returns>
    public bool TryUseItem ( string itemId )
    {
        ItemUseResult result = _itemUse.TryUse(
            itemId, Time.timeAsDouble, out float appliedAmount );

        Debug.Log(
            $"아이템 사용: ID={itemId}"
            + $" / 결과={result} / 실제 적용량={appliedAmount}" );
        return result == ItemUseResult.Success;
    }
}
