using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사운드 데이터 맵 - 설정 검사와 식별자별 조회 구성
/// </summary>
[CreateAssetMenu( fileName = "SoundDataMap", menuName = "Sound/Data Map" )]
public class SoundDataMap : ScriptableObject
{
    [SerializeField] SoundData[] _sounds = new SoundData[0]; //등록 사운드

    /// <summary>
    /// 설정 검사 후 런타임 조회표 구성
    /// </summary>
    /// <param name="lookup">구성한 조회표</param>
    /// <returns>구성 성공 여부</returns>
    public bool TryBuild ( out Dictionary<SoundId, SoundData> lookup )
    {
        lookup = new Dictionary<SoundId, SoundData>( );

        foreach ( SoundData data in _sounds )
        {
            if ( data == null || data.Id == SoundId.None )
            {
                Debug.LogError( "사운드 설정 또는 ID가 비어 있습니다.", this );
                return false;
            }

            if ( lookup.ContainsKey( data.Id ) == true )
            {
                Debug.LogError( $"사운드 ID 중복: {data.Id}", this );
                return false;
            }

            if ( data.Clips.Length == 0 )
            {
                Debug.LogError( $"사운드 음원 누락: {data.Id}", this );
                return false;
            }

            foreach ( AudioClip clip in data.Clips )
            {
                if ( clip != null )
                    continue;

                Debug.LogError( $"사운드 후보에 빈 음원 존재: {data.Id}", this );
                return false;
            }

            if ( ( data.Type == SoundType.Bgm || data.Type == SoundType.LoopSfx ) && data.Clips.Length != 1 )
            {
                Debug.LogError( $"BGM/반복 효과음은 음원 하나를 지정해야 합니다: {data.Id}", this );
                return false;
            }

            lookup.Add( data.Id, data );
        }

        return true;
    }
}
