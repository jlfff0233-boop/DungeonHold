using UnityEngine;

/// <summary>
/// 효과 발생 정보 - 공격 주체, 진영과 원래 회차 보존
/// </summary>
public readonly struct EffectContext
{
    /// <summary>
    /// 실제 공격 주체
    /// </summary>
    public GameObject Attacker { get; }
    /// <summary>
    /// 실제 효과 발생원
    /// </summary>
    public GameObject Source { get; }
    /// <summary>
    /// 공격 주체 종류
    /// </summary>
    public ActorType ActorType { get; }
    /// <summary>
    /// 공격 진영
    /// </summary>
    public Faction Faction { get; }
    /// <summary>
    /// 원래 공격 분류
    /// </summary>
    public AttackType AttackType { get; }
    /// <summary>
    /// 원래 공격 회차
    /// </summary>
    public int AttackId { get; }

    /// <summary>
    /// 효과 발생 정보 생성
    /// </summary>
    /// <param name="attacker">실제 공격 주체</param>
    /// <param name="source">실제 효과 발생원</param>
    /// <param name="actorType">공격 주체 종류</param>
    /// <param name="faction">공격 진영</param>
    /// <param name="attackType">원래 공격 분류</param>
    /// <param name="attackId">원래 공격 회차</param>
    public EffectContext (
        GameObject attacker, GameObject source,
        ActorType actorType, Faction faction,
        AttackType attackType, int attackId )
    {
        Attacker = attacker;
        Source = source;
        ActorType = actorType;
        Faction = faction;
        AttackType = attackType;
        AttackId = attackId;
    }
}

/// <summary>
/// 상태이상 요청 - 발생 정보, 종류와 지속시간 보존
/// </summary>
public readonly struct StatusEffectInfo
{
    /// <summary>
    /// 효과 발생 정보
    /// </summary>
    public EffectContext Context { get; }
    /// <summary>
    /// 동일 설정 식별용 참조
    /// </summary>
    public StatusEffectData Data { get; }
    /// <summary>
    /// 보존한 효과 종류
    /// </summary>
    public StatusEffectType Type { get; }
    /// <summary>
    /// 보존한 지속시간
    /// </summary>
    public float Duration { get; }
    /// <summary>
    /// 현재 공통 설정값을 보존한 효과 요청 생성
    /// </summary>
    /// <param name="context">효과 발생 정보</param>
    /// <param name="data">필수 효과 설정</param>
    public StatusEffectInfo (
        EffectContext context, StatusEffectData data )
    {
        Context = context;
        Data = data;

        //설정 참조는 식별에 사용하고 실행값은 생성 시 보존
        Type = data.Type;
        Duration = data.Duration;
    }
}
