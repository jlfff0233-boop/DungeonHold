using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 이동속도 강화 데이터 - 단계별 비용과 효과 설정
/// </summary>
[CreateAssetMenu( fileName = "PlayerMoveSpeedUpgradeData", menuName = "Upgrade/Player/MoveSpeed" )]
public class PlayerMoveSpeedUpgradeData : UpgradeData
{
    /// <summary>
    /// 강화 단계 - 이동속도 증가율 설정
    /// </summary>
    [Serializable]
    class LevelData : UpgradeLevelData
    {
        [SerializeField] float _moveSpeedBonus;    //이동속도 증가율

        /// <summary>
        /// 이동속도 증가율
        /// </summary>
        public float MoveSpeedBonus => _moveSpeedBonus;

        /// <inheritdoc />
        public override bool ValidateEffect ()
        {
            return _moveSpeedBonus >= 0f;
        }
    }

    [SerializeField] LevelData[] _levels;    //Lv1부터의 강화 단계

    /// <inheritdoc />
    protected override IReadOnlyList<UpgradeLevelData> GetLevels ()
    {
        return _levels;
    }

    /// <summary>
    /// 현재 단계의 이동속도 증가율 조회
    /// </summary>
    /// <param name="level">현재 단계, 미강화는 0</param>
    /// <returns>기본 이동속도 대비 증가율</returns>
    public float GetMoveSpeedBonus ( int level )
    {
        return level == 0 ? 0f : _levels[level - 1].MoveSpeedBonus;
    }
}
