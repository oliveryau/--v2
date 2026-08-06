using System;
using UnityEngine;

[Serializable]
public struct VipCompetitorDishOption
{
    public int dishIndex;
    public string displayName;
    public int fightRating;
}

[Serializable]
public class VipCompetitorProfile
{
    public VipCompetitor Competitor;
    [Min(1)] public int TownShopIndex = 1;
    public string RestaurantName;
    public string DisplayName;
    public float RestaurantRating = 3f;
    public VipCompetitorDishOption[] SignatureDishes = Array.Empty<VipCompetitorDishOption>();

    public string GetStealMessageName()
    {
        if (!string.IsNullOrWhiteSpace(DisplayName))
            return DisplayName;

        if (string.IsNullOrWhiteSpace(RestaurantName))
            return string.Empty;

        const string restaurantSuffix = "饭店";

        if (RestaurantName.EndsWith(restaurantSuffix, StringComparison.Ordinal))
            return RestaurantName.Substring(0, RestaurantName.Length - restaurantSuffix.Length);

        return RestaurantName;
    }
}
