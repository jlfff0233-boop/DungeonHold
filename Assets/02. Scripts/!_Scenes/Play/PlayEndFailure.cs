/// <summary>
/// 도전 종료 처리 실패 위치
/// </summary>
public enum PlayEndFailure
{
    None,
    Mission,        //미션
    Collection,     //도감
    Currency,       //재화
    CompanionCleanup,       //동료 정리
    MissionCleanup,     //미션 정리
    StageRecord,        //기존 클리어 기록
    RewardCalculation,    //종료 보상 계산
    RewardSave            //종료 보상 저장
}
