using System.Collections.Generic;

/// <summary>
/// 로비 등장 적 조회
/// </summary>
public class LobbyEnemyQuery
{
    LobbyEnemySource _source; //목록 제공자
    EnemyCollectionDataMap _map; //검사된 도감 매핑
    CollectionProgress _collections; //현재 진행의 도감
    LobbyEnemyPolicy _policy; //표시 정책

    /// <summary>
    /// 조회 연결
    /// </summary>
    /// <param name="source">목록 제공자</param>
    /// <param name="map">도감 매핑</param>
    /// <param name="collections">현재 도감</param>
    /// <param name="policy">표시 정책</param>
    public LobbyEnemyQuery (
        LobbyEnemySource source ,
        EnemyCollectionDataMap map ,
        CollectionProgress collections ,
        LobbyEnemyPolicy policy )
    {
        _source = source;
        _map = map;
        _collections = collections;
        _policy = policy;
    }

    /// <summary>
    /// 등장 적 조회
    /// </summary>
    /// <param name="mapId">맵</param>
    /// <param name="difficulty">난이도</param>
    /// <param name="entries">표시 목록</param>
    /// <param name="reason">실패 사유</param>
    /// <returns>조회 여부</returns>
    public bool TryGetEntries (
        string mapId , MissionDifficulty difficulty ,
        out IReadOnlyList<LobbyEnemyInfo> entries ,
        out string reason )
    {
        entries = null;
        if ( _source.TryGetEnemies ( mapId , difficulty ,
            out IReadOnlyList<EnemyData> enemies , out reason ) == false )
            return false;

        //외부 목록의 미제공과 정상 빈 목록을 구분
        if ( enemies == null )
        {
            reason = "등장 적 목록이 제공되지 않았습니다.";
            return false;
        }

        HashSet<EnemyData> registered = new HashSet<EnemyData> ( );
        List<LobbyEnemyInfo> result = new List<LobbyEnemyInfo> ( );

        foreach ( EnemyData enemy in enemies )
        {
            if ( enemy == null )
            {
                reason = "등장 적 목록에 빈 참조가 있습니다.";
                return false;
            }

            //원본 순서는 유지하고 같은 적은 한 번만 표시
            if ( registered.Add ( enemy ) == false )
                continue;

            if ( _map.TryGet ( enemy , out EnemyCollectionData data ) == false )
            {
                reason = $"도감에 등록되지 않은 적입니다: {enemy.name}";
                return false;
            }

            bool isUnlocked = _collections.IsUnlocked ( data.EnemyId );
            bool canReveal = isUnlocked || _policy == LobbyEnemyPolicy.RevealLocked;
            result.Add ( new LobbyEnemyInfo ( data.EnemyId ,
                canReveal == true ? data.DisplayName : "???" ,
                canReveal == true ? data.RepresentativeImage : null , isUnlocked ) );
        }

        //전체 매핑에 성공한 목록만 공개
        entries = result.AsReadOnly ( );
        reason = null;
        return true;
    }
}
