using System;
using UnityEngine;

/// <summary>
/// 히어로 부활 모듈 - 사망 연출 대기, 자동 부활과 사용 횟수 관리
/// </summary>
public class HeroRevive
{
    Transform _owner;    //대상의 위치와 로그 기준
    Health _health;      //체력 복구와 생존 상태
    Mover _mover;        //부활 위치 이동
    PlayerReviveData _reviveData;    //플레이어 부활 설정
    Transform _revivePoint;         //지정 부활 위치, 미지정 시 사망 위치

    int _usedReviveCount;       //사용한 부활 횟수
    float _reviveCooltime;     //부활 쿨타임
    bool _isWaitingRevive;      //부활 대기 여부

    bool _isWaitingDeathEnd;        //현재 사망 연출의 완료 신호 대기 여부
    bool _isWaitingReviveEnd;       //현재 부활 연출의 완료 신호 대기 여부
    Vector3 _deathPosition;     //지정 부활 지점이 없을 때 사용할 사망 위치
    DamageInfo _deathDamageInfo;        //사망 연출 완료 후 소진 이벤트에 전달할 피해 정보

    /// <summary>
    /// 사용한 부활 횟수
    /// </summary>
    public int UsedReviveCount => _usedReviveCount;

    /// <summary>
    /// 자동 부활 대기 여부
    /// </summary>
    public bool IsWaitingRevive => _isWaitingRevive;

    /// <summary>
    /// 자동 부활까지 남은 게임 시간
    /// </summary>
    public float RemainingReviveTime => _reviveCooltime;

    /// <summary>
    /// 부활 연출 완료 대기 여부
    /// </summary>
    public bool IsWaitingReviveEnd => _isWaitingReviveEnd;

    /// <summary>
    /// 체력 복구 전에 일반 행동을 제한할 준비 알림
    /// </summary>
    public event Action OnRevivePreparing;

    /// <summary>
    /// 부활 횟수 소진 이벤트(마지막 사망 피해 정보)
    /// </summary>
    public event Action<DamageInfo> OnReviveExhausted;

    /// <summary>
    /// 부활 처리에 사용할 대상과 이동 모듈 연결
    /// </summary>
    /// <param name="owner">대상의 위치와 로그 기준</param>
    /// <param name="health">체력 모듈</param>
    /// <param name="mover">위치 이동 모듈</param>
    public HeroRevive ( Transform owner , Health health , Mover mover )
    {
        _owner = owner;
        _health = health;
        _mover = mover;
    }

    /// <summary>
    /// 부활 설정 검사와 연결
    /// </summary>
    /// <param name="reviveData">플레이어 부활 설정</param>
    /// <param name="revivePoint">지정 부활 위치</param>
    /// <returns>설정 적용 성공 여부</returns>
    public bool Init ( PlayerReviveData reviveData , Transform revivePoint )
    {
        //자동 부활에 사용할 설정이 유효한지 먼저 검사
        if ( reviveData.Delay >= 0f == false
            || float.IsInfinity ( reviveData.Delay ) == true
            || reviveData.HpRate > 0f == false
            || reviveData.HpRate > 1f
            || reviveData.MaxCount < 0 )
        {
            Debug.LogError ( "플레이어 부활 설정이 유효하지 않습니다." , _owner );
            return false;
        }

        _reviveData = reviveData;
        _revivePoint = revivePoint;
        return true;
    }

    /// <summary>
    /// 전투 초기화 후 연출 대기와 자동 부활 대기 정리
    /// </summary>
    public void ResetPresentationState ( )
    {
        _isWaitingDeathEnd = false;
        _isWaitingReviveEnd = false;
        CancelAutoRevive ( );
    }

    /// <summary>
    /// 재활성화 시 생존 상태와 횟수에 맞지 않는 자동 부활 대기 정리
    /// </summary>
    public void Resume ( )
    {
        if ( _health.IsAlive == true
            || _usedReviveCount >= _reviveData.MaxCount )
        {
            CancelAutoRevive ( );
        }
    }

    /// <summary>
    /// 자동 부활 대기 갱신과 실행
    /// </summary>
    /// <param name="deltaTime">게임 시간 기준 프레임 경과 시간</param>
    public void UpdateRevive ( float deltaTime )
    {
        if ( _isWaitingRevive == false )
            return;

        //이미 부활했거나 횟수가 소진된 경우 정상적인 대기 취소
        if ( _health.IsAlive == true
            || _usedReviveCount >= _reviveData.MaxCount )
        {
            CancelAutoRevive ( );
            return;
        }

        //일시정지와 유효하지 않은 경과 시간에는 대기를 진행하지 않음
        if ( deltaTime > 0f == false || float.IsInfinity ( deltaTime ) == true )
            return;

        _reviveCooltime = Mathf.Max ( 0f , _reviveCooltime - deltaTime );

        if ( _reviveCooltime > 0f )
            return;

        float hp = _health.MaxHp * _reviveData.HpRate;

        //실패하더라도 다음 프레임에 반복 요청하지 않도록 이번 대기 종료
        CancelAutoRevive ( );

        bool revived = TryRevive ( hp , true );

        if ( revived == false )
        {
            Debug.LogError (
                $"자동 부활 실패: 대상={_owner.gameObject.name}, " +
                $"요청 체력={hp}, 현재 체력={_health.CurrentHp}, " +
                $"최대 체력={_health.MaxHp}, 체력 비율={_reviveData.HpRate}, " +
                $"생존 여부={_health.IsAlive}, " +
                $"사용 횟수={_usedReviveCount}/{_reviveData.MaxCount}" ,
                _owner );

            return;
        }

        //실제 부활 성공만 횟수에 반영, 기존 공격 쿨타임은 유지
        _usedReviveCount++;
    }

    /// <summary>
    /// 테스트용 지정 체력 부활 요청 전달
    /// </summary>
    /// <param name="hp">적용할 체력</param>
    public void Revive ( float hp )
    {
        //테스트 호출은 위치 이동과 자동 부활 횟수 소비 없이 같은 체력 API 사용
        TryRevive ( hp , false );
    }

    /// <summary>
    /// 사망 정보 기록과 연출 완료 대기 준비
    /// </summary>
    /// <param name="damageInfo">피해 정보</param>
    public void PrepareDeath ( DamageInfo damageInfo )
    {
        _deathPosition = _owner.position;
        _deathDamageInfo = damageInfo;

        CancelAutoRevive ( );
        _isWaitingDeathEnd = true;
        _isWaitingReviveEnd = false;
    }

    /// <summary>
    /// 실제 체력 복구에 따른 사망 대기 정리
    /// </summary>
    public void HandleRevived ( )
    {
        _isWaitingDeathEnd = false;
        CancelAutoRevive ( );
    }

    /// <summary>
    /// 현재 사망 연출 완료 후 소진 알림 또는 자동 부활 대기 시작
    /// </summary>
    public void CompleteDeathPresentation ( )
    {
        //중복 완료 신호와 이미 부활한 대상의 신호는 무시
        if ( _isWaitingDeathEnd == false || _health.IsAlive == true )
            return;

        //외부 이벤트 전에 완료 상태를 반영해 중복 발행 방지
        _isWaitingDeathEnd = false;

        if ( _usedReviveCount >= _reviveData.MaxCount )
        {
            OnReviveExhausted?.Invoke ( _deathDamageInfo );
            return;
        }

        BeginAutoRevive ( );
    }

    /// <summary>
    /// 현재 부활 연출 완료 신호 처리
    /// </summary>
    /// <returns>부활 연출 대기가 실제 해제되었는지 여부</returns>
    public bool CompleteRevivePresentation ( )
    {
        if ( _isWaitingReviveEnd == false || _health.IsAlive == false )
            return false;

        _isWaitingReviveEnd = false;
        return true;
    }

    /// <summary>
    /// 부활 연출 대기를 준비하고 체력 복구 요청
    /// </summary>
    /// <param name="hp">적용할 체력</param>
    /// <param name="relocate">부활 위치로 이동할지 여부</param>
    /// <returns>실제 부활 성공 여부</returns>
    bool TryRevive ( float hp , bool relocate )
    {
        if ( _health.IsAlive == true )
            return false;

        Vector3 previousPosition = _owner.position;

        //HP 복구 이벤트 전에 행동 제한과 연출 대기 준비
        _isWaitingReviveEnd = true;
        OnRevivePreparing?.Invoke ( );

        if ( relocate == true )
        {
            Vector3 position = _revivePoint != null
                ? _revivePoint.position
                : _deathPosition;

            _mover.Teleport ( position );
        }

        bool revived = _health.Revive ( hp );

        if ( revived == false )
        {
            //실패한 요청은 원래 위치로 복귀하고 횟수를 소비하지 않음
            if ( relocate == true )
                _mover.Teleport ( previousPosition );

            _isWaitingReviveEnd = false;
            return false;
        }

        //비활성화 중 호출되어 부활 이벤트를 받지 못한 경우도 정리
        _isWaitingDeathEnd = false;
        CancelAutoRevive ( );

        return true;
    }

    /// <summary>
    /// 자동 부활 대기 시작
    /// </summary>
    void BeginAutoRevive ( )
    {
        _reviveCooltime = _reviveData.Delay;
        _isWaitingRevive = true;
    }

    /// <summary>
    /// 자동 부활 대기 취소
    /// </summary>
    void CancelAutoRevive ( )
    {
        _isWaitingRevive = false;
        _reviveCooltime = 0f;
    }

    /// <summary>
    /// 새 스테이지 시작 시 자동 부활 사용 횟수 초기화
    /// </summary>
    public void ResetReviveCount ( )
    {
        _usedReviveCount = 0;
    }
}
