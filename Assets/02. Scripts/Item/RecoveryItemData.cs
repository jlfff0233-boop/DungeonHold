using UnityEngine;

/// <summary>
/// 회복 아이템 데이터 - 회복량 설정과 검사
/// </summary>
public abstract class RecoveryItemData : ItemData
{
    [Header ( "----- 회복 -----" )]
    [SerializeField] float _recoveryAmount;     //회복량

    /// <summary>
    /// 회복량
    /// </summary>
    protected float RecoveryAmount => _recoveryAmount;

    /// <summary>
    /// 공통 설정과 회복량 검사
    /// </summary>
    /// <returns>설정 사용 가능 여부</returns>
    public override bool Validate ( )
    {
        if ( base.Validate ( ) == false )
            return false;

        if ( _recoveryAmount <= 0f )
        {
            Debug.LogError (
                $"회복량은 0보다 커야 합니다: {name}, 회복량={_recoveryAmount}" , this );
            return false;
        }

        return true;
    }
}
