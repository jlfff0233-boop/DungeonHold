using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로비 등장 적 목록 제공
/// </summary>
public abstract class LobbyEnemySource : ScriptableObject
{
    /// <summary>
    /// 선택 구성의 적 조회
    /// </summary>
    /// <param name="mapId">맵</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="enemies">등장 적</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>조회 여부</returns>
    public abstract bool TryGetEnemies (
        string mapId, MissionDifficulty difficulty,
        out IReadOnlyList<EnemyData> enemies, out string reason );
}

/// <summary>
/// 미해금 적 표시 정책
/// </summary>
public enum LobbyEnemyPolicy
{
    Unset,        //정책 미지정
    HideLocked,   //이름과 이미지 숨김
    RevealLocked  //이름과 이미지 공개
}
