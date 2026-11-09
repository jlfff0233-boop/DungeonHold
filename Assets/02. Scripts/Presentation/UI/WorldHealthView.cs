using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldHealthView : MonoBehaviour
{
    [Header("대상 체력")]
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private Image _healthFill;
    [SerializeField] private TMP_Text _healthText;
    private readonly HealthFillTween _healthTween = new HealthFillTween();

    public void SetName(string targetName)
    {
        if (_nameText != null) _nameText.text = targetName ?? string.Empty;
    }

    public void SetHealth(float current, float max)
    {
        max = IsFinite(max) ? Mathf.Max(0f, max) : 0f;
        current = IsFinite(current) ? Mathf.Clamp(current, 0f, max) : 0f;
        if (max <= 0f) _healthTween.Reset();
        _healthTween.Set(_healthFill, max > 0f ? current / max : 0f);
        if (_healthText != null) _healthText.text = $"{current:0} / {max:0}";
    }

    public void SetColor(Color color)
    {
        if (_healthFill != null) _healthFill.color = color;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void ResetView()
    {
        _healthTween.Reset();
        SetName(string.Empty);
        SetHealth(0f, 0f);
    }

    private void OnDisable() => _healthTween.Reset();

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
