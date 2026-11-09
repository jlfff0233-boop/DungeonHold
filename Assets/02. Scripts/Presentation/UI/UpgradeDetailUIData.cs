using System;
using UnityEngine;

[CreateAssetMenu(
    fileName = "UpgradeUI_",
    menuName = "UI/Upgrade Detail Data")]
public class UpgradeDetailUIData : ScriptableObject
{
    [Serializable]
    public class CardData
    {
        public UpgradeData Upgrade;

        [TextArea(2, 4)]
        public string EffectText;
    }

    public string NameText;
    public string TypeText;

    [TextArea(2, 5)]
    public string DescriptionText;

    public Sprite Artwork;

    public CardData FirstCard = new CardData();
    public CardData SecondCard = new CardData();
}
