using UnityEngine;

/// <summary>
/// 스태프 스킬 데이터 - 강화 폭발 투사체 설정
/// </summary>
[CreateAssetMenu ( fileName = "StaffSkillData" , menuName = "Hero/StaffSkillData" )]
public class StaffSkillData : SkillData
{
    [Header ( "----- 폭발 투사체 -----" )]
    [SerializeField] ProjectileData _projectileData;    //생성/비행 설정

    /// <summary>
    /// 생성/비행 설정
    /// </summary>
    public ProjectileData ProjectileData => _projectileData;
}
