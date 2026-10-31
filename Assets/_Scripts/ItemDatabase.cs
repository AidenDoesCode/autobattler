using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ItemDatabase", menuName = "AutoBattler/Item Database")]
public class ItemDatabase : ScriptableObject
{
    [System.Serializable]
    public struct Recipe
    {
        public ItemData componentA;
        public ItemData componentB;
        public ItemData result;
    }

    public List<ItemData> allItems = new List<ItemData>();
    public List<Recipe> recipes = new List<Recipe>();

    /// <summary>
    /// Checks recipe book for two components and returns the crafted completed item.
    /// </summary>
    public ItemData CombineItems(ItemData a, ItemData b)
    {
        if (a == null || b == null) return null;
        if (a.isCompletedItem || b.isCompletedItem) return null; // Only basic components fuse

        foreach (var r in recipes)
        {
            if ((r.componentA == a && r.componentB == b) || (r.componentA == b && r.componentB == a))
            {
                return r.result;
            }
        }

        return null;
    }
}