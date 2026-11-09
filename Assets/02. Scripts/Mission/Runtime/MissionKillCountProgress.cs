/// <summary>
/// 미션 처치 집계 진행 - 현재 처치 수와 저장 완료한 처치 수 관리
/// </summary>
public class MissionKillCountProgress
{
    int _currentCount;      //미저장 접수를 포함한 처치 수
    int _savedCount;        //저장에 성공한 처치 수

    /// <summary>
    /// 현재 처치 수
    /// </summary>
    public int CurrentCount => _currentCount;
    /// <summary>
    /// 저장 완료한 처치 수
    /// </summary>
    public int SavedCount => _savedCount;

    /// <summary>
    /// 저장된 처치 수로 집계 진행 복원
    /// </summary>
    /// <param name="count">저장된 처치 수, 신규 기록은 0</param>
    public MissionKillCountProgress ( int count )
    {
        _currentCount = count;
        _savedCount = count;
    }

    /// <summary>
    /// 접수된 처치 한 번 반영
    /// </summary>
    public void AddKill ( )
    {
        //사건 접수 시 현재값만 증가하며 저장 성공 전까지 SavedCount는 마지막 확정값 유지
        _currentCount++;
    }

    /// <summary>
    /// 저장에 성공한 현재 처치 수 확정
    /// </summary>
    public void ApplySaved ( )
    {
        _savedCount = _currentCount;
    }
}
