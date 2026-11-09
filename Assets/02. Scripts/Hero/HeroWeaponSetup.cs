using UnityEngine;

/// <summary>
/// 히어로 무기 구성 - 무기별 구성 조회와 적용
/// </summary>
public class HeroWeaponSetup
{
    UnityEngine.Object _owner;    //설정 오류 로그의 대상
    HeroWeaponBinding [ ] _weaponBindings;    //캐릭터의 타입별 무기 연결
    Attacker _attacker;          //기본 공격 실행자 연결 대상
    SkillCaster _skillCaster;    //기본 스킬 실행자 연결 대상
    SkillCaster _ultimateCaster; //궁극기 실행자 연결 대상
    Mover _mover;                //무기별 이동 설정 적용 대상
    AnimatorHandler _animatorHandler;    //무기별 모션 설정 적용 대상

    WeaponData _weaponData;    //현재 적용한 무기 설정
    HeroWeaponBinding _currentWeaponBinding;    //현재 적용한 무기 연결

    /// <summary>
    /// 현재 적용한 무기 설정
    /// </summary>
    public WeaponData WeaponData => _weaponData;

    /// <summary>
    /// 무기 구성과 적용 대상 참조 연결
    /// </summary>
    /// <param name="owner">설정 오류 로그의 대상</param>
    /// <param name="weaponBindings">캐릭터의 타입별 무기 연결</param>
    /// <param name="attacker">기본 공격 모듈</param>
    /// <param name="skillCaster">기본 스킬 모듈</param>
    /// <param name="ultimateCaster">궁극기 모듈</param>
    /// <param name="mover">이동 모듈</param>
    /// <param name="animatorHandler">애니메이션 모듈</param>
    public HeroWeaponSetup (
        UnityEngine.Object owner , HeroWeaponBinding [ ] weaponBindings ,
        Attacker attacker , SkillCaster skillCaster , SkillCaster ultimateCaster ,
        Mover mover , AnimatorHandler animatorHandler )
    {
        _owner = owner;
        _weaponBindings = weaponBindings;
        _attacker = attacker;
        _skillCaster = skillCaster;
        _ultimateCaster = ultimateCaster;
        _mover = mover;
        _animatorHandler = animatorHandler;
    }

    /// <summary>
    /// 선택 무기의 단일 연결 조회와 필수 구성 확인
    /// </summary>
    /// <param name="weaponData">선택한 무기 설정</param>
    /// <param name="binding">검사에 사용할 무기 연결</param>
    /// <returns>필수 구성을 갖춘 단일 연결을 찾았는지 여부</returns>
    public bool TryResolve (
        WeaponData weaponData , out HeroWeaponBinding binding )
    {
        if ( TryGetWeaponBinding (
            _weaponBindings , weaponData.WeaponType , out binding ) == false )
        {
            Debug.LogError (
                $"무기 연결이 없거나 중복됩니다: {weaponData.WeaponType}" , _owner );
            return false;
        }

        //설정 출처가 바뀌는 경계에서 필수 구성을 한 번 확인
        if ( weaponData.CombatStatsData == null
            || weaponData.AttackData == null
            || weaponData.SkillData == null
            || weaponData.UltimateData == null
            || weaponData.AnimatorOverrideController == null
            || binding.AttackExecutor == null
            || binding.SkillExecutor == null
            || binding.UltimateExecutor == null
            || binding.WeaponModel == null
            || binding.Source == null )
        {
            Debug.LogError (
                $"무기 데이터 또는 연결에 누락이 있습니다: {weaponData.WeaponType}" , _owner );
            return false;
        }

        return true;
    }

    /// <summary>
    /// 기존 전투 모듈 초기화 전에 무기별 실행자 연결
    /// </summary>
    /// <param name="binding">검사를 마친 무기 연결</param>
    public void ApplyExecutors ( HeroWeaponBinding binding )
    {
        _attacker.SetExecutor ( binding.AttackExecutor );
        _skillCaster.SetExecutor ( binding.SkillExecutor );
        _ultimateCaster.SetExecutor ( binding.UltimateExecutor );
    }

    /// <summary>
    /// 전투 모듈 초기화 후 모델, 이동과 모션 설정 적용
    /// </summary>
    /// <param name="weaponData">선택한 무기 설정</param>
    /// <param name="binding">검사를 마친 무기 연결</param>
    /// <param name="position">표현 초기화의 위치 기준</param>
    /// <param name="upgrades">이번 도전의 이동/기본 공격속도 증가량</param>
    public void ApplyPresentation (
        WeaponData weaponData , HeroWeaponBinding binding , Vector3 position,
        HeroUpgradeStats upgrades = default )
    {
        //모델은 표시 전용 루트이며 실행자/발사점은 활성 계층에 유지
        if ( _currentWeaponBinding != null
            && _currentWeaponBinding.WeaponModel != binding.WeaponModel )
        {
            _currentWeaponBinding.WeaponModel.SetActive ( false );
        }

        binding.WeaponModel.SetActive ( true );

        _mover.SetMoveSpeed ( weaponData.MoveSpeed * ( 1f + upgrades.MoveSpeedBonus ) );
        _mover.SetAimRotationEnabled ( weaponData.UseAimRotation );

        _animatorHandler.ApplyOverride ( weaponData.AnimatorOverrideController );
        _animatorHandler.SetUpperStance ( weaponData.UseUpperStance );
        _animatorHandler.SelectUpperStance ( weaponData.WeaponType == WeaponType.Bow );
        var bowExecutor = weaponData.WeaponType == WeaponType.Bow
            ? binding.AttackExecutor as ProjectileAttackExecutor
            : null;
        _animatorHandler.SetBowAttackAim ( bowExecutor == null
            ? null
            : () => bowExecutor.GetPresentationAimDirection ( _mover.transform ) );
        _animatorHandler.ResetPresentation ( position );
        //Controller와 표현 초기화 후 Hero의 기본 공격 상태에만 배율 적용
        _animatorHandler.SetBasicAttackSpeed( 1f + upgrades.AttackSpeedBonus );

        //체력 변경 알림에서 새 구성을 조회할 수 있도록 먼저 반영
        _weaponData = weaponData;
        _currentWeaponBinding = binding;
    }

    /// <summary>
    /// 선택한 타입의 단일 무기 연결 조회
    /// </summary>
    /// <param name="weaponBindings">캐릭터의 타입별 무기 연결</param>
    /// <param name="weaponType">선택한 무기 타입</param>
    /// <param name="binding">캐릭터의 무기 연결</param>
    /// <returns>중복 없이 연결을 찾았는지 여부</returns>
    static bool TryGetWeaponBinding (
        HeroWeaponBinding [ ] weaponBindings ,
        WeaponType weaponType , out HeroWeaponBinding binding )
    {
        binding = null;

        foreach ( HeroWeaponBinding candidate in weaponBindings )
        {
            if ( candidate.WeaponType != weaponType )
                continue;

            if ( binding != null )
            {
                binding = null;
                return false;
            }

            binding = candidate;
        }

        return binding != null;
    }

    /// <summary>
    /// 선택 무기의 기본 공격 범위 기즈모 설정
    /// </summary>
    /// <param name="weaponBindings">캐릭터의 타입별 무기 연결</param>
    /// <param name="weaponData">에디터에서 선택한 무기 설정</param>
    public static void SetWeaponGizmo (
        HeroWeaponBinding [ ] weaponBindings , WeaponData weaponData )
    {
#if UNITY_EDITOR
        //에디터에서 데이터를 연결하는 중에도 표시 요청이 발생
        if ( weaponData.AttackData == null || weaponBindings == null )
            return;

        if ( TryGetWeaponBinding (
            weaponBindings , weaponData.WeaponType , out HeroWeaponBinding binding ) == false )
            return;

        if ( binding.AttackExecutor is AreaAttackExecutor areaExecutor )
        {
            areaExecutor.SetGizmoRange (
                weaponData.AttackData.Radius , weaponData.AttackData.Angle );
        }
#endif
    }
}
