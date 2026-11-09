/// <summary>
/// 설치물 감속 설정 제공 - 실제 사용하는 기본 감속 데이터 조회
/// </summary>
public interface IInstallationSlowSource
{
    /// <summary>
    /// 설치물이 사용하는 기본 감속 설정
    /// </summary>
    SlowEffectData SlowData { get; }
}
