using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "TrapDataMap",
    menuName = "KMB/Installation/TrapDataMap")]
public class TrapDataMap : ScriptableObject
{
    [SerializeField]
    private TrapData[] _traps = new TrapData[0];

    // 사용 전에 데이터 구성과 고정 ID를 검사
    public bool Validate()
    {
        if (_traps.Length == 0)
        {
            Debug.LogError(
                "설치물 카탈로그가 비어 있습니다.",
                this);

            return false;
        }

        HashSet<string> registeredIds =
            new HashSet<string>(StringComparer.Ordinal);

        foreach (TrapData data in _traps)
        {
            if (data == null || !data.IsValid)
            {
                Debug.LogError(
                    "설치물 카탈로그의 데이터와 프리팹 설정을 확인하세요.",
                    this);

                return false;
            }

            if (string.IsNullOrWhiteSpace(data.TrapId))
            {
                Debug.LogError(
                    $"설치물 ID가 비어 있습니다: {data.name}",
                    this);

                return false;
            }

            if (!registeredIds.Add(data.TrapId))
            {
                Debug.LogError(
                    $"설치물 ID가 중복됐습니다: {data.TrapId}",
                    this);

                return false;
            }
        }

        return true;
    }

    // Validate에 성공한 카탈로그에서 고정 ID로 조회
    // 알 수 없는 ID는 다른 설치물로 대체하지 않음
    public bool TryGet(string trapId, out TrapData data)
    {
        data = null;

        if (string.IsNullOrWhiteSpace(trapId))
        {
            return false;
        }

        foreach (TrapData candidate in _traps)
        {
            if (candidate.TrapId != trapId)
            {
                continue;
            }

            data = candidate;
            return true;
        }

        return false;
    }
}