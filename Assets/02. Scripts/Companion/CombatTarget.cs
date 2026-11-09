using UnityEngine;

/// <summary>
/// 전투 타겟 - 피해 수신 모듈의 진영과 생존 상태를 AI에 제공
/// </summary>
[RequireComponent ( typeof ( DamageReceiver ) )]
public class CombatTarget : MonoBehaviour
{
    DamageReceiver _damageReceiver;    //체력과 진영의 조회 원본

    /// <summary>
    /// 타겟 루트의 월드 위치
    /// </summary>
    public Vector3 Position => transform.position;

    /// <summary>
    /// 피해 수신 모듈에 설정된 소속 진영
    /// </summary>
    public Faction Faction => _damageReceiver.Faction;

    /// <summary>
    /// 초기화된 활성 생존 타겟 여부
    /// </summary>
    public bool IsValid =>
        isActiveAndEnabled
        && _damageReceiver.isActiveAndEnabled
        && _damageReceiver.IsAlive;

    private void Awake ( )
    {
        //동일한 루트의 피해 수신 모듈을 확보하고,
        //체력과 진영은 캐릭터 초기화 주체가 설정한 값을 그대로 조회
        _damageReceiver = GetComponent<DamageReceiver> ( );
    }
}
