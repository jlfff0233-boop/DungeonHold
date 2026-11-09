using UnityEngine;

/// <summary>
/// 콤보 단계 설정 - 기준 처치 수와 스테이지 재화 보상액
/// </summary>
[CreateAssetMenu ( fileName = "ComboRewardData", menuName = "Combo/ComboRewardData" )]
public class ComboRewardData : ScriptableObject
{
    [Header ( "----- 단계 보상 -----" )]
    [SerializeField] int _killCount = 5;    //단계 도달 처치 수
    [SerializeField] int _amount = 20;      //단계 스테이지 재화

    /// <summary>
    /// 단계 도달 처치 수
    /// </summary>
    public int KillCount => _killCount;

    /// <summary>
    /// 단계 스테이지 재화
    /// </summary>
    public int Amount => _amount;
}
