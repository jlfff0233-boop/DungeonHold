using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 강화 진행도 - 현재 슬롯의 구매 단계 관리
/// </summary>
public class UpgradeProgress
{
    /// <summary>
    /// 구매 단계(강화 아이디, 레벨)
    /// </summary>
    Dictionary<string , int> _levels = new( );

    /// <summary>
    /// 강화 변경 이벤트
    /// </summary>
    public event Action OnChanged;

    /// <summary>
    /// 저장 기록을 현재 설정과 대조하여 복원
    /// </summary>
    /// <param name="records">저장 기록</param>
    /// <param name="map">강화 설정</param>
    /// <returns>현재 설정과 호환되는지 여부</returns>
    public bool Init (
        IReadOnlyList<UpgradeSaveData> records , UpgradeDataMap map )
    {
        Dictionary<string , int> levels = new( );

        foreach ( UpgradeSaveData record in records )
        {
            if ( map.TryGet ( record.UpgradeId , out UpgradeData data ) == false
                || record.Level > data.MaxLevel )
            {
                Debug.LogError ( $"저장된 강화 설정을 확인하세요: "
                    + $"ID={record.UpgradeId}, 단계={record.Level}" );
                return false;
            }

            levels.Add ( record.UpgradeId , record.Level );
        }

        _levels = levels;
        return true;
    }

    /// <summary>
    /// 현재 강화 단계 조회
    /// </summary>
    /// <param name="upgradeId">강화 ID</param>
    /// <returns>기록이 없으면 0</returns>
    public int GetLevel ( string upgradeId )
    {
        _levels.TryGetValue ( upgradeId , out int level );
        return level;
    }

    /// <summary>
    /// 저장에 성공한 강화 단계 반영
    /// </summary>
    /// <param name="upgradeId">강화 ID</param>
    /// <param name="level">저장된 단계</param>
    public void ApplySavedLevel ( string upgradeId , int level )
    {
        _levels [ upgradeId ] = level;
    }

    /// <summary>
    /// 관련 상태 반영 후 변경 통지
    /// </summary>
    public void NotifyChanged ( )
    {
        OnChanged?.Invoke ( );
    }
}
