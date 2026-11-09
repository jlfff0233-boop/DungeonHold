using UnityEngine;

/// <summary>
/// 스태프 스킬 실행자 - 강화 폭발 투사체 준비와 발사
/// </summary>
public class StaffSkillExecutor : SkillExecutor
{
    /// <summary>
    /// 공유 투사체 데이터와 구분한 강화 마법탄 발사음
    /// </summary>
    public const SoundId LaunchSound = SoundId.StaffSkillLaunch;

    /// <summary>
    /// 공유 투사체 데이터와 구분한 강화 마법탄 충돌음
    /// </summary>
    public const SoundId ImpactSound = SoundId.StaffSkillImpact;

    [Header ( "----- 조준 -----" )]
    [SerializeField] AimProvider _aimProvider;    //발사 시 조준 정보 제공자

    [Header ( "----- 발사 -----" )]
    [SerializeField] Transform _launchOrigin;    //몸 쪽 발사 경로 시작점
    [SerializeField] Transform _firePoint;    //실제 발사점

    ProjectileData _projectileData;    //사용 시작 시 준비한 비행 설정 참조
    ExplosionProjectile _prefab;    //사용 시작 시 준비한 폭발 투사체 프리팹

    /// <summary>
    /// 강화 투사체 설정 검사와 발사 준비
    /// </summary>
    /// <param name="skillData">연결된 스킬 설정</param>
    /// <returns>설정 준비 성공 여부</returns>
    public override bool TryPrepare ( SkillData skillData )
    {
        StaffSkillData data = skillData as StaffSkillData;
        if ( data == null )
        {
            Debug.LogError ( "스태프 스킬 실행자에는 StaffSkillData를 연결해야 합니다." , this );
            return false;
        }

        ProjectileData projectileData = data.ProjectileData;
        ExplosionProjectile prefab = projectileData.Prefab as ExplosionProjectile;
        if ( prefab == null )
        {
            Debug.LogError ( "스태프 스킬에는 ExplosionProjectile 프리팹을 연결해야 합니다." , this );
            return false;
        }

        if ( projectileData.Speed <= 0f || projectileData.MaxDistance <= 0f
            || projectileData.CollisionRadius <= 0f )
        {
            Debug.LogError ( "폭발 투사체의 속도/거리/충돌 반경은 양수여야 합니다." , this );
            return false;
        }

        //TryUse에서 준비한 참조를 유지하고 실제 비행 수치는 발사 시 Init에서 보존
        _projectileData = projectileData;
        _prefab = prefab;
        return true;
    }

    /// <summary>
    /// 조준 방향으로 강화 투사체 발사
    /// </summary>
    /// <param name="execution">이번 스킬 실행</param>
    protected override void ExecuteAttack ( AttackExecution execution )
    {
        ProjectileData projectileData = _projectileData;
        ExplosionProjectile prefab = _prefab;
        AimInfo aim = _aimProvider.GetAim ( execution.Context.Attacker );
        Vector3 launchOrigin = _launchOrigin.position;
        Vector3 firePoint = _firePoint.position;
        Vector3 direction = aim.Point - firePoint;

        //발사점 뒤에 조준점이 있으면 역방향 발사 없이 이번 실행만 종료
        if ( Vector3.Dot ( direction , aim.Direction ) <= 0f )
        {
            execution.Complete ( );
            return;
        }

        ExplosionProjectile projectile = Instantiate (
            prefab , firePoint , Quaternion.LookRotation ( direction , Vector3.up ) );

        //Init 중 최초 충돌까지 처리 가능. 이후 완료 책임은 생성된 투사체에 있음
        projectile.SetSound( Sound );
        projectile.SetVfx( Vfx );
        projectile.Init ( execution , projectileData , launchOrigin , firePoint , direction.normalized ,
            LaunchSound , ImpactSound );
    }
}
