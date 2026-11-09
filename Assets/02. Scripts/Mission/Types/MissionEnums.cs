using System;

/// <summary>
/// 미션 난이도
/// </summary>
public enum MissionDifficulty
{
    Easy,       //쉬움
    Normal,     //보통
    Hard        //어려움
}

/// <summary>
/// 맵 방어 조건
/// </summary>
[Flags]
public enum MissionDefenseRule
{
    None = 0,       //방어 조건 없음
    NoPass = 1,     //통과 없음
    NoDamage = 2    //목표 피해 없음
}

/// <summary>
/// 도전 최종 결과
/// </summary>
public enum MissionOutcome
{
    Victory,        //승리
    Defeat,     //패배
    Abandoned       //포기
}

/// <summary>
/// 미션 처치 접수 결과
/// </summary>
public enum MissionKillResult
{
    Rejected,       //도전/구간/중복으로 거절
    Accepted,       //접수와 저장 성공
    AcceptedSavePending     //접수 성공, 저장 재시도 필요
}
