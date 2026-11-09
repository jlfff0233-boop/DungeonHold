#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 강화 검증 대상 - 임시 지원 정보 제공
/// </summary>
public class UpgradeVerificationTarget : MonoBehaviour,
    IInstallationUpgradeTarget, IInstallationSlowSource
{
    InstallationUpgradeSupport _supportedUpgrades;    //시험할 지원 종류
    SlowEffectData _slowData;    //시험할 기본 감속 설정

    /// <inheritdoc />
    public InstallationUpgradeSupport SupportedUpgrades => _supportedUpgrades;

    /// <inheritdoc />
    public SlowEffectData SlowData => _slowData;

    /// <summary>
    /// 시험 설정 연결
    /// </summary>
    /// <param name="support">지원 종류</param>
    /// <param name="slowData">감속 설정</param>
    public void Configure ( InstallationUpgradeSupport support, SlowEffectData slowData )
    {
        _supportedUpgrades = support;
        _slowData = slowData;
    }
}
#endif
