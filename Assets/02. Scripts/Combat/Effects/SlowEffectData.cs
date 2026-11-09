using UnityEngine;

/// <summary>
/// 감속 설정 - 지속시간과 이동속도 감소 비율
/// </summary>
[CreateAssetMenu ( fileName = "SlowEffectData" , menuName = "Combat/SlowEffectData" )]
public class SlowEffectData : StatusEffectData
{
    [Header ( "----- 감속 -----" )]
    [SerializeField] float _slowRate = 0.4f;    //이동속도 감소 비율

    /// <summary>
    /// 상태 이상 - 감속
    /// </summary>
    public override StatusEffectType Type => StatusEffectType.Slow;

    /// <summary>
    /// 이동속도 감소 비율
    /// </summary>
    public float SlowRate => _slowRate;
}
