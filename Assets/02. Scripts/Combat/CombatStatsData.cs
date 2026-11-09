using UnityEngine;

/// <summary>
/// 전투 능력치 데이터 - 체력 및 방어 능력치 설정
/// </summary>
[CreateAssetMenu( fileName = "CombatStatsData", menuName = "Combat/CombatStatsData" )]
public class CombatStatsData : ScriptableObject
{
    [Header( "----- 전투 능력치 -----" )]
    [SerializeField] float _maxHp;                  //최대 체력
    [SerializeField] float _damageReductionRate;    //피해 감소율

    /// <summary>
    /// 기본 최대 체력
    /// </summary>
    public float MaxHp => _maxHp;

    /// <summary>
    /// 기본 피해 감소율
    /// </summary>
    public float DamageReductionRate => _damageReductionRate;
}
