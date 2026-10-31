using UnityEngine;

public enum ItemComponentType
{
    None,
    Sword,      // +15 AD
    Vest,       // +20 Armor / +200 HP
    Rod         // +20 Ability Power / +15% AS
}

[CreateAssetMenu(fileName = "NewItem", menuName = "AutoBattler/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Item Info")]
    public string itemName;
    public string itemCode; // e.g. "Sword", "Vest", "GA", "IE"
    public Sprite icon;
    [TextArea(2, 3)]
    public string description;

    [Header("Crafting / Component Classification")]
    public bool isCompletedItem = false;
    public ItemComponentType componentType = ItemComponentType.None;

    [Header("Stat Modifiers")]
    public int bonusAttackDamage = 0;
    public int bonusHealth = 0;
    public float bonusAttackSpeed = 0f; // Multiplier (e.g. 0.20 for +20%)
    public int flatDamageReduction = 0;
}