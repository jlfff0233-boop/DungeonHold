using System;
using UnityEngine;

/// <summary>
/// 종료 지급 기록 - 저장된 점수와 실제 보상 내역
/// </summary>
[Serializable]
public class ClearRewardRecord
{
    [SerializeField] string _settlementId;    //정산 식별자
    [SerializeField] string _mapId;    //맵 ID
    [SerializeField] MissionDifficulty _difficulty;    //난이도
    [SerializeField] MissionOutcome _outcome;    //승리/패배
    [SerializeField] int _stars;    //별
    [SerializeField] int _score;    //확정 점수
    [SerializeField] int _clearAmount;    //클리어 보상
    [SerializeField] int _starAmount;    //최초 별 보상
    [SerializeField] int _failureAmount;    //패배 보상

    /// <summary>
    /// 지급을 구분하는 정산 ID
    /// </summary>
    public string SettlementId => _settlementId;
    /// <summary>
    /// 보상 대상 맵 ID
    /// </summary>
    public string MapId => _mapId;
    /// <summary>
    /// 보상 대상 난이도
    /// </summary>
    public MissionDifficulty Difficulty => _difficulty;
    /// <summary>
    /// 보상 대상 승패
    /// </summary>
    public MissionOutcome Outcome => _outcome;
    /// <summary>
    /// 이번 클리어의 별 수
    /// </summary>
    public int Stars => _stars;
    /// <summary>
    /// 이번 클리어의 확정 점수
    /// </summary>
    public int Score => _score;
    /// <summary>
    /// 클리어/최초 별/패배 보상 내역
    /// </summary>
    public ClearRewardAmounts Amounts => new ( _clearAmount , _starAmount , _failureAmount );

    /// <summary>
    /// 확정 계획에서 지급 기록 생성
    /// </summary>
    /// <param name="plan">정산 계획</param>
    public ClearRewardRecord ( ClearRewardPlan plan )
    {
        _settlementId = plan.SettlementId;
        _mapId = plan.Input.Context.MapId;
        _difficulty = plan.Input.Context.Difficulty;
        _outcome = plan.Input.Result.Outcome;
        _stars = plan.Input.Result.StarCount;
        _score = plan.Input.Score;
        _clearAmount = plan.Amounts.ClearAmount;
        _starAmount = plan.Amounts.StarAmount;
        _failureAmount = plan.Amounts.FailureAmount;
    }
}
