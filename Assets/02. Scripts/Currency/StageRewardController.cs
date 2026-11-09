using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 보상 연결부 - 처치 접수, 콤보와 재화 지급 조율
/// </summary>
public class StageRewardController : MonoBehaviour
{
    Currency _currency;           //실제 재화 지급 모듈
    Combo _combo;                 //현재 도전의 콤보

    HashSet<long> _handledLives = new HashSet<long> ( );    //현재 도전에 접수한 생명 식별값
    Queue<RewardRequest> _pending = new Queue<RewardRequest> ( );    //미지급 보상 요청

    int _stageId;                 //연결부가 시작한 도전 번호
    bool _isInitialized;          //초기화 완료 여부
    bool _isProcessing;           //동기 알림을 포함한 상태 변경 처리 중 여부
    double _lastGameTime;         //마지막으로 접수한 게임 시간
    long _nextLifeId;             //현재 도전에서 마지막으로 발급한 생명 식별값

    /// <summary>
    /// 현재 도전 번호, 종료 후에는 0
    /// </summary>
    public int CurrentStageId => _stageId;

    /// <summary>
    /// 현재 콤보 처치 수
    /// </summary>
    public int ComboKillCount => _combo == null ? 0 : _combo.KillCount;

    /// <summary>
    /// 다음 보상 기준 처치 수
    /// </summary>
    public int NextRewardKillCount => _combo == null ? 0 : _combo.NextRewardKillCount;

    /// <summary>
    /// 미지급 요청 수
    /// </summary>
    public int PendingRewardCount => _pending.Count;

    /// <summary>
    /// 현재 스테이지 재화 잔액
    /// </summary>
    public int StageCurrencyBalance =>
        _currency == null ? 0 : _currency.GetBalance ( CurrencyType.Stage );

    /// <summary>
    /// 콤보 변경 이벤트(현재 처치 수, 다음 보상 기준 처치 수)
    /// </summary>
    public event Action<int, int> OnComboChanged;

    /// <summary>
    /// 보상 지급 완료 이벤트(원래 처치의 보상 요청)
    /// </summary>
    public event Action<RewardRequest> OnRewardPaid;

    /// <summary>
    /// 진행 도전이 없는 상태에서 재화 모듈과 설정 연결
    /// </summary>
    /// <param name="currency">실제 재화 모듈</param>
    /// <param name="data">콤보 설정</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ( Currency currency, ComboData data )
    {
        if ( isActiveAndEnabled == false
            || _isProcessing == true
            || currency == null
            || data == null
            || data.IsValid ( ) == false
            || currency.IsStageActive == true )
            return false;

        if ( _currency != null && _currency.IsStageActive == true )
            return false;

        _currency = currency;
        _combo = new Combo ( data );
        _isInitialized = true;
        ClearStageState ( );
        return true;
    }

    /// <summary>
    /// 재화와 콤보의 새 도전 시작
    /// </summary>
    /// <param name="initialStageCurrencyBalance">시작 스테이지 재화</param>
    /// <returns>도전 시작 성공 여부</returns>
    public bool BeginStage ( int initialStageCurrencyBalance )
    {
        if ( isActiveAndEnabled == false
            || _isInitialized == false
            || _isProcessing == true )
            return false;

        _isProcessing = true;

        try
        {
            if ( _currency.BeginStage ( initialStageCurrencyBalance ) == false )
                return false;

            ClearStageState ( );
            _stageId = _currency.CurrentStageId;
            NotifyComboChanged ( );
            return true;
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// 현재 도전에 생성된 적의 새 생명 식별값 발급
    /// </summary>
    /// <param name="stageId">생명이 속할 도전 번호</param>
    /// <param name="lifeId">발급한 생명 식별값</param>
    /// <returns>발급 성공 여부</returns>
    public bool TryIssueLifeId ( int stageId, out long lifeId )
    {
        lifeId = 0;

        if ( IsCurrentStage ( stageId ) == false || _nextLifeId == long.MaxValue )
            return false;

        lifeId = ++_nextLifeId;
        return true;
    }

    /// <summary>
    /// 사망 한 건의 처치와 보상 요청 접수
    /// </summary>
    /// <param name="stageId">처치가 발생한 원래 도전 번호</param>
    /// <param name="lifeId">현재 도전에서 유일한 생명 식별값</param>
    /// <param name="killAmount">처치 보상액</param>
    /// <param name="now">상위 코드에서 전달한 게임 시간</param>
    /// <returns>새 처치 접수 여부, 실제 지급 성공과 구분</returns>
    public bool RegisterKill ( int stageId, long lifeId, int killAmount, double now )
    {
        if ( isActiveAndEnabled == false
            || _isProcessing == true
            || IsCurrentStage ( stageId ) == false
            || lifeId <= 0
            || killAmount < 0
            || now < _lastGameTime
            || _handledLives.Contains ( lifeId ) == true )
            return false;

        if ( _combo.TryGetNextKillReward ( now, out int comboAmount ) == false )
            return false;

        //상태를 변경하기 전에 한 처치의 합산 금액 범위 확인
        if ( killAmount > int.MaxValue - comboAmount )
            return false;

        _isProcessing = true;

        try
        {
            if ( _combo.TryRegisterKill ( now, out comboAmount ) == false )
                return false;

            _lastGameTime = now;
            _handledLives.Add ( lifeId );

            //동기 재화 알림이 발생하기 전에 접수와 미지급 요청 확정
            _pending.Enqueue ( new RewardRequest (
                stageId, lifeId, _combo.KillCount, killAmount, comboAmount ) );

            PayPendingRewards ( );
            NotifyComboChanged ( );
            return true;
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// 게임 시간 진행에 따른 콤보 만료 처리
    /// </summary>
    /// <param name="now">상위 코드에서 전달한 게임 시간</param>
    public void UpdateCombo ( double now )
    {
        if ( isActiveAndEnabled == false
            || _isProcessing == true
            || IsCurrentStage ( _stageId ) == false
            || now < _lastGameTime )
            return;

        _lastGameTime = now;

        if ( _combo.Expire ( now ) == false )
            return;

        _isProcessing = true;

        try
        {
            NotifyComboChanged ( );
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// 콤보 남은 시간 조회
    /// </summary>
    /// <param name="now">상위 코드에서 전달한 게임 시간</param>
    /// <returns>남은 유지시간</returns>
    public double GetComboRemainingTime ( double now )
    {
        return IsCurrentStage ( _stageId ) == true
            ? _combo.GetRemainingTime ( now )
            : 0d;
    }

    /// <summary>
    /// 처치를 다시 집계하지 않고 미지급 보상 재시도
    /// </summary>
    /// <returns>모든 미지급 요청 처리 여부</returns>
    public bool RetryPendingRewards ( )
    {
        if ( isActiveAndEnabled == false
            || _isProcessing == true
            || IsCurrentStage ( _stageId ) == false )
            return false;

        _isProcessing = true;

        try
        {
            return PayPendingRewards ( );
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// 미지급 처리 후 현재 도전 정상 종료
    /// </summary>
    /// <param name="stageId">종료할 도전 번호</param>
    /// <returns>도전 종료 성공 여부</returns>
    public bool EndStage ( int stageId )
    {
        if ( _isProcessing == true || IsCurrentStage ( stageId ) == false )
            return false;

        //미지급이 없으면 비활성 상태에서도 명시적으로 종료 가능
        if ( _pending.Count > 0 && isActiveAndEnabled == false )
            return false;

        _isProcessing = true;

        try
        {
            if ( PayPendingRewards ( ) == false )
                return false;

            if ( _currency.EndStage ( stageId ) == false )
                return false;

            ClearStageState ( );
            NotifyComboChanged ( );
            return true;
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// 남은 스테이지 보상을 폐기하고 현재 도전 포기
    /// </summary>
    /// <param name="stageId">포기할 도전 번호</param>
    /// <returns>도전 종료 성공 여부</returns>
    public bool AbandonStage ( int stageId )
    {
        if ( _isProcessing == true || IsCurrentStage ( stageId ) == false )
            return false;

        _isProcessing = true;

        try
        {
            if ( _currency.EndStage ( stageId ) == false )
                return false;

            ClearStageState ( );
            NotifyComboChanged ( );
            return true;
        }
        finally
        {
            _isProcessing = false;
        }
    }

    /// <summary>
    /// 처리 중인 호출 안에서 미지급 요청을 순서대로 지급
    /// </summary>
    /// <returns>모든 요청 처리 여부</returns>
    bool PayPendingRewards ( )
    {
        while ( _pending.Count > 0 )
        {
            RewardRequest request = _pending.Peek ( );

            if ( _currency.TryAddStageReward ( request.StageId, request.TotalAmount ) == false )
                return false;

            //지급 완료 알림 전에 요청 제거하여 같은 요청 재지급 방지
            _pending.Dequeue ( );
            OnRewardPaid?.Invoke ( request );
        }

        return true;
    }

    /// <summary>
    /// 현재 도전의 콤보, 중복과 미지급 기록 정리
    /// </summary>
    void ClearStageState ( )
    {
        _combo.Reset ( );
        _handledLives.Clear ( );
        _pending.Clear ( );
        _stageId = 0;
        _lastGameTime = 0d;
        _nextLifeId = 0;
    }

    /// <summary>
    /// 반영된 콤보 상태 알림
    /// </summary>
    void NotifyComboChanged ( )
    {
        OnComboChanged?.Invoke ( _combo.KillCount, _combo.NextRewardKillCount );
    }

    /// <summary>
    /// 보상 연결부와 재화의 현재 도전 일치 여부 확인
    /// </summary>
    /// <param name="stageId">확인할 도전 번호</param>
    /// <returns>현재 진행 도전 여부</returns>
    bool IsCurrentStage ( int stageId )
    {
        return _isInitialized == true
            && _currency != null
            && _currency.IsStageActive == true
            && _stageId > 0
            && stageId == _stageId
            && stageId == _currency.CurrentStageId;
    }
}
