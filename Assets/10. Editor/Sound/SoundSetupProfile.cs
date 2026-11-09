#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 사운드 설정 프로필 - 음원 배정 초안과 명시적인 연결 대상 보관
/// </summary>
[CreateAssetMenu( fileName = "SoundSetupProfile", menuName = "Sound/Setup Profile" )]
public class SoundSetupProfile : ScriptableObject
{
    /// <summary>
    /// 공용 음원 탐색 기본 폴더
    /// </summary>
    public const string DefaultAudioFolderPath = "Assets/99. Assets/Sound/SelectedAudio";

    [SerializeField] DefaultAsset _audioFolder; //메인 프로젝트의 음원 탐색 폴더
    [SerializeField] SoundDataMap _dataMap; //기존 런타임 데이터 맵
    [SerializeField] string _newMapPath = "Assets/03. Configs/SoundDataMap.asset"; //새 맵 생성 경로
    [SerializeField] GameObject _managerPrefab; //연결할 공통 관리자 프리팹
    [SerializeField] bool _connectManager = true; //관리자 데이터 맵 연결 여부
    [SerializeField] bool _replaceExisting; //충돌하는 기존 값의 교체 허용
    [SerializeField] List<SoundSetupEntry> _entries = new List<SoundSetupEntry>( ); //미확정 항목을 포함한 배정표
    [SerializeField] List<SoundSetupBinding> _bindings = new List<SoundSetupBinding>( ); //사용자가 지정한 연결 대상

    /// <summary>
    /// 메인 프로젝트의 음원 탐색 폴더
    /// </summary>
    public DefaultAsset AudioFolder => _audioFolder;

    /// <summary>
    /// 기존 런타임 데이터 맵
    /// </summary>
    public SoundDataMap DataMap => _dataMap;

    /// <summary>
    /// 새 맵 생성 경로
    /// </summary>
    public string NewMapPath => _newMapPath;

    /// <summary>
    /// 연결할 공통 관리자 프리팹
    /// </summary>
    public GameObject ManagerPrefab => _managerPrefab;

    /// <summary>
    /// 관리자 데이터 맵 연결 여부
    /// </summary>
    public bool ConnectManager => _connectManager;

    /// <summary>
    /// 충돌하는 기존 값의 교체 허용
    /// </summary>
    public bool ReplaceExisting => _replaceExisting;

    /// <summary>
    /// 미확정 항목을 포함한 배정표
    /// </summary>
    public IReadOnlyList<SoundSetupEntry> Entries => _entries;

    /// <summary>
    /// 사용자가 지정한 연결 대상
    /// </summary>
    public IReadOnlyList<SoundSetupBinding> Bindings => _bindings;

    /// <summary>
    /// 새 프로필의 기본 입력 폴더와 공통 관리자 지정
    /// </summary>
    /// <param name="audioFolder">음원 폴더</param>
    /// <param name="managerPrefab">공통 관리자 프리팹</param>
    public void InitializeDefaults ( DefaultAsset audioFolder, GameObject managerPrefab )
    {
        _audioFolder = audioFolder;
        _managerPrefab = managerPrefab;
    }

    /// <summary>
    /// JSON에서 해석한 신규 배정만 기존 초안에 추가
    /// </summary>
    /// <param name="entries">중복 선택 키를 제외한 신규 항목</param>
    public void AddEntries ( IEnumerable<SoundSetupEntry> entries )
    {
        _entries.AddRange( entries );
    }

    /// <summary>
    /// 자동 탐색 결과만 교체하고 사용자가 직접 지정한 연결은 유지
    /// </summary>
    /// <param name="bindings">현재 LJ 대상의 자동 연결 계획</param>
    public void ReplaceAutomaticBindings ( IEnumerable<SoundSetupBinding> bindings )
    {
        var previous = _bindings.FindAll( binding => binding.IsAutomatic );
        _bindings.RemoveAll( binding => binding.IsAutomatic );
        foreach ( SoundSetupBinding binding in bindings )
        {
            //사용자가 지정한 동일 필드가 있으면 자동 제안으로 중복 생성하지 않음
            if ( _bindings.Exists( item => item.IsAutomatic == false
                && item.Target == binding.Target && item.Field == binding.Field ) )
                continue;
            //같은 연결의 사용자 확인값과 공유 변경 선택은 다시 탐색해도 보존
            SoundSetupBinding existing = previous.Find( item => item.Target == binding.Target
                && item.Field == binding.Field && item.Id == binding.Id );
            _bindings.Add( existing ?? binding );
        }
    }

    /// <summary>
    /// 생성하거나 복구한 런타임 데이터 맵 연결 반영
    /// </summary>
    /// <param name="map">반영할 맵</param>
    public void SetDataMap ( SoundDataMap map )
    {
        _dataMap = map;
    }
}

/// <summary>
/// 사운드 배정 항목 - 하나의 선택 음원과 등록할 식별자
/// </summary>
[Serializable]
public class SoundSetupEntry
{
    [SerializeField] bool _register; //런타임 맵에 반영할지 여부
    [SerializeField] string _selectionKey; //JSON 재입력 시 중복을 구분하는 선택 키
    [SerializeField] string _purpose; //사용자가 선택한 원래 용도
    [SerializeField] SoundId _id; //실제 enum 숫자로 저장하는 식별자
    [SerializeField] SoundType _type = SoundType.Sfx; //출력 종류
    [SerializeField] AudioClip _clip; //프로젝트 안에서 찾은 음원
    [SerializeField] float _volume = 1f; //개별 음량
    [SerializeField] string _note; //호출 연계 또는 수동 배정 안내
    [SerializeField] bool _requiresLoopController; //반복 출력 모듈을 사용하는 지속음
    [SerializeField] bool _isReserve; //사용처를 정하지 않고 보관하는 예비 후보

    /// <summary>
    /// 런타임 맵에 반영할지 여부
    /// </summary>
    public bool Register => _register;

    /// <summary>
    /// JSON 재입력 시 중복을 구분하는 선택 키
    /// </summary>
    public string SelectionKey => _selectionKey;

    /// <summary>
    /// 사용자가 선택한 원래 용도
    /// </summary>
    public string Purpose => _purpose;

    /// <summary>
    /// 실제 enum 숫자로 저장하는 식별자
    /// </summary>
    public SoundId Id => _id;

    /// <summary>
    /// 출력 종류
    /// </summary>
    public SoundType Type => _type;

    /// <summary>
    /// 프로젝트 안에서 찾은 음원
    /// </summary>
    public AudioClip Clip => _clip;

    /// <summary>
    /// 개별 음량
    /// </summary>
    public float Volume => _volume;

    /// <summary>
    /// 호출 연계 또는 수동 배정 안내
    /// </summary>
    public string Note => _note;

    /// <summary>
    /// 반복 출력 모듈을 사용하는 지속음
    /// </summary>
    public bool RequiresLoopController => _requiresLoopController;

    /// <summary>
    /// 사용처를 정하지 않고 보관하는 예비 후보
    /// </summary>
    public bool IsReserve => _isReserve;

    /// <summary>
    /// 선택 원본 정보와 음원 참조를 보존한 편집 항목 생성
    /// </summary>
    /// <param name="selectionKey">중복 선택 구분 키</param>
    /// <param name="purpose">선택한 용도</param>
    /// <param name="clip">프로젝트 음원</param>
    /// <param name="note">연계 안내</param>
    /// <param name="requiresLoopController">반복 출력 모듈 사용 여부</param>
    /// <param name="isReserve">예비 후보 보관 여부</param>
    public SoundSetupEntry ( string selectionKey, string purpose, AudioClip clip,
        string note, bool requiresLoopController, bool isReserve = false )
    {
        _selectionKey = selectionKey;
        _purpose = purpose;
        _clip = clip;
        _note = note;
        _requiresLoopController = requiresLoopController;
        _isReserve = isReserve;
    }

    /// <summary>
    /// 식별자와 출력 설정을 하나의 배정으로 반영
    /// </summary>
    /// <param name="id">등록할 식별자</param>
    /// <param name="type">출력 종류</param>
    /// <param name="register">등록 여부</param>
    /// <param name="volume">개별 음량</param>
    public void SetRegistration ( SoundId id, SoundType type, bool register, float volume = 1f )
    {
        _id = id;
        _type = type;
        _register = register;
        _volume = volume;
    }

    /// <summary>
    /// 기존 배정값은 유지하고 미싱 음원 참조만 복구
    /// </summary>
    /// <param name="clip">파일명으로 찾은 프로젝트 음원</param>
    public void RestoreMissingClip ( AudioClip clip )
    {
        if ( _clip == null && clip != null )
            _clip = clip;
    }

}

/// <summary>
/// 사운드 연결 필드 - 기존 투사체와 지속 영역의 설정 위치
/// </summary>
public enum SoundSetupField
{
    ProjectileLaunch = 0, //투사체 발사
    ProjectileImpact = 1, //투사체 충돌
    AreaSpawn = 2, //영역 생성
    AreaTick = 3, //영역 주기 타격
    FireTrapLoop = 4 //화염 장판 지속음과 반복 모듈
}

/// <summary>
/// 사운드 연결 항목 - 서비스 연결 확인을 거친 명시적인 필드 변경
/// </summary>
[Serializable]
public class SoundSetupBinding
{
    [SerializeField] UnityEngine.Object _target; //ProjectileData 또는 영역/화염 장판 프리팹
    [SerializeField] SoundSetupField _field; //설정할 기존 필드
    [SerializeField] SoundId _id; //적용할 식별자
    [SerializeField] bool _serviceConnected; //실행 전에 SetSound가 전달되는지 사용자가 확인
    [SerializeField] bool _allowSharedChange; //여러 에셋에서 사용하는 데이터의 변경 허용
    [SerializeField] bool _isAutomatic; //자동 탐색에서 만든 연결 여부

    /// <summary>
    /// 자동 탐색에서 만든 연결 여부
    /// </summary>
    public bool IsAutomatic => _isAutomatic;

    /// <summary>
    /// Inspector에서 직접 편집할 빈 연결 항목 생성
    /// </summary>
    public SoundSetupBinding ()
    {
    }

    /// <summary>
    /// 실제 서비스 전달 확인과 구분한 자동 연결 초안 생성
    /// </summary>
    /// <param name="target">원본 연결 대상</param>
    /// <param name="field">지원 필드 종류</param>
    /// <param name="id">자동 배정한 식별자</param>
    public SoundSetupBinding ( UnityEngine.Object target, SoundSetupField field, SoundId id )
    {
        _target = target;
        _field = field;
        _id = id;
        _serviceConnected = false;
        _isAutomatic = true;
    }

    /// <summary>
    /// ProjectileData 또는 영역/화염 장판 프리팹
    /// </summary>
    public UnityEngine.Object Target => _target;

    /// <summary>
    /// 설정할 기존 필드
    /// </summary>
    public SoundSetupField Field => _field;

    /// <summary>
    /// 적용할 식별자
    /// </summary>
    public SoundId Id => _id;

    /// <summary>
    /// 실행 전에 SetSound가 전달되는지 사용자가 확인
    /// </summary>
    public bool ServiceConnected => _serviceConnected;

    /// <summary>
    /// 여러 에셋에서 사용하는 데이터의 변경 허용
    /// </summary>
    public bool AllowSharedChange => _allowSharedChange;
}
#endif
