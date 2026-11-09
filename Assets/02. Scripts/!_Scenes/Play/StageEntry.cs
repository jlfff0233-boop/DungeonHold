/// <summary>
/// 입장 선택 보관
/// </summary>
public class StageEntry
{
    StageEntrySelection? _pending; //시작 대기 선택
    StageEntrySelection? _current; //실제 도전 선택

    /// <summary>
    /// 대기 선택
    /// </summary>
    public StageEntrySelection? Pending => _pending;

    /// <summary>
    /// 도전 선택
    /// </summary>
    public StageEntrySelection? Current => _current;

    /// <summary>
    /// 입장 준비
    /// </summary>
    /// <param name="entry">선택</param>
    public void Prepare ( StageEntrySelection entry )
    {
        _pending = entry;
    }

    /// <summary>
    /// 같은 선택으로 재진입 준비
    /// </summary>
    /// <returns>입장 선택 존재 여부</returns>
    public bool PrepareRetry ()
    {
        //입장 실패 후 재진입은 대기 중인 선택 유지
        if ( _pending.HasValue == true )
            return true;

        _pending = _current;
        return _pending.HasValue;
    }

    /// <summary>
    /// 시작 성공 반영
    /// </summary>
    public void Commit ()
    {
        //씬 흐름이 시작 성공을 확인한 뒤 호출
        _current = _pending;
        _pending = null;
    }

    /// <summary>
    /// 대기 취소
    /// </summary>
    public void Cancel ()
    {
        _pending = null;
    }

    /// <summary>
    /// 로비 복귀 완료
    /// </summary>
    public void CompleteReturn ()
    {
        _pending = null;
        _current = null;
    }
}
