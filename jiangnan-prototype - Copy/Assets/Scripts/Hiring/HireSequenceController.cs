using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(300)]
public class HireSequenceController : MonoBehaviour
{
    [SerializeField] private List<HireSpot> _hireOrder;

    private MissionUiController _missionUi;
    private Coroutine _showHireSpotsRoutine;

    private void Awake()
    {
        RestoreSavedHireProgress();
    }

    private void OnEnable()
    {
        GameEvents.StateChanged += HandleStateChanged;
        GameEvents.MissionPartChanged += HandleMissionPartChanged;
        GameEvents.MissionPartCompleted += HandleMissionPartCompleted;
        GameEvents.BusinessSessionStarted += HandleBusinessSessionStarted;
        GameEvents.BusinessDowntimeStarted += HandleBusinessDowntimeStarted;
        GameEvents.SecondFloorUnlocked += HandleSecondFloorUnlocked;

        foreach (HireSpot spot in _hireOrder)
        {
            if (spot != null)
                spot.Clicked += HandleSpotClicked;
        }

        RefreshHireSequence();
    }

    private void Start()
    {
        RefreshHireSequence();
    }

    private void OnDisable()
    {
        GameEvents.StateChanged -= HandleStateChanged;
        GameEvents.MissionPartChanged -= HandleMissionPartChanged;
        GameEvents.MissionPartCompleted -= HandleMissionPartCompleted;
        GameEvents.BusinessSessionStarted -= HandleBusinessSessionStarted;
        GameEvents.BusinessDowntimeStarted -= HandleBusinessDowntimeStarted;
        GameEvents.SecondFloorUnlocked -= HandleSecondFloorUnlocked;

        if (_showHireSpotsRoutine != null)
        {
            StopCoroutine(_showHireSpotsRoutine);
            _showHireSpotsRoutine = null;
        }

        foreach (HireSpot spot in _hireOrder)
        {
            if (spot == null)
                continue;

            spot.Clicked -= HandleSpotClicked;
            spot.HireCompleted -= HandleSpotHireCompleted;
        }
    }

    public void OnStarterBuildsCompleted()
    {
        ShowHireSpotsAfterStarterBuild();
    }

    private void HandleStateChanged(GameState state)
    {
        RefreshHireSequence();
    }

    private void HandleMissionPartChanged(int partIndex)
    {
        RefreshHireSequence();
    }

    private void HandleMissionPartCompleted(int partIndex)
    {
        if (partIndex == MissionCatalog.StarterBuildMissionPartIndex)
            ShowHireSpotsAfterStarterBuild();
    }

    private void HandleBusinessSessionStarted()
    {
        // Hire spots stay available while the restaurant is open.
        RefreshHireSequence();
    }

    private void HandleBusinessDowntimeStarted()
    {
        RefreshHireSequence();
    }

    private void HandleSecondFloorUnlocked()
    {
        RefreshHireSequence();
    }

    private void ShowHireSpotsAfterStarterBuild()
    {
        if (_showHireSpotsRoutine != null)
            StopCoroutine(_showHireSpotsRoutine);

        _showHireSpotsRoutine = StartCoroutine(ShowHireSpotsAfterStarterBuildRoutine());
    }

    private IEnumerator ShowHireSpotsAfterStarterBuildRoutine()
    {
        yield return null;

        _showHireSpotsRoutine = null;

        if (!ShouldKeepHireSpotsAvailable())
            yield break;

        ActivatePendingHireSpots();
    }

    private void RefreshHireSequence()
    {
        if (ShouldKeepHireSpotsAvailable())
            ActivatePendingHireSpots();
        else
            LockUnhiredSpots();
    }

    private void ActivatePendingHireSpots()
    {
        if (_hireOrder.Count == 0)
        {
            CompleteHiring();
            return;
        }

        for (int i = 0; i < _hireOrder.Count; i++)
        {
            HireSpot spot = _hireOrder[i];

            if (spot == null || spot.IsHired || spot.State == HireSpotState.Hiring)
                continue;

            if (!CanActivateHireSpot(spot))
            {
                spot.SetState(HireSpotState.Locked);
                continue;
            }

            // Ground (and any other unlocked) hire spots appear together — not one-by-one.
            if (!spot.gameObject.activeSelf)
                spot.gameObject.SetActive(true);

            spot.ActivateForHiring();
        }

        SyncMissionHiredCounts();

        if (AreAllSpotsHired())
            CompleteHiring();
    }

    private static bool CanActivateHireSpot(HireSpot spot)
    {
        if (spot == null)
            return false;

        if (spot.Floor != RestaurantFloor.Second)
            return true;

        return RestaurantFloorUtil.IsUnlockedForCurrentPlayer();
    }

    private static bool ShouldKeepHireSpotsAvailable()
    {
        // Once business has opened (or is actively serving), hire spots stay available.
        if (PlayerProfileStorage.HasMainSceneBusinessStartedForCurrentPlayer()
            || (GameManager.Instance != null && GameManager.Instance.IsBusinessSessionActive))
        {
            return true;
        }

        int missionPart = GetCurrentMissionPartIndex();

        if (missionPart == MissionCatalog.HireMissionPartIndex)
            return true;

        if (missionPart > MissionCatalog.HireMissionPartIndex)
            return false;

        BuildSequenceController buildController = FindFirstObjectByType<BuildSequenceController>();
        return buildController != null && buildController.AreStarterBuildSpotsComplete();
    }

    private void CompleteHiring()
    {
        GameEvents.RaiseHiringCompleted();
    }

    private void HandleSpotClicked(HireSpot spot)
    {
        if (!ShouldKeepHireSpotsAvailable() || spot == null || spot.State != HireSpotState.Active)
            return;

        if (!CanActivateHireSpot(spot))
            return;

        // Second-floor hire walk-in is watched from the ground-floor camera (same as 一楼).
        if (spot.Floor == RestaurantFloor.Second)
            CharacterPanelController.Instance?.GoToFirstFloor();

        if (WorkerMovement.Instance == null)
            return;

        if (GoldManager.Instance == null || !GoldManager.Instance.TrySpend(spot.Cost))
            return;

        AudioManager.Play(SfxId.HireWorker);
        spot.HireCompleted += HandleSpotHireCompleted;
        spot.BeginHire();

        ActivatePendingHireSpots();
    }

    private void HandleSpotHireCompleted(HireSpot spot)
    {
        spot.HireCompleted -= HandleSpotHireCompleted;

        GameManager.Instance?.RegisterHired(spot.WorkerType);
        PlayerProfileStorage.SetMainSceneHiredSpotCountForCurrentPlayer(CountHiredSpots());

        // Absolute hire counts — second-floor waiter team must not complete from ground waiter alone.
        int chefs = 0;
        int waiters = 0;
        for (int i = 0; i < _hireOrder.Count; i++)
        {
            HireSpot hiredSpot = _hireOrder[i];
            if (hiredSpot == null || !hiredSpot.IsHired)
                continue;

            if (hiredSpot.WorkerType == WorkerType.Chef)
                chefs++;
            else if (hiredSpot.WorkerType == WorkerType.Waiter)
                waiters++;
        }

        EnsureMissionUi()?.SetHiredCounts(chefs, waiters);

        if (AreAllSpotsHired())
            CompleteHiring();
    }

    private int CountHiredSpots()
    {
        int count = 0;

        for (int i = 0; i < _hireOrder.Count; i++)
        {
            if (_hireOrder[i] != null && _hireOrder[i].IsHired)
                count++;
        }

        return count;
    }

    private bool AreAllSpotsHired()
    {
        if (_hireOrder.Count == 0)
            return true;

        for (int i = 0; i < _hireOrder.Count; i++)
        {
            HireSpot spot = _hireOrder[i];

            if (spot != null && !spot.IsHired)
                return false;
        }

        return true;
    }

    private void LockUnhiredSpots()
    {
        for (int i = 0; i < _hireOrder.Count; i++)
        {
            HireSpot spot = _hireOrder[i];

            if (spot == null || spot.IsHired || spot.State == HireSpotState.Hiring)
                continue;

            spot.SetState(HireSpotState.Locked);
        }
    }

    private static int GetCurrentMissionPartIndex()
    {
        int savedPart = PlayerProfileStorage.GetMainSceneMissionPartIndexForCurrentPlayer();
        MissionUiController missionUi = FindFirstObjectByType<MissionUiController>();

        if (missionUi == null)
            return savedPart;

        missionUi.EnsureInitialized();
        return Mathf.Max(missionUi.CurrentPartIndex, savedPart);
    }

    private void RestoreSavedHireProgress()
    {
        if (_hireOrder.Count == 0)
            return;

        int savedCount = PlayerProfileStorage.GetMainSceneHiredSpotCountForCurrentPlayer();
        int restoreCount = Mathf.Min(savedCount, _hireOrder.Count);

        for (int i = 0; i < restoreCount; i++)
        {
            HireSpot spot = _hireOrder[i];

            if (spot == null || spot.IsHired)
                continue;

            spot.RestoreHiredState();
            GameManager.Instance?.RegisterHired(spot.WorkerType);
        }
    }

    private void SyncMissionHiredCounts()
    {
        int chefs = 0;
        int waiters = 0;

        for (int i = 0; i < _hireOrder.Count; i++)
        {
            HireSpot spot = _hireOrder[i];

            if (spot == null || !spot.IsHired)
                continue;

            if (spot.WorkerType == WorkerType.Chef)
                chefs++;
            else if (spot.WorkerType == WorkerType.Waiter)
                waiters++;
        }

        EnsureMissionUi()?.SyncHiredCounts(chefs, waiters);
    }

    private MissionUiController EnsureMissionUi()
    {
        if (_missionUi != null)
        {
            _missionUi.EnsureInitialized();
            return _missionUi;
        }

        _missionUi = FindFirstObjectByType<MissionUiController>();

        if (_missionUi == null)
        {
            GameObject missionRoot = GameObject.Find("Mission");

            if (missionRoot != null)
                _missionUi = missionRoot.GetComponent<MissionUiController>()
                    ?? missionRoot.AddComponent<MissionUiController>();
        }

        _missionUi?.EnsureInitialized();
        return _missionUi;
    }
}
