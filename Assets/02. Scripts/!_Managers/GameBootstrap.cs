using UnityEngine;

/// <summary>
/// 게임 시작 준비 - 첫 씬 로드 전 공통 관리자 생성
/// </summary>
public static class GameBootstrap
{
    const string PrefabPath = "GameManager";      //Resources 내부 프리팹 경로

    /// <summary>
    /// 공통 관리자 프리팹 생성
    /// </summary>
    [RuntimeInitializeOnLoadMethod( RuntimeInitializeLoadType.BeforeSceneLoad )]
    static void CreateGameManager ()
    {
        GameManager prefab = Resources.Load<GameManager>( PrefabPath );

        if ( prefab == null )
        {
            Debug.LogError(
                "[게임 시작 실패] Resources/GameManager 프리팹을 확인하세요." );
            return;
        }

        Object.Instantiate( prefab );
    }
}
