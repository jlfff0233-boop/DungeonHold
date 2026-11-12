using System;
using UnityEngine;

/// <summary>
/// 동료 배치 상호작용 - 다중 포인트의 영역 선택과 배치 요청
/// </summary>
public class CompanionPlacementController : MonoBehaviour
{
    [Header ( "----- 배치 포인트 -----" )]
    [SerializeField] CompanionPlacementPoint [ ] _points = Array.Empty<CompanionPlacementPoint>( );    //맵의 배치 포인트

    CompanionHireController _hireController;    //고용 관리자
    Transform _hero;    //플레이어 루트
    bool _isInitialized;    //영역 검사에 필요한 연결 완료 여부
    CompanionPlacementPoint _currentPoint;    //현재 상호작용 포인트

    /// <summary>
    /// 현재 선택한 포인트, 영역 밖이거나 요청 불가이면 null
    /// </summary>
    public CompanionPlacementPoint CurrentPoint => _currentPoint;

    /// <summary>
    /// 현재 포인트에서 배치 요청 가능 여부
    /// </summary>
    public bool CanInteract => isActiveAndEnabled &&
        _isInitialized && _hireController.CanPlace && _currentPoint != null;

    /// <summary>
    /// 상호작용 대상 변경 이벤트(현재 포인트, 없으면 null)
    /// </summary>
    public event Action<CompanionPlacementPoint> OnCurrentPointChanged;

    /// <summary>
    /// 배치에 필요한 고용 관리자와 플레이어 연결
    /// </summary>
    /// <param name="hireController">초기화된 고용 관리자</param>
    /// <param name="hero">플레이어 루트</param>
    public void Init ( CompanionHireController hireController , Transform hero )
    {
        _hireController = hireController;
        _hero = hero;

        //영역 검사에 필요한 참조를 모두 받은 뒤 갱신 허용
        _isInitialized = true;
    }

    /// <summary>
    /// 현재 맵의 배치 포인트 연결
    /// </summary>
    /// <param name="points">맵의 배치 포인트</param>
    internal void SetPoints ( CompanionPlacementPoint [ ] points )
    {
        _points = points;
        RefreshCurrentPoint ( );
    }

    private void Update ( )
    {
        if ( _isInitialized == false )
            return;

        RefreshCurrentPoint ( );
    }

    /// <summary>
    /// 플레이어가 포함된 활성 영역 중 가장 가까운 포인트 선택
    /// </summary>
    void RefreshCurrentPoint ( )
    {
        CompanionPlacementPoint selected = null;

        if ( _hireController.CanPlace == true )
        {
            Vector3 playerPosition = _hero.position;
            float nearestDistance = float.MaxValue;

            foreach ( CompanionPlacementPoint point in _points )
            {
                if ( point.isActiveAndEnabled == false )
                    continue;

                if ( point.Contains ( playerPosition ) == false )
                    continue;

                float distance = ( point.Position - playerPosition ).sqrMagnitude;
                if ( distance >= nearestDistance )
                    continue;

                selected = point;
                nearestDistance = distance;
            }
        }

        if ( selected == _currentPoint )
            return;

        _currentPoint = selected;
        OnCurrentPointChanged?.Invoke ( _currentPoint );
    }

    /// <summary>
    /// 현재 영역에서 선택한 포인트에 고용 동료 배치
    /// </summary>
    /// <param name="hireId">배치할 고용 번호</param>
    /// <param name="result">배치 결과</param>
    /// <returns>배치 성공 여부</returns>
    public bool TryPlace ( int hireId , out CompanionHireResult result )
    {
        result = CompanionHireResult.Unavailable;
        if ( isActiveAndEnabled == false || _isInitialized == false )
            return false;

        //입력과 Update 순서에 상관없이 현재 플레이어 위치로 선택
        RefreshCurrentPoint ( );
        if ( _currentPoint == null )
            return false;

        return _hireController.TryPlace ( hireId , _currentPoint , out result );
    }

    private void OnDisable ( )
    {
        if ( _currentPoint == null )
            return;

        _currentPoint = null;
        OnCurrentPointChanged?.Invoke ( null );
    }
}
