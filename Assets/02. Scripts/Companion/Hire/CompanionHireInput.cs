using UnityEngine;

/// <summary>
/// 동료 배치 입력 - 고용 순번 배치 요청 연결
/// </summary>
public class CompanionHireInput : MonoBehaviour
{
    [SerializeField] InputHandler _inputHandler;    //플레이어 입력
    [SerializeField] CompanionHireController _hireController;    //고용 관리자
    [SerializeField] CompanionPlacementController _placementController;    //배치 요청

    private void OnEnable ( )
    {
        _inputHandler.OnPlaceCompanionAInput += HandlePlaceCompanionA;
        _inputHandler.OnPlaceCompanionBInput += HandlePlaceCompanionB;
    }

    /// <summary>
    /// 첫 번째 고용 기록 배치 요청
    /// </summary>
    void HandlePlaceCompanionA ( )
    {
        RequestPlace ( 0 );
    }

    /// <summary>
    /// 두 번째 고용 기록 배치 요청
    /// </summary>
    void HandlePlaceCompanionB ( )
    {
        RequestPlace ( 1 );
    }

    /// <summary>
    /// 고용 순번의 기록을 현재 상호작용 포인트에 배치
    /// </summary>
    /// <param name="index">0이면 첫 번째, 1이면 두 번째</param>
    void RequestPlace ( int index )
    {
        if ( _hireController.TryGetEntryAt (
            index , out CompanionHireEntry entry ) == false )
        {
            Debug.Log (
                $"동료 배치: 결과={CompanionHireResult.Unavailable}"
                + $" / 고용 순번={index + 1}" , this );
            return;
        }

        bool placed = _placementController.TryPlace (
            entry.Id , out CompanionHireResult result );

        Debug.Log (
            $"동료 배치: 성공={placed} / 결과={result}"
            + $" / 고용 순번={index + 1}"
            + $" / 고용 번호={entry.Id}" , this );
    }

    private void OnDisable ( )
    {
        _inputHandler.OnPlaceCompanionAInput -= HandlePlaceCompanionA;
        _inputHandler.OnPlaceCompanionBInput -= HandlePlaceCompanionB;
    }
}
