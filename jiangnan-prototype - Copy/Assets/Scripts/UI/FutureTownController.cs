using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FutureTownController : MonoBehaviour
{
    private const string TownBgName = "Town Bg";
    private const string ShopBgName = "Shop Bg";
    private const string HomeButtonName = "Home Button";
    private const string TownButtonName = "Town Button";
    private const string ShopUiName = "Shop UI";

    [Serializable]
    public class FutureShopEntry
    {
        [SerializeField] private GameObject _enterShopRoot;
        [SerializeField] private ItemCatalog _catalog;
        [SerializeField] private Sprite _shopBackground;

        public GameObject EnterShopRoot => _enterShopRoot;
        public ItemCatalog Catalog => _catalog;
        public Sprite ShopBackground => _shopBackground;

        public void BindRoot(GameObject root)
        {
            if (_enterShopRoot == null)
                _enterShopRoot = root;
        }
    }

    [SerializeField] private GameObject _townBg;
    [SerializeField] private GameObject _shopBg;
    [SerializeField] private Image _shopBgImage;
    [SerializeField] private MysteryMerchantShopController _shopController;
    [SerializeField] private GameObject _homeButtonRoot;
    [SerializeField] private Button _townButton;
    [SerializeField] private FutureShopEntry[] _shops;
    [SerializeField] private float _enterShopPulseMinScale = 0.95f;
    [SerializeField] private float _enterShopPulseMaxScale = 1.05f;
    [SerializeField] private float _enterShopPulseSpeed = 4f;

    private readonly List<Button> _enterShopButtons = new();

    private void Awake()
    {
        if (!RestaurantSceneMode.IsFutureScene)
        {
            enabled = false;
            return;
        }

        ResolveReferences();
        WireEnterShopButtons();
        WireTownButton();
        ShowTownView();
    }

    private void Update()
    {
        if (!enabled || _townBg == null || !_townBg.activeSelf || _shops == null)
            return;

        float pulseScale = GetPulseScale(_enterShopPulseMinScale, _enterShopPulseMaxScale, _enterShopPulseSpeed);
        for (int i = 0; i < _shops.Length; i++)
        {
            FutureShopEntry shop = _shops[i];
            if (shop?.EnterShopRoot == null)
                continue;

            RectTransform root = shop.EnterShopRoot.transform as RectTransform;
            if (root == null)
                continue;

            bool soldOut = _shopController != null && _shopController.IsShopSoldOut(i);
            root.localScale = Vector3.one * (soldOut ? 1f : pulseScale);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeEnterShopButtons();
        if (_townButton != null)
            _townButton.onClick.RemoveListener(HandleTownButtonClicked);
    }

    private void ResolveReferences()
    {
        if (_townBg == null)
            _townBg = FindNamedUi(TownBgName);

        if (_shopBg == null)
            _shopBg = FindNamedUi(ShopBgName);

        if (_shopBgImage == null && _shopBg != null)
            _shopBgImage = _shopBg.GetComponent<Image>();

        if (_shopController == null)
        {
            GameObject shopUi = FindNamedUi(ShopUiName);
            if (shopUi != null)
                _shopController = shopUi.GetComponent<MysteryMerchantShopController>();
        }

        if (_homeButtonRoot == null)
            _homeButtonRoot = FindNamedUi(HomeButtonName);

        if (_townButton == null)
        {
            GameObject townButtonObject = FindNamedUi(TownButtonName);
            if (townButtonObject != null)
                _townButton = townButtonObject.GetComponent<Button>();
        }

        ResolveShopRoots();
    }

    private void ResolveShopRoots()
    {
        if (_townBg == null)
            return;

        if (_shops == null || _shops.Length == 0)
        {
            Transform townTransform = _townBg.transform;
            int shopCount = 0;
            for (int i = 0; i < townTransform.childCount; i++)
            {
                Transform child = townTransform.GetChild(i);
                if (child != null && child.name.StartsWith("EnterShop", StringComparison.OrdinalIgnoreCase))
                    shopCount++;
            }

            _shops = new FutureShopEntry[shopCount];
            int writeIndex = 0;
            for (int i = 0; i < townTransform.childCount && writeIndex < shopCount; i++)
            {
                Transform child = townTransform.GetChild(i);
                if (child == null || !child.name.StartsWith("EnterShop", StringComparison.OrdinalIgnoreCase))
                    continue;

                _shops[writeIndex] = new FutureShopEntry();
                _shops[writeIndex].BindRoot(child.gameObject);
                writeIndex++;
            }

            return;
        }

        for (int i = 0; i < _shops.Length; i++)
        {
            FutureShopEntry shop = _shops[i];
            if (shop == null || shop.EnterShopRoot != null)
                continue;

            string objectName = $"EnterShop ({i + 1})";
            Transform child = _townBg.transform.Find(objectName);
            if (child != null)
                shop.BindRoot(child.gameObject);
        }
    }

    private void WireEnterShopButtons()
    {
        UnsubscribeEnterShopButtons();
        _enterShopButtons.Clear();

        if (_shops == null)
            return;

        for (int i = 0; i < _shops.Length; i++)
        {
            FutureShopEntry shop = _shops[i];
            if (shop?.EnterShopRoot == null)
                continue;

            Button button = EnsureButton(shop.EnterShopRoot);
            if (button == null)
                continue;

            int shopIndex = i;
            button.onClick.AddListener(() => HandleEnterShopClicked(shopIndex));
            _enterShopButtons.Add(button);
        }
    }

    private void UnsubscribeEnterShopButtons()
    {
        for (int i = 0; i < _enterShopButtons.Count; i++)
        {
            if (_enterShopButtons[i] != null)
                _enterShopButtons[i].onClick.RemoveAllListeners();
        }
    }

    private void WireTownButton()
    {
        if (_townButton == null)
            return;

        _townButton.onClick.RemoveListener(HandleTownButtonClicked);
        _townButton.onClick.AddListener(HandleTownButtonClicked);
    }

    private void HandleEnterShopClicked(int shopIndex)
    {
        if (_shops == null || shopIndex < 0 || shopIndex >= _shops.Length)
            return;

        FutureShopEntry shop = _shops[shopIndex];
        ApplyShopAppearance(shop);
        ShowShopView();

        if (_shopController != null)
            _shopController.OpenShop(shop != null ? shop.Catalog : null, shopIndex);
    }

    private void HandleTownButtonClicked()
    {
        ShowTownView();
    }

    private void ShowTownView()
    {
        SetActiveSafe(_townBg, true);
        SetActiveSafe(_shopBg, false);
        SetActiveSafe(_homeButtonRoot, true);
        SetActiveSafe(_townButton != null ? _townButton.gameObject : null, false);
    }

    private void ShowShopView()
    {
        SetActiveSafe(_townBg, false);
        SetActiveSafe(_shopBg, true);
        SetActiveSafe(_homeButtonRoot, false);
        SetActiveSafe(_townButton != null ? _townButton.gameObject : null, true);
    }

    private void ApplyShopAppearance(FutureShopEntry shop)
    {
        if (shop == null || _shopBgImage == null)
            return;

        if (shop.ShopBackground != null)
            _shopBgImage.sprite = shop.ShopBackground;

        _shopBgImage.color = Color.white;
        _shopBgImage.enabled = _shopBgImage.sprite != null;
    }

    private static void SetActiveSafe(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private static Button EnsureButton(GameObject target)
    {
        if (target == null)
            return null;

        Button button = target.GetComponent<Button>();
        if (button != null)
            return button;

        button = target.AddComponent<Button>();
        Image image = target.GetComponent<Image>();
        if (image != null)
            button.targetGraphic = image;

        return button;
    }

    private static float GetPulseScale(float minScale, float maxScale, float speed)
    {
        if (speed <= 0f)
            return 1f;

        float pulseT = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        return Mathf.Lerp(minScale, maxScale, pulseT);
    }

    private static GameObject FindNamedUi(string objectName)
    {
        GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate != null && string.Equals(candidate.name, objectName, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }
}
