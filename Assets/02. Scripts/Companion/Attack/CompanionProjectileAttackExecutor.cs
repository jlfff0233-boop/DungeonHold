using UnityEngine;

/// <summary>
/// 동료 투사체 실행자 - 타겟 조준 위치 전달과 기존 발사 기능 연결
/// </summary>
[RequireComponent ( typeof ( CompanionAimProvider ) )]
public class CompanionProjectileAttackExecutor
    : ProjectileAttackExecutor , ITargetedAttack
{
    CompanionAimProvider _companionAimProvider;    //같은 오브젝트의 동료 조준 제공자

    private void Awake ( )
    {
        _companionAimProvider = GetComponent<CompanionAimProvider> ( );
    }

    /// <summary>
    /// 이번 발사의 조준 위치를 조준 제공자에 전달
    /// </summary>
    /// <param name="position">피격 콜라이더 중심의 월드 위치</param>
    public void SetTargetPosition ( Vector3 position )
    {
        _companionAimProvider.SetTargetPosition ( position );
    }
}
