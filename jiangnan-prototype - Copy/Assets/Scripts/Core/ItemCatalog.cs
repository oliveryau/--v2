using System;
using UnityEngine;

[Serializable]
public class ShopItemDefinition
{
    public string Name = "Item";
    [Min(0)] public int Cost;
    [Min(0)] public int Worth;
    public Sprite Image;
}

[CreateAssetMenu(fileName = "ItemCatalog", menuName = "Jiangnan/Item Catalog")]
public class ItemCatalog : ScriptableObject
{
    [SerializeField] private ShopItemDefinition[] _items = Array.Empty<ShopItemDefinition>();

    public ShopItemDefinition[] Items => _items;

    public int ItemCount => _items != null ? _items.Length : 0;

    public bool TryGetItem(int index, out ShopItemDefinition item)
    {
        item = null;

        if (_items == null || index < 0 || index >= _items.Length)
            return false;

        item = _items[index];
        return item != null;
    }

    public bool ContainsItem(string itemName)
    {
        return TryGetItemByName(itemName, out _);
    }

    public bool TryGetItemByName(string itemName, out ShopItemDefinition item)
    {
        item = null;

        if (_items == null || string.IsNullOrWhiteSpace(itemName))
            return false;

        string trimmed = itemName.Trim();
        for (int i = 0; i < _items.Length; i++)
        {
            ShopItemDefinition candidate = _items[i];
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.Name))
                continue;

            if (string.Equals(candidate.Name.Trim(), trimmed, StringComparison.Ordinal))
            {
                item = candidate;
                return true;
            }
        }

        return false;
    }
}
