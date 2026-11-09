using UnityEngine;

/// <summary>
/// 검 스킬 설정 데이터 - 공통 스킬 설정과 넉백 설정
/// </summary>
[CreateAssetMenu (
    fileName = "SwordSkillData" , menuName = "Hero/Skill/SwordSkillData" )]
public class SwordSkillData : SkillData
{
    [Header ( "----- 넉백 -----" )]
    [SerializeField] KnockbackData _knockbackData;    //검 스킬의 넉백 거리와 시간

    /// <summary>
    /// 검 스킬 넉백 설정
    /// </summary>
    public KnockbackData KnockbackData => _knockbackData;
}
