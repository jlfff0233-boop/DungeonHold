using UnityEngine;
using UnityEngine.AI;

public class EnemySpawner : MonoBehaviour
{
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private EnemyPath _enemyPath;

    [SerializeField] private Hero _player;

    // Stage 2 스포너에서만 연결
    [SerializeField] private Stage2DefenseGoal _defenseGoal;

    // 적을 생성하되, 등록하기 전에는 이동을 시작하지 않음
    // 웨이브 데이터가 지정한 프리팹으로 생성
    public bool TryCreateEnemy(
        EnemyMovement enemyPrefab,
        out EnemyMovement enemy)
    {
        enemy = null;

        bool hasRequiredReferences =
            enemyPrefab != null &&
            _spawnPoint != null &&
            _enemyPath != null &&
            _player != null;

        if (!hasRequiredReferences)
        {
            Debug.LogError(
                "적 프리팹, 스폰 지점, 이동 경로와 플레이어를 확인하세요.",
                this);

            return false;
        }

        bool canCreate =
            isActiveAndEnabled &&
            enemyPrefab.gameObject.activeSelf &&
            enemyPrefab.enabled &&
            _enemyPath.WaypointCount > 0;

        if (!canCreate)
        {
            Debug.LogError(
                "스포너와 적 프리팹의 활성 상태 및 이동 경로를 확인하세요.",
                this);

            return false;
        }

        EnemyCombat prefabCombat = enemyPrefab.GetComponent<EnemyCombat>();

        EnemyData data = prefabCombat != null
            ? prefabCombat.Data
            : null;

        if (data == null || !data.IsValid)
        {
            Debug.LogError("적 프리팹의 EnemyData를 확인하세요.", this);
            return false;
        }

        NavMeshAgent prefabAgent =
            enemyPrefab.GetComponent<NavMeshAgent>();

        if (data.IsFlying &&
            (prefabAgent == null || prefabAgent.enabled))
        {
            Debug.LogError(
                "비행형 프리팹의 NavMeshAgent 컴포넌트 체크를 꺼주세요.",
                this);

            return false;
        }

        Vector3 spawnPosition = _spawnPoint.position;

        if (data.IsFlying)
        {
            spawnPosition += Vector3.up * data.FlightHeight;
        }

        enemy = Instantiate(
            enemyPrefab,
            spawnPosition,
            _spawnPoint.rotation);

        return true;
    }

    // 전투와 플레이어 감지가 준비된 뒤 이동 시작
    public bool TryInitializeEnemy(
    EnemyMovement enemy,
    int stageId,
    long lifeId)
    {
        if (!isActiveAndEnabled ||
            enemy == null ||
            _enemyPath == null ||
            _player == null)
        {
            return false;
        }

        EnemyCombat enemyCombat =
            enemy.GetComponent<EnemyCombat>();

        EnemyPlayerDetector playerDetector =
            enemy.GetComponent<EnemyPlayerDetector>();

        EnemyAttack enemyAttack =
            enemy.GetComponent<EnemyAttack>();

        EnemyReaction enemyReaction =
            enemy.GetComponent<EnemyReaction>();

        if (enemyCombat == null ||
            playerDetector == null ||
            enemyAttack == null ||
            enemyReaction == null)
        {
            Debug.LogError(
                "적 프리팹의 전투·감지·공격·반격 컴포넌트를 확인하세요.",
                enemy);

            return false;
        }

        EnemyData data = enemyCombat.Data;

        // 넉백 모듈이 이동 가능 여부를 검사하기 전에 준비
        if (!enemy.TryPrepareMovement(data))
        {
            return false;
        }

        if (!enemyCombat.TryInitialize())
        {
            return false;
        }

        // 전투 초기화가 끝난 뒤, 이동 전에 보상 정보 기록
        if (!enemyCombat.TryInitializeReward(stageId, lifeId))
        {
            return false;
        }

        // 지상 적마다 회피 우선순위를 분산
        // 공통으로 발급된 생명 번호를 사용하므로
        // 서로 다른 스포너에서 생성돼도 같은 순서로 배정
        if (!data.IsFlying)
        {
            NavMeshAgent agent =
                enemy.GetComponent<NavMeshAgent>();

            // 40~79 범위에서 순환 배정
            // 숫자가 낮은 적이 높은 우선순위를 가짐
            int priority = 40 + (int)((lifeId - 1) % 40);

            agent.avoidancePriority = priority;
        }

        if (!enemyAttack.TryInitialize(data.AttackData))
        {
            return false;
        }

        if (!playerDetector.TryInitialize(_player, data))
        {
            return false;
        }

        if (!enemyReaction.TryInitialize(data))
        {
            return false;
        }

        // 이동 시작 전에 이번 스폰의 도착 방식 전달
        if (!enemy.TrySetDefenseGoal(_defenseGoal))
        {
            return false;
        }

        // 모든 준비가 끝난 뒤 이동 시작
        return enemy.TryInitialize(_enemyPath, data);
    }
}