using UnityEngine;

/// <summary>
/// 공용 슬롯 데이터 - 설정 검사와 실행 계약
/// </summary>
public abstract class QuickSlotData : ScriptableObject
{
    /// <summary>
    /// 등록된 슬롯 설정 확인
    /// </summary>
    /// <param name="items">아이템 설정 목록</param>
    /// <param name="companions">동료 고용 설정 목록</param>
    /// <returns>설정 사용 가능 여부</returns>
    public abstract bool Validate (
        ItemDataMap items , CompanionHireDataMap companions );

    /// <summary>
    /// 슬롯에 지정한 기능 실행
    /// </summary>
    /// <param name="context">기존 시스템 실행 연결</param>
    /// <returns>실행 성공 여부</returns>
    public abstract bool TryExecute ( QuickSlotContext context );
}
