using System;
using UnityEngine;

/// <summary>
/// 미션 횟수 비교 방식
/// </summary>
public enum MissionCountComparison
{
    Exactly, //정확히 일치
    AtMost, //기준 이하
    AtLeast     //기준 이상
}

/// <summary>
/// 미션 횟수 조건 - 실제 횟수와 기준 비교
/// </summary>
[Serializable]
public struct MissionCountCondition
{
    [SerializeField] MissionCountComparison _comparison;        //비교 방식
    [SerializeField] int _count;    //기준 횟수

    /// <summary>
    /// 초기화에 사용할 비교 설정 검사
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    public bool Validate ( )
    {
        return _count >= 0
            && _comparison >= MissionCountComparison.Exactly
            && _comparison <= MissionCountComparison.AtLeast;
    }

    /// <summary>
    /// 실제 횟수와 검사 완료한 설정 비교
    /// </summary>
    /// <param name="actualCount">실제 횟수</param>
    /// <returns>조건 충족 여부</returns>
    public bool IsSatisfied ( int actualCount )
    {
        return _comparison switch
        {
            MissionCountComparison.Exactly => actualCount == _count,
            MissionCountComparison.AtMost => actualCount <= _count,
            MissionCountComparison.AtLeast => actualCount >= _count,
            _ => false
        };
    }
}
