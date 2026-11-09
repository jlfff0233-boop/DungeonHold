using UnityEngine;

/// <summary>
/// 무기 데이터 - 능력치/공격/스킬/모션 설정 묶음
/// </summary>
[CreateAssetMenu ( fileName = "WeaponData" , menuName = "Hero/WeaponData" )]
public class WeaponData : ScriptableObject
{
    [Header ( "----- 무기 -----" )]
    [SerializeField] WeaponType _weaponType;    //무기 타입

    [Header ( "----- 전투 -----" )]
    [SerializeField] CombatStatsData _combatStatsData;    //체력/방어 설정
    [SerializeField] AttackData _attackData;    //기본 공격 설정
    [SerializeField] SkillData _skillData;    //기본 스킬 설정
    [SerializeField] SkillData _ultimateData;    //궁극기 설정

    [Header ( "----- 이동 -----" )]
    [SerializeField] float _moveSpeed = 5f;    //기본 이동 속력
    [SerializeField] bool _useAimRotation;    //조준 방향 회전 사용 여부

    [Header ( "----- 모션 -----" )]
    [SerializeField] AnimatorOverrideController _animatorOverrideController;    //공통 Controller 기반 무기별 모션
    [SerializeField] bool _useUpperStance;    //상체 대기 자세 사용 여부

    /// <summary>
    /// 무기 타입
    /// </summary>
    public WeaponType WeaponType => _weaponType;

    /// <summary>
    /// 체력/방어 설정
    /// </summary>
    public CombatStatsData CombatStatsData => _combatStatsData;

    /// <summary>
    /// 기본 공격 설정
    /// </summary>
    public AttackData AttackData => _attackData;

    /// <summary>
    /// 기본 스킬 설정
    /// </summary>
    public SkillData SkillData => _skillData;

    /// <summary>
    /// 궁극기 설정
    /// </summary>
    public SkillData UltimateData => _ultimateData;

    /// <summary>
    /// 기본 이동 속력
    /// </summary>
    public float MoveSpeed => _moveSpeed;

    /// <summary>
    /// 조준 방향 회전 사용 여부
    /// </summary>
    public bool UseAimRotation => _useAimRotation;

    /// <summary>
    /// 무기별 모션 설정
    /// </summary>
    public AnimatorOverrideController AnimatorOverrideController
        => _animatorOverrideController;

    /// <summary>
    /// 상체 대기 자세 사용 여부
    /// </summary>
    public bool UseUpperStance => _useUpperStance;
}
