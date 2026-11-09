using System;
using UnityEngine;

/// <summary>
/// 강화 단계 - 공통 구매 비용
/// </summary>
[Serializable]
public abstract class UpgradeLevelData
{
    [SerializeField] int _price;    //해당 단계의 구매 비용

    /// <summary>
    /// 해당 단계의 구매 비용
    /// </summary>
    public int Price => _price;

    /// <summary>
    /// 해당 단계의 효과 설정 검사
    /// </summary>
    /// <returns>효과 설정 사용 가능 여부</returns>
    public abstract bool ValidateEffect ( );
}
