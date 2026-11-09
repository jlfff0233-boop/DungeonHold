/// <summary>
/// 감속 요청 - 공통 효과 정보와 감속 강도 보존
/// </summary>
public readonly struct SlowEffectInfo
{
    /// <summary>
    /// 보존한 공통 효과 요청
    /// </summary>
    public StatusEffectInfo EffectInfo { get; }

    /// <summary>
    /// 보존한 이동속도 감소 비율
    /// </summary>
    public float SlowRate { get; }

    /// <summary>
    /// 현재 설정값을 보존한 감속 요청 생성
    /// </summary>
    /// <param name="context">효과 발생 정보</param>
    /// <param name="data">필수 감속 설정</param>
    public SlowEffectInfo (
        EffectContext context, SlowEffectData data )
        : this ( context, data, data.SlowRate )
    {
    }

    /// <summary>
    /// 계산된 감속률을 보존한 감속 요청 생성
    /// </summary>
    /// <param name="context">효과 발생 정보</param>
    /// <param name="data">효과 식별과 지속시간에 사용할 설정</param>
    /// <param name="slowRate">강화를 반영한 요청 감속률</param>
    public SlowEffectInfo (
        EffectContext context, SlowEffectData data, float slowRate )
    {
        EffectInfo = new StatusEffectInfo( context, data );
        SlowRate = slowRate;
    }
}
