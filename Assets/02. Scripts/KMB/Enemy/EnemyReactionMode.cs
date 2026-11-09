public enum EnemyReactionMode
{
    Limited = 0,      // 정해진 횟수 반격 후 목적지 복귀
    Continuous = 1,   // 대상이 유효한 동안 지속 반격
    IgnorePlayer = 2  // 플레이어 반격 없이 목적지 이동
}