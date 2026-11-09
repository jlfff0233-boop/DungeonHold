using UnityEngine;

/// <summary>
/// 타이틀 요청 연결
/// </summary>
public class TitleScene : MonoBehaviour
{
    [SerializeField] LobbyController _lobby; //로비 연결부

    /// <summary>
    /// 첫 실행 진행 준비 요청과 타이틀 도착 전달
    /// </summary>
    private void Start ()
    {
        GameManager gameManager = GameManager.Instance;
        if ( gameManager == null )
        {
            Debug.LogError( "공통 관리자가 준비되지 않았습니다.", this );
            return;
        }

        gameManager.PrepareForTitle( );
        gameManager.Scenes.AttachTitle( _lobby );
    }

    /// <summary>
    /// 사용자 시작 요청
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>연결 여부</returns>
    public bool TryOpenLobby ( out string reason )
    {
        return GameManager.Instance.Scenes.TryOpenLobby( _lobby, out reason );
    }

    /// <summary>
    /// 플레이 이동 요청
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>접수 여부</returns>
    public bool TryEnterStage ( out string reason )
    {
        return GameManager.Instance.Scenes.TryEnterStage( out reason );
    }

    /// <summary>
    /// 로비 닫기 요청
    /// </summary>
    /// <param name="reason">실패 사유</param>
    /// <returns>해제 여부</returns>
    public bool TryCloseLobby ( out string reason )
    {
        return GameManager.Instance.Scenes.TryCloseLobby( out reason );
    }

    /// <summary>
    /// 타이틀 연결 해제
    /// </summary>
    private void OnDestroy ()
    {
        GameManager gameManager = GameManager.Instance;
        if ( gameManager != null )
            gameManager.Scenes.DetachTitle( _lobby );
    }
}
