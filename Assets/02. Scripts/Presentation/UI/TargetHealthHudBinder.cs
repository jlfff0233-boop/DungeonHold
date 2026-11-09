using UnityEngine;

public class TargetHealthHudBinder : MonoBehaviour
{
    [Header("실제 대상")]
    [SerializeField] private Health _health;
    [SerializeField] private StatusEffectController _statusEffects;

    [Header("체력 UI")]
    [SerializeField] private WorldHealthView _view;
    [SerializeField] private GameObject _healthRoot;

    [Header("상태 표시 - 없으면 비워두기")]
    [SerializeField] private GameObject _burnIndicator;
    [SerializeField] private GameObject _slowIndicator;
    [SerializeField] private GameObject _stunIndicator;

    [Header("머리 위 UI에만 사용")]
    [SerializeField] private bool _faceCamera;
    [SerializeField] private Camera _camera;

    private void LateUpdate()
    {
        bool alive =
            _health != null &&
            _health.isActiveAndEnabled &&
            _health.gameObject.activeInHierarchy &&
            _health.IsAlive;

        if (_health != null && _view != null)
        {
            _view.SetHealth(_health.CurrentHp, _health.MaxHp);
        }

        SetActiveIfChanged(_healthRoot, alive);

        bool canShowEffects =
            alive &&
            _statusEffects != null &&
            _statusEffects.isActiveAndEnabled;

        SetActiveIfChanged(
            _burnIndicator,
            canShowEffects &&
            _statusEffects.HasEffect(StatusEffectType.Burn));

        SetActiveIfChanged(
            _slowIndicator,
            canShowEffects &&
            _statusEffects.HasEffect(StatusEffectType.Slow));

        SetActiveIfChanged(
            _stunIndicator,
            canShowEffects &&
            _statusEffects.HasEffect(StatusEffectType.Stun));

        if (_faceCamera)
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                transform.rotation = _camera.transform.rotation;
            }
        }
    }

    private void OnDisable()
    {
        SetActiveIfChanged(_healthRoot, false);
        SetActiveIfChanged(_burnIndicator, false);
        SetActiveIfChanged(_slowIndicator, false);
        SetActiveIfChanged(_stunIndicator, false);
    }

    private static void SetActiveIfChanged(
        GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}