using UnityEngine;

/// <summary>
/// 동료 설정 데이터 - 전투 참조, 이동, 탐색과 접근 설정
/// </summary>
[CreateAssetMenu( fileName = "CompanionData", menuName = "Companion/CompanionData" )]
public class CompanionData : ScriptableObject
{
    [Header( "----- 전투 -----" )]
    [SerializeField] WeaponType _weaponType;    //동료의 고정 무기 타입
    [SerializeField] CombatStatsData _combatStatsData;      //체력과 방어 설정
    [SerializeField] AttackData _attackData;        //기본 공격 설정

    [Header( "----- 이동 -----" )]
    [SerializeField] float _moveSpeed;      //기본 이동 속력
    [SerializeField] float _rotationSpeed;      //초당 회전 각도
    [SerializeField] float _followDistance = 2f;        //플레이어 추종 정지 거리
    [SerializeField] float _arrivalDistance;        //배치점 도착 허용 거리

    [Header( "----- 행동 범위 -----" )]
    [SerializeField] float _detectionMargin;    //공격 거리 밖의 탐색 여유
    [SerializeField] float _defendRadius;    //배치점 기준 최대 이동 반경
    [SerializeField] float _activityRadius;     //현재 활동 중심 기준 활동 반경
    [SerializeField] float _attackRange;        //접근 정지와 공격 시작 거리
    [SerializeField] float _searchInterval;     //공격 대상과 후퇴 경로 갱신 간격

    [Header( "----- 후퇴 -----" )]
    [SerializeField] bool _canRetreat;    //후퇴 행동 사용 여부
    [SerializeField] float _retreatStartDistance;    //후퇴 시작 거리
    [SerializeField] float _retreatDistance;    //후퇴 시 확보할 거리
    [SerializeField] int _retreatCandidateCount;    //후퇴 후보 방향 수
    [SerializeField] float _retreatSampleDistance;    //후퇴 후보의 NavMesh 보정 범위

    /// <summary>
    /// 동료의 고정 무기 타입
    /// </summary>
    public WeaponType WeaponType => _weaponType;

    /// <summary>
    /// 체력과 방어 설정
    /// </summary>
    public CombatStatsData CombatStatsData => _combatStatsData;

    /// <summary>
    /// 기본 공격 설정
    /// </summary>
    public AttackData AttackData => _attackData;

    /// <summary>
    /// 기본 이동 속력
    /// </summary>
    public float MoveSpeed => _moveSpeed;

    /// <summary>
    /// 초당 회전 각도
    /// </summary>
    public float RotationSpeed => _rotationSpeed;

    /// <summary>
    /// 배치점 도착 허용 거리
    /// </summary>
    public float ArrivalDistance => _arrivalDistance;

    /// <summary>
    /// 플레이어 추종 정지 거리
    /// </summary>
    public float FollowDistance => _followDistance;

    /// <summary>
    /// 현재 위치 기준 탐색 반경
    /// </summary>
    public float DetectionRadius => _attackRange + _detectionMargin;

    /// <summary>
    /// 방어형 최대 이동 반경
    /// </summary>
    public float DefendRadius => _defendRadius;

    /// <summary>
    /// 후퇴 행동 사용 여부
    /// </summary>
    public bool CanRetreat => _canRetreat;

    /// <summary>
    /// 후퇴 시작 거리
    /// </summary>
    public float RetreatStartDistance => _retreatStartDistance;

    /// <summary>
    /// 후퇴 시 확보할 거리
    /// </summary>
    public float RetreatDistance => _retreatDistance;

    /// <summary>
    /// 후퇴 후보 방향 수
    /// </summary>
    public int RetreatCandidateCount => _retreatCandidateCount;

    /// <summary>
    /// 후퇴 후보의 NavMesh 보정 범위
    /// </summary>
    public float RetreatSampleDistance => _retreatSampleDistance;

    /// <summary>
    /// 현재 활동 중심 기준 활동 반경
    /// </summary>
    public float ActivityRadius => _activityRadius;

    /// <summary>
    /// 접근 정지와 공격 시작 거리
    /// </summary>
    public float AttackRange => _attackRange;

    /// <summary>
    /// 공격 대상과 후퇴 경로 갱신 간격
    /// </summary>
    public float SearchInterval => _searchInterval;

}
