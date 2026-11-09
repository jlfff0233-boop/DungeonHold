using System;
using UnityEngine;

[Serializable]
public class EnemySpawnData
{
    // 씬의 적이 아닌 Project 창의 프리팹 연결
    [SerializeField] private EnemyMovement _enemyPrefab;

    [SerializeField, Min(1)] private int _count = 1;

    public EnemyMovement EnemyPrefab => _enemyPrefab;
    public int Count => _count;

    public bool IsValid =>
        _enemyPrefab != null &&
        _count > 0;
}