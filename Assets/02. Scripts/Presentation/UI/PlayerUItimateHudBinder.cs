using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerUltimateHudBinder : MonoBehaviour
{
    [SerializeField] private Hero _hero;
    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _cooldownText;

    private void LateUpdate()
    {
        if (_hero == null || _cooldownText == null)
            return;

        SkillCaster caster = _hero.UltimateCaster;

        if (caster == null)
        {
            Show("연결 필요", Color.gray);
            return;
        }

        if (caster.IsBusy)
        {
            Show("사용 중", new Color32(255, 211, 90, 255));
        }
        else if (caster.RemainingCooldown > 0f)
        {
            Show(
                Mathf.CeilToInt(caster.RemainingCooldown).ToString(),
                Color.gray);
        }
        else if (_hero.CanUseUltimate)
        {
            Show("준비", Color.white);
        }
        else
        {
            Show("사용 불가", Color.gray);
        }
    }

    private void Show(string text, Color color)
    {
        _cooldownText.text = text;

        if (_icon != null)
            _icon.color = color;
    }
}
