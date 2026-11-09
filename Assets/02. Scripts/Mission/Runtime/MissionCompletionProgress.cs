/// <summary>
/// 미션 완료 진행 - 현재 완료 기록과 저장 완료한 기록 관리
/// </summary>
public class MissionCompletionProgress
{
    MissionSaveData _current;       //최신 완료/지급 기록
    MissionSaveData _saved;     //저장에 성공한 완료/지급 기록

    /// <summary>
    /// 현재 완료/지급 기록
    /// </summary>
    public MissionSaveData Current => _current;
    /// <summary>
    /// 저장 완료한 완료/지급 기록
    /// </summary>
    public MissionSaveData Saved => _saved;

    /// <summary>
    /// 저장된 기록으로 미션 진행 복원
    /// </summary>
    /// <param name="record">저장된 기록, 신규 미션은 미완료 기록</param>
    public MissionCompletionProgress ( MissionSaveData record )
    {
        //저장 기록은 내부 값을 수정하지 않고 교체하므로 복원 시 같은 기록을 참조해도 됨
        _current = record;
        _saved = record;
    }

    /// <summary>
    /// 신규 완료를 미지급 상태로 기록
    /// </summary>
    public void Complete ( )
    {
        //기존 저장 기록을 수정하지 않고 현재 기록만 교체해 저장 실패 시 확정 상태 보존
        _current = new MissionSaveData ( _current.MissionId , _current.Difficulty , true , false );
    }

    /// <summary>
    /// 저장에 성공한 완료/지급 기록 확정
    /// </summary>
    /// <param name="record">저장된 기록</param>
    public void ApplySaved ( MissionSaveData record )
    {
        _current = record;
        _saved = record;
    }
}
