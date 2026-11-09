using UnityEngine;

/// <summary>
/// 회피 데이터 - 이동 거리, 지속시간, 무적과 쿨타임 설정
/// </summary>
[CreateAssetMenu( fileName = "DodgeData", menuName = "Hero/DodgeData" )]
public class DodgeData : ScriptableObject
{
    [SerializeField] float _distance = 4f;      //회피 이동 거리
    [SerializeField] float _duration = 0.5f;        //회피 지속시간
    [SerializeField] float _invulnerableDuration = 0.2f;        //시작 후 무적 시간
    [SerializeField] float _cooldown = 2f;      //시작 시 적용할 쿨타임

    /// <summary>
    /// 회피 이동 거리
    /// </summary>
    public float Distance => _distance;

    /// <summary>
    /// 회피 지속시간
    /// </summary>
    public float Duration => _duration;

    /// <summary>
    /// 회피 시작 후 무적 시간
    /// </summary>
    public float InvulnerableDuration => _invulnerableDuration;

    /// <summary>
    /// 회피 시작 시 적용할 쿨타임
    /// </summary>
    public float Cooldown => _cooldown;
}
