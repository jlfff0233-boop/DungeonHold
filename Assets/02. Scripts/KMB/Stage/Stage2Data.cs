using UnityEngine;

[CreateAssetMenu(
    fileName = "Stage2Data",
    menuName = "KMB/Stage/Stage2 Data")]
public class Stage2Data : ScriptableObject
{
    // 적의 실제 공격을 받아 감소하는 방어 목표 체력
    [SerializeField, Min(1f)]
    private float _defenseGoalMaxHp = 100f;

    // 목록의 길이가 전체 웨이브 수
    [SerializeField]
    private WaveData[] _waves = new WaveData[0];

    [SerializeField, Min(0f)]
    private float _firstPreparationTime = 60f;

    [SerializeField, Min(0f)]
    private float _betweenWavePreparationTime = 30f;

    [SerializeField, Min(0.01f)]
    private float _spawnInterval = 1.5f;

    // 동시에 살아 있는 적의 최대 수
    [SerializeField, Min(1)]
    private int _maxActiveEnemies = 30;

    // 준비 시간을 제외한 전투 누적 제한시간
    [SerializeField, Min(1f)]
    private float _combatTimeLimit = 720f;

    public float DefenseGoalMaxHp => _defenseGoalMaxHp;
    public int WaveCount => _waves.Length;
    public float FirstPreparationTime => _firstPreparationTime;

    public float BetweenWavePreparationTime =>
        _betweenWavePreparationTime;

    public float SpawnInterval => _spawnInterval;
    public int MaxActiveEnemies => _maxActiveEnemies;
    public float CombatTimeLimit => _combatTimeLimit;

    public bool IsValid
    {
        get
        {
            bool hasValidSettings =
                IsPositiveFinite(_defenseGoalMaxHp) &&
                IsNonNegativeFinite(_firstPreparationTime) &&
                IsNonNegativeFinite(_betweenWavePreparationTime) &&
                IsPositiveFinite(_spawnInterval) &&
                _maxActiveEnemies > 0 &&
                IsPositiveFinite(_combatTimeLimit);

            if (!hasValidSettings)
            {
                return false;
            }

            if (_waves == null || _waves.Length == 0)
            {
                return false;
            }

            foreach (WaveData wave in _waves)
            {
                if (wave == null || !wave.IsValid)
                {
                    return false;
                }
            }

            return true;
        }
    }

    // 유효성 검사 후 실행 코드에서 웨이브 조회
    public WaveData GetWave(int index)
    {
        return _waves[index];
    }

    // 클리어 시 남은 방어 목표 HP 비율로 별 계산
    // 실패한 경우에는 이 메서드를 호출하지 않고 별 0개 처리
    public int CalculateStarCount(float currentHp, float maxHp)
    {
        if (!IsPositiveFinite(maxHp) ||
            !IsPositiveFinite(currentHp))
        {
            return 0;
        }

        if (currentHp >= maxHp)
        {
            return 3;
        }

        float hpRatio = currentHp / maxHp;

        return hpRatio >= 0.5f ? 2 : 1;
    }

    private bool IsPositiveFinite(float value)
    {
        return value > 0f && !float.IsInfinity(value);
    }

    private bool IsNonNegativeFinite(float value)
    {
        return value >= 0f && !float.IsInfinity(value);
    }
}