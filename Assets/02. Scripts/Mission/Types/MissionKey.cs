using System;

/// <summary>
/// 미션 키 - 고정 ID와 난이도로 기록 식별
/// </summary>
public readonly struct MissionKey : IEquatable<MissionKey>
{
    /// <summary>
    /// 미션 ID
    /// </summary>
    public string MissionId { get; }
    /// <summary>
    /// 기록을 구분할 난이도
    /// </summary>
    public MissionDifficulty Difficulty { get; }

    /// <summary>
    /// 난이도별 기록 키 생성
    /// </summary>
    /// <param name="id">미션 ID</param>
    /// <param name="difficulty">난이도</param>
    public MissionKey ( string id , MissionDifficulty difficulty )
    {
        MissionId = id;
        Difficulty = difficulty;
    }

    /// <summary>
    /// 같은 ID와 난이도의 키인지 비교
    /// </summary>
    /// <param name="other">비교할 키</param>
    /// <returns>같은 기록인지 여부</returns>
    public bool Equals ( MissionKey other )
    {
        return MissionId == other.MissionId && Difficulty == other.Difficulty;
    }

    /// <summary>
    /// 같은 타입의 기록 키와 값 비교
    /// </summary>
    /// <param name="obj">비교할 객체</param>
    /// <returns>같은 기록인지 여부</returns>
    public override bool Equals ( object obj )
    {
        return obj is MissionKey other && Equals ( other );
    }

    /// <summary>
    /// ID와 난이도의 딕셔너리 해시 계산
    /// </summary>
    /// <returns>키 해시</returns>
    public override int GetHashCode ( )
    {
        return HashCode.Combine ( MissionId , Difficulty );
    }
}
