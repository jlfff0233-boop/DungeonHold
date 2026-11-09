/// <summary>
/// 동료 고용 상태
/// </summary>
public enum CompanionHireState
{
    Hired,      //비용 지불 완료, 아직 미생성
    Prepared,   //생성 완료, 전투 시작 전 행동 보류
    Alive,      //고용된 동료 생존
    Dying,      //사망 모션 진행
    Waiting,    //객체 제거 후 재고용 대기
    Ready       //재고용 가능
}

/// <summary>
/// 동료 고용 기록 - 개별 고용 상태와 현재 객체 관리
/// </summary>
public class CompanionHireEntry
{
    int _id;    //실행 중 고용 기록 식별값
    CompanionHireData _data;    //해당 동료의 고용 설정
    Companion _instance;    //현재 생성된 동료
    CompanionHireState _state;    //현재 고용 상태
    double _readyTime;    //재고용 가능 시각
    CompanionPlacementPoint _placementPoint;    //점유한 배치 포인트

    /// <summary>
    /// 점유한 배치 포인트, 추종형 또는 미배치이면 null
    /// </summary>
    public CompanionPlacementPoint PlacementPoint => _placementPoint;

    /// <summary>
    /// 실행 중 고용 기록 식별값
    /// </summary>
    public int Id => _id;

    /// <summary>
    /// 해당 동료의 고용 설정
    /// </summary>
    public CompanionHireData Data => _data;

    /// <summary>
    /// 현재 생성된 동료
    /// </summary>
    public Companion Instance => _instance;

    /// <summary>
    /// 현재 고용 상태
    /// </summary>
    public CompanionHireState State => _state;

    /// <summary>
    /// 재고용 가능 시각
    /// </summary>
    public double ReadyTime => _readyTime;

    /// <summary>
    /// 고용 식별값과 설정 보존
    /// </summary>
    /// <param name="id">고용 식별값</param>
    /// <param name="data">고용 설정</param>
    public CompanionHireEntry ( int id , CompanionHireData data )
    {
        _id = id;
        _data = data;
        _state = CompanionHireState.Ready;
    }

    /// <summary>
    /// 비용 지불이 끝난 미생성 상태로 변경
    /// </summary>
    public void SetHired ( )
    {
        _instance = null;
        _placementPoint = null;
        _readyTime = 0;
        _state = CompanionHireState.Hired;
    }

    /// <summary>
    /// 초기화한 동료와 포인트 연결 후 행동 보류
    /// </summary>
    /// <param name="instance">초기화된 동료</param>
    /// <param name="point">배치 포인트, 추종형이면 null</param>
    public void SetPrepared ( Companion instance , CompanionPlacementPoint point )
    {
        _instance = instance;
        _placementPoint = point;
        _state = CompanionHireState.Prepared;
    }

    /// <summary>
    /// 준비된 동료의 전투 상태 확정
    /// </summary>
    public void SetAlive ( )
    {
        _state = CompanionHireState.Alive;
    }

    /// <summary>
    /// 사망 모션 진행 상태로 변경
    /// </summary>
    public void SetDying ( )
    {
        _state = CompanionHireState.Dying;
    }

    /// <summary>
    /// 타입별 재고용 가능 시각 반영
    /// </summary>
    /// <param name="readyTime">타입별 재고용 가능 시각</param>
    /// <param name="now">현재 게임 시각</param>
    public void SetRehireReadyTime ( double readyTime , double now )
    {
        _readyTime = readyTime;

        //사망 객체와 점유 포인트는 연출 완료까지 유지
        if ( _state == CompanionHireState.Dying )
            return;

        _state = now < readyTime
            ? CompanionHireState.Waiting
            : CompanionHireState.Ready;
    }

    /// <summary>
    /// 사망 객체 연결 해제 후 남은 대기에 맞춰 상태 확정
    /// </summary>
    /// <param name="now">사망 연출이 완료된 게임 시각</param>
    public void CompleteDeathPresentation ( double now )
    {
        _instance = null;
        _placementPoint = null;
        _state = now < _readyTime
            ? CompanionHireState.Waiting
            : CompanionHireState.Ready;
    }

    /// <summary>
    /// 기존 객체와 고용 상태를 유지하면서 점유 포인트 변경
    /// </summary>
    /// <param name="point">새 배치 포인트</param>
    public void SetPlacementPoint ( CompanionPlacementPoint point )
    {
        _placementPoint = point;
    }

    /// <summary>
    /// 대기가 끝난 기록을 재고용 가능 상태로 변경
    /// </summary>
    public void SetReady ( )
    {
        _state = CompanionHireState.Ready;
    }

    /// <summary>
    /// 도전 종료 시 현재 동료 연결 해제
    /// </summary>
    public void ClearInstance ( )
    {
        _instance = null;
        _placementPoint = null;
    }
}
