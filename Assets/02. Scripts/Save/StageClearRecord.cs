using System;
using UnityEngine;

/// <summary>
/// 맵과 난이도별 클리어 기록
/// </summary>
[Serializable]
public class StageClearRecord
{
    [SerializeField] string _mapId;    //고정 맵 ID
    [SerializeField] MissionDifficulty _difficulty;    //난이도
    [SerializeField] int _bestStars;    //최고 별
    [SerializeField] bool _hasScore;    //점수 기록 여부
    [SerializeField] int _bestScore;    //클리어 최고 점수

    /// <summary>
    /// 고정 맵 ID
    /// </summary>
    public string MapId => _mapId;

    /// <summary>
    /// 난이도
    /// </summary>
    public MissionDifficulty Difficulty => _difficulty;

    /// <summary>
    /// 최고 별
    /// </summary>
    public int BestStars => _bestStars;

    /// <summary>
    /// 점수 기록 여부
    /// </summary>
    public bool HasScore => _hasScore;

    /// <summary>
    /// 최고 점수
    /// </summary>
    public int BestScore => _bestScore;

    /// <summary>
    /// 클리어 기록 생성
    /// </summary>
    /// <param name="mapId">고정 맵 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="bestStars">최고 별</param>
    /// <param name="bestScore">최고 점수</param>
    public StageClearRecord (
        string mapId,
        MissionDifficulty difficulty,
        int bestStars,
        int bestScore )
    {
        _mapId = mapId;
        _difficulty = difficulty;
        _bestStars = bestStars;
        _hasScore = true;
        _bestScore = bestScore;
    }
}
