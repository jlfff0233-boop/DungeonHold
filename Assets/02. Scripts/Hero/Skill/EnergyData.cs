using UnityEngine;

/// <summary>
/// 에너지 설정 데이터 - 최대 에너지와 기본 공격 충전량
/// </summary>
[CreateAssetMenu ( fileName = "EnergyData" , menuName = "Hero/EnergyData" )]
public class EnergyData : ScriptableObject
{
    [Header ( "----- 에너지 -----" )]
    [SerializeField] float _maxEnergy = 100f;       //최대 에너지
    [SerializeField] float _basicAttackGain = 5f;       //기본 공격 유효 적중 시 충전량

    /// <summary>
    /// 최대 에너지
    /// </summary>
    public float MaxEnergy => _maxEnergy;

    /// <summary>
    /// 기본 공격 유효 적중 충전량
    /// </summary>
    public float BasicAttackGain => _basicAttackGain;
}