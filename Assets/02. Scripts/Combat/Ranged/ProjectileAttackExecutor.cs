using UnityEngine;

/// <summary>
/// 투사체 공격 실행자 - 발사 정보 확정과 투사체 생성
/// </summary>
public class ProjectileAttackExecutor : AttackExecutor
{
    [Header ( "----- 조준 -----" )]
    [SerializeField] AimProvider _aimProvider;    //조준 정보 제공자

    [Header ( "----- 발사 -----" )]
    [SerializeField] Transform _launchOrigin;    //몸 쪽 발사 경로 시작점
    [SerializeField] Transform _firePoint;    //실제 투사체 발사점
    [SerializeField] ProjectileData _projectileData;    //생성/비행 설정

    int _lastShotFrame = -1;    //발사 방향을 상체 표현에 전달하는 프레임
    Vector3 _lastShotDirection;    //투사체에 실제 적용한 방향

    /// <summary>
    /// 발사 프레임에는 실제 화살 방향을, 그 외에는 캐릭터 전방을 반환
    /// </summary>
    /// <param name="owner">조준 방향을 향하는 캐릭터 루트</param>
    /// <returns>상체 보정에 사용할 월드 방향</returns>
    internal Vector3 GetPresentationAimDirection ( Transform owner )
    {
        return _lastShotFrame == Time.frameCount ? _lastShotDirection : owner.forward;
    }

    /// <summary>
    /// 승인된 공격의 투사체 생성
    /// </summary>
    /// <param name="execution">이번 공격 실행</param>
    protected override void ExecuteAttack ( AttackExecution execution )
    {
        //정지한 채 남거나 충돌 검사가 성립하지 않는 설정 거절
        if ( _projectileData.Speed <= 0f
            || _projectileData.MaxDistance <= 0f
            || _projectileData.CollisionRadius <= 0f )
        {
            Debug.LogError (
                "투사체 속도/최대 거리/충돌 반경은 0보다 커야 합니다." , this );

            execution.Complete ( );
            return;
        }

        AimInfo aim = _aimProvider.GetAim ( execution.Context.Attacker );

        Vector3 origin = _firePoint.position;
        Vector3 direction = aim.Point - origin;

        //발사점 뒤의 가까운 조준점으로 역방향 발사하지 않음
        if ( Vector3.Dot ( direction , aim.Direction ) <= 0f )
        {
            execution.Complete ( );
            return;
        }

        Vector3 launchDirection = direction.normalized;
        _lastShotFrame = Time.frameCount;
        _lastShotDirection = launchDirection;

        Projectile projectile = Instantiate (
            _projectileData.Prefab ,
            origin ,
            Quaternion.LookRotation ( direction , Vector3.up ) );

        projectile.SetSound( Sound );
        projectile.SetVfx( Vfx );
        projectile.Init (
            execution ,
            _projectileData ,
            _launchOrigin.position ,
            origin ,
            launchDirection );
    }
}
