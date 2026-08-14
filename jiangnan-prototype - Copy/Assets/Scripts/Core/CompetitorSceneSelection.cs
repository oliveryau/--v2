using System;
using System.Collections.Generic;
using UnityEngine;

public static class CompetitorSceneSelection
{
    private static VipCompetitorProfile[] _profiles = Array.Empty<VipCompetitorProfile>();
    private static readonly Dictionary<VipCompetitor, VipCompetitorProfile> _profileByCompetitor = new();
    private static readonly Dictionary<int, VipCompetitorProfile> _profileByTownShopIndex = new();
    private static readonly HashSet<int> _blockedTownShopIndices = new();
    private static readonly HashSet<int> _onlineTownShopIndices = new();
    private static readonly HashSet<int> _stolenTownShopIndicesThisRun = new();
    private static VipCompetitor _selectedCompetitor = VipCompetitor.DaiWei;
    private static int _selectedTownShopIndex;
    private static bool _pendingChasedAlert;
    private static int _pendingChasedTownShopIndex;
    private static bool _pendingBusinessResumeAfterSteal;
    private static int _stolenNormalCustomersThisRun;
    private static int _stolenVipCustomersThisRun;

    private const int RequiredNormalStealsForBusinessResume = 3;
    private const int RequiredVipStealsForBusinessResume = 1;

    public static VipCompetitor SelectedCompetitor => _selectedCompetitor;
    public static int SelectedTownShopIndex => _selectedTownShopIndex;

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
        _selectedTownShopIndex = shopIndex;

        if (shopIndex > 0)
            MarkTownShopOnline(shopIndex);

        if (_profileByTownShopIndex.TryGetValue(shopIndex, out VipCompetitorProfile profile))
        {
            _selectedCompetitor = profile.Competitor;
            return;
        }

        _selectedCompetitor = VipCompetitor.DaiWei;
    }

    /// <summary>
    /// Called when the player is chased out of a competitor restaurant.
    /// Blocks re-entry for that town shop and queues the town chased alert.
    /// </summary>
    public static void MarkChasedOutFromCurrentVisit()
    {
        int shopIndex = ResolveCurrentTownShopIndex();
        if (shopIndex > 0)
        {
            _blockedTownShopIndices.Add(shopIndex);
            MarkTownShopOnline(shopIndex);
            _pendingChasedTownShopIndex = shopIndex;
        }

        _pendingChasedAlert = true;
    }

    public static bool IsTownShopEnterBlocked(int shopIndex)
    {
        return shopIndex > 0 && _blockedTownShopIndices.Contains(shopIndex);
    }

    public static void MarkTownShopOnline(int shopIndex)
    {
        if (shopIndex > 0)
            _onlineTownShopIndices.Add(shopIndex);
    }

    public static bool IsTownShopOnline(int shopIndex)
    {
        return shopIndex > 0 && _onlineTownShopIndices.Contains(shopIndex);
    }

    public static void ClearBlockedTownShops()
    {
        _blockedTownShopIndices.Clear();
        _onlineTownShopIndices.Clear();
    }

    public static bool TryConsumePendingChasedAlert(out int townShopIndex, out VipCompetitor competitor)
    {
        townShopIndex = 0;
        competitor = VipCompetitor.DaiWei;

        if (!_pendingChasedAlert)
            return false;

        _pendingChasedAlert = false;
        townShopIndex = _pendingChasedTownShopIndex > 0
            ? _pendingChasedTownShopIndex
            : ResolveCurrentTownShopIndex();
        competitor = _selectedCompetitor;
        _pendingChasedTownShopIndex = 0;
        return true;
    }

    /// <summary>
    /// Successful competitor steal. Main-scene lull only ends after enough steals this outing:
    /// at least 3 normal customers, or at least 1 VIP.
    /// </summary>
    public static void RegisterSuccessfulSteal(bool isVip)
    {
        int shopIndex = ResolveCurrentTownShopIndex();
        if (shopIndex > 0)
        {
            _stolenTownShopIndicesThisRun.Add(shopIndex);
            MarkTownShopOnline(shopIndex);
        }

        if (isVip)
            _stolenVipCustomersThisRun++;
        else
            _stolenNormalCustomersThisRun++;

        if (!HasMetBusinessResumeStealRequirement())
            return;

        _pendingBusinessResumeAfterSteal = true;
        PlayerProfileStorage.SetMainSceneServedVipCountForCurrentPlayer(0);
        PlayerProfileStorage.SetCompetitorVipStealAttemptedForCurrentPlayer();
        MissionUiController.NotifyStealRequirementMet();
    }

    public static bool HasMetBusinessResumeStealRequirement()
    {
        return _stolenVipCustomersThisRun >= RequiredVipStealsForBusinessResume
            || _stolenNormalCustomersThisRun >= RequiredNormalStealsForBusinessResume;
    }

    public static bool ConsumePendingBusinessResumeAfterSteal(out int stolenShopCount)
    {
        stolenShopCount = 0;

        if (!_pendingBusinessResumeAfterSteal)
            return false;

        _pendingBusinessResumeAfterSteal = false;
        stolenShopCount = _stolenTownShopIndicesThisRun.Count;
        ClearStealProgressThisRun();
        return true;
    }

    public static bool ConsumePendingBusinessResumeAfterSteal()
    {
        return ConsumePendingBusinessResumeAfterSteal(out _);
    }

    public static bool HasPendingBusinessResumeAfterSteal => _pendingBusinessResumeAfterSteal;

    private static void ClearStealProgressThisRun()
    {
        _stolenTownShopIndicesThisRun.Clear();
        _stolenNormalCustomersThisRun = 0;
        _stolenVipCustomersThisRun = 0;
    }

    private static int ResolveCurrentTownShopIndex()
    {
        if (_selectedTownShopIndex > 0)
            return _selectedTownShopIndex;

        return TryGetProfile(_selectedCompetitor, out VipCompetitorProfile profile)
            ? profile.TownShopIndex
            : 0;
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

    public static bool TryGetChaseThresholdRange(out float minSeconds, out float maxSeconds)
    {
        return TryGetChaseThresholdRange(_selectedCompetitor, out minSeconds, out maxSeconds);
    }

    public static bool TryGetChaseThresholdRange(
        VipCompetitor competitor,
        out float minSeconds,
        out float maxSeconds)
    {
        minSeconds = 0f;
        maxSeconds = 0f;

        if (!TryGetProfile(competitor, out VipCompetitorProfile profile) || profile == null)
            return false;

        profile.GetChaseThresholdRange(out minSeconds, out maxSeconds);
        return true;
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

    public static string GetChasedAlertMessage(VipCompetitor competitor)
    {
        return $"{GetCompetitorDisplayName(competitor)}不让你进店了！";
    }

    public static Sprite GetAngryFace(VipCompetitor competitor)
    {
        if (TryGetProfile(competitor, out VipCompetitorProfile profile) && profile.AngryFace != null)
            return profile.AngryFace;

        string resourcePath = competitor switch
        {
            VipCompetitor.DaiWei => "Face/dw-angry",
            VipCompetitor.HanXi => "Face/hx-angry",
            VipCompetitor.ChunHua => "Face/ch-angry",
            VipCompetitor.HongJie => "Face/hong-angry",
            _ => "Face/dw-angry"
        };

        return Resources.Load<Sprite>(resourcePath);
    }

    public static Sprite GetAngryFace()
    {
        return GetAngryFace(_selectedCompetitor);
    }

    public static Sprite GetProfilePic(VipCompetitor competitor)
    {
        if (TryGetProfile(competitor, out VipCompetitorProfile profile) && profile.ProfilePic != null)
            return profile.ProfilePic;

        return null;
    }

    public static Sprite GetProfilePic()
    {
        return GetProfilePic(_selectedCompetitor);
    }
}
