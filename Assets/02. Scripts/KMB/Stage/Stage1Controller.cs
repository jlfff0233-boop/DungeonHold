using UnityEngine;
using System.Collections.Generic;

public class Stage1Controller : MonoBehaviour, IStageEndLink
{
    [SerializeField] private Stage1Data _stageData;
    [SerializeField] private StageScoreData _scoreData;
    [SerializeField] private WaveController _waveController;

    [SerializeField] private LJPlay.PlayScene _playerScene;

    // 결과를 확정한 원래 도전 번호
    private int _endStageId;
    private bool _wasAbandoned;

    public string Error { get; private set; }

    // 종료를 예약한 프레임의 남은 사건 전달이 끝나기를 기다림
    private bool _isWaitingEndEventCompletion;
    private int _endRequestFrame;

    // 승패 판정과 실제 종료 정리 완료를 구분
    public bool HasCompletedStageEnd { get; private set; }
    public bool HasStageEndFailed { get; private set; }

    public bool HasFinished { get; private set; }
    public bool HasFailed { get; private set; }

    public bool HasSucceeded => HasFinished && !HasFailed && !_wasAbandoned;

    public int StarCount { get; private set; }
    public int FinalPassedCount { get; private set; }

    // 최종 점수 계산에 사용한 팀 처치 수
    public int FinalKillCount { get; private set; }

    // LJ 정산부와 결과 화면에 제공할 확정 점수
    public int FinalScore { get; private set; }

    // 정상 0점과 아직 계산되지 않은 상태를 구분
    public bool HasFinalScore { get; private set; }

    public string FailureReason { get; private set; }

    // 여러 웨이브에 걸쳐 사용한 전투 시간
    public float ElapsedCombatTime { get; private set; }

    // 이번 도전의 남은 전투 시간
    public float RemainingCombatTime { get; private set; }

    private void Awake()
    {
        bool hasValidSettings =
            _stageData != null &&
            _stageData.IsValid &&
            _scoreData != null &&
            _scoreData.IsValid &&
            _waveController != null &&
            _playerScene != null &&
            _playerScene.Hero != null;

        if (!hasValidSettings)
        {
            Debug.LogError(
            "Stage1Data, StageScoreData, " +
            "WaveController와 TestScene 연결을 확인하세요.",
            this);

            enabled = false;
            return;
        }

        ElapsedCombatTime = 0f;
        RemainingCombatTime = _stageData.CombatTimeLimit;
    }

    private bool TryFinalizeClearScore()
    {
        // 한 번 확정한 점수는 정산 재시도에서도 유지
        if (HasFinalScore)
        {
            return true;
        }

        if (!HasSucceeded)
        {
            return false;
        }

        int killCount = _waveController.TeamKillCount;

        bool wasCalculated =
            _scoreData.TryCalculateStage1ClearScore(
                killCount,
                FinalPassedCount,
                out int score);

        if (!wasCalculated)
        {
            if (!HasStageEndFailed)
            {
                Debug.LogError(
                    "Stage 1 점수 계산 실패: " +
                    "점수 설정과 최종 처치·통과 기록을 확인하세요.",
                    this);
            }

            HasStageEndFailed = true;
            return false;
        }

        FinalKillCount = killCount;
        FinalScore = score;
        HasFinalScore = true;

        Debug.Log(
            $"Stage 1 점수 확정: 도전 {_endStageId}, " +
            $"팀 처치 {FinalKillCount}명, " +
            $"통과 {FinalPassedCount}명, " +
            $"최종 점수 {FinalScore}점",
            this);

        return true;
    }

    private void LateUpdate()
    {
        if (!_isWaitingEndEventCompletion ||
            HasCompletedStageEnd)
        {
            return;
        }

        if (!_playerScene.isActiveAndEnabled)
        {
            return;
        }

        if (!_playerScene.IsAwaitingEndEvents)
        {
            _isWaitingEndEventCompletion = false;
            return;
        }

        if (HasPendingEndEvents())
        {
            return;
        }

        if (!TryCompleteEndEvents(_endStageId) && !HasStageEndFailed)
        {
            Debug.LogError(Error, this);
            HasStageEndFailed = true;
        }
    }

    private bool HasPendingEndEvents()
    {
        return Time.frameCount <= _endRequestFrame ||
            !_waveController.HasStoppedForStageResult ||
            _waveController.IsDispatchingEnemyDeath ||
            _playerScene.Missions == null ||
            _playerScene.Missions.IsProcessing;
    }

    public bool TryCompleteEndEvents(int stageId)
    {
        Error = null;

        if (!isActiveAndEnabled ||
            _playerScene == null ||
            !_playerScene.isActiveAndEnabled ||
            !_isWaitingEndEventCompletion ||
            stageId <= 0 ||
            stageId != _endStageId ||
            stageId != _playerScene.CurrentStageId ||
            !_playerScene.IsAwaitingEndEvents)
        {
            Error = "마지막 사건 전달을 기다리는 Stage 1 도전인지 확인하세요.";
            return false;
        }

        if (HasPendingEndEvents())
        {
            Error = "Stage 1의 마지막 전투 사건을 아직 처리 중입니다.";
            return false;
        }

        if (HasSucceeded && !TryFinalizeClearScore())
        {
            Error = "Stage 1 클리어 점수를 확정하지 못했습니다.";
            return false;
        }

        bool wasAccepted = HasSucceeded
            ? _playerScene.CompleteEndEvents(stageId, FinalScore)
            : _playerScene.CompleteEndEvents(stageId);

        if (!wasAccepted)
        {
            Error = $"Stage 1 마지막 사건 전달 완료 접수 실패. 도전 {stageId}";
            return false;
        }

        _isWaitingEndEventCompletion = false;
        HasStageEndFailed = false;

        string scoreText = HasFinalScore
            ? $"{FinalScore}점"
            : "해당 없음";

        Debug.Log(
            $"Stage 1 마지막 사건 전달 완료: " +
            $"도전 {_endStageId}, 점수 {scoreText}",
            this);

        return true;
    }

    private void OnEnable()
    {
        _playerScene.OnStageEndProcessed +=
            HandleStageEndProcessed;

        _playerScene.Hero.OnReviveExhausted +=
            HandlePlayerReviveExhausted;

        _waveController.OnPassedEnemyCountChanged +=
            HandlePassedEnemyCountChanged;

        _waveController.OnAllWavesCompleted +=
            HandleAllWavesCompleted;

        // 다시 활성화되었을 때도 현재 진행 결과 확인
        HandlePassedEnemyCountChanged(
            _waveController.PassedEnemyCount);

        if (_waveController.HasCompletedAllWaves &&
            !_waveController.HasStoppedForStageResult)
        {
            HandleAllWavesCompleted();
        }
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

        // 게임 시간 기준이므로 Time.timeScale이 0이면 감소하지 않음
        RemainingCombatTime = Mathf.Max(
            0f,
            RemainingCombatTime - Time.deltaTime);

        // 제한시간을 넘은 부분은 누적하지 않음
        ElapsedCombatTime +=
            previousRemainingTime - RemainingCombatTime;

        if (RemainingCombatTime > 0f)
        {
            return;
        }

        // 기존 실패·적 중단·종료 정산 흐름을 그대로 사용
        FinishStage(
            true,
            _waveController.PassedEnemyCount,
            0,
            "제한시간 초과");
    }

    private void HandlePassedEnemyCountChanged(int passedCount)
    {
        if (HasFinished)
        {
            return;
        }

        if (passedCount >= _stageData.PassLimit)
        {
            FinishStage(true, passedCount, 0);
        }
    }

    private void HandleAllWavesCompleted()
    {
        if (HasFinished ||
            _waveController.HasStoppedForStageResult)
        {
            return;
        }

        int passedCount = _waveController.PassedEnemyCount;

        // 마지막 통과와 전체 웨이브 완료가 겹쳐도 실패 우선
        if (passedCount >= _stageData.PassLimit)
        {
            FinishStage(true, passedCount, 0);
            return;
        }

        // 모든 웨이브를 완료했으므로 최소 1성
        int starCount = 1;

        // 높은 별 조건부터 검사
        if (passedCount <= _stageData.ThreeStarMaxPassed)
        {
            starCount = 3;
        }
        else if (passedCount <= _stageData.TwoStarMaxPassed)
        {
            starCount = 2;
        }

        FinishStage(false, passedCount, starCount);
    }

    private void FinishStage(
    bool hasFailed,
    int passedCount,
    int starCount,
    string failureReason = "적 통과 한도 도달")
    {
        if (HasFinished)
        {
            return;
        }

        HasFinished = true;
        HasFailed = hasFailed;
        FinalPassedCount = passedCount;
        StarCount = starCount;

        FailureReason = hasFailed
                        ? failureReason
                        : string.Empty;

        // 이번 판에서 보관한 번호로만 종료 요청
        _endStageId = _waveController.StageId;

        // 먼저 종료를 예약해 슬롯·설치 요청을 차단
        // 실제 정산은 LJ의 LateUpdate에서 처리
        TryRequestStageEnd();

        // 남은 적과 웨이브 진행 중단
        _waveController.StopForStageResult();

        if (HasFailed)
        {
            Debug.Log(
                $"스테이지 1 실패: 원인 {FailureReason}, " +
                $"통과 {FinalPassedCount}명 / " +
                $"한도 {_stageData.PassLimit}명, 별 {StarCount}개",
                this);
        }
        else
        {
            Debug.Log(
                $"스테이지 1 성공: " +
                $"통과 {FinalPassedCount}명, 별 {StarCount}개",
                this);
        }
    }

    public bool TryEndStage(int stageId)
    {
        Error = null;

        if (!isActiveAndEnabled ||
            _playerScene == null ||
            stageId <= 0 ||
            _playerScene.CurrentStageId != stageId ||
            !_playerScene.IsStageReady ||
            _playerScene.IsEndingStage)
        {
            Error = "현재 진행 중인 Stage 1 도전인지 확인하세요.";
            return false;
        }

        if (HasFinished)
        {
            if (stageId == _endStageId && TryRequestStageEnd())
                return true;

            Error = "이미 확정된 Stage 1 결과를 접수하지 못했습니다.";
            return false;
        }

        int passedCount = _waveController.PassedEnemyCount;
        var result = new MissionStageResult(
            stageId,
            MissionOutcome.Abandoned,
            passedCount,
            targetWasDamaged: false,
            defenseRecordValid: true,
            reasonId: "stage_abandoned",
            starCount: 0);

        if (!_playerScene.RequestEndStage(result, discardPendingCurrency: false))
        {
            Error = "Stage 1 포기 종료가 접수되지 않았습니다.";
            return false;
        }

        _wasAbandoned = true;
        HasFinished = true;
        FinalPassedCount = passedCount;
        StarCount = 0;

        _endStageId = stageId;
        _endRequestFrame = Time.frameCount;
        _isWaitingEndEventCompletion = true;

        _waveController.StopForStageResult();
        return true;
    }

    // 확정된 실제 결과로 최초 종료를 예약
    // 예약 이후 저장·정산 실패는 LJ의 RetryEndStage 경로 사용
    public bool TryRequestStageEnd()
    {
        if (!isActiveAndEnabled ||
            !HasFinished ||
            _wasAbandoned ||
            HasCompletedStageEnd ||
            _playerScene.IsEndingStage)
        {
            return false;
        }

        // 이 컨트롤러는 통과 수로 방어하는 스테이지 1 전용
        // 목표 HP 기록이 없는 상태를 무피해로 잘못 전달하지 않음
        if (_playerScene.Missions.StageContext.DefenseRule
            != MissionDefenseRule.NoPass)
        {
            HasStageEndFailed = true;

            Debug.LogError(
                "Stage1Controller는 NoPass 방어 규칙으로 연결하세요.",
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
            FinalPassedCount,
            targetWasDamaged: false,
            defenseRecordValid: true,
            reasonId: HasFailed ? FailureReason : "stage_clear",
            starCount: StarCount);

        bool wasAccepted =
            _endStageId > 0 &&
            _playerScene.RequestEndStage(
                result,
                false,
                collectionEnemies);

        HasStageEndFailed = !wasAccepted;

        if (!wasAccepted)
        {
            Debug.LogError(
                $"정식 스테이지 종료 예약 실패: 도전 {_endStageId}. " +
                "도전 상태와 도감 대상 등록을 확인하세요.",
                this);

            return false;
        }

        _endRequestFrame = Time.frameCount;
        _isWaitingEndEventCompletion = true;

        Debug.Log(
            $"정식 종료 예약 완료: 도전 {_endStageId}, " +
            $"결과 {result.Outcome}, 별 {result.StarCount}, " +
            $"통과 {result.PassedCount}, " +
            $"도감 대상 {collectionEnemies.Count}종",
            this);

        return true;
    }

    private void HandleStageEndProcessed(
        int stageId,
        bool abandon,
        bool succeeded)
    {
        // 이번에 확정한 결과의 종료 처리만 확인
        if (!HasFinished ||
            stageId != _endStageId ||
            abandon)
        {
            return;
        }

        HasStageEndFailed = !succeeded;

        if (!succeeded)
        {
            string retryMethod = _playerScene.IsStageCleanupPending
                ? "TryCompleteStageCleanup"
                : "RetryEndStage";

            Debug.LogError(
                $"도전 {_endStageId} 종료 처리 실패: " +
                $"위치 {_playerScene.LastEndFailure}. " +
                $"진행을 재개하지 않고 {retryMethod}로 재시도해야 합니다.",
                this);

            return;
        }

        HasCompletedStageEnd = true;

        string scoreText = HasFinalScore
            ? $"{FinalScore}점"
            : "해당 없음";

        Debug.Log(
            $"스테이지 종료 정리 완료: 도전 {stageId}, " +
            $"결과 {(_wasAbandoned ? "포기" : HasFailed ? "실패" : "성공")}, " +
            $"별 {StarCount}개, 점수 {scoreText}",
            this);

        if (_playerScene.TryGetEndResult(
        stageId,
        out MissionStageResult endResult))
        {
            Debug.Log(
                $"LJ 종료 결과 확인: 도전 {stageId}, " +
                $"결과 {endResult.Outcome}, " +
                $"별 {endResult.StarCount}, " +
                $"통과 {endResult.PassedCount}",
                this);
        }
    }

    private void HandlePlayerReviveExhausted(DamageInfo damageInfo)
    {
        if (HasFinished ||
            !_playerScene.IsStageReady ||
            _playerScene.IsEndingStage)
        {
            return;
        }

        FinishStage(
            true,
            _waveController.PassedEnemyCount,
            0,
            "플레이어 부활 횟수 소진");
    }

    private void OnDisable()
    {
        if (_waveController != null)
        {
            _waveController.OnPassedEnemyCountChanged -=
                HandlePassedEnemyCountChanged;

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

    // 실제 처치 목록이 아닌 스테이지 구성의 원본 적 종류 수집
    private bool TryCollectCollectionEnemies(
        out List<EnemyData> enemies)
    {
        enemies = new List<EnemyData>();

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
                        $"도감 대상 확인 실패: " +
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



    [ContextMenu("테스트/전투 시간 확인")]
    private void PrintCombatTimeForTest()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Debug.Log(
            $"전투 시간 확인: 누적 {ElapsedCombatTime:F1}초, " +
            $"남은 {RemainingCombatTime:F1}초, " +
            $"웨이브 전투 중 {_waveController.IsWaveRunning}, " +
            $"결과 확정 {HasFinished}",
            this);
    }
}
