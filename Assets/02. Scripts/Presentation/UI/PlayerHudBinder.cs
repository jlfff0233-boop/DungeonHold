using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHudBinder : MonoBehaviour
{
    [Header("표시할 UI")]
    [SerializeField] private PlayerHudView _view;

    [Header("씬의 플레이어 컴포넌트")]
    [SerializeField] private Health _health;
    [SerializeField] private Energy _energy;

    private Health _boundHealth;
    private Energy _boundEnergy;

    private void OnEnable()
    {
        TryBind();
        Refresh();
    }

    private void Start()
    {
        TryBind();
        Refresh();
    }

    private void LateUpdate()
    {
        if (_boundHealth != null && _boundEnergy != null && _view != null) return;
        TryBind();
        Refresh();
    }

    private void TryBind()
    {
        if (_view == null) _view = GetComponentInChildren<PlayerHudView>(true);
        if (_health == null || _energy == null)
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (LJTest.TestScene scene in root.GetComponentsInChildren<LJTest.TestScene>(true))
                {
                    if (!scene.isActiveAndEnabled || scene.Hero == null) continue;
                    if (_health == null) _health = scene.Hero.GetComponent<Health>();
                    if (_energy == null) _energy = scene.Hero.GetComponentInChildren<Energy>(true);
                    break;
                }
                if (_health != null && _energy != null) break;
            }
        if (_view == null || _health == null || _energy == null) return;
        if (_boundHealth != _health)
        {
            if (_boundHealth != null) _boundHealth.OnHpChanged -= HandleHealthChanged;
            _boundHealth = _health;
            _boundHealth.OnHpChanged += HandleHealthChanged;
        }
        if (_boundEnergy != _energy)
        {
            if (_boundEnergy != null) _boundEnergy.OnEnergyChanged -= HandleEnergyChanged;
            _boundEnergy = _energy;
            _boundEnergy.OnEnergyChanged += HandleEnergyChanged;
        }
    }

    private void OnDisable()
    {
        if (_boundHealth != null)
            _boundHealth.OnHpChanged -= HandleHealthChanged;

        if (_boundEnergy != null)
            _boundEnergy.OnEnergyChanged -= HandleEnergyChanged;

        _boundHealth = null;
        _boundEnergy = null;
    }

    public void Refresh()
    {
        if (_view == null)
            return;

        if (_boundHealth != null)
            _view.SetHealth(
                _boundHealth.CurrentHp,
                _boundHealth.MaxHp);

        if (_boundEnergy != null)
            _view.SetEnergy(
                _boundEnergy.CurrentEnergy,
                _boundEnergy.MaxEnergy);
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (_view != null)
            _view.SetHealth(current, max);
    }

    private void HandleEnergyChanged(float current, float max)
    {
        if (_view != null)
            _view.SetEnergy(current, max);
    }

#if UNITY_EDITOR
    [ContextMenu("HUD 테스트/체력 25 회복")]
    private void TestHeal()
    {
        if (Application.isPlaying && _health != null)
            _health.Heal(25f);
    }

    [ContextMenu("HUD 테스트/에너지 25 충전")]
    private void TestGainEnergy()
    {
        if (Application.isPlaying && _energy != null)
            _energy.Add(25f);
    }

    [ContextMenu("HUD 테스트/에너지 10 소비")]
    private void TestSpendEnergy()
    {
        if (Application.isPlaying && _energy != null)
            _energy.TrySpend(10f, out _);
    }
#endif
}
