/// <summary>
/// 화상 요청 - 공통 효과 정보, 주기당 피해량과 피해 간격 보존
/// </summary>
public readonly struct BurnEffectInfo
{
    /// <summary>
    /// 보존한 공통 효과 요청
    /// </summary>
    public StatusEffectInfo EffectInfo { get; }

    /// <summary>
    /// 보존한 주기당 원본 피해량
    /// </summary>
    public float RawDamage { get; }

    /// <summary>
    /// 보존한 피해 발생 간격
    /// </summary>
    public float TickInterval { get; }

    /// <summary>
    /// 현재 설정값을 보존한 화상 요청 생성
    /// </summary>
    /// <param name="context">효과 발생 정보</param>
    /// <param name="data">필수 화상 설정</param>
    public BurnEffectInfo (
        EffectContext context , BurnEffectData data )
    {
        EffectInfo = new StatusEffectInfo ( context , data );
        RawDamage = data.RawDamage;
        TickInterval = data.TickInterval;
    }
}