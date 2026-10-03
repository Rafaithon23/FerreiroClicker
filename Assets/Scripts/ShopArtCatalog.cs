using UnityEngine;

// Serialized references keep the same shop art available in editor and player builds.
public class ShopArtCatalog : ScriptableObject
{
    public Sprite windowSprite, cardSprite, categoryPlaque, iconFrame, lockSprite, closeSprite;
    public Font titleFont;
    public Sprite[] icons;
    public string[] upgradeIds;

    public Sprite FindIcon(string upgradeId)
    {
        if (upgradeIds == null || icons == null) return null;
        for (int i = 0; i < upgradeIds.Length && i < icons.Length; i++)
            if (upgradeIds[i] == upgradeId) return icons[i];
        return null;
    }
}
