using UnityEngine;

/// <summary>
/// 화상 설정 - 지속시간, 주기당 피해량과 피해 간격
/// </summary>
[CreateAssetMenu ( fileName = "BurnEffectData" , menuName = "Combat/BurnEffectData" )]
public class BurnEffectData : StatusEffectData
{
    [Header ( "----- 화상 -----" )]
    [SerializeField] float _rawDamage = 10f;        //주기당 방어 적용 전 피해량
    [SerializeField] float _tickInterval = 1f;      //피해 발생 간격

    /// <summary>
    /// 상태 이상 - 화상
    /// </summary>
    public override StatusEffectType Type => StatusEffectType.Burn;

    /// <summary>
    /// 주기당 방어 적용 전 피해량
    /// </summary>
    public float RawDamage => _rawDamage;

    /// <summary>
    /// 피해 발생 간격
    /// </summary>
    public float TickInterval => _tickInterval;
}