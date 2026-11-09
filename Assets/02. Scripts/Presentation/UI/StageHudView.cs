using TMPro;
using UnityEngine;

public class StageHudView : MonoBehaviour
{
    [Header("점수 / 재화")]
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _currencyText;

    [Header("스테이지 진행")]
    [SerializeField] private TMP_Text _passedEnemiesText;
    [SerializeField] private GameObject _passedEnemiesRoot;
    [SerializeField] private TMP_Text _timeText;
    [SerializeField] private TMP_Text _waveText;

    public void SetScore(int score)
    {
        if (_scoreText != null) _scoreText.text = Mathf.Max(0, score).ToString("N0");
    }

    public void SetCurrency(int amount)
    {
        if (_currencyText != null) _currencyText.text = Mathf.Max(0, amount).ToString("N0");
    }

    public void SetPassedEnemies(int current, int limit)
    {
        if (_passedEnemiesText != null)
        {
            _passedEnemiesText.text = $"{Mathf.Max(0, current)} / {Mathf.Max(0, limit)}";
        }
    }

    public void SetPassedEnemiesVisible(bool visible)
    {
        if (_passedEnemiesRoot != null) _passedEnemiesRoot.SetActive(visible);
        else if (_passedEnemiesText != null) _passedEnemiesText.gameObject.SetActive(visible);
    }

    // 경과 시간과 카운트다운 모두 외부에서 전달한 초를 그대로 표시합니다.
    public void SetTime(float seconds)
    {
        if (_timeText == null) return;
        if (float.IsNaN(seconds) || float.IsInfinity(seconds)) seconds = 0f;
        double totalSeconds = System.Math.Floor(System.Math.Max(0d, seconds));
        double minutes = System.Math.Floor(totalSeconds / 60d);
        double remainingSeconds = totalSeconds % 60d;
        _timeText.text = $"{minutes:00}:{remainingSeconds:00}";
    }

    public void SetWave(int current, int total)
    {
        if (_waveText != null)
        {
            _waveText.text = $"Wave {Mathf.Max(0, current)} / {Mathf.Max(0, total)}";
        }
    }

    public void ResetView()
    {
        SetScore(0);
        SetCurrency(0);
        SetPassedEnemies(0, 0);
        SetPassedEnemiesVisible(true);
        SetTime(0f);
        SetWave(0, 0);
    }
}

