using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설치물 공격 주기 강화 데이터 - 단계별 비용과 효과 설정
/// </summary>
[CreateAssetMenu(
    fileName = "InstallationIntervalUpgradeData", menuName = "Upgrade/Installation/Interval" )]
public class InstallationIntervalUpgradeData : InstallationUpgradeData
{
    /// <summary>
    /// 강화 단계 - 공격 주기 단축률 설정
    /// </summary>
    [Serializable]
    class LevelData : UpgradeLevelData
    {
        [SerializeField] float _intervalReduction;    //공격 주기 단축률

        /// <summary>
        /// 공격 주기 단축률
        /// </summary>
        public float IntervalReduction => _intervalReduction;

        /// <inheritdoc />
        public override bool ValidateEffect ()
        {
            return _intervalReduction >= 0f && _intervalReduction < 1f;
        }
    }

    [SerializeField] LevelData [ ] _levels;    //Lv1부터의 강화 단계

    /// <inheritdoc />
    protected override InstallationUpgradeSupport RequiredSupport
        => InstallationUpgradeSupport.Interval;

    /// <inheritdoc />
    protected override IReadOnlyList<UpgradeLevelData> GetLevels ()
    {
        return _levels;
    }

    /// <summary>
    /// 현재 단계의 공격 주기 단축률 조회
    /// </summary>
    /// <param name="level">현재 단계</param>
    /// <returns>기본 공격 주기에서 줄일 비율</returns>
    public float GetIntervalReduction ( int level )
    {
        return level == 0 ? 0f : _levels [ level - 1 ].IntervalReduction;
    }

    /// <inheritdoc />
    public override void ApplyToStats (
        int level,
        ref InstallationUpgradeStats stats )
    {
        stats.SetIntervalReduction( GetIntervalReduction( level ) );
    }
}
