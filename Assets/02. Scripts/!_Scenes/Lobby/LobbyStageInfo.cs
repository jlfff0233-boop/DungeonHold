using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 정보 - 표시와 난이도 설정
/// </summary>
[Serializable]
public class LobbyStageInfo
{
    [SerializeField] string _mapId;                 //맵 ID
    [SerializeField] string _displayName;           //표시 이름
    [SerializeField] string _description;           //맵 설명
    [SerializeField] Sprite _preview;               //미리보기
    [SerializeField]
    MissionDifficulty [ ] _difficulties =
    {
        MissionDifficulty.Normal
    };                                              //지원 난이도

    /// <summary>
    /// 맵 ID
    /// </summary>
    public string MapId => _mapId;

    /// <summary>
    /// 표시 이름
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// 맵 설명
    /// </summary>
    public string Description => _description;

    /// <summary>
    /// 미리보기
    /// </summary>
    public Sprite Preview => _preview;

    /// <summary>
    /// 지원 난이도
    /// </summary>
    public IReadOnlyList<MissionDifficulty> Difficulties => _difficulties;

    /// <summary>
    /// Inspector 편집용 기본 구성
    /// </summary>
    public LobbyStageInfo ()
    {
    }

    /// <summary>
    /// 선택 정보 구성
    /// </summary>
    /// <param name="mapId">맵 ID</param>
    /// <param name="displayName">표시 이름</param>
    /// <param name="description">설명</param>
    /// <param name="preview">보존할 이미지</param>
    /// <param name="difficulties">지원 난이도</param>
    public LobbyStageInfo ( string mapId, string displayName, string description,
        Sprite preview, MissionDifficulty [ ] difficulties )
    {
        _mapId = mapId;
        _displayName = displayName;
        _description = description;
        _preview = preview;
        _difficulties = difficulties;
    }

    /// <summary>
    /// 난이도 지원 확인
    /// </summary>
    /// <param name="difficulty">난이도</param>
    /// <returns>지원 여부</returns>
    public bool Supports ( MissionDifficulty difficulty )
    {
        foreach ( MissionDifficulty supported in _difficulties )
        {
            if ( supported == difficulty )
                return true;
        }

        return false;
    }
}
