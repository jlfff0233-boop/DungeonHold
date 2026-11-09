using UnityEngine;

/// <summary>
/// 콤보 보상 자료 맵 - 처치 수 순서의 단계 설정 목록
/// </summary>
[CreateAssetMenu ( fileName = "ComboRewardDataMap", menuName = "Combo/ComboRewardDataMap" )]
public class ComboRewardDataMap : ScriptableObject
{
    [Header ( "----- 단계 보상 목록 -----" )]
    [SerializeField] ComboRewardData[] _rewards = new ComboRewardData[0];    //처치 수 오름차순 단계 설정

    /// <summary>
    /// 보상 단계 수
    /// </summary>
    public int Count => _rewards.Length;

    /// <summary>
    /// 지정 순서의 보상 설정 조회
    /// </summary>
    /// <param name="index">단계 순서</param>
    /// <returns>단계 설정</returns>
    public ComboRewardData GetReward ( int index )
    {
        return _rewards[index];
    }

    /// <summary>
    /// 단계 참조, 처치 수 순서와 보상액 검사
    /// </summary>
    /// <returns>설정 유효 여부</returns>
    public bool IsValid ( )
    {
        if ( _rewards == null || _rewards.Length == 0 )
            return false;

        int previousKillCount = 0;

        for ( int i = 0; i < _rewards.Length; i++ )
        {
            ComboRewardData reward = _rewards[i];

            if ( reward == null
                || reward.KillCount <= previousKillCount
                || reward.Amount < 0 )
                return false;

            previousKillCount = reward.KillCount;
        }

        return true;
    }
}
