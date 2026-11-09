using UnityEngine;

/// <summary>
/// 미션 처치 집계 설정 - 같은 적/맵 범위의 처치 미션이 참조하는 집계 정의
/// </summary>
[CreateAssetMenu ( fileName = "MissionKillCountData" , menuName = "Mission/Kill Count" )]
public class MissionKillCountData : ScriptableObject
{
    [SerializeField] string _killCountId;     //저장할 처치 집계 ID
    [SerializeField] EnemyData _enemy;      //대상 적 설정
    [SerializeField] string _mapId;     //빈 값이면 맵 공통

    /// <summary>
    /// 같은 적/맵 범위의 처치 미션이 참조하는 집계 ID
    /// </summary>
    public string KillCountId => _killCountId;

    /// <summary>
    /// 초기화에 사용할 처치 집계 설정 검사
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    public bool Validate ( )
    {
        return string.IsNullOrWhiteSpace ( _killCountId ) == false && _enemy != null;
    }

    /// <summary>
    /// 집계 대상 맵 확인
    /// </summary>
    /// <param name="mapId">현재 맵</param>
    /// <returns>집계 대상 여부</returns>
    public bool MatchesMap ( string mapId )
    {
        return string.IsNullOrEmpty ( _mapId ) || _mapId == mapId;
    }

    /// <summary>
    /// 확정 처치의 적 설정 확인
    /// </summary>
    /// <param name="enemy">적 설정</param>
    /// <returns>집계 대상 여부</returns>
    public bool MatchesEnemy ( EnemyData enemy )
    {
        return _enemy == enemy;
    }
}
