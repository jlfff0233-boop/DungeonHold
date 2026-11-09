using UnityEngine;

/// <summary>
/// 설치물 슬롯 데이터 - 설치물 종류 선택 요청
/// </summary>
[CreateAssetMenu (
    fileName = "InstallationQuickSlotData" , menuName = "QuickSlot/Installation" )]
public class InstallationQuickSlotData : QuickSlotData
{
    [SerializeField] TrapData _installation;    //선택할 설치물 설정

    /// <summary>
    /// 설치물 설정
    /// </summary>
    public TrapData Installation => _installation;

    /// <inheritdoc />
    public override bool Validate (
        ItemDataMap items , CompanionHireDataMap companions )
    {
        return _installation != null && _installation.IsValid;
    }

    /// <inheritdoc />
    public override bool TryExecute ( QuickSlotContext context )
    {
        return context.TrySelectInstallation ( _installation );
    }
}
