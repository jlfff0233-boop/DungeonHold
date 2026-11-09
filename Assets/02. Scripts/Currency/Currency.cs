using System;
using UnityEngine;

/// <summary>
/// 재화 모듈 - 영구 재화/스테이지 재화 잔액, 지급, 소비와 도전 수명 관리
/// </summary>
public class Currency : MonoBehaviour
{
    [Header ( "----- 읽기 전용: 재화 상태 -----" )]
    [SerializeField] int _permanentCurrencyBalance;    //현재 저장 슬롯의 영구 재화 잔액
    [SerializeField] int _stageCurrencyBalance;        //현재 도전의 스테이지 재화 잔액

    int _currentStageId;       //현재 또는 마지막 도전 번호
    bool _isInitialized;      //영구 재화 잔액 초기화 성공 여부
    bool _isStageActive;      //스테이지 재화 사용 허용 여부

    /// <summary>
    /// 현재 또는 마지막 도전 번호
    /// </summary>
    public int CurrentStageId => _currentStageId;

    /// <summary>
    /// 현재 도전의 진행 여부
    /// </summary>
    public bool IsStageActive => _isStageActive;

    /// <summary>
    /// 재화 잔액 변경 이벤트(재화 종류, 변경 후 재화 잔액)
    /// </summary>
    public event Action<CurrencyType , int> OnBalanceChanged;

    /// <summary>
    /// 영구 재화 저장 요청 이벤트(변경 후 잔액, 최종 저장 성공 여부)
    /// </summary>
    public event Func<int , bool> OnPermanentSaveRequested;

    /// <summary>
    /// 저장 데이터의 영구 재화 잔액 적용
    /// </summary>
    /// <param name="permanentCurrencyBalance">새 데이터 또는 불러온 데이터의 영구 재화 잔액</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init ( int permanentCurrencyBalance )
    {
        //도전 중 저장 슬롯을 바꾸거나 재화 잔액을 재초기화하지 않음
        if ( isActiveAndEnabled == false
            || _isStageActive == true
            || permanentCurrencyBalance < 0 )
            return false;

        _permanentCurrencyBalance = permanentCurrencyBalance;
        _stageCurrencyBalance = 0;
        _isInitialized = true;

        //도전 번호는 유지하여 이전 도전의 늦은 요청과 구분
        //모든 초기 상태를 반영한 뒤 최초 표시용 알림
        OnBalanceChanged?.Invoke ( CurrencyType.Permanent , _permanentCurrencyBalance );
        OnBalanceChanged?.Invoke ( CurrencyType.Stage , _stageCurrencyBalance );

        return true;
    }

    /// <summary>
    /// 지정 재화의 현재 재화 잔액 조회
    /// </summary>
    /// <param name="type">조회할 재화 종류</param>
    /// <returns>현재 재화 잔액</returns>
    public int GetBalance ( CurrencyType type )
    {
        switch ( type )
        {
            case CurrencyType.Permanent:
                return _permanentCurrencyBalance;

            case CurrencyType.Stage:
                return _stageCurrencyBalance;

            default:
                throw new ArgumentOutOfRangeException (
                    nameof ( type ) , type , "지원하지 않는 재화 종류입니다." );
        }
    }

    /// <summary>
    /// 지정 금액의 소비 가능 여부 확인
    /// </summary>
    /// <param name="type">소비할 재화 종류</param>
    /// <param name="amount">소비할 금액</param>
    /// <returns>전액 소비 가능 여부</returns>
    public bool CanSpend ( CurrencyType type , int amount )
    {
        if ( CanUseCurrency ( type ) == false || amount < 0 )
            return false;

        return GetBalance ( type ) >= amount;
    }

    /// <summary>
    /// 지정 금액 소비, 영구 재화는 저장 성공 후 반영
    /// </summary>
    /// <param name="type">소비할 재화 종류</param>
    /// <param name="amount">소비할 금액</param>
    /// <returns>소비 성공 여부</returns>
    public bool TrySpend ( CurrencyType type , int amount )
    {
        if ( CanSpend ( type , amount ) == false )
            return false;

        //0원 요청은 성공하지만 재화 잔액 변경 알림은 발행하지 않음
        if ( amount == 0 )
            return true;

        int currencyBalance = GetBalance ( type ) - amount;
        return TryApplyBalance ( type , currencyBalance );
    }

    /// <summary>
    /// 지정 금액 지급, 영구 재화는 저장 성공 후 반영
    /// </summary>
    /// <param name="type">지급할 재화 종류</param>
    /// <param name="amount">지급할 금액</param>
    /// <returns>지급 성공 여부</returns>
    public bool TryAdd ( CurrencyType type , int amount )
    {
        if ( CanUseCurrency ( type ) == false || amount < 0 )
            return false;

        int currencyBalance = GetBalance ( type );

        //정수 범위를 넘는 지급은 재화 잔액을 변경하지 않음
        if ( amount > int.MaxValue - currencyBalance )
            return false;

        if ( amount == 0 )
            return true;

        return TryApplyBalance ( type , currencyBalance + amount );
    }

    /// <summary>
    /// 새 도전의 스테이지 재화 설정
    /// </summary>
    /// <param name="initialStageCurrencyBalance">난이도 설정에서 전달한 시작 스테이지 재화</param>
    /// <returns>도전 시작 성공 여부</returns>
    public bool BeginStage ( int initialStageCurrencyBalance )
    {
        if ( isActiveAndEnabled == false
            || _isInitialized == false
            || _isStageActive == true
            || initialStageCurrencyBalance < 0
            || _currentStageId == int.MaxValue )
            return false;

        //새 번호와 진행 상태를 먼저 반영하여 변경 알림에서 조회 가능
        _currentStageId++;
        _isStageActive = true;

        ApplyBalance ( CurrencyType.Stage , initialStageCurrencyBalance );
        return true;
    }

    /// <summary>
    /// 현재 도전의 스테이지 재화 지급
    /// </summary>
    /// <param name="stageId">보상이 발생한 원래 도전 번호</param>
    /// <param name="amount">지급할 금액</param>
    /// <returns>현재 도전의 지급 성공 여부</returns>
    public bool TryAddStageReward ( int stageId , int amount )
    {
        if ( _isStageActive == false
            || stageId != _currentStageId )
            return false;

        return TryAdd ( CurrencyType.Stage , amount );
    }

    /// <summary>
    /// 지정 도전 종료와 스테이지 재화 잔액 폐기
    /// </summary>
    /// <param name="stageId">종료할 도전 번호</param>
    /// <returns>현재 도전 종료 성공 여부</returns>
    public bool EndStage ( int stageId )
    {
        if ( _isInitialized == false
            || _isStageActive == false
            || stageId != _currentStageId )
            return false;

        //지급을 먼저 차단한 뒤 스테이지 재화 잔액 정리와 알림
        _isStageActive = false;
        ApplyBalance ( CurrencyType.Stage , 0 );
        return true;
    }

    /// <summary>
    /// 통합 저장에 성공한 영구 재화 잔액 적용
    /// </summary>
    /// <param name="balance">저장 완료된 영구 재화 잔액</param>
    public void ApplySavedPermanentBalance ( int balance )
    {
        ApplyBalance ( CurrencyType.Permanent , balance );
    }

    /// <summary>
    /// 재화 종류와 현재 사용 조건 확인
    /// </summary>
    /// <param name="type">사용할 재화 종류</param>
    /// <returns>해당 재화 사용 가능 여부</returns>
    bool CanUseCurrency ( CurrencyType type )
    {
        if ( isActiveAndEnabled == false
            || _isInitialized == false )
            return false;

        switch ( type )
        {
            case CurrencyType.Permanent:
                return true;

            case CurrencyType.Stage:
                return _isStageActive;

            default:
                return false;
        }
    }

    /// <summary>
    /// 영구 재화 저장 성공 여부를 확인한 뒤 잔액 변경
    /// </summary>
    /// <param name="type">변경할 재화 종류</param>
    /// <param name="currencyBalance">변경 후 재화 잔액</param>
    /// <returns>저장과 잔액 변경 성공 여부</returns>
    bool TryApplyBalance ( CurrencyType type , int currencyBalance )
    {
        if ( type == CurrencyType.Permanent )
        {
            //저장 담당 하나만 구독하며 연결 누락 시 영구 재화를 변경하지 않음
            if ( OnPermanentSaveRequested == null )
            {
                Debug.LogError ( "영구 재화 저장 요청이 연결되지 않았습니다." , this );
                return false;
            }

            if ( OnPermanentSaveRequested.Invoke ( currencyBalance ) == false )
                return false;
        }

        ApplyBalance ( type , currencyBalance );
        return true;
    }

    /// <summary>
    /// 실제 재화 잔액 반영 후 변경 알림
    /// </summary>
    /// <param name="type">변경할 재화 종류</param>
    /// <param name="currencyBalance">변경 후 재화 잔액</param>
    void ApplyBalance ( CurrencyType type , int currencyBalance )
    {
        if ( GetBalance ( type ) == currencyBalance )
            return;

        switch ( type )
        {
            case CurrencyType.Permanent:
                _permanentCurrencyBalance = currencyBalance;
                break;

            case CurrencyType.Stage:
                _stageCurrencyBalance = currencyBalance;
                break;
        }

        OnBalanceChanged?.Invoke ( type , currencyBalance );
    }
}
