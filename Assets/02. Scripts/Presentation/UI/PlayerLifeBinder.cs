using TMPro;
using UnityEngine;

public class PlayerLifeHudBinder : MonoBehaviour
{
    [SerializeField] private Hero _hero;
    [SerializeField] private Health _health;
    [SerializeField] private PlayerReviveData _reviveData;

    [SerializeField] private GameObject _noticeRoot;
    [SerializeField] private TMP_Text _noticeText;

    private void LateUpdate()
    {
        if (_hero == null ||
            _health == null ||
            _reviveData == null ||
            _noticeRoot == null ||
            _noticeText == null)
        {
            return;
        }

        string message = "";

        if (!_health.IsAlive)
        {
            if (_hero.IsWaitingRevive)
            {
                int seconds = Mathf.CeilToInt(
                    Mathf.Max(0f, _hero.RemainingReviveTime));

                message = $"부활까지 {seconds}초";
            }
            else if (_hero.UsedReviveCount >= _reviveData.MaxCount)
            {
                message = "사망 — 자동 부활 횟수 소진";
            }
            else
            {
                message = "사망";
            }
        }

        _noticeText.text = message;

        bool visible = message.Length > 0;

        if (_noticeRoot.activeSelf != visible)
        {
            _noticeRoot.SetActive(visible);
        }
    }
}
