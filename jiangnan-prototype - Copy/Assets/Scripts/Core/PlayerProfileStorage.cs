using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public class PlayerProfileData
{
    public string displayName;
    public int gold;
    public bool hasClaimedStarterLoan;
    public bool hasBuiltTownRestaurant;
    public int mainSceneBuiltSpotCount;
    public int mainSceneBuiltSpotMask;
    public string[] mainSceneBuiltSpotIds;
    public int mainSceneMissionPartIndex;
    public bool mainSceneSecondFloorRevealed;
    public int mainSceneHiredSpotCount;
    public bool mainSceneBusinessStarted;
    public int mainSceneServedVipCount;
    public int[] tableLevels;
    public bool[] brokenTables;
    public bool[] unlockedDishes;
    public int signatureDishIndex = -1;
    public bool hasSavedSignatureDish;
    public bool hasDismissedTableBuildInfo;
    public bool hasCompetitorVipStealAttempted;
    public bool hasReceivedFirstVipCustomer;
    public bool hasOpenedMenu;
    public bool hasDismissedMenuSignaturePrompts;
    public int restaurantRatingSampleCount;
    public float restaurantRatingSampleSum;
    public bool hasSavedRestaurantRating;
    public float restaurantRating;
    public int restaurantRatingServedProgress;
    public int restaurantRatingLeaveProgress;
}

public static class PlayerProfileStorage
{
    private const string ProfilesFolderName = "PlayerProfiles";
    private const string LastPlayerNameKey = "jiangnan.last_player_name";
    private const string PendingLoanPresentationKey = "jiangnan.pending_loan_presentation";
    // Ground-floor upgradeable tables: Table (1) … Table (6). VIP table is excluded from save indices.
    private const int MainSceneTableCount = 6;
    public const int MainSceneBuildSpotCount = 11;
    public const int MainSceneStarterBuildSpotCount = 2;
    public const int DishCount = 9;
    public const float DefaultRestaurantRating = 3f;
    public const float MinRestaurantRating = 2.5f;
    public const float MaxRestaurantRating = 5f;

    public static string CurrentPlayerName { get; private set; }

    public static bool HasCurrentPlayerName =>
        !string.IsNullOrWhiteSpace(CurrentPlayerName);

    public static void SetCurrentPlayerName(string displayName)
    {
        CurrentPlayerName = SanitizeDisplayName(displayName);
    }

    public static bool TryLoadLastPlayerName(out string displayName)
    {
        displayName = PlayerPrefs.GetString(LastPlayerNameKey, string.Empty);

        if (string.IsNullOrWhiteSpace(displayName))
            return false;

        displayName = SanitizeDisplayName(displayName);
        CurrentPlayerName = displayName;
        return true;
    }

    public static bool SavePlayerName(string displayName)
    {
        displayName = SanitizeDisplayName(displayName);

        if (string.IsNullOrWhiteSpace(displayName))
            return false;

        CurrentPlayerName = displayName;
        PlayerPrefs.SetString(LastPlayerNameKey, displayName);
        PlayerPrefs.Save();

        PlayerProfileData profile = LoadProfile(displayName) ?? new PlayerProfileData
        {
            displayName = displayName,
            gold = 0
        };

        profile.displayName = displayName;
        WriteProfile(displayName, profile);
        return true;
    }

    public static int LoadGoldForPlayer(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return 0;

        PlayerProfileData profile = LoadProfile(displayName);
        return profile == null ? 0 : Mathf.Max(0, profile.gold);
    }

    public static void MarkLoanPresentationPending()
    {
        PlayerPrefs.SetInt(PendingLoanPresentationKey, 1);
        PlayerPrefs.Save();
    }

    public static bool IsLoanPresentationPending()
    {
        return PlayerPrefs.GetInt(PendingLoanPresentationKey, 0) == 1;
    }

    public static bool ConsumeLoanPresentationPending()
    {
        bool pending = PlayerPrefs.GetInt(PendingLoanPresentationKey, 0) == 1;

        if (pending)
        {
            PlayerPrefs.DeleteKey(PendingLoanPresentationKey);
            PlayerPrefs.Save();
        }

        return pending;
    }

    public static bool HasClaimedStarterLoanForCurrentPlayer() =>
        TryGetCurrentProfile(out PlayerProfileData profile) && profile.hasClaimedStarterLoan;

    public static void SetStarterLoanClaimedForCurrentPlayer() =>
        ModifyCurrentProfile(profile => profile.hasClaimedStarterLoan = true);

    public static void SaveGoldForCurrentPlayer(int gold) =>
        ModifyCurrentProfile(profile => profile.gold = Mathf.Max(0, gold));

    public static bool HasBuiltTownRestaurantForCurrentPlayer() =>
        TryGetCurrentProfile(out PlayerProfileData profile) && profile.hasBuiltTownRestaurant;

    public static void SetTownRestaurantBuiltForCurrentPlayer() =>
        ModifyCurrentProfile(profile => profile.hasBuiltTownRestaurant = true);

    public static int GetMainSceneBuiltSpotCountForCurrentPlayer() =>
        TryGetCurrentProfile(out PlayerProfileData profile) ? Mathf.Max(0, profile.mainSceneBuiltSpotCount) : 0;

    public static void SetMainSceneBuiltSpotCountForCurrentPlayer(int builtSpotCount) =>
        ModifyCurrentProfile(profile => profile.mainSceneBuiltSpotCount = Mathf.Max(0, builtSpotCount));

    public static int GetMainSceneBuiltSpotMaskForCurrentPlayer() =>
        TryGetCurrentProfile(out PlayerProfileData profile) ? profile.mainSceneBuiltSpotMask : 0;

    public static void SetMainSceneBuiltSpotMaskForCurrentPlayer(int builtSpotMask) =>
        ModifyCurrentProfile(profile => profile.mainSceneBuiltSpotMask = builtSpotMask);

    public static string[] GetMainSceneBuiltSpotIdsForCurrentPlayer()
    {
        if (!TryGetCurrentProfile(out PlayerProfileData profile) || profile.mainSceneBuiltSpotIds == null)
            return Array.Empty<string>();

        return CopyBuiltSpotIds(profile.mainSceneBuiltSpotIds);
    }

    public static void SetMainSceneBuiltSpotIdsForCurrentPlayer(string[] builtSpotIds) =>
        ModifyCurrentProfile(profile =>
        {
            profile.mainSceneBuiltSpotIds = CopyBuiltSpotIds(builtSpotIds);
            profile.mainSceneBuiltSpotCount = profile.mainSceneBuiltSpotIds.Length;
        });

    private static string[] CopyBuiltSpotIds(string[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<string>();

        List<string> ids = new List<string>(source.Length);

        for (int i = 0; i < source.Length; i++)
        {
            string id = source[i];

            if (string.IsNullOrWhiteSpace(id))
                continue;

            id = id.Trim();

            if (!ids.Contains(id))
                ids.Add(id);
        }

        return ids.Count == 0 ? Array.Empty<string>() : ids.ToArray();
    }

    public static int GetMainSceneMissionPartIndexForCurrentPlayer() =>
        TryGetCurrentProfile(out PlayerProfileData profile) ? Mathf.Max(0, profile.mainSceneMissionPartIndex) : 0;

    public static void SetMainSceneMissionPartIndexForCurrentPlayer(int partIndex) =>
        ModifyCurrentProfile(profile => profile.mainSceneMissionPartIndex = Mathf.Max(0, partIndex));

    public static bool HasMainSceneSecondFloorRevealedForCurrentPlayer() =>
        TryGetCurrentProfile(out PlayerProfileData profile) && profile.mainSceneSecondFloorRevealed;

    public static void SetMainSceneSecondFloorRevealedForCurrentPlayer()
    {
        if (HasMainSceneSecondFloorRevealedForCurrentPlayer())
            return;

        ModifyCurrentProfile(profile => profile.mainSceneSecondFloorRevealed = true);
    }

    public static int GetMainSceneHiredSpotCountForCurrentPlayer() =>
        TryGetCurrentProfile(out PlayerProfileData profile) ? Mathf.Max(0, profile.mainSceneHiredSpotCount) : 0;

    public static void SetMainSceneHiredSpotCountForCurrentPlayer(int hiredSpotCount) =>
        ModifyCurrentProfile(profile => profile.mainSceneHiredSpotCount = Mathf.Max(0, hiredSpotCount));

    public static bool HasMainSceneBusinessStartedForCurrentPlayer() =>
        TryGetCurrentProfile(out PlayerProfileData profile) && profile.mainSceneBusinessStarted;

    public static void SetMainSceneBusinessStartedForCurrentPlayer() =>
        ModifyCurrentProfile(profile => profile.mainSceneBusinessStarted = true);

    public static int GetMainSceneServedVipCountForCurrentPlayer() =>
        TryGetCurrentProfile(out PlayerProfileData profile) ? Mathf.Max(0, profile.mainSceneServedVipCount) : 0;

    public static void SetMainSceneServedVipCountForCurrentPlayer(int servedVipCount) =>
        ModifyCurrentProfile(profile => profile.mainSceneServedVipCount = Mathf.Max(0, servedVipCount));

    /// <summary>
    /// True when the main restaurant should keep spawning customers
    /// (business open and VIP serve-stop threshold not reached).
    /// </summary>
    public static bool ShouldMainSceneSpawnCustomersForCurrentPlayer(int servedVipSpawnStopCount)
    {
        if (!HasMainSceneBusinessStartedForCurrentPlayer())
            return false;

        if (servedVipSpawnStopCount <= 0)
            return true;

        return GetMainSceneServedVipCountForCurrentPlayer() < servedVipSpawnStopCount;
    }

    public static int GetTableLevelForCurrentPlayer(int tableIndex)
    {
        if (!EnsureCurrentPlayerLoaded() || tableIndex < 0 || tableIndex >= MainSceneTableCount)
            return 1;

        PlayerProfileData profile = LoadProfile(CurrentPlayerName);

        if (profile == null)
            return 1;

        EnsureTableLevels(profile);
        return Mathf.Clamp(profile.tableLevels[tableIndex], 1, 3);
    }

    public static void SetTableLevelForCurrentPlayer(int tableIndex, int level)
    {
        if (!EnsureCurrentPlayerLoaded() || tableIndex < 0 || tableIndex >= MainSceneTableCount)
            return;

        ModifyCurrentProfile(profile =>
        {
            EnsureTableLevels(profile);
            profile.tableLevels[tableIndex] = Mathf.Clamp(level, 1, 3);
        });
    }

    public static bool IsTableBrokenForCurrentPlayer(int tableIndex)
    {
        if (!EnsureCurrentPlayerLoaded() || tableIndex < 0 || tableIndex >= MainSceneTableCount)
            return false;

        PlayerProfileData profile = LoadProfile(CurrentPlayerName);

        if (profile == null)
            return false;

        EnsureBrokenTables(profile);
        return profile.brokenTables[tableIndex];
    }

    public static void SetTableBrokenForCurrentPlayer(int tableIndex, bool broken)
    {
        if (!EnsureCurrentPlayerLoaded() || tableIndex < 0 || tableIndex >= MainSceneTableCount)
            return;

        ModifyCurrentProfile(profile =>
        {
            EnsureBrokenTables(profile);
            profile.brokenTables[tableIndex] = broken;
        });
    }

    private static void EnsureBrokenTables(PlayerProfileData profile)
    {
        if (profile.brokenTables != null && profile.brokenTables.Length >= MainSceneTableCount)
            return;

        bool[] broken = new bool[MainSceneTableCount];

        if (profile.brokenTables != null)
        {
            int count = Mathf.Min(profile.brokenTables.Length, MainSceneTableCount);

            for (int i = 0; i < count; i++)
                broken[i] = profile.brokenTables[i];
        }

        profile.brokenTables = broken;
    }

    private static void EnsureTableLevels(PlayerProfileData profile)
    {
        if (profile.tableLevels != null && profile.tableLevels.Length >= MainSceneTableCount)
            return;

        int[] levels = new int[MainSceneTableCount];
        for (int i = 0; i < MainSceneTableCount; i++)
            levels[i] = 1;

        if (profile.tableLevels != null)
        {
            int count = Mathf.Min(profile.tableLevels.Length, MainSceneTableCount);
            for (int i = 0; i < count; i++)
                levels[i] = Mathf.Clamp(profile.tableLevels[i], 1, 3);
        }

        profile.tableLevels = levels;
    }

    public static bool[] GetUnlockedDishesForCurrentPlayer()
    {
        if (!EnsureCurrentPlayerLoaded())
            return null;

        PlayerProfileData profile = LoadProfile(CurrentPlayerName);

        if (profile == null || !HasSavedUnlockedDishes(profile))
            return null;

        return CopyUnlockedDishes(profile.unlockedDishes);
    }

    public static bool IsDishUnlockedForCurrentPlayer(int dishIndex)
    {
        if (dishIndex < 0 || dishIndex >= DishCount)
            return false;

        bool[] unlocked = GetUnlockedDishesForCurrentPlayer();
        return unlocked != null && unlocked[dishIndex];
    }

    public static void SetDishUnlockedForCurrentPlayer(int dishIndex, bool unlocked)
    {
        if (!EnsureCurrentPlayerLoaded() || dishIndex < 0 || dishIndex >= DishCount)
            return;

        ModifyCurrentProfile(profile =>
        {
            EnsureUnlockedDishes(profile);
            profile.unlockedDishes[dishIndex] = unlocked;
        });
    }

    public static void SaveUnlockedDishesForCurrentPlayer(bool[] unlockedDishes)
    {
        if (!EnsureCurrentPlayerLoaded())
            return;

        ModifyCurrentProfile(profile => profile.unlockedDishes = CopyUnlockedDishes(unlockedDishes));
    }

    private static bool HasSavedUnlockedDishes(PlayerProfileData profile)
    {
        return profile != null
            && profile.unlockedDishes != null
            && profile.unlockedDishes.Length >= DishCount;
    }

    private static void EnsureUnlockedDishes(PlayerProfileData profile)
    {
        if (profile.unlockedDishes == null || profile.unlockedDishes.Length < DishCount)
            profile.unlockedDishes = new bool[DishCount];
    }

    private static bool[] CopyUnlockedDishes(bool[] source)
    {
        bool[] copy = new bool[DishCount];

        if (source == null)
            return copy;

        int count = Mathf.Min(source.Length, DishCount);

        for (int i = 0; i < count; i++)
            copy[i] = source[i];

        return copy;
    }

    public static int GetSignatureDishIndexForCurrentPlayer()
    {
        if (!EnsureCurrentPlayerLoaded())
            return -1;

        PlayerProfileData profile = LoadProfile(CurrentPlayerName);

        if (profile == null || !HasSavedUnlockedDishes(profile) || !profile.hasSavedSignatureDish)
            return -1;

        return Mathf.Clamp(profile.signatureDishIndex, 0, DishCount - 1);
    }

    public static void SetSignatureDishIndexForCurrentPlayer(int dishIndex)
    {
        if (!EnsureCurrentPlayerLoaded() || dishIndex < -1 || dishIndex >= DishCount)
            return;

        ModifyCurrentProfile(profile =>
        {
            EnsureUnlockedDishes(profile);
            profile.signatureDishIndex = dishIndex;
            profile.hasSavedSignatureDish = dishIndex >= 0;
        });
    }

    public static bool HasDismissedTableBuildInfoForCurrentPlayer() =>
        TryGetCurrentProfile(out PlayerProfileData profile) && profile.hasDismissedTableBuildInfo;

    public static void SetTableBuildInfoDismissedForCurrentPlayer() =>
        ModifyCurrentProfile(profile => profile.hasDismissedTableBuildInfo = true);

    public static bool HasCompetitorVipStealAttemptedForCurrentPlayer() =>
        TryGetCurrentProfile(out PlayerProfileData profile) && profile.hasCompetitorVipStealAttempted;

    public static void SetCompetitorVipStealAttemptedForCurrentPlayer()
    {
        if (HasCompetitorVipStealAttemptedForCurrentPlayer())
            return;

        ModifyCurrentProfile(profile => profile.hasCompetitorVipStealAttempted = true);
    }

    public static bool HasReceivedFirstVipCustomerForCurrentPlayer()
    {
        return TryGetCurrentProfile(out PlayerProfileData profile) && profile.hasReceivedFirstVipCustomer;
    }

    public static void SetFirstVipCustomerReceivedForCurrentPlayer()
    {
        if (!TryGetCurrentProfile(out PlayerProfileData profile) || profile.hasReceivedFirstVipCustomer)
            return;

        ModifyCurrentProfile(storedProfile => storedProfile.hasReceivedFirstVipCustomer = true);
    }

    public static bool HasOpenedMenuForCurrentPlayer() =>
        TryGetCurrentProfile(out PlayerProfileData profile) && profile.hasOpenedMenu;

    public static void SetMenuOpenedForCurrentPlayer()
    {
        if (HasOpenedMenuForCurrentPlayer())
            return;

        ModifyCurrentProfile(profile => profile.hasOpenedMenu = true);
    }

    public static bool HasDismissedMenuSignaturePromptsForCurrentPlayer() =>
        TryGetCurrentProfile(out PlayerProfileData profile) && profile.hasDismissedMenuSignaturePrompts;

    public static void SetDismissedMenuSignaturePromptsForCurrentPlayer() =>
        ModifyCurrentProfile(profile => profile.hasDismissedMenuSignaturePrompts = true);

    public static bool TryGetRestaurantRatingStateForCurrentPlayer(
        out float rating,
        out int servedProgress,
        out int leaveProgress,
        out bool hasSaved)
    {
        rating = DefaultRestaurantRating;
        servedProgress = 0;
        leaveProgress = 0;
        hasSaved = false;

        if (!TryGetCurrentProfile(out PlayerProfileData profile))
            return false;

        if (profile.hasSavedRestaurantRating)
        {
            rating = profile.restaurantRating;
            servedProgress = profile.restaurantRatingServedProgress;
            leaveProgress = profile.restaurantRatingLeaveProgress;
            hasSaved = true;
            return true;
        }

        return true;
    }

    public static bool TryGetLegacyRestaurantRatingAverageForCurrentPlayer(out float averageRating)
    {
        averageRating = DefaultRestaurantRating;

        if (!TryGetCurrentProfile(out PlayerProfileData profile))
            return false;

        if (profile.restaurantRatingSampleCount <= 0)
            return false;

        averageRating = profile.restaurantRatingSampleSum / profile.restaurantRatingSampleCount;
        return true;
    }

    public static void SetRestaurantRatingStateForCurrentPlayer(
        float rating,
        int servedProgress,
        int leaveProgress)
    {
        ModifyCurrentProfile(profile =>
        {
            profile.hasSavedRestaurantRating = true;
            profile.restaurantRating = Mathf.Clamp(
                rating,
                MinRestaurantRating,
                MaxRestaurantRating);
            profile.restaurantRatingServedProgress = Mathf.Max(0, servedProgress);
            profile.restaurantRatingLeaveProgress = Mathf.Max(0, leaveProgress);
        });
    }

    public static int ResetAllPlayerData()
    {
        int deletedCount = DeleteAllProfileFiles();
        PlayerPrefs.DeleteKey(LastPlayerNameKey);
        PlayerPrefs.DeleteKey(PendingLoanPresentationKey);
        PlayerPrefs.Save();
        CurrentPlayerName = null;
        return deletedCount;
    }

    private static int DeleteAllProfileFiles()
    {
        string profilesDirectory = GetProfilesDirectory();

        if (!Directory.Exists(profilesDirectory))
            return 0;

        string[] profileFiles = Directory.GetFiles(profilesDirectory, "*.json");
        int deletedCount = 0;

        for (int i = 0; i < profileFiles.Length; i++)
        {
            try
            {
                File.Delete(profileFiles[i]);
                deletedCount++;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Failed to delete profile file {profileFiles[i]}: {exception.Message}");
            }
        }

        return deletedCount;
    }

    private static bool TryGetCurrentProfile(out PlayerProfileData profile)
    {
        profile = null;

        if (!EnsureCurrentPlayerLoaded())
            return false;

        profile = LoadProfile(CurrentPlayerName);
        return profile != null;
    }

    private static void ModifyCurrentProfile(Action<PlayerProfileData> mutate)
    {
        if (!EnsureCurrentPlayerLoaded() || mutate == null)
            return;

        PlayerProfileData profile = LoadProfile(CurrentPlayerName) ?? new PlayerProfileData();
        profile.displayName = CurrentPlayerName;
        mutate(profile);
        WriteProfile(CurrentPlayerName, profile);
    }

    private static bool EnsureCurrentPlayerLoaded()
    {
        if (HasCurrentPlayerName)
            return true;

        return TryLoadLastPlayerName(out _);
    }

    private static PlayerProfileData LoadProfile(string displayName)
    {
        string path = GetProfilePath(displayName);

        if (!File.Exists(path))
            return null;

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            return JsonUtility.FromJson<PlayerProfileData>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to load player profile at {path}: {exception.Message}");
            return null;
        }
    }

    private static void WriteProfile(string displayName, PlayerProfileData profile)
    {
        string directory = GetProfilesDirectory();
        Directory.CreateDirectory(directory);

        string path = GetProfilePath(displayName);
        string json = JsonUtility.ToJson(profile, true);

        try
        {
            File.WriteAllText(path, json, Encoding.UTF8);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Failed to save player profile at {path}: {exception.Message}");
        }
    }

    private static string GetProfilesDirectory()
    {
        return Path.Combine(Application.persistentDataPath, ProfilesFolderName);
    }

    private static string GetProfilePath(string displayName)
    {
        return Path.Combine(GetProfilesDirectory(), $"{BuildProfileFileName(displayName)}.json");
    }

    private static string BuildProfileFileName(string displayName)
    {
        string sanitized = SanitizeDisplayName(displayName).ToLowerInvariant();
        StringBuilder builder = new StringBuilder(sanitized.Length);

        for (int i = 0; i < sanitized.Length; i++)
        {
            char character = sanitized[i];
            builder.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return builder.Length > 0 ? builder.ToString() : "player";
    }

    private static string SanitizeDisplayName(string displayName)
    {
        return displayName == null ? string.Empty : displayName.Trim();
    }
}
