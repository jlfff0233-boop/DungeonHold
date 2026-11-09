using UnityEngine;

/// <summary>
/// 상태이상 설정 - 공통 지속시간과 효과 종류 계약
/// </summary>
public abstract class StatusEffectData : ScriptableObject
{
    [Header( "----- 효과 -----" )]
    [SerializeField] float _duration = 3f;      //요청 지속시간

    /// <summary>
    /// 자식 설정이 결정하는 효과 종류
    /// </summary>
    public abstract StatusEffectType Type { get; }

    /// <summary>
    /// 요청 지속시간
    /// </summary>
    public float Duration => _duration;
}
