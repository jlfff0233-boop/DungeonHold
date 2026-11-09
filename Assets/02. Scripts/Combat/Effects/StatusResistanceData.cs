using UnityEngine;

/// <summary>
/// 상태 저항 설정 - 기절 면역, 감속 적용 비율
/// </summary>
[CreateAssetMenu(
    fileName = "StatusResistanceData",
    menuName = "Combat/StatusResistanceData" )]
public class StatusResistanceData : ScriptableObject
{
    [Header( "----- 기절 -----" )]
    [SerializeField] bool _isStunImmune;    //기절 면역 여부

    [Header( "----- 감속 -----" )]
    [SerializeField] float _slowEffectMultiplier = 1f;    //0이면 감속 면역
    [SerializeField] float _maxSlowRate = 0.6f;           //최대 감소 비율, 초기값

    /// <summary>
    /// 기절 면역 여부
    /// </summary>
    public bool IsStunImmune => _isStunImmune;

    /// <summary>
    /// 감속 강도 배율, 0이면 면역
    /// </summary>
    public float SlowEffectMultiplier => _slowEffectMultiplier;

    /// <summary>
    /// 최대 감소 비율, 최종 밸런스 미확정
    /// </summary>
    public float MaxSlowRate => _maxSlowRate;
}
