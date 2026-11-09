using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyMovement :
    MonoBehaviour,
    IMoveSpeedModifier,
    IKnockbackMover
{
    // 초기화 시 EnemyData에서 전달받는 런타임 기본 속도
    private float _moveSpeed;
    [SerializeField] private float _arrivalDistance = 0.3f;

    private Transform _chaseTarget;
    private bool _isChasingPlayer;

    [SerializeField, Min(0.05f)]
    private float _chaseRepathInterval = 0.2f;

    [SerializeField, Min(0.01f)]
    private float _chaseSampleDistance = 1f;

    // 플레이어 발 위치와 NavMesh 사이의 허용 높이 차이
    [SerializeField, Min(0.01f)]
    private float _maxChaseTargetHeight = 0.5f;

    private NavMeshPath _chaseValidationPath;

    private Transform _lastCheckedChaseTarget;
    private double _nextChaseTargetCheckTime;
    private bool _hasReachableChaseTarget;
    private Vector3 _chaseGroundPosition;

    private float _remainingChaseRepathTime;

    // 생성한 적에게 스폰하는 쪽에서 경로를 전달
    private EnemyPath _enemyPath;

    private NavMeshAgent _navMeshAgent;
    private NavMeshPath _movementPath;

    private int _currentWaypointIndex;
    private Vector3 _currentWaypointPosition;
    private bool _isMoving;
    private bool _hasPassed;
    private float _moveSpeedMultiplier = 1f;

    private bool _isMovementPaused;

    private bool _isMovementPrepared;
    private bool _isFlying;
    private float _flightHeight;
    private float _defenseGoalAttackRange;
    private float _flightRotationSpeed;
    private Vector3 _flyingVelocity;

    public bool IsFlying => _isFlying;

    public bool HasPassed => _hasPassed;

    public bool IsMovementPaused => _isMovementPaused;

    // 첫 웨이포인트 이전 구간을 구성할 생성 위치
    private Vector3 _routeStartPosition;

    // 추격 종료 후 실제 이동을 재개할 때 한 번만 복귀 지점 선택
    private bool _needsReturnWaypointSelection;

    private NavMeshPath _returnRoutePath;
    private NavMeshPath _returnAccessPath;

    // 사망·통과·이동 실패로 종료된 적은 강제 이동 불가
    public bool CanKnockback =>
            _isMovementPrepared &&
            isActiveAndEnabled &&
            !_hasPassed &&
            (_isFlying ||
            (_navMeshAgent != null &&
            _navMeshAgent.isActiveAndEnabled &&
            _navMeshAgent.isOnNavMesh));

    private float _knockbackDistanceMultiplier = 1f;

    // 스포너가 전달하는 Stage 2 방어 목표
    private Stage2DefenseGoal _defenseGoal;

    // 목표가 파괴돼 참조가 사라져도 도착 방식은 유지
    private bool _usesDefenseGoal;
    private bool _hasReachedDefenseGoal;

    public Stage2DefenseGoal DefenseGoal => _defenseGoal;
    public bool HasReachedDefenseGoal => _hasReachedDefenseGoal;

    // 최종 지점을 통과한 적을 구독자에게 전달
    public event Action<EnemyMovement> OnPassed;

    private void Awake()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _movementPath = new NavMeshPath();
        _returnRoutePath = new NavMeshPath();
        _returnAccessPath = new NavMeshPath();
        _chaseValidationPath = new NavMeshPath();
    }

    // 경로를 전달받고 첫 웨이포인트로 이동 시작
    public bool TryInitialize(EnemyPath enemyPath, EnemyData data)
    {
        // 현재 예시는 새로 생성한 활성 적을 한 번 초기화하는 용도
        if (!isActiveAndEnabled || !_isMovementPrepared || _isMoving || _hasPassed)
        {
            Debug.LogError("현재 상태에서는 적의 이동을 초기화할 수 없습니다.", this);
            return false;
        }

        if (data == null || !data.IsValid)
        {
            Debug.LogError(
                "적 이동 초기화에 유효한 EnemyData가 필요합니다.",
                this);

            return false;
        }

        _moveSpeed = data.MoveSpeed;

        _defenseGoalAttackRange = data.AttackData.Radius;

        _knockbackDistanceMultiplier = data.KnockbackDistanceMultiplier;

        _enemyPath = enemyPath;

        if (_enemyPath == null || _enemyPath.WaypointCount == 0)
        {
            StopWithError("이동 경로가 연결되지 않았거나 웨이포인트가 없습니다.");
            return false;
        }

        bool hasValidGroundSettings =
                _isFlying ||
                (_navMeshAgent.stoppingDistance < _arrivalDistance &&
                _chaseRepathInterval > 0f &&
                !float.IsInfinity(_chaseRepathInterval));

        bool isValidSetting =
            _moveSpeed > 0f &&
            _arrivalDistance > 0f &&
            hasValidGroundSettings;

        if (!isValidSetting)
        {
            StopWithError(
                "속도와 도착 거리는 양수여야 하며, Stopping Distance는 도착 거리보다 작아야 합니다.");
            return false;
        }

        if (!_isFlying)
        {
            _navMeshAgent.speed = _moveSpeed * _moveSpeedMultiplier;
        }
        _currentWaypointIndex = 0;

        _routeStartPosition = transform.position;
        _needsReturnWaypointSelection = false;

        MoveToCurrentWaypoint();

        // 경로 적용에 실패하면 MoveToCurrentWaypoint에서 이동 실패 처리
        return _isMoving;
    }

    private void Update()
    {
        if (!_isMoving || _hasPassed)
        {
            return;
        }

        if (_isFlying)
        {
            _flyingVelocity = Vector3.zero;

            if (!_isMovementPaused)
            {
                UpdateFlyingMovement();
            }

            return;
        }

        if (!_navMeshAgent.isActiveAndEnabled || !_navMeshAgent.isOnNavMesh)
        {
            StopWithError("적이 활성 NavMesh 위에 있지 않습니다.");
            return;
        }

        // 일시 정지 중에는 웨이포인트 진행과 도착 판정 중단
        if (_isMovementPaused)
        {
            return;
        }

        // 추격 중에는 플레이어 방향으로 이동하며
        // 기존 웨이포인트 번호를 진행시키지 않음
        if (_isChasingPlayer)
        {
            UpdateChaseMovement();
            return;
        }

        // 경로 재계산 중에는 도착이나 이동 실패로 판단하지 않음
        if (_navMeshAgent.pathPending)
        {
            return;
        }

        if (_navMeshAgent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            StopWithError("현재 웨이포인트까지 이동 가능한 경로가 없습니다.");
            return;
        }

        float remainingDistance = _navMeshAgent.remainingDistance;

        // 아직 남은 거리를 알 수 없다면 다음 프레임에서 다시 확인
        if (float.IsInfinity(remainingDistance) || float.IsNaN(remainingDistance))
        {
            return;
        }

        // 지상 적은 NavMesh 높이를 따라가므로
        // 도착 판정에서는 경로 거리와 수평 거리를 함께 확인
        Vector3 waypointOffset =
            _currentWaypointPosition - transform.position;

        waypointOffset.y = 0f;

        float distanceToWaypoint = waypointOffset.magnitude;

        bool hasArrived =
            remainingDistance <= _arrivalDistance &&
            distanceToWaypoint <= _arrivalDistance;

        if (hasArrived)
        {
            if (TryHandleDefenseGoalArrival())
            {
                return;
            }

            _currentWaypointIndex++;

            if (_currentWaypointIndex >= _enemyPath.WaypointCount)
            {
                HandlePassed();
            }
            else
            {
                MoveToCurrentWaypoint();
            }
        }
        else if (!_navMeshAgent.hasPath)
        {
            StopWithError(
                $"목표에 도착하기 전에 이동 경로가 없어졌습니다.\n" +
                $"적: {name}, 경로: {_enemyPath.name}\n" +
                $"목표: {_enemyPath.transform.GetChild(_currentWaypointIndex).name}\n" +
                $"현재 위치: {transform.position.ToString("F3")}\n" +
                $"목표 위치: {_currentWaypointPosition.ToString("F3")}\n" +
                $"남은 경로 거리: {remainingDistance:F3}, " +
                $"실제 목표 거리: {distanceToWaypoint:F3}, " +
                $"도착 허용 거리: {_arrivalDistance:F3}");
        }
    }

    // 현재 웨이포인트까지 완전한 경로를 확보한 뒤 이동
    private void MoveToCurrentWaypoint()
    {
        if (_isFlying)
        {
            if (_enemyPath == null ||
                !_enemyPath.TryGetWaypointPosition(
                    _currentWaypointIndex,
                    out Vector3 waypointPosition))
            {
                StopWithError("비행 웨이포인트를 찾을 수 없습니다.");
                return;
            }

            bool isDefenseGoalApproach =
            _usesDefenseGoal &&
            _defenseGoal != null &&
            _currentWaypointIndex == _enemyPath.WaypointCount - 1;

            if (isDefenseGoalApproach)
            {
                // 마지막 구간에서는 목표 위의 공격 가능한 높이로 접근
                // 현재 공격 거리 2라면 목표보다 최대 1 높은 위치를 사용
                float approachHeight = Mathf.Min(
                    _flightHeight,
                    _defenseGoalAttackRange * 0.5f);

                _currentWaypointPosition =
                    _defenseGoal.transform.position +
                    Vector3.up * approachHeight;
            }
            else
            {
                // 일반 구간과 Stage 1은 기존 비행 높이 유지
                _currentWaypointPosition =
                    waypointPosition + Vector3.up * _flightHeight;
            }

            _isMoving = true;
            return;
        }

        if (!_navMeshAgent.isActiveAndEnabled || !_navMeshAgent.isOnNavMesh)
        {
            StopWithError("적이 활성 NavMesh 위에 있지 않습니다.");
            return;
        }

        if (_needsReturnWaypointSelection)
        {
            int previousIndex = _currentWaypointIndex;

            if (!TrySelectReturnWaypoint(out int returnIndex))
            {
                StopWithError(
                    "현재 위치에서 복귀 가능한 경로 구간을 찾지 못했습니다.");

                return;
            }

            _currentWaypointIndex = returnIndex;
            _needsReturnWaypointSelection = false;

            Debug.Log(
                $"복귀 웨이포인트 재선택: " +
                $"{previousIndex} → {_currentWaypointIndex}, " +
                $"현재 위치 {transform.position}",
                this);
        }

        if (_enemyPath == null ||
            !_enemyPath.TryGetWaypointPosition(
                _currentWaypointIndex, out _currentWaypointPosition))
        {
            StopWithError("현재 웨이포인트를 찾을 수 없습니다.");
            return;
        }

        bool hasCalculatedPath =
            _navMeshAgent.CalculatePath(_currentWaypointPosition, _movementPath);

        if (!hasCalculatedPath ||
            _movementPath.status != NavMeshPathStatus.PathComplete)
        {
            StopWithError("현재 웨이포인트까지 완전한 경로를 만들 수 없습니다.");
            return;
        }

        if (!_navMeshAgent.SetPath(_movementPath))
        {
            StopWithError("계산한 이동 경로를 적용할 수 없습니다.");
            return;
        }

        _navMeshAgent.isStopped = false;
        _isMoving = true;
    }

    // 마지막 지점 도착은 처치와 구분하여 한 번만 처리
    // 통과 상태를 확정한 뒤 알림을 보내고 제거
    private void HandlePassed()
    {
        if (_hasPassed)
        {
            return;
        }

        _hasPassed = true;
        _isMoving = false;
        _isMovementPaused = false;
        _flyingVelocity = Vector3.zero;

        if (!_isFlying &&
            _navMeshAgent.isActiveAndEnabled &&
            _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = true;
        }

        Debug.Log("적이 최종 지점을 통과했습니다.", this);

        // 제거 전에 알려야 구독자가 해당 적의 등록을 해제할 수 있음
        OnPassed?.Invoke(this);

        Destroy(gameObject);
    }

    // 현재 웨이포인트를 유지하면서 이동 일시 정지 또는 재개
    public bool TrySetMovementPaused(bool isPaused)
    {
        bool canChangeState =
                isActiveAndEnabled &&
                _isMoving &&
                !_hasPassed &&
                (_isFlying ||
                (_navMeshAgent != null &&
                _navMeshAgent.isActiveAndEnabled &&
                _navMeshAgent.isOnNavMesh));

        if (!canChangeState)
        {
            return false;
        }

        // 같은 상태를 다시 요청해도 경로를 반복 계산하지 않음
        if (_isMovementPaused == isPaused)
        {
            return true;
        }

        if (_isFlying)
        {
            _isMovementPaused = isPaused;
            _flyingVelocity = Vector3.zero;

            Debug.Log(
                $"비행 이동 정지 상태: {isPaused}, " +
                $"웨이포인트 {_currentWaypointIndex}",
                this);

            return true;
        }

        if (isPaused)
        {
            // 경로와 웨이포인트 번호는 보존
            _navMeshAgent.isStopped = true;
            _navMeshAgent.velocity = Vector3.zero;
            _isMovementPaused = true;

            Debug.Log(
                $"적 이동 일시 정지: 웨이포인트 {_currentWaypointIndex}, " +
                $"위치 {transform.position}",
                this);

            return true;
        }

        // 반격 도중에는 추격을 재개하고,
        // 반격 종료 후에는 보존한 웨이포인트로 복귀
        if (_isChasingPlayer)
        {
            _remainingChaseRepathTime = 0f;
            UpdateChaseMovement();
        }
        else
        {
            MoveToCurrentWaypoint();
        }

        if (!_isMoving)
        {
            return false;
        }

        _isMovementPaused = false;

        Debug.Log(
            $"적 이동 재개: 추격 {_isChasingPlayer}, " +
            $"보존한 웨이포인트 {_currentWaypointIndex}",
            this);

        return true;
    }

    // 사망한 적은 이동과 도착 판정을 더 이상 진행하지 않음
    public void StopMovement()
    {
        _isMoving = false;
        _isMovementPaused = false;
        _flyingVelocity = Vector3.zero;

        if (_navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.ResetPath();
        }

        // 영구 이동 중단: 사망한 적을 NavMeshAgent 회피 대상에서도 제외
        _navMeshAgent.enabled = false;

        enabled = false;
    }

    // 이동 실패 시 정지하고 오류를 한 번 표시
    private void StopWithError(string message)
    {
        _isMoving = false;
        _isMovementPaused = false;

        if (_navMeshAgent.isActiveAndEnabled && _navMeshAgent.isOnNavMesh)
        {
            _navMeshAgent.isStopped = true;
            _navMeshAgent.ResetPath();
        }

        Debug.LogError(message, this);
        enabled = false;
    }

    // 공통 상태이상 모듈이 계산한 최종 배율을 전달받음
    public void SetMoveSpeedMultiplier(float multiplier)
    {
        if (float.IsNaN(multiplier) ||
            float.IsInfinity(multiplier) ||
            multiplier < 0f)
        {
            Debug.LogError("이동속도 배율이 유효하지 않습니다.", this);
            return;
        }

        _moveSpeedMultiplier = multiplier;

        if (_navMeshAgent != null &&
            _navMeshAgent.isActiveAndEnabled)
        {
            _navMeshAgent.speed =
                _moveSpeed * _moveSpeedMultiplier;
        }
    }

    // 넉백 중 일반 이동이 정지된 상태에서 호출
    public bool MoveKnockback(
        Vector3 displacement,
        float deltaTime)
    {
        if (!CanKnockback ||
            !_isMovementPaused ||
            deltaTime <= 0f ||
            float.IsNaN(deltaTime) ||
            float.IsInfinity(deltaTime))
        {
            return false;
        }

        displacement.y = 0f;

        if (float.IsNaN(displacement.x) ||
            float.IsNaN(displacement.z) ||
            float.IsInfinity(displacement.x) ||
            float.IsInfinity(displacement.z))
        {
            return false;
        }

        // 넉백 시간은 유지하고 실제 이동 거리만 조정
        displacement *= _knockbackDistanceMultiplier;

        Vector3 destination =
            transform.position + displacement;

        // 공중 넉백은 높이를 유지하는 수평 이동
        if (_isFlying)
        {
            transform.position = destination;
            _flyingVelocity = Vector3.zero;
            return true;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = _navMeshAgent.agentTypeID,
            areaMask = _navMeshAgent.areaMask
        };

        // 요청 방향의 NavMesh 경계 검사
        bool blocked = NavMesh.Raycast(
            transform.position,
            destination,
            out NavMeshHit hit,
            filter);

        Vector3 position = blocked
            ? hit.position + Vector3.up * _navMeshAgent.baseOffset
            : destination;

        // Agent 계산 위치와 실제 오브젝트 위치를 함께 반영
        _navMeshAgent.nextPosition = position;
        transform.position = _navMeshAgent.nextPosition;

        // 경계에 막히면 공통 넉백 모듈이 진행을 종료
        return !blocked;
    }

    public void SetChaseTarget(Transform target)
    {
        bool shouldChase = target != null;

        if (_chaseTarget == target &&
            _isChasingPlayer == shouldChase)
        {
            return;
        }

        bool wasChasing = _isChasingPlayer;

        _chaseTarget = target;
        _isChasingPlayer = shouldChase;
        _remainingChaseRepathTime = 0f;

        if (_isChasingPlayer)
        {
            Debug.Log(
                $"플레이어 추격 시작: " +
                $"보존한 웨이포인트 {_currentWaypointIndex}",
                this);
        }
        else if (wasChasing)
        {
            // 정지 중이라면 이동 재개 시점의 위치를 기준으로 선택
            _needsReturnWaypointSelection = true;

            // 이동 중 추격이 끝났다면 즉시 원래 경로 복원
            // 정지 중이라면 정지 해제 시 복원
            if (isActiveAndEnabled &&
                _isMoving &&
                !_hasPassed &&
                !_isMovementPaused)
            {
                MoveToCurrentWaypoint();
            }

            Debug.Log(
            "플레이어 추격 종료: 이동 재개 시 복귀 구간 재선택",
            this);
        }
    }

    private void UpdateChaseMovement()
    {
        if (_chaseTarget == null)
        {
            SetChaseTarget(null);
            return;
        }

        _remainingChaseRepathTime -= Time.deltaTime;

        if (_remainingChaseRepathTime > 0f)
        {
            return;
        }

        _remainingChaseRepathTime = _chaseRepathInterval;

        if (!CanChaseTarget(_chaseTarget))
        {
            MarkChaseTargetUnreachable();
            return;
        }

        // 공중의 Transform 위치 대신 확인된 NavMesh 위치 사용
        bool hasCalculatedPath = _navMeshAgent.CalculatePath(
            _chaseGroundPosition,
            _movementPath);

        if (!hasCalculatedPath ||
            _movementPath.status != NavMeshPathStatus.PathComplete)
        {
            MarkChaseTargetUnreachable();
            return;
        }

        if (!_navMeshAgent.SetPath(_movementPath))
        {
            MarkChaseTargetUnreachable();
            return;
        }

        _navMeshAgent.isStopped = false;
    }

    private void MarkChaseTargetUnreachable()
    {
        _hasReachableChaseTarget = false;

        _nextChaseTargetCheckTime =
            Time.timeAsDouble + _chaseRepathInterval;

        // 반격 코드가 복귀를 결정할 때까지 잠시 정지
        // 이동 컴포넌트와 웨이포인트 진행 상태는 유지
        _navMeshAgent.isStopped = true;
        _navMeshAgent.velocity = Vector3.zero;
    }

    private bool TrySelectReturnWaypoint(out int waypointIndex)
    {
        waypointIndex = _currentWaypointIndex;

        if (_enemyPath == null ||
            _currentWaypointIndex >= _enemyPath.WaypointCount)
        {
            return false;
        }

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = _navMeshAgent.agentTypeID,
            areaMask = _navMeshAgent.areaMask
        };

        float bestDistance = float.PositiveInfinity;
        bool hasCandidate = false;

        // 현재 목표부터 이후 구간만 검사
        // 이미 완료한 웨이포인트로 목표 번호를 되돌리지 않음
        for (int nextIndex = _currentWaypointIndex;
             nextIndex < _enemyPath.WaypointCount;
             nextIndex++)
        {
            Vector3 segmentStart = _routeStartPosition;

            if (nextIndex > 0 &&
                !_enemyPath.TryGetWaypointPosition(
                    nextIndex - 1, out segmentStart))
            {
                return false;
            }

            if (!_enemyPath.TryGetWaypointPosition(
                nextIndex, out Vector3 segmentEnd))
            {
                return false;
            }

            // 두 웨이포인트 사이의 실제 NavMesh 경로 조회
            bool hasRoute = NavMesh.CalculatePath(
                segmentStart,
                segmentEnd,
                filter,
                _returnRoutePath);

            if (!hasRoute ||
                _returnRoutePath.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            Vector3[] corners = _returnRoutePath.corners;

            // 시작점과 끝점이 같은 짧은 구간도 후보로 검사
            int edgeCount = Mathf.Max(1, corners.Length - 1);

            for (int edgeIndex = 0;
                 edgeIndex < edgeCount;
                 edgeIndex++)
            {
                Vector3 edgeStart = corners.Length > 0
                    ? corners[edgeIndex]
                    : segmentEnd;

                Vector3 edgeEnd = corners.Length > 1
                    ? corners[edgeIndex + 1]
                    : edgeStart;

                Vector3 closestPoint = GetClosestPointOnSegment(
                    transform.position,
                    edgeStart,
                    edgeEnd);

                if (!TryGetReturnAccessDistance(
                    closestPoint, out float accessDistance))
                {
                    continue;
                }

                // 거의 같은 거리라면 먼저 나온 구간을 유지
                // 겹치는 지점에서 불필요하게 먼 번호를 고르지 않음
                if (accessDistance >= bestDistance - 0.01f)
                {
                    continue;
                }

                bestDistance = accessDistance;

                // 선택한 구간의 진행 방향 끝점
                waypointIndex = nextIndex;
                hasCandidate = true;
            }
        }

        return hasCandidate;
    }

    private Vector3 GetClosestPointOnSegment(
    Vector3 position,
    Vector3 start,
    Vector3 end)
    {
        Vector3 segment = end - start;

        if (segment.sqrMagnitude <= 0.0001f)
        {
            return start;
        }

        // 높이도 포함하여 경사와 다른 층의 구간을 구분
        float progress = Vector3.Dot(
            position - start,
            segment) / segment.sqrMagnitude;

        progress = Mathf.Clamp01(progress);

        return start + segment * progress;
    }

    private bool TryGetReturnAccessDistance(
        Vector3 point,
        out float distance)
    {
        distance = 0f;

        bool hasPath = _navMeshAgent.CalculatePath(
            point,
            _returnAccessPath);

        if (!hasPath ||
            _returnAccessPath.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        // 벽 너머의 직선거리가 아닌 실제 접근 경로 길이 비교
        Vector3 previousPosition = transform.position;
        Vector3[] corners = _returnAccessPath.corners;

        foreach (Vector3 corner in corners)
        {
            distance += Vector3.Distance(previousPosition, corner);
            previousPosition = corner;
        }

        distance += Vector3.Distance(previousPosition, point);

        return true;
    }

    public Vector3 Velocity
    {
        get
        {
            if (!isActiveAndEnabled ||
                !_isMoving ||
                _isMovementPaused ||
                _hasPassed)
            {
                return Vector3.zero;
            }

            if (_isFlying)
            {
                return _flyingVelocity;
            }

            if (_navMeshAgent.isActiveAndEnabled &&
                _navMeshAgent.isOnNavMesh)
            {
                return _navMeshAgent.velocity;
            }

            return Vector3.zero;
        }
    }

    // 전투·넉백 초기화 전에 이동 방식부터 준비
    public bool TryPrepareMovement(EnemyData data)
    {
        if (!isActiveAndEnabled ||
            _isMovementPrepared ||
            data == null ||
            !data.IsValid)
        {
            Debug.LogError("적 이동 준비 상태와 데이터를 확인하세요.", this);
            return false;
        }

        if (data.IsFlying &&
            data.ReactionMode != EnemyReactionMode.IgnorePlayer)
        {
            Debug.LogError(
                "현재 비행형의 반격 방식은 IgnorePlayer로 설정하세요.",
                this);

            return false;
        }

        _isFlying = data.IsFlying;
        _flightHeight = data.FlightHeight;
        _flightRotationSpeed = data.ReactionRotationSpeed;

        if (_isFlying)
        {
            _navMeshAgent.enabled = false;
        }
        else if (!_navMeshAgent.isActiveAndEnabled ||
                 !_navMeshAgent.isOnNavMesh)
        {
            Debug.LogError(
                "지상 적은 활성 NavMeshAgent와 이동 가능한 NavMesh가 필요합니다.",
                this);

            return false;
        }

        _isMovementPrepared = true;
        return true;
    }

    private void UpdateFlyingMovement()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
        {
            return;
        }

        Vector3 previousPosition = transform.position;

        transform.position = Vector3.MoveTowards(
            previousPosition,
            _currentWaypointPosition,
            _moveSpeed * _moveSpeedMultiplier * deltaTime);

        _flyingVelocity =
            (transform.position - previousPosition) / deltaTime;

        // 몸체는 수평 방향으로 회전
        Vector3 direction = _flyingVelocity;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(direction, Vector3.up);

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _flightRotationSpeed * deltaTime);
        }

        float arrivalDistance = _arrivalDistance;

        bool isDefenseGoalApproach =
            _usesDefenseGoal &&
            _defenseGoal != null &&
            _currentWaypointIndex == _enemyPath.WaypointCount - 1;

        if (isDefenseGoalApproach)
        {
            // 공격 거리가 짧아도 범위 밖에서 도착 처리되지 않도록 제한
            arrivalDistance = Mathf.Min(
                _arrivalDistance,
                _defenseGoalAttackRange * 0.25f);
        }

        if (Vector3.Distance(
            transform.position,
            _currentWaypointPosition) > arrivalDistance)
        {
            return;
        }

        if (TryHandleDefenseGoalArrival())
        {
            return;
        }

        _currentWaypointIndex++;

        if (_currentWaypointIndex >= _enemyPath.WaypointCount)
        {
            HandlePassed();
        }
        else
        {
            MoveToCurrentWaypoint();
        }
    }

    public bool CanChaseTarget(Transform target)
    {
        if (target == null ||
            _isFlying ||
            !isActiveAndEnabled ||
            _hasPassed ||
            !_navMeshAgent.isActiveAndEnabled ||
            !_navMeshAgent.isOnNavMesh)
        {
            return false;
        }

        // 같은 대상의 경로를 매 프레임 다시 계산하지 않음
        if (_lastCheckedChaseTarget == target &&
            Time.timeAsDouble < _nextChaseTargetCheckTime)
        {
            return _hasReachableChaseTarget;
        }

        _lastCheckedChaseTarget = target;
        _nextChaseTargetCheckTime =
            Time.timeAsDouble + _chaseRepathInterval;

        _hasReachableChaseTarget = false;

        NavMeshQueryFilter filter = new NavMeshQueryFilter
        {
            agentTypeID = _navMeshAgent.agentTypeID,
            areaMask = _navMeshAgent.areaMask
        };

        if (!NavMesh.SamplePosition(
            target.position,
            out NavMeshHit hit,
            _chaseSampleDistance,
            filter))
        {
            return false;
        }

        // 머리 위·공중 위치를 먼 아래의 바닥으로 간주하지 않음
        float heightDifference =
            Mathf.Abs(target.position.y - hit.position.y);

        if (heightDifference > _maxChaseTargetHeight)
        {
            return false;
        }

        _chaseGroundPosition = hit.position;

        bool hasPath = _navMeshAgent.CalculatePath(
            _chaseGroundPosition,
            _chaseValidationPath);

        _hasReachableChaseTarget =
            hasPath &&
            _chaseValidationPath.status ==
                NavMeshPathStatus.PathComplete;

        return _hasReachableChaseTarget;
    }

    // 이동 초기화 전에 스포너에서 호출
    // 목표가 없으면 기존 통과 방식을 사용
    public bool TrySetDefenseGoal(Stage2DefenseGoal defenseGoal)
    {
        if (_isMoving || _hasPassed)
        {
            return false;
        }

        _defenseGoal = defenseGoal;
        _usesDefenseGoal = defenseGoal != null;
        _hasReachedDefenseGoal = false;

        return true;
    }

    // 마지막 웨이포인트에서 Stage 2 도착 처리
    private bool TryHandleDefenseGoalArrival()
    {
        bool shouldHandleGoal =
            _usesDefenseGoal &&
            _currentWaypointIndex == _enemyPath.WaypointCount - 1;

        if (!shouldHandleGoal)
        {
            return false;
        }

        if (!TrySetMovementPaused(true))
        {
            StopWithError("방어 목표 도착 후 이동 정지에 실패했습니다.");

            // 실패해도 기존 통과·제거 처리로 넘어가지 않음
            return true;
        }

        _hasReachedDefenseGoal = true;

        // 통과 이벤트를 보내거나 적을 제거하지 않음
        Debug.Log("방어 목표 도착: 공격 대기 상태", this);

        return true;
    }
}