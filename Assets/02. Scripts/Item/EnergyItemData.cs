using UnityEngine;

/// <summary>
/// 에너지 아이템 데이터 - 에너지 충전 조건과 적용
/// </summary>
[CreateAssetMenu ( fileName = "EnergyItemData" , menuName = "Item/EnergyItemData" )]
public class EnergyItemData : RecoveryItemData
{
    /// <summary>
    /// 에너지 충전 필요 여부 확인
    /// </summary>
    /// <param name="context">사용 대상의 모듈</param>
    /// <returns>에너지가 최대치보다 적은지 여부</returns>
    public override bool CanApply ( ItemUseContext context )
    {
        return context.Energy.CurrentEnergy < context.Energy.MaxEnergy;
    }

    /// <summary>
    /// 에너지 충전
    /// </summary>
    /// <param name="context">사용 대상의 모듈</param>
    /// <returns>실제 충전량</returns>
    public override float Apply ( ItemUseContext context )
    {
        return context.Energy.Add ( RecoveryAmount );
    }
}
