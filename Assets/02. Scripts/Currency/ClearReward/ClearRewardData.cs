using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기존 별 보상 처리 - 도입 전 기록의 지급 기준
/// </summary>
public enum LegacyStarPolicy
{
    GrantOnClear = 1,    //다시 달성하면 지급
    AlreadyPaid = 2     //기존 최고 별까지 지급한 것으로 처리
}

/// <summary>
/// 종료 보상 설정 - 금액/배율과 맵별 기준 점수
/// </summary>
[CreateAssetMenu (
    fileName = "ClearRewardData" , menuName = "Currency/ClearRewardData" )]
public class ClearRewardData : ScriptableObject
{
    [SerializeField] int _baseAmount = 300;    //기본 클리어 보상
    [SerializeField] int _failureAmount = 50;    //패배 보상
    [SerializeField] int _hardPercent = 120;    //어려움 배율 백분율
    [SerializeField] int _maxScoreBonusPercent = 50;    //최대 점수 추가 배율 백분율
    [SerializeField] int _twoStarAmount = 100;    //최초 2성 보상
    [SerializeField] int _threeStarAmount = 200;    //최초 3성 보상
    [SerializeField] LegacyStarPolicy _legacyStars = LegacyStarPolicy.AlreadyPaid;    //기존 기록의 처리 선택
    [SerializeField] List<ClearScoreTarget> _targets = new ( );    //배정한 기준 점수

    /// <summary>
    /// 점수와 난이도 배율 적용 전 클리어 보상
    /// </summary>
    public int BaseAmount => _baseAmount;
    /// <summary>
    /// 패배 시 지급할 보상
    /// </summary>
    public int FailureAmount => _failureAmount;
    /// <summary>
    /// 어려움 난이도 배율 백분율
    /// </summary>
    public int HardPercent => _hardPercent;
    /// <summary>
    /// 기준 점수 달성 시 추가 배율 백분율
    /// </summary>
    public int MaxScoreBonusPercent => _maxScoreBonusPercent;
    /// <summary>
    /// 최초 2성 달성 보상
    /// </summary>
    public int TwoStarAmount => _twoStarAmount;
    /// <summary>
    /// 최초 3성 달성의 추가 보상
    /// </summary>
    public int ThreeStarAmount => _threeStarAmount;
    /// <summary>
    /// 도입 전 최고 별의 보상 정책
    /// </summary>
    public LegacyStarPolicy LegacyStars => _legacyStars;

    /// <summary>
    /// 보상 설정 구조 검사
    /// </summary>
    /// <returns>유효 여부</returns>
    public bool Validate ( )
    {
        if ( _baseAmount < 0 || _failureAmount < 0 || _hardPercent < 100
            || _maxScoreBonusPercent < 0 || _twoStarAmount < 0 || _threeStarAmount < 0
            || ( long ) _twoStarAmount + _threeStarAmount > int.MaxValue
            || _legacyStars < LegacyStarPolicy.GrantOnClear
            || _legacyStars > LegacyStarPolicy.AlreadyPaid || _targets == null )
        {
            Debug.LogError ( "종료 보상 금액/배율/기존 별 정책을 확인하세요." , this );
            return false;
        }

        HashSet<(string, MissionDifficulty)> keys = new ( );
        foreach ( ClearScoreTarget target in _targets )
        {
            if ( target == null || string.IsNullOrWhiteSpace ( target.MapId ) == true
                || target.Difficulty < MissionDifficulty.Easy || target.Difficulty > MissionDifficulty.Hard
                || target.Score <= 0 || keys.Add ( (target.MapId, target.Difficulty) ) == false )
            {
                Debug.LogError ( "보상 기준 점수의 맵/난이도/양수 점수 또는 중복을 확인하세요." , this );
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 선택한 도전의 기준 점수 조회
    /// </summary>
    /// <param name="mapId">맵 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="score">기준 점수</param>
    /// <returns>배정 여부</returns>
    public bool TryGetTargetScore (
        string mapId , MissionDifficulty difficulty , out int score )
    {
        foreach ( ClearScoreTarget target in _targets )
        {
            if ( target.MapId != mapId || target.Difficulty != difficulty )
                continue;

            score = target.Score;
            return true;
        }

        score = 0;
        return false;
    }
}
