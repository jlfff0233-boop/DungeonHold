using System.Collections.Generic;
using LJTest;
using UnityEngine;

public class Stage3Controller : MonoBehaviour
{
    [SerializeField] private WaveController _waveController;
    [SerializeField] private Stage2DefenseGoal _defenseGoal;
    [SerializeField] private TestScene _playerScene;
    [SerializeField] private Stage1Data _stageData;
    [SerializeField] private StageScoreData _scoreData;
    private int? _clearScore;

    private int _endingStageId;
    private int _endRequestFrame;
    private bool _waitingForEndEvents;
    private float _elapsedCombatTime;
    private Hero _subscribedHero;

    private void OnEnable()
    {
        if (_waveController == null || _defenseGoal == null ||
            _playerScene == null || _playerScene.Hero == null || 
            _stageData == null || !_stageData.IsValid
            || _scoreData == null || !_scoreData.IsValid)
        {
            Debug.LogError("Stage3Controller의 참조와 통과 한도를 확인하세요.", this);
            enabled = false;
            return;
        }

        _waveController.OnPassedEnemyCountChanged += HandlePassedCountChanged;
        _defenseGoal.OnDepleted += HandleGoalDepleted;
        _waveController.OnAllWavesCompleted += HandleAllWavesCompleted;
        _subscribedHero = _playerScene.Hero;
        _subscribedHero.OnReviveExhausted += HandlePlayerReviveExhausted;
    }

    private void OnDisable()
    {
        if (_waveController != null)
        {
            _waveController.OnPassedEnemyCountChanged -= HandlePassedCountChanged;
            _waveController.OnAllWavesCompleted -= HandleAllWavesCompleted;

        }

        if (_defenseGoal != null)
            _defenseGoal.OnDepleted -= HandleGoalDepleted;

        if (_subscribedHero != null)
            _subscribedHero.OnReviveExhausted -= HandlePlayerReviveExhausted;

        _subscribedHero = null;
    }

    private void Update()
    {
        if (_endingStageId != 0 ||
            !_waveController.IsWaveRunning ||
            !_playerScene.IsStageReady ||
            _playerScene.IsEndingStage)
        {
            return;
        }

        _elapsedCombatTime += Time.deltaTime;

        if (_elapsedCombatTime >= _stageData.CombatTimeLimit)
            RequestDefeat("combat_time_expired", "제한시간 초과");
    }

    private void HandlePassedCountChanged(int passedCount)
    {
        if (passedCount >= _stageData.PassLimit)
            RequestDefeat("pass_limit_reached", "적 통과 한도 도달");
    }

    private void HandleGoalDepleted()
    {
        RequestDefeat("defense_goal_destroyed", "방어 목표 파괴");
    }

    private void HandlePlayerReviveExhausted(DamageInfo damageInfo)
    {
        RequestDefeat("player_revive_exhausted", "플레이어 부활 횟수 소진");
    }

    private void HandleAllWavesCompleted()
    {
        int stageId = _waveController.StageId;
        if (_endingStageId != 0 || stageId <= 0 ||
            _playerScene.CurrentStageId != stageId ||
            !_playerScene.IsStageReady || _playerScene.IsEndingStage)
        {
            return;
        }

        if (!_defenseGoal.IsAlive)
        {
            HandleGoalDepleted();
            return;
        }

        int passedCount = _waveController.PassedEnemyCount;
        if (passedCount >= _stageData.PassLimit)
        {
            HandlePassedCountChanged(passedCount);
            return;
        }

        int killCount = _waveController.TeamKillCount;
        float currentHp = _defenseGoal.CurrentHp;
        float maxHp = _defenseGoal.MaxHp;
        bool targetWasDamaged = _defenseGoal.HasTakenDamage;

        if (!_scoreData.TryCalculateStage3ClearScore(
                killCount, passedCount, currentHp, maxHp, out int score))
        {
            Debug.LogError("Stage3 클리어 점수 계산에 실패했습니다.", this);
            return;
        }

        var enemies = new HashSet<EnemyData>();
        for (int waveIndex = 0; waveIndex < _stageData.WaveCount; waveIndex++)
        {
            WaveData wave = _stageData.GetWave(waveIndex);
            for (int groupIndex = 0; groupIndex < wave.SpawnGroupCount; groupIndex++)
            {
                EnemyCombat combat = wave.GetSpawnGroup(groupIndex)
                    .EnemyPrefab.GetComponent<EnemyCombat>();

                if (combat == null || combat.Data == null)
                {
                    Debug.LogError(
                        $"Stage3 도감 데이터 누락: 웨이브 {waveIndex + 1}, 그룹 {groupIndex + 1}",
                        this);
                    return;
                }

                enemies.Add(combat.Data);
            }
        }

        int stars;
        if (passedCount == 0 && !targetWasDamaged)
            stars = 3;
        else if ((double)currentHp * 2 >= maxHp)
            stars = 2;
        else
            stars = 1;

        var result = new MissionStageResult(
            stageId,
            MissionOutcome.Victory,
            passedCount,
            targetWasDamaged: targetWasDamaged,
            defenseRecordValid: true,
            reasonId: "stage_clear",
            starCount: stars);

        if (!_playerScene.RequestEndStage(result, false, new List<EnemyData>(enemies)))
        {
            Debug.LogError("Stage3 승리 종료 예약이 거절됐습니다.", this);
            return;
        }

        _endingStageId = stageId;
        _endRequestFrame = Time.frameCount;
        _clearScore = score;
        _waitingForEndEvents = true;

        _waveController.StopForStageResult();

        Debug.Log(
            $"Stage3 승리 접수: 처치 {killCount}, 통과 {passedCount}, " +
            $"HP {currentHp:F1}/{maxHp:F1}, 별 {stars}개, {score}점",
            this);
    }

    private void RequestDefeat(string reasonId, string reason)
    {
        int stageId = _waveController.StageId;
        if (_endingStageId != 0 || stageId <= 0 ||
            _playerScene.CurrentStageId != stageId ||
            !_playerScene.IsStageReady || _playerScene.IsEndingStage)
        {
            return;
        }

        var result = new MissionStageResult(
            stageId,
            MissionOutcome.Defeat,
            _waveController.PassedEnemyCount,
            targetWasDamaged: _defenseGoal.HasTakenDamage,
            defenseRecordValid: true,
            reasonId: reasonId,
            starCount: 0);

        if (!_playerScene.RequestEndStage(result, false))
        {
            Debug.LogError($"Stage3 종료 예약이 거절됐습니다. 원인 {reason}", this);
            return;
        }

        _endingStageId = stageId;
        _endRequestFrame = Time.frameCount;
        _waitingForEndEvents = true;

        _waveController.StopForStageResult();
        Debug.Log($"Stage3 실패: {reason}", this);
    }

    private void LateUpdate()
    {
        if (!_waitingForEndEvents || Time.frameCount <= _endRequestFrame ||
            !_playerScene.IsAwaitingEndEvents ||
            _playerScene.Missions == null || _playerScene.Missions.IsProcessing)
        {
            return;
        }

        _waitingForEndEvents = false;

        bool accepted = _clearScore.HasValue
            ? _playerScene.CompleteEndEvents(_endingStageId, _clearScore.Value)
            : _playerScene.CompleteEndEvents(_endingStageId);

        if (!accepted)
            Debug.LogError("Stage3 정산 요청이 거절됐습니다.", this);
    }
}
