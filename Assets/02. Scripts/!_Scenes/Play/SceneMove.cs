using System;
using LJPlay;
using UnityEngine;

/// <summary>
/// 이동 처리 상태
/// </summary>
public enum SceneMoveState
{
    Idle,
    WaitingForEnd,
    ReadyToLoad,
    Loading
}

/// <summary>
/// 종료 완료 후 이동 대기
/// </summary>
public class SceneMove
{
    PlayScene _scene; //종료 알림 소유자
    SceneMoveState _state; //이동 상태
    int _stageId; //대상 도전
    string _path; //대상 씬
    PlayEndFailure _failure; //마지막 종료 실패

    /// <summary>
    /// 이동 상태
    /// </summary>
    public SceneMoveState State => _state;

    /// <summary>
    /// 종료 실패 위치
    /// </summary>
    public PlayEndFailure Failure => _failure;

    /// <summary>
    /// 종료 결과 변경
    /// </summary>
    public event Action OnChanged;

    /// <summary>
    /// 이동 대기 시작
    /// </summary>
    /// <param name="scene">플레이 연결부</param>
    /// <param name="stageId">도전</param>
    /// <param name="path">대상 씬</param>
    /// <returns>접수 여부</returns>
    public bool TryPrepare ( PlayScene scene, int stageId, string path )
    {
        if ( _state != SceneMoveState.Idle )
            return false;

        if ( string.IsNullOrWhiteSpace( path ) == true
            || Application.CanStreamedLevelBeLoaded( path ) == false )
            return false;

        if ( stageId == 0 )
        {
            if ( scene.TrySavePendingProgress( ) == false )
                return false;

            _path = path;
            _state = SceneMoveState.ReadyToLoad;
            return true;
        }

        bool isCompleted = scene.IsStageCompleted( stageId );
        if ( isCompleted == false && scene.CurrentStageId != stageId )
            return false;

        _failure = scene.LastEndFailure;
        _scene = scene;
        _stageId = stageId;
        _path = path;
        _state = SceneMoveState.WaitingForEnd;

        //종료 요청 전에 구독하고 이미 완료된 결과도 조회
        _scene.OnStageEndProcessed += HandleStageEnd;
        if ( isCompleted == true )
            _state = SceneMoveState.ReadyToLoad;

        return true;
    }

    /// <summary>
    /// 종료 결과 수신
    /// </summary>
    /// <param name="stageId">도전</param>
    /// <param name="shouldDiscardCurrency">보상 폐기</param>
    /// <param name="isSucceeded">성공 여부</param>
    void HandleStageEnd ( int stageId, bool shouldDiscardCurrency, bool isSucceeded )
    {
        if ( stageId != _stageId )
            return;

        _failure = _scene.LastEndFailure;
        //남은 종료 구독자 전달 중에는 씬을 제거하지 않음
        if ( isSucceeded == true )
            _state = SceneMoveState.ReadyToLoad;

        //실패해도 구독을 유지해 명시적 재시도의 결과를 수신
        OnChanged?.Invoke( );
    }

    /// <summary>
    /// 로드 요청 확보
    /// </summary>
    /// <param name="path">대상 씬</param>
    /// <returns>로드 가능 여부</returns>
    public bool TryBeginLoad ( out string path )
    {
        path = null;
        if ( _state != SceneMoveState.ReadyToLoad )
            return false;

        _state = SceneMoveState.Loading;
        Unbind( );
        path = _path;
        return true;
    }

    /// <summary>
    /// 로드 시작 실패 반영
    /// </summary>
    public void FailLoad ()
    {
        if ( _state == SceneMoveState.Loading )
            _state = SceneMoveState.ReadyToLoad;
    }

    /// <summary>
    /// 연결 해제
    /// </summary>
    public void Dispose ()
    {
        Unbind( );
        _path = null;
        _stageId = 0;
        _failure = PlayEndFailure.None;
        _state = SceneMoveState.Idle;
    }

    /// <summary>
    /// 종료 구독 해제
    /// </summary>
    void Unbind ()
    {
        if ( _scene == null )
            return;

        _scene.OnStageEndProcessed -= HandleStageEnd;
        _scene = null;
    }
}
