using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 동료 이동 - NavMesh 이동, 활동 범위 제한, 방향 회전
/// </summary>
[RequireComponent ( typeof ( NavMeshAgent ) )]
public class CompanionMover : MonoBehaviour, IMoveSpeedModifier, IKnockbackMover
{
    [Header ( "----- 디버그 -----" )]
    [SerializeField] bool _showDebugLog = true;     //일반 디버그 로그 출력 여부

    [Header ( "----- 컴포넌트 -----" )]
    [SerializeField] NavMeshAgent _agent;       //경로 계산과 이동 시뮬레이션

    CompanionData _data;        //이동과 활동 범위 설정
    float _moveSpeedMultiplier = 1f;    //상태이상 이동 배율
    Vector3 _activityCenter;        //현재 활동과 복귀 기준 위치
    float _activityRadius;    //현재 이동 방식의 활동 반경
    NavMeshPath _path;     //이동 요청의 경로 계산 결과
    bool _canMove;      //이동과 회전 허용 여부
    int _moveFrame = -1; //일반 이동 적용 프레임
    float _moveSqrSpeed; //실제 수평 이동 속도 제곱

    /// <summary>
    /// 현재 프레임의 실제 일반 이동 여부
    /// </summary>
    public bool HasMovedThisFrame =>
        _moveFrame == Time.frameCount && _moveSqrSpeed > 0.01f;

    /// <summary>
    /// 이동과 회전 허용 여부
    /// </summary>
    public bool CanMove => isActiveAndEnabled && _canMove;

    /// <summary>
    /// 일반 행동 허용과 별개인 NavMesh 강제 이동 가능 여부
    /// </summary>
    public bool CanKnockback =>
        isActiveAndEnabled && _agent.isActiveAndEnabled && _agent.isOnNavMesh;

    /// <summary>
    /// Agent가 경로를 따라 이동 중인지 여부
    /// </summary>
    public bool IsMoving =>
        _agent.isOnNavMesh
        && _agent.isStopped == false
        && _agent.hasPath;

    /// <summary>
    /// 현재 경로의 도착 허용 거리 안에 있는지 여부
    /// </summary>
    public bool HasReachedDestination =>
        IsMoving && _agent.remainingDistance <= _data.ArrivalDistance;

    private void Awake ( )
    {
        //Unity 내부 기능을 사용하는 경로 객체는
        //필드 초기화가 아닌 Awake에서 생성
        _path = new NavMeshPath ( );
    }

    /// <summary>
    /// 이동 설정, 활동 중심, 초기 배치 위치 초기화
    /// </summary>
    /// <param name="data">동료 설정</param>
    /// <param name="activityCenter">월드 활동 중심</param>
    /// <param name="placementPoint">초기 배치 위치</param>
    /// <param name="activityRadius">현재 이동 방식의 활동 반경</param>
    /// <returns>NavMesh와 초기 위치의 유효 여부</returns>
    public bool Init (
        CompanionData data , Vector3 activityCenter ,
        Vector3? placementPoint , float activityRadius )
    {
        _data = data;
        _activityCenter = activityCenter;
        _activityRadius = activityRadius;
        _canMove = false;
        _moveFrame = -1;
        _moveSqrSpeed = 0f;

        //Agent가 Transform을 바로 이동시키면 활동 범위 밖의 위치도 적용될 수 있음
        //위치 자동 반영을 끄고 UpdateMove에서 계산 위치 검사 후 수동 동기화
        _agent.updatePosition = false;
        _agent.updateRotation = false;      //자동 회전도 해제
        _agent.autoRepath = false;
        _moveSpeedMultiplier = 1f;
        _agent.speed = data.MoveSpeed * _moveSpeedMultiplier;

        //공격 거리와 활동 중심 도착 거리에 따른 정지는 Companion에서 판단
        _agent.stoppingDistance = 0f;

        //배치형은 적 탐색 전에 지정점에 위치 적용
        //Transform만 변경하지 않고 Agent의 위치도 함께 이동
        if ( placementPoint.HasValue == true )
        {
            if ( _showDebugLog == true )
                Debug.Log ( $"[동료 이동:{name}] 초기 배치 요청 " +
                    $"/ 이동 전:{transform.position} / 지정점:{placementPoint.Value}" , this );

            if ( _agent.Warp ( placementPoint.Value ) == false )
            {
                Debug.LogError ( $"[동료 이동:{name}] 초기 배치 실패 " +
                    $"/ 지정점:{placementPoint.Value}" , this );
                return false;
            }

            Vector3 warpedAgentPosition = _agent.nextPosition;
            Vector3 placementOffset = warpedAgentPosition - placementPoint.Value;
            placementOffset.y = 0f;

            if ( _showDebugLog == true )
                Debug.Log ( $"[동료 이동:{name}] 초기 배치 결과 " +
                    $"/ 지정점:{placementPoint.Value} " +
                    $"/ 실제 Agent 위치:{warpedAgentPosition} " +
                    $"/ 수평 오차:{placementOffset.magnitude:F2} " +
                    $"/ Base Offset:{_agent.baseOffset}" , this );

            //기존 수동 위치 동기화 유지
            transform.position = warpedAgentPosition;
        }
        else
        {
            if ( _showDebugLog == true )
                Debug.Log ( $"[동료 이동:{name}] 추종형 시작 위치 유지 " +
                    $"/ 실제 위치:{transform.position} / 활동 중심:{activityCenter}" ,
                    this );
        }

        Vector3 activityOffset = transform.position - activityCenter;
        activityOffset.y = 0f;

        if ( _showDebugLog == true )
            Debug.Log (
                $"[동료 이동:{name}] 초기 위치 검사 / " +
                $"NavMesh 위:{_agent.isOnNavMesh} / " +
                $"활동 범위 안:{IsInsideActivity ( transform.position )} / " +
                $"현재 위치:{transform.position} / " +
                $"활동 중심:{activityCenter} / " +
                $"수평 거리:{activityOffset.magnitude:F2} / " +
                $"활동 반경:{_activityRadius}" , this );

        if ( _agent.isOnNavMesh == false )
        {
            Debug.LogError (
                $"[동료 이동:{name}] 초기화 실패: Agent가 NavMesh에 연결되지 않음 / " +
                $"현재 위치:{transform.position} / " +
                $"Agent 활성:{_agent.isActiveAndEnabled} / " +
                $"Agent Type:{_agent.agentTypeID} / " +
                $"Base Offset:{_agent.baseOffset}" , this );
            return false;
        }

        if ( IsInsideActivity ( transform.position ) == false )
        {
            Debug.LogError (
                $"[동료 이동:{name}] 초기화 실패: 활동 중심 기준 활동 범위 밖 / " +
                $"현재 위치:{transform.position} / " +
                $"활동 중심:{activityCenter} / " +
                $"수평 거리:{activityOffset.magnitude:F2} / " +
                $"활동 반경:{_activityRadius}" , this );
            return false;
        }

        Stop ( );
        return true;
    }

    /// <summary>
    /// 이동과 회전 허용 여부 설정
    /// </summary>
    /// <param name="enabled">행동 허용 여부</param>
    public void SetMoveEnabled ( bool enabled )
    {
        _canMove = enabled;

        if ( enabled == false )
            Stop ( );
    }

    /// <summary>
    /// 기본 이동속도에 상태이상 배율 적용
    /// </summary>
    /// <param name="multiplier">최종 이동속도 배율</param>
    public void SetMoveSpeedMultiplier ( float multiplier )
    {
        _moveSpeedMultiplier = multiplier;
        _agent.speed = _data.MoveSpeed * _moveSpeedMultiplier;
    }

    /// <summary>
    /// 현재 활동 중심과 이동 반경 갱신
    /// </summary>
    /// <param name="activityCenter">플레이어 위치 또는 고정 배치점</param>
    /// <param name="activityRadius">현재 이동 방식의 활동 반경</param>
    public void SetActivityCenter ( Vector3 activityCenter , float activityRadius )
    {
        _activityCenter = activityCenter;
        _activityRadius = activityRadius;
    }

    /// <summary>
    /// 확보한 배치 위치로 Agent와 실제 위치 동기화
    /// </summary>
    /// <param name="position">공간 검사를 통과한 NavMesh 위치</param>
    /// <returns>위치 변경 성공 여부</returns>
    public bool TryReposition ( Vector3 position )
    {
        if ( _agent.Warp ( position ) == false )
            return false;

        transform.position = _agent.nextPosition;
        return true;
    }

    /// <summary>
    /// 완전한 경로로 목적지 이동 요청
    /// </summary>
    /// <param name="destination">월드 목적지</param>
    /// <param name="limitActivity">목적지의 활동 범위 제한 여부, 복귀는 false</param>
    /// <returns>이동 요청 성공 여부</returns>
    public bool MoveTo ( Vector3 destination , bool limitActivity = true )
    {
        if ( CanMove == false || _agent.isOnNavMesh == false )
            return false;

        //배치점 복귀 등 직접 이동은 목적지 근처에서 자동 감속
        _agent.autoBraking = true;

        if ( TryMoveTo ( destination , limitActivity ) == true )
            return true;

        Stop ( );
        return false;
    }

    /// <summary>
    /// 이동 상태를 초기화하지 않고 경로 확보와 적용 시도
    /// </summary>
    /// <param name="destination">월드 목적지</param>
    /// <param name="limitActivity">활동 범위 제한 여부</param>
    /// <returns>경로 확보와 적용 성공 여부</returns>
    bool TryMoveTo ( Vector3 destination , bool limitActivity )
    {
        if ( limitActivity == true && IsInsideActivity ( destination ) == false )
            return false;

        //정지 거리와 별개로 새 목적지의 변화량을 비교
        //ArrivalDistance 이상 달라지면 새 경로 계산과 적용 시도
        if ( _agent.isStopped == false
            && _agent.hasPath == true
            && _agent.isPathStale == false
            && _agent.pathStatus == NavMeshPathStatus.PathComplete
            && ( _agent.destination - destination ).sqrMagnitude
                < _data.ArrivalDistance * _data.ArrivalDistance )
            return true;

        //후보 실패만으로 기존 이동 상태를 초기화하지 않음
        if ( _agent.CalculatePath ( destination , _path ) == false
            || _path.status != NavMeshPathStatus.PathComplete )
        {
            return false;
        }

        if ( _agent.SetPath ( _path ) == false )
            return false;

        //경로만 교체하고 속도와 nextPosition은 보존
        _agent.isStopped = false;
        return true;
    }

    /// <summary>
    /// 타겟의 공격 거리 안에 있는 NavMesh 위치로 접근
    /// </summary>
    /// <param name="targetPosition">타겟 루트 위치</param>
    /// <param name="attackRange">접근 정지와 공격 시작 거리</param>
    /// <returns>접근 가능한 경로 확보 여부</returns>
    public bool MoveToAttackRange ( Vector3 targetPosition , float attackRange )
    {
        if ( CanMove == false || _agent.isOnNavMesh == false )
            return false;

        //추종 이후 적 접근으로 전환할 때 자동 감속 복원
        _agent.autoBraking = true;

        return MoveToRange ( targetPosition , attackRange );
    }

    /// <summary>
    /// 현재 Hero 위치를 목적지로 추종 경로 요청
    /// </summary>
    /// <param name="heroPosition">현재 Hero 월드 위치</param>
    /// <returns>추종 경로 확보 여부</returns>
    public bool MoveToFollow ( Vector3 heroPosition )
    {
        if ( CanMove == false || _agent.isOnNavMesh == false )
            return false;

        //추종 정지는 Companion의 수평 거리 판단에서 처리
        _agent.autoBraking = false;

        //Hero 주변의 접근 지점을 만들지 않고 현재 위치로 경로 요청
        //유효한 기존 경로는 목적지 변화량이 ArrivalDistance 미만이면 유지
        if ( TryMoveTo ( heroPosition , false ) == true )
            return true;

        //최종 경로 확보 실패 시에만 이동 중단
        Stop ( );
        return false;
    }

    /// <summary>
    /// 지정 거리 안의 접근 위치로 이동 요청
    /// </summary>
    /// <param name="targetPosition">접근 기준 위치</param>
    /// <param name="distance">접근 정지 거리</param>
    /// <returns>접근 가능한 경로 확보 여부</returns>
    bool MoveToRange (
        Vector3 targetPosition , float distance )
    {
        //적 접근은 기존 끝점이 공격 사거리 안이면 경로 유지
        //Hero 추종은 MoveToFollow에서 별도로 처리
        if ( _agent.isStopped == false
            && _agent.hasPath == true
            && _agent.isPathStale == false
            && _agent.pathStatus == NavMeshPathStatus.PathComplete
            && IsInsideActivity ( _agent.destination ) == true
            && IsRangePosition ( _agent.destination , targetPosition , distance ) == true )
        {
            return true;
        }

        Vector3 direction = transform.position - targetPosition;
        direction.y = 0f;
        direction.Normalize ( );

        //정지 거리의 경계보다 조금 안쪽을 요청해
        //공격 사거리 진입 판단에 여유 확보
        float margin = Mathf.Min (
            _data.ArrivalDistance , distance * 0.5f );

        Vector3 approachPosition =
            targetPosition + direction * ( distance - margin );

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = _agent.agentTypeID ,
            areaMask = _agent.areaMask
        };

        //첫 후보가 실패해도 이동 상태를 유지한 채 두 번째 후보 확인
        if ( NavMesh.SamplePosition (
            approachPosition , out NavMeshHit hit ,
            _data.ArrivalDistance , filter ) == true )
        {
            if ( IsRangePosition (
                hit.position , targetPosition , distance ) == true
                && TryMoveTo ( hit.position , true ) == true )
                return true;
        }

        if ( NavMesh.SamplePosition (
            targetPosition , out hit , distance , filter ) == true )
        {
            if ( IsRangePosition (
                hit.position , targetPosition , distance ) == true
                && TryMoveTo ( hit.position , true ) == true )
                return true;
        }

        //두 후보 모두 경로를 확보하지 못한 경우에만 정지
        Stop ( );
        return false;
    }

    /// <summary>
    /// 접근 목적지의 공격 거리 충족 여부 확인
    /// </summary>
    /// <param name="position">후보 이동 위치</param>
    /// <param name="targetPosition">타겟 루트 위치</param>
    /// <param name="distance">접근 정지 거리</param>
    /// <returns>접근에 사용할 수 있는 위치 여부</returns>
    bool IsRangePosition (
        Vector3 position , Vector3 targetPosition , float distance )
    {
        Vector3 offset = position - targetPosition;
        offset.y = 0f;

        return offset.sqrMagnitude <= distance * distance;
    }

    /// <summary>
    /// 위협에서 멀어지는 활동 반경 내 경로 요청
    /// </summary>
    /// <param name="threatPosition">가까운 위협의 위치</param>
    /// <returns>후퇴 경로 확보 여부</returns>
    public bool MoveAwayFrom ( Vector3 threatPosition )
    {
        if ( CanMove == false || _agent.isOnNavMesh == false )
            return false;

        _agent.autoBraking = true;
        Vector3 direction = transform.position - threatPosition;
        direction.y = 0f;

        //같은 수평 위치에서는 현재 정면을 후보 탐색 기준으로 사용
        if ( direction.sqrMagnitude == 0f )
            direction = transform.forward;
        else
            direction.Normalize ( );

        Vector3 currentOffset = transform.position - threatPosition;
        currentOffset.y = 0f;
        float currentDistance = currentOffset.sqrMagnitude;

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = _agent.agentTypeID ,
            areaMask = _agent.areaMask
        };

        for ( int i = 0 ; i < _data.RetreatCandidateCount ; i++ )
        {
            //게임 수치가 아닌 원의 각도를 후보 수로 분할
            float angle = 360f * i / _data.RetreatCandidateCount;
            Vector3 candidateDirection =
                Quaternion.AngleAxis ( angle , Vector3.up ) * direction;
            Vector3 requested = threatPosition
                + candidateDirection * _data.RetreatDistance;

            //확보 거리보다 방어 반경을 우선하여 후보 위치 제한
            Vector3 offset = requested - _activityCenter;
            offset.y = 0f;

            if ( offset.sqrMagnitude > _activityRadius * _activityRadius )
            {
                offset = offset.normalized * _activityRadius;
                requested.x = _activityCenter.x + offset.x;
                requested.z = _activityCenter.z + offset.z;
            }

            if ( NavMesh.SamplePosition ( requested , out NavMeshHit hit ,
                _data.RetreatSampleDistance , filter ) == false )
                continue;

            //이미 도착한 위치를 새 후퇴 경로로 반복 승인하지 않음
            Vector3 displacement = hit.position - transform.position;
            displacement.y = 0f;

            if ( displacement.sqrMagnitude <= _data.ArrivalDistance * _data.ArrivalDistance )
                continue;

            Vector3 threatOffset = hit.position - threatPosition;
            threatOffset.y = 0f;

            if ( threatOffset.sqrMagnitude <= currentDistance )
                continue;

            //최종 활동 범위와 완전한 경로는 공통 이동 요청에서 검사
            if ( TryMoveTo ( hit.position , true ) == true )
                return true;
        }

        Stop ( );
        return false;
    }

    /// <summary>
    /// 이동 결과와 이동 방향 회전 적용,
    /// 적 추적은 활동 범위 검사
    /// </summary>
    /// <param name="deltaTime">프레임 경과 시간</param>
    /// <param name="limitActivity">활동 범위 제한 여부, 복귀는 false</param>
    /// <returns>현재 이동 유지 가능 여부</returns>
    public bool UpdateMove ( float deltaTime , bool limitActivity = true )
    {
        _moveFrame = Time.frameCount;
        _moveSqrSpeed = 0f;
        if ( CanMove == false || deltaTime <= 0f )
            return true;

        if ( _agent.isOnNavMesh == false )
            return false;

        if ( _agent.isStopped == true )
        {
            //정지 중 시뮬레이션 위치가
            //캐릭터 위치와 분리되지 않도록 유지
            _agent.nextPosition = transform.position;
            return true;
        }

        if ( _agent.isPathStale == true
            || ( _agent.hasPath == true && _agent.pathStatus != NavMeshPathStatus.PathComplete ) )
        {
            Stop ( );
            return false;
        }

        Vector3 nextPosition = _agent.nextPosition;

        //nextPosition은 아직 Transform에 적용하지 않은 Agent의 계산 위치
        //목적지가 범위 안이어도 우회와 회피 이동은 밖으로 나갈 수 있음
        //Transform에 먼저 적용하지 않고 마지막 유효 위치에서 이동 중단
        if ( limitActivity == true && IsInsideActivity ( nextPosition ) == false )
        {
            Stop ( );
            return false;
        }

        Vector3 direction = nextPosition - transform.position;
        direction.y = 0f;

        //검사를 통과한 계산 위치만 실제 캐릭터 위치에 반영
        //프레임당 거리 대신 실제 속도로 발소리 조건 제공
        _moveSqrSpeed = direction.sqrMagnitude / ( deltaTime * deltaTime );
        transform.position = nextPosition;

        if ( direction.sqrMagnitude > 0.0001f )
            Face ( transform.position + direction , deltaTime );

        return true;
    }

    /// <summary>
    /// NavMesh 경계 안에서 강제 이동 적용
    /// </summary>
    /// <param name="displacement">요청 수평 이동량</param>
    /// <param name="deltaTime">프레임 경과 시간</param>
    /// <returns>NavMesh 경계에 막히지 않았는지 여부</returns>
    public bool MoveKnockback ( Vector3 displacement , float deltaTime )
    {
        if ( CanKnockback == false )
            return false;

        displacement.y = 0f;
        Vector3 destination = transform.position + displacement;

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = _agent.agentTypeID ,
            areaMask = _agent.areaMask
        };

        //일반 경로는 시작 시 행동 제한으로 정리됨
        //넉백은 우회 경로와 활동 범위 제한 없이 요청 방향으로만 이동
        bool blocked = NavMesh.Raycast (
            transform.position , destination , out NavMeshHit hit , filter );

        //경계에 막혔을 때만 hit 위치를 사용하고 그 프레임에 넉백을 종료
        //통과한 경우에는 요청 위치를 Agent에 전달하여 표면에 맞게 제한
        Vector3 position = blocked == true
            ? hit.position + Vector3.up * _agent.baseOffset
            : destination;

        _agent.nextPosition = position;
        transform.position = _agent.nextPosition;
        return blocked == false;
    }

    /// <summary>
    /// 경로와 속도를 정리하고 현재 위치에 정지
    /// </summary>
    public void Stop ( )
    {
        _moveFrame = -1;
        _moveSqrSpeed = 0f;
        if ( _agent.isOnNavMesh == false )
            return;

        //정지 플래그만으로는 이전 경로가 남으므로 경로와 속도도 함께 정리
        _agent.isStopped = true;
        _agent.ResetPath ( );
        _agent.velocity = Vector3.zero;

        //아직 적용하지 않은 계산 위치가
        //공격 정지나 재이동 때 반영되지 않도록 동기화
        _agent.nextPosition = transform.position;
    }

    /// <summary>
    /// 지정 위치를 향해 수평 회전
    /// </summary>
    /// <param name="position">바라볼 월드 위치</param>
    /// <param name="deltaTime">프레임 경과 시간</param>
    /// <returns>목표 방향과의 차이가 1도 이하인지 여부</returns>
    public bool Face ( Vector3 position , float deltaTime )
    {
        if ( CanMove == false )
            return false;

        Vector3 direction = position - transform.position;
        direction.y = 0f;

        if ( direction.sqrMagnitude <= 0.0001f )
            return true;

        Quaternion rotation = Quaternion.LookRotation ( direction );

        transform.rotation = Quaternion.RotateTowards (
            transform.rotation , rotation , _data.RotationSpeed * deltaTime );

        return Quaternion.Angle ( transform.rotation , rotation ) <= 1f;
    }

    /// <summary>
    /// 지정 위치와의 수평 거리 확인
    /// </summary>
    /// <param name="position">비교할 월드 위치</param>
    /// <param name="distance">허용 거리</param>
    /// <returns>허용 거리 안인지 여부</returns>
    public bool IsAt ( Vector3 position , float distance )
    {
        Vector3 offset = position - transform.position;
        offset.y = 0f;

        return offset.sqrMagnitude <= distance * distance;
    }

    /// <summary>
    /// 활동 중심 기준 활동 범위 확인
    /// </summary>
    /// <param name="position">검사할 월드 위치</param>
    /// <returns>활동 범위 안인지 여부</returns>
    bool IsInsideActivity ( Vector3 position )
    {
        //추종형은 갱신된 플레이어 위치,
        //배치형은 고정 배치점을 기준으로 검사
        Vector3 offset = position - _activityCenter;
        offset.y = 0f;

        return offset.sqrMagnitude
            <= _activityRadius * _activityRadius;
    }

    private void OnDisable ( )
    {
        Stop ( );
    }

#if UNITY_EDITOR
    private void OnDrawGizmos ( )
    {
        if ( _agent == null || _agent.isOnNavMesh == false
            || _agent.hasPath == false )
            return;

        //후보 계산 경로가 아닌 Agent가 실제 사용 중인 경로 표시
        Vector3 [ ] corners = _agent.path.corners;
        Color previousColor = Gizmos.color;
        Gizmos.color = Color.green;

        for ( int i = 1 ; i < corners.Length ; i++ )
            Gizmos.DrawLine ( corners [ i - 1 ] , corners [ i ] );

        Gizmos.color = previousColor;
    }
#endif
}
