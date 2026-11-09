using UnityEngine;

/// <summary>
/// 넉백 설정 - 강제 이동 거리와 진행 시간
/// </summary>
[CreateAssetMenu( fileName = "KnockbackData", menuName = "Combat/KnockbackData" )]
public class KnockbackData : ScriptableObject
{
    [Header( "----- 넉백 -----" )]
    [SerializeField] float _distance = 2f;      //요청 이동 거리
    [SerializeField] float _duration = 0.3f;    //이동 진행 시간

    /// <summary>
    /// 요청 이동 거리
    /// </summary>
    public float Distance => _distance;

    /// <summary>
    /// 이동 진행 시간
    /// </summary>
    public float Duration => _duration;
}
