using System;
using System.IO;
using UnityEngine;

/// <summary>
/// JSON 저장소 - 슬롯 파일 읽기와 쓰기
/// </summary>
public class JsonSaveStorage : ISaveStorage
{
    string _directoryPath;      //저장 폴더 경로
    string _savePath;       //슬롯 본 파일 경로
    string _temporaryPath;      //쓰기 중 사용할 임시 파일 경로
    string _backupPath;         //교체 전 정상 파일 경로
    string _beforeRestorePath;      //복원 직전 원본
    string _beforeRestoreTempPath;  //원본 보관 임시 파일

    /// <summary>
    /// 슬롯 번호에 따른 저장 경로 준비
    /// </summary>
    /// <param name="slotId">저장 슬롯 번호</param>
    public JsonSaveStorage ( int slotId )
        : this ( slotId, Path.Combine ( Application.persistentDataPath, "Saves" ) )
    {
    }

    /// <summary>
    /// 지정한 폴더의 슬롯 저장 경로 준비
    /// </summary>
    /// <param name="slotId">저장 슬롯 번호</param>
    /// <param name="directoryPath">저장 폴더</param>
    public JsonSaveStorage ( int slotId , string directoryPath )
    {
        _directoryPath = directoryPath;

        _savePath = Path.Combine ( _directoryPath , $"slot_{slotId}.json" );

        //일반 저장은 임시 파일 작성 후 본 파일 교체, 직전 파일은 .bak으로 보관
        _temporaryPath = _savePath + ".tmp";
        _backupPath = _savePath + ".bak";
        //복원 직전 보관본은 이후 일반 저장과 별도로 유지
        _beforeRestorePath = _savePath + ".before_restore";
        _beforeRestoreTempPath = _beforeRestorePath + ".tmp";
    }

#if UNITY_EDITOR
    /// <summary>
    /// 검증할 슬롯 파일 경로
    /// </summary>
    public string DebugSavePath => _savePath;

    /// <summary>
    /// 검증할 복원 전 보관본 경로
    /// </summary>
    public string DebugBeforeRestorePath => _beforeRestorePath;
#endif

    /// <summary>
    /// 슬롯 파일을 읽어 저장 데이터로 변환
    /// </summary>
    /// <param name="data">읽은 데이터</param>
    /// <param name="error">실패 원인, 정상 처리 시 null</param>
    /// <returns>읽기와 변환 결과</returns>
    public SaveResult TryLoad ( out SaveData data , out string error )
    {
        return ReadData ( _savePath , out data , out error );
    }

    /// <summary>
    /// 복원 전 보관본 읽기
    /// </summary>
    /// <param name="data">읽은 진행</param>
    /// <param name="error">실패 내용</param>
    /// <returns>읽기 결과</returns>
    public SaveResult TryLoadBeforeRestore ( out SaveData data , out string error )
    {
        //보관본 복구는 명시적 요청에서만 수행하며 일반 불러오기의 대체 파일로 쓰지 않음
        return ReadData ( _beforeRestorePath , out data , out error );
    }

    /// <summary>
    /// 지정한 로컬 파일 읽기와 JSON 변환
    /// </summary>
    /// <param name="path">읽을 파일</param>
    /// <param name="data">읽은 진행</param>
    /// <param name="error">실패 내용</param>
    /// <returns>읽기 결과</returns>
    SaveResult ReadData ( string path , out SaveData data , out string error )
    {
        data = null;
        error = null;

        string json;

        //읽기와 JSON 변환이 모두 끝나야 호출부에 데이터 전달
        //파일 없음만 신규 생성 대상으로 반환하고, 나머지 실패는 기존 파일 유지
        try
        {
            json = File.ReadAllText ( path );
        }
        catch ( FileNotFoundException )
        {
            //저장 폴더는 있지만 해당 슬롯 파일이 없는 상태
            return SaveResult.NotFound;
        }
        catch ( DirectoryNotFoundException )
        {
            //최초 실행처럼 저장 폴더 자체가 아직 없는 상태
            return SaveResult.NotFound;
        }
        catch ( Exception exception ) when (
            exception is IOException
            || exception is UnauthorizedAccessException )
        {
            //잠금/권한 등의 읽기 실패는 신규 생성과 구분
            error = $"{path} / {exception.Message}";
            return SaveResult.ReadFailed;
        }

        //JSON 해석/필수 배열 검사/변환은 로컬과 외부 복원이 같은 경로 사용
        SaveResult result = SaveJsonReader.ReadProgress ( json, out data, out error );

        if ( result != SaveResult.Success )
            error = $"{path} / {error}";

        return result;
    }

    /// <summary>
    /// 임시 파일 작성을 마친 뒤 슬롯 본 파일에 반영
    /// </summary>
    /// <param name="data">저장할 데이터</param>
    /// <param name="error">실패 원인, 정상 처리 시 null</param>
    /// <returns>파일 저장 결과</returns>
    public SaveResult TrySave ( SaveData data , out string error )
    {
        error = null;

        //데이터 구성 오류는 파일 쓰기 실패로 숨기지 않음
        string json = JsonUtility.ToJson ( data , true );

        //임시 파일 작성을 먼저 완료
        //실패 시 재시도 여부는 서비스가 결정하며 이 함수는 한 번만 저장
        try
        {
            Directory.CreateDirectory ( _directoryPath );
            File.WriteAllText ( _temporaryPath , json );

            if ( File.Exists ( _savePath ) == true )
                File.Replace ( _temporaryPath , _savePath , _backupPath );
            else
                File.Move ( _temporaryPath , _savePath );
        }
        catch ( Exception exception ) when (
            exception is IOException
            || exception is UnauthorizedAccessException )
        {
            //기존 본 파일을 먼저 삭제하는 대체 동작은 수행하지 않음
            //남은 임시 파일은 다음 저장에서 다시 작성하고 불러오기에는 사용하지 않음
            error = $"{_savePath} / {exception.Message}";
            return SaveResult.WriteFailed;
        }

        return SaveResult.Success;
    }

    /// <summary>
    /// 복원 직전 본 파일을 별도 보관
    /// </summary>
    /// <param name="error">실패 내용</param>
    /// <returns>보존 결과, 원본이 없으면 NotFound</returns>
    public SaveResult TryPreserveBeforeRestore ( out string error )
    {
        error = null;
        bool hadOriginal = true;

        //보관 임시 파일을 완성한 후에만 기존 보관본 교체
        try
        {
            File.Copy ( _savePath , _beforeRestoreTempPath , true );
        }
        catch ( FileNotFoundException )
        {
            hadOriginal = false;
        }
        catch ( DirectoryNotFoundException )
        {
            return SaveResult.NotFound;
        }
        catch ( Exception exception ) when (
            exception is IOException || exception is UnauthorizedAccessException )
        {
            error = exception.Message;
            return SaveResult.WriteFailed;
        }

        try
        {
            //원본 없는 복원에서 예전 보관본을 현재 원본으로 오인하지 않도록 정리
            if ( hadOriginal == false )
            {
                File.Delete ( _beforeRestoreTempPath );
                File.Delete ( _beforeRestorePath );
                return SaveResult.NotFound;
            }

            //복사가 끝난 임시 보관본으로 교체하고 본 파일은 그대로 유지
            if ( File.Exists ( _beforeRestorePath ) == true )
                File.Replace ( _beforeRestoreTempPath , _beforeRestorePath , null );
            else
                File.Move ( _beforeRestoreTempPath , _beforeRestorePath );
        }
        catch ( Exception exception ) when (
            exception is IOException || exception is UnauthorizedAccessException )
        {
            error = exception.Message;
            return SaveResult.WriteFailed;
        }

        return SaveResult.Success;
    }

    /// <summary>
    /// 진행 파일과 부속 파일 삭제
    /// </summary>
    /// <param name="error">삭제 실패 원인</param>
    /// <returns>삭제 결과</returns>
    public SaveResult TryDelete ( out string error )
    {
        //로컬 진행 파일만 삭제하며 Windows 인증 정보와 서버 백업은 유지
        error = null;

        try
        {
            //부속 파일 정리가 실패하면 본 파일 유지
            File.Delete ( _beforeRestoreTempPath );
            File.Delete ( _beforeRestorePath );
            File.Delete ( _temporaryPath );
            File.Delete ( _backupPath );
            File.Delete ( _savePath );
        }
        catch ( DirectoryNotFoundException )
        {
            return SaveResult.Success;
        }
        catch ( Exception exception ) when (
            exception is IOException
            || exception is UnauthorizedAccessException )
        {
            error = $"{_savePath} / {exception.Message}";
            return SaveResult.DeleteFailed;
        }

        return SaveResult.Success;
    }
}
