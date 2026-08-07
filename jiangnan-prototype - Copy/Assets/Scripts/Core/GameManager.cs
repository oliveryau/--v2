using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Start Requirements")]
    [SerializeField] private int _requiredReceptions;
    [SerializeField] private int _requiredTables;
    [SerializeField] private int _requiredStoves;

    [Header("Fire Evacuation")]
    [SerializeField] private float _fireEvacuationDuration;

    public GameState State { get; private set; } = GameState.Building;
    public bool IsBuilding => State == GameState.Building;
    public bool IsHiring => State == GameState.Hiring;
    public bool IsBusiness => State == GameState.Business;
    public bool IsBusinessSessionActive { get; private set; }
    /// <summary>True while customers are being served.</summary>
    public bool IsBusinessOpen => IsBusinessSessionActive;
    /// <summary>
    /// Legacy close-summary flag. Close/reopen cycles are removed; kept for compatibility.
    /// </summary>
    public bool IsBusinessCloseSummaryPending { get; private set; }
    /// <summary>Legacy downtime flag. Always false with the continuous open loop.</summary>
    public bool IsBusinessClosed => false;
    /// <summary>Alias for <see cref="IsBusinessClosed"/>.</summary>
    public bool IsBusinessDowntime => IsBusinessClosed;
    public float BusinessSessionRemainingSeconds => 0f;

    private readonly Dictionary<PlaceableType, int> _placedCounts = new();
    private readonly Dictionary<WorkerType, int> _hiredCounts = new();
    private float _fireEvacuationTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (RestaurantSceneMode.IsCompetitorScene)
        {
            SetState(GameState.Business);
            OpenBusinessSession();
            return;
        }

        if (PlayerProfileStorage.HasMainSceneBusinessStartedForCurrentPlayer())
        {
            // Resume serving immediately — no close/improve cycle.
            SetState(GameState.Business);
            OpenBusinessSession();
            return;
        }

        SetState(GameState.Building);
        GameEvents.RaiseStateChanged(State);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (State != GameState.FireEvacuation)
            return;

        _fireEvacuationTimer -= Time.deltaTime;
        if (_fireEvacuationTimer <= 0f)
        {
            SetState(GameState.Business);
            if (!IsBusinessSessionActive
                && PlayerProfileStorage.HasMainSceneBusinessStartedForCurrentPlayer())
            {
                OpenBusinessSession();
            }
        }
    }

    public bool TryStartHiring()
    {
        // Early missions gate hire spots by mission part while staying in Building.
        return State == GameState.Building;
    }

    public bool TryStartBusiness()
    {
        if (State != GameState.Building)
            return false;

        SetState(GameState.Business);
        PlayerProfileStorage.SetMainSceneBusinessStartedForCurrentPlayer();
        return true;
    }

    public bool TryOpenBusinessSession()
    {
        if (!RestaurantSceneMode.IsMainScene && !RestaurantSceneMode.IsCompetitorScene)
            return false;

        if (IsBusinessSessionActive)
            return false;

        if (State == GameState.Building)
        {
            if (!TryStartBusiness())
                return false;
        }
        else if (State != GameState.Business)
        {
            return false;
        }

        OpenBusinessSession();
        return true;
    }

    public void ResumeBusinessIfPreviouslyStarted()
    {
        if (!PlayerProfileStorage.HasMainSceneBusinessStartedForCurrentPlayer())
            return;

        SetState(GameState.Business);
        if (!IsBusinessSessionActive)
            OpenBusinessSession();
    }

    public void TriggerFire()
    {
        if (State != GameState.Business)
            return;

        if (IsBusinessSessionActive)
            EndBusinessSession(showCloseSummary: false);

        SetState(GameState.FireEvacuation);
        _fireEvacuationTimer = _fireEvacuationDuration;
    }

    public void RegisterPlaced(PlaceableType type)
    {
        _placedCounts.TryGetValue(type, out int count);
        _placedCounts[type] = count + 1;
    }

    public void RegisterHired(WorkerType type)
    {
        _hiredCounts.TryGetValue(type, out int count);
        _hiredCounts[type] = count + 1;
    }

    private void OpenBusinessSession()
    {
        IsBusinessCloseSummaryPending = false;
        IsBusinessSessionActive = true;
        GoldManager.Instance?.BeginBusinessSessionIncomeTracking();
        GameEvents.RaiseBusinessSessionStarted();
    }

    private void EndBusinessSession(bool showCloseSummary = true)
    {
        if (!IsBusinessSessionActive)
            return;

        IsBusinessSessionActive = false;
        // Close/reopen overview cycle removed — only used for temporary pauses (e.g. fire).
        IsBusinessCloseSummaryPending = false;
        GameEvents.RaiseBusinessSessionEnded();
    }

    public void AcknowledgeBusinessCloseSummary()
    {
        IsBusinessCloseSummaryPending = false;

        if (PlayerProfileStorage.HasMainSceneBusinessStartedForCurrentPlayer()
            && !IsBusinessSessionActive
            && State != GameState.FireEvacuation)
        {
            SetState(GameState.Business);
            OpenBusinessSession();
        }
    }

    /// <summary>
    /// Legacy entry for the old improve-between-sessions loop. Reopens business instead.
    /// </summary>
    public void EnterBusinessImprovementPhase(bool resetSessionIncome = true)
    {
        IsBusinessCloseSummaryPending = false;

        if (resetSessionIncome)
            GoldManager.Instance?.ResetBusinessSessionIncome();

        if (PlayerProfileStorage.HasMainSceneBusinessStartedForCurrentPlayer())
        {
            SetState(GameState.Business);
            if (!IsBusinessSessionActive)
                OpenBusinessSession();
            return;
        }

        SetState(GameState.Building);
    }

    /// <summary>
    /// Legacy closed-state entry. Reopens business if it was already started.
    /// </summary>
    public void EnterBusinessClosed()
    {
        EnterBusinessImprovementPhase(resetSessionIncome: false);
    }

    public void TryRaiseBusinessFloorClearedIfEmpty()
    {
        // Close-summary flow removed.
    }

    private void SetState(GameState state)
    {
        if (State == state)
            return;

        State = state;
        GameEvents.RaiseStateChanged(state);
    }
}
