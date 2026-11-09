using UnityEngine;

/// <summary>
/// 활 궁극기 데이터 - 화살비 생성과 시전 거리/타격 주기 설정
/// </summary>
[CreateAssetMenu (
    fileName = "BowUltimateData" , menuName = "Hero/BowUltimateData" )]
public class BowUltimateData : SkillData
{
    [Header ( "----- 화살비 -----" )]
    [SerializeField] ArrowRainArea _areaPrefab;    //화살비 영역 프리팹
    [SerializeField] float _maxCastDistance = 30f;    //사용 주체 기준 최대 시전 거리
    [SerializeField] int _tickCount = 6;    //전체 타격 횟수
    [SerializeField] float _tickInterval = 0.5f;    //타격 간격

    /// <summary>
    /// 화살비 영역 프리팹
    /// </summary>
    public ArrowRainArea AreaPrefab => _areaPrefab;

    /// <summary>
    /// 사용 주체 기준 최대 시전 거리
    /// </summary>
    public float MaxCastDistance => _maxCastDistance;

    /// <summary>
    /// 전체 타격 횟수
    /// </summary>
    public int TickCount => _tickCount;

    /// <summary>
    /// 타격 간격
    /// </summary>
    public float TickInterval => _tickInterval;
}
