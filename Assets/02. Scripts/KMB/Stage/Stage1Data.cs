using UnityEngine;

[CreateAssetMenu(
    fileName = "Stage1Data",
    menuName = "KMB/Stage/Stage1 Data")]
public class Stage1Data : ScriptableObject
{
    // 이 수에 도달하면 즉시 실패
    [SerializeField, Min(1)]
    private int _passLimit = 10;

    // 클리어했을 때 별을 평가하는 통과 수 기준
    [SerializeField, Min(0)]
    private int _twoStarMaxPassed = 3;

    [SerializeField, Min(0)]
    private int _threeStarMaxPassed = 0;

    // 목록의 길이가 스테이지의 웨이브 수
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

    // 준비 시간을 제외한 전체 웨이브의 전투 제한시간
    [SerializeField, Min(1f)]
    private float _combatTimeLimit = 720f;

    public float CombatTimeLimit => _combatTimeLimit;

    public int PassLimit => _passLimit;
    public int TwoStarMaxPassed => _twoStarMaxPassed;
    public int ThreeStarMaxPassed => _threeStarMaxPassed;

    public int WaveCount => _waves.Length;

    public float FirstPreparationTime => _firstPreparationTime;
    public float BetweenWavePreparationTime =>
        _betweenWavePreparationTime;

    public float SpawnInterval => _spawnInterval;

    public int MaxActiveEnemies => _maxActiveEnemies;

    public bool IsValid
    {
        get
        {
            bool hasValidRules =
                _passLimit > 0 &&
                _threeStarMaxPassed >= 0 &&
                _threeStarMaxPassed < _twoStarMaxPassed &&
                _twoStarMaxPassed < _passLimit &&
                _firstPreparationTime >= 0f &&
                !float.IsInfinity(_firstPreparationTime) &&
                _betweenWavePreparationTime >= 0f &&
                !float.IsInfinity(_betweenWavePreparationTime) &&
                _spawnInterval > 0f &&
                !float.IsInfinity(_spawnInterval) &&
                _maxActiveEnemies > 0 &&
                _combatTimeLimit > 0f &&
                !float.IsInfinity(_combatTimeLimit);

            if (!hasValidRules)
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

    // 유효성 검사 후 실행 코드에서 해당 웨이브 조회
    public WaveData GetWave(int index)
    {
        return _waves[index];
    }
}