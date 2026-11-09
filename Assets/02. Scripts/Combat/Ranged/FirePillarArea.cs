using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 불기둥 영역 - 고정 구형 범위의 주기 피해와 실행 수명 관리
/// </summary>
public class FirePillarArea : MonoBehaviour
{
    [Header ( "----- 디버그 -----" )]
    [SerializeField] bool _showDebugLog = true;    //불기둥 생성/타격/종료 로그 출력 여부

    [Header ( "----- 탐색 -----" )]
    [SerializeField] HitDetector _hitDetector;      //중복 수신자를 제거하는 범위 탐색 모듈
    [SerializeField] LayerMask _targetMask;     //피해 대상 레이어

    [Header ( "----- 불기둥 표현 -----" )]
    [SerializeField] GameObject _pillarPrefab;    //비활성 상태로 저장한 불기둥 표현

    [SerializeField] SoundId _spawnSound; //생성음, None이면 미사용
    [SerializeField] SoundId _tickSound; //주기 효과음, None이면 미사용
    [SerializeField] VfxEffect _spawnVfx; //추가 생성 단발 효과

    GameObject _pillar;    //이번 영역에 생성한 불기둥
    SoundManager _sound; //생성 실행자가 연결한 서비스
    bool _ignoreSoundDistance; //생성 시 확정한 본인 궁극기 출력 여부
    VfxManager _vfx; //생성 실행자가 연결한 VFX 서비스

    AttackExecution _execution;     //이번 궁극기 실행
    Vector3 _center;        //생성 시 고정한 중심
    float _radius;      //사용 시작 시 확정된 피해 반경
    float _totalDamage;     //전체 타격의 원본 피해 합계
    float _damagePerTick;       //일반 타격의 원본 피해
    int _tickCount;     //전체 타격 횟수
    int _completedTicks;        //이미 시작한 타격 횟수
    float _tickInterval;        //타격 간격
    double _startedTime;        //생성 시점의 게임 시간
    bool _isFinished = true;       //초기화 전과 종료 이후 진행 차단

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
    /// 원래 실행과 타격 설정 보존 후 첫 타격 대기 시작
    /// </summary>
    /// <param name="execution">이번 궁극기 실행</param>
    /// <param name="tickCount">전체 타격 횟수</param>
    /// <param name="tickInterval">첫 타격 대기 시간과 이후 간격</param>
    public void Init ( AttackExecution execution , int tickCount , float tickInterval )
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
                $"불기둥 생성: ID={GetInstanceID ( )}, " +
                $"공격자={execution.Context.Attacker}, " +
                $"회차={execution.Context.AttackId}, " +
                $"위치={_center}, 반경={_radius}, " +
                $"대상당 총 원본 피해={_totalDamage}, " +
                $"횟수={_tickCount}, 간격={_tickInterval}",
                this );
        }

        //영역 설정을 모두 보존한 뒤 불기둥 생성
        if ( _spawnVfx != null )
            _vfx.Play( _spawnVfx, _center, Quaternion.identity );

        CreatePillar ( );
        if ( _spawnSound != SoundId.None )
            _sound.PlayAt( _spawnSound, _center, _ignoreSoundDistance );
    }

    private void Update ( )
    {
        if ( _isFinished == true )
            return;

        //모션 종료/사용자 사망은 유지하고
        //실행 무효화 시에만 잔여 타격 중단
        if ( _execution.IsActive == false )
        {
            Finish ( "실행 무효화" );
            return;
        }

        double elapsedTime = Time.timeAsDouble - _startedTime;
        bool tickSoundRequested = false;

        //프레임 지연으로 놓친 주기도 처리하되
        //생성 프레임의 deltaTime은 미리 더하지 않음
        while ( _completedTicks < _tickCount
            && elapsedTime >= ( _completedTicks + 1 ) * ( double ) _tickInterval )
        {
            //피해 콜백이 실행되기 전에 이번 타격의 진행 상태 확정
            _completedTicks++;

            //같은 갱신에서 여러 주기를 처리해도 소리는 한 번만 요청
            if ( tickSoundRequested == false && _tickSound != SoundId.None )
            {
                tickSoundRequested = true;
                _sound.PlayAt( _tickSound, _center, _ignoreSoundDistance );
            }

            if ( ApplyTick ( _completedTicks ) == false )
            {
                Finish ( "타격 중 실행 무효화" );
                return;
            }
        }

        if ( _completedTicks >= _tickCount )
            Finish ( "전체 타격 완료" );
    }

    /// <summary>
    /// 영역 중심에 불기둥 생성 후 활성화
    /// </summary>
    void CreatePillar ( )
    {
        //비활성 프리팹을 생성하여 참조와 위치를 먼저 확정
        _pillar = Instantiate (
            _pillarPrefab , _center , Quaternion.identity , transform );

        _pillar.SetActive ( true );
    }

    /// <summary>
    /// 생성한 불기둥 참조 해제와 객체 제거
    /// </summary>
    void ClearPillar ( )
    {
        if ( _pillar == null )
            return;

        //비활성화 콜백 전에 관리 참조부터 해제
        GameObject pillar = _pillar;
        _pillar = null;

        pillar.SetActive ( false );
        Destroy ( pillar );
    }

    /// <summary>
    /// 매 주기 범위 대상을 다시 탐색하여 총 피해의 이번 분량 적용
    /// </summary>
    /// <param name="tick">이번 타격 번호</param>
    /// <returns>타격 후에도 실행이 유효한지 여부</returns>
    bool ApplyTick ( int tick )
    {
        AttackExecution execution = _execution;
        AttackContext context = execution.Context;
        List<IDamageable> targets = new List<IDamageable> ( );
        _hitDetector.Detect (
            _center , _radius , Vector3.forward , 360f , _targetMask , targets );

        //AttackData.RawDamage는 1회 피해가 아닌 총 피해
        //마지막 분량으로 나눗셈 오차 보정
        float rawDamage = tick == _tickCount
            ? _totalDamage - _damagePerTick * ( _tickCount - 1 ) 
            : _damagePerTick;

        if ( _showDebugLog == true )
        {
            Debug.Log (
                $"불기둥 타격: ID={GetInstanceID ( )}, " +
                $"회차={context.AttackId}, 타격={tick}/{_tickCount}, " +
                $"예정 시간={tick * ( double ) _tickInterval:F2}초, " +
                $"실제 경과={Time.timeAsDouble - _startedTime:F2}초, " +
                $"대상 수={targets.Count}, 원본 피해={rawDamage}",
                this );
        }

        DamageInfo damageInfo = new DamageInfo (
            context.Attacker , gameObject , context.ActorType , context.Faction ,
            rawDamage , context.AttackType , context.AttackId );

        float appliedTotal = 0f;

        foreach ( IDamageable target in targets )
        {
            //앞선 피해 이벤트에서 실행 취소 또는 다른 대상 제거가 발생할 수 있음
            if ( execution.IsActive == false )
                return false;

            if ( target is Component component && ( component == null
                || component.gameObject.activeInHierarchy == false ) )
                continue;

            //피해 콜백 전에 대상 이름을 보존
            Component targetComponent = target as Component;
            string targetName = targetComponent != null
                ? targetComponent.name : target.ToString ( );

            float appliedDamage = target.TakeDamage ( damageInfo );
            appliedTotal += appliedDamage;

            if ( _showDebugLog == true )
            {
                Debug.Log (
                    $"불기둥 피해: ID={GetInstanceID ( )}, " +
                    $"회차={context.AttackId}, 타격={tick}/{_tickCount}, " +
                    $"대상={targetName}, 원본 피해={rawDamage}, " +
                    $"실제 피해={appliedDamage}",
                    this );
            }
        }

        //궁극기는 기본 공격 충전 콜백을 보유하지 않으며 별도 화상도 부여하지 않음
        execution.ReportAppliedDamage ( appliedTotal );
        return execution.IsActive;
    }

    /// <summary>
    /// 기둥 표현과 실행 정리 후 영역 제거
    /// </summary>
    /// <param name="reason">불기둥 종료 사유</param>
    void Finish ( string reason )
    {
        if ( _isFinished == true )
            return;

        //Destroy는 프레임 끝에 처리되므로 완료 표시를 먼저 반영
        _isFinished = true;

        if ( _showDebugLog == true )
        {
            Debug.Log (
                $"불기둥 종료: ID={GetInstanceID ( )}, " +
                $"회차={_execution.Context.AttackId}, 사유={reason}, " +
                $"시작한 타격={_completedTicks}/{_tickCount}, " +
                $"경과={Time.timeAsDouble - _startedTime:F2}초",
                this );
        }

        ClearPillar ( );
        _execution.Complete ( );
        Destroy ( gameObject );
    }

    private void OnDisable ( )
    {
        //정상 종료 후 비활성화에서는 종료 로그를 중복 출력하지 않음
        if ( _isFinished == false && _execution != null
            && _showDebugLog == true )
        {
            Debug.Log (
                $"불기둥 외부 비활성화/제거: ID={GetInstanceID ( )}, " +
                $"회차={_execution.Context.AttackId}, " +
                $"시작한 타격={_completedTicks}/{_tickCount}",
                this );
        }

        //외부 비활성화도 종료로 처리
        //생성 전 편집 상태에는 실행이 없을 수 있음
        _isFinished = true;
        ClearPillar ( );
        _execution?.Complete ( );
    }

    private void OnDrawGizmos ( )
    {
        if ( _execution == null || _isFinished == true )
            return;

        Color previousColor = Gizmos.color;
        Gizmos.color = new Color ( 1f , 0.4f , 0f );
        Gizmos.DrawWireSphere ( _center , _radius );
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere ( _center , 0.08f );
        Gizmos.color = previousColor;
    }
}
