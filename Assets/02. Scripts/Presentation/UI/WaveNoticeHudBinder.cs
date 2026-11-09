using TMPro;
using UnityEngine;

public class WaveNoticeHudBinder : MonoBehaviour
{
    [SerializeField] private WaveController _waveController;
    [SerializeField] private GameObject _noticeRoot;
    [SerializeField] private TMP_Text _noticeText;
    [SerializeField] private float _displaySeconds = 2f;

    private WaveController _boundWave;
    private float _remainingTime;

    private void OnEnable()
    {
        HideNotice();

        if (_waveController == null ||
            _noticeRoot == null ||
            _noticeText == null)
        {
            Debug.LogWarning(
                "웨이브와 안내 UI를 연결하세요.", this);
            return;
        }

        _boundWave = _waveController;
        _boundWave.OnWaveStarted += HandleWaveStarted;
        _boundWave.OnWaveCompleted += HandleWaveCompleted;
        _boundWave.OnAllWavesCompleted += HandleAllWavesCompleted;
    }

    private void Update()
    {
        if (_remainingTime <= 0f)
            return;

        _remainingTime -= Time.deltaTime;

        if (_remainingTime <= 0f)
        {
            HideNotice();
        }
    }

    private void HandleWaveStarted(int waveNumber)
    {
        ShowNotice($"웨이브 {waveNumber} 시작");
    }

    private void HandleWaveCompleted(int waveNumber)
    {
        ShowNotice($"웨이브 {waveNumber} 종료");
    }

    private void HandleAllWavesCompleted()
    {
        ShowNotice("모든 웨이브 종료");
    }

    private void ShowNotice(string message)
    {
        if (_noticeRoot == null || _noticeText == null)
            return;

        _noticeText.text = message;
        _remainingTime = Mathf.Max(0.1f, _displaySeconds);
        _noticeRoot.SetActive(true);
    }

    private void HideNotice()
    {
        _remainingTime = 0f;

        if (_noticeRoot != null)
        {
            _noticeRoot.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (_boundWave != null)
        {
            _boundWave.OnWaveStarted -= HandleWaveStarted;
            _boundWave.OnWaveCompleted -= HandleWaveCompleted;
            _boundWave.OnAllWavesCompleted -= HandleAllWavesCompleted;
        }

        _boundWave = null;
        HideNotice();
    }
}
