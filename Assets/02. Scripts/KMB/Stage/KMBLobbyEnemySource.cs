using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "KMB_LobbyEnemySource",
    menuName = "KMB/Lobby Enemy Source")]
public class KMBLobbyEnemySource : LobbyEnemySource
{
    [SerializeField] private Stage1Data _stage1NormalData;

    public override bool TryGetEnemies(
        string mapId,
        MissionDifficulty difficulty,
        out IReadOnlyList<EnemyData> enemies,
        out string reason)
    {
        enemies = Array.Empty<EnemyData>();

        if (mapId != "stage_1" || difficulty != MissionDifficulty.Normal)
        {
            reason = $"등장 적 조회를 지원하지 않는 구성입니다. 맵 {mapId}, 난이도 {difficulty}";
            return false;
        }

        if (_stage1NormalData == null || !_stage1NormalData.IsValid)
        {
            reason = "Stage 1 보통 난이도의 웨이브 데이터를 확인하세요.";
            return false;
        }

        var result = new List<EnemyData>();
        var seen = new HashSet<EnemyData>();

        for (int waveIndex = 0; waveIndex < _stage1NormalData.WaveCount; waveIndex++)
        {
            WaveData wave = _stage1NormalData.GetWave(waveIndex);

            for (int groupIndex = 0; groupIndex < wave.SpawnGroupCount; groupIndex++)
            {
                EnemyMovement prefab = wave.GetSpawnGroup(groupIndex).EnemyPrefab;
                if (!prefab.TryGetComponent(out EnemyCombat combat) || combat.Data == null)
                {
                    reason = $"웨이브 {waveIndex + 1}, 스폰 그룹 {groupIndex + 1}의 EnemyData가 누락됐습니다.";
                    return false;
                }

                if (seen.Add(combat.Data))
                    result.Add(combat.Data);
            }
        }

        enemies = result;
        reason = null;
        return true;
    }
}