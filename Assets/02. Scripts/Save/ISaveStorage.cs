/// <summary>
/// 진행 저장소 - 진행 읽기/저장과 복원 전 보관
/// </summary>
public interface ISaveStorage
{
    /// <summary>
    /// 현재 진행 읽기
    /// </summary>
    /// <param name="data">읽은 진행</param>
    /// <param name="error">실패 내용</param>
    /// <returns>읽기 결과</returns>
    SaveResult TryLoad ( out SaveData data , out string error );

    /// <summary>
    /// 현재 진행 저장
    /// </summary>
    /// <param name="data">저장할 진행</param>
    /// <param name="error">실패 내용</param>
    /// <returns>저장 결과</returns>
    SaveResult TrySave ( SaveData data , out string error );

    /// <summary>
    /// 현재 진행과 보관본 삭제
    /// </summary>
    /// <param name="error">실패 내용</param>
    /// <returns>삭제 결과</returns>
    SaveResult TryDelete ( out string error );

    /// <summary>
    /// 복원 직전 진행 보관
    /// </summary>
    /// <param name="error">실패 내용</param>
    /// <returns>보관 결과</returns>
    SaveResult TryPreserveBeforeRestore ( out string error );

    /// <summary>
    /// 복원 전 보관본 읽기
    /// </summary>
    /// <param name="data">보관한 진행</param>
    /// <param name="error">실패 내용</param>
    /// <returns>읽기 결과</returns>
    SaveResult TryLoadBeforeRestore (
        out SaveData data , out string error );
}
