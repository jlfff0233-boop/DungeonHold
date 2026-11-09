using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화살비 영역 - 고정 범위의 주기 피해와 실행 수명 관리
/// </summary>
public class ArrowRainArea : MonoBehaviour
{
    [Header ( "----- 디버그 -----" )]
    [SerializeField] bool _showDebugLog = true;    //생성/타격/종료 로그 출력 여부

    [Header ( "----- 탐색 -----" )]
    [SerializeField] HitDetector _hitDetector;    //이 영역의 범위 탐색 모듈
    [SerializeField] LayerMask _targetMask;    //피해 대상 레이어

    [Header ( "----- 화살 표현 -----" )]
    [SerializeField] GameObject _arrowPrefab;    //비활성 상태로 저장한 표현용 화살
    [SerializeField] int _arrowsPerTick = 10;    //주기마다 생성할 화살 수
    [SerializeField] float _spawnHeight = 10f;    //착지 위치 기준 생성 높이
    [SerializeField] LayerMask _landingMask;    //착지 지면 레이어
    [SerializeField] VfxEffect _landingVfx;    //착지 단발 효과

    [SerializeField] SoundId _spawnSound; //생성음, None이면 미사용
    [SerializeField] SoundId _tickSound; //주기 효과음, None이면 미사용
    [SerializeField] VfxEffect _spawnVfx; //추가 생성 단발 효과

    readonly List<FallingArrow> _arrows =
        new List<FallingArrow> ( );    //현재 낙하 중인 화살 목록
    SoundManager _sound; //생성 실행자가 연결한 서비스
    bool _ignoreSoundDistance; //생성 시 확정한 본인 궁극기 출력 여부
    VfxManager _vfx; //생성 실행자가 연결한 VFX 서비스

    AttackExecution _execution;    //이번 영역의 원래 궁극기 실행
    Vector3 _center;    //생성 시 고정한 피해 중심
    float _radius;    //원래 실행의 피해 반경
    float _totalDamage;    //대상당 전체 원본 피해
    float _damagePerTick;    //일반 타격의 원본 피해
    int _tickCount;    //전체 타격 횟수
    int _completedTicks;    //이미 시작한 타격 횟수
    float _tickInterval;    //타격 간격
    double _startedTime;    //영역 생성 시점의 게임 시간
    bool _isFinished = true;    //초기화 전과 종료 이후 진행 차단

    /// <summary>
    /// 영역 VFX 서비스 연결
    /// </summary>
    /// <param name="vfx">씬 재생 서비스</param>
    public void SetVfx ( VfxManager vfx )
    {
        _vfx = vfx;
    }

    /// <summary>
    /// 영역 사운드 서비스 연결
    /// </summary>
    /// <param name="sound">공통 관리자</param>
    public void SetSound ( SoundManager sound )
    {
        _sound = sound;
    }

    /// <summary>
    /// 낙하 화살 - 생성 객체와 이동 구간 보관
    /// </summary>
    struct FallingArrow
    {
        public Transform Transform;    //생성한 화살 묶음
        public VfxEffect[] Effects;    //묶음 안의 부착 효과
        public Vector3 StartPosition;    //낙하 시작 위치
        public Vector3 EndPosition;    //착지 위치
        public Vector3 GroundNormal;    //착지 지면의 법선
        public double StartedTime;    //예정된 낙하 시작 시간
    }

    /// <summary>
    /// 원래 실행과 타격 설정 보존 후 영역 진행 시작
    /// </summary>
    /// <param name="execution">이번 궁극기 실행</param>
    /// <param name="tickCount">전체 타격 횟수</param>
    /// <param name="tickInterval">타격 간격</param>
    public void Init (
        AttackExecution execution , int tickCount , float tickInterval )
    {
        _execution = execution;
        //시전자 비활성화나 무기 변경 후에도 생성 당시의 출력 방식을 유지
        SoundPlayer soundPlayer = execution.Context.Attacker.GetComponent<SoundPlayer>( );
        _ignoreSoundDistance = soundPlayer != null && soundPlayer.Is2DOutput;
        _center = transform.position;
        _radius = execution.Context.Radius;
        _totalDamage = execution.Context.RawDamage;
        _tickCount = tickCount;
        _tickInterval = tickInterval;
        _damagePerTick = _totalDamage / _tickCount;
        _completedTicks = 0;
        _startedTime = Time.timeAsDouble;
        _isFinished = false;

        if ( _showDebugLog == true )
        {
            Debug.Log (
                $"화살비 생성: ID={GetInstanceID ( )}, " +
                $"회차={execution.Context.AttackId}, 중심={_center}, " +
                $"반경={_radius}, 총 피해={_totalDamage}, " +
                $"횟수={_tickCount}, 간격={_tickInterval}" , this );
        }

        //실행 정보와 타격 설정을 모두 보존한 뒤 첫 화살 생성
        if ( _spawnVfx != null )
            _vfx.Play( _spawnVfx, _center, Quaternion.identity );

        if ( _spawnSound != SoundId.None )
            _sound.PlayAt( _spawnSound, _center, _ignoreSoundDistance );
        SpawnArrows ( _startedTime , _startedTime );
    }

    private void Update ( )
    {
        if ( _isFinished == true )
            return;

        if ( _execution.IsActive == false )
        {
            Finish ( "실행 무효화" );
            return;
        }

        //이 프레임의 피해와 낙하 계산에 같은 시간 사용
        double currentTime = Time.timeAsDouble;
        double elapsedTime = currentTime - _startedTime;

        UpdateArrows ( currentTime );

        //화살 정리 과정에서 영역이 비활성화된 경우 진행 중단
        if ( _isFinished == true )
            return;

        int previousTicks = _completedTicks;
        bool tickSoundRequested = false;

        while ( _completedTicks < _tickCount
            && elapsedTime >=
                ( _completedTicks + 1 ) * ( double ) _tickInterval )
        {
            //피해 콜백 전에 이번 타격의 진행 횟수 확정
            _completedTicks++;

            //밀린 피해는 모두 처리하고 소리는 이번 갱신에 한 번만 요청
            if ( tickSoundRequested == false && _tickSound != SoundId.None )
            {
                tickSoundRequested = true;
                _sound.PlayAt( _tickSound, _center, _ignoreSoundDistance );
            }

            if ( ApplyTick ( _completedTicks ) == false )
            {
                Finish ( "실행 무효화" );
                return;
            }

            if ( _isFinished == true )
                return;
        }

        if ( _completedTicks >= _tickCount )
        {
            Finish ( "전체 타격 완료" );
            return;
        }

        //지연 프레임에서도 이미 지나간 화살 묶음은 생성하지 않음
        if ( _completedTicks != previousTicks )
        {
            double nextStartedTime =
                _startedTime + _completedTicks * ( double ) _tickInterval;

            SpawnArrows ( nextStartedTime , currentTime );
        }
    }

    /// <summary>
    /// 현재 주기의 화살을 낙하 진행 위치에 생성
    /// </summary>
    /// <param name="startedTime">예정된 낙하 시작 시간</param>
    /// <param name="currentTime">현재 프레임의 게임 시간</param>
    void SpawnArrows ( double startedTime , double currentTime )
    {
        float progress = Mathf.Clamp01 (
            ( float ) ( ( currentTime - startedTime ) / _tickInterval ) );

        Quaternion rotation = Quaternion.LookRotation (
            Vector3.down , Vector3.forward );

        for ( int i = 0; i < _arrowsPerTick; i++ )
        {
            //표현 활성화 콜백에서 실행이 종료되면 추가 생성 중단
            if ( _isFinished == true || _execution.IsActive == false )
                return;

            Vector2 offset = Random.insideUnitCircle * _radius;

            Vector3 endPosition = _center
                + new Vector3 ( offset.x , 0f , offset.y );
            Vector3 groundNormal = Vector3.zero;

            //지면 레이어를 지정한 경우에만 실제 착지점을 조회
            if ( _landingMask.value != 0 )
            {
                Vector3 rayOrigin = endPosition + Vector3.up * _spawnHeight;
                if ( Physics.Raycast ( rayOrigin , Vector3.down , out RaycastHit groundHit ,
                    _spawnHeight * 2f , _landingMask , QueryTriggerInteraction.Ignore ) == false )
                    continue;

                endPosition = groundHit.point;
                groundNormal = groundHit.normal;
            }

            Vector3 startPosition = endPosition
                + Vector3.up * _spawnHeight;

            Vector3 currentPosition = Vector3.Lerp (
                startPosition , endPosition , progress );

            //비활성 프리팹을 최종 위치와 방향으로 생성
            GameObject arrowObject = Instantiate (
                _arrowPrefab , currentPosition , rotation , transform );

            FallingArrow arrow = new FallingArrow
            {
                Transform = arrowObject.transform,
                Effects = System.Array.Empty<VfxEffect>( ),
                StartPosition = startPosition,
                EndPosition = endPosition,
                GroundNormal = groundNormal,
                StartedTime = startedTime
            };

            //활성화 이후 종료 요청이 들어와도 정리할 수 있도록 먼저 등록
            _arrows.Add ( arrow );
            arrowObject.SetActive ( true );

            //활성화 과정에서 영역이 종료됐다면 효과를 시작하지 않음
            if ( _isFinished == true )
                return;

            int index = _arrows.Count - 1;
            arrow.Effects = _vfx.PlayAttached( arrowObject.transform );
            _arrows[index] = arrow;
        }
    }

    /// <summary>
    /// 게임 시간에 맞춰 화살 이동 후 착지한 객체 제거
    /// </summary>
    /// <param name="currentTime">현재 프레임의 게임 시간</param>
    void UpdateArrows ( double currentTime )
    {
        for ( int i = _arrows.Count - 1; i >= 0; i-- )
        {
            FallingArrow arrow = _arrows[i];

            //표현 객체가 외부에서 제거된 경우 기록 정리
            if ( arrow.Transform == null )
            {
                RemoveArrow( i, true );
                continue;
            }

            float progress = Mathf.Clamp01 (
                ( float ) ( ( currentTime - arrow.StartedTime )
                    / _tickInterval ) );

            arrow.Transform.position = Vector3.Lerp (
                arrow.StartPosition , arrow.EndPosition , progress );

            if ( progress < 1f )
                continue;

            //착지 위치 반영 후 부착 효과를 분리
            RemoveArrow( i, false );

            if ( _isFinished == true )
                return;

            if ( _landingVfx != null && arrow.GroundNormal != Vector3.zero )
            {
                Quaternion rotation = Quaternion.FromToRotation ( Vector3.up , arrow.GroundNormal );
                _vfx.Play ( _landingVfx , arrow.EndPosition , rotation );
            }
        }
    }

    /// <summary>
    /// 관리 기록 해제 후 화살과 부착 효과 종료
    /// </summary>
    /// <param name="index">제거할 화살 기록</param>
    /// <param name="clear">잔상 즉시 제거 여부</param>
    void RemoveArrow ( int index, bool clear )
    {
        FallingArrow arrow = _arrows[index];

        //비활성화와 제거보다 먼저 관리 기록 해제
        _arrows.RemoveAt( index );

        foreach ( VfxEffect effect in arrow.Effects )
        {
            if ( effect == null )
                continue;

            if ( clear == true )
                effect.Clear( );
            else
                effect.Stop( );
        }

        if ( arrow.Transform == null )
            return;

        GameObject arrowObject = arrow.Transform.gameObject;
        arrowObject.SetActive( false );
        Destroy( arrowObject );
    }

    /// <summary>
    /// 남은 화살과 부착 효과 즉시 정리
    /// </summary>
    void ClearArrows ( )
    {
        while ( _arrows.Count > 0 )
            RemoveArrow( _arrows.Count - 1, true );
    }

    /// <summary>
    /// 이번 주기의 대상 재탐색과 중복 없는 범위 피해
    /// </summary>
    /// <param name="tick">이번 타격 번호</param>
    /// <returns>실행이 유효한 상태로 타격을 마쳤는지 여부</returns>
    bool ApplyTick ( int tick )
    {
        if ( _execution.IsActive == false )
            return false;

        AttackExecution execution = _execution;
        AttackContext context = execution.Context;

        //매 주기 새로 탐색하여 진입/이탈과 다중 Collider를 반영
        List<IDamageable> targets = new List<IDamageable> ( );

        _hitDetector.Detect (
            _center , _radius , Vector3.forward ,
            360f , _targetMask , targets );

        //나눗셈 오차가 전체 원본 피해에 누적되지 않도록 마지막 분량 계산
        float rawDamage = tick == _tickCount
            ? _totalDamage - _damagePerTick * ( _tickCount - 1 )
            : _damagePerTick;

        DamageInfo damageInfo = new DamageInfo (
            context.Attacker , gameObject ,
            context.ActorType , context.Faction ,
            rawDamage , context.AttackType , context.AttackId );

        if ( _showDebugLog == true )
        {
            Debug.Log (
                $"화살비 타격 시작: ID={GetInstanceID ( )}, " +
                $"회차={context.AttackId}, 타격={tick}/{_tickCount}, " +
                $"탐색 대상={targets.Count}, 원본 피해={rawDamage}" , this );
        }

        float appliedTotal = 0f;

        foreach ( IDamageable target in targets )
        {
            //이전 대상의 피해 이벤트에서 실행이 정리되면 즉시 중단
            if ( execution.IsActive == false )
                return false;

            string targetName;

            if ( target is Component component )
            {
                //앞선 사망 이벤트에서 파괴/비활성화된 Unity 대상 제외
                if ( component == null
                    || component.gameObject.activeInHierarchy == false )
                    continue;

                targetName = component.name;
            }
            else
            {
                targetName = target.GetType ( ).Name;
            }

            float appliedDamage = target.TakeDamage ( damageInfo );
            appliedTotal += appliedDamage;

            if ( _showDebugLog == true )
            {
                Debug.Log (
                    $"화살비 피해 결과: ID={GetInstanceID ( )}, " +
                    $"회차={context.AttackId}, 타격={tick}/{_tickCount}, " +
                    $"대상={targetName}, 원본 피해={rawDamage}, " +
                    $"실제 피해={appliedDamage}" , this );
            }

            if ( execution.IsActive == false )
                return false;
        }

        //Ultimate 실행에는 기본 공격 충전 콜백이 연결되지 않음
        execution.ReportAppliedDamage ( appliedTotal );
        return execution.IsActive;
    }

    /// <summary>
    /// 낙하 화살과 실행 정리 후 영역 제거
    /// </summary>
    /// <param name="reason">영역 종료 사유</param>
    void Finish ( string reason )
    {
        if ( _isFinished == true )
            return;

        //Destroy가 프레임 끝에 처리되므로 제거 예약부터 진행 차단
        _isFinished = true;

        if ( _showDebugLog == true )
        {
            Debug.Log (
                $"화살비 종료: ID={GetInstanceID ( )}, " +
                $"회차={_execution.Context.AttackId}, " +
                $"타격={_completedTicks}/{_tickCount}, 사유={reason}" , this );
        }

        ClearArrows ( );
        _execution.Complete ( );
        Destroy ( gameObject );
    }

    private void OnDisable ( )
    {
        //외부 비활성화도 종료로 처리하고 남은 화살 정리
        _isFinished = true;
        ClearArrows ( );
        _execution?.Complete ( );
    }

    private void OnDrawGizmos ( )
    {
        //초기화 전 프리팹 편집 상태에서는 실행 범위를 표시하지 않음
        if ( _execution == null || _isFinished == true )
            return;

        Color previousColor = Gizmos.color;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere ( _center , _radius );

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere ( _center , 0.08f );

        Gizmos.color = previousColor;
    }
}
