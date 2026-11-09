using UnityEngine;

[CreateAssetMenu(
    fileName = "EnemyData",
    menuName = "KMB/Enemy/EnemyData")]
public class EnemyData : ScriptableObject
{
    // 기존 공통 데이터를 참조하여 수치 중복 저장 방지
    [SerializeField] private CombatStatsData _combatStatsData;
    [SerializeField] private AttackData _attackData;

    [SerializeField] private float _moveSpeed = 3f;

    // 이 적을 처치했을 때 지급할 기본 스테이지 재화
    [SerializeField, Min(0)] private int _killReward = 20;

    [SerializeField] private float _reactionRotationSpeed = 720f;

    [SerializeField]
    private EnemyReactionMode _reactionMode =
    EnemyReactionMode.Limited;

    [SerializeField] private float _reactionStartDistance = 2f;
    [SerializeField] private float _reactionEndDistance = 4f;

    // 한 번의 반격에서 사용할 타격 기회 수
    [SerializeField, Min(1)]
    private int _reactionAttackCount = 1;

    // 마지막 공격 동작 종료 후 재반격 금지 시간
    [SerializeField, Min(0f)]
    private float _reactionCooldown = 3f;

    [SerializeField] private StatusResistanceData _statusResistance;

    // 일반 적 1, 탱커 0.5
    [SerializeField, Min(0.01f)]
    private float _knockbackDistanceMultiplier = 1f;

    [SerializeField]
    private EnemyMovementType _movementType =
    EnemyMovementType.Ground;

    // 지상 웨이포인트 위치에 더할 비행 높이
    [SerializeField, Min(0.1f)]
    private float _flightHeight = 3f;

    public StatusResistanceData StatusResistance => _statusResistance;

    public CombatStatsData CombatStatsData => _combatStatsData;
    public AttackData AttackData => _attackData;
    public float MoveSpeed => _moveSpeed;

    public float ReactionStartDistance => _reactionStartDistance;
    public float ReactionEndDistance => _reactionEndDistance;
    public float ReactionRotationSpeed => _reactionRotationSpeed;

    public int KillReward => _killReward;

    public EnemyReactionMode ReactionMode => _reactionMode;
    public int ReactionAttackCount => _reactionAttackCount;
    public float ReactionCooldown => _reactionCooldown;

    public float KnockbackDistanceMultiplier =>
    _knockbackDistanceMultiplier;

    public EnemyMovementType MovementType => _movementType;

    public bool IsFlying =>
        _movementType == EnemyMovementType.Flying;

    public float FlightHeight => _flightHeight;

    public bool IsValid =>
    _combatStatsData != null &&
    _attackData != null &&
    _statusResistance != null &&
    _killReward >= 0 &&
    IsPositiveFinite(_moveSpeed) &&
    IsPositiveFinite(_reactionStartDistance) &&
    IsPositiveFinite(_reactionEndDistance) &&
    _reactionEndDistance > _reactionStartDistance &&
    IsPositiveFinite(_reactionRotationSpeed) &&
    (_reactionMode != EnemyReactionMode.Limited ||
     _reactionAttackCount > 0) &&
    _reactionCooldown >= 0f &&
    !float.IsInfinity(_reactionCooldown) &&
    IsPositiveFinite(_knockbackDistanceMultiplier) &&
    (!IsFlying || IsPositiveFinite(_flightHeight));

    private bool IsPositiveFinite(float value)
    {
        return value > 0f && !float.IsInfinity(value);
    }
}