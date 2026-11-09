using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 도감 맵 - 설정 검사와 고정 ID/원본 적 매핑
/// </summary>
[CreateAssetMenu( fileName = "EnemyCollectionDataMap", menuName = "Collection/Data Map" )]
public class EnemyCollectionDataMap : ScriptableObject
{
    [SerializeField]
    EnemyCollectionData [ ] _entries =
        Array.Empty<EnemyCollectionData>( );    //표시 순서의 도감 항목

    /// <summary>
    /// 고정 ID 조회(아이디, 적 도감 데이터)
    /// </summary>
    Dictionary<string, EnemyCollectionData> _byId = new( );

    /// <summary>
    /// 원본 적 조회(적 데이터, 적 도감 데이터)
    /// </summary>
    Dictionary<EnemyData, EnemyCollectionData> _byEnemy = new( );

    /// <summary>
    /// 입력 목록
    /// </summary>
    public IReadOnlyList<EnemyCollectionData> Entries => _entries;

    /// <summary>
    /// 초기화에서 설정을 한 번 검사하고 조회 사전 구성
    /// </summary>
    /// <returns>전체 설정 유효 여부</returns>
    public bool Validate ()
    {
        Dictionary<string, EnemyCollectionData> byId = new( );
        Dictionary<EnemyData, EnemyCollectionData> byEnemy = new( );

        for ( int i = 0; i < _entries.Length; i++ )
        {
            EnemyCollectionData data = _entries [ i ];

            if ( data == null )
            {
                Debug.LogError( $"도감 맵의 {i}번 항목이 비어 있습니다.", this );
                return false;
            }

            if ( string.IsNullOrWhiteSpace( data.EnemyId ) == true )
            {
                Debug.LogError( $"도감 고정 ID가 없습니다: {data.name}", data );
                return false;
            }

            if ( data.Enemy == null )
            {
                Debug.LogError( $"도감 원본 적이 연결되지 않았습니다: {data.EnemyId}", data );
                return false;
            }

            if ( data.RewardAmount < 0 )
            {
                Debug.LogError( $"도감 보상이 음수입니다: {data.EnemyId}, 보상={data.RewardAmount}", data );
                return false;
            }

            EnemyData enemy = data.Enemy;

            //원본 내부 전투 설정은 EnemyData의 책임이며
            //도감에서는 ID와 원본의 일대일 매핑만 구성
            if ( byId.TryAdd( data.EnemyId, data ) == false )
            {
                Debug.LogError( $"도감 ID가 중복됩니다: {data.EnemyId}", data );
                return false;
            }

            if ( byEnemy.TryAdd( enemy, data ) == false )
            {
                Debug.LogError( $"같은 원본 적이 여러 도감 항목에 연결됐습니다: {enemy.name}", data );
                return false;
            }
        }

        //일부 항목만 검사한 사전을
        //실행 중에 사용하지 않도록 전체 성공 후 교체
        _byId = byId;
        _byEnemy = byEnemy;

        return true;
    }

    /// <summary>
    /// 고정 적 ID로 도감 설정 조회
    /// </summary>
    /// <param name="enemyId">조회할 고정 ID</param>
    /// <param name="data">등록된 설정</param>
    /// <returns>등록 여부</returns>
    public bool TryGet (
        string enemyId,
        out EnemyCollectionData data )
    {
        return _byId.TryGetValue( enemyId, out data );
    }

    /// <summary>
    /// 종료 결과의 원본 적을 도감 설정으로 매핑
    /// </summary>
    /// <param name="enemy">스테이지 구성에 포함된 원본 적</param>
    /// <param name="data">등록된 설정</param>
    /// <returns>등록 여부</returns>
    public bool TryGet (
        EnemyData enemy,
        out EnemyCollectionData data )
    {
        return _byEnemy.TryGetValue( enemy, out data );
    }
}
