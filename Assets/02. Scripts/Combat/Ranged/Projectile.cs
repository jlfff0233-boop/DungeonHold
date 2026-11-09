using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 투사체 - 직선 비행/충돌 탐색과 실행 수명 관리
/// </summary>
public abstract class Projectile : MonoBehaviour
{
    [Header ( "----- 디버그 -----" )]
    [SerializeField] bool _showDebugLog = true;    //투사체 검증 로그 출력 여부

    AttackExecution _execution;    //이번 발사의 원래 실행
    SoundManager _sound; //발사 시 연결한 서비스
    SoundId _impactSound; //이번 투사체의 충돌음
    VfxManager _vfx; //발사 시 전달받은 VFX 서비스
    VfxEffect _impactVfx; //이번 충돌 효과 설정
    VfxEffect _flightEffect; //재생 중인 비행 효과
    VfxEffect[] _attachedEffects = System.Array.Empty<VfxEffect>( ); //부착된 비행 효과

    Vector3 _direction;    //발사 시 확정한 이동 방향
    float _speed;    //발사 시 확정한 이동 속력
    float _remainingDistance;    //남은 비행 거리
    float _collisionRadius;    //충돌 검사 반경
    LayerMask _collisionMask;    //충돌 검사 레이어

    /// <summary>
    /// 최초 충돌 전 VFX 서비스 연결
    /// </summary>
    /// <param name="vfx">씬 재생 서비스</param>
    public void SetVfx ( VfxManager vfx )
    {
        _vfx = vfx;
    }

    /// <summary>
    /// 충돌 위치의 단발 효과 요청
    /// </summary>
    /// <param name="point">충돌 위치</param>
    protected void PlayImpactVfx ( Vector3 point )
    {
        if ( _impactVfx != null )
            _vfx.Play( _impactVfx, point, transform.rotation );
    }

    /// <summary>
    /// 비행 효과 종료와 보관 참조 해제
    /// </summary>
    /// <param name="clear">남은 파티클 즉시 제거 여부</param>
    void StopFlight ( bool clear )
    {
        if ( _flightEffect == null )
            return;

        VfxEffect effect = _flightEffect;
        _flightEffect = null;

        if ( clear == true )
            effect.Clear( );
        else
            effect.Stop( );
    }

    /// <summary>
    /// 부착 효과 참조 해제 후 종료
    /// </summary>
    /// <param name="clear">잔상 즉시 제거 여부</param>
    void StopAttached ( bool clear )
    {
        VfxEffect[] effects = _attachedEffects;
        _attachedEffects = System.Array.Empty<VfxEffect>( );

        foreach ( VfxEffect effect in effects )
        {
            //외부에서 먼저 제거됐거나 재생에 실패한 효과는 제외
            if ( effect == null )
                continue;

            if ( clear == true )
                effect.Clear( );
            else
                effect.Stop( );
        }
    }

    /// <summary>
    /// 최초 충돌 전 사운드 서비스 연결
    /// </summary>
    /// <param name="sound">공통 관리자</param>
    public void SetSound ( SoundManager sound )
    {
        _sound = sound;
    }

    /// <summary>
    /// 충돌 위치의 효과음 요청
    /// </summary>
    /// <param name="point">충돌 위치</param>
    protected void PlayImpactSound ( Vector3 point )
    {
        if ( _impactSound != SoundId.None )
            _sound.PlayAt( _impactSound, point );
    }

    /// <summary>
    /// 투사체 충돌 후보 - 이동 구간의 처리 순서와 충돌 위치 보관
    /// </summary>
    struct ProjectileHit
    {
        public Collider Collider;    //충돌 대상
        public Vector3 Point;    //충돌 위치
        public float Distance;    //이동 경로상 충돌 거리
        public float SqrPointDistance;    //같은 거리 후보의 위치 비교값
    }

    /// <summary>
    /// 이번 발사의 원래 실행
    /// </summary>
    protected AttackExecution Execution => _execution;

    /// <summary>
    /// 투사체 검증 로그 출력 여부
    /// </summary>
    protected bool ShowDebugLog => _showDebugLog;

    /// <summary>
    /// 발사 정보 보존과 최초 차폐 검사
    /// </summary>
    /// <param name="execution">이번 공격 실행</param>
    /// <param name="data">투사체 설정</param>
    /// <param name="launchOrigin">몸 쪽 발사 경로 시작점</param>
    /// <param name="firePoint">실제 발사 위치</param>
    /// <param name="direction">정규화된 이동 방향</param>
    public void Init (
        AttackExecution execution ,
        ProjectileData data ,
        Vector3 launchOrigin ,
        Vector3 firePoint ,
        Vector3 direction )
    {
        Init( execution, data, launchOrigin, firePoint, direction, data.LaunchSound, data.ImpactSound );
    }

    /// <summary>
    /// 공유 데이터를 변경하지 않고 이번 발사의 사운드를 지정하여 초기화
    /// </summary>
    /// <param name="execution">이번 공격 실행</param>
    /// <param name="data">투사체 설정</param>
    /// <param name="launchOrigin">몸 쪽 발사 경로 시작점</param>
    /// <param name="firePoint">실제 발사 위치</param>
    /// <param name="direction">정규화된 이동 방향</param>
    /// <param name="launchSound">이번 발사음, None이면 미사용</param>
    /// <param name="impactSound">이번 충돌음, None이면 미사용</param>
    public void Init (
        AttackExecution execution , ProjectileData data ,
        Vector3 launchOrigin , Vector3 firePoint , Vector3 direction ,
        SoundId launchSound , SoundId impactSound )
    {
        _execution = execution;
        _direction = direction;
        _speed = data.Speed;
        _remainingDistance = data.MaxDistance;
        _collisionRadius = data.CollisionRadius;
        _collisionMask = data.CollisionMask;
        _impactSound = impactSound;
        _impactVfx = data.ImpactVfx;

        transform.position = firePoint;

        //최초 충돌 검사 전에 적중 정보와 발사 표현 준비
        PrepareHit ( );

        //최초 차폐 검사 전에 부착 효과와 별도 비행 효과 준비
        _attachedEffects = _vfx.PlayAttached( transform );

        if ( data.LaunchVfx != null )
            _vfx.Play( data.LaunchVfx, firePoint, transform.rotation );

        if ( data.FlightVfx != null )
            _flightEffect = _vfx.Follow( data.FlightVfx, transform );

        //발사음 요청
        if ( launchSound != SoundId.None )
        {
            //출력 방식은 공격자 연결 단계에서 결정하며 충돌음은 월드 출력 유지
            SoundPlayer launchPlayer = execution.Context.Attacker.GetComponent<SoundPlayer>( );
            if ( launchPlayer != null )
                launchPlayer.Play( launchSound );
            else if ( execution.Context.ActorType == ActorType.Player || execution.Context.ActorType == ActorType.Companion )
                Debug.LogError( "캐릭터 발사음에 필요한 공격자의 SoundPlayer가 없습니다.", this );
            else
                _sound.PlayAt( launchSound, firePoint );
        }

        if ( _showDebugLog == true )
        {
            Debug.Log (
                $"투사체 생성/발사: 이름={name}, ID={GetInstanceID ( )}, " +
                $"공격자={execution.Context.Attacker}, 회차={execution.Context.AttackId}, " +
                $"위치={firePoint}, 방향={direction}, " +
                $"속도={_speed}, 최대 거리={_remainingDistance}",
                this );
        }

        //발사점이 벽 너머에 있어도 몸에서 발사점까지의 차폐를 적용
        if ( TryProcessHits (
            launchOrigin , firePoint - launchOrigin ) == false )
        {
            transform.position = firePoint;
        }
    }

    /// <summary>
    /// 최초 충돌 검사 전에 자식의 적중 정보 준비
    /// </summary>
    protected virtual void PrepareHit ( )
    {
    }

    private void Update ( )
    {
        //일반 모션 종료/사망은 유지하고 실행 무효화만 비행 중단
        if ( _execution.IsActive == false )
        {
            Finish ( "실행 무효화" );
            return;
        }

        float deltaTime = Time.deltaTime;

        if ( deltaTime <= 0f )
            return;

        float distance = Mathf.Min ( _speed * deltaTime , _remainingDistance );

        Vector3 origin = transform.position;
        Vector3 displacement = _direction * distance;
        Vector3 destination = origin + displacement;

        if ( TryProcessHits ( origin , displacement ) == true )
            return;

        //관통 중 충돌 위치로 옮겨졌더라도 원래 프레임 목적지까지 이동
        transform.position = destination;

        //피해 대상 수와 관계없이 이번 프레임 이동 거리는 한 번만 차감
        _remainingDistance = Mathf.Max ( 0f , _remainingDistance - distance );

        if ( _remainingDistance <= 0f )
            Finish ( "최대 거리 도달" );
    }

    /// <summary>
    /// 시작 겹침과 이동 구간 충돌을 수집하여 가까운 순서로 처리
    /// </summary>
    /// <param name="origin">검사 시작 위치</param>
    /// <param name="displacement">검사할 이동량</param>
    /// <returns>충돌 처리 중 투사체가 종료됐는지 여부</returns>
    bool TryProcessHits ( Vector3 origin , Vector3 displacement )
    {
        //피해 이벤트에서 다른 투사체가 실행돼도 이번 후보 목록은 유지
        List<ProjectileHit> candidates = new List<ProjectileHit> ( );
        HashSet<Collider> overlappingColliders = new HashSet<Collider> ( );

        Collider [ ] overlaps = Physics.OverlapSphere (
            origin ,
            _collisionRadius ,
            _collisionMask ,
            QueryTriggerInteraction.Collide );

        foreach ( Collider collider in overlaps )
        {
            if ( CanCollide ( collider ) == false )
                continue;

            Vector3 point = collider.ClosestPoint ( origin );
            overlappingColliders.Add ( collider );

            candidates.Add ( new ProjectileHit
            {
                Collider = collider ,
                Point = point ,
                Distance = 0f ,
                SqrPointDistance = ( point - origin ).sqrMagnitude
            } );
        }

        float distance = displacement.magnitude;

        //같은 위치의 발사 기준점은 시작 겹침만 검사
        if ( distance > 0f )
        {
            RaycastHit [ ] hits = Physics.SphereCastAll (
                origin ,
                _collisionRadius ,
                displacement / distance ,
                distance ,
                _collisionMask ,
                QueryTriggerInteraction.Collide );

            foreach ( RaycastHit hit in hits )
            {
                //시작 겹침은 위에서 정확한 위치로 등록했으므로 중복 수집 제외
                if ( overlappingColliders.Contains ( hit.collider ) == true )
                    continue;

                if ( CanCollide ( hit.collider ) == false )
                    continue;

                candidates.Add ( new ProjectileHit
                {
                    Collider = hit.collider ,
                    Point = hit.point ,
                    Distance = hit.distance ,
                    SqrPointDistance = ( hit.point - origin ).sqrMagnitude
                } );
            }
        }

        candidates.Sort ( CompareHits );

        foreach ( ProjectileHit candidate in candidates )
        {
            //이전 대상의 피해 이벤트에서 실행이 정리됐으면 후속 피해 중단
            if ( _execution.IsActive == false )
            {
                Finish ( "실행 무효화" );
                return true;
            }

            Collider collider = candidate.Collider;

            //이전 피해의 사망 처리에서 제거/비활성화된 후보는 제외
            if ( collider == null
                || collider.enabled == false
                || collider.gameObject.activeInHierarchy == false )
                continue;

            //앞선 적중으로 같은 수신자가 처리됐을 수 있으므로 다시 확인
            if ( CanCollide ( collider ) == false )
                continue;

            if ( HandleHit ( collider , candidate.Point ) == true )
                return true;
        }

        return false;
    }

    /// <summary>
    /// 이동 경로상 가까운 충돌부터 처리하도록 비교
    /// </summary>
    /// <param name="first">첫 번째 후보</param>
    /// <param name="second">두 번째 후보</param>
    /// <returns>충돌 처리 순서 비교값</returns>
    int CompareHits ( ProjectileHit first , ProjectileHit second )
    {
        int distanceOrder = first.Distance.CompareTo ( second.Distance );

        if ( distanceOrder != 0 )
            return distanceOrder;

        //시작부터 겹친 후보는 시작점에 가까운 위치를 우선
        return first.SqrPointDistance.CompareTo ( second.SqrPointDistance );
    }

    /// <summary>
    /// 발사 주체와 구역 감지용 Trigger를 충돌에서 제외
    /// </summary>
    /// <param name="collider">충돌 후보</param>
    /// <returns>충돌 처리 대상 여부</returns>
    protected virtual bool CanCollide ( Collider collider )
    {
        GameObject owner = _execution.Context.Attacker;

        //발사 후 주체가 파괴되어도 남은 투사체는 비행 유지
        if ( owner != null
            && collider.transform.IsChildOf ( owner.transform ) == true )
            return false;

        if ( collider.isTrigger == true
            && collider.GetComponentInParent<IDamageable> ( ) == null )
            return false;

        return true;
    }

    /// <summary>
    /// 충돌 위치 반영과 자식 처리 결과에 따른 비행 종료
    /// </summary>
    /// <param name="collider">충돌 대상</param>
    /// <param name="point">충돌 위치</param>
    /// <returns>투사체가 종료됐는지 여부</returns>
    bool HandleHit ( Collider collider , Vector3 point )
    {
        transform.position = point;

        if ( _showDebugLog == true )
        {
            Debug.Log (
                $"투사체 충돌: ID={GetInstanceID ( )}, " +
                $"회차={_execution.Context.AttackId}, 대상={collider.name}, " +
                $"레이어={LayerMask.LayerToName ( collider.gameObject.layer )}, " +
                $"위치={point}",
                this );
        }

        bool shouldFinish = ApplyHit ( collider , point );

        //피해 이벤트에서 비활성화/재초기화가 발생한 경우 우선 종료
        if ( _execution.IsActive == false )
        {
            Finish ( "실행 무효화" );
            return true;
        }

        if ( shouldFinish == true )
        {
            Finish ( "충돌" );
            return true;
        }

        return false;
    }

    /// <summary>
    /// 투사체 종류별 적중 처리와 종료 여부 결정
    /// </summary>
    /// <param name="collider">충돌 대상</param>
    /// <param name="point">충돌 위치</param>
    /// <returns>이번 충돌로 종료할지 여부</returns>
    protected abstract bool ApplyHit ( Collider collider , Vector3 point );

    /// <summary>
    /// 비행 종료와 실행 정리 후 투사체 제거
    /// </summary>
    /// <param name="reason">투사체 종료 사유</param>
    void Finish ( string reason )
    {
        if ( _showDebugLog == true )
        {
            Debug.Log (
                $"투사체 소멸 요청: ID={GetInstanceID ( )}, " +
                $"회차={_execution.Context.AttackId}, 사유={reason}, " +
                $"위치={transform.position}",
                this );
        }

        //정상 종료는 잔상을 분리하고 실행 무효화는 즉시 정리
        bool clear = _execution.IsActive == false;
        StopFlight( clear );
        StopAttached( clear );

        _execution.Complete ( );
        Destroy ( gameObject );
    }

    private void OnDisable ( )
    {
        StopFlight( true );
        StopAttached( true );

        //외부에서 비활성화/제거된 투사체의 실행 기록도 해제
        _execution?.Complete ( );
    }
}
