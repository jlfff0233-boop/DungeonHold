
/// <summary>
/// 플레이어 강화 결과 - 기본값에 적용할 증가량
/// </summary>
public struct HeroUpgradeStats
{
    float _maxHpBonus;         //최대 HP 추가량
    float _moveSpeedBonus;     //이동속도 증가율
    float _damageBonus;        //직접 피해 증가율
    float _attackSpeedBonus;   //기본 공격속도 증가율

    /// <summary>
    /// 최대 HP 추가량
    /// </summary>
    public float MaxHpBonus => _maxHpBonus;

    /// <summary>
    /// 이동속도 증가율
    /// </summary>
    public float MoveSpeedBonus => _moveSpeedBonus;

    /// <summary>
    /// 직접 피해 증가율
    /// </summary>
    public float DamageBonus => _damageBonus;

    /// <summary>
    /// 기본 공격속도 증가율
    /// </summary>
    public float AttackSpeedBonus => _attackSpeedBonus;

    /// <summary>
    /// 이번 도전의 플레이어 강화 결과 생성
    /// </summary>
    /// <param name="maxHpBonus">최대 HP 추가량</param>
    /// <param name="moveSpeedBonus">이동속도 증가율</param>
    /// <param name="damageBonus">직접 피해 증가율</param>
    /// <param name="attackSpeedBonus">기본 공격속도 증가율</param>
    public HeroUpgradeStats (
        float maxHpBonus , float moveSpeedBonus ,
        float damageBonus , float attackSpeedBonus )
    {
        _maxHpBonus = maxHpBonus;
        _moveSpeedBonus = moveSpeedBonus;
        _damageBonus = damageBonus;
        _attackSpeedBonus = attackSpeedBonus;
    }
}

/// <summary>
/// 동료 강화 결과 - HP와 직접 피해 증가율
/// </summary>
public struct CompanionUpgradeStats
{
    float _hpBonus;        //최대 HP 증가율
    float _damageBonus;    //직접 피해 증가율

    /// <summary>
    /// 최대 HP 증가율
    /// </summary>
    public float HpBonus => _hpBonus;

    /// <summary>
    /// 직접 피해 증가율
    /// </summary>
    public float DamageBonus => _damageBonus;

    /// <summary>
    /// 이번 도전의 동료 강화 결과 생성
    /// </summary>
    /// <param name="hpBonus">최대 HP 증가율</param>
    /// <param name="damageBonus">직접 피해 증가율</param>
    public CompanionUpgradeStats ( float hpBonus , float damageBonus )
    {
        _hpBonus = hpBonus;
        _damageBonus = damageBonus;
    }
}

/// <summary>
/// 설치물 강화 결과 - 피해 증가율, 공격 주기 단축률과 감속 추가량
/// </summary>
public struct InstallationUpgradeStats
{
    float _damageBonus;     //피해 증가율
    float _intervalReduction;       //공격 주기 단축률
    float _slowRateBonus;           //감속률 추가량

    /// <summary>
    /// 피해 증가율
    /// </summary>
    public float DamageBonus => _damageBonus;

    /// <summary>
    /// 공격 주기 단축률
    /// </summary>
    public float IntervalReduction => _intervalReduction;

    /// <summary>
    /// 감속률 추가량
    /// </summary>
    public float SlowRateBonus => _slowRateBonus;

    /// <summary>
    /// 이번 도전의 설치물 강화 결과 생성
    /// </summary>
    /// <param name="damageBonus">피해 증가율</param>
    /// <param name="intervalReduction">공격 주기 단축률</param>
    public InstallationUpgradeStats (
        float damageBonus , float intervalReduction )
        : this ( damageBonus, intervalReduction, 0f )
    {
    }

    /// <summary>
    /// 설치물의 도전별 강화 결과 생성
    /// </summary>
    /// <param name="damageBonus">피해 증가율</param>
    /// <param name="intervalReduction">공격 주기 단축률</param>
    /// <param name="slowRateBonus">감속률 추가량</param>
    public InstallationUpgradeStats (
        float damageBonus, float intervalReduction, float slowRateBonus )
    {
        _damageBonus = damageBonus;
        _intervalReduction = intervalReduction;
        _slowRateBonus = slowRateBonus;
    }

    /// <summary>
    /// 현재 단계의 피해 증가율 설정
    /// </summary>
    /// <param name="damageBonus">검사를 마친 피해 증가율</param>
    public void SetDamageBonus ( float damageBonus )
    {
        _damageBonus = damageBonus;
    }

    /// <summary>
    /// 현재 단계의 공격 주기 단축률 설정
    /// </summary>
    /// <param name="intervalReduction">검사를 마친 공격 주기 단축률</param>
    public void SetIntervalReduction ( float intervalReduction )
    {
        _intervalReduction = intervalReduction;
    }

    /// <summary>
    /// 현재 단계의 감속률 추가량 설정
    /// </summary>
    /// <param name="slowRateBonus">검사를 마친 감속률 추가량</param>
    public void SetSlowRateBonus ( float slowRateBonus )
    {
        _slowRateBonus = slowRateBonus;
    }

    /// <summary>
    /// 기본 피해에 강화 적용
    /// </summary>
    /// <param name="baseDamage">기본 피해</param>
    /// <returns>강화된 피해</returns>
    public float CalculateDamage ( float baseDamage )
    {
        return baseDamage * ( 1f + _damageBonus );
    }

    /// <summary>
    /// 기본 공격 주기에 강화 적용
    /// </summary>
    /// <param name="baseInterval">기본 공격 주기</param>
    /// <returns>단축된 공격 주기</returns>
    public float CalculateInterval ( float baseInterval )
    {
        return baseInterval * ( 1f - _intervalReduction );
    }

    /// <summary>
    /// 기본 감속률에 강화 적용
    /// </summary>
    /// <param name="baseSlowRate">기본 감속률</param>
    /// <returns>강화된 요청 감속률</returns>
    public float CalculateSlowRate ( float baseSlowRate )
    {
        return CalculateSlowRate( baseSlowRate, _slowRateBonus );
    }

    /// <summary>
    /// 요청 감속률 계산
    /// </summary>
    /// <param name="baseSlowRate">기본 감속률</param>
    /// <param name="slowRateBonus">감속 추가량</param>
    /// <returns>계산된 감속률</returns>
    public static float CalculateSlowRate ( float baseSlowRate, float slowRateBonus )
    {
        //중간 비교 정밀도에 의존하지 않고 요청과 같은 float 결과로 확정
        return (float) ( (double) baseSlowRate + slowRateBonus );
    }
}
