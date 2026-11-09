using UnityEngine;

/// <summary>
/// 공격 실행 정보 전달
/// </summary>
public struct AttackContext
{
    /// <summary>
    /// 공격자
    /// </summary>
    public GameObject Attacker { get; }

    /// <summary>
    /// 피해 발생원
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
    /// 공격의 분류
    /// </summary>
    public AttackType AttackType { get; }

    /// <summary>
    /// 공격 회차
    /// </summary>
    public int AttackId { get; }

    /// <summary>
    /// 원본 피해량
    /// </summary>
    public float RawDamage { get; }

    /// <summary>
    /// 탐색 반경
    /// </summary>
    public float Radius { get; }

    /// <summary>
    /// 전체 탐색 각도
    /// </summary>
    public float Angle { get; }

    /// <summary>
    /// 공격 실행 정보 생성
    /// </summary>
    /// <param name="attacker">공격자</param>
    /// <param name="source">피해 발생원</param>
    /// <param name="actorType">주체 종류</param>
    /// <param name="faction">소속 진영</param>
    /// <param name="attackType">공격 분류</param>
    /// <param name="attackId">공격 회차</param>
    /// <param name="rawDamage">원본 피해량</param>
    /// <param name="radius">탐색 반경</param>
    /// <param name="angle">전체 탐색 각도</param>
    public AttackContext (
        GameObject attacker, GameObject source,
        ActorType actorType, Faction faction,
        AttackType attackType, int attackId,
        float rawDamage, float radius, float angle )
    {
        Attacker = attacker;
        Source = source;
        ActorType = actorType;
        Faction = faction;
        AttackType = attackType;
        AttackId = attackId;

        //이번 공격에 사용할 수치를 보존
        RawDamage = rawDamage;
        Radius = radius;
        Angle = angle;
    }
}
