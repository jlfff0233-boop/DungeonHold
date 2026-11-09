using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화면 설정 - 지원 목록과 저장값 복원/변경 요청
/// </summary>
public class DisplaySettings : IDisposable
{
    DisplaySettingsStorage _storage;    //화면 설정 로컬 저장소
    FullScreenMode [ ] _modes =
    {
        FullScreenMode.ExclusiveFullScreen,
        FullScreenMode.FullScreenWindow,
        FullScreenMode.Windowed
    };    //Windows에서 제공할 화면 모드

    List<Vector2Int> _resolutions = new ( );    //중복 제거한 해상도 목록
    DisplaySetting _settings;                  //마지막 접수 설정
    DisplaySetting _defaultSettings;           //현재 환경의 기본 설정
    string _loadError;                         //최초 복원 실패 사유

    /// <summary>
    /// 지원 화면 모드
    /// </summary>
    public IReadOnlyList<FullScreenMode> Modes => _modes;

    /// <summary>
    /// 선택 가능한 해상도
    /// </summary>
    public IReadOnlyList<Vector2Int> Resolutions => _resolutions;

    /// <summary>
    /// 마지막으로 접수한 설정, 실제 화면 전환 완료와 구분
    /// </summary>
    public DisplaySetting Settings => _settings;

    /// <summary>
    /// 현재 환경의 기본 설정
    /// </summary>
    public DisplaySetting DefaultSettings => _defaultSettings;

    /// <summary>
    /// 최초 복원 실패 사유
    /// </summary>
    public string LoadError => _loadError;

    /// <summary>
    /// 설정 변경 요청 접수 이벤트
    /// </summary>
    public event Action OnSettingsChanged;

    /// <summary>
    /// 화면 설정 저장소 연결
    /// </summary>
    /// <param name="storage">로컬 저장소</param>
    public DisplaySettings ( DisplaySettingsStorage storage )
    {
        _storage = storage;
    }

    /// <summary>
    /// 지원 목록 구성과 저장 설정 복원
    /// </summary>
    /// <param name="reason">복원 실패 사유</param>
    /// <returns>저장 설정 복원 성공 또는 파일 없음</returns>
    public bool Init ( out string reason )
    {
        Resolution native = Screen.currentResolution;
        Vector2Int nativeSize = new ( native.width , native.height );
        BuildResolutions ( nativeSize );

        _defaultSettings = new DisplaySetting (
            FullScreenMode.FullScreenWindow , nativeSize.x , nativeSize.y );

        //복원 실패 시에도 사용할 기본값을 먼저 준비하며 원본은 덮어쓰지 않음
        _settings = _defaultSettings;
        _loadError = string.Empty;

        if ( _storage.TryLoad (
            out DisplaySetting? saved , out reason ) == false )
        {
            _loadError = reason;
            return false;
        }

        if ( saved.HasValue == false )
        {
            Debug.Log ( $"[화면 설정 검증] 최초 초기화: 저장 파일 없음 / 기본값 "
                + $"{_settings.Mode} / {_settings.Width} x {_settings.Height}" );
            return true;
        }

        if ( Validate ( saved.Value , out reason ) == false )
        {
            _loadError = reason;
            return false;
        }

        _settings = saved.Value;
        Debug.Log ( $"[화면 설정 검증] 최초 복원 성공: "
            + $"{_settings.Mode} / {_settings.Width} x {_settings.Height}" );
        return true;
    }

    /// <summary>
    /// 복원한 설정으로 최초 화면 변경 요청
    /// </summary>
    public void ApplyInitialSettings ( )
    {
        //Init에서 확정한 값이므로 검사/저장/변경 알림을 반복하지 않음
        RequestScreenChange ( _settings );
    }

    /// <summary>
    /// 저장 없이 화면 변경 요청
    /// </summary>
    /// <param name="setting">변경할 설정</param>
    /// <param name="reason">거절 사유</param>
    /// <returns>변경 요청 접수 여부</returns>
    public bool TryApply ( DisplaySetting setting , out string reason )
    {
        return TryApplySettings ( setting , false , out reason );
    }

    /// <summary>
    /// 저장 성공 후 화면 변경 요청
    /// </summary>
    /// <param name="setting">저장하고 변경할 설정</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>저장과 변경 요청 접수 여부</returns>
    public bool TrySaveAndApply ( DisplaySetting setting , out string reason )
    {
        return TryApplySettings ( setting , true , out reason );
    }

    /// <summary>
    /// 설정 검사와 선택적 저장 후 화면 변경 요청
    /// </summary>
    /// <param name="setting">변경할 설정</param>
    /// <param name="save">파일 저장 여부</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>변경 요청 접수 여부</returns>
    bool TryApplySettings (
        DisplaySetting setting , bool save , out string reason )
    {
        if ( Validate ( setting , out reason ) == false )
            return false;

        //저장 실패 시 화면 요청과 접수 설정 변경/알림을 실행하지 않음
        if ( save == true
            && _storage.TrySave ( setting , out reason ) == false )
            return false;

        RequestScreenChange ( setting );

        //화면 전환은 프레임 끝에 반영되므로 알림은 요청 접수를 뜻함
        _settings = setting;
        OnSettingsChanged?.Invoke ( );
        return true;
    }

    /// <summary>
    /// Unity 화면 변경 요청
    /// </summary>
    /// <param name="setting">확정한 설정</param>
    void RequestScreenChange ( DisplaySetting setting )
    {
        Screen.SetResolution ( setting.Width , setting.Height , setting.Mode );
    }

    /// <summary>
    /// 현재 환경의 해상도 목록 구성
    /// </summary>
    /// <param name="nativeSize">현재 모니터 해상도</param>
    void BuildResolutions ( Vector2Int nativeSize )
    {
        _resolutions.Clear ( );

        foreach ( Resolution resolution in Screen.resolutions )
        {
            Vector2Int size = new ( resolution.width , resolution.height );

            //주사율만 다른 동일 크기를 하나로 합침
            if ( _resolutions.Contains ( size ) == false )
                _resolutions.Add ( size );
        }

        if ( _resolutions.Contains ( nativeSize ) == false )
            _resolutions.Add ( nativeSize );

        _resolutions.Sort ( ( left , right ) =>
        {
            int widthOrder = left.x.CompareTo ( right.x );
            return widthOrder != 0 ? widthOrder : left.y.CompareTo ( right.y );
        } );
    }

    /// <summary>
    /// 지원 모드와 해상도 검사
    /// </summary>
    /// <param name="setting">검사할 설정</param>
    /// <param name="reason">거절 사유</param>
    /// <returns>지원 설정 여부</returns>
    bool Validate ( DisplaySetting setting , out string reason )
    {
        reason = string.Empty;

        if ( Array.IndexOf ( _modes , setting.Mode ) < 0 )
        {
            reason = $"지원하지 않는 화면 모드입니다: {setting.Mode}";
            return false;
        }

        Vector2Int size = new ( setting.Width , setting.Height );

        if ( _resolutions.Contains ( size ) == false )
        {
            reason = $"현재 목록에 없는 해상도입니다: {setting.Width} x {setting.Height}";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 설정 변경 이벤트 참조 정리
    /// </summary>
    public void Dispose ( )
    {
        OnSettingsChanged = null;
    }
}
