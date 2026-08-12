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
    [Tooltip("Chase when stay timer remaining is within this range (seconds left).")]
    [Min(0f)] public float ChaseThresholdMinSeconds = 1f;
    [Min(0f)] public float ChaseThresholdMaxSeconds = 5f;
    public Sprite AngryFace;
    public VipCompetitorDishOption[] SignatureDishes = Array.Empty<VipCompetitorDishOption>();

    public void GetChaseThresholdRange(out float minSeconds, out float maxSeconds)
    {
        minSeconds = Mathf.Max(0f, ChaseThresholdMinSeconds);
        maxSeconds = Mathf.Max(minSeconds, ChaseThresholdMaxSeconds);
    }

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
