using UnityEngine;

/// <summary>
/// 동료 고용 데이터 맵 - 무기 타입에 대응하는 고용 설정 조회
/// </summary>
[CreateAssetMenu (
    fileName = "CompanionHireDataMap" ,
    menuName = "Companion/CompanionHireDataMap" )]
public class CompanionHireDataMap : ScriptableObject
{
    [SerializeField] CompanionHireData [ ] _companions;    //타입별 고용 설정

    /// <summary>
    /// 타입에 대응하는 단일 고용 설정 조회
    /// </summary>
    /// <param name="weaponType">조회할 무기 타입</param>
    /// <param name="hireData">조회한 고용 설정</param>
    /// <returns>중복 없이 데이터를 찾았는지 여부</returns>
    public bool TryGet (
        WeaponType weaponType , out CompanionHireData hireData )
    {
        hireData = null;

        foreach ( CompanionHireData data in _companions )
        {
            if ( data == null || data.CompanionData == null )
                return false;

            if ( data.CompanionData.WeaponType != weaponType )
                continue;

            if ( hireData != null )
            {
                hireData = null;
                return false;
            }

            hireData = data;
        }

        return hireData != null;
    }
}
