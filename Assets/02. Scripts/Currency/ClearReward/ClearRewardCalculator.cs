using System;

/// <summary>
/// 종료 보상 계산기 - 확정 점수와 최초 별 보상 계산
/// </summary>
public static class ClearRewardCalculator
{
    /// <summary>
    /// 클리어/최초 별/패배 보상 계산
    /// </summary>
    /// <param name="data">검사한 설정</param>
    /// <param name="input">도전 결과와 지급 이력</param>
    /// <param name="amounts">계산 금액</param>
    /// <returns>정수 금액 변환 성공 여부</returns>
    public static bool TryCalculate (
        ClearRewardData data ,
        ClearRewardInput input ,
        out ClearRewardAmounts amounts )
    {
        amounts = default;

        //포기는 호출부에서 제외, 패배는 실패 보상만 계산
        if ( input.Result.Outcome == MissionOutcome.Defeat )
        {
            amounts = new ClearRewardAmounts ( 0 , 0 , data.FailureAmount );
            return true;
        }

        //입력 검사는 호출부에서 완료, 배율 적용 후 최종 금액을 버림
        decimal ratio = Math.Min ( ( decimal ) input.Score / input.TargetScore , 1m );
        int percent = input.Context.Difficulty == MissionDifficulty.Hard
            ? data.HardPercent
            : 100;
        decimal reward = decimal.Floor ( data.BaseAmount * ( percent / 100m )
            * ( 1m + data.MaxScoreBonusPercent / 100m * ratio ) );

        if ( reward > int.MaxValue )
            return false;

        //기존 지급 단계를 넘긴 별 보상만 합산
        int stars = 0;

        if ( input.Result.StarCount >= 2 && input.PaidStars < 2 )
            stars += data.TwoStarAmount;

        if ( input.Result.StarCount >= 3 && input.PaidStars < 3 )
            stars += data.ThreeStarAmount;

        amounts = new ClearRewardAmounts ( ( int ) reward , stars , 0 );
        return true;
    }
}
