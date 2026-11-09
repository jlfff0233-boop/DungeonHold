using UnityEngine;

/// <summary>
/// 피해 정보 및 공격자 정보 전달
/// </summary>
public readonly struct DamageInfo
{
    /// <summary>
    /// 공격자의 루트 오브젝트
    /// </summary>
    public GameObject Attacker { get; }

    /// <summary>
    /// 피해 발생 오브젝트
    /// </summary>
    public GameObject Source { get; }

    /// <summary>
    /// 공격자의 종류
    /// </summary>
    public ActorType ActorType { get; }

    /// <summary>
    /// 공격자의 진영
    /// </summary>
    public Faction Faction { get; }

    /// <summary>
    /// 방어 계산 전 원본 피해량
    /// </summary>
    public float RawDamage { get; }

    /// <summary>
    /// 공격의 분류
    /// </summary>
    public AttackType AttackType { get; }

    /// <summary>
    /// 공격 회차 식별자
    /// </summary>
    public int AttackId { get; }

    /// <summary>
    /// 전달할 피해 정보 생성
    /// </summary>
    /// <param name="attacker">공격자 오브젝트</param>
    /// <param name="source">실제 피해를 발생시킨 오브젝트</param>
    /// <param name="actorType">공격자의 종류</param>
    /// <param name="faction">공격자의 진영</param>
    /// <param name="rawDamage">방어 계산 전 원본 피해량</param>
    /// <param name="attackType">공격의 분류</param>
    /// <param name="attackId">공격 회차 식별자</param>
    public DamageInfo (
        GameObject attacker,
        GameObject source,
        ActorType actorType,
        Faction faction,
        float rawDamage,
        AttackType attackType,
        int attackId )
    {
        //주체 종류와 진영은 독립적으로 전달받음
        Attacker = attacker;
        Source = source;
        ActorType = actorType;
        Faction = faction;

        //요청 정보를 그대로 보존하고
        //피해 허용과 방어 계산은 수신 측에서 처리
        RawDamage = rawDamage;
        AttackType = attackType;
        AttackId = attackId;
    }
}
