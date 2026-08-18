using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class CustomerManager : MonoBehaviour
{
    public static CustomerManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private CustomerPool _customerPool;
    [SerializeField] private CustomerQueue _customerQueue;
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private Transform _exitPoint;

    [Header("Spawn")]
    [SerializeField] private float _spawnInterval;
    [SerializeField] private int _maxCustomersForFirstTable = 8;
    [SerializeField] private int _maxCustomersPerAdditionalTable = 4;
    [SerializeField] private int _vipSpawnInterval;
    [SerializeField] private int _maxActiveVips;
    [Tooltip("After this many VIPs have fully left (happy or discontent), stop spawning more VIPs. Lull starts after the following prankster visit. 0 = unlimited.")]
    [SerializeField] private int _servedVipSpawnStopCount = 1;
    [Tooltip("Max active customers during the post-VIP lull.")]
    [SerializeField] private int _postVipServeMaxCustomers = 4;
    [Tooltip("Spawn interval during the post-VIP lull.")]
    [SerializeField] private float _postVipServeSpawnInterval = 5f;
    [SerializeField] private Transform _vipEntryWaypoint;

    [Header("Business Resume Prewarm")]
    [Tooltip("When returning to an open business outside the post-VIP lull, seed this many mid-visit customers.")]
    [SerializeField] private int _resumePrewarmMinCustomers = 4;
    [SerializeField] private int _resumePrewarmMaxCustomers = 8;
    [Tooltip("When returning during the post-VIP lull, seed this lighter mid-visit count.")]
    [SerializeField] private int _resumePrewarmLullMinCustomers = 2;
    [SerializeField] private int _resumePrewarmLullMaxCustomers = 4;

    [Header("Timings")]
    [SerializeField] private float _queueWaitTimeout;
    [SerializeField] private float _foodWaitTimeout;
    [SerializeField] private float _eatDuration;
    [Tooltip("Competitor scene only: seconds a VIP waits at the VIP waypoint after a seat is free before walking to it.")]
    [SerializeField] private float _vipWaypointHoldSeconds = 5f;
    [Tooltip("Competitor scene only: seconds a normal customer waits in queue after a seat is free before walking to it.")]
    [SerializeField] private float _competitorQueueHoldSeconds = 3f;
    [Tooltip("Competitor scene only: extra delay per queue slot after Waypoint_1 before walking to a seat (slot 0 = Waypoint_1).")]
    [SerializeField] private float _competitorSeatStaggerSeconds = 1f;

    [Header("VIP Events")]
    [SerializeField] private float _vipSettleDelay = 3f;
    [SerializeField] private float _vipEventGapDelay = 1.5f;
    [SerializeField] private int _vipEventBonusPerRequest = 1000;
    [SerializeField] private float _vipCallLadyLeaveDelay = 2.5f;
    [Tooltip("Minimum seconds a call lady stays on Idle or CleanTable at a lackey point.")]
    [SerializeField] private float _callLadyPostAnimMinDuration = 2f;
    [Tooltip("Maximum seconds a call lady stays on Idle or CleanTable before picking again.")]
    [SerializeField] private float _callLadyPostAnimMaxDuration = 5f;
    [SerializeField] private Worker[] _vipCallLadyWorkers;
    [SerializeField] private Transform[] _vipLackeyPoints;
    [SerializeField] private Transform[] _vipCallLadyWaypoints;
    [SerializeField] private Worker _vipPerformer;
    [Tooltip("Optional GeTai prop reference.")]
    [SerializeField] private Transform _vipStagePoint;
    [Tooltip("Where the GeTai performer stands.")]
    [SerializeField] private Transform _vipPerformStagePoint;
    [Tooltip("World anchor for the 表演 button.")]
    [SerializeField] private Transform _vipPerformStageUiPoint;
    [SerializeField] private Transform _vipPerformerWaypoint;
    [Tooltip("Looping music notes under GeTai. Plays while the performer is on stage.")]
    [SerializeField] private ParticleSystem _vipStageMusicParticles;
    [Tooltip("AudioSource child on the performer that plays performer_loop while on stage.")]
    [SerializeField] private AudioSource _vipStageAudioSource;

    private readonly Dictionary<Customer, Coroutine> _activeFlows = new();
    private readonly HashSet<Customer> _completedPayments = new();
    private readonly Dictionary<Transform, int> _awaitingPaymentCounts = new();
    private readonly Dictionary<Transform, int> _vipAwaitingPaymentCounts = new();
    private readonly List<TableSeat> _registeredSeats = new();
    private Coroutine _spawnRoutine;
    private Coroutine _callLadyDismissRoutine;
    private readonly Dictionary<Worker, Coroutine> _callLadyPostAnimRoutines = new();
    private Coroutine _performerReturnRoutine;
    private int _spawnCount;
    private bool _awaitingVipAfterPrankster;
    private int _customersSincePranksterLeft;
    private int _servedVipCount;
    private int _runtimeVipSpawnStopCount;
    private bool _appliedCurrentLullSideEffects;
    private bool _pendingLullAfterPrankster;
    private bool _callLadiesActive;
    private bool _performerOnStage;
    private bool _vipStageMusicParticlesInitialized;
    private Customer _vipAwaitingIntro;
    private bool _vipIntroAcknowledged;
    private VipEventType? _pendingVipEvent;
    private Customer _pendingVipEventCustomer;
    private bool _vipEventAcknowledged;

    private PranksterManager _pranksterManager;
    private CompetitorVisitorManager _competitorVisitorManager;
    private const string VipStageMusicParticlesName = "Music Particles";
    private const string PerformerAudioSourceName = "AudioSource";

    /// <summary>Optional GeTai prop reference.</summary>
    public Transform VipStagePoint
    {
        get
        {
            CacheVipPerformerReferences();
            return _vipStagePoint;
        }
    }

    /// <summary>World anchor for the 表演 button (not the performer stand).</summary>
    public Transform VipPerformStageUiPoint
    {
        get
        {
            CacheVipPerformerReferences();
            return _vipPerformStageUiPoint;
        }
    }

    /// <summary>Where the GeTai performer stands.</summary>
    public Transform VipPerformStagePoint
    {
        get
        {
            CacheVipPerformerReferences();
            return _vipPerformStagePoint;
        }
    }

    public IReadOnlyList<Customer> ActiveCustomers => _customerPool != null
        ? _customerPool.ActiveCustomers
        : System.Array.Empty<Customer>();

    public bool HasActiveCustomers =>
        _customerPool != null && _customerPool.ActiveCustomers.Count > 0;

    /// <summary>True while a VIP request button should stay on screen waiting for a tap.</summary>
    public bool VipEventAwaitingTap =>
        _pendingVipEvent.HasValue && !_vipEventAcknowledged && _pendingVipEventCustomer != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_customerQueue == null)
            _customerQueue = GetComponent<CustomerQueue>();

        if (_vipEntryWaypoint == null)
            _vipEntryWaypoint = FindTransformByName("Customer Waypoint_4");

        CacheVipCallLadyReferences();
        CacheVipPerformerReferences();
        HideVipCallLadyWorkers();
        HideVipPerformerIfUnhired();
    }

    private void Start()
    {
        // HireSequence restores after our Awake; sync stationed call ladies / performer once that finishes.
        CacheVipCallLadyReferences();
        CacheVipPerformerReferences();
    }

    private void CacheVipCallLadyReferences()
    {
        if (_vipLackeyPoints == null || _vipLackeyPoints.Length == 0)
        {
            Transform lackey1 = FindTransformByName("Lackey Point (1)");
            Transform lackey2 = FindTransformByName("Lackey Point (2)");
            _vipLackeyPoints = new[] { lackey1, lackey2 };
        }

        if (_vipCallLadyWaypoints == null || _vipCallLadyWaypoints.Length == 0)
        {
            Transform waypoint1 = FindTransformByName("CallLady Waypoint (1)");
            Transform waypoint2 = FindTransformByName("CallLady Waypoint (2)");
            _vipCallLadyWaypoints = new[] { waypoint1, waypoint2 };
        }

        if (_vipCallLadyWorkers != null && _vipCallLadyWorkers.Length > 0)
            return;

        Worker[] workers = FindObjectsByType<Worker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<Worker> callLadies = new();

        for (int i = 0; i < workers.Length; i++)
        {
            Worker worker = workers[i];
            if (worker == null || !worker.ExcludeFromServicePool)
                continue;

            // Skip the stage performer — only CallLady extras belong here.
            if (_vipPerformer != null && worker == _vipPerformer)
                continue;

            if (worker.name.IndexOf("Performer", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            callLadies.Add(worker);
        }

        if (callLadies.Count > 0)
            _vipCallLadyWorkers = callLadies.ToArray();
    }

    private void HideVipCallLadyWorkers()
    {
        if (_vipCallLadyWorkers == null)
            return;

        // Hire restore (later Awake) may already have stationed them — leave those active.
        if (AreCallLadiesAlreadyStationed())
            return;

        StopAllCallLadyPostAnims(returnToIdle: true);

        for (int i = 0; i < _vipCallLadyWorkers.Length; i++)
        {
            Worker worker = _vipCallLadyWorkers[i];
            if (worker == null)
                continue;

            if (IsWorkerOwnedByHiredOrHiringSpot(worker))
                continue;

            worker.gameObject.SetActive(false);
        }

        _callLadiesActive = false;
    }

    private bool AreCallLadiesAlreadyStationed()
    {
        if (_vipCallLadyWorkers == null)
            return false;

        for (int i = 0; i < _vipCallLadyWorkers.Length; i++)
        {
            Worker worker = _vipCallLadyWorkers[i];
            if (worker != null && worker.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    private void CacheVipPerformerReferences()
    {
        // Optional GeTai prop reference.
        if (_vipStagePoint == null)
            _vipStagePoint = FindTransformByName("GeTai");

        // Performer stand. Never fall back to GeTai.
        if (_vipPerformStagePoint == null)
        {
            _vipPerformStagePoint = FindTransformByName("Stage Position")
                ?? FindTransformByName("Placeholder Stage");
        }

        if (_vipPerformStageUiPoint == null)
            _vipPerformStageUiPoint = FindTransformByName("Stage UI Position");

        if (_vipPerformerWaypoint == null)
            _vipPerformerWaypoint = FindTransformByName("Performer Waypoint");

        CacheVipStageMusicParticles();

        if (_vipPerformer == null)
        {
            Worker[] workers = FindObjectsByType<Worker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < workers.Length; i++)
            {
                Worker worker = workers[i];
                if (worker == null || !worker.ExcludeFromServicePool || !worker.ServesVipFloorOnly)
                    continue;

                if (worker.name.IndexOf("Performer", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                _vipPerformer = worker;
                break;
            }
        }

        CacheVipStageAudioSource();
    }

    private void HideVipPerformerIfUnhired()
    {
        CacheVipPerformerReferences();
        if (_vipPerformer == null)
            return;

        // Leave alone if the second-floor hire already walked them in / restored them.
        if (IsWorkerOwnedByHiredOrHiringSpot(_vipPerformer))
            return;

        _vipPerformer.gameObject.SetActive(false);
        _performerOnStage = false;
        StopVipStageMusic();
    }

    private static bool IsWorkerOwnedByHiredOrHiringSpot(Worker worker)
    {
        if (worker == null)
            return false;

        HireSpot[] spots = FindObjectsByType<HireSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < spots.Length; i++)
        {
            HireSpot spot = spots[i];
            if (spot == null || spot.Workers == null)
                continue;

            if (spot.State != HireSpotState.Hired && spot.State != HireSpotState.Hiring)
                continue;

            for (int j = 0; j < spot.Workers.Length; j++)
            {
                if (spot.Workers[j] == worker.gameObject)
                    return true;
            }
        }

        return false;
    }

    private void OnDestroy()
    {
        StopVipStageMusic();

        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        GameEvents.StateChanged += HandleStateChanged;
        GameEvents.BusinessSessionStarted += HandleBusinessSessionStarted;
        GameEvents.BusinessSessionEnded += HandleBusinessSessionEnded;

        if (GameManager.Instance != null
            && GameManager.Instance.IsBusiness
            && GameManager.Instance.IsBusinessSessionActive)
        {
            BeginSpawning();
        }
    }

    private void HandleBusinessSessionStarted()
    {
        BeginSpawning();
    }

    private void HandleBusinessSessionEnded()
    {
        // Stop new arrivals, but let seated customers finish their visits.
        StopSpawning();
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Business)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsBusinessSessionActive)
                BeginSpawning();

            return;
        }

        StopSpawning();
        ResetAlternationSpawnTracking();

        if (_customerPool != null && _customerPool.ActiveCustomers.Count > 0)
            EvacuateAllCustomers();
    }

    public void AcknowledgeVipIntro()
    {
        if (_vipAwaitingIntro == null)
            return;

        _vipIntroAcknowledged = true;

        if (RestaurantSceneMode.IsMainScene)
            PlayerProfileStorage.SetMainSceneVipSeatedVisitPendingForCurrentPlayer();
    }

    public void AcknowledgeVipEvent(VipEventType eventType)
    {
        if (!VipEventAwaitingTap || !_pendingVipEvent.HasValue || _pendingVipEvent.Value != eventType)
            return;

        _vipEventAcknowledged = true;
    }

    private void BeginPendingVipEvent(Customer customer, VipEventType eventType)
    {
        _pendingVipEvent = eventType;
        _pendingVipEventCustomer = customer;
        _vipEventAcknowledged = false;
    }

    private void ClearPendingVipEvent()
    {
        _pendingVipEvent = null;
        _pendingVipEventCustomer = null;
        _vipEventAcknowledged = false;
    }

    /// <summary>
    /// Only the VIP who owns the pending request may clear/hide event UI.
    /// Normal customers leaving must not wipe the VIP button/timer mid-wait.
    /// </summary>
    private void ClearVipEventUiIfOwnedBy(Customer customer)
    {
        if (customer == null)
            return;

        UIManager.Instance?.HideVipWaitTimer(customer);

        if (_pendingVipEventCustomer == null || _pendingVipEventCustomer != customer)
            return;

        ClearPendingVipEvent();
        UIManager.Instance?.HideAllVipEventButtons(restoreCravingDialogue: false);
    }

    private void ReleaseCustomerToPool(Customer customer)
    {
        if (customer == null)
            return;

        _customerPool.Release(customer);
        NotifyFloorClearedIfNeeded();
    }

    private static void NotifyFloorClearedIfNeeded()
    {
        GameManager.Instance?.TryRaiseBusinessFloorClearedIfEmpty();
    }

    private void OnDisable()
    {
        PersistSeatedVipVisitIfNeeded();

        GameEvents.StateChanged -= HandleStateChanged;
        GameEvents.BusinessSessionStarted -= HandleBusinessSessionStarted;
        GameEvents.BusinessSessionEnded -= HandleBusinessSessionEnded;
        StopSpawning();
        StopAllCustomerFlows();

        if (_customerPool != null)
            _customerPool.ReleaseAll();

        _completedPayments.Clear();
        _awaitingPaymentCounts.Clear();
        _vipAwaitingPaymentCounts.Clear();
        StopAllCallLadyPostAnims(returnToIdle: false);
    }

    public void NotifyPranksterVisitEndedForAlternation()
    {
        if (!IsVipPranksterAlternationEnabled())
            return;

        _customersSincePranksterLeft = 0;

        // Rival owner only drops by after the last prankster, right before the lull opens.
        if (_pendingLullAfterPrankster && TryScheduleCompetitorVisitorVisit())
            return;

        FinishAlternationTurn();
    }

    public void NotifyCompetitorVisitorEndedForAlternation()
    {
        if (!IsVipPranksterAlternationEnabled())
            return;

        FinishAlternationTurn();
    }

    private void FinishAlternationTurn()
    {
        // Final VIP already left: finish this turn, then open the lull (portal).
        if (_pendingLullAfterPrankster)
        {
            _pendingLullAfterPrankster = false;
            _awaitingVipAfterPrankster = false;
            TryUnlockPostVipLull(offerPortalPresentation: true);
            return;
        }

        _awaitingVipAfterPrankster = true;
    }

    private bool TryScheduleCompetitorVisitorVisit()
    {
        if (_competitorVisitorManager == null)
            _competitorVisitorManager = FindFirstObjectByType<CompetitorVisitorManager>();

        return _competitorVisitorManager != null && _competitorVisitorManager.TryScheduleVisit();
    }

    public void NotifyVipLeftForAlternation()
    {
        if (!IsVipPranksterAlternationEnabled())
            return;

        // Exclusive turn: once we wait for a prankster, stop waiting for a VIP.
        _awaitingVipAfterPrankster = false;
        _customersSincePranksterLeft = 0;
    }

    public bool IsAwaitingVipForAlternation =>
        IsVipPranksterAlternationEnabled() && _awaitingVipAfterPrankster;

    public void CompletePayment(Customer customer)
    {
        if (customer == null || !IsAwaitingPayment(customer))
            return;

        MarkPaymentCompleted(customer);
        AwardPayment(ResolvePaymentForCustomer(customer));
        NotifyPaymentCollected(customer);
    }

    private static void BindPendingPayment(Customer customer, TableSeat seat)
    {
        if (customer == null || seat == null)
            return;

        DiningTable table = FindTableForSeat(seat);
        customer.PendingPaymentAnchor = seat.PaymentUiAnchor;
        customer.PendingPaymentTableLevel = table != null ? table.Level : 1;
        CustomerManager.Instance?.RegisterAwaitingPayment(customer);
    }

    private static int ResolvePaymentForCustomer(Customer customer)
    {
        if (customer == null)
            return 0;

        int tableLevel = customer.Seat != null
            ? (FindTableForSeat(customer.Seat)?.Level ?? 1)
            : customer.PendingPaymentTableLevel;

        return GoldManager.Instance.GetCustomerPayment(customer, tableLevel);
    }

    private static DiningTable FindTableForSeat(TableSeat seat)
    {
        return seat?.ParentTable;
    }

    public void RegisterAwaitingPayment(Customer customer)
    {
        Transform anchor = ResolvePaymentAnchor(customer);

        if (anchor == null)
            return;

        IncrementPaymentCount(_awaitingPaymentCounts, anchor);

        if (customer.IsVip)
            IncrementPaymentCount(_vipAwaitingPaymentCounts, anchor);
    }

    private void UnregisterAwaitingPayment(Customer customer)
    {
        Transform anchor = ResolvePaymentAnchor(customer);

        if (anchor == null)
            return;

        DecrementPaymentCount(_awaitingPaymentCounts, anchor);

        if (customer.IsVip)
            DecrementPaymentCount(_vipAwaitingPaymentCounts, anchor);
    }

    private static Transform ResolvePaymentAnchor(Customer customer)
    {
        if (customer == null)
            return null;

        if (customer.PendingPaymentAnchor != null)
            return customer.PendingPaymentAnchor;

        return customer.Seat != null ? customer.Seat.PaymentUiAnchor : null;
    }

    private static void IncrementPaymentCount(Dictionary<Transform, int> counts, Transform anchor)
    {
        counts.TryGetValue(anchor, out int count);
        counts[anchor] = count + 1;
    }

    private static void DecrementPaymentCount(Dictionary<Transform, int> counts, Transform anchor)
    {
        if (!counts.TryGetValue(anchor, out int count))
            return;

        count--;

        if (count <= 0)
            counts.Remove(anchor);
        else
            counts[anchor] = count;
    }

    public void CompletePaymentsAtPaymentAnchor(Transform paymentUiAnchor)
    {
        CompletePaymentsAtPaymentAnchor(paymentUiAnchor, awardGold: true);
    }

    /// <summary>
    /// Completes all awaiting payments at the anchor. When <paramref name="awardGold"/> is false,
    /// returns the payment total without adding gold (used so VIP treasure can award on chest open).
    /// </summary>
    public int CompletePaymentsAtPaymentAnchor(Transform paymentUiAnchor, bool awardGold)
    {
        if (paymentUiAnchor == null || _customerPool == null)
            return 0;

        IReadOnlyList<Customer> customers = _customerPool.ActiveCustomers;
        int totalPayment = 0;
        int customersServed = 0;

        for (int i = 0; i < customers.Count; i++)
        {
            Customer customer = customers[i];

            if (!IsAwaitingPayment(customer))
                continue;

            if (!MatchesPaymentAnchor(customer, paymentUiAnchor))
                continue;

            MarkPaymentCompleted(customer);

            totalPayment += ResolvePaymentForCustomer(customer);
            customersServed++;
        }

        if (customersServed == 0)
            return 0;

        if (awardGold)
            AwardPayment(totalPayment);

        NotifyTableStatusForPaymentAnchor(paymentUiAnchor);
        return totalPayment;
    }

    private void MarkPaymentCompleted(Customer customer)
    {
        UnregisterAwaitingPayment(customer);
        _completedPayments.Add(customer);
    }

    private static void AwardPayment(int payment)
    {
        if (payment > 0 && GoldManager.Instance != null)
            GoldManager.Instance.AddGold(payment);
    }

    private static TableSeat ResolveActiveSeat(Customer customer, TableSeat fallbackSeat = null)
    {
        if (customer?.Seat != null)
            return customer.Seat;

        return fallbackSeat;
    }

    private static void ReleaseSeatAndNotify(TableSeat seat)
    {
        if (seat == null)
            return;

        seat.Release();
        NotifyTableSeatChanged(seat);
    }

    private static void ReleaseCustomerSeatAndNotify(Customer customer, TableSeat fallbackSeat = null)
    {
        ReleaseSeatAndNotify(ResolveActiveSeat(customer, fallbackSeat));
    }

    private static void NotifyPaymentCollected(Customer customer)
    {
        if (customer == null)
            return;

        if (customer.Seat != null)
            NotifyTableSeatChanged(customer.Seat);
        else
            NotifyTableStatusForPaymentAnchor(customer.PendingPaymentAnchor);
    }

    private static bool MatchesPaymentAnchor(Customer customer, Transform paymentUiAnchor)
    {
        if (customer == null || paymentUiAnchor == null)
            return false;

        if (customer.Seat != null && customer.Seat.PaymentUiAnchor == paymentUiAnchor)
            return true;

        return customer.PendingPaymentAnchor == paymentUiAnchor;
    }

    public bool IsAwaitingPayment(Customer customer)
    {
        return customer != null
            && customer.State == CustomerState.Paying
            && !_completedPayments.Contains(customer);
    }

    public bool HasAwaitingPaymentsAt(Transform paymentUiAnchor)
    {
        return HasAwaitingPaymentsAt(paymentUiAnchor, vipOnly: false);
    }

    public bool HasVipAwaitingPaymentsAt(Transform paymentUiAnchor)
    {
        return HasAwaitingPaymentsAt(paymentUiAnchor, vipOnly: true);
    }

    private bool HasAwaitingPaymentsAt(Transform paymentUiAnchor, bool vipOnly)
    {
        if (paymentUiAnchor == null)
            return false;

        Dictionary<Transform, int> counts = vipOnly ? _vipAwaitingPaymentCounts : _awaitingPaymentCounts;
        return counts.TryGetValue(paymentUiAnchor, out int count) && count > 0;
    }

    public void RegisterSeats(IReadOnlyList<TableSeat> seats)
    {
        if (seats == null)
            return;

        for (int i = 0; i < seats.Count; i++)
        {
            TableSeat seat = seats[i];

            if (seat != null && !_registeredSeats.Contains(seat))
                _registeredSeats.Add(seat);
        }
    }

    public void UnregisterSeats(IReadOnlyList<TableSeat> seats)
    {
        if (seats == null)
            return;

        for (int i = 0; i < seats.Count; i++)
            _registeredSeats.Remove(seats[i]);
    }

    public void RebindAwaitingPaymentSeatIfNeeded(Customer customer, TableSeat seat)
    {
        if (customer == null || seat == null || !IsAwaitingPayment(customer))
            return;

        UnregisterAwaitingPayment(customer);
        BindPendingPayment(customer, seat);
    }

    private void BeginSpawning()
    {
        if (_spawnRoutine != null)
            return;

        bool resumeAfterSteal = false;
        int stolenShopCount = 0;
        if (RestaurantSceneMode.IsMainScene)
            resumeAfterSteal = CompetitorSceneSelection.ConsumePendingBusinessResumeAfterSteal(out stolenShopCount);

        RestoreServedVipCountFromSave();
        RestoreVipSpawnStopCountFromSave();

        // Successful competitor steal exits post-VIP lull traffic and restarts the VIP cycle.
        if (resumeAfterSteal)
        {
            _servedVipCount = 0;
            PlayerProfileStorage.SetMainSceneServedVipCountForCurrentPlayer(0);
            ResetAlternationSpawnTracking();
            _appliedCurrentLullSideEffects = false;

            // Base limit (usually 1) + one VIP per distinct competitor shop stolen from this outing.
            int baseStopCount = Mathf.Max(1, _servedVipSpawnStopCount);
            _runtimeVipSpawnStopCount = baseStopCount + Mathf.Max(0, stolenShopCount);
            PlayerProfileStorage.SetMainSceneVipSpawnStopCountOverrideForCurrentPlayer(_runtimeVipSpawnStopCount);
            _pendingLullAfterPrankster = false;
            PlayerProfileStorage.ClearMainSceneVipSeatedVisitPendingForCurrentPlayer();
        }

        // Resume / migrate: if enough VIPs already left and none are active, unlock lull now.
        // Do not present the portal when returning to an already-active lull.
        TryUnlockPostVipLull(offerPortalPresentation: false);
        PortalUiController.EnsureHidden();

        _spawnCount = 0;

        if (IsVipPranksterAlternationEnabled())
        {
            _customersSincePranksterLeft = 0;
            _awaitingVipAfterPrankster = true;
        }

        bool prewarm = ShouldPrewarmBusinessOnResume(resumeAfterSteal);
        _spawnRoutine = StartCoroutine(SpawnLoopWithOptionalPrewarm(prewarm));
    }

    private void RestoreServedVipCountFromSave()
    {
        if (!RestaurantSceneMode.IsMainScene)
        {
            _servedVipCount = 0;
            return;
        }

        // Saved value is how many VIPs have fully left this cycle (happy or discontent).
        _servedVipCount = PlayerProfileStorage.GetMainSceneServedVipCountForCurrentPlayer();
    }

    private void RestoreVipSpawnStopCountFromSave()
    {
        if (!RestaurantSceneMode.IsMainScene)
        {
            _runtimeVipSpawnStopCount = 0;
            return;
        }

        _runtimeVipSpawnStopCount = PlayerProfileStorage.GetMainSceneVipSpawnStopCountOverrideForCurrentPlayer();
    }

    private int GetEffectiveVipSpawnStopCount()
    {
        if (_runtimeVipSpawnStopCount > 0)
            return _runtimeVipSpawnStopCount;

        return Mathf.Max(0, _servedVipSpawnStopCount);
    }

    private static bool ShouldPrewarmBusinessOnResume(bool resumeAfterSteal = false)
    {
        if (GameManager.Instance == null)
            return false;

        // Competitor shops always open mid-service.
        if (RestaurantSceneMode.IsCompetitorScene)
            return true;

        if (!RestaurantSceneMode.IsMainScene)
            return false;

        // After stealing from a competitor, reopen mid-service like a normal busy resume.
        if (resumeAfterSteal || CompetitorSceneSelection.HasPendingBusinessResumeAfterSteal)
            return true;

        return GameManager.Instance.DidResumeBusinessSessionOnLoad;
    }

    private void StopSpawning()
    {
        if (_spawnRoutine == null)
            return;

        StopCoroutine(_spawnRoutine);
        _spawnRoutine = null;
    }

    /// <summary>
    /// Counts a VIP toward the post-VIP lull only after they fully leave
    /// (happy payment leave or discontented leave). Mid-visit quit does not count.
    /// </summary>
    private void RegisterVipServed()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return;

        _servedVipCount++;
        PlayerProfileStorage.SetMainSceneServedVipCountForCurrentPlayer(_servedVipCount);
        PlayerProfileStorage.ClearMainSceneVipSeatedVisitPendingForCurrentPlayer();
    }

    /// <summary>True once enough VIPs have fully left — no more VIP spawns.</summary>
    private bool HasReachedVipVisitLimit()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return false;

        int stopCount = GetEffectiveVipSpawnStopCount();
        return stopCount > 0 && _servedVipCount >= stopCount;
    }

    /// <summary>
    /// Lull: enough VIPs have fully left and none remain in the restaurant.
    /// Main scene only — competitor restaurants keep normal traffic indefinitely.
    /// </summary>
    private bool IsPostVipLullActive()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return false;

        // Last VIP left, but the follow-up prankster has not finished yet.
        if (_pendingLullAfterPrankster)
            return false;

        return HasReachedVipVisitLimit() && GetActiveVipCount() <= 0;
    }

    private void TryUnlockPostVipLull(bool offerPortalPresentation = false)
    {
        if (!IsPostVipLullActive())
        {
            _appliedCurrentLullSideEffects = false;
            return;
        }

        PlayerProfileStorage.SetPostVipLullUnlockedForCurrentPlayer();

        if (_appliedCurrentLullSideEffects)
            return;

        _appliedCurrentLullSideEffects = true;

        // Lull cycle start: drop steal VIP bonus and re-enable competitor enter buttons.
        _runtimeVipSpawnStopCount = 0;
        PlayerProfileStorage.ClearMainSceneVipSpawnStopCountOverrideForCurrentPlayer();
        CompetitorSceneSelection.ClearBlockedTownShops();

        if (offerPortalPresentation)
            PortalUiController.PresentForLull();
    }

    /// <summary>
    /// Post-VIP lull town prompt: lull active and light traffic.
    /// </summary>
    public bool ShouldShowPostVipTownPopup()
    {
        if (!IsPostVipLullActive())
            return false;

        if (_customerPool == null)
            return false;

        return _customerPool.ActiveCustomers.Count <= Mathf.Max(0, _postVipServeMaxCustomers);
    }

    private IEnumerator SpawnLoopWithOptionalPrewarm(bool prewarm)
    {
        bool restoreSeatedVip = ShouldRestoreSeatedVipVisit();
        if (prewarm || restoreSeatedVip)
        {
            // Let seats/tables finish enabling before seeding mid-session customers.
            float seatWait = 0f;
            while (seatWait < 1f)
            {
                bool seatsReady = _registeredSeats.Count > 0;
                bool vipSeatReady = !restoreSeatedVip || HasAvailableVipSeat();
                if (seatsReady && vipSeatReady)
                    break;

                seatWait += Time.deltaTime;
                yield return null;
            }

            TrySeedSeatedVipVisitFromSave();

            if (prewarm)
                SeedMidSessionCustomers();
        }

        yield return SpawnLoop();
        _spawnRoutine = null;
    }

    private void SeedMidSessionCustomers()
    {
        if (_customerPool == null)
            return;

        int maxCustomers = GetCurrentMaxCustomers();
        if (maxCustomers <= 0)
            return;

        int minCount;
        int maxCount;
        if (IsPostVipLullActive())
        {
            minCount = Mathf.Max(0, _resumePrewarmLullMinCustomers);
            maxCount = Mathf.Max(minCount, _resumePrewarmLullMaxCustomers);
        }
        else
        {
            minCount = Mathf.Max(0, _resumePrewarmMinCustomers);
            maxCount = Mathf.Max(minCount, _resumePrewarmMaxCustomers);
        }

        int target = Random.Range(minCount, maxCount + 1);
        target = Mathf.Min(target, maxCustomers);

        if (target <= 0)
            return;

        int seatedBudget = Mathf.Max(1, (target + 1) / 2);
        int queued = 0;
        int seated = 0;

        for (int i = 0; i < target; i++)
        {
            bool preferSeat = seated < seatedBudget;
            if (preferSeat && TrySeedSeatedCustomer(eating: seated % 2 == 0))
            {
                seated++;
                _spawnCount++;
                continue;
            }

            if (TrySeedQueuedCustomer())
            {
                queued++;
                _spawnCount++;
                continue;
            }

            if (TrySeedSeatedCustomer(eating: Random.value < 0.5f))
            {
                seated++;
                _spawnCount++;
            }
        }
    }

    private static bool ShouldRestoreSeatedVipVisit()
    {
        return RestaurantSceneMode.IsMainScene
            && PlayerProfileStorage.HasMainSceneVipSeatedVisitPendingForCurrentPlayer();
    }

    private void PersistSeatedVipVisitIfNeeded()
    {
        if (!RestaurantSceneMode.IsMainScene || _customerPool == null)
            return;

        // Still waiting on the intro button — do not reseat them on return.
        if (_vipAwaitingIntro != null)
            return;

        IReadOnlyList<Customer> customers = _customerPool.ActiveCustomers;
        for (int i = 0; i < customers.Count; i++)
        {
            Customer customer = customers[i];
            if (customer == null || !customer.IsVip || customer.IgnoresVipCap)
                continue;

            if (customer.State == CustomerState.Leaving)
                continue;

            PlayerProfileStorage.SetMainSceneVipSeatedVisitPendingForCurrentPlayer();
            return;
        }
    }

    private bool TrySeedSeatedVipVisitFromSave()
    {
        if (!ShouldRestoreSeatedVipVisit())
            return false;

        if (HasReachedVipVisitLimit() || IsPostVipLullActive())
        {
            PlayerProfileStorage.ClearMainSceneVipSeatedVisitPendingForCurrentPlayer();
            return false;
        }

        if (_customerPool == null || !_customerPool.HasVipPrefabs || !CanStartVipPhaseContent())
            return false;

        if (GetActiveVipCount() >= Mathf.Max(1, _maxActiveVips))
            return false;

        Customer customer = _customerPool.GetVip(_spawnPoint != null ? _spawnPoint.position : Vector3.zero);
        if (customer == null)
            return false;

        TableSeat seat = FindFreeSeat(customer);
        if (seat == null || !seat.TryReserve(customer))
        {
            ReleaseCustomerToPool(customer);
            return false;
        }

        customer.WarpTo(seat.Position);
        if (customer.Locomotion != null)
            customer.Locomotion.FaceDirection(seat.Rotation);
        customer.EnterStationary();
        NotifyTableSeatChanged(seat);

        customer.SetState(CustomerState.Ordering);
        MarkFirstVipCustomerReceivedIfNeeded(customer);
        UIManager.Instance?.NotifyVipSeatedForTaste(customer);

        Coroutine flow = StartCoroutine(RunVipSeatedEvents(customer, seat, skipSettleDelay: true));
        _activeFlows[customer] = flow;
        _spawnCount++;
        GameEvents.RaiseCustomerSpawned();
        return true;
    }

    private bool TrySeedSeatedCustomer(bool eating)
    {
        if (_customerPool == null || CustomerMovement.Instance == null)
            return false;

        if (_customerPool.ActiveCustomers.Count >= GetCurrentMaxCustomers())
            return false;

        Customer customer = _customerPool.Get(_spawnPoint != null ? _spawnPoint.position : Vector3.zero);
        if (customer == null)
            return false;

        TableSeat seat = FindFreeSeat(customer);
        if (seat == null || !seat.TryReserve(customer))
        {
            ReleaseCustomerToPool(customer);
            return false;
        }

        customer.WarpTo(seat.Position);
        customer.Locomotion.FaceDirection(seat.Rotation);
        customer.EnterStationary();
        NotifyTableSeatChanged(seat);

        Coroutine flow = eating
            ? StartCoroutine(RunPrewarmedEatingFlow(customer, seat))
            : StartCoroutine(RunPrewarmedOrderingFlow(customer, seat));
        _activeFlows[customer] = flow;
        GameEvents.RaiseCustomerSpawned();
        return true;
    }

    private bool TrySeedQueuedCustomer()
    {
        if (_customerPool == null || _customerQueue == null || _customerQueue.IsFull)
            return false;

        if (_customerPool.ActiveCustomers.Count >= GetCurrentMaxCustomers())
            return false;

        Customer customer = _customerPool.Get(_spawnPoint != null ? _spawnPoint.position : Vector3.zero);
        if (customer == null)
            return false;

        if (!_customerQueue.TryAssign(customer, out int queueSlot))
        {
            ReleaseCustomerToPool(customer);
            return false;
        }

        customer.WarpTo(_customerQueue.GetSlotPosition(queueSlot));
        customer.EnterStationary();
        customer.SetState(CustomerState.Queue);

        Coroutine flow = StartCoroutine(RunCustomerFlowAfterQueued(customer));
        _activeFlows[customer] = flow;
        GameEvents.RaiseCustomerSpawned();
        return true;
    }

    private IEnumerator RunPrewarmedOrderingFlow(Customer customer, TableSeat seat)
    {
        customer.SetState(CustomerState.Ordering);

        DishOrder order = WorkerManager.Instance != null
            ? WorkerManager.Instance.SubmitOrder(customer)
            : null;

        yield return RunCustomerFlowFromOrdering(customer, seat, order);
    }

    private IEnumerator RunPrewarmedEatingFlow(Customer customer, TableSeat seat)
    {
        customer.SetState(CustomerState.Eating);

        float remaining = _eatDuration * Random.Range(0.2f, 0.85f);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        yield return FinishMealPaymentAndLeave(customer, seat);
    }

    private IEnumerator SpawnLoop()
    {
        while (GameManager.Instance != null
            && GameManager.Instance.IsBusiness
            && GameManager.Instance.IsBusinessSessionActive)
        {
            yield return new WaitForSeconds(GetCurrentSpawnInterval());

            if (!CanSpawnCustomer())
                continue;

            SpawnCustomer();
        }
    }

    private float GetCurrentSpawnInterval()
    {
        if (IsPostVipLullActive())
            return Mathf.Max(0.01f, _postVipServeSpawnInterval);

        return Mathf.Max(0.01f, _spawnInterval);
    }

    private bool CanSpawnCustomer()
    {
        if (_customerPool == null || _customerPool.ActiveCustomers.Count >= GetCurrentMaxCustomers())
            return false;

        if (_customerQueue != null && _customerQueue.IsFull)
            return false;

        return true;
    }

    private int GetCurrentMaxCustomers()
    {
        int builtTables = CountBuiltTables();

        if (builtTables <= 0)
            return 0;

        int firstTableCap = Mathf.Max(0, _maxCustomersForFirstTable);
        int perAdditional = Mathf.Max(0, _maxCustomersPerAdditionalTable);
        int maxCustomers = firstTableCap + (builtTables - 1) * perAdditional;

        // During the post-VIP lull, keep a light trickle instead of emptying the shop.
        if (IsPostVipLullActive())
            maxCustomers = Mathf.Min(maxCustomers, Mathf.Max(0, _postVipServeMaxCustomers));

        return maxCustomers;
    }

    private static int CountBuiltTables()
    {
        DiningTable[] tables = FindObjectsByType<DiningTable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int count = 0;

        for (int i = 0; i < tables.Length; i++)
        {
            DiningTable table = tables[i];

            if (table == null || !table.isActiveAndEnabled || !table.gameObject.activeInHierarchy)
                continue;

            // VIP / second-floor tables don't expand the ground-floor customer cap.
            if (table.IsVipTable)
                continue;

            count++;
        }

        return count;
    }

    private void SpawnCustomer()
    {
        if (_customerPool == null || _spawnPoint == null || CustomerMovement.Instance == null || !CanSpawnCustomer())
            return;

        _spawnCount++;
        if (IsVipPranksterAlternationEnabled() && _awaitingVipAfterPrankster)
        {
            if (_pranksterManager == null)
                _pranksterManager = FindFirstObjectByType<PranksterManager>();

            if (_pranksterManager == null || !_pranksterManager.IsVisitActive)
                _customersSincePranksterLeft++;
        }

        // Before VIP prep (stairs / VIP table / stage / 2F staff) is ready, keep spawning normals.
        // VIP only appears once MeetsVipSpawnRequirements() is true on a VIP cadence tick.
        bool spawnVip = ShouldSpawnVip();
        Customer customer = spawnVip
            ? _customerPool.GetVip(_spawnPoint.position)
            : _customerPool.Get(_spawnPoint.position);

        if (customer == null)
        {
            // VIP slot was claimed but spawn failed — keep waiting for a VIP, not a prankster.
            if (spawnVip && IsVipPranksterAlternationEnabled())
            {
                _awaitingVipAfterPrankster = true;
                _customersSincePranksterLeft = 0;
            }

            return;
        }

        if (spawnVip)
        {
            MarkFirstVipCustomerReceivedIfNeeded(customer);

            if (!RestaurantSceneMode.IsCompetitorScene)
                AudioManager.Play(SfxId.VipArrival);
        }

        Coroutine flow = StartCoroutine(RunCustomerFlow(customer));
        _activeFlows[customer] = flow;
        GameEvents.RaiseCustomerSpawned();
    }

    private static void MarkFirstVipCustomerReceivedIfNeeded(Customer customer)
    {
        if (!RestaurantSceneMode.IsMainScene || customer == null || !customer.IsVip)
            return;

        PlayerProfileStorage.SetFirstVipCustomerReceivedForCurrentPlayer();
    }

    private bool ShouldSpawnVip()
    {
        if (_customerPool == null || !_customerPool.HasVipPrefabs)
            return false;

        // No more VIPs after enough have fully left — only the reduced normal-customer trickle.
        if (HasReachedVipVisitLimit())
            return false;

        if (!MeetsVipSpawnRequirements())
            return false;

        if (GetActiveVipCount() >= Mathf.Max(1, _maxActiveVips))
            return false;

        if (RestaurantSceneMode.IsMainScene)
        {
            if (_pranksterManager == null)
                _pranksterManager = FindFirstObjectByType<PranksterManager>();

            // Don't overlap an active / imminent prankster visit.
            if (_pranksterManager != null
                && (_pranksterManager.IsVisitActive || _pranksterManager.IsAwaitingPranksterSpawn))
            {
                return false;
            }

            if (_competitorVisitorManager == null)
                _competitorVisitorManager = FindFirstObjectByType<CompetitorVisitorManager>();

            // The rival owner takes the turn after the last prankster — no VIP during it.
            if (_competitorVisitorManager != null
                && (_competitorVisitorManager.IsVisitActive || _competitorVisitorManager.IsAwaitingSpawn))
            {
                return false;
            }
        }

        return IsVipSpawnCadenceDue(_spawnCount);
    }

    private bool IsVipSpawnCadenceDue(int spawnCount)
    {
        if (_vipSpawnInterval <= 0)
            return false;

        return spawnCount % _vipSpawnInterval == 0;
    }

    /// <summary>
    /// True once VIP late-game content is unlocked (mission 5 + 2F staff + stage + VIP table built).
    /// Does not require a free VIP seat — used to gate pranksters during early business.
    /// </summary>
    public bool CanStartVipPhaseContent()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return true;

        if (!HasCompletedVipPrepMission())
            return false;

        if (!AreSecondFloorVipStaffStationed())
            return false;

        if (!HasVipStageBuilt())
            return false;

        return HasVipTableBuilt();
    }

    private bool MeetsVipSpawnRequirements()
    {
        // Competitor shops: VIPs may queue at the VIP waypoint even when the seat is taken
        // (up to _maxActiveVips). Seat availability is handled in the VIP flow.
        if (RestaurantSceneMode.IsCompetitorScene)
            return true;

        if (!CanStartVipPhaseContent())
            return false;

        return HasAvailableVipSeat();
    }

    private static bool HasVipTableBuilt()
    {
        DiningTable[] tables = FindObjectsByType<DiningTable>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < tables.Length; i++)
        {
            DiningTable table = tables[i];
            if (table != null && table.IsVipTable && table.gameObject.activeInHierarchy && !table.IsBroken)
                return true;
        }

        // Fall back to build spots in case the VIP table object is still activating.
        BuildSpot[] spots = FindObjectsByType<BuildSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < spots.Length; i++)
        {
            BuildSpot spot = spots[i];
            if (spot != null && spot.IsBuilt && spot.PlaceableType == PlaceableType.VipTable)
                return true;
        }

        return false;
    }

    private static bool HasCompletedVipPrepMission()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return true;

        int missionPart = PlayerProfileStorage.GetMainSceneMissionPartIndexForCurrentPlayer();
        MissionUiController missionUi = FindFirstObjectByType<MissionUiController>();

        if (missionUi != null)
        {
            missionUi.EnsureInitialized();
            missionPart = Mathf.Max(missionPart, missionUi.CurrentPartIndex);
        }

        return missionPart > MissionCatalog.VipPrepMissionPartIndex;
    }

    private static bool HasVipStageBuilt()
    {
        BuildSpot[] spots = FindObjectsByType<BuildSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < spots.Length; i++)
        {
            BuildSpot spot = spots[i];
            if (spot != null && spot.IsBuilt && spot.PlaceableType == PlaceableType.VipStage)
                return true;
        }

        return false;
    }

    /// <summary>
    /// VIP only after the second-floor hire walk-in finishes (FemaleWaiter + call ladies + performer
    /// have reached their waypoints). HireSpot flips to Hired only then.
    /// </summary>
    private bool AreSecondFloorVipStaffStationed()
    {
        HireSpot secondFloorHire = FindSecondFloorWaiterHireSpot();
        if (secondFloorHire != null)
            return secondFloorHire.IsHired;

        // Scenes without a second-floor hire spot: fall back to VIP waiter roster.
        return WorkerManager.Instance != null
            && WorkerManager.Instance.HasVipFloorWaiterHired();
    }

    private static HireSpot FindSecondFloorWaiterHireSpot()
    {
        HireSpot[] spots = FindObjectsByType<HireSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < spots.Length; i++)
        {
            HireSpot spot = spots[i];
            if (spot != null
                && spot.Floor == RestaurantFloor.Second
                && spot.WorkerType == WorkerType.Waiter)
            {
                return spot;
            }
        }

        return null;
    }

    private bool HasAvailableVipSeat()
    {
        for (int i = 0; i < _registeredSeats.Count; i++)
        {
            TableSeat seat = _registeredSeats[i];

            if (seat == null || !seat.isActiveAndEnabled)
                continue;

            DiningTable table = FindTableForSeat(seat);

            if (table == null || !table.IsVipTable || table.IsBroken)
                continue;

            return true;
        }

        return false;
    }

    private static bool IsVipPranksterAlternationEnabled()
    {
        return RestaurantSceneMode.IsMainScene;
    }

    private void ResetAlternationSpawnTracking()
    {
        _awaitingVipAfterPrankster = false;
        _customersSincePranksterLeft = 0;
    }

    private int GetActiveVipCount()
    {
        if (_customerPool == null)
            return 0;

        int vipCount = 0;
        IReadOnlyList<Customer> customers = _customerPool.ActiveCustomers;

        for (int i = 0; i < customers.Count; i++)
        {
            Customer customer = customers[i];

            if (customer != null && customer.IsVip && !customer.IgnoresVipCap)
                vipCount++;
        }

        return vipCount;
    }

    /// <summary>First active VIP in the restaurant, if any.</summary>
    public bool TryGetActiveVip(out Customer vip)
    {
        vip = null;

        if (_customerPool == null)
            return false;

        IReadOnlyList<Customer> customers = _customerPool.ActiveCustomers;
        for (int i = 0; i < customers.Count; i++)
        {
            Customer customer = customers[i];
            if (customer == null || !customer.IsVip || !customer.gameObject.activeInHierarchy)
                continue;

            vip = customer;
            return true;
        }

        return false;
    }

    private IEnumerator RunCustomerFlow(Customer customer)
    {
        if (customer != null && customer.IsVip)
        {
            yield return RunVipCustomerFlow(customer);
            yield break;
        }

        if (_customerQueue == null || !_customerQueue.TryAssign(customer, out int queueSlot))
        {
            yield return Leave(customer);
            yield break;
        }

        customer.SetState(CustomerState.Queue);
        yield return CustomerMovement.Instance.MoveTo(customer, _customerQueue.GetSlotPosition(queueSlot));
        customer.EnterStationary();
        yield return RunCustomerFlowAfterQueued(customer);
    }

    private IEnumerator RunVipCustomerFlow(Customer customer)
    {
        customer.SetState(CustomerState.Queue);

        if (_vipEntryWaypoint != null && CustomerMovement.Instance != null)
            yield return CustomerMovement.Instance.MoveTo(customer, _vipEntryWaypoint.position);

        customer.EnterStationary();

        // Competitor: hold at VIP waypoint, then seat (no intro/events).
        if (RestaurantSceneMode.IsCompetitorScene)
        {
            yield return RunCompetitorVipSeatAndServe(customer);
            yield break;
        }

        UIManager.Instance?.PlayVipAnnouncement();

        _vipAwaitingIntro = customer;
        _vipIntroAcknowledged = false;
        UIManager.Instance?.ShowVipIntroButton(customer);
        UIManager.Instance?.ShowVipWaitTimer(customer, _queueWaitTimeout);

        float introWait = 0f;
        while (!_vipIntroAcknowledged && introWait < _queueWaitTimeout)
        {
            introWait += Time.deltaTime;
            UIManager.Instance?.UpdateVipWaitTimer(customer, Mathf.Max(0f, _queueWaitTimeout - introWait), _queueWaitTimeout);
            yield return null;
        }

        bool introAccepted = _vipIntroAcknowledged;
        UIManager.Instance?.HideVipIntroButton();
        ClearVipIntroState(customer);

        if (!introAccepted)
        {
            UIManager.Instance?.HideVipWaitTimer(customer);
            UIManager.Instance?.SetVipDialogue(VipDialogueState.UnhappyLeave);
            yield return Leave(customer);
            yield break;
        }

        yield return SeatVipCustomer(customer);
    }

    private IEnumerator RunCompetitorVipSeatAndServe(Customer customer)
    {
        float seatWait = 0f;
        TableSeat reservedSeat = null;
        float holdSeconds = Mathf.Max(0f, _vipWaypointHoldSeconds);

        while (reservedSeat == null && seatWait < _queueWaitTimeout)
        {
            TableSeat seat = FindFreeSeat(customer);
            if (seat == null || !seat.TryReserve(customer))
            {
                seatWait += Time.deltaTime;
                yield return null;
                continue;
            }

            NotifyTableSeatChanged(seat);

            float holdElapsed = 0f;
            while (holdElapsed < holdSeconds)
            {
                holdElapsed += Time.deltaTime;
                yield return null;
            }

            reservedSeat = seat;
        }

        if (reservedSeat == null)
        {
            yield return Leave(customer);
            yield break;
        }

        TableSeat assignedSeat = null;
        yield return TryWalkCustomerToSeat(customer, reservedSeat, result => assignedSeat = result);

        if (assignedSeat == null)
        {
            yield return Leave(customer);
            yield break;
        }

        customer.SetState(CustomerState.Ordering);

        DishOrder order = WorkerManager.Instance != null
            ? WorkerManager.Instance.SubmitOrder(customer)
            : null;
        yield return RunCustomerFlowFromOrdering(customer, assignedSeat, order);
    }

    private IEnumerator SeatVipCustomer(Customer customer)
    {
        TableSeat seat = null;
        float seatWait = 0f;

        if (!RestaurantSceneMode.IsCompetitorScene)
            UIManager.Instance?.ShowVipWaitTimer(customer, _queueWaitTimeout);

        while (seat == null && seatWait < _queueWaitTimeout)
        {
            seat = FindFreeSeat(customer);

            if (seat == null)
            {
                seatWait += Time.deltaTime;
                if (!RestaurantSceneMode.IsCompetitorScene)
                {
                    UIManager.Instance?.UpdateVipWaitTimer(
                        customer,
                        Mathf.Max(0f, _queueWaitTimeout - seatWait),
                        _queueWaitTimeout);
                }

                yield return null;
            }
        }

        if (seat == null)
        {
            if (!RestaurantSceneMode.IsCompetitorScene)
            {
                UIManager.Instance?.HideVipWaitTimer(customer);
                UIManager.Instance?.SetVipDialogue(VipDialogueState.UnhappyLeave);
            }

            yield return Leave(customer);
            yield break;
        }

        if (!RestaurantSceneMode.IsCompetitorScene)
            UIManager.Instance?.HideVipWaitTimer(customer);

        TableSeat assignedSeat = null;
        yield return TryWalkCustomerToSeat(customer, seat, result => assignedSeat = result);

        if (assignedSeat == null)
        {
            yield return Leave(customer);
            yield break;
        }

        customer.SetState(CustomerState.Ordering);

        // Competitor VIP: no request mini-game — just wait for food, eat, leave.
        if (RestaurantSceneMode.IsCompetitorScene)
        {
            DishOrder order = WorkerManager.Instance != null
                ? WorkerManager.Instance.SubmitOrder(customer)
                : null;
            yield return RunCustomerFlowFromOrdering(customer, assignedSeat, order);
            yield break;
        }

        UIManager.Instance?.NotifyVipSeatedForTaste(customer);
        yield return RunVipSeatedEvents(customer, assignedSeat);
    }

    private IEnumerator RunVipSeatedEvents(Customer customer, TableSeat seat, bool skipSettleDelay = false)
    {
        customer.VipEventBonus = 0;

        // Chef starts prepping VIP food immediately; waiter only collects after 上菜.
        DishOrder vipOrder = WorkerManager.Instance != null
            ? WorkerManager.Instance.SubmitVipHeldOrder(customer)
            : null;

        if (!skipSettleDelay && _vipSettleDelay > 0f)
            yield return new WaitForSeconds(_vipSettleDelay);

        VipEventType[] events = BuildVipEventSequence();

        for (int i = 0; i < events.Length; i++)
        {
            // One event at a time: short pause before each request after the first.
            if (i > 0 && _vipEventGapDelay > 0f)
                yield return new WaitForSeconds(_vipEventGapDelay);

            bool fulfilled = false;

            if (events[i] == VipEventType.ServeDish)
                yield return RunVipServeDishEvent(customer, vipOrder, result => fulfilled = result);
            else
                yield return RunSingleVipEvent(customer, events[i], result => fulfilled = result);

            if (events[i] == VipEventType.ServeDish && !fulfilled)
            {
                UIManager.Instance?.HideAllVipEventButtons(restoreCravingDialogue: false);
                UIManager.Instance?.HideVipWaitTimer(customer);
                UIManager.Instance?.SetVipDialogue(VipDialogueState.UnhappyLeave);
                WorkerManager.Instance?.CancelOrder(vipOrder);
                ReleaseCustomerSeatAndNotify(customer, seat);
                yield return Leave(customer);
                yield break;
            }

            if (fulfilled)
                customer.VipEventBonus += Mathf.Max(0, _vipEventBonusPerRequest);
            else if (events[i] != VipEventType.ServeDish)
                UIManager.Instance?.SetVipDialogue(VipDialogueState.Discontent);
        }

        UIManager.Instance?.HideAllVipEventButtons();
        UIManager.Instance?.HideVipWaitTimer(customer);

        customer.SetState(CustomerState.Eating);
        yield return new WaitForSeconds(_eatDuration);

        if (RestaurantSceneMode.IsCompetitorScene)
        {
            ReleaseCustomerSeatAndNotify(customer, seat);
            yield return Leave(customer);
            yield break;
        }

        _completedPayments.Remove(customer);
        BindPendingPayment(customer, ResolveActiveSeat(customer, seat));
        customer.SetState(CustomerState.Paying);

        while (!_completedPayments.Contains(customer))
            yield return null;

        _completedPayments.Remove(customer);
        ReleaseCustomerSeatAndNotify(customer, seat);
        yield return Leave(customer);
    }

    private static VipEventType[] BuildVipEventSequence()
    {
        // Competitor shops run VIP service without player taps — just food service.
        if (RestaurantSceneMode.IsCompetitorScene)
            return new[] { VipEventType.ServeDish };

        // Always ServeDish + exactly one of CallLady / WatchStage, shuffled order.
        VipEventType optionalEvent = Random.value < 0.5f
            ? VipEventType.CallLady
            : VipEventType.WatchStage;

        VipEventType[] sequence = { optionalEvent, VipEventType.ServeDish };

        if (Random.value < 0.5f)
        {
            VipEventType temp = sequence[0];
            sequence[0] = sequence[1];
            sequence[1] = temp;
        }

        return sequence;
    }

    private IEnumerator RunVipServeDishEvent(Customer customer, DishOrder order, System.Action<bool> onComplete)
    {
        BeginPendingVipEvent(customer, VipEventType.ServeDish);

        bool competitorAutoServe = RestaurantSceneMode.IsCompetitorScene;

        if (!competitorAutoServe)
        {
            UIManager.Instance?.ShowVipEventButton(VipEventType.ServeDish, customer);
            UIManager.Instance?.ShowVipWaitTimer(customer, _foodWaitTimeout);
        }
        else
        {
            // No VIP serve UI in competitor — release the held dish immediately.
            _vipEventAcknowledged = true;
        }

        float wait = 0f;
        while (!_vipEventAcknowledged && wait < _foodWaitTimeout)
        {
            wait += Time.deltaTime;
            UIManager.Instance?.UpdateVipWaitTimer(customer, Mathf.Max(0f, _foodWaitTimeout - wait), _foodWaitTimeout);
            yield return null;
        }

        bool acknowledged = _vipEventAcknowledged;
        ClearPendingVipEvent();
        if (!competitorAutoServe)
            UIManager.Instance?.HideVipEventButton(VipEventType.ServeDish);

        if (!acknowledged)
        {
            UIManager.Instance?.HideVipWaitTimer(customer);
            onComplete?.Invoke(false);
            yield break;
        }

        // Player requested 上菜 — release the held ready dish so the VIP waiter collects it.
        if (order != null && WorkerManager.Instance != null)
            WorkerManager.Instance.ReleaseOrderForServe(order);

        float deliveryWait = 0f;
        if (!competitorAutoServe)
            UIManager.Instance?.ShowVipWaitTimer(customer, _foodWaitTimeout);

        while (order != null
            && !order.IsDelivered
            && !order.IsCancelled
            && deliveryWait < _foodWaitTimeout)
        {
            deliveryWait += Time.deltaTime;
            if (!competitorAutoServe)
            {
                UIManager.Instance?.UpdateVipWaitTimer(
                    customer,
                    Mathf.Max(0f, _foodWaitTimeout - deliveryWait),
                    _foodWaitTimeout);
            }

            yield return null;
        }

        if (!competitorAutoServe)
            UIManager.Instance?.HideVipWaitTimer(customer);

        bool delivered = order != null && order.IsDelivered && !order.IsCancelled;

        // Competitor may not have a VIP-floor waiter path — don't fail the whole visit.
        if (competitorAutoServe && !delivered)
            delivered = true;

        onComplete?.Invoke(delivered);
    }

    private IEnumerator RunSingleVipEvent(Customer customer, VipEventType eventType, System.Action<bool> onComplete)
    {
        BeginPendingVipEvent(customer, eventType);

        if (RestaurantSceneMode.IsCompetitorScene)
            _vipEventAcknowledged = true;
        else
        {
            UIManager.Instance?.ShowVipEventButton(eventType, customer);
            UIManager.Instance?.ShowVipWaitTimer(customer, _foodWaitTimeout);
        }

        float wait = 0f;
        while (!_vipEventAcknowledged && wait < _foodWaitTimeout)
        {
            wait += Time.deltaTime;
            UIManager.Instance?.UpdateVipWaitTimer(customer, Mathf.Max(0f, _foodWaitTimeout - wait), _foodWaitTimeout);
            yield return null;
        }

        bool acknowledged = _vipEventAcknowledged;
        ClearPendingVipEvent();
        UIManager.Instance?.HideVipEventButton(eventType);
        UIManager.Instance?.HideVipWaitTimer(customer);

        if (!acknowledged)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        if (eventType == VipEventType.CallLady)
            yield return SpawnVipCallLadies(customer);
        else if (eventType == VipEventType.WatchStage)
            yield return RunVipWatchStage(customer);

        onComplete?.Invoke(true);
    }

    private IEnumerator RunVipWatchStage(Customer vip)
    {
        CacheVipPerformerReferences();

        Worker performer = _vipPerformer;
        if (performer == null || !performer.gameObject.activeInHierarchy)
            yield break;

        if (_performerReturnRoutine != null)
        {
            StopCoroutine(_performerReturnRoutine);
            _performerReturnRoutine = null;
        }

        Transform stage = _vipPerformStagePoint;
        performer.Locomotion?.ExitStationary();
        performer.SetState(WorkerState.Wait);

        if (stage != null)
            yield return MoveWorkerTo(performer, stage.position);

        // Face the seated VIP (seat facing is more stable than the root while walking).
        Transform faceTarget = vip != null && vip.Seat != null
            ? vip.Seat.transform
            : vip != null ? vip.transform : null;
        FaceWorkerToward(performer, faceTarget);
        performer.Locomotion?.EnterStationary();
        _performerOnStage = true;
        PlayVipStageMusic();
    }

    private IEnumerator SpawnVipCallLadies(Customer vip)
    {
        CacheVipCallLadyReferences();

        if (_vipCallLadyWorkers == null || _vipCallLadyWorkers.Length == 0)
            yield break;

        if (_callLadyDismissRoutine != null)
        {
            StopCoroutine(_callLadyDismissRoutine);
            _callLadyDismissRoutine = null;
        }

        // Already at the VIP — stay put for this request.
        if (_callLadiesActive)
        {
            RestartCallLadyPostAnims();
            yield break;
        }

        int walksPending = 0;

        for (int i = 0; i < _vipCallLadyWorkers.Length; i++)
        {
            Worker worker = _vipCallLadyWorkers[i];
            if (worker == null)
                continue;

            // They enter with the second-floor hire; if somehow inactive, skip.
            if (!worker.gameObject.activeInHierarchy)
                continue;

            Transform lackeyPoint = _vipLackeyPoints != null && i < _vipLackeyPoints.Length
                ? _vipLackeyPoints[i]
                : null;

            worker.Locomotion?.ExitStationary();
            worker.SetState(WorkerState.Wait);

            walksPending++;
            StartCoroutine(WalkCallLadyToPost(worker, lackeyPoint, vip, () => walksPending--));
        }

        while (walksPending > 0)
            yield return null;

        _callLadiesActive = walksPending == 0 && HasAnyActiveCallLady();
    }

    private bool HasAnyActiveCallLady()
    {
        if (_vipCallLadyWorkers == null)
            return false;

        for (int i = 0; i < _vipCallLadyWorkers.Length; i++)
        {
            Worker worker = _vipCallLadyWorkers[i];
            if (worker != null && worker.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    private IEnumerator WalkCallLadyToPost(
        Worker worker,
        Transform lackeyPoint,
        Customer vip,
        System.Action onArrived)
    {
        Vector3 destination = lackeyPoint != null
            ? lackeyPoint.position
            : (worker != null ? worker.transform.position : Vector3.zero);

        if (CustomerMovement.Instance != null)
            yield return MoveWorkerTo(worker, destination);

        if (worker != null)
        {
            worker.Locomotion?.EnterStationary();
            FaceWorkerToward(worker, vip != null ? vip.transform : null);
            StartCallLadyPostAnim(worker);
        }

        onArrived?.Invoke();
    }

    private void RestartCallLadyPostAnims()
    {
        if (_vipCallLadyWorkers == null)
            return;

        for (int i = 0; i < _vipCallLadyWorkers.Length; i++)
        {
            Worker worker = _vipCallLadyWorkers[i];
            if (worker == null || !worker.gameObject.activeInHierarchy)
                continue;

            StartCallLadyPostAnim(worker);
        }
    }

    private void StartCallLadyPostAnim(Worker worker)
    {
        StopCallLadyPostAnim(worker, returnToIdle: false);
        if (worker == null || !worker.gameObject.activeInHierarchy)
            return;

        _callLadyPostAnimRoutines[worker] = StartCoroutine(CallLadyPostAnimRoutine(worker));
    }

    private void StopCallLadyPostAnim(Worker worker, bool returnToIdle)
    {
        if (worker != null && _callLadyPostAnimRoutines.TryGetValue(worker, out Coroutine routine))
        {
            if (routine != null)
                StopCoroutine(routine);

            _callLadyPostAnimRoutines.Remove(worker);
        }

        if (returnToIdle)
            SetCallLadyCleaning(worker, false);
    }

    private void StopAllCallLadyPostAnims(bool returnToIdle)
    {
        if (_callLadyPostAnimRoutines.Count == 0)
        {
            if (!returnToIdle)
                return;

            if (_vipCallLadyWorkers == null)
                return;

            for (int i = 0; i < _vipCallLadyWorkers.Length; i++)
                SetCallLadyCleaning(_vipCallLadyWorkers[i], false);

            return;
        }

        List<Worker> workers = new List<Worker>(_callLadyPostAnimRoutines.Keys);
        for (int i = 0; i < workers.Count; i++)
            StopCallLadyPostAnim(workers[i], returnToIdle);
    }

    private IEnumerator CallLadyPostAnimRoutine(Worker worker)
    {
        float minDuration = Mathf.Max(0.01f, _callLadyPostAnimMinDuration);
        float maxDuration = Mathf.Max(minDuration, _callLadyPostAnimMaxDuration);

        while (worker != null && worker.gameObject.activeInHierarchy)
        {
            SetCallLadyCleaning(worker, Random.value >= 0.5f);
            yield return new WaitForSeconds(Random.Range(minDuration, maxDuration));
        }

        if (worker != null)
            _callLadyPostAnimRoutines.Remove(worker);
    }

    private static void SetCallLadyCleaning(Worker worker, bool isCleaning)
    {
        if (worker == null)
            return;

        WorkerCharacterAnimator animator = worker.GetComponent<WorkerCharacterAnimator>();
        animator?.SetCleaning(isCleaning);
    }

    private static void FaceWorkerToward(Worker worker, Transform target)
    {
        if (worker == null || target == null)
            return;

        Vector3 direction = target.position - worker.transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        worker.FaceDirection(Quaternion.LookRotation(direction.normalized));
    }

    private static IEnumerator MoveWorkerTo(Worker worker, Vector3 destination)
    {
        if (worker == null || worker.Locomotion == null)
            yield break;

        yield return NavMeshMovement.MoveTo(worker.Locomotion, destination);
    }

    private void BeginDismissVipCallLadies()
    {
        if (!_callLadiesActive)
            return;

        if (_callLadyDismissRoutine != null)
            StopCoroutine(_callLadyDismissRoutine);

        _callLadyDismissRoutine = StartCoroutine(DismissVipCallLadiesRoutine());
    }

    private IEnumerator DismissVipCallLadiesRoutine()
    {
        StopAllCallLadyPostAnims(returnToIdle: true);

        float delay = Mathf.Max(0f, _vipCallLadyLeaveDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        CacheVipCallLadyReferences();

        if (_vipCallLadyWorkers != null)
        {
            for (int i = 0; i < _vipCallLadyWorkers.Length; i++)
            {
                Worker worker = _vipCallLadyWorkers[i];
                if (worker == null || !worker.gameObject.activeInHierarchy)
                    continue;

                Transform waypoint = ResolveCallLadyWaypoint(worker, i);

                worker.Locomotion?.ExitStationary();

                if (waypoint != null)
                {
                    yield return MoveWorkerTo(worker, waypoint.position);
                    worker.FaceDirection(waypoint.rotation);
                }

                worker.Locomotion?.EnterStationary();
                worker.SetState(WorkerState.Wait);
            }
        }

        _callLadiesActive = false;
        _callLadyDismissRoutine = null;
    }

    private void BeginReturnVipPerformer()
    {
        StopVipStageMusic();

        if (!_performerOnStage)
            return;

        if (_performerReturnRoutine != null)
            StopCoroutine(_performerReturnRoutine);

        _performerReturnRoutine = StartCoroutine(ReturnVipPerformerRoutine());
    }

    private IEnumerator ReturnVipPerformerRoutine()
    {
        float delay = Mathf.Max(0f, _vipCallLadyLeaveDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        CacheVipPerformerReferences();

        Worker performer = _vipPerformer;
        if (performer != null && performer.gameObject.activeInHierarchy)
        {
            Transform waypoint = performer.WaitPoint != null
                ? performer.WaitPoint
                : _vipPerformerWaypoint;

            performer.Locomotion?.ExitStationary();

            if (waypoint != null)
            {
                yield return MoveWorkerTo(performer, waypoint.position);
                performer.FaceDirection(waypoint.rotation);
            }

            performer.Locomotion?.EnterStationary();
            performer.SetState(WorkerState.Wait);
        }

        _performerOnStage = false;
        _performerReturnRoutine = null;
    }

    private Transform ResolveCallLadyWaypoint(Worker worker, int index)
    {
        if (worker != null && worker.WaitPoint != null)
            return worker.WaitPoint;

        if (_vipCallLadyWaypoints != null && index >= 0 && index < _vipCallLadyWaypoints.Length)
            return _vipCallLadyWaypoints[index];

        return null;
    }

    private void ClearVipIntroState(Customer customer)
    {
        if (_vipAwaitingIntro == customer)
        {
            _vipAwaitingIntro = null;
            _vipIntroAcknowledged = false;
            UIManager.Instance?.HideVipIntroButton();
        }
    }

    private IEnumerator RunCustomerFlowAfterQueued(Customer customer)
    {
        float waitTimer = 0f;
        TableSeat reservedSeat = null;

        while (reservedSeat == null && waitTimer < _queueWaitTimeout)
        {
            TableSeat seat = FindFreeSeat(customer);
            if (seat == null || !seat.TryReserve(customer))
            {
                waitTimer += Time.deltaTime;
                yield return null;
                continue;
            }

            NotifyTableSeatChanged(seat);

            // Competitor: hold in queue, then stagger walks by slot (Waypoint_1 first, then +1s each).
            if (RestaurantSceneMode.IsCompetitorScene)
            {
                int queueSlot = Mathf.Max(0, customer.QueueSlotIndex);
                float holdSeconds = Mathf.Max(0f, _competitorQueueHoldSeconds)
                    + queueSlot * Mathf.Max(0f, _competitorSeatStaggerSeconds);
                float holdElapsed = 0f;
                while (holdElapsed < holdSeconds)
                {
                    holdElapsed += Time.deltaTime;
                    yield return null;
                }
            }

            reservedSeat = seat;
        }

        if (reservedSeat == null)
        {
            _customerQueue.Release(customer.QueueSlotIndex);
            customer.QueueSlotIndex = -1;
            yield return Leave(customer);
            yield break;
        }

        _customerQueue.Release(customer.QueueSlotIndex);
        customer.QueueSlotIndex = -1;

        TableSeat assignedSeat = null;
        yield return TryWalkCustomerToSeat(customer, reservedSeat, result => assignedSeat = result);

        if (assignedSeat == null)
        {
            yield return Leave(customer);
            yield break;
        }

        customer.SetState(CustomerState.Ordering);

        DishOrder order = WorkerManager.Instance != null
            ? WorkerManager.Instance.SubmitOrder(customer)
            : null;

        yield return RunCustomerFlowFromOrdering(customer, assignedSeat, order);
    }

    private IEnumerator RunCustomerFlowFromOrdering(Customer customer, TableSeat seat, DishOrder order)
    {
        float foodWaitTimer = 0f;

        while (order != null && !order.IsDelivered && foodWaitTimer < _foodWaitTimeout)
        {
            foodWaitTimer += Time.deltaTime;
            yield return null;
        }

        if (order == null || !order.IsDelivered)
        {
            WorkerManager.Instance?.CancelOrdersForCustomer(customer);

            ReleaseCustomerSeatAndNotify(customer, seat);
            yield return Leave(customer);
            yield break;
        }

        customer.SetState(CustomerState.Eating);

        yield return new WaitForSeconds(_eatDuration);

        yield return FinishMealPaymentAndLeave(customer, seat);
    }

    private IEnumerator FinishMealPaymentAndLeave(Customer customer, TableSeat seat)
    {
        if (RestaurantSceneMode.IsCompetitorScene)
        {
            ReleaseCustomerSeatAndNotify(customer, seat);
            yield return Leave(customer);
            yield break;
        }

        _completedPayments.Remove(customer);
        BindPendingPayment(customer, ResolveActiveSeat(customer, seat));
        customer.SetState(CustomerState.Paying);

        if (customer.IsVip)
        {
            while (!_completedPayments.Contains(customer))
                yield return null;
        }
        else if (RestaurantSceneMode.IsMainScene)
            yield return WaitForNormalCustomerPayment(customer);
        else
            CompletePayment(customer);

        _completedPayments.Remove(customer);
        ReleaseCustomerSeatAndNotify(customer, seat);
        yield return Leave(customer);
    }

    private IEnumerator WaitForNormalCustomerPayment(Customer customer)
    {
        float timeout = UIManager.Instance != null
            ? UIManager.Instance.NormalCoinCollectionHoldSeconds
            : 3f;
        float elapsed = 0f;

        while (customer != null && !_completedPayments.Contains(customer) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (customer != null && !_completedPayments.Contains(customer))
            CompletePayment(customer);
    }

    private IEnumerator Leave(Customer customer)
    {
        bool wasVip = customer != null && customer.IsVip;

        ClearVipIntroState(customer);
        ClearVipEventUiIfOwnedBy(customer);

        if (wasVip)
        {
            BeginDismissVipCallLadies();
            BeginReturnVipPerformer();
        }

        WorkerManager.Instance?.CancelOrdersForCustomer(customer);

        customer.ClearPendingPayment();
        customer.SetState(CustomerState.Leaving);

        if (_exitPoint != null && CustomerMovement.Instance != null)
            yield return CustomerMovement.Instance.MoveTo(customer, _exitPoint.position);

        _activeFlows.Remove(customer);
        _completedPayments.Remove(customer);

        if (_customerQueue != null && customer.QueueSlotIndex >= 0)
            _customerQueue.Release(customer.QueueSlotIndex);

        ReleaseCustomerSeatAndNotify(customer);
        ReleaseCustomerToPool(customer);

        if (wasVip)
        {
            RegisterVipServed();

            if (RestaurantSceneMode.IsMainScene)
                MissionUiController.NotifyServeVipFinished();

            if (GetActiveVipCount() <= 0)
            {
                UIManager.Instance?.HideVipUi();

                // Last VIP of this cycle: let the follow-up prankster finish, then open lull.
                if (HasReachedVipVisitLimit()
                    && IsVipPranksterAlternationEnabled()
                    && CanStartVipPhaseContent())
                {
                    _pendingLullAfterPrankster = true;
                }
                else
                {
                    TryUnlockPostVipLull(offerPortalPresentation: true);
                }
            }
        }
    }

    private IEnumerator TryWalkCustomerToSeat(Customer customer, TableSeat preferredSeat, System.Action<TableSeat> onComplete)
    {
        const int maxAttempts = 4;
        TableSeat assignedSeat = null;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            TableSeat seat = attempt == 0 ? preferredSeat : FindFreeSeat(customer);

            if (seat == null)
                break;

            // Prefer/hold seat may already be taken by someone else — never walk onto an occupied seat.
            if (!seat.TryReserve(customer))
                continue;

            customer.Locomotion.ExitStationary();
            customer.SetState(CustomerState.WalkingToSeat);
            yield return CustomerMovement.Instance.MoveTo(customer, seat.WalkDestination);

            if (!HasReachedSeatApproach(customer, seat))
            {
                seat.Release();
                continue;
            }

            customer.WarpTo(seat.Position);
            customer.Locomotion.FaceDirection(seat.Rotation);
            assignedSeat = seat;
            break;
        }

        onComplete?.Invoke(assignedSeat);
    }

    private static bool HasReachedSeatApproach(Customer customer, TableSeat seat, float maxDistance = 0.75f)
    {
        if (customer == null || seat == null)
            return false;

        return Vector3.Distance(customer.transform.position, seat.WalkDestination) <= maxDistance;
    }

    public void ExpelNonVipCustomersFromTable(DiningTable table)
    {
        if (table == null)
            return;

        List<Customer> customersToExpel = CollectNonVipCustomersAtTable(table);

        for (int i = 0; i < customersToExpel.Count; i++)
            ExpelCustomerImmediately(customersToExpel[i]);

        UIManager.Instance?.TryHideSeatPaymentUi(table.PaymentAnchor);
        GameEvents.RaiseTableStatusChanged(table);
    }

    /// <summary>Rival raid: every non-VIP customer walks out at once, without paying.</summary>
    public void ExpelAllNonVipCustomers()
    {
        if (_customerPool == null)
            return;

        List<Customer> customersToExpel = new();
        IReadOnlyList<Customer> activeCustomers = _customerPool.ActiveCustomers;

        for (int i = 0; i < activeCustomers.Count; i++)
        {
            Customer customer = activeCustomers[i];

            if (customer == null || customer.IsVip || customer.State == CustomerState.Leaving)
                continue;

            customersToExpel.Add(customer);
        }

        for (int i = 0; i < customersToExpel.Count; i++)
            ExpelCustomerImmediately(customersToExpel[i]);
    }

    private List<Customer> CollectNonVipCustomersAtTable(DiningTable table)
    {
        List<Customer> customersToExpel = new();
        HashSet<Customer> seen = new();

        for (int i = 0; i < _registeredSeats.Count; i++)
        {
            TableSeat seat = _registeredSeats[i];

            if (seat == null || !table.ContainsSeat(seat) || !seat.IsOccupied)
                continue;

            Customer customer = seat.Occupant;

            if (customer == null || customer.IsVip || !seen.Add(customer))
                continue;

            customersToExpel.Add(customer);
        }

        if (_customerPool == null)
            return customersToExpel;

        Transform paymentAnchor = table.PaymentAnchor;
        IReadOnlyList<Customer> activeCustomers = _customerPool.ActiveCustomers;

        for (int i = 0; i < activeCustomers.Count; i++)
        {
            Customer customer = activeCustomers[i];

            if (customer == null || customer.IsVip || !seen.Add(customer))
                continue;

            if (!IsAwaitingPayment(customer))
                continue;

            if (paymentAnchor != null && MatchesPaymentAnchor(customer, paymentAnchor))
                customersToExpel.Add(customer);
        }

        return customersToExpel;
    }

    /// <summary>
    /// Competitor scene: steal a customer/VIP who is still queuing. They leave immediately.
    /// </summary>
    public bool TryStealQueuedCustomer(Customer customer)
    {
        if (!RestaurantSceneMode.IsCompetitorScene || customer == null)
            return false;

        if (customer.IsImmuneToCompetitorSteal || customer.WasStolenByCompetitor)
            return false;

        if (customer.State != CustomerState.Queue)
            return false;

        customer.MarkStolenByCompetitor();
        ExpelCustomerImmediately(customer);
        return true;
    }

    private void ExpelCustomerImmediately(Customer customer)
    {
        if (customer == null)
            return;

        if (_activeFlows.TryGetValue(customer, out Coroutine flow))
        {
            StopCoroutine(flow);
            _activeFlows.Remove(customer);
        }

        UnregisterAwaitingPayment(customer);
        _completedPayments.Remove(customer);

        // Queued steals have no dish yet — skip worker reassignment on the tap frame.
        if (customer.State != CustomerState.Queue)
            WorkerManager.Instance?.CancelOrdersForCustomer(customer);

        Transform paymentAnchor = customer.PendingPaymentAnchor;

        if (paymentAnchor == null && customer.Seat != null)
            paymentAnchor = customer.Seat.PaymentUiAnchor;

        customer.ClearPendingPayment();

        if (paymentAnchor != null)
            UIManager.Instance?.TryHideSeatPaymentUi(paymentAnchor);

        if (_customerQueue != null && customer.QueueSlotIndex >= 0)
            _customerQueue.Release(customer.QueueSlotIndex);

        ReleaseCustomerSeatAndNotify(customer);

        customer.QueueSlotIndex = -1;
        customer.SetState(CustomerState.Leaving);
        StartCoroutine(LeaveStolenCustomer(customer));
    }

    private IEnumerator LeaveStolenCustomer(Customer customer)
    {
        // Let the steal tap finish this frame before NavMesh pathing, which can hitch.
        yield return null;
        yield return Leave(customer);
    }

    private TableSeat FindFreeSeat(Customer customer)
    {
        if (_registeredSeats.Count == 0)
            return null;

        bool wantsVipSeat = customer != null && customer.IsVip;
        TableSeat chosen = null;
        int availableCount = 0;

        for (int i = 0; i < _registeredSeats.Count; i++)
        {
            TableSeat seat = _registeredSeats[i];

            if (seat == null || !seat.isActiveAndEnabled || seat.IsOccupied)
                continue;

            DiningTable table = FindTableForSeat(seat);

            if (table != null && table.IsBroken)
                continue;

            bool isVipSeat = table != null && table.IsVipTable;

            if (wantsVipSeat != isVipSeat)
                continue;

            availableCount++;

            if (Random.Range(0, availableCount) == 0)
                chosen = seat;
        }

        return chosen;
    }

    private static void NotifyTableSeatChanged(TableSeat seat)
    {
        if (seat == null)
            return;

        DiningTable table = seat.ParentTable;

        if (table != null)
            GameEvents.RaiseTableStatusChanged(table);
    }

    private static void NotifyTableStatusForPaymentAnchor(Transform paymentAnchor)
    {
        if (paymentAnchor == null)
            return;

        DiningTable table = paymentAnchor.GetComponentInParent<DiningTable>();

        if (table != null)
            GameEvents.RaiseTableStatusChanged(table);
    }

    private static Transform FindTransformByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        GameObject found = GameObject.Find(objectName);

        return found != null ? found.transform : null;
    }

    private void EvacuateAllCustomers()
    {
        if (_customerPool == null)
            return;

        List<Customer> activeCustomers = new(_customerPool.ActiveCustomers);

        for (int i = 0; i < activeCustomers.Count; i++)
        {
            Customer customer = activeCustomers[i];

            if (customer == null || !_activeFlows.ContainsKey(customer))
                continue;

            WorkerManager.Instance?.CancelOrdersForCustomer(customer);
            StopCoroutine(_activeFlows[customer]);
            _activeFlows.Remove(customer);
            customer.StopMovement();
            StartCoroutine(Leave(customer));
        }
    }

    private void StopAllCustomerFlows()
    {
        foreach (KeyValuePair<Customer, Coroutine> entry in _activeFlows)
        {
            if (entry.Value != null)
                StopCoroutine(entry.Value);

            entry.Key?.StopMovement();
        }

        _activeFlows.Clear();
        _vipAwaitingIntro = null;
        _vipIntroAcknowledged = false;
        UIManager.Instance?.HideVipIntroButton();
        UIManager.Instance?.HideVipWaitTimer();
        StopVipStageMusic();
    }

    private void CacheVipStageMusicParticles()
    {
        if (_vipStageMusicParticles == null)
        {
            Transform geTai = _vipStagePoint != null
                ? _vipStagePoint
                : FindTransformByName("GeTai");
            Transform found = FindChildTransformByName(geTai, VipStageMusicParticlesName);

            if (found == null)
                found = FindTransformByName(VipStageMusicParticlesName);

            if (found != null)
            {
                _vipStageMusicParticles = found.GetComponent<ParticleSystem>()
                    ?? found.GetComponentInChildren<ParticleSystem>(true);
            }
        }

        if (_vipStageMusicParticles == null)
            return;

        ParticleSystem.MainModule main = _vipStageMusicParticles.main;
        main.loop = true;
        main.playOnAwake = false;

        if (_vipStageMusicParticlesInitialized)
            return;

        _vipStageMusicParticlesInitialized = true;
        StopVipStageMusicParticles();
    }

    private void PlayVipStageMusicParticles()
    {
        CacheVipStageMusicParticles();

        if (_vipStageMusicParticles == null)
            return;

        if (!_vipStageMusicParticles.gameObject.activeSelf)
            _vipStageMusicParticles.gameObject.SetActive(true);

        ParticleSystem.MainModule main = _vipStageMusicParticles.main;
        main.loop = true;
        main.playOnAwake = false;
        _vipStageMusicParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _vipStageMusicParticles.Play(true);
    }

    private void StopVipStageMusicParticles()
    {
        if (_vipStageMusicParticles == null)
            return;

        _vipStageMusicParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void PlayVipStageMusic()
    {
        PlayVipStageMusicParticles();
        PlayVipStageAudio();
    }

    private void StopVipStageMusic()
    {
        StopVipStageMusicParticles();
        StopVipStageAudio();
    }

    private void CacheVipStageAudioSource()
    {
        if (_vipStageAudioSource != null)
            return;

        if (_vipPerformer == null)
            return;

        Transform audioObject = FindChildTransformByName(_vipPerformer.transform, PerformerAudioSourceName);
        if (audioObject != null && audioObject != _vipPerformer.transform)
            _vipStageAudioSource = audioObject.GetComponent<AudioSource>();
    }

    private void PlayVipStageAudio()
    {
        CacheVipStageAudioSource();
        if (_vipStageAudioSource == null)
            return;

        _vipStageAudioSource.playOnAwake = false;
        AudioManager.PlayBgmOn(_vipStageAudioSource, BgmId.Performer);
    }

    private void StopVipStageAudio()
    {
        CacheVipStageAudioSource();
        AudioManager.StopSource(_vipStageAudioSource);
    }

    private static Transform FindChildTransformByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
            return null;

        if (string.Equals(root.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform nested = FindChildTransformByName(root.GetChild(i), objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }
}
