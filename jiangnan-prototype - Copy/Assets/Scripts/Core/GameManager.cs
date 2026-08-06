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

    [Header("Business Session")]
    [SerializeField] private float _businessSessionDurationSeconds = 180f;

    [Header("Fire Evacuation")]
    [SerializeField] private float _fireEvacuationDuration;

    public GameState State { get; private set; } = GameState.Building;
    public bool IsBuilding => State == GameState.Building;
    public bool IsHiring => State == GameState.Hiring;
    public bool IsBusiness => State == GameState.Business;
    public bool IsBusinessSessionActive { get; private set; }
    /// <summary>True while the timed open session is running (serve customers).</summary>
    public bool IsBusinessOpen => IsBusinessSessionActive;
    /// <summary>
    /// Waiting for remaining customers to leave / overview acknowledge after a session ends.
    /// Build/hire/Open Business stay hidden during this window.
    /// </summary>
    public bool IsBusinessCloseSummaryPending { get; private set; }
    /// <summary>
    /// Closed between sessions: Building (or Hiring) so players can improve the restaurant.
    /// </summary>
    public bool IsBusinessClosed =>
        !IsBusinessSessionActive
        && !IsBusinessCloseSummaryPending
        && PlayerProfileStorage.HasMainSceneBusinessStartedForCurrentPlayer()
        && IsBuilding;
    /// <summary>Alias for <see cref="IsBusinessClosed"/>.</summary>
    public bool IsBusinessDowntime => IsBusinessClosed;
    public float BusinessSessionRemainingSeconds => Mathf.Max(0f, _businessSessionRemaining);

    private readonly Dictionary<PlaceableType, int> _placedCounts = new();
    private readonly Dictionary<WorkerType, int> _hiredCounts = new();
    private float _fireEvacuationTimer;
    private float _businessSessionRemaining;

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
            // Resume in the closed/improve loop so build spots and Open Business return.
            EnterBusinessImprovementPhase(resetSessionIncome: false);
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
        if (State == GameState.FireEvacuation)
        {
            _fireEvacuationTimer -= Time.deltaTime;
            if (_fireEvacuationTimer <= 0f)
                SetState(GameState.Building);

            return;
        }

        if (!IsBusinessSessionActive)
            return;

        _businessSessionRemaining -= Time.deltaTime;

        if (_businessSessionRemaining <= 0f)
            EndBusinessSession();
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

        if (IsBusinessSessionActive || IsBusinessCloseSummaryPending)
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
        _businessSessionRemaining = Mathf.Max(1f, _businessSessionDurationSeconds);
        GoldManager.Instance?.BeginBusinessSessionIncomeTracking();
        GameEvents.RaiseBusinessSessionStarted();
    }

    private void EndBusinessSession(bool showCloseSummary = true)
    {
        if (!IsBusinessSessionActive)
            return;

        IsBusinessSessionActive = false;
        _businessSessionRemaining = 0f;
        // Keep session income tracking on so late payments still count toward the overview.

        if (showCloseSummary && RestaurantSceneMode.IsMainScene)
            IsBusinessCloseSummaryPending = true;

        GameEvents.RaiseBusinessSessionEnded();

        if (IsBusinessCloseSummaryPending)
            TryRaiseBusinessFloorClearedIfEmpty();
    }

    public void AcknowledgeBusinessCloseSummary()
    {
        if (!IsBusinessCloseSummaryPending)
            return;

        EnterBusinessImprovementPhase(resetSessionIncome: true);
    }

    /// <summary>
    /// Return to the building/improvement phase between timed business sessions.
    /// </summary>
    public void EnterBusinessImprovementPhase(bool resetSessionIncome = true)
    {
        if (IsBusinessSessionActive)
            return;

        IsBusinessCloseSummaryPending = false;

        if (resetSessionIncome)
            GoldManager.Instance?.ResetBusinessSessionIncome();

        SetState(GameState.Building);
        GameEvents.RaiseBusinessDowntimeStarted();
    }

    /// <summary>
    /// Enter the closed/improve state (Building). Safe to call if already closed.
    /// </summary>
    public void EnterBusinessClosed()
    {
        if (IsBusinessSessionActive)
            return;

        if (IsBusinessCloseSummaryPending)
        {
            AcknowledgeBusinessCloseSummary();
            return;
        }

        EnterBusinessImprovementPhase(resetSessionIncome: false);
    }

    public void TryRaiseBusinessFloorClearedIfEmpty()
    {
        if (!IsBusinessCloseSummaryPending)
            return;

        if (CustomerManager.Instance != null && !CustomerManager.Instance.IsRestaurantClearForCloseSummary())
            return;

        GameEvents.RaiseBusinessFloorCleared();
    }

    private void SetState(GameState state)
    {
        if (State == state)
            return;

        State = state;
        GameEvents.RaiseStateChanged(state);
    }
}
