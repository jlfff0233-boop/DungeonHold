using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상태이상 모듈 - 화상, 감속, 기절의 적용, 갱신과 정리
/// </summary>
public class StatusEffectController : MonoBehaviour
{
    /// <summary>
    /// 감속 기록 - 같은 설정의 남은 시간과 적용 강도
    /// </summary>
    class SlowState
    {
        public SlowEffectData Data;       //동일 감속 설정 식별
        public float RemainingTime;     //남은 지속시간
        public float SlowRate;      //저항을 반영한 감소 비율
    }

    /// <summary>
    /// 화상 기록 - 최신 요청, 남은 지속시간과 다음 피해 시간
    /// </summary>
    class BurnState
    {
        public BurnEffectInfo Info;       //최신 화상 요청
        public float RemainingTime;      //남은 지속시간
        public float RemainingTickTime;  //다음 피해까지 남은 시간
    }

    /// <summary>
    /// 활성 감속 기록
    /// </summary>
    List<SlowState> _slowStates = new List<SlowState> ( );
    BurnState _burnState;     //대상당 하나만 유지하는 화상 기록

    Behaviour _updateOwner;     //프레임 갱신 주체
    Health _health;     //대상 체력
    DamageReceiver _damageReceiver;     //공통 수신 조건
    IMoveSpeedModifier _mover;      //이동속도 적용 대상

    bool _isInitialized;        //효과 모듈 초기화 여부
    bool _isStunImmune;     //기절 면역 여부
    float _slowEffectMultiplier;        //감속 강도 배율, 0이면 면역
    float _maxSlowRate;     //최대 감소 비율
    float _remainingStunTime;       //남은 기절 시간
    float _moveSpeedMultiplier = 1f;        //현재 이동속도 배율

    /// <summary>
    /// 화상 유지 여부
    /// </summary>
    public bool IsBurning => _burnState != null;

    /// <summary>
    /// 기절 여부
    /// </summary>
    public bool IsStunned => _remainingStunTime > 0f;

    /// <summary>
    /// 남은 기절 시간
    /// </summary>
    public float RemainingStunTime => _remainingStunTime;

    /// <summary>
    /// 유지 중인 감속 기록 수
    /// </summary>
    public int SlowCount => _slowStates.Count;

    /// <summary>
    /// 최종 이동속도 배율
    /// </summary>
    public float MoveSpeedMultiplier => _moveSpeedMultiplier;

    /// <summary>
    /// 효과 변경 이벤트(변경된 효과 종류)
    /// </summary>
    public event Action<StatusEffectType> OnEffectChanged;


    #region ----- 초기화 -----

    /// <summary>
    /// 기존 효과 정리 후 대상 참조와 저항 초기화
    /// </summary>
    /// <param name="updateOwner">프레임 갱신 주체</param>
    /// <param name="health">대상 체력</param>
    /// <param name="receiver">대상 피해 수신 모듈</param>
    /// <param name="mover">대상 이동속도 적용 모듈</param>
    /// <param name="resistance">대상 상태 저항 설정</param>
    /// <returns>초기화 성공 여부</returns>
    public bool Init (
        Behaviour updateOwner ,
        Health health ,
        DamageReceiver receiver ,
        IMoveSpeedModifier mover ,
        StatusResistanceData resistance )
    {
        //상위 주체가 먼저 행동을 제한한 상태에서 호출
        //정리 알림 중 이전 참조로 새 효과를 받지 않음
        _isInitialized = false;

        float slowMultiplier = resistance.SlowEffectMultiplier;
        float maxSlowRate = resistance.MaxSlowRate;

        if ( IsRate ( slowMultiplier ) == false
            || IsRate ( maxSlowRate ) == false )
        {
            Debug.LogError (
                "감속 강도 배율과 최대 감소 비율은 0~1이어야 합니다." , this );
            return false;
        }

        //이전 대상에 배율 복구와 정리 알림을 먼저 전달
        ClearEffects ( );

        if ( _damageReceiver != null )
            _damageReceiver.OnDied -= HandleDied;

        //이전 효과의 정리 알림이 끝난 뒤 새 대상 참조로 교체
        _updateOwner = updateOwner;
        _health = health;
        _damageReceiver = receiver;
        _mover = mover;

        _isStunImmune = resistance.IsStunImmune;
        _slowEffectMultiplier = slowMultiplier;
        _maxSlowRate = maxSlowRate;

        //새 참조에도 초기 배율을 명시적으로 적용
        _moveSpeedMultiplier = 1f;
        _mover.SetMoveSpeedMultiplier ( 1f );

        //비활성화 중 사망 정리를 위해 초기화 수명으로 구독
        _damageReceiver.OnDied += HandleDied;
        _isInitialized = true;

        return true;
    }

    #endregion

    #region ----- 공용 -----

    /// <summary>
    /// 시간 감소, 주기 피해와 만료 상태 반영
    /// </summary>
    /// <param name="deltaTime">게임 시간 기준 경과 시간</param>
    public void UpdateEffects ( float deltaTime )
    {
        if ( _isInitialized == false
            || isActiveAndEnabled == false
            || _updateOwner.isActiveAndEnabled == false
            || _health.IsAlive == false )
            return;

        if ( IsPositive ( deltaTime ) == false )
            return;

        //이번 갱신 전에 존재한 화상만 경과 시간 처리
        BurnState burnState = _burnState;

        bool slowEnded = UpdateSlow ( deltaTime );
        bool stunEnded = UpdateStun ( deltaTime );

        if ( slowEnded == true )
            RefreshMoveSpeed ( );

        //행동 제한과 이동 배율을 먼저 반영한 뒤 화상 피해 처리
        if ( slowEnded == true )
            OnEffectChanged?.Invoke ( StatusEffectType.Slow );

        if ( stunEnded == true )
            OnEffectChanged?.Invoke ( StatusEffectType.Stun );

        UpdateBurn ( burnState , deltaTime );
    }

    /// <summary>
    /// 사망한 대상의 효과 정리
    /// </summary>
    /// <param name="damageInfo">사망 피해 정보</param>
    void HandleDied ( DamageInfo damageInfo )
    {
        //갱신 주체가 비활성화되어도
        //사망은 보존 정책보다 우선하여 기록을 제거
        ClearEffects ( );
    }

    /// <summary>
    /// 기존 효과 정리와 표시용 변경 알림
    /// </summary>
    public void ClearEffects ( )
    {
        bool hadBurn = IsBurning;
        bool hadSlow = _slowStates.Count > 0;
        bool hadStun = IsStunned;

        if ( hadBurn == false
            && hadSlow == false
            && hadStun == false )
            return;

        //모든 기존 기록을 먼저 정리한 뒤 알림
        _burnState = null;
        _slowStates.Clear ( );
        _remainingStunTime = 0f;

        //화상만 정리한 경우 이동 모듈을 다시 갱신할 필요 없음
        if ( hadSlow == true )
            RefreshMoveSpeed ( );

        //알림 이후 새로 적용된 효과를 다시 지우지 않음
        if ( hadSlow == true )
            OnEffectChanged?.Invoke ( StatusEffectType.Slow );

        if ( hadStun == true )
            OnEffectChanged?.Invoke ( StatusEffectType.Stun );

        if ( hadBurn == true )
            OnEffectChanged?.Invoke ( StatusEffectType.Burn );
    }

    /// <summary>
    /// 공통 효과 수신 조건 확인
    /// </summary>
    /// <param name="effectInfo">보존된 공통 효과 요청</param>
    /// <returns>공통 조건 충족 여부</returns>
    bool CheckCanApply ( StatusEffectInfo effectInfo )
    {
        if ( _isInitialized == false
            || isActiveAndEnabled == false
            || _updateOwner.isActiveAndEnabled == false )
            return false;

        if ( effectInfo.Data == null
            || IsPositive ( effectInfo.Duration ) == false )
            return false;

        return _damageReceiver.CanReceiveEffect ( effectInfo.Context );
    }

    /// <summary>
    /// 지정 종류의 효과 유지 여부 조회
    /// </summary>
    /// <param name="type">효과 종류</param>
    /// <returns>유지 중인 효과 존재 여부</returns>
    public bool HasEffect ( StatusEffectType type )
    {
        switch ( type )
        {
            case StatusEffectType.Burn:
                return IsBurning;

            case StatusEffectType.Slow:
                return _slowStates.Count > 0;

            case StatusEffectType.Stun:
                return IsStunned;

            default:
                return false;
        }
    }

    #endregion

    #region ----- 감속 -----

    /// <summary>
    /// 감속 시간 감소와 만료 기록 제거
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    /// <returns>만료 기록 제거 여부</returns>
    bool UpdateSlow ( float deltaTime )
    {
        bool removed = false;

        //뒤에서 제거하여 아직 처리하지 않은 기록의 인덱스를 유지
        //여기서는 기록만 정리하고 배율 적용/알림은 호출한 UpdateEffects에서 수행
        for ( int i = _slowStates.Count - 1 ; i >= 0 ; i-- )
        {
            SlowState state = _slowStates [ i ];

            state.RemainingTime = Mathf.Max ( 0f , state.RemainingTime - deltaTime );

            if ( state.RemainingTime > 0f )
                continue;

            _slowStates.RemoveAt ( i );
            removed = true;
        }

        return removed;
    }

    /// <summary>
    /// 감속 요청 적용 또는 변경
    /// </summary>
    /// <param name="effectInfo">보존된 감속 요청</param>
    /// <returns>실제 적용 또는 변경 여부</returns>
    public bool TryApply ( SlowEffectInfo effectInfo )
    {
        StatusEffectInfo commonInfo = effectInfo.EffectInfo;

        if ( commonInfo.Type != StatusEffectType.Slow )
            return false;

        if ( CheckCanApply ( commonInfo ) == false )
            return false;

        return ApplySlow ( effectInfo );
    }

    /// <summary>
    /// 감속 적용 또는 시간과 강도 변경
    /// </summary>
    /// <param name="effectInfo">감속 요청</param>
    /// <returns>실제 적용 또는 변경 여부</returns>
    bool ApplySlow ( SlowEffectInfo effectInfo )
    {
        if ( IsRate ( effectInfo.SlowRate ) == false )
            return false;

        float slowRate = Mathf.Min (
            effectInfo.SlowRate * _slowEffectMultiplier , _maxSlowRate );

        if ( slowRate <= 0f )
            return false;

        StatusEffectInfo commonInfo = effectInfo.EffectInfo;

        //생성자에서 감속 설정만 받으므로 해당 타입으로 조회
        SlowEffectData data = ( SlowEffectData ) commonInfo.Data;
        SlowState state = FindSlowState ( data );

        if ( state == null )
        {
            state = new SlowState
            {
                Data = data ,
                RemainingTime = commonInfo.Duration ,
                SlowRate = slowRate
            };

            _slowStates.Add ( state );
        }
        else
        {
            float remainingTime = Mathf.Max (
                state.RemainingTime , commonInfo.Duration );

            if ( remainingTime == state.RemainingTime
                && slowRate == state.SlowRate )
                return false;

            state.RemainingTime = remainingTime;
            state.SlowRate = slowRate;
        }

        RefreshMoveSpeed ( );
        OnEffectChanged?.Invoke ( StatusEffectType.Slow );

        return true;
    }

    /// <summary>
    /// 남은 가장 강한 감속으로 이동 배율 반영
    /// </summary>
    void RefreshMoveSpeed ( )
    {
        float slowRate = 0f;

        foreach ( SlowState state in _slowStates )
            slowRate = Mathf.Max ( slowRate , state.SlowRate );

        //강한 감속이 끝나면 남은 약한 감속을 사용하고,
        //모두 끝났을 때만 배율 1로 복구
        _moveSpeedMultiplier = 1f - slowRate;
        _mover.SetMoveSpeedMultiplier ( _moveSpeedMultiplier );
    }

    /// <summary>
    /// 지정 설정의 감속 시간과 적용 강도 조회
    /// </summary>
    /// <param name="data">조회할 감속 설정</param>
    /// <param name="remainingTime">남은 시간</param>
    /// <param name="slowRate">저항을 반영한 감소 비율</param>
    /// <returns>해당 감속 기록 존재 여부</returns>
    public bool TryGetSlowState (
        SlowEffectData data ,
        out float remainingTime ,
        out float slowRate )
    {
        remainingTime = 0f;
        slowRate = 0f;

        //종류 전체의 대표 시간이 아닌
        //지정 SO의 기록을 조회하여 복수 감속을 구분
        SlowState state = FindSlowState ( data );

        if ( state == null )
            return false;

        remainingTime = state.RemainingTime;
        slowRate = state.SlowRate;

        return true;
    }

    /// <summary>
    /// 같은 설정의 감속 기록 조회
    /// </summary>
    /// <param name="data">감속 설정</param>
    /// <returns>해당 기록 또는 null</returns>
    SlowState FindSlowState ( SlowEffectData data )
    {
        foreach ( SlowState state in _slowStates )
        {
            if ( state.Data == data )
                return state;
        }

        return null;
    }

    #endregion

    #region ----- 스턴 -----

    /// <summary>
    /// 기절 시간 감소
    /// </summary>
    /// <param name="deltaTime">경과 시간</param>
    /// <returns>이번 갱신의 기절 종료 여부</returns>
    bool UpdateStun ( float deltaTime )
    {
        if ( IsStunned == false )
            return false;

        _remainingStunTime = Mathf.Max ( 0f , _remainingStunTime - deltaTime );

        return IsStunned == false;
    }

    /// <summary>
    /// 공통 요청 중 기절 적용 또는 변경
    /// </summary>
    /// <param name="effectInfo">보존된 공통 효과 요청</param>
    /// <returns>실제 적용 또는 변경 여부</returns>
    public bool TryApply ( StatusEffectInfo effectInfo )
    {
        if ( effectInfo.Type != StatusEffectType.Stun )
            return false;

        if ( CheckCanApply ( effectInfo ) == false )
            return false;

        return ApplyStun ( effectInfo );
    }

    /// <summary>
    /// 기절 적용 또는 남은 시간 연장
    /// </summary>
    /// <param name="effectInfo">기절 요청</param>
    /// <returns>실제 적용 또는 변경 여부</returns>
    bool ApplyStun ( StatusEffectInfo effectInfo )
    {
        if ( _isStunImmune == true )
            return false;

        //기절은 하나의 시간을 유지하며
        //재적용으로 남은 시간을 연장할 때만 알림
        float remainingTime = Mathf.Max ( _remainingStunTime , effectInfo.Duration );

        if ( remainingTime == _remainingStunTime )
            return false;

        _remainingStunTime = remainingTime;

        //상태 반영 후 주체가 행동 제한과 진행 행동 취소
        OnEffectChanged?.Invoke ( StatusEffectType.Stun );

        return true;
    }

    #endregion

    #region ----- 화상 -----

    /// <summary>
    /// 화상의 유효 시간 안에서 주기 피해와 만료 처리
    /// </summary>
    /// <param name="state">이번 갱신 전에 보관한 화상 기록</param>
    /// <param name="deltaTime">게임 시간 기준 경과 시간</param>
    void UpdateBurn ( BurnState state , float deltaTime )
    {
        //앞선 효과 알림에서 정리하거나 교체한 기록은 처리하지 않음
        if ( state == null
            || _burnState != state
            || _isInitialized == false
            || isActiveAndEnabled == false
            || _updateOwner.isActiveAndEnabled == false
            || _damageReceiver.isActiveAndEnabled == false
            || _health.IsAlive == false )
            return;

        //만료 이후의 프레임 시간은 피해 계산에 포함하지 않음
        float remainingDeltaTime = Mathf.Min ( deltaTime , state.RemainingTime );

        while ( remainingDeltaTime >= state.RemainingTickTime )
        {
            float tickTime = state.RemainingTickTime;

            //피해 시점까지 시간을 먼저 반영
            remainingDeltaTime -= tickTime;
            state.RemainingTime = Mathf.Max ( 0f , state.RemainingTime - tickTime );

            BurnEffectInfo effectInfo = state.Info;
            EffectContext context = effectInfo.EffectInfo.Context;

            //피격 콜백에서 재적용하더라도 다음 피해 시간이 준비된 상태
            state.RemainingTickTime = effectInfo.TickInterval;

            DamageInfo damageInfo = new DamageInfo (
                context.Attacker ,
                context.Source ,
                context.ActorType ,
                context.Faction ,
                effectInfo.RawDamage ,
                AttackType.DamageOverTime ,
                context.AttackId );

            _damageReceiver.TakeDamage ( damageInfo );

            //피해 콜백의 사망, 해제, 재적용 또는 비활성화 이후
            //이전 기록으로 남은 피해를 계속 처리하지 않음
            if ( _burnState != state
                || _isInitialized == false
                || isActiveAndEnabled == false
                || _updateOwner.isActiveAndEnabled == false
                || _damageReceiver.isActiveAndEnabled == false
                || _health.IsAlive == false )
                return;
        }

        //다음 피해에 도달하지 않은 나머지 시간 반영
        state.RemainingTime = Mathf.Max ( 0f , state.RemainingTime - remainingDeltaTime );

        state.RemainingTickTime -= remainingDeltaTime;

        if ( state.RemainingTime > 0f )
            return;

        //마지막 유효 피해 처리 후 기록을 제거하고 종료 알림
        _burnState = null;
        OnEffectChanged?.Invoke ( StatusEffectType.Burn );
    }

    /// <summary>
    /// 화상 요청 적용 또는 변경
    /// </summary>
    /// <param name="effectInfo">보존된 화상 요청</param>
    /// <returns>실제 적용 또는 변경 여부</returns>
    public bool TryApply ( BurnEffectInfo effectInfo )
    {
        StatusEffectInfo commonInfo = effectInfo.EffectInfo;

        if ( commonInfo.Type != StatusEffectType.Burn )
            return false;

        if ( CheckCanApply ( commonInfo ) == false )
            return false;

        if ( IsPositive ( effectInfo.RawDamage ) == false
            || IsPositive ( effectInfo.TickInterval ) == false )
            return false;

        return ApplyBurn ( effectInfo );
    }

    /// <summary>
    /// 화상 생성, 남은 시간과 피해 정보 갱신
    /// </summary>
    /// <param name="effectInfo">화상 요청</param>
    /// <returns>실제 적용 또는 변경 여부</returns>
    bool ApplyBurn ( BurnEffectInfo effectInfo )
    {
        BurnState previousState = _burnState;

        float remainingTime = effectInfo.EffectInfo.Duration;
        float remainingTickTime = effectInfo.TickInterval;

        if ( previousState != null )
        {
            remainingTime = Mathf.Max ( previousState.RemainingTime , remainingTime );

            //재적용해도 다음 피해까지 남은 시간은 유지
            remainingTickTime = previousState.RemainingTickTime;

            //시간과 보존된 요청이 모두 같으면 변경 없음
            if ( remainingTime == previousState.RemainingTime
                && previousState.Info.Equals ( effectInfo ) == true )
                return false;
        }

        //재적용은 기록을 교체하여
        //피해 콜백 이후 이전 기록의 갱신을 계속하지 않도록 구분
        _burnState = new BurnState
        {
            Info = effectInfo ,
            RemainingTime = remainingTime ,
            RemainingTickTime = remainingTickTime
        };

        OnEffectChanged?.Invoke ( StatusEffectType.Burn );

        return true;
    }

    /// <summary>
    /// 현재 화상의 남은 시간과 피해 설정 조회
    /// </summary>
    /// <param name="remainingTime">남은 지속시간</param>
    /// <param name="remainingTickTime">다음 피해까지 남은 시간</param>
    /// <param name="rawDamage">주기당 원본 피해량</param>
    /// <param name="tickInterval">피해 발생 간격</param>
    /// <returns>화상 기록 존재 여부</returns>
    public bool TryGetBurnState (
        out float remainingTime ,
        out float remainingTickTime ,
        out float rawDamage ,
        out float tickInterval )
    {
        remainingTime = 0f;
        remainingTickTime = 0f;
        rawDamage = 0f;
        tickInterval = 0f;

        if ( _burnState == null )
            return false;

        remainingTime = _burnState.RemainingTime;
        remainingTickTime = _burnState.RemainingTickTime;
        rawDamage = _burnState.Info.RawDamage;
        tickInterval = _burnState.Info.TickInterval;

        return true;
    }

    #endregion

    #region ----- 검사 -----

    /// <summary>
    /// 유한한 양수 검사
    /// </summary>
    /// <param name="value">검사할 값</param>
    /// <returns>유한한 양수 여부</returns>
    bool IsPositive ( float value )
    {
        return value > 0f
            && float.IsInfinity ( value ) == false;
    }

    /// <summary>
    /// 유한한 0~1 비율 검사
    /// </summary>
    /// <param name="value">검사할 값</param>
    /// <returns>허용 범위 여부</returns>
    bool IsRate ( float value )
    {
        return value >= 0f && value <= 1f;
    }

    #endregion

    private void OnDestroy ( )
    {
        if ( _damageReceiver != null )
            _damageReceiver.OnDied -= HandleDied;
    }
}
