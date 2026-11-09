using UnityEngine;

[RequireComponent(typeof(EnemyCombat))]
public class EnemyPlayerDetector : MonoBehaviour
{
    private float _reactionStartDistance;
    private float _reactionEndDistance;

    private EnemyCombat _enemyCombat;
    private Hero _player;
    private Health _playerHealth;

    private bool _isInitialized;
    private bool _isInReactionRange;

    public Hero Player => _player;
    public bool IsInReactionRange => _isInReactionRange;

    public float CurrentDistance { get; private set; } =
        float.PositiveInfinity;

    private void Awake()
    {
        _enemyCombat = GetComponent<EnemyCombat>();
    }

    // 씬의 플레이어는 스포너에서 전달
    public bool TryInitialize(Hero player, EnemyData data)
    {
        bool hasValidSettings =
            !_isInitialized &&
            isActiveAndEnabled &&
            player != null &&
            data != null &&
            data.IsValid;

        if (!hasValidSettings)
        {
            Debug.LogError(
                "플레이어와 EnemyData를 확인하세요.",
                this);

            return false;
        }

        Health playerHealth = player.GetComponent<Health>();

        if (playerHealth == null)
        {
            Debug.LogError(
                "플레이어 루트에 Health가 필요합니다.",
                this);

            return false;
        }

        _player = player;
        _playerHealth = playerHealth;

        _reactionStartDistance = data.ReactionStartDistance;
        _reactionEndDistance = data.ReactionEndDistance;

        _isInitialized = true;
        return true;
    }

    private void Update()
    {
        bool hasValidTarget =
            _isInitialized &&
            _enemyCombat.CanReceiveDamage &&
            _player != null &&
            _player.isActiveAndEnabled &&
            _playerHealth != null &&
            _playerHealth.IsAlive;

        if (!hasValidTarget)
        {
            ClearDetection();
            return;
        }

        // 현재 단계에서는 두 루트 위치 사이의 직선 거리 검사
        CurrentDistance = Vector3.Distance(
            transform.position,
            _player.transform.position);

        if (!_isInReactionRange &&
            CurrentDistance <= _reactionStartDistance)
        {
            _isInReactionRange = true;

            Debug.Log(
                $"플레이어 반격 범위 진입: 적 {name}, " +
                $"거리 {CurrentDistance:F2}m",
                this);
        }
        else if (_isInReactionRange &&
                 CurrentDistance >= _reactionEndDistance)
        {
            _isInReactionRange = false;

            Debug.Log(
                $"플레이어 반격 범위 이탈: 적 {name}, " +
                $"거리 {CurrentDistance:F2}m",
                this);
        }
    }

    private void ClearDetection()
    {
        CurrentDistance = float.PositiveInfinity;

        if (!_isInReactionRange)
        {
            return;
        }

        _isInReactionRange = false;

        Debug.Log(
            $"플레이어 감지 종료: 적 {name}, 대상 또는 적이 유효하지 않습니다.",
            this);
    }

    private void OnDisable()
    {
        ClearDetection();
    }
}