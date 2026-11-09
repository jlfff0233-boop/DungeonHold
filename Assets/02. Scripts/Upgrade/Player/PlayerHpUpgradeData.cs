using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 최대 HP 강화 데이터 - 단계별 비용과 효과 설정
/// </summary>
[CreateAssetMenu( fileName = "PlayerHpUpgradeData", menuName = "Upgrade/Player/MaxHp" )]
public class PlayerHpUpgradeData : UpgradeData
{
    /// <summary>
    /// 강화 단계 - 최대 HP 추가량 설정
    /// </summary>
    [Serializable]
    class LevelData : UpgradeLevelData
    {
        [SerializeField] float _maxHpBonus;    //최대 HP 추가량

        /// <summary>
        /// 최대 HP 추가량
        /// </summary>
        public float MaxHpBonus => _maxHpBonus;

        /// <inheritdoc />
        public override bool ValidateEffect ()
        {
            return _maxHpBonus >= 0f;
        }
    }

    [SerializeField] LevelData[] _levels;    //Lv1부터의 강화 단계

    /// <inheritdoc />
    protected override IReadOnlyList<UpgradeLevelData> GetLevels ()
    {
        return _levels;
    }

    /// <summary>
    /// 현재 단계의 최대 HP 추가량 조회
    /// </summary>
    /// <param name="level">현재 단계, 미강화는 0</param>
    /// <returns>기본 최대 HP에 더할 값</returns>
    public float GetMaxHpBonus ( int level )
    {
        return level == 0 ? 0f : _levels[level - 1].MaxHpBonus;
    }
}
