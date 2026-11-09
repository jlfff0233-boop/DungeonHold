using System;
using UnityEngine;

/// <summary>
/// 별 지급 기록 - 맵/난이도별 최초 지급 단계
/// </summary>
[Serializable]
public class StarRewardRecord
{
    [SerializeField] string _mapId;    //맵 ID
    [SerializeField] MissionDifficulty _difficulty;    //난이도
    [SerializeField] int _paidStars;    //지급한 단계, 0/2/3
    [SerializeField] bool _legacyPending;    //도입 전 기록의 정책 적용 대기

    /// <summary>
    /// 별 보상 대상 맵 ID
    /// </summary>
    public string MapId => _mapId;
    /// <summary>
    /// 별 보상 대상 난이도
    /// </summary>
    public MissionDifficulty Difficulty => _difficulty;
    /// <summary>
    /// 지급 완료한 별 단계, 0/2/3
    /// </summary>
    public int PaidStars => _paidStars;
    /// <summary>
    /// 기존 별 보상 정책의 적용 대기 여부
    /// </summary>
    public bool LegacyPending => _legacyPending;

    /// <summary>
    /// 별 지급 기록 생성
    /// </summary>
    /// <param name="mapId">맵 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="paidStars">지급 단계</param>
    /// <param name="legacyPending">기존 정책 대기</param>
    public StarRewardRecord (
        string mapId ,
        MissionDifficulty difficulty ,
        int paidStars ,
        bool legacyPending = false )
    {
        _mapId = mapId;
        _difficulty = difficulty;
        _paidStars = paidStars;
        _legacyPending = legacyPending;
    }
}
