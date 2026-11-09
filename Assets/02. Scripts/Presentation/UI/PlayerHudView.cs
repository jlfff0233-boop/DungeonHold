using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHudView : MonoBehaviour
{
    [Header("캐릭터")]
    [SerializeField] private TMP_Text _characterNameText;
    [SerializeField] private Image _portraitImage;

    [Header("체력 / 에너지")]
    [SerializeField] private Image _healthFill;
    [SerializeField] private TMP_Text _healthText;
    [SerializeField] private Image _energyFill;
    [SerializeField] private TMP_Text _energyText;
    private readonly HealthFillTween _healthTween = new HealthFillTween();

    [Header("무기")]
    [SerializeField] private Image _weaponIcon;
    [SerializeField] private TMP_Text _weaponNameText;

    [Header("설치물 - 같은 인덱스가 같은 슬롯")]
    [SerializeField] private Image[] _installationIcons = new Image[0];
    [SerializeField] private TMP_Text[] _installationCostTexts = new TMP_Text[0];
    [SerializeField] private GameObject[] _installationHighlights = new GameObject[0];

    public void SetCharacter(string characterName, Sprite portrait)
    {
        if (_characterNameText != null) _characterNameText.text = characterName ?? string.Empty;
        SetIcon(_portraitImage, portrait);
    }

    public void SetHealth(float current, float max)
    {
        max = IsFinite(max) ? Mathf.Max(0f, max) : 0f;
        current = IsFinite(current) ? Mathf.Clamp(current, 0f, max) : 0f;
        if (max <= 0f) _healthTween.Reset();
        _healthTween.Set(_healthFill, max > 0f ? current / max : 0f);
        if (_healthText != null) _healthText.text = $"{current:0} / {max:0}";
    }

    public void SetEnergy(float current, float max)
    {
        SetGauge(_energyFill, _energyText, current, max);
    }

    public void SetWeapon(string weaponName, Sprite icon)
    {
        if (_weaponNameText != null) _weaponNameText.text = weaponName ?? string.Empty;
        SetIcon(_weaponIcon, icon);
    }

    public void SetInstallationSlot(int index, Sprite icon, int cost, bool selected)
    {
        if (index < 0 || index >= _installationIcons.Length)
        {
            Debug.LogWarning($"설치물 슬롯 인덱스가 유효하지 않습니다: {index}", this);
            return;
        }

        SetIcon(_installationIcons[index], icon);
        if (index < _installationCostTexts.Length && _installationCostTexts[index] != null)
        {
            _installationCostTexts[index].text = icon == null ? string.Empty : Mathf.Max(0, cost).ToString("N0");
        }
        if (index < _installationHighlights.Length && _installationHighlights[index] != null)
        {
            _installationHighlights[index].SetActive(selected && icon != null);
        }
    }

    // -1이면 선택 표시를 모두 해제합니다.
    public void SetSelectedInstallation(int index)
    {
        for (int i = 0; i < _installationHighlights.Length; i++)
        {
            if (_installationHighlights[i] != null)
            {
                bool hasIcon = i < _installationIcons.Length && _installationIcons[i] != null
                    && _installationIcons[i].sprite != null;
                _installationHighlights[i].SetActive(i == index && hasIcon);
            }
        }
    }

    public void ResetView()
    {
        _healthTween.Reset();
        SetCharacter(string.Empty, null);
        SetHealth(0f, 0f);
        SetEnergy(0f, 0f);
        SetWeapon(string.Empty, null);
        for (int i = 0; i < _installationIcons.Length; i++)
        {
            SetInstallationSlot(i, null, 0, false);
        }
        SetSelectedInstallation(-1);
    }

    private static void SetIcon(Image image, Sprite sprite)
    {
        if (image == null) return;
        image.sprite = sprite;
        image.enabled = sprite != null;
    }

    private static void SetGauge(Image fill, TMP_Text text, float current, float max)
    {
        max = IsFinite(max) ? Mathf.Max(0f, max) : 0f;
        current = IsFinite(current) ? Mathf.Clamp(current, 0f, max) : 0f;
        if (fill != null) fill.fillAmount = max > 0f ? current / max : 0f;
        if (text != null) text.text = $"{current:0} / {max:0}";
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void OnDisable() => _healthTween.Reset();
}
