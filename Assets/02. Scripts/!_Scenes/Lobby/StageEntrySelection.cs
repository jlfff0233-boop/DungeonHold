/// <summary>
/// 입장 준비 결과
/// </summary>
public enum EntryPreparationResult
{
    Success,            //입장 준비 완료
    Unavailable,        //현재 요청 불가
    WeaponNotSelected,  //선택 무기 없음
    SaveFailed          //미저장 진행 처리 실패
}

/// <summary>
/// 입장 선택 - 확정한 맵/난이도/무기
/// </summary>
public struct StageEntrySelection
{
    string _mapId;                      //맵 ID
    MissionDifficulty _difficulty;     //난이도
    WeaponType _weaponType;             //무기 타입

    /// <summary>
    /// 맵 ID
    /// </summary>
    public string MapId => _mapId;

    /// <summary>
    /// 난이도
    /// </summary>
    public MissionDifficulty Difficulty => _difficulty;

    /// <summary>
    /// 무기 타입
    /// </summary>
    public WeaponType WeaponType => _weaponType;

    /// <summary>
    /// 입장 선택 보관
    /// </summary>
    /// <param name="mapId">맵 ID</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="weaponType">무기 타입</param>
    public StageEntrySelection (
        string mapId, MissionDifficulty difficulty, WeaponType weaponType )
    {
        _mapId = mapId;
        _difficulty = difficulty;
        _weaponType = weaponType;
    }
}
