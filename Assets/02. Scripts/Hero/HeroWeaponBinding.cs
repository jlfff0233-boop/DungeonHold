using System;
using UnityEngine;

/// <summary>
/// 히어로 무기 연결 - 무기 타입별 실행자와 모델 참조
/// </summary>
[Serializable]
public class HeroWeaponBinding
{
    [SerializeField] WeaponType _weaponType;    //무기 타입
    [SerializeField] AttackExecutor _attackExecutor;    //기본 공격 실행자
    [SerializeField] AttackExecutor _skillExecutor;    //기본 스킬 실행자
    [SerializeField] AttackExecutor _ultimateExecutor;    //궁극기 실행자
    [SerializeField] GameObject _weaponModel;    //선택 시 표시할 무기 모델
    [SerializeField] GameObject _source;    //기본 피해 발생원

    /// <summary>
    /// 무기 타입
    /// </summary>
    public WeaponType WeaponType => _weaponType;

    /// <summary>
    /// 기본 공격 실행자
    /// </summary>
    public AttackExecutor AttackExecutor => _attackExecutor;

    /// <summary>
    /// 기본 스킬 실행자
    /// </summary>
    public AttackExecutor SkillExecutor => _skillExecutor;

    /// <summary>
    /// 궁극기 실행자
    /// </summary>
    public AttackExecutor UltimateExecutor => _ultimateExecutor;

    /// <summary>
    /// 선택 시 표시할 무기 모델
    /// </summary>
    public GameObject WeaponModel => _weaponModel;

    /// <summary>
    /// 기본 피해 발생원
    /// </summary>
    public GameObject Source => _source;
}
