using UnityEngine;

/// <summary>
/// 스킬 설정 데이터 - 공격 설정, 에너지 비용, 완료 후 쿨타임
/// </summary>
[CreateAssetMenu ( fileName = "SkillData" , menuName = "Hero/SkillData" )]
public class SkillData : ScriptableObject
{
    [Header ( "----- 공격 -----" )]
    [SerializeField] AttackData _attackData;        //스킬 피해량과 타격 범위

    [Header ( "----- 사용 -----" )]
    [SerializeField] float _energyCost = 30f;       //시작 승인 시 소비량
    [SerializeField] float _cooldown = 5f;      //완료 또는 취소 후 쿨타임

    /// <summary>
    /// 스킬 공격 설정
    /// </summary>
    public AttackData AttackData => _attackData;

    /// <summary>
    /// 에너지 비용
    /// </summary>
    public float EnergyCost => _energyCost;

    /// <summary>
    /// 완료 또는 취소 후 쿨타임
    /// </summary>
    public float Cooldown => _cooldown;
}