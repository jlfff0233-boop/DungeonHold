using System;

/// <summary>
/// 볼륨 설정 - 저장값 복원과 출력 적용 조율
/// </summary>
public class VolumeSettings : IDisposable
{
    VolumeSettingsStorage _storage;    //볼륨 설정 저장소
    SoundManager _sound;               //실제 볼륨 출력
    VolumeSetting _settings;           //복원값 또는 마지막 적용 성공값
    VolumeSetting _defaultSettings = new ( 1f , 1f , 1f );    //기본 볼륨
    string _loadError;                 //최초 복원 실패 사유

    /// <summary>
    /// 복원값 또는 마지막 적용 성공값
    /// </summary>
    public VolumeSetting Settings => _settings;

    /// <summary>
    /// 기본 볼륨 설정
    /// </summary>
    public VolumeSetting DefaultSettings => _defaultSettings;

    /// <summary>
    /// 최초 복원 실패 사유
    /// </summary>
    public string LoadError => _loadError;

    /// <summary>
    /// 볼륨 적용 성공 후 설정 변경 알림
    /// </summary>
    public event Action OnSettingsChanged;

    /// <summary>
    /// 저장소와 출력 관리자 연결
    /// </summary>
    /// <param name="storage">볼륨 설정 저장소</param>
    /// <param name="sound">실제 출력 관리자</param>
    public VolumeSettings (
        VolumeSettingsStorage storage , SoundManager sound )
    {
        _storage = storage;
        _sound = sound;
    }

    /// <summary>
    /// 기본값 준비와 저장 설정 복원
    /// </summary>
    /// <param name="reason">복원 실패 사유</param>
    /// <returns>복원 성공 또는 파일 없음</returns>
    public bool Init ( out string reason )
    {
        //복원 실패 시 기본값으로 시작하며 원본 파일은 덮어쓰지 않음
        _settings = _defaultSettings;
        _loadError = string.Empty;

        if ( _storage.TryLoad ( out VolumeSetting? saved , out reason ) == false )
        {
            _loadError = reason;
            return false;
        }

        if ( saved.HasValue == false )
            return true;

        VolumeSetting candidate = saved.Value;

        if ( SoundManager.ValidateVolumes (
            candidate.Master , candidate.Bgm , candidate.Sfx , out reason ) == false )
        {
            _loadError = reason;
            return false;
        }

        _settings = candidate;
        return true;
    }

    /// <summary>
    /// 사운드 초기화 후 준비한 볼륨 최초 적용
    /// </summary>
    /// <param name="reason">출력 적용 실패 사유</param>
    /// <returns>출력 적용 성공 여부</returns>
    public bool ApplyInitialSettings ( out string reason )
    {
        //소유자가 출력 초기화를 완료했으며
        //복원값 검사/저장/변경 알림을 반복하지 않음
        return ApplyVolumes ( _settings , out reason );
    }

    /// <summary>
    /// 저장 없이 볼륨 적용
    /// </summary>
    /// <param name="setting">적용할 설정</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>적용 성공 여부</returns>
    public bool TryApply ( VolumeSetting setting , out string reason )
    {
        return TryApplySettings ( setting , false , out reason );
    }

    /// <summary>
    /// 파일 저장 성공 후 볼륨 적용
    /// </summary>
    /// <param name="setting">저장하고 적용할 설정</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>저장과 출력 적용 성공 여부</returns>
    public bool TrySaveAndApply ( VolumeSetting setting , out string reason )
    {
        return TryApplySettings ( setting , true , out reason );
    }

    /// <summary>
    /// 요청 검사와 선택적 저장 후 출력 적용
    /// </summary>
    /// <param name="setting">적용할 설정</param>
    /// <param name="save">파일 저장 여부</param>
    /// <param name="reason">실패 단계와 사유</param>
    /// <returns>요청 전체 성공 여부</returns>
    bool TryApplySettings ( VolumeSetting setting , bool save , out string reason )
    {
        //출력 준비 전에는 파일부터 바꾸지 않음
        if ( _sound.CanApplyVolumes == false )
        {
            reason = "사운드 초기화 후 볼륨을 적용해야 합니다.";
            return false;
        }

        if ( SoundManager.ValidateVolumes (
            setting.Master , setting.Bgm , setting.Sfx , out reason ) == false )
            return false;

        if ( save == true && _storage.TrySave ( setting , out reason ) == false )
        {
            reason = $"볼륨 설정 저장 실패: {reason}";
            return false;
        }

        if ( ApplyVolumes ( setting , out reason ) == false )
        {
            if ( save == true )
                reason = $"볼륨 설정 저장은 완료됐지만 출력 적용에 실패했습니다. {reason}";

            return false;
        }

        //세 채널 출력 적용이 성공한 뒤 조회값과 알림 반영
        _settings = setting;
        OnSettingsChanged?.Invoke ( );
        return true;
    }

    /// <summary>
    /// 준비한 설정을 실제 출력에 전달
    /// </summary>
    /// <param name="setting">검사를 마친 설정</param>
    /// <param name="reason">믹서 적용 또는 복구 실패 사유</param>
    /// <returns>출력 적용 성공 여부</returns>
    bool ApplyVolumes ( VolumeSetting setting , out string reason )
    {
        return _sound.TryApplyPreparedVolumes (
            setting.Master , setting.Bgm , setting.Sfx , out reason );
    }

    /// <summary>
    /// 변경 이벤트 참조 정리
    /// </summary>
    public void Dispose ( )
    {
        OnSettingsChanged = null;
    }
}
