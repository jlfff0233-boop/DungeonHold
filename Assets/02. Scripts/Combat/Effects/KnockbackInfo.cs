using UnityEngine;

/// <summary>
/// 넉백 요청 - 발생 정보, 방향, 거리와 시간 보존
/// </summary>
public readonly struct KnockbackInfo
{
    /// <summary>
    /// 효과 발생 정보
    /// </summary>
    public EffectContext Context { get; }

    /// <summary>
    /// 요청 이동 방향
    /// </summary>
    public Vector3 Direction { get; }

    /// <summary>
    /// 요청 이동 거리
    /// </summary>
    public float Distance { get; }

    /// <summary>
    /// 요청 진행 시간
    /// </summary>
    public float Duration { get; }

    /// <summary>
    /// 현재 설정값을 보존한 넉백 요청 생성
    /// </summary>
    /// <param name="context">효과 발생 정보</param>
    /// <param name="data">필수 넉백 설정</param>
    /// <param name="direction">월드 기준 이동 방향</param>
    public KnockbackInfo (
        EffectContext context, KnockbackData data, Vector3 direction )
        : this ( context, data.Distance, data.Duration, direction )
    {
    }

    /// <summary>
    /// 보존한 수치로 넉백 요청 생성
    /// </summary>
    /// <param name="context">효과 발생 정보</param>
    /// <param name="distance">요청 이동 거리</param>
    /// <param name="duration">요청 진행 시간</param>
    /// <param name="direction">월드 기준 이동 방향</param>
    public KnockbackInfo (
        EffectContext context, float distance,
        float duration, Vector3 direction )
    {
        //시작 시 보존한 거리/시간과 타격 시점의 방향을 요청 안에 함께 보존
        //수치 유효성/수신 가능 여부는 기존 KnockbackController에서 최종 검사
        Context = context;
        Direction = direction;
        Distance = distance;
        Duration = duration;
    }
}
