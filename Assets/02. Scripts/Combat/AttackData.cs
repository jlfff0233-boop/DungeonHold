using UnityEngine;

/// <summary>
/// 공격 설정 데이터 - 피해량, 공격 간격, 탐색 범위 설정
/// </summary>
[CreateAssetMenu( fileName = "AttackData", menuName = "Combat/AttackData" )]
public class AttackData : ScriptableObject
{
    [Header( "----- 공격 -----" )]
    [SerializeField] float _rawDamage;         //원본 피해량
    [SerializeField] float _attackInterval;    //공격 간격

    [Header( "----- 타격 범위 -----" )]
    [SerializeField] float _radius;    //탐색 반경
    [SerializeField] float _angle;     //전체 탐색 각도

    /// <summary>
    /// 원본 피해량
    /// </summary>
    public float RawDamage => _rawDamage;

    /// <summary>
    /// 공격 간격
    /// </summary>
    public float AttackInterval => _attackInterval;

    /// <summary>
    /// 탐색 반경
    /// </summary>
    public float Radius => _radius;

    /// <summary>
    /// 전체 탐색 각도
    /// </summary>
    public float Angle => _angle;
}
