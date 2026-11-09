using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 강화 데이터 맵 - 초기 설정 검사와 ID 및 타입별 조회
/// </summary>
[CreateAssetMenu ( fileName = "UpgradeDataMap" , menuName = "Upgrade/UpgradeDataMap" )]
public class UpgradeDataMap : ScriptableObject
{
    [SerializeField] UpgradeData [ ] _upgrades;    //전체 강화 설정

    /// <summary>
    /// 전체 강화 설정
    /// </summary>
    public IReadOnlyList<UpgradeData> Upgrades => _upgrades;

    /// <summary>
    /// 초기화에서 설정과 ID 및 대상 중복 검사
    /// </summary>
    /// <returns>전체 설정 사용 가능 여부</returns>
    public bool Validate ( )
    {
        if ( _upgrades == null )
        {
            Debug.LogError ( "강화 설정 목록이 필요합니다." , this );
            return false;
        }

        HashSet<string> ids = new HashSet<string> ( );
        HashSet<Type> players = new HashSet<Type> ( );
        HashSet<(Type, WeaponType)> weapons = new HashSet<(Type, WeaponType)> ( );
        HashSet<CompanionData> companions = new HashSet<CompanionData> ( );
        HashSet<(Type, TrapData)> installations = new HashSet<(Type, TrapData)> ( );

        for ( int i = 0 ; i < _upgrades.Length ; i++ )
        {
            UpgradeData data = _upgrades [ i ];

            if ( data == null )
            {
                Debug.LogError ( $"강화 목록의 {i}번 설정이 비어 있습니다." , this );
                return false;
            }

            if ( data.Validate ( out string error ) == false )
            {
                Debug.LogError ( $"강화 설정 오류: {data.name} / {error}" , data );
                return false;
            }

            if ( ids.Add ( data.UpgradeId ) == false )
            {
                Debug.LogError ( $"강화 ID 중복: {data.UpgradeId}" , data );
                return false;
            }

            //같은 대상의 같은 항목만 차단하며 서로 다른 강화 항목은 독립 등록
            bool unique = true;
            switch ( data )
            {
                case PlayerHpUpgradeData _:
                case PlayerMoveSpeedUpgradeData _:
                    unique = players.Add ( data.GetType ( ) );
                    break;

                case WeaponUpgradeData weapon:
                    unique = weapons.Add ( (data.GetType ( ), weapon.WeaponType) );
                    break;

                case CompanionUpgradeData companion:
                    unique = companions.Add ( companion.Companion );
                    break;

                case InstallationUpgradeData installation:
                    unique = installations.Add ( (data.GetType ( ), installation.Installation) );
                    break;
            }

            if ( unique == false )
            {
                Debug.LogError ( $"강화 대상/항목 중복: {data.name}" , data );
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 고정 ID로 강화 설정 조회
    /// </summary>
    /// <param name="upgradeId">강화 ID</param>
    /// <param name="data">조회한 설정</param>
    /// <returns>설정 존재 여부</returns>
    public bool TryGet ( string upgradeId , out UpgradeData data )
    {
        foreach ( UpgradeData candidate in _upgrades )
        {
            if ( candidate.UpgradeId != upgradeId )
                continue;

            data = candidate;
            return true;
        }

        data = null;
        return false;
    }

    /// <summary>
    /// 지정한 타입의 강화 설정 조회
    /// </summary>
    /// <typeparam name="T">조회할 강화 데이터 타입</typeparam>
    /// <returns>해당 타입의 등록된 설정</returns>
    public IEnumerable<T> GetAll<T> ( ) where T : UpgradeData
    {
        foreach ( UpgradeData data in _upgrades )
        {
            if ( data is T typedData )
                yield return typedData;
        }
    }
}
