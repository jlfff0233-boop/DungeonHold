using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타격 탐색 모듈 - 타격 대상 탐색
/// </summary>
public class HitDetector : MonoBehaviour
{
    /// <summary>
    /// 타격 대상 해시셋
    /// </summary>
    HashSet<IDamageable> _detectedTargets = new HashSet<IDamageable>( );

    /// <summary>
    /// 타격 대상 탐색
    /// </summary>
    /// <param name="center">탐색 중심점</param>
    /// <param name="radius">탐색 반경</param>
    /// <param name="forward">전방</param>
    /// <param name="angle">전체 탐색 각도</param>
    /// <param name="layerMask">탐색 레이어</param>
    /// <param name="results">탐색 결과 목록</param>
    public void Detect (
        Vector3 center, float radius,
        Vector3 forward, float angle,
        LayerMask layerMask, List<IDamageable> results )
    {
        //이전 탐색 결과 제거
        results.Clear( );
        _detectedTargets.Clear( );

        //유효하지 않은 범위는 탐색하지 않음
        if ( radius > 0f == false
            || float.IsInfinity( radius ) == true )
            return;

        if ( float.IsNaN( angle ) == true
            || float.IsInfinity( angle ) == true )
            return;

        //전체 각도 범위 제한
        angle = Mathf.Clamp( angle, 0f, 360f );

        //캐릭터의 수평 전방을 기준으로 사용
        forward.y = 0f;

        if ( angle < 360f && forward.sqrMagnitude <= 0f )
            return;

        forward.Normalize( );

        //피격용 Trigger Collider도 포함
        Collider [ ] colliders = Physics.OverlapSphere(
            center, radius, layerMask, QueryTriggerInteraction.Collide );

        float halfAngle = angle * 0.5f;

        foreach ( Collider collider in colliders )
        {
            //Collider 중심의 수평 방향으로 각도 검사
            Vector3 direction = collider.bounds.center - center;
            direction.y = 0f;

            if ( angle < 360f && direction.sqrMagnitude > 0f )
            {
                if ( Vector3.Angle( forward, direction ) > halfAngle )
                    continue;
            }

            //자식 Collider에서도 부모 피해 수신자 식별
            IDamageable target = collider.GetComponentInParent<IDamageable>( );

            if ( target == null )
                continue;

            //같은 수신자의 여러 Collider는 한 번만 포함
            if ( _detectedTargets.Add( target ) == false )
                continue;

            results.Add( target );
        }
    }
}
