using System;
using UnityEngine;

/// <summary>
/// 튜토리얼 대상 연결 - 씬 UI 이름으로 단계 데이터 연결
/// </summary>
[Serializable]
public class TutorialTarget
{
    [SerializeField] RectTransform _target;    //실제 강조할 씬 UI

    /// <summary>
    /// 대상 오브젝트 이름으로 사용하는 식별자
    /// </summary>
    public string Id => _target.name;

    /// <summary>
    /// 실제 강조할 씬 UI
    /// </summary>
    public RectTransform Target => _target;
}
