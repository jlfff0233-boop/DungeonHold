using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 데이터 맵 - 고정 식별자에 대응하는 설정 조회
/// </summary>
[CreateAssetMenu ( fileName = "ItemDataMap" , menuName = "Item/ItemDataMap" )]
public class ItemDataMap : ScriptableObject
{
    [SerializeField] ItemData [ ] _items = new ItemData [ 0 ];    //사용할 아이템 설정

    /// <summary>
    /// 아이템 설정과 식별자 중복 검사
    /// </summary>
    /// <returns>목록 사용 가능 여부</returns>
    public bool Validate ( )
    {
        HashSet<string> itemIds = new HashSet<string> ( );

        for ( int i = 0 ; i < _items.Length ; i++ )
        {
            ItemData item = _items [ i ];

            if ( item == null )
            {
                Debug.LogError ( $"아이템 목록의 {i}번 설정이 비어 있습니다." , this );
                return false;
            }

            if ( item.Validate ( ) == false )
                return false;

            if ( itemIds.Add ( item.ItemId ) == false )
            {
                Debug.LogError ( $"아이템 ID가 중복됐습니다: {item.ItemId}" , this );
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 식별자에 대응하는 아이템 조회
    /// </summary>
    /// <param name="itemId">아이템 식별자</param>
    /// <param name="itemData">조회한 설정</param>
    /// <returns>설정 조회 성공 여부</returns>
    public bool TryGet ( string itemId , out ItemData itemData )
    {
        foreach ( ItemData item in _items )
        {
            if ( item.ItemId != itemId )
                continue;

            itemData = item;
            return true;
        }

        itemData = null;
        return false;
    }
}
