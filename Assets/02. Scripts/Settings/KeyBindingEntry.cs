using System;

/// <summary>
/// 단축키 항목 - 기능과 개별 바인딩 식별 정보
/// </summary>
public class KeyBindingEntry
{
    string _actionPath;    //공용 액션 경로
    Guid _bindingId;       //개별 바인딩 식별자
    string _name;         //표시할 기능명

    /// <summary>
    /// 공용 액션 경로
    /// </summary>
    public string ActionPath => _actionPath;

    /// <summary>
    /// 개별 바인딩 식별자
    /// </summary>
    public Guid BindingId => _bindingId;

    /// <summary>
    /// 표시할 기능명
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// 단축키 목록 항목 생성
    /// </summary>
    /// <param name="actionPath">공용 액션 경로</param>
    /// <param name="bindingId">개별 바인딩 식별자</param>
    /// <param name="name">표시할 기능명</param>
    public KeyBindingEntry (
        string actionPath , Guid bindingId , string name )
    {
        _actionPath = actionPath;
        _bindingId = bindingId;
        _name = name;
    }
}
