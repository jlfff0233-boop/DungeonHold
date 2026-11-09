using UnityEngine;
using System.Collections.Generic;
using UnityEngine;

public class Stage2Controller : MonoBehaviour
{
    [SerializeField] private WaveController _waveController;
    [SerializeField] private Stage2DefenseGoal _defenseGoal;
    [SerializeField] private LJTest.TestScene _playerScene;
    [SerializeField] private Stage2Data _stageData;
    [SerializeField] private StageScoreData _scoreData;

    private int _endStageId;
    private int _endRequestFrame;

    private bool _hasRequestedEnd;
    private bool _isWaitingEndEvents;

    public bool HasFinished { get; private set; }
    public bool HasFailed { get; private set; }
    public int StarCount { get; private set; }
    public float FinalHpRatio { get; private set; }
    // 종료 시점의 원본 HP를 보관해 점수 계산에 사용
    public float FinalCurrentHp { get; private set; }
    public float FinalMaxHp { get; private set; }
    public bool FinalTargetWasDamaged { get; private set; }

    // 최종 점수 계산에 사용한 팀 처치 수
    public int FinalKillCount { get; private set; }

    // LJ 정산부와 결과 화면에 제공할 확정 점수
    public int FinalScore { get; private set; }

    // 정상 0점과 아직 계산되지 않은 상태를 구분
    public bool HasFinalScore { get; private set; }

    // 게임 실패와 종료 정산 실패는 별도로 관리
    public bool HasCompletedStageEnd { get; private set; }
    public bool HasStageEndFailed { get; private set; }

    // 여러 웨이브에 걸쳐 사용한 전투 시간
    public float ElapsedCombatTime { get; private set; }

    // 준비 시간을 제외한 남은 전투 시간
    public float RemainingCombatTime { get; private set; }

    // 결과 확인에 사용할 실패 사유
    public string FailureReason { get; private set; }

    // 공용 종료 결과에 전달할 실패 사유 ID
    private string _failureReasonId;

    private void Awake()
    {
        bool hasValidSettings =
            _waveController != null &&
            _defenseGoal != null &&
            _playerScene != null &&
            _playerScene.Hero != null &&
            _stageData != null &&
            _stageData.IsValid &&
            _scoreData != null &&
            _scoreData.IsValid;

        if (!hasValidSettings)
        {
            Debug.LogError(
                "Stage2Controller의 WaveController, " +
                "DefenseGoal, TestScene, Stage2Data와 " +
                "StageScoreData 설정을 확인하세요.",
                this);

            enabled = false;
            return;
        }

        ElapsedCombatTime = 0f;
        RemainingCombatTime = _stageData.CombatTimeLimit;
    }

    private void Update()
    {
        bool canCountCombatTime =
            !HasFinished &&
            _waveController.IsWaveRunning &&
            _playerScene.IsStageReady &&
            !_playerScene.IsEndingStage;

        if (!canCountCombatTime)
        {
            return;
        }

        float previousRemainingTime = RemainingCombatTime;

        RemainingCombatTime = Mathf.Max(
            0f,
            RemainingCombatTime - Time.deltaTime);

        // 제한 시간을 초과한 부분은 누적하지 않음
        ElapsedCombatTime +=
            previousRemainingTime - RemainingCombatTime;

        if (RemainingCombatTime > 0f)
        {
            return;
        }

        FinishStage(
            true,
            "combat_time_expired",
            "제한시간 초과");
    }

    private void OnEnable()
    {
        _defenseGoal.OnDepleted += HandleGoalDepleted;

        _waveController.OnAllWavesCompleted +=
            HandleAllWavesCompleted;

        _playerScene.OnStageEndProcessed +=
            HandleStageEndProcessed;

        _playerScene.Hero.OnReviveExhausted +=
            HandlePlayerReviveExhausted;
    }

    private void OnDisable()
    {
        if (_defenseGoal != null)
        {
            _defenseGoal.OnDepleted -= HandleGoalDepleted;
        }

        if (_waveController != null)
        {
            _waveController.OnAllWavesCompleted -=
                HandleAllWavesCompleted;
        }

        if (_playerScene != null)
        {
            _playerScene.OnStageEndProcessed -=
                HandleStageEndProcessed;

            if (_playerScene.Hero != null)
            {
                _playerScene.Hero.OnReviveExhausted -=
                    HandlePlayerReviveExhausted;
            }
        }
    }

    private void HandlePlayerReviveExhausted(DamageInfo damageInfo)
    {
        // 마지막 사망 연출 이후 전달되는 부활 소진 알림 사용
        // 중복 종료 방지는 기존 FinishStage에서 처리
        FinishStage(
            true,
            "player_revive_exhausted",
            "플레이어 부활 횟수 소진");
    }

    private void HandleGoalDepleted()
    {
        FinishStage(
            true,
            "defense_goal_destroyed",
            "방어 목표 파괴");
    }

    private void HandleAllWavesCompleted()
    {
        // 모든 웨이브가 끝났어도 목표가 파괴됐다면 실패
        if (!_defenseGoal.IsAlive)
        {
            HandleGoalDepleted();
            return;
        }

        FinishStage(false);
    }

    private void FinishStage(
        bool hasFailed,
        string failureReasonId = null,
        string failureReason = null)
    {
        if (HasFinished ||
            !_playerScene.IsStageReady ||
            _playerScene.IsEndingStage)
        {
            return;
        }

        HasFinished = true;
        HasFailed = hasFailed;

        _failureReasonId = hasFailed ? failureReasonId : null;
        FailureReason = hasFailed ? failureReason : null;

        _endStageId = _waveController.StageId;

        // 종료 시점의 HP를 한 번 보관
        // 이후 정산 재시도에서도 같은 기록 사용
        FinalCurrentHp = _defenseGoal.CurrentHp;
        FinalMaxHp = _defenseGoal.MaxHp;

        // 비율은 로그와 결과 표시용으로 유지
        FinalHpRatio = Mathf.Clamp01(
            FinalCurrentHp / FinalMaxHp);

        FinalTargetWasDamaged = _defenseGoal.HasTakenDamage;

        StarCount = hasFailed
                ? 0
                : _stageData.CalculateStarCount(
                FinalCurrentHp,
                FinalMaxHp);

        // 기존 종료 예약과 남은 적 활동 중단 흐름 사용
        TryRequestStageEnd();
        _waveController.StopForStageResult();

        string outcome = hasFailed ? "실패" : "클리어";

        string reasonText = hasFailed
            ? $", 원인 {FailureReason}"
            : string.Empty;

        Debug.Log(
            $"스테이지 2 {outcome}: 도전 {_endStageId}, " +
            $"남은 HP {FinalHpRatio:P1}, 별 {StarCount}개" +
            reasonText,
            this);
    }

    // 종료 예약 자체가 거절됐을 때 재요청 가능
    public bool TryRequestStageEnd()
    {
        bool canRequest =
            isActiveAndEnabled &&
            HasFinished &&
            !_hasRequestedEnd &&
            !HasCompletedStageEnd &&
            _endStageId > 0 &&
            _playerScene.CurrentStageId == _endStageId &&
            !_playerScene.IsEndingStage;

        if (!canRequest)
        {
            return false;
        }

        bool hasCorrectDefenseRule =
            _playerScene.Missions != null &&
            _playerScene.Missions.StageContext.DefenseRule
                == MissionDefenseRule.NoDamage;

        if (!hasCorrectDefenseRule)
        {
            HasStageEndFailed = true;

            Debug.LogError(
                "Stage 2 TestScene의 Mission Defense Rule을 " +
                "NoDamage로 설정하세요.",
                this);

            return false;
        }

        List<EnemyData> collectionEnemies =
            new List<EnemyData>();

        if (!HasFailed &&
            !TryCollectCollectionEnemies(out collectionEnemies))
        {
            HasStageEndFailed = true;
            return false;
        }

        MissionStageResult result = new MissionStageResult(
            _endStageId,
            HasFailed ? MissionOutcome.Defeat : MissionOutcome.Victory,
            _waveController.PassedEnemyCount,
            targetWasDamaged: FinalTargetWasDamaged,
            defenseRecordValid: true,
            reasonId: HasFailed
                ? _failureReasonId
                : "stage_clear",
            starCount: StarCount);

        bool wasAccepted = _playerScene.RequestEndStage(
            result,
            false,
            collectionEnemies);

        HasStageEndFailed = !wasAccepted;

        if (!wasAccepted)
        {
            Debug.LogError(
                $"Stage 2 종료 예약 실패: 도전 {_endStageId}",
                this);

            return false;
        }

        _hasRequestedEnd = true;
        _isWaitingEndEvents = true;
        _endRequestFrame = Time.frameCount;

        Debug.Log(
            $"Stage 2 결과 접수: 도전 {_endStageId}, " +
            $"결과 {result.Outcome}, 별 {StarCount}개",
            this);

        return true;
    }

    private bool TryCollectCollectionEnemies(
    out List<EnemyData> enemies)
    {
        enemies = new List<EnemyData>();

        if (_stageData == null || !_stageData.IsValid)
        {
            Debug.LogError(
                "Stage 2 도감 대상 확인 실패: Stage2Data 설정을 확인하세요.",
                this);

            return false;
        }

        HashSet<EnemyData> registered =
            new HashSet<EnemyData>();

        for (int waveIndex = 0;
             waveIndex < _stageData.WaveCount;
             waveIndex++)
        {
            WaveData wave = _stageData.GetWave(waveIndex);

            for (int groupIndex = 0;
                 groupIndex < wave.SpawnGroupCount;
                 groupIndex++)
            {
                EnemySpawnData group =
                    wave.GetSpawnGroup(groupIndex);

                EnemyCombat combat =
                    group.EnemyPrefab.GetComponent<EnemyCombat>();

                if (combat == null || combat.Data == null)
                {
                    Debug.LogError(
                        $"Stage 2 도감 대상 확인 실패: " +
                        $"{waveIndex + 1}웨이브, 그룹 {groupIndex}",
                        this);

                    return false;
                }

                if (registered.Add(combat.Data))
                {
                    enemies.Add(combat.Data);
                }
            }
        }

        return true;
    }

    private bool TryFinalizeClearScore()
    {
        // 한 번 확정한 점수는 정산 재시도에서도 유지
        if (HasFinalScore)
        {
            return true;
        }

        if (!HasFinished || HasFailed)
        {
            return false;
        }

        int killCount = _waveController.TeamKillCount;

        bool wasCalculated =
                _scoreData.TryCalculateStage2ClearScore(
                killCount,
                FinalCurrentHp,
                FinalMaxHp,
                out int score);

        if (!wasCalculated)
        {
            if (!HasStageEndFailed)
            {
                Debug.LogError(
                    "Stage 2 점수 계산 실패: " +
                    "점수 설정과 최종 처치·HP 기록을 확인하세요.",
                    this);
            }

            HasStageEndFailed = true;
            return false;
        }

        FinalKillCount = killCount;
        FinalScore = score;
        HasFinalScore = true;

        Debug.Log(
            $"Stage 2 점수 확정: 도전 {_endStageId}, " +
            $"팀 처치 {FinalKillCount}명, " +
            $"남은 HP {FinalHpRatio:P1}, " +
            $"최종 점수 {FinalScore}점",
            this);

        return true;
    }

    private void LateUpdate()
    {
        if (!_isWaitingEndEvents ||
            HasCompletedStageEnd)
        {
            return;
        }

        // 종료 원인이 된 프레임의 남은 사건 전달을 기다림
        if (Time.frameCount <= _endRequestFrame ||
            !_playerScene.isActiveAndEnabled)
        {
            return;
        }

        if (!_playerScene.IsAwaitingEndEvents)
        {
            _isWaitingEndEvents = false;
            return;
        }

        if (_playerScene.Missions.IsProcessing)
        {
            return;
        }

        // 계산 실패를 정상 0점으로 전달하지 않음
        if (!HasFailed && !TryFinalizeClearScore())
        {
            return;
        }

        bool wasAccepted;

        if (HasFailed)
        {
            // 패배는 점수 없이 종료
            wasAccepted =
                _playerScene.CompleteEndEvents(_endStageId);
        }
        else
        {
            // 승리는 확정 점수를 포함해 종료
            wasAccepted =
                _playerScene.CompleteEndEvents(
                    _endStageId,
                    FinalScore);
        }

        if (!wasAccepted)
        {
            // 대기를 유지하며 같은 오류의 반복 출력 방지
            if (!HasStageEndFailed)
            {
                Debug.LogError(
                    $"Stage 2 마지막 사건 전달 완료 접수 실패: " +
                    $"도전 {_endStageId}",
                    this);
            }

            HasStageEndFailed = true;
            return;
        }

        _isWaitingEndEvents = false;
        HasStageEndFailed = false;

        string scoreText = HasFinalScore
            ? $"{FinalScore}점"
            : "해당 없음";

        Debug.Log(
            $"Stage 2 마지막 사건 전달 완료: " +
            $"도전 {_endStageId}, 점수 {scoreText}",
            this);
    }

    private void HandleStageEndProcessed(
        int stageId,
        bool abandon,
        bool succeeded)
    {
        if (!HasFinished ||
            stageId != _endStageId ||
            abandon)
        {
            return;
        }

        HasStageEndFailed = !succeeded;

        if (!succeeded)
        {
            string retryMethod =
                _playerScene.IsStageCleanupPending
                    ? "TryCompleteStageCleanup"
                    : "RetryEndStage";

            Debug.LogError(
                $"Stage 2 종료 처리 실패: " +
                $"{_playerScene.LastEndFailure}. " +
                $"{retryMethod}로 재시도가 필요합니다.",
                this);

            return;
        }

        HasCompletedStageEnd = true;
        _isWaitingEndEvents = false;

        string outcome = HasFailed ? "실패" : "클리어";

        string scoreText = HasFinalScore
            ? $"{FinalScore}점"
            : "해당 없음";

        Debug.Log(
            $"Stage 2 {outcome} 정산·정리 완료: " +
            $"도전 {stageId}, 별 {StarCount}개, " +
            $"점수 {scoreText}",
            this);
    }
}