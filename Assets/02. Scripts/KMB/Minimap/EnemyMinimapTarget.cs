using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(
    typeof(MinimapTarget),
    typeof(EnemyCombat),
    typeof(EnemyMovement))]
public class EnemyMinimapTarget : MonoBehaviour
{
    private MinimapTarget _minimapTarget;
    private EnemyCombat _enemyCombat;
    private EnemyMovement _enemyMovement;

    private void Awake()
    {
        _minimapTarget = GetComponent<MinimapTarget>();
        _enemyCombat = GetComponent<EnemyCombat>();
        _enemyMovement = GetComponent<EnemyMovement>();
    }

    private void OnEnable()
    {
        _enemyCombat.OnDied += HandleEnemyDied;
        _enemyMovement.OnPassed += HandleEnemyPassed;

        // 이미 사망하거나 통과한 적을 다시 표시하지 않음
        bool canShow =
            !_enemyCombat.IsDead &&
            !_enemyMovement.HasPassed;

        _minimapTarget.SetVisible(canShow);
    }

    private void OnDisable()
    {
        if (_enemyCombat != null)
        {
            _enemyCombat.OnDied -= HandleEnemyDied;
        }

        if (_enemyMovement != null)
        {
            _enemyMovement.OnPassed -= HandleEnemyPassed;
        }

        // 연결 컴포넌트만 꺼진 경우에도 아이콘 숨김
        if (_minimapTarget != null)
        {
            _minimapTarget.SetVisible(false);
        }
    }

    private void HandleEnemyDied(
        EnemyCombat enemy,
        DamageInfo damageInfo)
    {
        // 사망 연출이 끝날 때까지 기다리지 않고 즉시 숨김
        _minimapTarget.SetVisible(false);
    }

    private void HandleEnemyPassed(EnemyMovement enemy)
    {
        // Stage 1의 통과 처리에서도 즉시 숨김
        _minimapTarget.SetVisible(false);
    }
}