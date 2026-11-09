#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 클리어 보상 검증 - 예상 지급액과 재실행 복원 비교
/// </summary>
[InitializeOnLoad]
public class ClearRewardCheck
{
    int _stageId;    //검증할 도전 번호
    int _score;    //검증할 확정 점수
    MissionOutcome _outcome;    //검증할 승패
    ClearRewardAmounts _expected;    //정산 전 확정한 예상 보상

    /// <summary>
    /// 프로젝트별 복원 비교 기준 키
    /// </summary>
    static string RestoreKey => "DungeonHold.ClearRewardCheck." + Application.dataPath;

    /// <summary>
    /// 복원 비교 기준 보유 여부
    /// </summary>
    public static bool HasRestoreBaseline =>
        string.IsNullOrEmpty ( SessionState.GetString ( RestoreKey, string.Empty ) ) == false;

    /// <summary>
    /// Play 중단을 거친 복원 비교 대기 여부
    /// </summary>
    public static bool CanCompareRestore => HasRestoreBaseline
        && SessionState.GetBool ( RestoreKey + ".ready", false );

    /// <summary>
    /// Inspector 선택과 관계없이 Play 중단 감지
    /// </summary>
    static ClearRewardCheck ( )
    {
        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
    }

    #region ----- 보상 비교 -----

    /// <summary>
    /// 정산 전에 도전과 예상 금액 고정
    /// </summary>
    /// <param name="stageId">도전 번호</param>
    /// <param name="score">확정 점수</param>
    /// <param name="expected">디버그에 입력한 예상 금액</param>
    /// <param name="outcome">검증할 승패</param>
    public void Prepare ( int stageId, int score, ClearRewardAmounts expected,
        MissionOutcome outcome = MissionOutcome.Victory )
    {
        _stageId = stageId;
        _score = score;
        _expected = expected;
        _outcome = outcome;
    }

    /// <summary>
    /// 해당 도전의 비교 준비 여부 조회
    /// </summary>
    /// <param name="stageId">도전 번호</param>
    /// <returns>준비 여부</returns>
    public bool IsPrepared ( int stageId )
    {
        return _stageId != 0 && _stageId == stageId;
    }

    /// <summary>
    /// 종료 보상의 계산액과 실제 지급액 비교
    /// </summary>
    /// <param name="actual">종료 보상 결과</param>
    /// <param name="message">비교 결과</param>
    /// <returns>검증 통과 여부</returns>
    public bool CompareReward ( ClearRewardResult actual, out string message )
    {
        if ( IsPrepared ( actual.StageId ) == false )
        {
            message = "준비한 검증 도전과 결과가 다릅니다.";
            return false;
        }
        ClearRewardState expectedState = _outcome == MissionOutcome.Abandoned
            ? ClearRewardState.NotEligible : ClearRewardState.Committed;
        if ( actual.State != expectedState )
        {
            message = $"보상 저장 미완료: {actual.State}";
            return false;
        }

        bool passed = actual.HasFinalScore == ( _outcome == MissionOutcome.Victory ) && actual.Score == _score
            && SameAmounts ( _expected, actual.CalculatedAmounts )
            && SameAmounts ( _expected, actual.PaidAmounts );
        message = $"보상 검증: 통과={passed} / 도전={_stageId} / 점수={actual.Score}"
            + $"\n예상: {DescribeAmounts ( _expected )}"
            + $"\n계산: {DescribeAmounts ( actual.CalculatedAmounts )}"
            + $"\n지급: {DescribeAmounts ( actual.PaidAmounts )}";
        return passed;
    }

    /// <summary>
    /// 도전별 예상값 해제
    /// </summary>
    public void Clear ( )
    {
        _stageId = 0;
        _score = 0;
        _expected = default;
    }

    /// <summary>
    /// 정산 전 지급 이력으로 예상 별 보상 산정
    /// </summary>
    /// <param name="before">정산 전 확정 저장값</param>
    /// <param name="context">현재 도전</param>
    /// <param name="policy">기존 별 정책</param>
    /// <param name="stars">승리 별</param>
    /// <param name="twoStarAmount">검증 기준의 최초 2성 금액</param>
    /// <param name="threeStarAmount">검증 기준의 최초 3성 추가 금액</param>
    /// <returns>예상 별 보상</returns>
    public static int GetStarAmount (
        SaveData before, MissionStageContext context, LegacyStarPolicy policy,
        int stars, int twoStarAmount, int threeStarAmount )
    {
        StarRewardRecord record = before.StarRewards.FirstOrDefault (
            item => item.MapId == context.MapId && item.Difficulty == context.Difficulty );
        int paidStars = record != null ? record.PaidStars : 0;
        if ( record != null && record.LegacyPending == true
            && policy == LegacyStarPolicy.AlreadyPaid )
        {
            //확정 저장값은 별 이력에 대응하는 클리어 기록 검사를 마친 상태
            StageClearRecord stage = before.StageRecords.First (
                item => item.MapId == context.MapId && item.Difficulty == context.Difficulty );
            paidStars = stage.BestStars;
        }

        int amount = 0;
        if ( stars >= 2 && paidStars < 2 ) amount += twoStarAmount;
        if ( stars >= 3 && paidStars < 3 ) amount += threeStarAmount;
        return amount;
    }

    /// <summary>
    /// 종류별 보상 금액 비교
    /// </summary>
    /// <param name="expected">예상 금액</param>
    /// <param name="actual">실제 금액</param>
    /// <returns>일치 여부</returns>
    static bool SameAmounts ( ClearRewardAmounts expected, ClearRewardAmounts actual )
    {
        return expected.ClearAmount == actual.ClearAmount
            && expected.StarAmount == actual.StarAmount
            && expected.FailureAmount == actual.FailureAmount;
    }

    /// <summary>
    /// 보상 금액 요약
    /// </summary>
    /// <param name="amounts">표시할 금액</param>
    /// <returns>종류별 금액과 합계</returns>
    static string DescribeAmounts ( ClearRewardAmounts amounts )
    {
        return $"클리어={amounts.ClearAmount} / 최초 별={amounts.StarAmount}"
            + $" / 패배={amounts.FailureAmount} / 합계={amounts.Total}";
    }

    #endregion

    #region ----- 재실행 복원 -----

    /// <summary>
    /// 확정 저장값을 재실행 비교 기준으로 보관
    /// </summary>
    /// <param name="saved">현재 확정 저장값</param>
    public static void CaptureRestore ( SaveData saved )
    {
        //게임 저장 없이 에디터에 보관하며 같은 Play 안에서는 복원 통과로 처리하지 않음
        SessionState.SetString ( RestoreKey, JsonUtility.ToJson ( saved ) );
        SessionState.SetBool ( RestoreKey + ".ready", false );
    }

    /// <summary>
    /// 재실행 후 불러온 진행과 기준값 비교
    /// </summary>
    /// <param name="restored">최초 불러오기를 마친 저장값</param>
    /// <param name="message">비교 결과</param>
    /// <returns>복원 검증 통과 여부</returns>
    public static bool CompareRestore ( SaveData restored, out string message )
    {
        if ( CanCompareRestore == false )
        {
            message = "비교 기준을 기록한 뒤 Play를 중단하고 다시 실행하세요.";
            return false;
        }

        SaveData before = JsonUtility.FromJson<SaveData> (
            SessionState.GetString ( RestoreKey, string.Empty ) );
        Dictionary<string, string> expected = DescribeRestore ( before );
        Dictionary<string, string> actual = DescribeRestore ( restored );
        List<string> differences = new List<string> ( );

        //목록 순서와 관계없이 맵/난이도별 기록을 대조
        foreach ( string key in expected.Keys.Union ( actual.Keys ) )
        {
            bool hadValue = expected.TryGetValue ( key, out string beforeValue );
            bool hasValue = actual.TryGetValue ( key, out string afterValue );
            if ( hadValue && hasValue && beforeValue == afterValue )
                continue;

            differences.Add ( $"{key}: {( hadValue ? beforeValue : "없음" )}"
                + $" → {( hasValue ? afterValue : "없음" )}" );
        }

        //최초 불러오기 한 번만 비교하고 결과는 Inspector에 유지
        CancelRestore ( );
        bool passed = differences.Count == 0;
        message = passed ? "재실행 복원 검증: 통과"
            : "재실행 복원 검증: 불일치\n" + string.Join ( "\n", differences );
        return passed;
    }

    /// <summary>
    /// 복원 비교 기준 취소
    /// </summary>
    public static void CancelRestore ( )
    {
        SessionState.EraseString ( RestoreKey );
        SessionState.EraseBool ( RestoreKey + ".ready" );
    }

    /// <summary>
    /// Play 중단 후 다음 불러오기 비교 허용
    /// </summary>
    /// <param name="state">Play 전환 상태</param>
    static void HandlePlayModeChanged ( PlayModeStateChange state )
    {
        if ( state == PlayModeStateChange.ExitingPlayMode && HasRestoreBaseline == true )
            SessionState.SetBool ( RestoreKey + ".ready", true );
    }

    /// <summary>
    /// 보상 관련 저장값을 비교 항목으로 구성
    /// </summary>
    /// <param name="data">비교할 저장값</param>
    /// <returns>항목별 기록</returns>
    static Dictionary<string, string> DescribeRestore ( SaveData data )
    {
        Dictionary<string, string> values = new Dictionary<string, string>
        {
            [ "저장 버전" ] = data.Version.ToString ( ),
            [ "영구 재화" ] = data.PermanentCurrencyBalance.ToString ( )
        };
        foreach ( StageClearRecord record in data.StageRecords )
            values.Add ( $"최고 기록/{record.MapId}/{record.Difficulty}",
                $"별={record.BestStars} / 점수 기록={record.HasScore} / 점수={record.BestScore}" );
        foreach ( StarRewardRecord record in data.StarRewards )
            values.Add ( $"별 지급/{record.MapId}/{record.Difficulty}",
                $"지급 단계={record.PaidStars} / 기존 정책 대기={record.LegacyPending}" );
        if ( data.HasClearReward == true )
        {
            ClearRewardRecord record = data.LastClearReward;
            values.Add ( "최근 종료 보상", $"{record.SettlementId} / {record.MapId}/{record.Difficulty}"
                + $" / {record.Outcome} / 별={record.Stars} / 점수={record.Score}"
                + $" / {DescribeAmounts ( record.Amounts )}" );
        }
        return values;
    }

    #endregion
}
#endif
