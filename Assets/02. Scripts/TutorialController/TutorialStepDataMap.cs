using UnityEngine;

/// <summary>
/// 튜토리얼 단계 데이터맵 - 단계 원본 연결과 표시 순서 관리
/// </summary>
[CreateAssetMenu ( fileName = "TutorialStepDataMap" , menuName = "Tutorial/Tutorial Step Data Map" )]
public class TutorialStepDataMap : ScriptableObject
{
    [SerializeField] TutorialData [ ] _steps = new TutorialData [ 0 ];    //표시 순서대로 연결한 단계 SO

    /// <summary>
    /// 전체 단계 수
    /// </summary>
    public int StepCount => _steps.Length;

    /// <summary>
    /// 지정한 순서의 단계 원본 조회
    /// </summary>
    /// <param name="index">0부터 시작하는 단계 번호</param>
    /// <returns>해당 단계 SO</returns>
    public TutorialData GetStep ( int index )
    {
        return _steps [ index ];
    }
}
