using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 미션 데이터 맵 - 설정 검사와 선행 평가 순서 준비
/// </summary>
[CreateAssetMenu ( fileName = "MissionDataMap" , menuName = "Mission/Data Map" )]
public class MissionDataMap : ScriptableObject
{
    [SerializeField] MissionData [ ] _missions;     //전체 미션

    /// <summary>
    /// 미션 ID별 등록 설정 조회(아이디, 미션 데이터)
    /// </summary>
    Dictionary<string , MissionData> _registeredMissions = new( );
    /// <summary>
    /// 선행 평가 순서
    /// </summary>
    List<MissionData> _ordered = new List<MissionData> ( );

    /// <summary>
    /// 선행 미션부터 평가할 순서로 정렬한 목록
    /// </summary>
    public IReadOnlyList<MissionData> OrderedMissions => _ordered.AsReadOnly ( );

    /// <summary>
    /// 설정을 검사하고 선행 미션부터 순서 준비
    /// </summary>
    /// <returns>전체 설정 유효 여부</returns>
    public bool Validate ( )
    {
        if ( _missions == null || _missions.Length == 0 )
        {
            Debug.LogError ( "미션 목록이 비어 있습니다." , this );
            return false;
        }

        //검사 중에는 임시 목록을 만들고 모든 검사가 성공한 뒤에만 실제 조회/평가 목록 교체
        Dictionary<string , MissionData> registeredMissions = new( );
        Dictionary<string , MissionKillCountData> killCounts = new( );
        HashSet<MissionKillCountData> checkedKillCounts = new HashSet<MissionKillCountData> ( );

        foreach ( MissionData data in _missions )
        {
            if ( data == null || data.Validate ( ) == false || registeredMissions.ContainsKey ( data.MissionId ) == true )
            {
                Debug.LogError ( "미션 설정 누락 또는 ID 중복을 확인하세요." , this );
                return false;
            }

            registeredMissions.Add ( data.MissionId , data );

            if ( data is KillMissionData kill )
            {
                MissionKillCountData killCount = kill.KillCountData;

                //같은 집계 SO를 여러 단계에서 참조할 수 있어 설정 검사는 참조당 한 번만 수행
                if ( checkedKillCounts.Add ( killCount ) == true && killCount.Validate ( ) == false )
                {
                    Debug.LogError ( $"처치 집계 설정 오류: {killCount.name}" , this );
                    return false;
                }

                if ( killCounts.TryGetValue (
                    killCount.KillCountId , out MissionKillCountData registered ) == true )
                {
                    //다른 SO가 같은 집계 ID를 쓰면 적/맵 범위가 달라도 저장 기록이 합쳐지므로 거절
                    if ( registered != killCount )
                    {
                        Debug.LogError ( $"처치 집계 ID 충돌: {killCount.KillCountId}" , this );
                        return false;
                    }
                }
                else
                {
                    killCounts.Add ( killCount.KillCountId , killCount );
                }
            }
        }

        //전체 ID 등록 후 선행 미션을 찾으므로 Inspector의 배열 순서에 의존하지 않음
        foreach ( MissionData data in _missions )
        {
            if ( string.IsNullOrEmpty ( data.PrerequisiteId ) == true )
                continue;

            if ( registeredMissions.TryGetValue ( data.PrerequisiteId , out MissionData prerequisite ) == false )
            {
                Debug.LogError ( $"선행 미션 누락: {data.MissionId}" , this );
                return false;
            }

            foreach ( MissionDifficulty difficulty in data.Difficulties )
            {
                if ( prerequisite.Supports ( difficulty ) == false )
                {
                    Debug.LogError ( $"선행 미션 난이도 불일치: {data.MissionId}" , this );
                    return false;
                }
            }
        }

        //선행 미션을 먼저 넣고 순환 참조를 검사하여 실행 중에는 한 번의 순회로 평가 가능
        Dictionary<string , int> visits = new( );
        List<MissionData> ordered = new List<MissionData> ( );

        foreach ( MissionData data in _missions )
        {
            if ( AppendInOrder ( data , registeredMissions , visits , ordered ) == false )
            {
                Debug.LogError ( "미션 선행 조건에 순환 참조가 있습니다." , this );
                return false;
            }
        }

        _registeredMissions = registeredMissions;
        _ordered = ordered;

        return true;
    }

    /// <summary>
    /// 선행 미션을 먼저 평가 목록에 추가
    /// </summary>
    /// <param name="data">현재 미션</param>
    /// <param name="registeredMissions">미션 ID별 등록 설정</param>
    /// <param name="visits">방문 상태</param>
    /// <param name="ordered">평가 목록</param>
    /// <returns>순환 없이 추가했는지 여부</returns>
    bool AppendInOrder (
        MissionData data ,
        Dictionary<string , MissionData> registeredMissions ,
        Dictionary<string , int> visits ,
        List<MissionData> ordered )
    {
        //1은 선행 탐색 중, 2는 목록 추가 완료이며
        //탐색 중인 미션에 재진입하면 순환 참조
        if ( visits.TryGetValue ( data.MissionId , out int state ) == true )
            return state == 2;

        visits.Add ( data.MissionId , 1 );

        if ( string.IsNullOrEmpty ( data.PrerequisiteId ) == false
            && AppendInOrder ( registeredMissions [ data.PrerequisiteId ] , registeredMissions , visits , ordered ) == false )
            return false;

        //선행 탐색을 마친 뒤 자신을 추가하여 선행 → 후행 순서를 보장
        visits [ data.MissionId ] = 2;
        ordered.Add ( data );

        return true;
    }

    /// <summary>
    /// 검사 완료한 설정에서 ID 조회
    /// </summary>
    /// <param name="missionId">미션 ID</param>
    /// <param name="data">조회 결과</param>
    /// <returns>등록 여부</returns>
    public bool TryGet ( string missionId , out MissionData data )
    {
        data = null;

        if ( string.IsNullOrEmpty ( missionId ) == true )
            return false;

        return _registeredMissions.TryGetValue ( missionId , out data );
    }

    /// <summary>
    /// 등록이 보장된 미션의 설정 조회
    /// </summary>
    /// <param name="missionId">등록된 ID</param>
    /// <returns>미션 설정</returns>
    public MissionData Get ( string missionId )
    {
        return _registeredMissions [ missionId ];
    }
}
