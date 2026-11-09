using Newtonsoft.Json;

/// <summary>
/// 화면 설정 저장소 - 설정 직렬화와 읽기
/// </summary>
public class DisplaySettingsStorage
{
    LocalSettingsFile _file;    //화면 설정 파일

    /// <summary>
    /// 화면 설정 파일 구성
    /// </summary>
    /// <param name="directory">설정 저장 폴더</param>
    public DisplaySettingsStorage ( string directory )
    {
        _file = new LocalSettingsFile ( directory , "displaysettings.json" );
    }

    /// <summary>
    /// 저장한 화면 설정 읽기
    /// </summary>
    /// <param name="setting">읽은 설정</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>읽기 성공 또는 파일 없음</returns>
    public bool TryLoad ( out DisplaySetting? setting , out string reason )
    {
        setting = null;

        if ( _file.TryRead ( out string json , out reason ) == false )
            return false;

        if ( json == null )
            return true;

        if ( SaveJsonReader.TryReadDisplaySettings (
            json , out DisplaySetting candidate , out reason ) == false )
            return false;

        setting = candidate;
        return true;
    }

    /// <summary>
    /// 설정 직렬화 후 저장
    /// </summary>
    /// <param name="setting">검사를 마친 설정</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>저장 성공 여부</returns>
    public bool TrySave ( DisplaySetting setting , out string reason )
    {
        string json = JsonConvert.SerializeObject (
            new
            {
                mode = ( int ) setting.Mode ,
                width = setting.Width ,
                height = setting.Height
            } ,
            Formatting.Indented );

        return _file.TryWrite ( json , out reason );
    }
}
