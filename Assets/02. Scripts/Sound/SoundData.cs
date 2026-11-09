using System;
using UnityEngine;

/// <summary>
/// 사운드 설정 - 식별자, 출력과 음원 후보
/// </summary>
[Serializable]
public class SoundData
{
    [SerializeField] SoundId _id; //식별자
    [SerializeField] SoundType _type; //출력 종류
    [SerializeField] AudioClip[] _clips = new AudioClip[0]; //음원 후보
    [SerializeField, Range( 0f, 1f )] float _volume = 1f; //개별 음량

    public SoundId Id => _id;
    public SoundType Type => _type;
    public AudioClip[] Clips => _clips;
    public float Volume => _volume;

    /// <summary>
    /// 음원 후보 중 하나 선택
    /// </summary>
    /// <returns>재생 음원</returns>
    public AudioClip PickClip ()
    {
        return _clips[UnityEngine.Random.Range( 0, _clips.Length )];
    }
}
