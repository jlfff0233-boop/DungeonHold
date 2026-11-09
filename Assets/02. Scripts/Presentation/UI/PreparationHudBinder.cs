using UnityEngine;
using UnityEngine.UI;

public class PreparationHudBinder : MonoBehaviour
{
    [SerializeField] private WaveController _waveController;
    [SerializeField] private GameObject _hintRoot;
    [SerializeField] private Button _startButton;

    private void LateUpdate()
    {
        Refresh();
    }

    public void RequestWaveStart()
    {
        if (_waveController == null ||
            !_waveController.isActiveAndEnabled)
        {
            return;
        }

        bool started = _waveController.TryStartWave();

        if (started)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        bool preparing =
            _waveController != null &&
            _waveController.isActiveAndEnabled &&
            _waveController.IsPreparing;

        if (_startButton != null)
        {
            _startButton.interactable = preparing;
        }

        if (_hintRoot != null &&
            _hintRoot.activeSelf != preparing)
        {
            _hintRoot.SetActive(preparing);
        }
    }
}