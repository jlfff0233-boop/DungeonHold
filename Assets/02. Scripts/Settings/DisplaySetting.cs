using UnityEngine;

/// <summary>
/// 화면 설정값 - 화면 모드와 해상도
/// </summary>
public struct DisplaySetting
{
    FullScreenMode _mode;       //전달할 화면 모드
    int _width;     //전달할 가로 크기
    int _height;        //전달할 세로 크기

    /// <summary>
    /// 화면 모드
    /// </summary>
    public FullScreenMode Mode => _mode;

    /// <summary>
    /// 가로 크기
    /// </summary>
    public int Width => _width;

    /// <summary>
    /// 세로 크기
    /// </summary>
    public int Height => _height;

    /// <summary>
    /// 화면 설정값 구성
    /// </summary>
    /// <param name="mode">화면 모드</param>
    /// <param name="width">가로 크기</param>
    /// <param name="height">세로 크기</param>
    public DisplaySetting ( FullScreenMode mode , int width , int height )
    {
        _mode = mode;
        _width = width;
        _height = height;
    }
}