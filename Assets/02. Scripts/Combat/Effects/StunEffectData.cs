using UnityEngine;

/// <summary>
/// 기절 설정 - 이동과 공격 제한 지속시간
/// </summary>
[CreateAssetMenu ( fileName = "StunEffectData" , menuName = "Combat/StunEffectData" )]
public class StunEffectData : StatusEffectData
{
    /// <summary>
    /// 상태이상 종류 - 기절
    /// </summary>
    public override StatusEffectType Type => StatusEffectType.Stun;
}
