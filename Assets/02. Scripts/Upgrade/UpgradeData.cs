using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 강화 공통 데이터 - 저장 ID와 단계별 구매 정보
/// </summary>
public abstract class UpgradeData : ScriptableObject
{
    [SerializeField] string _upgradeId;    //저장용 고정 ID

    /// <summary>
    /// 저장용 고정 ID
    /// </summary>
    public string UpgradeId => _upgradeId;

    /// <summary>
    /// 최대 구매 단계
    /// </summary>
    public int MaxLevel => GetLevels ( ).Count;

    /// <summary>
    /// 지정 단계의 구매 비용 조회
    /// </summary>
    /// <param name="level">1부터 시작하는 구매 단계</param>
    /// <returns>해당 단계의 비용</returns>
    public int GetPrice ( int level )
    {
        return GetLevels ( ) [ level - 1 ].Price;
    }

    /// <summary>
    /// 강화 데이터의 단계 목록 제공
    /// </summary>
    /// <returns>Lv1부터 시작하는 단계 목록</returns>
    protected abstract IReadOnlyList<UpgradeLevelData> GetLevels ( );

    /// <summary>
    /// 강화 대상 검사
    /// </summary>
    /// <returns>대상 설정 사용 가능 여부</returns>
    protected virtual bool ValidateTarget ( )
    {
        return true;
    }

    /// <summary>
    /// 초기화 시 ID, 대상과 단계별 설정 검사
    /// </summary>
    /// <param name="error">잘못된 설정 설명</param>
    /// <returns>전체 설정 사용 가능 여부</returns>
    public bool Validate ( out string error )
    {
        error = null;

        if ( string.IsNullOrWhiteSpace ( _upgradeId ) == true )
        {
            error = "고정 ID가 필요합니다.";
            return false;
        }

        if ( ValidateTarget ( ) == false )
        {
            error = "강화 대상 설정을 확인하세요.";
            return false;
        }

        IReadOnlyList<UpgradeLevelData> levels = GetLevels ( );

        if ( levels == null || levels.Count == 0 )
        {
            error = "강화 단계가 필요합니다.";
            return false;
        }

        for ( int i = 0 ; i < levels.Count ; i++ )
        {
            UpgradeLevelData level = levels [ i ];

            if ( level == null || level.Price <= 0
                || level.ValidateEffect ( ) == false )
            {
                error = $"Lv{i + 1} 비용 또는 효과 설정을 확인하세요.";
                return false;
            }
        }

        return true;
    }
}
