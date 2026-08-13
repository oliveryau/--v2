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

    public static ItemCatalog LoadOrCreateDefault()
    {
        ItemCatalog catalog = Resources.Load<ItemCatalog>("ItemCatalog");

        if (catalog != null)
            return catalog;

        return CreateInstance<ItemCatalog>();
    }
}
