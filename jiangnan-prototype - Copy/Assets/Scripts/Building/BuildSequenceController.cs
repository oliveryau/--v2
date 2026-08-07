using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class BuildSequenceController : MonoBehaviour
{
    [Header("Build Spots")]
    [Tooltip("Mission 1 spots unlocked on first visit (Counter, Stove).")]
    [SerializeField] private List<BuildSpot> _starterSpots = new();
    [Tooltip("Mission 3 spots (Table 1-2). Shown only during the table-build mission.")]
    [SerializeField] private List<BuildSpot> _missionTableSpots = new();
    [Tooltip("Ground-floor spots unlocked after the table mission (Table 3-6, Stairs).")]
    [SerializeField] private List<BuildSpot> _groundFloorFollowUpSpots = new();
    [Tooltip("Second-floor spots unlocked after the stairs are built (VIP Stage, VIP Table).")]
    [SerializeField] private List<BuildSpot> _secondFloorBuildSpots = new();
    [Tooltip("Legacy follow-up list. Migrated into ground/second-floor lists when those are empty.")]
    [SerializeField] private List<BuildSpot> _followUpSpots = new();
    [Tooltip("Legacy ordered list. Used only if starter spots are empty.")]
    [SerializeField] private List<BuildSpot> _buildOrder = new();
    [SerializeField] private int _legacyStarterCount = PlayerProfileStorage.MainSceneStarterBuildSpotCount;

    [Header("Upper Floor")]
    [SerializeField] private BuildSpot _stairsBuildSpot;
    [SerializeField] private GameObject _secondFloorBuildSpotsRoot;
    [Tooltip("Legacy reference to the second-floor build-spot parent.")]
    [SerializeField] private GameObject _secondFloorRoot;

    private readonly HashSet<BuildSpot> _deliveringSpots = new();
    private readonly List<BuildSpot> _allSpots = new();
    private MissionUiController _missionUi;

    private void Awake()
    {
        MigrateLegacyBuildOrderIfNeeded();
        MigrateLegacyFollowUpSpotsIfNeeded();
        AutoWireBuildSpotGroupsFromSceneIfNeeded();
        RebuildSpotList();
        CacheSecondFloorBuildSpotsRoot();
        RestoreSavedBuildProgress();
        SyncSecondFloorBuildSpotsRoot();
    }

    private void OnEnable()
    {
        GameEvents.StateChanged += HandleStateChanged;
        GameEvents.MissionPartChanged += HandleMissionPartChanged;
        GameEvents.MissionPartCompleted += HandleMissionPartCompleted;
        GameEvents.MainSceneSecondFloorRevealRequested += HandleFollowUpBuildSpotsRevealRequested;
        GameEvents.BusinessSessionStarted += HandleBusinessSessionChanged;
        GameEvents.BusinessSessionEnded += HandleBusinessSessionChanged;
        GameEvents.BusinessDowntimeStarted += HandleBusinessDowntimeStarted;
        GameEvents.RestaurantFloorChanged += HandleRestaurantFloorChanged;

        for (int i = 0; i < _allSpots.Count; i++)
        {
            BuildSpot spot = _allSpots[i];

            if (spot != null)
                spot.Clicked += HandleSpotClicked;
        }

        if (CanInteractWithBuildSpots())
            BeginSequence();
    }

    private void OnDisable()
    {
        GameEvents.StateChanged -= HandleStateChanged;
        GameEvents.MissionPartChanged -= HandleMissionPartChanged;
        GameEvents.MissionPartCompleted -= HandleMissionPartCompleted;
        GameEvents.MainSceneSecondFloorRevealRequested -= HandleFollowUpBuildSpotsRevealRequested;
        GameEvents.BusinessSessionStarted -= HandleBusinessSessionChanged;
        GameEvents.BusinessSessionEnded -= HandleBusinessSessionChanged;
        GameEvents.BusinessDowntimeStarted -= HandleBusinessDowntimeStarted;
        GameEvents.RestaurantFloorChanged -= HandleRestaurantFloorChanged;

        for (int i = 0; i < _allSpots.Count; i++)
        {
            BuildSpot spot = _allSpots[i];

            if (spot == null)
                continue;

            spot.Clicked -= HandleSpotClicked;
            spot.BuildCompleted -= HandleSpotBuildCompleted;
        }
    }

    private void MigrateLegacyBuildOrderIfNeeded()
    {
        if (_starterSpots != null && _starterSpots.Count > 0)
            return;

        if (_buildOrder == null || _buildOrder.Count == 0)
            return;

        int starterCount = Mathf.Clamp(_legacyStarterCount, 0, _buildOrder.Count);
        _starterSpots = new List<BuildSpot>();
        _followUpSpots ??= new List<BuildSpot>();

        for (int i = 0; i < _buildOrder.Count; i++)
        {
            BuildSpot spot = _buildOrder[i];

            if (spot == null)
                continue;

            if (i < starterCount)
                _starterSpots.Add(spot);
            else if (!_followUpSpots.Contains(spot))
                _followUpSpots.Add(spot);
        }
    }

    private void MigrateLegacyFollowUpSpotsIfNeeded()
    {
        _missionTableSpots ??= new List<BuildSpot>();
        _groundFloorFollowUpSpots ??= new List<BuildSpot>();
        _secondFloorBuildSpots ??= new List<BuildSpot>();

        SplitTableSpotsOutOfStartersIfNeeded();

        if (_groundFloorFollowUpSpots.Count == 0 && _secondFloorBuildSpots.Count == 0)
        {
            if (_followUpSpots != null)
            {
                for (int i = 0; i < _followUpSpots.Count; i++)
                    AddSpotToFollowUpGroup(_followUpSpots[i]);
            }

            if (_groundFloorFollowUpSpots.Count == 0
                && _secondFloorBuildSpots.Count == 0
                && _buildOrder != null
                && _buildOrder.Count > _legacyStarterCount)
            {
                for (int i = _legacyStarterCount; i < _buildOrder.Count; i++)
                    AddSpotToFollowUpGroup(_buildOrder[i]);
            }
        }

        CacheStairsBuildSpot();
    }

    private void SplitTableSpotsOutOfStartersIfNeeded()
    {
        if (_starterSpots == null || _starterSpots.Count == 0)
            return;

        // Older setups kept Table 1/2 in starters; move them to the table-mission group.
        for (int i = _starterSpots.Count - 1; i >= 0; i--)
        {
            BuildSpot spot = _starterSpots[i];

            if (spot == null || spot.PlaceableType != PlaceableType.Table)
                continue;

            _starterSpots.RemoveAt(i);

            if (!_missionTableSpots.Contains(spot))
                _missionTableSpots.Add(spot);
        }
    }

    private void AutoWireBuildSpotGroupsFromSceneIfNeeded()
    {
        // If serialized scene references get out of sync (eg. during prefab/merge),
        // these lists can end up missing/null -> unlock happens, but click handlers
        // never get connected to the real BuildSpot components.
        // This fallback reconstructs groups by BuildSpot GameObject names.

        bool startersOk = HasSpotByName(_starterSpots, "BuildSpot_Counter")
            && HasSpotByName(_starterSpots, "BuildSpot_Stove");
        bool tablesOk = HasSpotByName(_missionTableSpots, "BuildSpot_Table (1)")
            && HasSpotByName(_missionTableSpots, "BuildSpot_Table (2)");
        bool groundOk = HasSpotByName(_groundFloorFollowUpSpots, "BuildSpot_Table (3)")
            && HasSpotByName(_groundFloorFollowUpSpots, "BuildSpot_Table (4)")
            && HasSpotByName(_groundFloorFollowUpSpots, "BuildSpot_Table (5)")
            && HasSpotByName(_groundFloorFollowUpSpots, "BuildSpot_Table (6)")
            && HasSpotByName(_groundFloorFollowUpSpots, "BuildSpot_Stairs");
        bool secondOk = HasSpotByName(_secondFloorBuildSpots, "BuildSpot_VIPStage")
            && HasSpotByName(_secondFloorBuildSpots, "BuildSpot_VIPTable (1)");

        if (startersOk && tablesOk && groundOk && secondOk)
            return;

        BuildSpot[] allSpots = FindObjectsByType<BuildSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Dictionary<string, BuildSpot> byExactName = new();

        for (int i = 0; i < allSpots.Length; i++)
        {
            BuildSpot spot = allSpots[i];
            if (spot == null)
                continue;

            // Keep first match only.
            byExactName.TryAdd(spot.gameObject.name, spot);
        }

        if (!startersOk)
        {
            _starterSpots.Clear();
            AddIfExists(byExactName, "BuildSpot_Counter", _starterSpots);
            AddIfExists(byExactName, "BuildSpot_Stove", _starterSpots);
        }

        if (!tablesOk)
        {
            _missionTableSpots.Clear();
            AddIfExists(byExactName, "BuildSpot_Table (1)", _missionTableSpots);
            AddIfExists(byExactName, "BuildSpot_Table (2)", _missionTableSpots);
        }

        if (!groundOk)
        {
            _groundFloorFollowUpSpots.Clear();

            AddIfExists(byExactName, "BuildSpot_Table (3)", _groundFloorFollowUpSpots);
            AddIfExists(byExactName, "BuildSpot_Table (4)", _groundFloorFollowUpSpots);
            AddIfExists(byExactName, "BuildSpot_Table (5)", _groundFloorFollowUpSpots);
            AddIfExists(byExactName, "BuildSpot_Table (6)", _groundFloorFollowUpSpots);
            AddIfExists(byExactName, "BuildSpot_Stairs", _groundFloorFollowUpSpots);
        }

        if (!secondOk)
        {
            _secondFloorBuildSpots.Clear();

            AddIfExists(byExactName, "BuildSpot_VIPStage", _secondFloorBuildSpots);
            // Table name includes an index in the scene.
            AddIfExists(byExactName, "BuildSpot_VIPTable (1)", _secondFloorBuildSpots);
        }

        // Re-evaluate stairs spot after potential list rewiring.
        _stairsBuildSpot = null;
        CacheStairsBuildSpot();
    }

    private static bool HasSpotByName(List<BuildSpot> spots, string exactName)
    {
        if (spots == null)
            return false;

        for (int i = 0; i < spots.Count; i++)
        {
            BuildSpot spot = spots[i];
            if (spot != null && spot.gameObject != null && string.Equals(spot.gameObject.name, exactName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static int CountNonNull(List<BuildSpot> list)
    {
        if (list == null)
            return 0;

        int count = 0;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null)
                count++;
        }

        return count;
    }

    private static void AddIfExists(Dictionary<string, BuildSpot> byName, string exactName, List<BuildSpot> target)
    {
        if (byName == null || target == null)
            return;

        if (byName.TryGetValue(exactName, out BuildSpot spot) && spot != null)
            target.Add(spot);
    }

    private void AddSpotToFollowUpGroup(BuildSpot spot)
    {
        if (spot == null)
            return;

        if (IsSecondFloorBuildSpot(spot))
        {
            if (!_secondFloorBuildSpots.Contains(spot))
                _secondFloorBuildSpots.Add(spot);

            return;
        }

        if (!_groundFloorFollowUpSpots.Contains(spot))
            _groundFloorFollowUpSpots.Add(spot);
    }

    private void HandleFollowUpBuildSpotsRevealRequested()
    {
        RefreshSpotStates();
    }

    private void HandleMissionPartChanged(int partIndex)
    {
        RefreshSpotStates();
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Building || state == GameState.Business)
            BeginSequence();
    }

    private void HandleBusinessSessionChanged()
    {
        RefreshSpotStates();
        SyncSecondFloorBuildSpotsRoot();
    }

    private void HandleBusinessDowntimeStarted()
    {
        RefreshSpotStates();
        SyncSecondFloorBuildSpotsRoot();
    }

    private void HandleRestaurantFloorChanged(int floor)
    {
        SyncSecondFloorBuildSpotsRoot();
        SyncBuildSpotViewFloorVisibility();
    }

    private void SyncBuildSpotViewFloorVisibility()
    {
        for (int i = 0; i < _allSpots.Count; i++)
        {
            BuildSpot spot = _allSpots[i];
            if (spot != null)
                spot.RefreshViewFloorVisibility();
        }
    }

    private void HandleMissionPartCompleted(int partIndex)
    {
        RefreshSpotStates();
        SyncSecondFloorBuildSpotsRoot();

        if (partIndex == MissionCatalog.StarterBuildMissionPartIndex)
            NotifyHireSpotsAfterStarterBuild();
    }

    private void BeginSequence()
    {
        RebuildSpotList();
        _deliveringSpots.Clear();
        EnsureMissionUi();
        SyncMissionPlacedCounts();
        RefreshSpotStates();
        SyncSecondFloorBuildSpotsRoot();
    }

    private void HandleSpotClicked(BuildSpot spot)
    {
        if (!CanInteractWithBuildSpots() || spot == null || spot.State != BuildSpotState.Active)
            return;

        if (_deliveringSpots.Contains(spot))
            return;

        if (GoldManager.Instance == null || !GoldManager.Instance.TrySpend(spot.Cost))
            return;

        _deliveringSpots.Add(spot);
        spot.BuildCompleted += HandleSpotBuildCompleted;
        spot.BeginDelivery();
    }

    private void HandleSpotBuildCompleted(BuildSpot spot)
    {
        if (spot == null)
            return;

        spot.BuildCompleted -= HandleSpotBuildCompleted;
        _deliveringSpots.Remove(spot);

        GameManager.Instance?.RegisterPlaced(spot.PlaceableType);
        SaveBuiltSpotMask();
        EnsureMissionUi();
        _missionUi?.NotifyPlaceableBuilt(spot.PlaceableType);

        if (AreStarterBuildSpotsComplete())
            NotifyHireSpotsAfterStarterBuild();

        RefreshSpotStates();
        SyncSecondFloorBuildSpotsRoot();
    }

    private void NotifyHireSpotsAfterStarterBuild()
    {
        HireSequenceController hireController = FindFirstObjectByType<HireSequenceController>();

        if (hireController != null)
            hireController.OnStarterBuildsCompleted();
    }

    private void RefreshSpotStates()
    {
        int missionPart = GetCurrentMissionPartIndex();

        // Mission 1: counter + stove only.
        bool showStarters = missionPart == MissionCatalog.StarterBuildMissionPartIndex;
        // Mission 2: hire only — no build spots.
        bool showMissionTables = missionPart >= MissionCatalog.TableBuildMissionPartIndex;
        bool showGroundFollowUps = missionPart > MissionCatalog.TableBuildMissionPartIndex;
        bool showSecondFloor = showGroundFollowUps && IsSecondFloorUnlocked;

        SetSpotGroupState(_starterSpots, activateUnbuilt: showStarters);
        SetSpotGroupState(_missionTableSpots, activateUnbuilt: showMissionTables);
        SetSpotGroupState(_groundFloorFollowUpSpots, activateUnbuilt: showGroundFollowUps);
        SetSpotGroupState(_secondFloorBuildSpots, activateUnbuilt: showSecondFloor);
        SyncSecondFloorBuildSpotsRoot();
        SyncBuildSpotViewFloorVisibility();
    }

    private static bool IsBuildMissionActive()
    {
        int missionPart = GetCurrentMissionPartIndex();
        return missionPart == MissionCatalog.StarterBuildMissionPartIndex
            || missionPart >= MissionCatalog.TableBuildMissionPartIndex;
    }

    private static int GetCurrentMissionPartIndex()
    {
        MissionUiController missionUi = FindFirstObjectByType<MissionUiController>();
        return missionUi != null
            ? missionUi.CurrentPartIndex
            : PlayerProfileStorage.GetMainSceneMissionPartIndexForCurrentPlayer();
    }

    private void SetSpotGroupState(List<BuildSpot> spots, bool activateUnbuilt)
    {
        if (spots == null)
            return;

        for (int i = 0; i < spots.Count; i++)
        {
            BuildSpot spot = spots[i];

            if (spot == null || spot.IsBuilt)
                continue;

            if (_deliveringSpots.Contains(spot))
                continue;

            spot.SetState(activateUnbuilt ? BuildSpotState.Active : BuildSpotState.Locked);
        }
    }

    private bool HasDeliveringSecondFloorSpot()
    {
        foreach (BuildSpot spot in _deliveringSpots)
        {
            if (spot != null && IsSecondFloorBuildSpot(spot))
                return true;
        }

        return false;
    }

    public bool AreStarterBuildSpotsComplete() => AreSpotsBuilt(_starterSpots);

    private static bool AreSpotsBuilt(List<BuildSpot> spots)
    {
        if (spots == null || spots.Count == 0)
            return true;

        for (int i = 0; i < spots.Count; i++)
        {
            BuildSpot spot = spots[i];

            if (spot != null && !spot.IsBuilt)
                return false;
        }

        return true;
    }

    private void SyncSecondFloorBuildSpotsRoot()
    {
        CacheSecondFloorBuildSpotsRoot();

        if (_secondFloorBuildSpotsRoot == null)
            return;

        bool unlocked = IsSecondFloorUnlocked;

        if (unlocked)
        {
            bool wasRevealed = PlayerProfileStorage.HasMainSceneSecondFloorRevealedForCurrentPlayer();
            PlayerProfileStorage.SetMainSceneSecondFloorRevealedForCurrentPlayer();

            if (!wasRevealed)
                GameEvents.RaiseSecondFloorUnlocked();
        }

        bool viewingSecondFloor = CharacterPanelController.Instance == null
            || CharacterPanelController.Instance.CurrentFloor == (int)RestaurantFloor.Second;

        _secondFloorBuildSpotsRoot.SetActive(unlocked && (viewingSecondFloor || HasDeliveringSecondFloorSpot()));

        if (unlocked && CharacterPanelController.Instance != null)
            CharacterPanelController.Instance.RefreshSecondFloorVisibilityRoots();
    }

    private void CacheSecondFloorBuildSpotsRoot()
    {
        if (_secondFloorBuildSpotsRoot != null)
            return;

        if (_secondFloorRoot != null)
        {
            _secondFloorBuildSpotsRoot = _secondFloorRoot;
            return;
        }

        GameObject found = FindSceneObjectByNameIncludingInactive("BuildSpots_SecondFloor");

        if (found == null)
            found = FindSceneObjectByNameIncludingInactive("Second Floor");

        _secondFloorBuildSpotsRoot = found;
    }

    private void CacheStairsBuildSpot()
    {
        if (_stairsBuildSpot != null)
            return;

        if (_groundFloorFollowUpSpots == null)
            return;

        for (int i = 0; i < _groundFloorFollowUpSpots.Count; i++)
        {
            BuildSpot spot = _groundFloorFollowUpSpots[i];

            if (spot != null && IsStairsSpot(spot))
            {
                _stairsBuildSpot = spot;
                return;
            }
        }
    }

    public bool IsSecondFloorUnlocked => IsStairsBuilt()
        || PlayerProfileStorage.HasMainSceneSecondFloorRevealedForCurrentPlayer();

    private bool IsStairsBuilt()
    {
        CacheStairsBuildSpot();

        return _stairsBuildSpot != null && _stairsBuildSpot.IsBuilt;
    }

    private static bool IsStairsSpot(BuildSpot spot)
    {
        return spot != null
            && spot.name.IndexOf("Stairs", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsSecondFloorBuildSpot(BuildSpot spot)
    {
        if (spot == null)
            return false;

        Transform current = spot.transform;

        while (current != null)
        {
            if (string.Equals(current.name, "BuildSpots_SecondFloor", System.StringComparison.OrdinalIgnoreCase))
                return true;

            current = current.parent;
        }

        return spot.name.IndexOf("VIP", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static GameObject FindSceneObjectByNameIncludingInactive(string objectName)
    {
        GameObject found = GameObject.Find(objectName);

        if (found != null)
            return found;

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (candidate != null
                && string.Equals(candidate.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private void EnsureMissionUi()
    {
        if (_missionUi == null)
        {
            _missionUi = FindFirstObjectByType<MissionUiController>();

            if (_missionUi == null)
            {
                GameObject missionRoot = GameObject.Find("Mission");

                if (missionRoot != null)
                    _missionUi = missionRoot.GetComponent<MissionUiController>()
                        ?? missionRoot.AddComponent<MissionUiController>();
            }
        }

        _missionUi?.EnsureInitialized();
    }

    private void SyncMissionPlacedCounts()
    {
        if (_missionUi == null)
            return;

        int receptions = 0;
        int stoves = 0;
        int tables = 0;
        int stairs = 0;
        int vipTables = 0;

        for (int i = 0; i < _allSpots.Count; i++)
        {
            BuildSpot spot = _allSpots[i];

            if (spot == null || !spot.IsBuilt)
                continue;

            switch (spot.PlaceableType)
            {
                case PlaceableType.Reception:
                    receptions++;
                    break;
                case PlaceableType.Stove:
                    stoves++;
                    break;
                case PlaceableType.Table:
                    tables++;
                    break;
                case PlaceableType.Stairs:
                    stairs++;
                    break;
                case PlaceableType.VipTable:
                    vipTables++;
                    break;
            }
        }

        _missionUi.SyncPlacedCounts(receptions, stoves, tables, stairs, vipTables);
    }

    private void RebuildSpotList()
    {
        _allSpots.Clear();
        AppendSpots(_starterSpots);
        AppendSpots(_missionTableSpots);
        AppendSpots(_groundFloorFollowUpSpots);
        AppendSpots(_secondFloorBuildSpots);
    }

    private void AppendSpots(List<BuildSpot> spots)
    {
        if (spots == null)
            return;

        for (int i = 0; i < spots.Count; i++)
        {
            BuildSpot spot = spots[i];

            if (spot != null && !_allSpots.Contains(spot))
                _allSpots.Add(spot);
        }
    }

    private void SaveBuiltSpotMask()
    {
        List<string> builtIds = new List<string>(_allSpots.Count);
        int mask = 0;

        for (int i = 0; i < _allSpots.Count; i++)
        {
            BuildSpot spot = _allSpots[i];

            if (spot == null || !spot.IsBuilt)
                continue;

            string spotId = GetBuildSpotSaveId(spot);

            if (!string.IsNullOrEmpty(spotId) && !builtIds.Contains(spotId))
                builtIds.Add(spotId);

            mask |= 1 << i;
        }

        // Prefer stable per-spot IDs (GameObject names) so partial builds like
        // Table(1)/(2)/(3)/(5) restore correctly under this player's profile.
        PlayerProfileStorage.SetMainSceneBuiltSpotIdsForCurrentPlayer(builtIds.ToArray());
        PlayerProfileStorage.SetMainSceneBuiltSpotMaskForCurrentPlayer(mask);
    }

    private void RestoreSavedBuildProgress()
    {
        RebuildSpotList();

        if (_allSpots.Count == 0)
            return;

        HashSet<string> builtIds = ResolveSavedBuiltSpotIds();

        for (int i = 0; i < _allSpots.Count; i++)
        {
            BuildSpot spot = _allSpots[i];

            if (spot == null || spot.IsBuilt)
                continue;

            string spotId = GetBuildSpotSaveId(spot);

            if (string.IsNullOrEmpty(spotId) || !builtIds.Contains(spotId))
                continue;

            spot.SetState(BuildSpotState.Built);
            GameManager.Instance?.RegisterPlaced(spot.PlaceableType);
        }

        if (IsStairsBuilt())
        {
            bool wasRevealed = PlayerProfileStorage.HasMainSceneSecondFloorRevealedForCurrentPlayer();
            PlayerProfileStorage.SetMainSceneSecondFloorRevealedForCurrentPlayer();

            if (!wasRevealed)
                GameEvents.RaiseSecondFloorUnlocked();
        }
    }

    private HashSet<string> ResolveSavedBuiltSpotIds()
    {
        HashSet<string> builtIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] savedIds = PlayerProfileStorage.GetMainSceneBuiltSpotIdsForCurrentPlayer();

        if (savedIds != null && savedIds.Length > 0)
        {
            for (int i = 0; i < savedIds.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(savedIds[i]))
                    builtIds.Add(savedIds[i].Trim());
            }

            return builtIds;
        }

        // Legacy mask migration: bit i meant "_allSpots[i] was built".
        int mask = PlayerProfileStorage.GetMainSceneBuiltSpotMaskForCurrentPlayer();

        if (mask != 0)
        {
            for (int i = 0; i < _allSpots.Count; i++)
            {
                if ((mask & (1 << i)) == 0)
                    continue;

                BuildSpot spot = _allSpots[i];
                string spotId = GetBuildSpotSaveId(spot);

                if (!string.IsNullOrEmpty(spotId))
                    builtIds.Add(spotId);
            }

            if (builtIds.Count > 0)
                PlayerProfileStorage.SetMainSceneBuiltSpotIdsForCurrentPlayer(ToIdArray(builtIds));

            return builtIds;
        }

        // Oldest legacy: only a sequential count was saved.
        int legacyCount = PlayerProfileStorage.GetMainSceneBuiltSpotCountForCurrentPlayer();
        int restoreCount = Mathf.Min(legacyCount, _allSpots.Count);

        for (int i = 0; i < restoreCount; i++)
        {
            string spotId = GetBuildSpotSaveId(_allSpots[i]);

            if (!string.IsNullOrEmpty(spotId))
                builtIds.Add(spotId);
        }

        if (builtIds.Count > 0)
            PlayerProfileStorage.SetMainSceneBuiltSpotIdsForCurrentPlayer(ToIdArray(builtIds));

        return builtIds;
    }

    private static string GetBuildSpotSaveId(BuildSpot spot)
    {
        return spot != null && spot.gameObject != null
            ? spot.gameObject.name
            : null;
    }

    private static string[] ToIdArray(HashSet<string> ids)
    {
        if (ids == null || ids.Count == 0)
            return Array.Empty<string>();

        string[] result = new string[ids.Count];
        ids.CopyTo(result);
        return result;
    }

    private static bool CanInteractWithBuildSpots()
    {
        if (GameManager.Instance == null)
            return true;

        if (!IsBuildMissionActive())
            return false;

        // Builds stay available during open business (no close/improve cycle).
        return GameManager.Instance.IsBuilding || GameManager.Instance.IsBusiness;
    }
}
