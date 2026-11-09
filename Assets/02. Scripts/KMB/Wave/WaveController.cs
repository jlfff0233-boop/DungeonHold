using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

public class WaveController : MonoBehaviour
{
    [SerializeField] private Stage1Data _stageData;

    // Stage 1 데이터와 둘 중 하나만 연결
    [SerializeField] private Stage2Data _stage2Data;

    // 각 스포너가 자신의 스폰 지점과 이동 경로를 사용
    [SerializeField]
    private EnemySpawner[] _enemySpawners =
        new EnemySpawner[0];

    private HashSet<EnemyMovement> _activeEnemies =
        new HashSet<EnemyMovement>();

    private int _passedEnemyCount;


    private int _currentWaveNumber;
    private float _remainingPreparationTime;
    private bool _isPreparing;

    // 이벤트 인자는 1부터 시작하는 웨이브 번호
    public event Action<int> OnPreparationStarted;      // 해당 웨이브의 준비 시작
    public event Action<int> OnWaveStarted;             // 해당 웨이브 시작
    public event Action<int> OnWaveCompleted;           // 생성 완료, 활성 적 0으로 해당 웨이브 완료

    // 모든 웨이브 진행 종료이며, 스테이지 클리어 판정은 별도
    public event Action OnAllWavesCompleted;            // 모든 웨이브 완료

    // 이번 스테이지에서 누적 통과한 적 수 전달
    public event Action<int> OnPassedEnemyCountChanged;

    private int _diedEnemyCount;

    // 점수에 사용하는 플레이어·동료·설치물의 누적 처치 수
    private int _teamKillCount;

    [SerializeField] private StageRewardController _stageRewards;

    [SerializeField] private LJPlay.PlayScene _playerScene;

    // 첫 전투 시작은 도전당 한 번만 실행
    private bool _hasBegunCombat;

    // 맵 번호가 아닌 이번 플레이 회차의 도전 번호
    private int _stageId;

    public int StageId => _stageId;

    // KMB WaveController들이 발급하는 생명 번호를 공유
    // 웨이브가 바뀌어도 초기화하지 않음
    private static long _lastIssuedLifeId;

    public int DiedEnemyCount => _diedEnemyCount;
    public int TeamKillCount => _teamKillCount;

    public int CurrentWaveNumber => _currentWaveNumber;
    public float RemainingPreparationTime => _remainingPreparationTime;
    public bool IsPreparing => _isPreparing;
    public bool HasCompletedAllWaves { get; private set; }

    private bool _isWaveRunning;
    private bool _hasFinishedSpawning;
    private bool _hasWaveFailed;

    public int ActiveEnemyCount => _activeEnemies.Count;
    public int PassedEnemyCount => _passedEnemyCount;
    public bool IsWaveRunning => _isWaveRunning;

    private int _enemyDeathDispatchDepth;

    public bool IsDispatchingEnemyDeath => _enemyDeathDispatchDepth > 0;
    public bool HasValidatedSetup { get; private set; }

    // 스테이지 결과에 의해 웨이브 진행이 중단되었는지 여부
    public bool HasStoppedForStageResult { get; private set; }

    // 연결된 스테이지 데이터에서 웨이브 진행 설정 조회
    private int WaveCount =>
        _stage2Data != null
            ? _stage2Data.WaveCount
            : _stageData.WaveCount;

    private float FirstPreparationTime =>
        _stage2Data != null
            ? _stage2Data.FirstPreparationTime
            : _stageData.FirstPreparationTime;

    private float BetweenWavePreparationTime =>
        _stage2Data != null
            ? _stage2Data.BetweenWavePreparationTime
            : _stageData.BetweenWavePreparationTime;

    private float SpawnInterval =>
        _stage2Data != null
            ? _stage2Data.SpawnInterval
            : _stageData.SpawnInterval;

    private int MaxActiveEnemies =>
        _stage2Data != null
            ? _stage2Data.MaxActiveEnemies
            : _stageData.MaxActiveEnemies;

    private WaveData GetWave(int index)
    {
        return _stage2Data != null
            ? _stage2Data.GetWave(index)
            : _stageData.GetWave(index);
    }

    private void Start()
    {
        bool hasStage1Data = _stageData != null;
        bool hasStage2Data = _stage2Data != null;

        // 둘 다 연결되거나 둘 다 비어 있는 설정은 거절
        if (hasStage1Data == hasStage2Data)
        {
            StopWaveWithError(
                "Stage1Data와 Stage2Data 중 하나만 연결하세요.");

            return;
        }

        bool hasValidStageData = hasStage2Data
            ? _stage2Data.IsValid
            : _stageData.IsValid;

        bool isValidSetting =
            _playerScene != null &&
            _playerScene.isActiveAndEnabled &&
            _enemySpawners != null &&
            _enemySpawners.Length > 0 &&
            _stageRewards != null &&
            _stageRewards == _playerScene.Rewards &&
            hasValidStageData;

        if (!isValidSetting)
        {
            StopWaveWithError(
                "TestScene, 스포너 목록, 동일한 보상 시스템과 " +
                "스테이지 데이터 설정을 확인하세요.");

            return;
        }

        // 같은 스포너를 여러 칸에 연결하면
        // 한 묶음에서 같은 지점을 선택할 수 있으므로 검사
        HashSet<EnemySpawner> registeredSpawners =
            new HashSet<EnemySpawner>();

        foreach (EnemySpawner spawner in _enemySpawners)
        {
            if (spawner == null ||
                !spawner.isActiveAndEnabled ||
                !registeredSpawners.Add(spawner))
            {
                StopWaveWithError(
                    "스포너의 누락, 비활성 또는 중복 연결을 확인하세요.");

                return;
            }
        }

        for (int waveIndex = 0;
            waveIndex < WaveCount;
            waveIndex++)
        {
            WaveData wave = GetWave(waveIndex);

            if (wave.SpawnBatchSize > _enemySpawners.Length)
            {
                StopWaveWithError(
                    $"{waveIndex + 1}웨이브의 동시 생성 수가 " +
                    "연결된 스포너 수보다 많습니다.");

                return;
            }
        }

        HasValidatedSetup = true;

        StartCoroutine(RunWavesWhenStageReady());
    }

    // 준비 중인 웨이브를 시작하며, 전투 중 요청은 거절
    public bool TryStartWave()
    {
        bool canStart =
            isActiveAndEnabled &&
            _isPreparing &&
            !_isWaveRunning &&
            !_hasWaveFailed &&
            !HasStoppedForStageResult &&
            _playerScene.isActiveAndEnabled &&
            _playerScene.IsStageReady &&
            !_playerScene.IsEndingStage &&
            _stageId > 0 &&
            _stageRewards.CurrentStageId == _stageId;

        if (!canStart)
        {
            return false;
        }

        // 첫 적을 생성하기 전에 LJ 동료 전투 준비부터 완료
        // 이후 웨이브에서는 반복 호출하지 않음
        if (!_hasBegunCombat)
        {
            bool wasStarted = _playerScene.BeginCombat(
                out CompanionHireResult result);

            if (!wasStarted)
            {
                StopWaveWithError(
                    $"첫 전투 준비에 실패했습니다: {result}");

                return false;
            }

            _hasBegunCombat = true;

            Debug.Log(
                $"첫 전투 연결 완료: 도전 {_stageId}",
                this);
        }
        else
        {
            // 첫 전투 이후에는 동료 시작을 반복하지 않고
            // 현재 웨이브가 전투 구간임을 전달
            if (!_playerScene.SetPlayPhase(_stageId, true))
            {
                StopWaveWithError(
                    $"전투 구간 전환 실패: " +
                    $"도전 {_stageId}, 웨이브 {_currentWaveNumber}");

                return false;
            }

            Debug.Log(
                $"LJ 전투 구간 전환: " +
                $"도전 {_stageId}, 웨이브 {_currentWaveNumber}",
                this);
        }

        _isPreparing = false;
        _remainingPreparationTime = 0f;
        _isWaveRunning = true;
        _hasFinishedSpawning = false;

        Debug.Log($"{_currentWaveNumber}웨이브 시작", this);

        OnWaveStarted?.Invoke(_currentWaveNumber);
        return true;
    }

    private IEnumerator SpawnEnemies(WaveData wave)
    {
        WaitForSeconds spawnWait =
            new WaitForSeconds(SpawnInterval);

        int spawnedCount = 0;
        int groupIndex = 0;
        int spawnedInGroup = 0;

        // 한 묶음 안에서 중복 없이 선택할 스포너 번호
        int[] spawnerIndices = new int[_enemySpawners.Length];

        while (groupIndex < wave.SpawnGroupCount)
        {
            if (_hasWaveFailed || HasStoppedForStageResult)
            {
                yield break;
            }

            // 첫 묶음은 바로 생성하고 이후 묶음 사이에 간격 적용
            if (spawnedCount > 0)
            {
                yield return spawnWait;
            }

            // 활성 적 상한에 도달하면 빈자리가 생길 때까지 대기
            while (ActiveEnemyCount >= MaxActiveEnemies)
            {
                if (_hasWaveFailed || HasStoppedForStageResult)
                {
                    yield break;
                }

                yield return null;
            }

            if (_hasWaveFailed || HasStoppedForStageResult)
            {
                yield break;
            }

            // 현재 여유 자리 안에서 이번 묶음의 생성 수 결정
            int availableSlots =
                MaxActiveEnemies - ActiveEnemyCount;

            int batchSize =
                Mathf.Min(wave.SpawnBatchSize, availableSlots);

            // 새 묶음에서는 모든 스포너를 다시 선택 후보로 사용
            for (int index = 0;
                 index < spawnerIndices.Length;
                 index++)
            {
                spawnerIndices[index] = index;
            }

            for (int batchIndex = 0;
                 batchIndex < batchSize &&
                 groupIndex < wave.SpawnGroupCount;
                 batchIndex++)
            {
                if (_hasWaveFailed || HasStoppedForStageResult)
                {
                    yield break;
                }

                // 아직 선택하지 않은 범위에서 하나 선택
                // 선택한 번호를 앞쪽으로 옮겨 이번 묶음의 재선택 방지
                int randomIndex = UnityEngine.Random.Range(
                    batchIndex,
                    spawnerIndices.Length);

                int selectedIndex = spawnerIndices[randomIndex];

                spawnerIndices[randomIndex] =
                    spawnerIndices[batchIndex];

                spawnerIndices[batchIndex] = selectedIndex;

                EnemySpawner spawner =
                    _enemySpawners[selectedIndex];

                if (spawner == null || !spawner.isActiveAndEnabled)
                {
                    StopWaveWithError(
                        "생성 중 스포너가 없어졌거나 비활성화되었습니다.");

                    yield break;
                }

                int stageId = _stageId;

                bool canSpawnInStage =
                    _stageRewards.isActiveAndEnabled &&
                    stageId > 0 &&
                    _stageRewards.CurrentStageId == stageId &&
                    _playerScene.isActiveAndEnabled &&
                    _playerScene.IsStageReady &&
                    !_playerScene.IsEndingStage;

                if (!canSpawnInStage)
                {
                    StopWaveWithError(
                        "적을 생성할 원래 도전이 종료되었거나 변경되었습니다.");

                    yield break;
                }

                EnemySpawnData spawnGroup =
                    wave.GetSpawnGroup(groupIndex);

                if (!spawner.TryCreateEnemy(
                    spawnGroup.EnemyPrefab,
                    out EnemyMovement enemy))
                {
                    StopWaveWithError("적 생성에 실패했습니다.");
                    yield break;
                }

                // 이동 전에 통과·사망 알림 등록
                if (!TryRegisterEnemy(enemy))
                {
                    Destroy(enemy.gameObject);

                    StopWaveWithError("적 등록에 실패했습니다.");
                    yield break;
                }

                long lifeId = ++_lastIssuedLifeId;

                // 생성에 사용한 스포너로 초기화해야
                // 해당 스포너에 연결된 이동 경로를 전달받음
                if (!spawner.TryInitializeEnemy(
                    enemy, stageId, lifeId))
                {
                    TryUnregisterEnemy(enemy);
                    Destroy(enemy.gameObject);

                    StopWaveWithError("적 초기화에 실패했습니다.");
                    yield break;
                }

                spawnedCount++;
                spawnedInGroup++;

                // 현재 종류의 수량을 채웠으면 다음 그룹으로 이동
                // 그룹이 바뀌어도 이번 생성 묶음은 계속 진행
                if (spawnedInGroup >= spawnGroup.Count)
                {
                    groupIndex++;
                    spawnedInGroup = 0;
                }
            }
        }

        _hasFinishedSpawning = true;

        Debug.Log(
            $"{_currentWaveNumber}웨이브 예정된 적 생성 완료: " +
            $"{spawnedCount}마리",
            this);

        CheckWaveCompleted();
    }

    // TestScene과 WaveController의 Start 실행 순서에 의존하지 않음
    private IEnumerator RunWavesWhenStageReady()
    {
        while (!_playerScene.IsStageReady)
        {
            if (!_playerScene.isActiveAndEnabled)
            {
                StopWaveWithError(
                    "플레이어 도전 준비 전에 TestScene이 비활성화되었습니다.");

                yield break;
            }

            yield return null;
        }

        _stageId = _stageRewards.CurrentStageId;

        if (_stageId <= 0)
        {
            StopWaveWithError(
                "플레이어 준비 후 유효한 도전 번호를 확인할 수 없습니다.");

            yield break;
        }

        // 도전 준비가 끝난 다음 첫 웨이브 준비 시간 시작
        yield return RunWaves();
    }

    // 준비, 생성, 남은 적 대기, 다음 웨이브를 순서대로 진행
    private IEnumerator RunWaves()
    {
        for (int waveIndex = 0; waveIndex < WaveCount; waveIndex++)
        {
            _currentWaveNumber = waveIndex + 1;
            // 첫 웨이브만 긴 준비 시간 적용
            _remainingPreparationTime = waveIndex == 0
            ? FirstPreparationTime
            : BetweenWavePreparationTime;
            _hasFinishedSpawning = false;
            _isPreparing = true;

            Debug.Log(
            $"{_currentWaveNumber}웨이브 준비 시작: " +
            $"{_remainingPreparationTime:F1}초",
            this);

            OnPreparationStarted?.Invoke(_currentWaveNumber);

            // 시간 만료 또는 외부 조기 시작 요청까지 대기
            while (_isPreparing && !_hasWaveFailed)
            {
                _remainingPreparationTime = Mathf.Max(
                    0f,
                    _remainingPreparationTime - Time.deltaTime);

                if (_remainingPreparationTime <= 0f)
                {
                    TryStartWave();
                }

                yield return null;
            }

            if (_hasWaveFailed)
            {
                yield break;
            }

            // 시작 요청이 여러 번 와도 생성은 이곳에서 한 번만 실행
            WaveData wave = GetWave(waveIndex);
            yield return SpawnEnemies(wave);

            if (_hasWaveFailed)
            {
                yield break;
            }

            // 생성이 끝나도 활성 적이 남아 있으면 완료까지 대기
            while (_isWaveRunning && !_hasWaveFailed)
            {
                yield return null;
            }

            if (_hasWaveFailed)
            {
                yield break;
            }
        }

        HasCompletedAllWaves = true;

        Debug.Log("전체 웨이브 종료", this);

        OnAllWavesCompleted?.Invoke();
    }

    // 통과와 사망 결과를 모두 구독
    private bool TryRegisterEnemy(EnemyMovement enemy)
    {
        if (enemy == null)
        {
            return false;
        }

        EnemyCombat enemyCombat = enemy.GetComponent<EnemyCombat>();

        if (enemyCombat == null || !_activeEnemies.Add(enemy))
        {
            return false;
        }

        enemy.OnPassed += HandleEnemyPassed;
        enemyCombat.OnDied += HandleEnemyDied;

        Debug.Log($"현재 활성 적: {ActiveEnemyCount}", this);
        return true;
    }

    private bool TryUnregisterEnemy(EnemyMovement enemy)
    {
        if (enemy == null || !_activeEnemies.Remove(enemy))
        {
            return false;
        }

        enemy.OnPassed -= HandleEnemyPassed;

        EnemyCombat enemyCombat = enemy.GetComponent<EnemyCombat>();

        if (enemyCombat != null)
        {
            enemyCombat.OnDied -= HandleEnemyDied;
        }

        Debug.Log($"현재 활성 적: {ActiveEnemyCount}", this);
        return true;
    }

    private void HandleEnemyDied(
    EnemyCombat enemyCombat,
    DamageInfo damageInfo)
    {
        EnemyMovement enemy =
            enemyCombat.GetComponent<EnemyMovement>();

        // 등록 해제에 성공한 첫 사망 알림만 처리
        if (!TryUnregisterEnemy(enemy))
        {
            return;
        }

        // 외부 알림 전에 원래 생명의 정보를 보관
        int stageId = enemyCombat.RewardStageId;
        long lifeId = enemyCombat.RewardLifeId;
        int killReward = enemyCombat.KillReward;
        EnemyData enemyData = enemyCombat.Data;

        _diedEnemyCount++;

        Debug.Log(
            $"현재 사망한 적: {DiedEnemyCount}",
            this);

        // 재화 보상과 별개로 실제 적 사망을 미션에 전달
        // 통과·단순 제거·생성 실패는 이 사망 경로를 사용하지 않음
        MissionKillContext kill = new MissionKillContext(
            stageId,
            lifeId,
            enemyData);

        MissionKillResult missionResult =
            _playerScene.RegisterMissionKill(kill);

        Debug.Log(
            $"미션 처치 접수: 도전 {stageId}, " +
            $"생명 {lifeId}, 적 {enemyData.name}, " +
            $"원인 {damageInfo.ActorType}/{damageInfo.Faction}, " +
            $"결과 {missionResult}",
            this);

        if (missionResult == MissionKillResult.AcceptedSavePending)
        {
            // 이미 집계된 처치이므로 같은 생명을 다시 전달하지 않음
            Debug.LogWarning(
                $"미션 처치는 접수됐지만 저장 대기 중입니다: " +
                $"도전 {stageId}, 생명 {lifeId}. " +
                "처치 재전달이 아닌 미션 저장 재시도가 필요합니다.",
                this);
        }

        // 기존 팀 처치 재화 지급 기준 유지
        // 화상도 원래 공격 주체와 진영 정보로 판정
        bool isTeamKill =
            damageInfo.Faction == Faction.Ally &&
            (damageInfo.ActorType == ActorType.Player ||
             damageInfo.ActorType == ActorType.Companion ||
             damageInfo.ActorType == ActorType.Installation);

        bool wasRewardAccepted = true;

        if (isTeamKill)
        {
            // 실제 처치는 보상 지급 성공 여부와 별도로 집계
            _teamKillCount++;

            wasRewardAccepted = _stageRewards.RegisterKill(
                stageId,
                lifeId,
                killReward,
                Time.timeAsDouble);

            if (wasRewardAccepted)
            {
                Debug.Log(
                    $"적 처치 접수 완료: " +
                    $"생명 {lifeId}, 기본 보상 {killReward}, " +
                    $"현재 재화 {_stageRewards.StageCurrencyBalance}, " +
                    $"미지급 요청 {_stageRewards.PendingRewardCount}",
                    this);

                if (_stageRewards.PendingRewardCount > 0)
                {
                    Debug.LogWarning(
                        "접수된 재화 보상 중 미지급 요청이 남아 있습니다.",
                        this);
                }
            }
            else
            {
                Debug.LogError(
                    $"적 처치 보상 접수 실패: " +
                    $"도전 {stageId}, 생명 {lifeId}",
                    this);
            }
        }

        // 한쪽 요청이 실패해도 다른 요청까지 생략하지 않음
        // 두 요청을 마친 뒤 진행 여부 판단
        if (missionResult == MissionKillResult.Rejected ||
            !wasRewardAccepted)
        {
            StopWaveWithError(
                $"처치 전달 상태를 확인하세요: " +
                $"미션 {missionResult}, 재화 접수 {wasRewardAccepted}");

            return;
        }

        // 마지막 적의 미션·재화 요청을 마친 뒤에만
        // 준비 구간 전환과 웨이브 완료 판정
        CheckWaveCompleted();
    }

    private void HandleEnemyPassed(EnemyMovement enemy)
    {
        if (!TryUnregisterEnemy(enemy))
        {
            return;
        }

        _passedEnemyCount++;

        Debug.Log($"현재 통과한 적: {PassedEnemyCount}", this);

        // 스테이지가 실패 여부를 먼저 판정할 수 있도록 알림
        OnPassedEnemyCountChanged?.Invoke(_passedEnemyCount);

        CheckWaveCompleted();
    }

    // 생성 완료와 활성 적 0을 함께 만족해야 웨이브 완료
    private void CheckWaveCompleted()
    {
        bool canComplete =
            _isWaveRunning &&
            !_hasWaveFailed &&
            !HasStoppedForStageResult &&
            _hasFinishedSpawning &&
            ActiveEnemyCount == 0;

        if (!canComplete)
        {
            return;
        }

        // 마지막 적의 사망·통과 처리를 마친 뒤 구간 전환
        if (!_playerScene.SetPlayPhase(_stageId, false))
        {
            StopWaveWithError(
                $"준비 구간 전환 실패: " +
                $"도전 {_stageId}, 웨이브 {_currentWaveNumber}");

            return;
        }

        // 진행 상태를 먼저 종료해 중복 완료 방지
        _isWaveRunning = false;

        Debug.Log(
            $"LJ 준비 구간 전환: " +
            $"도전 {_stageId}, 완료 웨이브 {_currentWaveNumber}",
            this);

        Debug.Log(
            $"{_currentWaveNumber}웨이브 완료",
            this);

        OnWaveCompleted?.Invoke(_currentWaveNumber);
    }

    private void StopWaveWithError(string message)
    {
        _hasWaveFailed = true;
        _isPreparing = false;
        _isWaveRunning = false;
        _remainingPreparationTime = 0f;

        Debug.Log($"웨이브 진행 중단: {message}", this);
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        if (_isPreparing || _isWaveRunning)
        {
            StopWaveWithError("WaveController가 비활성화되었습니다.");
        }
    }

    private void OnDestroy()
    {
        foreach (EnemyMovement enemy in _activeEnemies)
        {
            if (enemy == null)
            {
                continue;
            }

            enemy.OnPassed -= HandleEnemyPassed;

            EnemyCombat enemyCombat = enemy.GetComponent<EnemyCombat>();

            if (enemyCombat != null)
            {
                enemyCombat.OnDied -= HandleEnemyDied;
            }
        }

        _activeEnemies.Clear();
    }

    // 스테이지 결과에 따라 생성과 남은 적의 활동 중단
    public void StopForStageResult()
    {
        if (HasStoppedForStageResult)
        {
            return;
        }

        // 외부 알림이나 비활성화 처리 전에 진행 상태 확정
        HasStoppedForStageResult = true;
        _isPreparing = false;
        _isWaveRunning = false;
        _remainingPreparationTime = 0f;

        StopAllCoroutines();

        // 등록 해제로 원본 집합이 바뀌므로 복사본을 순회
        EnemyMovement[] remainingEnemies =
            new EnemyMovement[_activeEnemies.Count];

        _activeEnemies.CopyTo(remainingEnemies);

        foreach (EnemyMovement enemy in remainingEnemies)
        {
            if (enemy == null)
            {
                continue;
            }

            // 결과 정리는 사망이나 통과로 집계하지 않음
            if (TryUnregisterEnemy(enemy))
            {
                enemy.gameObject.SetActive(false);
            }
        }

        Debug.Log("스테이지 결과에 따라 웨이브 진행을 중단했습니다.", this);
    }
}