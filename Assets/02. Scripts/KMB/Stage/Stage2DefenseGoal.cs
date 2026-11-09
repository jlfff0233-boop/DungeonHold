using System;
using UnityEngine;

[RequireComponent(typeof(Health))]
[RequireComponent(typeof(DamageReceiver))]
public class Stage2DefenseGoal : MonoBehaviour
{
    [SerializeField] private Stage2Data _stageData;

    private Health _health;
    private DamageReceiver _damageReceiver;

    private bool _isInitialized;

    public float CurrentHp => _health.CurrentHp;
    public float MaxHp => _health.MaxHp;

    // 회복해도 실제 피해를 받은 기록은 유지
    public bool HasTakenDamage { get; private set; }

    public bool IsAlive =>
        _isInitialized &&
        _health.IsAlive;

    // 이후 적 공격 코드에서 대상의 유효성을 확인할 때 사용
    public bool CanReceiveDamage =>
        isActiveAndEnabled &&
        IsAlive &&
        _health.isActiveAndEnabled &&
        _damageReceiver.isActiveAndEnabled;

    // 실제 공격 피해는 공통 피해 수신부로 전달
    public DamageReceiver Receiver => _damageReceiver;

    // 이후 Stage2Controller에서 실패 처리에 연결
    public event Action OnDepleted;

    private void Awake()
    {
        _health = GetComponent<Health>();
        _damageReceiver = GetComponent<DamageReceiver>();

        bool hasValidSettings =
            _health != null &&
            _damageReceiver != null &&
            _stageData != null &&
            _stageData.IsValid;

        if (!hasValidSettings)
        {
            Debug.LogError(
                "방어 목표의 Health, DamageReceiver와 " +
                "Stage2Data 설정을 확인하세요.",
                this);

            enabled = false;
            return;
        }

        // 이번 씬 실행의 방어 목표 체력 초기화
        _health.Init(_stageData.DefenseGoalMaxHp);

        // 방어 목표는 아군 진영이며 현재 피해 감소율은 0
        bool wasInitialized = _damageReceiver.Init(
            gameObject,
            _health,
            Faction.Ally,
            0f);

        if (!wasInitialized)
        {
            Debug.LogError(
                "방어 목표의 피해 수신 초기화에 실패했습니다.",
                this);

            enabled = false;
            return;
        }

        _isInitialized = true;
    }

    private void OnEnable()
    {
        if (!_isInitialized)
        {
            return;
        }

        _damageReceiver.OnDamaged += HandleDamaged;
        _damageReceiver.OnDied += HandleDied;
    }

    private void OnDisable()
    {
        if (_damageReceiver != null)
        {
            _damageReceiver.OnDamaged -= HandleDamaged;
            _damageReceiver.OnDied -= HandleDied;
        }
    }

    private void HandleDamaged(
    DamageInfo damageInfo,
    float appliedDamage)
    {
        if (appliedDamage > 0f)
        {
            HasTakenDamage = true;
        }

        Debug.Log(
            $"방어 목표 피격: 피해 {appliedDamage:F1}, " +
            $"HP {_health.CurrentHp:F1}/{_health.MaxHp:F1}",
            this);
    }

    private void HandleDied(DamageInfo damageInfo)
    {
        Debug.Log("방어 목표 HP가 소진되었습니다.", this);

        OnDepleted?.Invoke();
    }
}