using System;
using UnityEngine;

/// <summary>
/// 미션 완료 저장 기록 - 최초 완료와 지급 상태 보관
/// </summary>
[Serializable]
public class MissionSaveData
{
    [SerializeField] string _missionId;     //미션 ID
    [SerializeField] MissionDifficulty _difficulty;     //난이도
    [SerializeField] bool _isCompleted;     //최초 완료
    [SerializeField] bool _isRewardPaid;        //보상 지급 완료

    /// <summary>
    /// 미션 ID
    /// </summary>
    public string MissionId => _missionId;
    /// <summary>
    /// 난이도
    /// </summary>
    public MissionDifficulty Difficulty => _difficulty;
    /// <summary>
    /// 완료 여부
    /// </summary>
    public bool IsCompleted => _isCompleted;
    /// <summary>
    /// 보상 지급 여부
    /// </summary>
    public bool IsRewardPaid => _isRewardPaid;

    /// <summary>
    /// 완료 기록 생성
    /// </summary>
    /// <param name="missionId">미션 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="isCompleted">완료 여부</param>
    /// <param name="isRewardPaid">지급 여부</param>
    public MissionSaveData (
        string missionId , MissionDifficulty difficulty ,
        bool isCompleted , bool isRewardPaid )
    {
        _missionId = missionId;
        _difficulty = difficulty;
        _isCompleted = isCompleted;
        _isRewardPaid = isRewardPaid;
    }
}

