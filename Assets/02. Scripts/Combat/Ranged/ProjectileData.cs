using UnityEngine;

/// <summary>
/// 투사체 설정 데이터 - 생성과 비행/충돌 설정
/// </summary>
[CreateAssetMenu (
    fileName = "ProjectileData" , menuName = "Combat/ProjectileData" )]
public class ProjectileData : ScriptableObject
{
    [Header ( "----- 생성 -----" )]
    [SerializeField] Projectile _prefab;    //투사체 프리팹

    [Header ( "----- 비행 -----" )]
    [SerializeField] float _speed = 20f;    //초당 이동 거리
    [SerializeField] float _maxDistance = 30f;    //발사 후 최대 이동 거리

    [Header ( "----- 충돌 -----" )]
    [SerializeField] float _collisionRadius = 0.05f;    //이동 충돌 검사 반경
    [SerializeField] LayerMask _collisionMask;    //적 피격 영역과 지형 레이어

    [Header( "----- 사운드 -----" )]
    [SerializeField] SoundId _launchSound; //실제 발사음
    [SerializeField] SoundId _impactSound; //충돌음

    [Header( "----- VFX -----" )]
    [SerializeField] VfxEffect _launchVfx; //발사 단발 효과
    [SerializeField] VfxEffect _flightVfx; //비행 유지 효과
    [SerializeField] VfxEffect _impactVfx; //충돌 단발 효과

    public VfxEffect LaunchVfx => _launchVfx;
    public VfxEffect FlightVfx => _flightVfx;
    public VfxEffect ImpactVfx => _impactVfx;

    public SoundId LaunchSound => _launchSound;
    public SoundId ImpactSound => _impactSound;

    /// <summary>
    /// 투사체 프리팹
    /// </summary>
    public Projectile Prefab => _prefab;

    /// <summary>
    /// 초당 이동 거리
    /// </summary>
    public float Speed => _speed;

    /// <summary>
    /// 최대 이동 거리
    /// </summary>
    public float MaxDistance => _maxDistance;

    /// <summary>
    /// 충돌 검사 반경
    /// </summary>
    public float CollisionRadius => _collisionRadius;

    /// <summary>
    /// 충돌 검사 레이어
    /// </summary>
    public LayerMask CollisionMask => _collisionMask;
}
