using TMPro;
using UnityEngine;

public class WaveSummaryHudBinder : MonoBehaviour
{
    [SerializeField] private WaveController _waveController;
    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _killCountText;
    [SerializeField] private TMP_Text _passedCountText;
    [SerializeField] private GameObject _hudRoot;

    private bool _isOpen;
    private bool _hudWasActive;
    private CursorLockMode _previousCursorLock;
    private bool _previousCursorVisible;

    private WaveController _boundWave;

    public GameObject ResultPanel => _panel;
    public bool IsOpen => _isOpen;

    private void LateUpdate()
    {
        if (!_isOpen) return;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnEnable()
    {
        Close();

        if (_waveController == null || _panel == null)
        {
            Debug.LogWarning(
                "웨이브와 결과 패널 참조를 연결하세요.", this);
            return;
        }

        _boundWave = _waveController;
        _boundWave.OnAllWavesCompleted += ShowSummary;

        if (_boundWave.HasCompletedAllWaves)
        {
            ShowSummary();
        }
    }

    private void ShowSummary()
    {
        if (_boundWave == null)
            return;

        int kills = _boundWave.DiedEnemyCount;
        int passed = _boundWave.PassedEnemyCount;

        if (_killCountText != null)
            _killCountText.text = kills.ToString("N0");
        if (_passedCountText != null)
            _passedCountText.text = passed.ToString("N0");

        if (!_isOpen)
        {
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _hudWasActive =
                _hudRoot != null && _hudRoot.activeSelf;

            _isOpen = true;
        }

        if (_hudRoot != null)
        {
            _hudRoot.SetActive(false);
        }

        _panel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Close()
    {
        if (_panel != null)
        {
            _panel.SetActive(false);
        }

        if (_isOpen && _hudRoot != null)
        {
            _hudRoot.SetActive(_hudWasActive);
        }

        if (_isOpen)
        {
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
        }

        _isOpen = false;
    }

    private void OnDisable()
    {
        if (_boundWave != null)
        {
            _boundWave.OnAllWavesCompleted -= ShowSummary;
        }

        _boundWave = null;
        Close();
    }
}