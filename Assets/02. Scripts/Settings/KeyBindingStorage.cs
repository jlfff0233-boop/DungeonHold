using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 단축키 저장 데이터 - 변경한 바인딩 목록
/// </summary>
[Serializable]
public class KeyBindingSaveData
{
    [SerializeField] List<KeyBindingOverride> _bindings;    //변경한 배정 목록

    /// <summary>
    /// 변경한 배정 목록
    /// </summary>
    public IReadOnlyList<KeyBindingOverride> Bindings => _bindings;

    /// <summary>
    /// 저장할 변경 배정 구성
    /// </summary>
    /// <param name="bindings">변경한 배정 목록</param>
    public KeyBindingSaveData ( List<KeyBindingOverride> bindings )
    {
        _bindings = bindings;
    }
}

/// <summary>
/// 단축키 변경 기록 - 바인딩 식별자와 입력 경로
/// </summary>
[Serializable]
public class KeyBindingOverride
{
    [SerializeField] string _bindingId;    //저장할 바인딩 식별자
    [SerializeField] string _path;         //변경한 입력 경로

    /// <summary>
    /// 저장할 바인딩 식별자
    /// </summary>
    public string BindingId => _bindingId;

    /// <summary>
    /// 변경한 입력 경로
    /// </summary>
    public string Path => _path;

    /// <summary>
    /// 개별 변경 배정 생성
    /// </summary>
    /// <param name="bindingId">바인딩 식별자</param>
    /// <param name="path">변경한 입력 경로</param>
    public KeyBindingOverride ( string bindingId , string path )
    {
        _bindingId = bindingId;
        _path = path;
    }
}

/// <summary>
/// 단축키 저장소 - 로컬 설정 읽기/쓰기
/// </summary>
public class KeyBindingStorage
{
    LocalSettingsFile _file;    //단축키 설정 파일

#if UNITY_EDITOR
    /// <summary>
    /// 검증할 실제 설정 파일 경로
    /// </summary>
    public string DebugSavePath => _file.DebugSavePath;
#endif

    /// <summary>
    /// 단축키 저장 경로 구성
    /// </summary>
    /// <param name="directory">설정 저장 폴더</param>
    public KeyBindingStorage ( string directory )
    {
        _file = new LocalSettingsFile ( directory , "keybindings.json" );
    }

    /// <summary>
    /// 저장된 변경 배정 읽기
    /// </summary>
    /// <param name="data">저장 데이터, 최초 실행이면 null</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>읽기 성공 또는 저장 파일 없음</returns>
    public bool TryLoad (
        out KeyBindingSaveData data , out string reason )
    {
        data = null;
        if ( _file.TryRead ( out string json , out reason ) == false )
            return false;

        //공통 파일 처리에서 null은 파일 없음을 뜻함
        if ( json == null )
            return true;

        //누락과 정상 빈 배열은 원문에서 구분하고 성공한 후보만 전달
        return SaveJsonReader.TryReadKeyBindings ( json, out data, out reason );
    }

    /// <summary>
    /// 임시 파일 작성 후 확정 설정 파일 교체
    /// </summary>
    /// <param name="data">저장할 변경 배정</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>저장 성공 여부</returns>
    public bool TrySave (
        KeyBindingSaveData data , out string reason )
    {
        string json = JsonUtility.ToJson ( data , true );
        return _file.TryWrite ( json , out reason );
    }
}
