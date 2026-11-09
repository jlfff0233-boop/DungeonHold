using Newtonsoft.Json;

/// <summary>
/// 볼륨 설정 저장소 - 설정 직렬화와 읽기
/// </summary>
public class VolumeSettingsStorage
{
    LocalSettingsFile _file;    //볼륨 설정 파일

    /// <summary>
    /// 볼륨 설정 파일 구성
    /// </summary>
    /// <param name="directory">설정 저장 폴더</param>
    public VolumeSettingsStorage ( string directory )
    {
        _file = new LocalSettingsFile ( directory , "volumesettings.json" );
    }

    /// <summary>
    /// 저장한 볼륨 설정 읽기
    /// </summary>
    /// <param name="setting">읽은 설정, 파일이 없으면 null</param>
    /// <param name="reason">읽기 실패 사유</param>
    /// <returns>읽기 성공 또는 파일 없음</returns>
    public bool TryLoad ( out VolumeSetting? setting , out string reason )
    {
        setting = null;

        if ( _file.TryRead ( out string json , out reason ) == false )
            return false;

        if ( json == null )
            return true;

        if ( SaveJsonReader.TryReadVolumeSettings (
            json , out VolumeSetting candidate , out reason ) == false )
            return false;

        setting = candidate;
        return true;
    }

    /// <summary>
    /// 설정 직렬화 후 저장
    /// </summary>
    /// <param name="setting">검사를 마친 설정</param>
    /// <param name="reason">저장 실패 사유</param>
    /// <returns>저장 성공 여부</returns>
    public bool TrySave ( VolumeSetting setting , out string reason )
    {
        string json = JsonConvert.SerializeObject (
            new
            {
                master = setting.Master ,
                bgm = setting.Bgm ,
                sfx = setting.Sfx
            } ,
            Formatting.Indented );

        return _file.TryWrite ( json , out reason );
    }
}
