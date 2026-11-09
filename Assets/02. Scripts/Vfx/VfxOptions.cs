using System;
using UnityEngine;

/// <summary>
/// 월드 효과의 일괄 정리 범위
/// </summary>
public enum VfxScope
{
    Gameplay = 0,
    Presentation = 1
}

/// <summary>
/// 월드 효과의 시간과 정리 범위
/// </summary>
[Serializable]
public struct VfxOptions
{
    [SerializeField] VfxScope _scope;		//일괄 정리 범위
    [SerializeField] bool _useUnscaledTime;		//일시정지 중에도 진행

    public VfxScope Scope => _scope;
    public bool UseUnscaledTime => _useUnscaledTime;

    /// <summary>
    /// 시간 기준과 정리 범위를 명시하여 요청 구성
    /// </summary>
    public VfxOptions ( VfxScope scope, bool useUnscaledTime )
    {
        _scope = scope;
        _useUnscaledTime = useUnscaledTime;
    }
}
