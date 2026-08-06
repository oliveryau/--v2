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
        LockUnhiredSpots();
    }

    private void HandleBusinessDowntimeStarted()
    {
        if (!ShouldKeepHireSpotsAvailable())
            LockUnhiredSpots();
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

        bool activatedFirstPending = false;

        for (int i = 0; i < _hireOrder.Count; i++)
        {
            HireSpot spot = _hireOrder[i];

            if (spot == null || spot.IsHired || spot.State == HireSpotState.Hiring)
                continue;

            if (!activatedFirstPending)
            {
                spot.ActivateForHiring();
                activatedFirstPending = true;
            }
            else
            {
                spot.SetState(HireSpotState.Locked);
            }
        }

        SyncMissionHiredCounts();

        if (AreAllSpotsHired())
            CompleteHiring();
    }

    private static bool ShouldKeepHireSpotsAvailable()
    {
        if (PlayerProfileStorage.HasMainSceneBusinessStartedForCurrentPlayer())
            return false;

        if (GameManager.Instance != null
            && (GameManager.Instance.IsBusinessSessionActive
                || GameManager.Instance.IsBusinessCloseSummaryPending))
        {
            return false;
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

        if (WorkerMovement.Instance == null)
            return;

        if (GoldManager.Instance == null || !GoldManager.Instance.TrySpend(spot.Cost))
            return;

        AudioManager.Play(SfxId.HireWorker);
        spot.HireCompleted += HandleSpotHireCompleted;
        spot.BeginHire();

        ActivateNextPendingSpot();
    }

    private void HandleSpotHireCompleted(HireSpot spot)
    {
        spot.HireCompleted -= HandleSpotHireCompleted;

        GameManager.Instance?.RegisterHired(spot.WorkerType);
        PlayerProfileStorage.SetMainSceneHiredSpotCountForCurrentPlayer(CountHiredSpots());
        EnsureMissionUi()?.NotifyWorkerHired(spot.WorkerType);

        if (AreAllSpotsHired())
            CompleteHiring();
    }

    private void ActivateNextPendingSpot()
    {
        for (int i = 0; i < _hireOrder.Count; i++)
        {
            HireSpot spot = _hireOrder[i];

            if (spot == null || spot.IsHired || spot.State == HireSpotState.Hiring)
                continue;

            if (spot.State == HireSpotState.Active)
                return;

            spot.ActivateForHiring();
            return;
        }
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
