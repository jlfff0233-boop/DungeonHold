using UnityEngine;

/// <summary>
/// 도감 적 분류
/// </summary>
public enum EnemyCollectionKind
{
    Normal,    //일반 적
    Boss       //보스
}

/// <summary>
/// 적 도감 데이터 - 원본 적 식별과 표시/최초 해금 보상 설정
/// </summary>
[CreateAssetMenu( fileName = "EnemyCollectionData", menuName = "Collection/Enemy" )]
public class EnemyCollectionData : ScriptableObject
{
    [Header( "----- 식별 -----" )]
    [SerializeField] string _enemyId;    //저장에 사용하는 고정 적 ID
    [SerializeField] EnemyData _enemy;    //실제 전투 설정
    [SerializeField] EnemyCollectionKind _kind;    //일반 적/보스 분류

    [Header( "----- 표시 -----" )]
    [SerializeField] string _displayName;    //해금 후 표시 이름
    [SerializeField] Sprite _representativeImage;    //3D 원본의 대표 이미지
    [SerializeField, TextArea] string _feature;    //적의 특징 설명
    [SerializeField, TextArea] string _attackDescription;    //공격 형태 설명
    [SerializeField, TextArea] string _backgroundStory;    //적의 배경 스토리

    [Header( "----- 최초 해금 -----" )]
    [SerializeField] int _rewardAmount;    //최초 해금 영구 재화, 0도 허용

    /// <summary>
    /// 저장에 사용하는 고정 적 ID
    /// </summary>
    public string EnemyId => _enemyId;

    /// <summary>
    /// 도감 항목에 대응하는 원본 적 설정
    /// </summary>
    public EnemyData Enemy => _enemy;

    /// <summary>
    /// 일반 적/보스 분류
    /// </summary>
    public EnemyCollectionKind Kind => _kind;

    /// <summary>
    /// 해금 후 표시 이름
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// 해금 후 대표 이미지
    /// </summary>
    public Sprite RepresentativeImage => _representativeImage;

    /// <summary>
    /// 적의 특징 설명
    /// </summary>
    public string Feature => _feature;

    /// <summary>
    /// 공격 형태 설명
    /// </summary>
    public string AttackDescription => _attackDescription;

    /// <summary>
    /// 적의 배경 스토리
    /// </summary>
    public string BackgroundStory => _backgroundStory ?? string.Empty;

    /// <summary>
    /// 최초 해금 영구 재화 보상
    /// </summary>
    public int RewardAmount => _rewardAmount;
}
