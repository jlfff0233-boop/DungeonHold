/// <summary>
/// 설치물 강화 대상 - 지원 종류 제공
/// </summary>
public interface IInstallationUpgradeTarget
{
    /// <summary>
    /// 지원하는 강화 종류
    /// </summary>
    InstallationUpgradeSupport SupportedUpgrades { get; }
}
