using UnityEngine;

/// <summary>
/// 스태프 궁극기 데이터 - 불기둥 생성과 주기 타격 설정
/// </summary>
[CreateAssetMenu ( fileName = "StaffUltimateData" , menuName = "Hero/StaffUltimateData" )]
public class StaffUltimateData : SkillData
{
    [Header ( "----- 불기둥 -----" )]
    [SerializeField] FirePillarArea _areaPrefab;    //고정 영역 프리팹
    [SerializeField] float _maxCastDistance = 30f;    //지면 선택 최대 거리
    [SerializeField] int _tickCount = 5;    //총 피해를 나누어 적용할 횟수
    [SerializeField] float _tickInterval = 1f;    //첫 타격 대기 시간과 이후 간격

    /// <summary>
    /// 고정 영역 프리팹
    /// </summary>
    public FirePillarArea AreaPrefab => _areaPrefab;

    /// <summary>
    /// 지면 선택 최대 거리
    /// </summary>
    public float MaxCastDistance => _maxCastDistance;

    /// <summary>
    /// 총 피해를 나누어 적용할 횟수
    /// </summary>
    public int TickCount => _tickCount;

    /// <summary>
    /// 첫 타격 대기 시간과 이후 간격
    /// </summary>
    public float TickInterval => _tickInterval;
}
