using System;
using UnityEngine;

/// <summary>
/// 히어로 회피 모듈 - 회피 이동, 무적, 쿨타임과 종료 관리
/// </summary>
public class HeroDodge
{
    Mover _mover;       //회피 회전과 실제 이동
    DamageReceiver _damageReceiver;     //무적 적용 대상
    DodgeData _data;        //회피 설정

    bool _isDodging;        //회피 진행 여부
    Vector3 _direction;     //시작 시 확정한 수평 방향
    float _elapsedTime;     //이번 회피의 경과 시간
    float _movedTime;       //이미 이동에 반영한 진행 시간
    float _remainingCooldown;       //남은 회피 쿨타임

    /// <summary>
    /// 회피 진행 여부
    /// </summary>
    public bool IsDodging => _isDodging;

    /// <summary>
    /// 무적 유지 여부
    /// </summary>
    public bool IsInvulnerable =>
        _isDodging && _elapsedTime < _data.InvulnerableDuration;

    /// <summary>
    /// 남은 회피 쿨타임
    /// </summary>
    public float RemainingCooldown => _remainingCooldown;

    /// <summary>
    /// 회피 시작 알림
    /// </summary>
    public event Action OnStarted;

    /// <summary>
    /// 회피 종료 알림(종료 사유)
    /// </summary>
    public event Action<AttackEndReason> OnEnded;

    /// <summary>
    /// 회피 이동과 무적 적용 대상 연결
    /// </summary>
    /// <param name="mover">이동 모듈</param>
    /// <param name="damageReceiver">피해 수신 모듈</param>
    public HeroDodge ( Mover mover , DamageReceiver damageReceiver )
    {
        _mover = mover;
        _damageReceiver = damageReceiver;
    }

    /// <summary>
    /// 회피 설정 검사와 연결
    /// </summary>
    /// <param name="data">회피 설정</param>
    /// <returns>설정 적용 성공 여부</returns>
    public bool Init ( DodgeData data )
    {
        if ( data == null
            || data.Distance <= 0f
            || data.Duration <= 0f
            || data.InvulnerableDuration < 0f
            || data.InvulnerableDuration > data.Duration
            || data.Cooldown < 0f )
        {
            Debug.LogError ( "플레이어 회피 설정이 유효하지 않습니다." , _mover );
            return false;
        }

        //진행 행동은 Hero가 초기화 전에 거절하며 기존 쿨타임은 유지
        _data = data;
        return true;
    }

    /// <summary>
    /// 지상 회피 시작 요청
    /// </summary>
    /// <param name="direction">월드 기준 이동 입력 방향</param>
    /// <returns>회피 시작 여부</returns>
    public bool TryStart ( Vector3 direction )
    {
        if ( _isDodging == true
            || _remainingCooldown > 0f
            || _mover.CanDodge == false )
            return false;

        direction.y = 0f;
        _direction = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : _mover.transform.forward;

        //시작 알림에서 Hero가 CanAct를 재계산하므로 상태부터 확정
        _isDodging = true;
        _elapsedTime = 0f;
        _movedTime = 0f;
        _remainingCooldown = _data.Cooldown;

        _damageReceiver.SetInvulnerable ( IsInvulnerable );
        _mover.FaceDodgeDirection ( _direction );

        //방향 적용 후 Hero가 이동 입력을 정리해도 현재 회피 방향은 유지
        //시작 알림에서 일반 행동 제한과 Animator 진입 요청을 동기로 처리
        OnStarted?.Invoke ( );
        return true;
    }

    /// <summary>
    /// 같은 경과 시간으로 회피 진행, 무적 만료와 쿨타임 갱신
    /// </summary>
    /// <param name="deltaTime">게임 시간 기준 프레임 경과 시간</param>
    public void UpdateTimers ( float deltaTime )
    {
        if ( deltaTime <= 0f )
            return;

        _remainingCooldown = Mathf.Max (
            0f , _remainingCooldown - deltaTime );

        if ( _isDodging == false )
            return;

        bool wasInvulnerable = IsInvulnerable;
        _elapsedTime = Mathf.Min ( _data.Duration , _elapsedTime + deltaTime );

        //상태이상 피해와 입력보다 먼저 호출하여 이번 프레임의 무적 만료 반영
        if ( wasInvulnerable == true && IsInvulnerable == false )
            _damageReceiver.SetInvulnerable ( false );
    }

    /// <summary>
    /// 진행 중인 회피의 미반영 이동량 적용 및 종료
    /// </summary>
    /// <param name="deltaTime">중력 갱신에 사용할 프레임 경과 시간</param>
    public void UpdateMovement ( float deltaTime )
    {
        if ( deltaTime <= 0f )
            return;

        //Hero.UpdateMove의 회피 분기에서만 호출
        //시간은 UpdateTimers에서 한 번만 갱신하고 이동은 그 차이만 반영
        float moveTime = _elapsedTime - _movedTime;
        float speed = _data.Distance / _data.Duration;
        _movedTime = _elapsedTime;

        //벽 충돌은 이동만 제한하며 회피 시간과 무적을 즉시 취소하지 않음
        _mover.MoveForced ( _direction * speed * moveTime , deltaTime );

        //이동 콜백에서 사망/넉백으로 취소됐다면 정상 완료를 중복 발행하지 않음
        if ( _isDodging == false )
            return;

        if ( _elapsedTime >= _data.Duration )
            End ( AttackEndReason.Completed );
    }

    /// <summary>
    /// 기존 행동 취소 경로에 따른 회피 취소
    /// </summary>
    /// <param name="reason">종료 사유</param>
    public void Cancel ( AttackEndReason reason )
    {
        End ( reason );
    }

    /// <summary>
    /// 회피 상태와 무적 정리 후 종료 알림
    /// </summary>
    /// <param name="reason">종료 사유</param>
    void End ( AttackEndReason reason )
    {
        if ( _isDodging == false )
            return;

        _isDodging = false;
        _elapsedTime = 0f;
        _movedTime = 0f;
        _damageReceiver.SetInvulnerable ( false );

        if ( reason == AttackEndReason.Completed )
            _mover.BeginDodgeTurn ( );

        //정리된 상태를 Hero가 조회해 복귀 대기와 행동 제한을 결정
        //넉백/사망의 최종 모션은 이 알림이 끝난 뒤 기존 처리에서 요청
        OnEnded?.Invoke ( reason );
    }

    /// <summary>
    /// 새 스테이지의 회피 쿨타임 초기화
    /// </summary>
    public void ResetCooldown ( )
    {
        _remainingCooldown = 0f;
    }
}
