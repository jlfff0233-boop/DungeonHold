using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설치물 감속 강화 데이터 - 단계별 비용과 감속 추가량 설정
/// </summary>
[CreateAssetMenu(
    fileName = "InstallationSlowUpgradeData", menuName = "Upgrade/Installation/Slow" )]
public class InstallationSlowUpgradeData : InstallationUpgradeData
{
    /// <summary>
    /// 강화 단계 - 감속률 추가량 설정
    /// </summary>
    [Serializable]
    class LevelData : UpgradeLevelData
    {
        [SerializeField] float _slowRateBonus;    //기본 감속률에 더할 추가량

        /// <summary>
        /// 감속률 추가량
        /// </summary>
        public float SlowRateBonus => _slowRateBonus;

        /// <inheritdoc />
        public override bool ValidateEffect ()
        {
            return _slowRateBonus >= 0f && _slowRateBonus <= 1f;
        }
    }

    [SerializeField] LevelData [ ] _levels;    //Lv1부터의 강화 단계

    /// <inheritdoc />
    protected override InstallationUpgradeSupport RequiredSupport
        => InstallationUpgradeSupport.Slow;

    /// <inheritdoc />
    protected override IReadOnlyList<UpgradeLevelData> GetLevels ()
    {
        return _levels;
    }

    /// <summary>
    /// 현재 단계의 감속률 추가량 조회
    /// </summary>
    /// <param name="level">현재 단계</param>
    /// <returns>기본 감속률에 더할 추가량</returns>
    public float GetSlowRateBonus ( int level )
    {
        return level == 0 ? 0f : _levels [ level - 1 ].SlowRateBonus;
    }

    /// <inheritdoc />
    public override void ApplyToStats (
        int level,
        ref InstallationUpgradeStats stats )
    {
        stats.SetSlowRateBonus( GetSlowRateBonus( level ) );
    }

    /// <inheritdoc />
    protected override bool ValidateEffectCompatibility ( out string error )
    {
        error = null;

        IInstallationSlowSource source =
            Installation.Prefab.GetComponent<IInstallationSlowSource>( );

        if ( source == null )
        {
            error = "설치물의 기본 감속 설정 제공 연결이 필요합니다.";
            return false;
        }

        SlowEffectData slowData = source.SlowData;

        if ( slowData == null )
        {
            error = "설치물이 사용하는 기본 감속 설정이 필요합니다.";
            return false;
        }

        float baseSlowRate = slowData.SlowRate;
        bool isValidBaseRate = baseSlowRate >= 0f && baseSlowRate <= 1f;

        if ( isValidBaseRate == false )
        {
            error = "설치물의 기본 감속률은 0~1이어야 합니다.";
            return false;
        }

        //기본 검사에서 확인한 개별 단계는 다시 검사하지 않음
        //여기서는 실제 기본값과 강화 추가량의 합계만 확인
        for ( int i = 0; i < _levels.Length; i++ )
        {
            float slowRate = InstallationUpgradeStats.CalculateSlowRate(
                baseSlowRate, _levels [ i ].SlowRateBonus );

            if ( slowRate <= 1f )
                continue;

            error = $"Lv{i + 1}의 감속률은 1 이하여야 합니다. "
                + $"기본={baseSlowRate:R} / 추가={_levels [ i ].SlowRateBonus:R} / 계산={slowRate:R}";
            return false;
        }

        return true;
    }
}
