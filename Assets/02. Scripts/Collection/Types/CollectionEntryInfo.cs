using UnityEngine;

/// <summary>
/// 도감 표시 정보 - 잠금 상태별 공개 정보 구성
/// </summary>
public class CollectionEntryInfo
{
    string _enemyId;    //고정 적 ID
    EnemyCollectionKind _kind;    //일반 적/보스 분류

    bool _isUnlocked;    //해금 여부
    bool _isNew;    //미확인 신규 여부

    string _displayName;    //잠금 시 ???
    Sprite _representativeImage;    //해금 후 대표 이미지
    string _feature;    //해금 후 특징
    string _attackDescription;    //해금 후 공격 형태
    string _backgroundStory;    //해금 후 배경 스토리

    float _maxHp;    //원본 기본 최대 HP
    float _damage;    //원본 기본 피해량

    float _moveSpeed;    //효과 적용 전 기본 이동속도
    bool _isStunImmune;    //기절 면역
    float _slowEffectMultiplier;    //감속 강도 배율
    float _maxSlowRate;    //최대 감속률

    /// <summary>
    /// 고정 적 ID
    /// </summary>
    public string EnemyId => _enemyId;

    /// <summary>
    /// 일반 적/보스 분류
    /// </summary>
    public EnemyCollectionKind Kind => _kind;

    /// <summary>
    /// 해금 여부
    /// </summary>
    public bool IsUnlocked => _isUnlocked;

    /// <summary>
    /// 미확인 신규 여부
    /// </summary>
    public bool IsNew => _isNew;

    /// <summary>
    /// 표시 이름, 잠금 시 ???
    /// </summary>
    public string DisplayName => _displayName;

    /// <summary>
    /// 해금 후 대표 이미지
    /// </summary>
    public Sprite RepresentativeImage => _representativeImage;

    /// <summary>
    /// 해금 후 특징 설명
    /// </summary>
    public string Feature => _feature;

    /// <summary>
    /// 해금 후 공격 형태 설명
    /// </summary>
    public string AttackDescription => _attackDescription;

    /// <summary>
    /// 해금 후 배경 스토리
    /// </summary>
    public string BackgroundStory => _backgroundStory;

    /// <summary>
    /// 해금 후 원본 기본 최대 HP
    /// </summary>
    public float MaxHp => _maxHp;

    /// <summary>
    /// 해금 후 원본 기본 피해량
    /// </summary>
    public float Damage => _damage;

    /// <summary>
    /// 해금 후 효과 적용 전 기본 이동속도
    /// </summary>
    public float MoveSpeed => _moveSpeed;

    /// <summary>
    /// 해금 후 기절 면역 여부
    /// </summary>
    public bool IsStunImmune => _isStunImmune;

    /// <summary>
    /// 해금 후 감속 강도 배율
    /// </summary>
    public float SlowEffectMultiplier => _slowEffectMultiplier;

    /// <summary>
    /// 해금 후 최대 감속률
    /// </summary>
    public float MaxSlowRate => _maxSlowRate;

    /// <summary>
    /// 잠긴 항목의 상세 정보를 채우지 않고 표시 값 구성
    /// </summary>
    /// <param name="data">검사된 도감 설정</param>
    /// <param name="isUnlocked">현재 슬롯의 해금 여부</param>
    /// <param name="isNew">현재 슬롯의 신규 여부</param>
    public CollectionEntryInfo (
        EnemyCollectionData data,
        bool isUnlocked,
        bool isNew )
    {
        _enemyId = data.EnemyId;
        _kind = data.Kind;
        _isUnlocked = isUnlocked;
        _isNew = isNew;
        _displayName = "???";
        _feature = string.Empty;
        _attackDescription = string.Empty;
        _backgroundStory = string.Empty;

        if ( isUnlocked == false )
            return;

        //원본 SO의 기본값을 사용하며
        //현재 HP나 감속된 런타임 수치를 참조하지 않음
        EnemyData enemy = data.Enemy;

        _displayName = data.DisplayName;
        _representativeImage = data.RepresentativeImage;
        _feature = data.Feature;
        _attackDescription = data.AttackDescription;
        _backgroundStory = data.BackgroundStory;

        _maxHp = enemy.CombatStatsData.MaxHp;
        _damage = enemy.AttackData.RawDamage;
        _moveSpeed = enemy.MoveSpeed;

        _isStunImmune = enemy.StatusResistance.IsStunImmune;
        _slowEffectMultiplier = enemy.StatusResistance.SlowEffectMultiplier;
        _maxSlowRate = enemy.StatusResistance.MaxSlowRate;
    }
}
