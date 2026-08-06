using UnityEngine;

[CreateAssetMenu(fileName = "VipCompetitorCatalog", menuName = "Jiangnan/VIP Competitor Catalog")]
public class VipCompetitorCatalog : ScriptableObject
{
    [SerializeField] private VipCompetitorProfile[] _profiles = CreateDefaultProfiles();

    public VipCompetitorProfile[] Profiles => _profiles;

    public void ConfigureSelection()
    {
        CompetitorSceneSelection.Configure(_profiles);
    }

    public static VipCompetitorCatalog LoadOrCreateDefault()
    {
        VipCompetitorCatalog catalog = Resources.Load<VipCompetitorCatalog>("VipCompetitorCatalog");

        if (catalog != null)
            return catalog;

        catalog = CreateInstance<VipCompetitorCatalog>();
        catalog._profiles = CreateDefaultProfiles();
        return catalog;
    }

    private void OnValidate()
    {
        if (_profiles == null || _profiles.Length == 0)
            _profiles = CreateDefaultProfiles();
    }

    private static VipCompetitorProfile[] CreateDefaultProfiles()
    {
        return new[]
        {
            new VipCompetitorProfile
            {
                Competitor = VipCompetitor.DaiWei,
                TownShopIndex = 1,
                RestaurantName = "戴威饭店",
                DisplayName = "戴威",
                RestaurantRating = 4.4f,
                SignatureDishes = new[]
                {
                    new VipCompetitorDishOption { dishIndex = 7, displayName = "北京烧鸭", fightRating = 78 },
                    new VipCompetitorDishOption { dishIndex = 1, displayName = "叫花鸡", fightRating = 85 }
                }
            },
            new VipCompetitorProfile
            {
                Competitor = VipCompetitor.JiaHeng,
                TownShopIndex = 2,
                RestaurantName = "嘉恒饭店",
                DisplayName = "嘉恒",
                RestaurantRating = 3.7f,
                SignatureDishes = new[]
                {
                    new VipCompetitorDishOption { dishIndex = 8, displayName = "佛跳墙", fightRating = 73 },
                    new VipCompetitorDishOption { dishIndex = 6, displayName = "红烧肉", fightRating = 68 }
                }
            },
            new VipCompetitorProfile
            {
                Competitor = VipCompetitor.ChunHua,
                TownShopIndex = 3,
                RestaurantName = "春华饭店",
                DisplayName = "春华",
                RestaurantRating = 3.9f,
                SignatureDishes = new[]
                {
                    new VipCompetitorDishOption { dishIndex = 0, displayName = "东坡肉", fightRating = 88 },
                    new VipCompetitorDishOption { dishIndex = 6, displayName = "鱼", fightRating = 70 }
                }
            },
            new VipCompetitorProfile
            {
                Competitor = VipCompetitor.HongJie,
                TownShopIndex = 4,
                RestaurantName = "红姐饭店",
                DisplayName = "红姐",
                RestaurantRating = 4.1f,
                SignatureDishes = new[]
                {
                    new VipCompetitorDishOption { dishIndex = 7, displayName = "麻婆豆腐", fightRating = 74 },
                    new VipCompetitorDishOption { dishIndex = 8, displayName = "包子", fightRating = 66 }
                }
            }
        };
    }
}
