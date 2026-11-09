using UnityEngine;

public class EnemyDamageTest : MonoBehaviour
{
    [SerializeField] private EnemyCombat _targetEnemy;
    [SerializeField] private float _testDamage = 25f;

    [ContextMenu("테스트 피해 주기")]
    private void ApplyTestDamage()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Play 모드에서 피해 테스트를 실행하세요.", this);
            return;
        }

        if (_targetEnemy == null || !_targetEnemy.CanReceiveDamage)
        {
            Debug.LogWarning("초기화된 생존 적을 Target Enemy에 연결하세요.", this);
            return;
        }

        bool isValidDamage =
            _testDamage > 0f &&
            !float.IsInfinity(_testDamage);

        if (!isValidDamage)
        {
            Debug.LogWarning("테스트 피해량은 유한한 양수여야 합니다.", this);
            return;
        }

        // 플레이어 공격을 흉내 내는 테스트 정보
        DamageInfo damageInfo = new DamageInfo(
            gameObject,
            gameObject,
            ActorType.Player,
            Faction.Ally,
            _testDamage,
            AttackType.BasicAttack,
            0);

        // HP 직접 수정 없이 실제 공통 피해 수신 계약 사용
        IDamageable damageable =
            _targetEnemy.GetComponent<DamageReceiver>();

        float appliedDamage = damageable.TakeDamage(damageInfo);

        Debug.Log($"테스트 피해 요청 결과: 실제 피해 {appliedDamage}", this);
    }
}