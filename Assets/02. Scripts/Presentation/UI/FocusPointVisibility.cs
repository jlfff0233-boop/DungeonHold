using UnityEngine;
using UnityEngine.UI;

// Only controls the crosshair image; menu owners retain control of its GameObject.
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class FocusPointVisibility : MonoBehaviour
{
    [SerializeField] private WaveController _waveController;
    [SerializeField] private PauseMenuController _pauseMenu;
    private Image _image;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _image.enabled = false;
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private void OnDisable()
    {
        if (_image != null) _image.enabled = false;
    }

    private void Refresh()
    {
        if (_image == null) return;
        _image.enabled = _waveController != null &&
            _waveController.isActiveAndEnabled &&
            _waveController.IsWaveRunning &&
            !_waveController.HasStoppedForStageResult &&
            !_waveController.HasCompletedAllWaves &&
            (_pauseMenu == null || !_pauseMenu.IsPaused) &&
            Time.timeScale > 0f &&
            Cursor.lockState == CursorLockMode.Locked &&
            !Cursor.visible;
    }
}
