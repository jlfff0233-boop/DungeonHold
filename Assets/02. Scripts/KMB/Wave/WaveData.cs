using System;
using UnityEngine;

[Serializable]
public class WaveData
{
    // 해당 웨이브에 등장할 적 구성
    [SerializeField]
    private EnemySpawnData[] _spawnGroups =
        new EnemySpawnData[0];

    // 한 번의 생성 묶음에서 출현할 적 수
    [SerializeField, Min(1)]
    private int _spawnBatchSize = 1;

    public int SpawnBatchSize => _spawnBatchSize;

    public int SpawnGroupCount => _spawnGroups.Length;

    public bool IsValid
    {
        get
        {
            if (_spawnBatchSize < 1 || _spawnGroups == null || _spawnGroups.Length == 0)
            {
                return false;
            }

            foreach (EnemySpawnData spawnGroup in _spawnGroups)
            {
                if (spawnGroup == null || !spawnGroup.IsValid)
                {
                    return false;
                }
            }

            return true;
        }
    }

    // 유효성 검사 후 실행 코드에서 순서대로 조회
    public EnemySpawnData GetSpawnGroup(int index)
    {
        return _spawnGroups[index];
    }
}