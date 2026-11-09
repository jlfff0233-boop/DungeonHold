/// <summary>
/// 볼륨 설정값 - 마스터/BGM/SFX 볼륨 전달
/// </summary>
public struct VolumeSetting
{
    float _master;    //마스터 볼륨
    float _bgm;       //BGM 볼륨
    float _sfx;       //효과음 볼륨

    /// <summary>
    /// 마스터 볼륨
    /// </summary>
    public float Master => _master;

    /// <summary>
    /// BGM 볼륨
    /// </summary>
    public float Bgm => _bgm;

    /// <summary>
    /// 효과음 볼륨
    /// </summary>
    public float Sfx => _sfx;

    /// <summary>
    /// 볼륨 설정값 구성
    /// </summary>
    /// <param name="master">마스터 볼륨</param>
    /// <param name="bgm">BGM 볼륨</param>
    /// <param name="sfx">효과음 볼륨</param>
    public VolumeSetting ( float master , float bgm , float sfx )
    {
        _master = master;
        _bgm = bgm;
        _sfx = sfx;
    }
}
