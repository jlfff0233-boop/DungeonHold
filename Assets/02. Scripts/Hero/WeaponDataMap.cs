using UnityEngine;

/// <summary>
/// 무기 데이터 맵 - 무기 타입에 대응하는 설정 조회
/// </summary>
[CreateAssetMenu ( fileName = "WeaponDataMap" , menuName = "Hero/WeaponDataMap" )]
public class WeaponDataMap : ScriptableObject
{
    [SerializeField] WeaponData [ ] _weapons;    //사용할 무기 데이터 목록

    /// <summary>
    /// 타입에 대응하는 단일 무기 데이터 조회
    /// </summary>
    /// <param name="weaponType">조회할 무기 타입</param>
    /// <param name="weaponData">조회한 무기 데이터</param>
    /// <returns>중복 없이 데이터를 찾았는지 여부</returns>
    public bool TryGet ( WeaponType weaponType , out WeaponData weaponData )
    {
        weaponData = null;

        foreach ( WeaponData data in _weapons )
        {
            //목록의 빈 항목은 구성 오류로 처리
            if ( data == null )
                return false;

            if ( data.WeaponType != weaponType )
                continue;

            if ( weaponData != null )
            {
                weaponData = null;
                return false;
            }

            weaponData = data;
        }

        return weaponData != null;
    }
}
