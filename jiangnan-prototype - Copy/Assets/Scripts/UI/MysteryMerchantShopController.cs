using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MysteryMerchantShopController : MonoBehaviour
{
    private const string ShopUiName = "Shop UI";
    private const string MerchantUiName = "Merchant UI";
    private const string DialogueName = "Dialogue";
    private const string ItemListName = "Item List";
    private const string ItemTitleName = "Item Title";
    private const string ItemImageName = "Item Image";
    private const string BuyUiName = "Buy UI";
    private const string CostName = "Cost";
    private const string WelcomeDialogue = "欢迎！来尝尝菜品吧！";
    private const string ThanksDialogue = "感谢！下次再来！";
    private const int MinShopItemCount = 2;
    private const int MaxShopItemCount = 3;

    [SerializeField] private ItemCatalog _itemCatalog;
    [SerializeField] private Transform _itemListRoot;
    [SerializeField] private TextMeshProUGUI _merchantDialogueText;

    private readonly List<ShopSlotUi> _slots = new();
    private readonly List<int> _catalogIndexScratch = new();
    private bool _purchasedThisVisit;

    private sealed class ShopSlotUi
    {
        public GameObject Root;
        public TextMeshProUGUI TitleText;
        public Image ItemImage;
        public Button BuyButton;
        public RectTransform BuyUiRoot;
        public TextMeshProUGUI CostText;
        public ShopItemDefinition OfferedItem;
        public bool Sold;
    }

    private void Awake()
    {
        EnsureCatalog();
        CacheSlots();
        EnsureDialogue();
        // Each Future scene visit starts with the welcome line; thanks only after a buy this visit.
        _purchasedThisVisit = false;
        SetMerchantDialogue(WelcomeDialogue);
        RefreshRandomStock();
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            ShopSlotUi slot = _slots[i];
            if (slot?.BuyButton == null)
                continue;

            slot.BuyButton.onClick.RemoveAllListeners();
        }
    }

    private void EnsureCatalog()
    {
        if (_itemCatalog != null)
            return;

        _itemCatalog = ItemCatalog.LoadOrCreateDefault();
    }

    private void EnsureDialogue()
    {
        if (_merchantDialogueText != null)
            return;

        GameObject merchantUi = GameObject.Find(MerchantUiName);
        if (merchantUi == null)
            return;

        Transform dialogue = FindChildTransform(merchantUi.transform, DialogueName);
        if (dialogue != null)
            _merchantDialogueText = dialogue.GetComponent<TextMeshProUGUI>();
    }

    private void SetMerchantDialogue(string text)
    {
        if (_merchantDialogueText != null)
            _merchantDialogueText.text = text ?? string.Empty;
    }

    private void CacheSlots()
    {
        _slots.Clear();

        if (_itemListRoot == null)
        {
            Transform shopUi = transform;
            if (!string.Equals(shopUi.name, ShopUiName, System.StringComparison.OrdinalIgnoreCase))
            {
                GameObject shopObject = GameObject.Find(ShopUiName);
                shopUi = shopObject != null ? shopObject.transform : null;
            }

            if (shopUi != null)
            {
                Transform itemList = shopUi.Find(ItemListName);
                if (itemList == null)
                    itemList = FindDeepChild(shopUi, ItemListName);

                _itemListRoot = itemList;
            }
        }

        if (_itemListRoot == null)
            return;

        for (int i = 0; i < _itemListRoot.childCount; i++)
        {
            Transform itemRoot = _itemListRoot.GetChild(i);
            if (itemRoot == null || !itemRoot.name.StartsWith("Item", System.StringComparison.OrdinalIgnoreCase))
                continue;

            ShopSlotUi slot = BuildSlot(itemRoot);
            if (slot == null)
                continue;

            int slotIndex = _slots.Count;
            if (slot.BuyButton != null)
                slot.BuyButton.onClick.AddListener(() => HandleBuyClicked(slotIndex));

            _slots.Add(slot);
        }
    }

    private static ShopSlotUi BuildSlot(Transform itemRoot)
    {
        if (itemRoot == null)
            return null;

        TextMeshProUGUI title = FindChildComponent<TextMeshProUGUI>(itemRoot, ItemTitleName);
        Image image = FindChildComponent<Image>(itemRoot, ItemImageName);
        Transform buyUi = FindChildTransform(itemRoot, BuyUiName);
        Button buyButton = buyUi != null ? buyUi.GetComponent<Button>() : null;
        TextMeshProUGUI cost = buyUi != null
            ? FindChildComponent<TextMeshProUGUI>(buyUi, CostName)
            : null;

        if (buyButton != null && buyButton.targetGraphic == null && buyUi != null)
        {
            Image background = FindChildComponent<Image>(buyUi, "Background");
            if (background != null)
                buyButton.targetGraphic = background;
        }

        return new ShopSlotUi
        {
            Root = itemRoot.gameObject,
            TitleText = title,
            ItemImage = image,
            BuyButton = buyButton,
            BuyUiRoot = buyUi as RectTransform,
            CostText = cost
        };
    }

    private void RefreshRandomStock()
    {
        EnsureCatalog();

        if (_slots.Count == 0)
            return;

        ShopItemDefinition[] catalogItems = _itemCatalog != null ? _itemCatalog.Items : null;
        int catalogCount = catalogItems != null ? catalogItems.Length : 0;
        if (catalogCount <= 0)
        {
            for (int i = 0; i < _slots.Count; i++)
                SetSlotActive(_slots[i], false);
            return;
        }

        int offerCount = Mathf.Clamp(
            Random.Range(MinShopItemCount, MaxShopItemCount + 1),
            MinShopItemCount,
            Mathf.Min(MaxShopItemCount, _slots.Count, catalogCount));

        _catalogIndexScratch.Clear();
        for (int i = 0; i < catalogCount; i++)
        {
            if (catalogItems[i] != null)
                _catalogIndexScratch.Add(i);
        }

        Shuffle(_catalogIndexScratch);

        for (int i = 0; i < _slots.Count; i++)
        {
            ShopSlotUi slot = _slots[i];
            if (i >= offerCount || i >= _catalogIndexScratch.Count)
            {
                ClearSlot(slot);
                SetSlotActive(slot, false);
                continue;
            }

            ShopItemDefinition item = catalogItems[_catalogIndexScratch[i]];
            ApplyItemToSlot(slot, item);
            SetSlotActive(slot, true);
        }
    }

    private void ApplyItemToSlot(ShopSlotUi slot, ShopItemDefinition item)
    {
        if (slot == null)
            return;

        slot.OfferedItem = item;
        slot.Sold = false;

        if (slot.TitleText != null)
            slot.TitleText.text = item != null ? item.Name : string.Empty;

        if (slot.ItemImage != null)
        {
            slot.ItemImage.sprite = item != null ? item.Image : null;
            slot.ItemImage.enabled = item != null && item.Image != null;
            slot.ItemImage.preserveAspect = true;
        }

        if (slot.CostText != null)
            slot.CostText.text = item != null ? item.Cost.ToString() : "0";

        if (slot.BuyButton != null)
            slot.BuyButton.interactable = item != null;
    }

    private static void ClearSlot(ShopSlotUi slot)
    {
        if (slot == null)
            return;

        slot.OfferedItem = null;
        slot.Sold = false;

        if (slot.TitleText != null)
            slot.TitleText.text = string.Empty;

        if (slot.ItemImage != null)
        {
            slot.ItemImage.sprite = null;
            slot.ItemImage.enabled = false;
        }

        if (slot.CostText != null)
            slot.CostText.text = string.Empty;

        if (slot.BuyButton != null)
            slot.BuyButton.interactable = false;
    }

    private static void SetSlotActive(ShopSlotUi slot, bool active)
    {
        if (slot?.Root != null)
            slot.Root.SetActive(active);
    }

    private void HandleBuyClicked(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count)
            return;

        ShopSlotUi slot = _slots[slotIndex];
        if (slot == null || slot.Sold || slot.OfferedItem == null || slot.Root == null || !slot.Root.activeSelf)
            return;

        int cost = Mathf.Max(0, slot.OfferedItem.Cost);
        if (GoldManager.Instance == null || !GoldManager.Instance.TrySpend(cost))
            return;

        slot.Sold = true;
        if (slot.BuyButton != null)
            slot.BuyButton.interactable = false;

        PlayerProfileStorage.AddBagItemForCurrentPlayer(slot.OfferedItem, 1);
        MissionUiController.NotifyFutureItemPurchased();

        // Purchased stock leaves the shelf until the next visit re-rolls offers.
        SetSlotActive(slot, false);

        if (!_purchasedThisVisit)
        {
            _purchasedThisVisit = true;
            SetMerchantDialogue(ThanksDialogue);
        }
    }

    private static void Shuffle(List<int> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = values[i];
            values[i] = values[j];
            values[j] = temp;
        }
    }

    private static Transform FindDeepChild(Transform root, string objectName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindDeepChild(child, objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private static Transform FindChildTransform(Transform root, string objectName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return FindDeepChild(root, objectName);
    }

    private static T FindChildComponent<T>(Transform root, string objectName) where T : Component
    {
        Transform child = FindChildTransform(root, objectName);
        return child != null ? child.GetComponent<T>() : null;
    }
}
