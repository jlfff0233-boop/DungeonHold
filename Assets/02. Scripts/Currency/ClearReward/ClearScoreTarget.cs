using System;
using UnityEngine;

/// <summary>
/// 보상 기준 점수 - 맵/난이도별 달성 기준
/// </summary>
[Serializable]
public class ClearScoreTarget
{
    [SerializeField] string _mapId;    //맵 ID
    [SerializeField] MissionDifficulty _difficulty;    //난이도
    [SerializeField] int _score;    //최대 보상 배율의 기준 점수

    /// <summary>
    /// 보상 기준의 맵 ID
    /// </summary>
    public string MapId => _mapId;
    /// <summary>
    /// 보상 기준의 난이도
    /// </summary>
    public MissionDifficulty Difficulty => _difficulty;
    /// <summary>
    /// 최대 보상 배율에 도달하는 기준 점수
    /// </summary>
    public int Score => _score;
}
