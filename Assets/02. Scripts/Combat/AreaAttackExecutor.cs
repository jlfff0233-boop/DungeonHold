using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 범위 공격 실행자 - 범위 탐색과 단발 피해 처리
/// </summary>
public class AreaAttackExecutor : AttackExecutor
{
    [Header( "----- 컴포넌트 -----" )]
    [SerializeField] HitDetector _hitDetector;    //타격 탐색 모듈

    [Header( "----- 타격 기준 -----" )]
    [SerializeField] Transform _detectionCenter;    //탐색 중심
    [SerializeField] Transform _directionTarget;    //캐릭터 방향 기준
    [SerializeField] LayerMask _layerMask;          //탐색 레이어
    [SerializeField] VfxEffect _attackVfx; //실제 공격 실행의 단발 효과
    [SerializeField] VfxEffect _ultimateVfx; //궁극기 전용 단발 효과

#if UNITY_EDITOR
    float _gizmoRadius;    //초기화 또는 마지막 타격에서 받은 탐색 반경
    float _gizmoAngle;     //초기화 또는 마지막 타격에서 받은 전체 탐색 각도
    List<Vector3> _lastDetectedPositions = new List<Vector3>( );    //마지막 탐색 대상의 위치
#endif

    /// <summary>
    /// 공격 실행
    /// </summary>
    /// <param name="execution">이번 공격 실행</param>
    protected override void ExecuteAttack ( AttackExecution execution )
    {
        AttackContext context = execution.Context;
        //실행마다 목록을 분리해
        //이벤트 중 다른 실행이 호출돼도 기존 목록 유지
        List<IDamageable> targets = new List<IDamageable>();

        //승인된 실행에서만 요청하며 대상 수와 관계없이 한 번 재생
        VfxEffect effect = context.AttackType == AttackType.Ultimate && _ultimateVfx != null
            ? _ultimateVfx
            : _attackVfx;

        if ( effect != null )
        {
            Vfx.Play( effect, _detectionCenter.position,
                Quaternion.LookRotation( _directionTarget.forward, Vector3.up ) );
        }

        //타격 시점의 위치와 캐릭터 방향으로 탐색
        _hitDetector.Detect(
            _detectionCenter.position, context.Radius,
            _directionTarget.forward, context.Angle,
            _layerMask, targets );

#if UNITY_EDITOR
        SetGizmoRange( context.Radius, context.Angle );

        //피해 이벤트로 대상이 비활성화되기 전에 탐색 결과 위치 보존
        foreach ( IDamageable target in targets )
        {
            if ( target is Component component && component != null )
                _lastDetectedPositions.Add( component.transform.position );
        }
#endif

        DamageInfo damageInfo = new DamageInfo(
            context.Attacker, context.Source,
            context.ActorType, context.Faction,
            context.RawDamage, context.AttackType, context.AttackId );

        float totalDamage = 0f;

        foreach ( IDamageable target in targets )
        {
            //스테이지 정리 등으로 실행 자체가 무효화되면 추가 피해 중단
            if ( execution.IsActive == false )
                break;

            //대상별 피해 허용은 수신 측에서 판단
            float appliedDamage = target.TakeDamage( damageInfo );

            totalDamage += appliedDamage;
        }

        execution.ReportAppliedDamage ( totalDamage );
        execution.Complete ( );
    }

#if UNITY_EDITOR
    /// <summary>
    /// 기즈모 탐색 범위 설정
    /// </summary>
    /// <param name="radius">탐색 반경</param>
    /// <param name="angle">전체 탐색 각도</param>
    public void SetGizmoRange ( float radius, float angle )
    {
        _gizmoRadius = radius;
        _gizmoAngle = angle;
        _lastDetectedPositions.Clear( );
    }

    private void OnDrawGizmos ()
    {
        //Inspector 연결 중에도 호출되므로 아직 없는 표시 기준은 생략
        if ( _detectionCenter == null )
            return;

        Color previousColor = Gizmos.color;
        Vector3 center = _detectionCenter.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere( center, 0.08f );

        if ( _gizmoRadius > 0f )
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere( center, _gizmoRadius );

            if ( _directionTarget != null )
            {
                //실제 각도 필터와 동일하게 수평 전방과 전체 각도 사용
                Vector3 forward = _directionTarget.forward;
                forward.y = 0f;
                forward.Normalize( );

                Gizmos.color = Color.blue;
                Gizmos.DrawLine( center, center + forward * _gizmoRadius );

                float angle = Mathf.Clamp( _gizmoAngle, 0f, 360f );

                if ( angle < 360f )
                {
                    float halfAngle = angle * 0.5f;
                    Vector3 left = Quaternion.AngleAxis( -halfAngle, Vector3.up ) * forward;
                    Vector3 right = Quaternion.AngleAxis( halfAngle, Vector3.up ) * forward;

                    Gizmos.color = Color.yellow;
                    Gizmos.DrawLine( center, center + left * _gizmoRadius );
                    Gizmos.DrawLine( center, center + right * _gizmoRadius );
                }
            }
        }

        //표시 대상은 실제 피해 여부가 아닌 마지막 탐색 결과
        Gizmos.color = Color.green;

        foreach ( Vector3 position in _lastDetectedPositions )
            Gizmos.DrawWireSphere( position, 0.15f );

        Gizmos.color = previousColor;
    }
#endif
}
