using System;
using UnityEngine;

/// <summary>
/// 궁극기 조준 - 범위 지정 상태와 위치 확정 관리
/// </summary>
public class HeroUltimateAim
{
    GameObject _owner;                  //궁극기 사용자
    Func<bool> _tryUse;                  //기존 궁극기 사용 요청
    IUltimateAimTarget _target;          //현재 무기의 위치 지정 기능
    bool _isAiming;                     //범위 지정 중 여부
    bool _isValid;                      //현재 후보의 시전 가능 여부
    bool _hasGroundPoint;               //현재 후보의 지면 존재 여부
    Vector3 _point;                     //현재 후보 위치

    /// <summary>
    /// 현재 무기의 위치 지정 기능 보유 여부
    /// </summary>
    public bool HasTarget => _target != null;

    /// <summary>
    /// 범위 지정 중 여부
    /// </summary>
    public bool IsAiming => _isAiming;

    /// <summary>
    /// 현재 후보의 시전 가능 여부
    /// </summary>
    public bool IsValid => _isValid;

    /// <summary>
    /// 범위를 표시할 지면 위치 존재 여부
    /// </summary>
    public bool HasGroundPoint => _hasGroundPoint;

    /// <summary>
    /// 현재 후보 위치
    /// </summary>
    public Vector3 Point => _point;

    /// <summary>
    /// 현재 궁극기의 피해 반경
    /// </summary>
    public float Radius => HasTarget ? _target.AimRadius : 0f;

    /// <summary>
    /// 조준 표시 변경 이벤트
    /// </summary>
    public event Action OnChanged;

    /// <summary>
    /// 사용자와 기존 궁극기 사용 함수 연결
    /// </summary>
    /// <param name="owner">궁극기 사용자</param>
    /// <param name="tryUse">실제 궁극기 사용 함수</param>
    public HeroUltimateAim ( GameObject owner , Func<bool> tryUse )
    {
        _owner = owner;
        _tryUse = tryUse;
    }

    /// <summary>
    /// 이전 범위 지정 종료 후 현재 무기 연결
    /// </summary>
    /// <param name="target">위치 지정 기능, 검은 null</param>
    public void Init ( IUltimateAimTarget target )
    {
        Cancel ( );
        _target = target;
    }

    /// <summary>
    /// 범위 지정 시작
    /// </summary>
    /// <returns>범위 지정 시작 여부</returns>
    public bool TryBegin ( )
    {
        _isAiming = true;
        ReadPoint ( );
        OnChanged?.Invoke ( );
        return true;
    }

    /// <summary>
    /// 조준 중 후보 위치와 표시 상태 갱신
    /// </summary>
    public void UpdateAim ( )
    {
        if ( _isAiming == false )
            return;

        Vector3 previousPoint = _point;
        bool previousValid = _isValid;
        bool previousGroundPoint = _hasGroundPoint;
        ReadPoint ( );

        if ( previousPoint != _point
            || previousValid != _isValid
            || previousGroundPoint != _hasGroundPoint )
            OnChanged?.Invoke ( );
    }

    /// <summary>
    /// 최신 후보를 확정하고 기존 궁극기 사용 요청
    /// </summary>
    /// <returns>궁극기 시작 승인 여부</returns>
    public bool TryConfirm ( )
    {
        //확정 입력 시 다시 조회하므로 이전 프레임의 후보를 사용하지 않음
        ReadPoint ( );

        if ( _isValid == false )
        {
            OnChanged?.Invoke ( );
            return false;
        }

        IUltimateAimTarget target = _target;
        target.SetAimPoint ( _point );

        bool started = _tryUse ( );

        target.ClearAimPoint ( );
        return started;
    }

    /// <summary>
    /// 현재 후보 위치 조회
    /// </summary>
    void ReadPoint ( )
    {
        _isValid = _target.TryGetAimPoint (
            _owner , out _point , out _hasGroundPoint );
    }

    /// <summary>
    /// 미확정 범위 지정과 임시 위치 해제
    /// </summary>
    public void Cancel ( )
    {
        if ( _isAiming == false )
            return;

        _isAiming = false;
        _isValid = false;
        _hasGroundPoint = false;
        _target.ClearAimPoint ( );
        OnChanged?.Invoke ( );
    }
}
