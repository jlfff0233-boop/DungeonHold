/// <summary>
/// 메모리 저장소 - 검증용 진행과 복원 전 보관본 관리
/// </summary>
public class MemorySaveStorage : ISaveStorage
{
    SaveData _data;              //현재 저장한 진행
    SaveData _beforeRestore;     //복원 직전 보관한 진행

    /// <summary>
    /// 현재 진행 읽기
    /// </summary>
    /// <param name="data">읽은 진행 복사본</param>
    /// <param name="error">실패 내용</param>
    /// <returns>읽기 결과</returns>
    public SaveResult TryLoad (
        out SaveData data , out string error )
    {
        data = null;
        error = null;

        if ( _data == null )
            return SaveResult.NotFound;

        //호출부에서 수정해도 저장값은 유지
        data = _data.Copy ( );
        return SaveResult.Success;
    }

    /// <summary>
    /// 현재 진행 저장
    /// </summary>
    /// <param name="data">저장할 진행</param>
    /// <param name="error">실패 내용</param>
    /// <returns>저장 결과</returns>
    public SaveResult TrySave ( SaveData data , out string error )
    {
        error = null;

        //복사를 마친 뒤 새 저장값으로 교체
        SaveData snapshot = data.Copy ( );
        _data = snapshot;

        return SaveResult.Success;
    }

    /// <summary>
    /// 현재 진행과 보관본 삭제
    /// </summary>
    /// <param name="error">실패 내용</param>
    /// <returns>삭제 결과</returns>
    public SaveResult TryDelete ( out string error )
    {
        error = null;
        _data = null;
        _beforeRestore = null;

        return SaveResult.Success;
    }

    /// <summary>
    /// 복원 직전 진행 보관
    /// </summary>
    /// <param name="error">실패 내용</param>
    /// <returns>보관 결과</returns>
    public SaveResult TryPreserveBeforeRestore ( out string error )
    {
        error = null;

        if ( _data == null )
        {
            //원본 없는 복원에서 이전 보관본을 사용하지 않음
            _beforeRestore = null;
            return SaveResult.NotFound;
        }

        _beforeRestore = _data.Copy ( );
        return SaveResult.Success;
    }

    /// <summary>
    /// 복원 전 보관본 읽기
    /// </summary>
    /// <param name="data">보관한 진행 복사본</param>
    /// <param name="error">실패 내용</param>
    /// <returns>읽기 결과</returns>
    public SaveResult TryLoadBeforeRestore (
        out SaveData data , out string error )
    {
        data = null;
        error = null;

        if ( _beforeRestore == null )
            return SaveResult.NotFound;

        //복구 후보를 변경해도 보관본은 유지
        data = _beforeRestore.Copy ( );
        return SaveResult.Success;
    }
}
