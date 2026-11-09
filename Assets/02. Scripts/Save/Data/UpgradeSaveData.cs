using System;
using UnityEngine;

/// <summary>
/// 강화 저장 데이터 - 고정 ID와 구매 단계
/// </summary>
[Serializable]
public class UpgradeSaveData
{
    [SerializeField] string _upgradeId;     //강화 고정 ID
    [SerializeField] int _level;        //구매 단계

    /// <summary>
    /// 강화 고정 ID
    /// </summary>
    public string UpgradeId => _upgradeId;

    /// <summary>
    /// 구매 단계
    /// </summary>
    public int Level => _level;

    /// <summary>
    /// 저장할 강화 기록 생성
    /// </summary>
    /// <param name="upgradeId">강화 ID</param>
    /// <param name="level">구매 단계</param>
    public UpgradeSaveData ( string upgradeId, int level )
    {
        _upgradeId = upgradeId;
        _level = level;
    }
}
