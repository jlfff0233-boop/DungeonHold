using UnityEngine;

[CreateAssetMenu(
    fileName = "InstallationEconomyData",
    menuName = "KMB/Installation/Economy Data")]
public class InstallationEconomyData : ScriptableObject
{
    // 70이면 실제 지불 비용의 70% 환불
    [SerializeField, Range(0, 100)]
    private int _refundPercent = 70;

    public int RefundPercent => _refundPercent;

    public bool IsValid =>
        _refundPercent >= 0 &&
        _refundPercent <= 100;
}