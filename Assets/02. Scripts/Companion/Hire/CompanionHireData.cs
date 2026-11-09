using UnityEngine;

/// <summary>
/// 동료 고용 데이터 - 생성 구성과 고용, 재고용 비용 관리
/// </summary>
[CreateAssetMenu (
    fileName = "CompanionHireData" , menuName = "Companion/CompanionHireData" )]
public class CompanionHireData : ScriptableObject
{
    [Header ( "----- 동료 -----" )]
    [SerializeField] Companion _prefab;    //활성 상태의 동료 프리팹
    [SerializeField] CompanionData _companionData;    //전투와 이동 설정

    [Header ( "----- 비용 -----" )]
    [SerializeField] int _hireCost;    //신규 고용 비용

    [Header ( "----- 재고용 -----" )]
    [SerializeField] float _rehireDelay;    //사망 판정부터 적용하는 타입별 대기시간

    /// <summary>
    /// 생성할 동료 프리팹
    /// </summary>
    public Companion Prefab => _prefab;

    /// <summary>
    /// 동료 전투와 이동 설정
    /// </summary>
    public CompanionData CompanionData => _companionData;

    /// <summary>
    /// 신규 고용 비용
    /// </summary>
    public int HireCost => _hireCost;

    /// <summary>
    /// 최초 고용과 동일한 재고용 비용
    /// </summary>
    public int RehireCost => _hireCost;

    /// <summary>
    /// 사망 판정부터 적용하는 타입별 재고용 대기시간
    /// </summary>
    public float RehireDelay => _rehireDelay;
}
