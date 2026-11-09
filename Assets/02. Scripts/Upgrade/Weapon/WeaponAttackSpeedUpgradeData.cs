using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 무기 기본 공격속도 강화 데이터 - 단계별 비용과 효과 설정
/// </summary>
[CreateAssetMenu( fileName = "WeaponAttackSpeedUpgradeData", menuName = "Upgrade/Weapon/AttackSpeed" )]
public class WeaponAttackSpeedUpgradeData : WeaponUpgradeData
{
    /// <summary>
    /// 강화 단계 - 기본 공격속도 증가율 설정
    /// </summary>
    [Serializable]
    class LevelData : UpgradeLevelData
    {
        [SerializeField] float _attackSpeedBonus;    //기본 공격속도 증가율

        /// <summary>
        /// 기본 공격속도 증가율
        /// </summary>
        public float AttackSpeedBonus => _attackSpeedBonus;

        /// <inheritdoc />
        public override bool ValidateEffect ()
        {
            return _attackSpeedBonus >= 0f;
        }
    }

    [SerializeField] LevelData[] _levels;    //Lv1부터의 강화 단계

    /// <inheritdoc />
    protected override IReadOnlyList<UpgradeLevelData> GetLevels ()
    {
        return _levels;
    }

    /// <summary>
    /// 현재 단계의 기본 공격속도 증가율 조회
    /// </summary>
    /// <param name="level">현재 단계, 미강화는 0</param>
    /// <returns>기본 공격속도 대비 증가율</returns>
    public float GetAttackSpeedBonus ( int level )
    {
        return level == 0 ? 0f : _levels[level - 1].AttackSpeedBonus;
    }
}
