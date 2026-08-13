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
                ChaseThresholdMinSeconds = 6f,
                ChaseThresholdMaxSeconds = 8f
            },
            new VipCompetitorProfile
            {
                Competitor = VipCompetitor.HanXi,
                TownShopIndex = 2,
                RestaurantName = "韩熙饭店",
                DisplayName = "韩熙",
                RestaurantRating = 3.7f,
                ChaseThresholdMinSeconds = 1f,
                ChaseThresholdMaxSeconds = 2f
            },
            new VipCompetitorProfile
            {
                Competitor = VipCompetitor.ChunHua,
                TownShopIndex = 3,
                RestaurantName = "春华饭店",
                DisplayName = "春华",
                RestaurantRating = 3.9f,
                ChaseThresholdMinSeconds = 4f,
                ChaseThresholdMaxSeconds = 5f
            },
            new VipCompetitorProfile
            {
                Competitor = VipCompetitor.HongJie,
                TownShopIndex = 4,
                RestaurantName = "红姐饭店",
                DisplayName = "红姐",
                RestaurantRating = 4.1f,
                ChaseThresholdMinSeconds = 10f,
                ChaseThresholdMaxSeconds = 11f
            }
        };
    }
}
