using UnityEngine;

/// <summary>
/// 아이템 데이터 - 공통 설정과 효과 사용 계약
/// </summary>
public abstract class ItemData : ScriptableObject
{
    [Header( "----- 아이템 -----" )]
    [SerializeField] string _itemId;        //아이템 아이디
    [SerializeField] string _itemName;       //아이템 이름
    [SerializeField] Sprite _icon;      //아이템 아이콘
    [SerializeField] int _maxCount;     //최대 보유 수량
    [SerializeField] float _cooldown = 5f;      //재사용 대기시간
    [SerializeField] VfxEffect _useVfx; //사용 성공 단발 효과

    /// <summary>
    /// 사용 성공 효과
    /// </summary>
    public VfxEffect UseVfx => _useVfx;

    /// <summary>
    /// 아이템 아이디
    /// </summary>
    public string ItemId => _itemId;

    /// <summary>
    /// 아이템 이름
    /// </summary>
    public string ItemName => _itemName;

    /// <summary>
    /// 아이템 아이콘
    /// </summary>
    public Sprite Icon => _icon;

    /// <summary>
    /// 최대 보유 수량
    /// </summary>
    public int MaxCount => _maxCount;

    /// <summary>
    /// 재사용 대기시간
    /// </summary>
    public float Cooldown => _cooldown;

    /// <summary>
    /// 초기화 전에 공통 설정 검사
    /// </summary>
    /// <returns>설정 사용 가능 여부</returns>
    public virtual bool Validate ()
    {
        if ( string.IsNullOrWhiteSpace( _itemId ) == true )
        {
            Debug.LogError( $"빈 ID: {name}", this );
            return false;
        }

        if ( _maxCount <= 0 )
        {
            Debug.LogError(
                $"수량 확인: {name}, 수량={_maxCount}", this );
            return false;
        }

        if ( _cooldown < 0f )
        {
            Debug.LogError(
                $"쿨타임 확인: {name}, 쿨타임={_cooldown}", this );
            return false;
        }

        return true;
    }

    /// <summary>
    /// 대상에게 효과를 적용할 수 있는지 확인
    /// </summary>
    /// <param name="context">사용 대상의 모듈</param>
    /// <returns>효과 적용 가능 여부</returns>
    public abstract bool CanApply ( ItemUseContext context );

    /// <summary>
    /// 사용이 승인된 대상에게 효과 적용
    /// </summary>
    /// <param name="context">사용 대상의 모듈</param>
    /// <returns>실제 적용량</returns>
    public abstract float Apply ( ItemUseContext context );
}
