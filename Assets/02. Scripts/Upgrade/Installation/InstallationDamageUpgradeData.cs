using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설치물 피해 강화 데이터 - 단계별 비용과 효과 설정
/// </summary>
[CreateAssetMenu(
    fileName = "InstallationDamageUpgradeData", menuName = "Upgrade/Installation/Damage" )]
public class InstallationDamageUpgradeData : InstallationUpgradeData
{
    /// <summary>
    /// 강화 단계 - 피해 증가율 설정
    /// </summary>
    [Serializable]
    class LevelData : UpgradeLevelData
    {
        [SerializeField] float _damageBonus;    //피해 증가율

        /// <summary>
        /// 피해 증가율
        /// </summary>
        public float DamageBonus => _damageBonus;

        /// <inheritdoc />
        public override bool ValidateEffect ()
        {
            return _damageBonus >= 0f;
        }
    }

    [SerializeField] LevelData [ ] _levels;    //Lv1부터의 강화 단계

    /// <inheritdoc />
    protected override InstallationUpgradeSupport RequiredSupport
        => InstallationUpgradeSupport.Damage;

    /// <inheritdoc />
    protected override IReadOnlyList<UpgradeLevelData> GetLevels ()
    {
        return _levels;
    }

    /// <summary>
    /// 현재 단계의 피해 증가율 조회
    /// </summary>
    /// <param name="level">현재 단계, 미강화는 0</param>
    /// <returns>기본 피해 대비 증가율</returns>
    public float GetDamageBonus ( int level )
    {
        return level == 0 ? 0f : _levels [ level - 1 ].DamageBonus;
    }

    /// <inheritdoc />
    public override void ApplyToStats (
        int level, ref InstallationUpgradeStats stats )
    {
        stats.SetDamageBonus( GetDamageBonus( level ) );
    }
}
