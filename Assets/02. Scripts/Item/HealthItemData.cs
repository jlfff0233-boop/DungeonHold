using UnityEngine;

/// <summary>
/// 체력 아이템 데이터 - 체력 회복 조건과 적용
/// </summary>
[CreateAssetMenu ( fileName = "HealthItemData" , menuName = "Item/HealthItemData" )]
public class HealthItemData : RecoveryItemData
{
    /// <summary>
    /// 체력 회복 필요 여부 확인
    /// </summary>
    /// <param name="context">사용 대상의 모듈</param>
    /// <returns>체력이 최대치보다 적은지 여부</returns>
    public override bool CanApply ( ItemUseContext context )
    {
        return context.Health.CurrentHp < context.Health.MaxHp;
    }

    /// <summary>
    /// 체력 회복
    /// </summary>
    /// <param name="context">사용 대상의 모듈</param>
    /// <returns>실제 회복량</returns>
    public override float Apply ( ItemUseContext context )
    {
        return context.Health.Heal ( RecoveryAmount );
    }
}
