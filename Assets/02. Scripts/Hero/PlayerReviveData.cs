using UnityEngine;

/// <summary>
/// 플레이어 부활 데이터 - 자동 부활 시간, 체력 비율, 횟수 설정
/// </summary>
[CreateAssetMenu( fileName = "PlayerReviveData", menuName = "Hero/PlayerReviveData" )]
public class PlayerReviveData : ScriptableObject
{
    [SerializeField] float _delay = 5f;       //자동 부활 대기 시간
    [SerializeField] float _hpRate = 0.5f;    //최대 체력 대비 부활 체력 비율
    [SerializeField] int _maxCount = 2;       //스테이지당 자동 부활 최대 횟수

    /// <summary>
    /// 자동 부활 대기 시간
    /// </summary>
    public float Delay => _delay;

    /// <summary>
    /// 최대 체력 대비 부활 체력 비율
    /// </summary>
    public float HpRate => _hpRate;

    /// <summary>
    /// 스테이지당 자동 부활 최대 횟수
    /// </summary>
    public int MaxCount => _maxCount;
}
