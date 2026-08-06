using System;
using System.Collections.Generic;

public static class CompetitorSceneSelection
{
    private static VipCompetitorProfile[] _profiles = Array.Empty<VipCompetitorProfile>();
    private static readonly Dictionary<VipCompetitor, VipCompetitorProfile> _profileByCompetitor = new();
    private static readonly Dictionary<int, VipCompetitorProfile> _profileByTownShopIndex = new();
    private static VipCompetitor _selectedCompetitor = VipCompetitor.DaiWei;

    public static VipCompetitor SelectedCompetitor => _selectedCompetitor;

    public static void Configure(VipCompetitorProfile[] profiles)
    {
        _profiles = profiles ?? Array.Empty<VipCompetitorProfile>();
        _profileByCompetitor.Clear();
        _profileByTownShopIndex.Clear();

        for (int i = 0; i < _profiles.Length; i++)
        {
            VipCompetitorProfile profile = _profiles[i];

            if (profile == null)
                continue;

            _profileByCompetitor[profile.Competitor] = profile;

            if (profile.TownShopIndex > 0)
                _profileByTownShopIndex[profile.TownShopIndex] = profile;
        }
    }

    public static void SelectFromTownShopIndex(int shopIndex)
    {
        if (_profileByTownShopIndex.TryGetValue(shopIndex, out VipCompetitorProfile profile))
        {
            _selectedCompetitor = profile.Competitor;
            return;
        }

        _selectedCompetitor = VipCompetitor.DaiWei;
    }

    public static VipCompetitor PickRandomStealCompetitor()
    {
        if (_profiles == null || _profiles.Length == 0)
            return VipCompetitor.DaiWei;

        VipCompetitorProfile profile = _profiles[UnityEngine.Random.Range(0, _profiles.Length)];
        return profile != null ? profile.Competitor : VipCompetitor.DaiWei;
    }

    public static bool TryGetProfile(VipCompetitor competitor, out VipCompetitorProfile profile)
    {
        return _profileByCompetitor.TryGetValue(competitor, out profile);
    }

    public static bool TryGetProfileByTownShopIndex(int shopIndex, out VipCompetitorProfile profile)
    {
        return _profileByTownShopIndex.TryGetValue(shopIndex, out profile);
    }

    public static string GetRestaurantName(VipCompetitor competitor)
    {
        return TryGetProfile(competitor, out VipCompetitorProfile profile)
            ? profile.RestaurantName
            : "戴威饭店";
    }

    public static string GetRestaurantName()
    {
        return GetRestaurantName(_selectedCompetitor);
    }

    public static float GetRestaurantRating(VipCompetitor competitor)
    {
        return TryGetProfile(competitor, out VipCompetitorProfile profile)
            ? profile.RestaurantRating
            : 3f;
    }

    public static float GetRestaurantRating()
    {
        return GetRestaurantRating(_selectedCompetitor);
    }

    public static VipCompetitorDishOption[] GetSignatureDishes(VipCompetitor competitor)
    {
        return TryGetProfile(competitor, out VipCompetitorProfile profile)
            ? profile.SignatureDishes
            : Array.Empty<VipCompetitorDishOption>();
    }

    public static string GetCompetitorDisplayName(VipCompetitor competitor)
    {
        return TryGetProfile(competitor, out VipCompetitorProfile profile)
            ? profile.GetStealMessageName()
            : "戴威";
    }

    public static string GetStealDefenceMessage(VipCompetitor competitor)
    {
        if (!TryGetProfile(competitor, out VipCompetitorProfile profile))
            return "戴威来抢你的贵客了!";

        return $"{profile.GetStealMessageName()}来抢你的贵客了!";
    }

    public static string GetStealAttackMessage(VipCompetitor competitor)
    {
        if (!TryGetProfile(competitor, out VipCompetitorProfile profile))
            return "你来抢戴威的贵客了!";

        return $"你来抢{profile.GetStealMessageName()}的贵客了!";
    }
}
