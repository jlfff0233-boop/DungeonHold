using UnityEngine;

/// <summary>
/// 콤보 설정 - 유지시간과 보상 자료 맵 연결
/// </summary>
[CreateAssetMenu ( fileName = "ComboData", menuName = "Combo/ComboData" )]
public class ComboData : ScriptableObject
{
    [Header ( "----- 콤보 -----" )]
    [SerializeField] float _duration = 4f;                  //마지막 처치 이후 유지시간
    [SerializeField] ComboRewardDataMap _rewardDataMap;     //단계 보상 자료 맵

    /// <summary>
    /// 콤보 유지시간
    /// </summary>
    public float Duration => _duration;

    /// <summary>
    /// 단계 보상 자료 맵
    /// </summary>
    public ComboRewardDataMap RewardDataMap => _rewardDataMap;

    /// <summary>
    /// 양수 유지시간과 보상 자료 맵 검사
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    public bool IsValid ( )
    {
        return _duration > 0f
            && _rewardDataMap != null
            && _rewardDataMap.IsValid ( );
    }
}
