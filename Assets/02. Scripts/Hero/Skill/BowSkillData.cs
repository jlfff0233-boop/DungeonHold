using UnityEngine;

/// <summary>
/// 활 스킬 데이터 - 관통 화살 설정과 최대 적중 대상 수
/// </summary>
[CreateAssetMenu (
    fileName = "BowSkillData" , menuName = "Hero/BowSkillData" )]
public class BowSkillData : SkillData
{
    [Header ( "----- 관통 화살 -----" )]
    [SerializeField] ProjectileData _projectileData;    //생성/비행 설정
    [SerializeField] int _maxHitCount = 3;    //최대 유효 적중 대상 수

    /// <summary>
    /// 생성/비행 설정
    /// </summary>
    public ProjectileData ProjectileData => _projectileData;

    /// <summary>
    /// 최대 유효 적중 대상 수
    /// </summary>
    public int MaxHitCount => _maxHitCount;
}
