using UnityEngine;

/// <summary>
/// 설치물 강화 데이터 - 공통 대상과 효과 반영 계약
/// </summary>
public abstract class InstallationUpgradeData : UpgradeData
{
    [SerializeField] TrapData _installation;    //강화 대상 설치물

    /// <summary>
    /// 강화 대상 설치물
    /// </summary>
    public TrapData Installation => _installation;

    /// <summary>
    /// 필요한 강화 종류
    /// </summary>
    protected abstract InstallationUpgradeSupport RequiredSupport { get; }

    /// <inheritdoc />
    protected override bool ValidateTarget ()
    {
        return _installation != null && _installation.Prefab != null;
    }

    /// <summary>
    /// 현재 단계의 효과를 설치물 강화 결과에 반영
    /// </summary>
    /// <param name="level">현재 단계</param>
    /// <param name="stats">강화 결과</param>
    public abstract void ApplyToStats (
        int level, ref InstallationUpgradeStats stats );

    /// <summary>
    /// 데이터와 설치물의 효과 호환성 검사
    /// </summary>
    /// <param name="error">호환되지 않는 설정 설명</param>
    /// <returns>실제 설치물에 적용할 수 있는지 여부</returns>
    public bool ValidateCompatibility ( out string error )
    {
        error = null;

        IInstallationUpgradeTarget target =
            _installation.Prefab.GetComponent<IInstallationUpgradeTarget>( );

        if ( target == null )
        {
            error = "설치물의 강화 지원 정보 연결이 필요합니다.";
            return false;
        }

        InstallationUpgradeSupport required = RequiredSupport;
        bool isSupported = ( target.SupportedUpgrades & required ) == required;

        if ( isSupported == false )
        {
            error = $"설치물이 {required} 강화를 지원하지 않습니다.";
            return false;
        }

        //공통 지원 검사 이후 효과별 설정 검사
        return ValidateEffectCompatibility( out error );
    }

    /// <summary>
    /// 효과별 설정 검사
    /// </summary>
    /// <param name="error">설정 오류</param>
    /// <returns>사용 가능 여부</returns>
    protected virtual bool ValidateEffectCompatibility ( out string error )
    {
        error = null;
        return true;
    }
}
