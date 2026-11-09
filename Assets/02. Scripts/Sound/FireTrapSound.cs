using UnityEngine;

/// <summary>
/// 화염 장판 사운드 - 전달받은 작동 상태와 지속음 연결
/// </summary>
[DisallowMultipleComponent]
[RequireComponent( typeof( SoundLoopPlayer ) )]
public class FireTrapSound : MonoBehaviour
{
    [SerializeField] SoundId _loopSound = SoundId.FireTrapLoop; //화염 장판 지속음

    SoundLoopPlayer _player; //이 장판의 반복 출력
    bool _wasOperating; //외부에서 마지막으로 전달받은 작동 상태
    bool _reportedMissingService; //초기화 순서 오류의 반복 알림 방지

    private void Awake ()
    {
        _player = GetComponent<SoundLoopPlayer>( );
    }

    /// <summary>
    /// 작동 상태를 전달받기 전에 공통 사운드 서비스 연결
    /// </summary>
    /// <param name="sound">공통 사운드 서비스</param>
    public void SetSound ( SoundManager sound )
    {
        //비활성 프리팹은 Awake 전에 서비스를 전달받을 수 있음
        if ( _player == null )
            _player = GetComponent<SoundLoopPlayer>( );
        _player.Init( sound );
        _wasOperating = false;
        _reportedMissingService = false;
        if ( sound == null )
        {
            Debug.LogWarning( "화염 장판 사운드 서비스가 연결되지 않았습니다.", this );
            _reportedMissingService = true;
        }
    }

    /// <summary>
    /// 외부에서 확정한 작동 상태 전환에 따라 지속음 시작 또는 중단
    /// </summary>
    /// <param name="isOperating">장판 작동 여부</param>
    public void SetOperating ( bool isOperating )
    {
        if ( isOperating == false )
        {
            _wasOperating = false;
            if ( _player != null )
                _player.Stop( );
            return;
        }
        if ( isActiveAndEnabled == false )
            return;
        if ( _player == null || _player.Service == null )
        {
            if ( _reportedMissingService == false )
            {
                Debug.LogWarning( "SetSound 이후에 화염 장판 작동을 요청하세요.", this );
                _reportedMissingService = true;
            }
            return;
        }
        if ( _wasOperating == isOperating )
            return;
        _wasOperating = _player.TryPlay( _loopSound );
    }

    private void OnDisable ()
    {
        _wasOperating = false;
        if ( _player != null )
            _player.Stop( );
    }
}
