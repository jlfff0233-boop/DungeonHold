using UnityEngine;

/// <summary>
/// 로비 등장 적 표시 정보
/// </summary>
public class LobbyEnemyInfo
{
    string _enemyId; //적 ID
    string _displayName; //표시 이름
    Sprite _icon; //표시 이미지
    bool _isUnlocked; //도감 해금 여부

    /// <summary>
    /// 적 ID
    /// </summary>
    public string EnemyId => _enemyId;

    /// <summary>
    /// 표시 이름
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// 표시 이미지
    /// </summary>
    public Sprite Icon => _icon;

    /// <summary>
    /// 도감 해금 여부
    /// </summary>
    public bool IsUnlocked => _isUnlocked;

    /// <summary>
    /// 표시 정보 생성
    /// </summary>
    /// <param name="enemyId">적 ID</param>
    /// <param name="displayName">이름</param>
    /// <param name="icon">이미지</param>
    /// <param name="isUnlocked">해금 여부</param>
    public LobbyEnemyInfo (
        string enemyId, string displayName, Sprite icon, bool isUnlocked )
    {
        _enemyId = enemyId;
        _displayName = displayName;
        _icon = icon;
        _isUnlocked = isUnlocked;
    }
}
