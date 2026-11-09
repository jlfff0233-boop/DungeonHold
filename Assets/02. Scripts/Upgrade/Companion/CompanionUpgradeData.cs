using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 동료 강화 데이터 - 동료별 HP와 직접 피해 설정
/// </summary>
[CreateAssetMenu( fileName = "CompanionUpgradeData", menuName = "Upgrade/Companion/Stats" )]
public class CompanionUpgradeData : UpgradeData
{
    /// <summary>
    /// 강화 단계 - 동료 HP와 직접 피해 증가율
    /// </summary>
    [Serializable]
    class LevelData : UpgradeLevelData
    {
        [SerializeField] float _hpBonus;        //최대 HP 증가율
        [SerializeField] float _damageBonus;    //직접 피해 증가율

        /// <summary>
        /// 최대 HP 증가율
        /// </summary>
        public float HpBonus => _hpBonus;

        /// <summary>
        /// 직접 피해 증가율
        /// </summary>
        public float DamageBonus => _damageBonus;

        /// <inheritdoc />
        public override bool ValidateEffect ()
        {
            return _hpBonus >= 0f && _damageBonus >= 0f;
        }
    }

    [SerializeField] CompanionData _companion;    //강화 대상 동료
    [SerializeField] LevelData[] _levels;        //Lv1부터의 강화 단계

    /// <summary>
    /// 강화 대상 동료
    /// </summary>
    public CompanionData Companion => _companion;

    /// <inheritdoc />
    protected override IReadOnlyList<UpgradeLevelData> GetLevels ()
    {
        return _levels;
    }

    /// <inheritdoc />
    protected override bool ValidateTarget ()
    {
        return _companion != null;
    }

    /// <summary>
    /// 현재 단계의 동료 강화 결과 조회
    /// </summary>
    /// <param name="level">현재 단계, 미강화는 0</param>
    /// <returns>기본 HP와 피해 대비 증가율</returns>
    public CompanionUpgradeStats GetStats ( int level )
    {
        if ( level == 0 )
            return default;

        LevelData data = _levels[level - 1];
        return new CompanionUpgradeStats( data.HpBonus, data.DamageBonus );
    }
}
