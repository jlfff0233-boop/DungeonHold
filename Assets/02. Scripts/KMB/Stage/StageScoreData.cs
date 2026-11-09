using UnityEngine;

[CreateAssetMenu(
    fileName = "StageScoreData",
    menuName = "KMB/Stage/Score Data")]
public class StageScoreData : ScriptableObject
{
    // 클리어 기본 점수
    [SerializeField, Min(0)]
    private int _clearScore = 1000;

    // 팀 전체의 적 처치 1회당 점수
    [SerializeField, Min(0)]
    private int _scorePerKill = 100;

    // Stage 1: 적 통과 1회당 감점
    [SerializeField, Min(0)]
    private int _passedEnemyPenalty = 100;

    // Stage 2: 방어 목표 HP가 100%일 때의 추가 점수
    [SerializeField, Min(0)]
    private int _maxDefenseScore = 1000;

    public bool IsValid =>
        _clearScore >= 0 &&
        _scorePerKill >= 0 &&
        _passedEnemyPenalty >= 0 &&
        _maxDefenseScore >= 0;

    // Stage 1 클리어 점수
    public bool TryCalculateStage1ClearScore(
        int killCount,
        int passedCount,
        out int score)
    {
        score = 0;

        if (!IsValid || killCount < 0 || passedCount < 0)
        {
            return false;
        }

        long totalScore =
            (long)_clearScore +
            (long)_scorePerKill * killCount -
            (long)_passedEnemyPenalty * passedCount;

        return TryConvertScore(totalScore, out score);
    }

    // Stage 2 클리어 점수
    // 비율을 float로 먼저 계산하지 않고 원본 HP로 계산
    public bool TryCalculateStage2ClearScore(
        int killCount,
        float currentHp,
        float maxHp,
        out int score)
    {
        score = 0;

        bool hasValidRecord =
            IsValid &&
            killCount >= 0 &&
            !float.IsNaN(currentHp) &&
            !float.IsInfinity(currentHp) &&
            !float.IsNaN(maxHp) &&
            !float.IsInfinity(maxHp) &&
            currentHp > 0f &&
            maxHp > 0f &&
            currentHp <= maxHp;

        if (!hasValidRecord)
        {
            return false;
        }

        // double로 곱한 뒤 나누어 중간 float 비율의 오차 방지
        // HP 추가 점수의 소수점 이하 버림 규칙은 유지
        double defenseScoreValue =
            (double)_maxDefenseScore * currentHp / maxHp;

        long defenseScore =
            (long)System.Math.Floor(defenseScoreValue);

        long totalScore =
            (long)_clearScore +
            (long)_scorePerKill * killCount +
            defenseScore;

        return TryConvertScore(totalScore, out score);
    }

    public bool TryCalculateStage3ClearScore(
    int killCount,
    int passedCount,
    float currentHp,
    float maxHp,
    out int score)
    {
        score = 0;

        if (!IsValid || killCount < 0 || passedCount < 0 ||
            float.IsNaN(currentHp) || float.IsInfinity(currentHp) ||
            float.IsNaN(maxHp) || float.IsInfinity(maxHp) ||
            currentHp <= 0f || maxHp <= 0f || currentHp > maxHp)
        {
            return false;
        }

        long defenseScore = (long)System.Math.Floor(
            (double)_maxDefenseScore * currentHp / maxHp);

        long totalScore =
            (long)_clearScore +
            (long)_scorePerKill * killCount +
            defenseScore -
            (long)_passedEnemyPenalty * passedCount;

        return TryConvertScore(totalScore, out score);
    }

    private bool TryConvertScore(long totalScore, out int score)
    {
        score = 0;

        // 음수 점수는 정상적인 최저 점수인 0점으로 처리
        if (totalScore < 0)
        {
            totalScore = 0;
        }

        // 전달할 수 없는 큰 점수는 계산 실패로 구분
        if (totalScore > int.MaxValue)
        {
            return false;
        }

        score = (int)totalScore;
        return true;
    }
}