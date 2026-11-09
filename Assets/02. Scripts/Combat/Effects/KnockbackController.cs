using System;
using UnityEngine;

/// <summary>
/// 넉백 모듈 - 강제 이동 요청, 진행, 교체와 종료 관리
/// </summary>
public class KnockbackController : MonoBehaviour
{
    /// <summary>
    /// 넉백 기록 - 확정 방향, 속력과 남은 시간
    /// </summary>
    class KnockbackState
    {
        public Vector3 Direction;     //정규화한 수평 방향
        public float Speed;           //초당 강제 이동 거리
        public float RemainingTime;   //남은 진행 시간
        public bool AllowAfterDeath;  //이번 치명타에 따른 사망 후 이동 허용
    }

    Behaviour _updateOwner;          //프레임 갱신 주체
    Health _health;                  //대상 체력
    DamageReceiver _damageReceiver;  //공통 수신 조건과 사망 알림
    IKnockbackMover _mover;          //실제 강제 이동 모듈
    KnockbackState _state;           //현재 넉백 기록
    bool _isInitialized;             //초기화 성공 여부

    /// <summary>
    /// 넉백 진행 여부
    /// </summary>
    public bool IsKnockback => _state != null;

    /// <summary>
    /// 남은 넉백 시간
    /// </summary>
    public float RemainingTime => _state != null ? _state.RemainingTime : 0f;

    /// <summary>
    /// 넉백 변경 이벤트(진행 여부)
    /// </summary>
    public event Action<bool> OnKnockbackChanged;

    /// <summary>
    /// 기존 넉백 정리 후 대상 참조 초기화
    /// </summary>
    /// <param name="updateOwner">프레임 갱신 주체</param>
    /// <param name="health">대상 체력</param>
    /// <param name="receiver">대상 피해 수신 모듈</param>
    /// <param name="mover">실제 강제 이동 모듈</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        Behaviour updateOwner, Health health,
        DamageReceiver receiver, IKnockbackMover mover )
    {
        //상위 주체가 행동을 제한한 뒤 호출하며 정리 알림 중 새 요청은 거절
        _isInitialized = false;
        CancelKnockback( );

        if ( _damageReceiver != null )
            _damageReceiver.OnDied -= HandleDied;

        if ( _health != null )
            _health.OnRevived -= HandleRevived;

        if ( mover.CanKnockback == false )
        {
            Debug.LogError( "넉백 이동 모듈이 이동 가능한 상태가 아닙니다.", this );
            return false;
        }

        _updateOwner = updateOwner;
        _health = health;
        _damageReceiver = receiver;
        _mover = mover;

        //활성 수명이 아닌 초기화 수명으로 구독하여 비활성화 중 사망도 정리
        _damageReceiver.OnDied += HandleDied;
        _health.OnRevived += HandleRevived;
        _isInitialized = true;
        return true;
    }

    /// <summary>
    /// 넉백 시작 또는 진행 중인 요청 교체
    /// </summary>
    /// <param name="info">보존된 넉백 요청</param>
    /// <returns>요청 적용 여부</returns>
    public bool TryApply ( KnockbackInfo info )
    {
        if ( _isInitialized == false
            || isActiveAndEnabled == false
            || _updateOwner.isActiveAndEnabled == false
            || _mover.CanKnockback == false )
            return false;

        //피해와 같은 자기/진영/무적/생존 조건을 재사용
        if ( _damageReceiver.CanReceiveEffect( info.Context ) == false )
            return false;

        return TryStartKnockback ( info, false );
    }

    /// <summary>
    /// 실제 피해 결과에 따른 일반 넉백 또는 치명타 넉백 요청
    /// </summary>
    /// <param name="info">같은 타격의 넉백 요청</param>
    /// <param name="appliedDamage">이번 타격의 실제 피해량</param>
    /// <param name="killedTarget">이번 타격으로 사망했는지 여부</param>
    /// <returns>넉백 적용 여부</returns>
    public bool TryApplyFromHit (
        KnockbackInfo info, float appliedDamage, bool killedTarget )
    {
        //피해 직후 같은 호출 흐름의 결과만 전달하며 지연 요청용 기록은 만들지 않음
        //대상 참조를 읽기 전에 초기화와 실행 가능 상태부터 검사
        if ( _isInitialized == false
            || isActiveAndEnabled == false
            || _updateOwner.isActiveAndEnabled == false
            || _damageReceiver.isActiveAndEnabled == false
            || _mover.CanKnockback == false )
            return false;

        if ( IsPositive ( appliedDamage ) == false )
            return false;

        //치명타가 아니면 기존 생존/진영/무적 검사와 재적용 규칙 유지
        if ( killedTarget == false )
            return TryApply ( info );

        //피해 이벤트 중 부활한 대상에 이전 생명의 사망 넉백을 시작하지 않음
        if ( _health.IsAlive == true )
            return false;

        return TryStartKnockback ( info, true );
    }

    /// <summary>
    /// 남은 시간 안에서 강제 이동과 종료 처리
    /// </summary>
    /// <param name="deltaTime">게임 시간 기준 프레임 시간</param>
    public void UpdateKnockback ( float deltaTime )
    {
        KnockbackState state = _state;

        if ( state == null )
            return;

        if ( _isInitialized == false
            || isActiveAndEnabled == false
            || _updateOwner.isActiveAndEnabled == false
            || ( _health.IsAlive == false && state.AllowAfterDeath == false )
            || ( _health.IsAlive == true && state.AllowAfterDeath == true )
            || _damageReceiver.isActiveAndEnabled == false
            || _mover.CanKnockback == false )
        {
            CancelKnockback( );
            return;
        }

        //일시정지는 진행 시간을 보존하고 이동도 실행하지 않음
        if ( IsPositive( deltaTime ) == false )
            return;

        //종료 프레임의 수평 이동은 남은 시간까지만 계산
        //중력은 Mover에서 전체 프레임 시간으로 한 번 처리
        float moveTime = Mathf.Min( deltaTime, state.RemainingTime );
        Vector3 displacement = state.Direction * state.Speed * moveTime;
        state.RemainingTime = Mathf.Max( 0f, state.RemainingTime - moveTime );

        bool canContinue = _mover.MoveKnockback( displacement, deltaTime );

        //CharacterController 이동 콜백에서 교체/취소된 이전 기록은 덮어쓰지 않음
        if ( _state != state )
            return;

        //벽에 막힌 이동량은 이후 프레임에 보상하지 않고 종료
        if ( canContinue == false || state.RemainingTime <= 0f )
            CancelKnockback( );
    }

    /// <summary>
    /// 진행 중 넉백 정리와 종료 알림
    /// </summary>
    public void CancelKnockback ()
    {
        if ( _state == null )
            return;

        //먼저 기록을 비운 뒤 상위 주체가 기절/생존/연출 제한을 재계산
        _state = null;
        OnKnockbackChanged?.Invoke( false );
    }

    /// <summary>
    /// 사망한 대상의 넉백 정리
    /// </summary>
    /// <param name="damageInfo">사망 피해 정보</param>
    void HandleDied ( DamageInfo damageInfo )
    {
        //이전 넉백은 정리하며 이번 치명타 넉백은 피해 반환 이후 별도로 요청
        CancelKnockback( );
    }

    /// <summary>
    /// 부활한 대상의 이전 생명에 속한 넉백 정리
    /// </summary>
    void HandleRevived ()
    {
        //사망 중 갱신이 중단된 주체도 부활 이벤트에서 즉시 이전 이동 정리
        CancelKnockback ( );
    }

    /// <summary>
    /// 공통 넉백 수치 검사와 이동 기록 생성
    /// </summary>
    /// <param name="info">보존된 넉백 요청</param>
    /// <param name="allowAfterDeath">이번 치명타의 사망 후 이동 허용 여부</param>
    /// <returns>넉백 시작 또는 교체 여부</returns>
    bool TryStartKnockback ( KnockbackInfo info, bool allowAfterDeath )
    {
        if ( IsPositive( info.Distance ) == false
            || IsPositive( info.Duration ) == false )
            return false;

        Vector3 direction = info.Direction;
        direction.y = 0f;

        //거의 같은 위치의 요청은 정규화할 방향이 없으므로 거절
        float directionLength = direction.sqrMagnitude;
        if ( directionLength > 0.0001f == false
            || float.IsInfinity( directionLength ) == true )
            return false;

        float speed = info.Distance / info.Duration;

        if ( IsPositive( speed ) == false )
            return false;

        direction.Normalize( );
        bool wasKnockback = IsKnockback;

        //재적용은 남은 이동에 합산하지 않고 새 방향/거리/시간으로 교체
        _state = new KnockbackState
        {
            Direction = direction,
            Speed = speed,
            RemainingTime = info.Duration,
            AllowAfterDeath = allowAfterDeath
        };

        //상태 확정 후 상위 주체가 행동을 제한하고 진행 공격을 취소
        //진행 중 교체에서는 행동 허용 상태를 풀었다 다시 걸지 않음
        if ( wasKnockback == false )
            OnKnockbackChanged?.Invoke( true );

        return true;
    }

    /// <summary>
    /// 유한한 양수 검사
    /// </summary>
    /// <param name="value">검사할 값</param>
    /// <returns>유한한 양수 여부</returns>
    bool IsPositive ( float value )
    {
        return value > 0f && float.IsInfinity( value ) == false;
    }

    private void OnDisable ()
    {
        //비활성화한 강제 이동은 재활성화 후 이어서 실행하지 않음
        CancelKnockback( );
    }

    private void OnDestroy ()
    {
        if ( _damageReceiver != null )
            _damageReceiver.OnDied -= HandleDied;

        if ( _health != null )
            _health.OnRevived -= HandleRevived;
    }
}
