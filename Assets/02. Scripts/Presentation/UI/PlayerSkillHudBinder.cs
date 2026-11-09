using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSkillHudBinder : MonoBehaviour
{
    [SerializeField] private Hero _hero;
    [SerializeField] private SkillCaster _skillCaster;

    [SerializeField] private Image _icon;
    [SerializeField] private TMP_Text _cooldownText;

    private void LateUpdate()
    {
        if (_hero == null ||
            _skillCaster == null ||
            _cooldownText == null)
        {
            return;
        }

        string text;
        Color color;

        if (_skillCaster.IsBusy)
        {
            text = "";
            color = Color.white;
        }
        else if (_skillCaster.RemainingCooldown > 0f)
        {
            text = Mathf.CeilToInt(
                _skillCaster.RemainingCooldown).ToString();

            color = new Color32(110, 110, 110, 255);
        }
        else if (_hero.CanUseSkill)
        {
            text = "준비";
            color = Color.white;
        }
        else
        {
            text = "사용 불가";
            color = new Color32(110, 110, 110, 255);
        }

        _cooldownText.text = text;

        if (_icon != null)
        {
            _icon.color = color;
        }
    }
}
