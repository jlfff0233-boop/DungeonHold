using System;
using System.IO;

/// <summary>
/// 로컬 설정 파일 - 설정 원문 읽기와 임시 파일 교체
/// </summary>
public class LocalSettingsFile
{
    string _directory;        //설정 저장 폴더
    string _path;             //확정 설정 파일
    string _temporaryPath;    //교체 전 임시 파일

#if UNITY_EDITOR
    /// <summary>
    /// 검증할 설정 파일 경로
    /// </summary>
    public string DebugSavePath => _path;
#endif

    /// <summary>
    /// 설정 파일 경로 구성
    /// </summary>
    /// <param name="directory">설정 저장 폴더</param>
    /// <param name="fileName">설정 파일명</param>
    public LocalSettingsFile ( string directory , string fileName )
    {
        _directory = directory;
        _path = Path.Combine ( directory , fileName );
        _temporaryPath = _path + ".tmp";
    }

    /// <summary>
    /// 설정 원문 읽기
    /// </summary>
    /// <param name="json">읽은 원문, 파일이 없으면 null</param>
    /// <param name="reason">읽기 실패 사유</param>
    /// <returns>읽기 성공 또는 파일 없음</returns>
    public bool TryRead ( out string json , out string reason )
    {
        json = null;
        reason = string.Empty;

        try
        {
            json = File.ReadAllText ( _path );
        }
        catch ( FileNotFoundException )
        {
            return true;
        }
        catch ( DirectoryNotFoundException )
        {
            return true;
        }
        catch ( Exception exception ) when (
            exception is IOException || exception is UnauthorizedAccessException )
        {
            reason = exception.Message;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 임시 파일 작성 후 확정 파일 교체
    /// </summary>
    /// <param name="json">저장할 원문</param>
    /// <param name="reason">저장 실패 사유</param>
    /// <returns>저장 성공 여부</returns>
    public bool TryWrite ( string json , out string reason )
    {
        reason = string.Empty;

        //원본을 먼저 삭제하지 않으며
        //실패한 임시 파일은 복원에 사용하지 않음
        try
        {
            Directory.CreateDirectory ( _directory );
            File.WriteAllText ( _temporaryPath , json );

            if ( File.Exists ( _path ) == true )
                File.Replace ( _temporaryPath , _path , null );
            else
                File.Move ( _temporaryPath , _path );
        }
        catch ( Exception exception ) when (
            exception is IOException
            || exception is UnauthorizedAccessException )
        {
            reason = exception.Message;
            return false;
        }

        return true;
    }
}
