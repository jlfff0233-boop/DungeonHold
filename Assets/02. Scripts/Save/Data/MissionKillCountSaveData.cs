using System;
using UnityEngine;

/// <summary>
/// 미션 처치 집계 저장 기록 - 난이도별 처치 수 보관
/// </summary>
[Serializable]
public class MissionKillCountSaveData
{
    [SerializeField] string _counterId;     //처치 집계 ID, 기존 JSON 필드명 유지
    [SerializeField] MissionDifficulty _difficulty;     //난이도
    [SerializeField] int _count;        //실제 누적 수

    /// <summary>
    /// 미션 처치 집계 ID
    /// </summary>
    public string KillCountId => _counterId;
    /// <summary>
    /// 미션 난이도
    /// </summary>
    public MissionDifficulty Difficulty => _difficulty;
    /// <summary>
    /// 실제 누적 수
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// 처치 집계 기록 생성
    /// </summary>
    /// <param name="killCountId">처치 집계 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="count">누적 수</param>
    public MissionKillCountSaveData (
        string killCountId , MissionDifficulty difficulty , int count )
    {
        _counterId = killCountId;
        _difficulty = difficulty;
        _count = count;
    }
}
