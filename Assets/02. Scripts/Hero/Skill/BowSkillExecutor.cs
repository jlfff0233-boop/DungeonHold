using UnityEngine;

/// <summary>
/// 활 스킬 실행자 - 관통 화살 설정 준비와 발사
/// </summary>
public class BowSkillExecutor : SkillExecutor
{
    [Header ( "----- 디버그 -----" )]
    [SerializeField] bool _showDebugLog = true;    //발사 거절 로그 출력 여부

    [Header ( "----- 조준 -----" )]
    [SerializeField] AimProvider _aimProvider;    //조준 정보 제공자

    [Header ( "----- 발사 -----" )]
    [SerializeField] Transform _launchOrigin;    //몸 쪽 발사 경로 시작점
    [SerializeField] Transform _firePoint;    //실제 발사점

    ProjectileData _projectileData;    //사용 시작 시 준비한 비행 설정 참조
    PiercingProjectile _prefab;    //사용 시작 시 준비한 관통 화살 프리팹
    int _maxHitCount;    //사용 시작 시 보존한 최대 유효 적중 대상 수

    /// <summary>
    /// 관통 스킬 설정 검사와 실행 설정 준비
    /// </summary>
    /// <param name="skillData">연결된 스킬 설정</param>
    /// <returns>준비 성공 여부</returns>
    public override bool TryPrepare ( SkillData skillData )
    {
        BowSkillData bowSkillData = skillData as BowSkillData;

        if ( bowSkillData == null )
        {
            Debug.LogError (
                "활 스킬 실행자에는 BowSkillData를 연결해야 합니다." , this );
            return false;
        }

        ProjectileData projectileData = bowSkillData.ProjectileData;

        if ( projectileData == null )
        {
            Debug.LogError (
                "활 스킬의 ProjectileData가 연결되지 않았습니다." , this );
            return false;
        }

        PiercingProjectile prefab =
            projectileData.Prefab as PiercingProjectile;

        if ( prefab == null )
        {
            Debug.LogError (
                "활 스킬에는 PiercingProjectile 프리팹을 연결해야 합니다." , this );
            return false;
        }

        if ( prefab.gameObject.activeSelf == false || prefab.enabled == false )
        {
            Debug.LogError (
                "관통 화살 프리팹과 스크립트는 활성 상태여야 합니다." , this );
            return false;
        }

        if ( projectileData.Speed <= 0f
            || projectileData.MaxDistance <= 0f
            || projectileData.CollisionRadius <= 0f
            || bowSkillData.MaxHitCount <= 0 )
        {
            Debug.LogError (
                "관통 화살의 속도/거리/충돌 반경/최대 대상 수는 양수여야 합니다." , this );
            return false;
        }

        //검사가 모두 끝난 뒤 이번 사용의 설정을 함께 반영
        _projectileData = projectileData;
        _prefab = prefab;
        _maxHitCount = bowSkillData.MaxHitCount;

        return true;
    }

    /// <summary>
    /// 승인된 스킬의 조준 방향 확정과 관통 화살 생성
    /// </summary>
    /// <param name="execution">이번 스킬 실행</param>
    protected override void ExecuteAttack ( AttackExecution execution )
    {
        //생성 과정에서 외부 콜백이 발생해도 이번 발사 설정 유지
        ProjectileData projectileData = _projectileData;
        PiercingProjectile prefab = _prefab;
        int maxHitCount = _maxHitCount;

        AimInfo aim = _aimProvider.GetAim ( execution.Context.Attacker );

        Vector3 launchOrigin = _launchOrigin.position;
        Vector3 firePoint = _firePoint.position;
        Vector3 direction = aim.Point - firePoint;

        //기본 화살과 동일하게 발사점 뒤를 향한 역방향 발사는 거절
        if ( Vector3.Dot ( direction , aim.Direction ) <= 0f )
        {
            if ( _showDebugLog == true )
            {
                Debug.Log (
                    $"관통 화살 발사 거절: 회차={execution.Context.AttackId}, " +
                    $"사유=조준점이 발사점 뒤에 있음" , this );
            }

            execution.Complete ( );
            return;
        }

        PiercingProjectile projectile = Instantiate (
            prefab , firePoint ,
            Quaternion.LookRotation ( direction , Vector3.up ) );

        projectile.SetSound( Sound );
        projectile.SetVfx( Vfx );
        projectile.InitPiercing (
            execution , projectileData ,
            launchOrigin , firePoint , direction.normalized , maxHitCount );
    }
}
