using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 설정 관리자 - 설정 객체 소유와 초기화/종료 조율
/// </summary>
public class SettingsManager : IDisposable
{
    KeyBindingSettings _keyBindings;    //단축키 편집과 입력 대기
    DisplaySettings _display;           //화면 설정과 변경 요청
    VolumeSettings _volume;             //볼륨 설정과 출력 적용

    /// <summary>
    /// 단축키 설정
    /// </summary>
    public KeyBindingSettings KeyBindings => _keyBindings;

    /// <summary>
    /// 화면 설정
    /// </summary>
    public DisplaySettings Display => _display;

    /// <summary>
    /// 볼륨 설정
    /// </summary>
    public VolumeSettings Volume => _volume;

    /// <summary>
    /// 설정 객체와 로컬 저장소 생성
    /// </summary>
    /// <param name="actions">프로젝트 공용 입력 액션</param>
    /// <param name="directory">설정 저장 폴더</param>
    /// <param name="sound">실제 볼륨 출력 관리자</param>
    public SettingsManager ( InputActionAsset actions , string directory , SoundManager sound )
    {
        KeyBindingStorage keyStorage = new ( directory );
        _keyBindings = new KeyBindingSettings ( actions , keyStorage );

        DisplaySettingsStorage displayStorage = new ( directory );
        _display = new DisplaySettings ( displayStorage );

        VolumeSettingsStorage volumeStorage = new ( directory );
        _volume = new VolumeSettings ( volumeStorage , sound );
    }

    /// <summary>
    /// 설정별 저장값 복원
    /// </summary>
    public void Init ()
    {
        //한 설정의 복원 실패로 다른 설정의 초기화를 막지 않음
        if ( _keyBindings.Init ( out string keyReason ) == false )
            Debug.LogError ( $"단축키 설정 복원 실패: {keyReason}" );

        if ( _display.Init ( out string displayReason ) == false )
            Debug.LogError ( $"화면 설정 복원 실패: {displayReason}" );

        if ( _volume.Init ( out string volumeReason ) == false )
            Debug.LogError ( $"볼륨 설정 복원 실패: {volumeReason}" );
    }

    /// <summary>
    /// 시작 시 복원한 화면 설정으로 변경 요청
    /// </summary>
    public void ApplyInitialSettings ()
    {
        _display.ApplyInitialSettings ( );
    }

    /// <summary>
    /// 단축키 입력 대기와 Editor 입력 관찰 갱신
    /// </summary>
    public void Update ()
    {
        _keyBindings.UpdateCapture ( );
#if UNITY_EDITOR
        _keyBindings.UpdateDebugInputLog ( );
#endif
    }

    /// <summary>
    /// 포커스 상실 시 단축키 재지정 대기 취소
    /// </summary>
    public void HandleFocusLost ()
    {
        _keyBindings.CancelRebind ( );
    }

    /// <summary>
    /// 편집과 이벤트 참조를 알림 없이 정리
    /// </summary>
    public void Dispose ()
    {
        _keyBindings.Dispose ( );
        _display.Dispose ( );
        _volume.Dispose ( );
    }
}
