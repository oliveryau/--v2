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
    [SerializeField] private Transform _vipEntryWaypoint;

    [Header("Timings")]
    [SerializeField] private float _queueWaitTimeout;
    [SerializeField] private float _foodWaitTimeout;
    [SerializeField] private float _eatDuration;

    [Header("VIP Events")]
    [SerializeField] private float _vipSettleDelay = 3f;
    [SerializeField] private float _vipEventGapDelay = 1.5f;
    [SerializeField] private int _vipEventBonusPerRequest = 1000;
    [SerializeField] private float _vipCallLadyLeaveDelay = 2.5f;
    [SerializeField] private float _vipSideServiceHoldDuration = 3f;
    [SerializeField] private Worker _vipFloorWaiter;
    [SerializeField] private Transform _vipWaiterServePoint;
    [SerializeField] private Worker[] _vipCallLadyWorkers;
    [SerializeField] private Transform[] _vipLackeyPoints;
    [SerializeField] private Transform[] _vipCallLadyWaypoints;
    [SerializeField] private Worker _vipPerformer;
    [SerializeField] private Transform _vipStagePoint;
    [SerializeField] private Transform _vipPerformerWaypoint;

    private readonly Dictionary<Customer, Coroutine> _activeFlows = new();
    private readonly HashSet<Customer> _completedPayments = new();
    private readonly Dictionary<Transform, int> _awaitingPaymentCounts = new();
    private readonly Dictionary<Transform, int> _vipAwaitingPaymentCounts = new();
    private readonly List<TableSeat> _registeredSeats = new();
    private Coroutine _spawnRoutine;
    private Coroutine _callLadyDismissRoutine;
    private Coroutine _performerReturnRoutine;
    private int _spawnCount;
    private bool _awaitingVipAfterPrankster;
    private int _customersSincePranksterLeft;
    private bool _callLadiesActive;
    private bool _callLadiesStationed;
    private bool _performerOnStage;
    private Customer _vipAwaitingIntro;
    private bool _vipIntroAcknowledged;
    private VipEventType? _pendingVipEvent;
    private Customer _pendingVipEventCustomer;
    private bool _vipEventAcknowledged;

    private PranksterManager _pranksterManager;

    /// <summary>World anchor for the VIP GeTai / stage UI button.</summary>
    public Transform VipStagePoint
    {
        get
        {
            CacheVipPerformerReferences();
            return _vipStagePoint;
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

    public VipEventType? PendingVipEvent => _pendingVipEvent;

    public Customer PendingVipEventCustomer => _pendingVipEventCustomer;

    /// <summary>
    /// Restaurant is clear enough to show the close-of-business overview:
    /// no active customers remain in the pool.
    /// </summary>
    public bool IsRestaurantClearForCloseSummary() => !HasActiveCustomers;

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
        CacheVipFloorWaiterReferences();
        CacheVipPerformerReferences();
        HideVipCallLadyWorkers();
        HideVipPerformerIfUnhired();
    }

    private void Start()
    {
        // HireSequence restores after our Awake; sync stationed call ladies / performer once that finishes.
        CacheVipCallLadyReferences();
        CacheVipPerformerReferences();
        if (AreCallLadiesAlreadyStationed())
            _callLadiesStationed = true;
    }

    private void CacheVipFloorWaiterReferences()
    {
        if (_vipWaiterServePoint == null)
            _vipWaiterServePoint = FindTransformByName("Waiter3 Servepoint");

        if (_vipFloorWaiter != null)
            return;

        Worker[] workers = FindObjectsByType<Worker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < workers.Length; i++)
        {
            Worker worker = workers[i];
            if (worker == null || worker.ExcludeFromServicePool || !worker.ServesVipFloorOnly)
                continue;

            if (worker.WorkerType != WorkerType.Waiter)
                continue;

            _vipFloorWaiter = worker;
            return;
        }
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
        {
            _callLadiesStationed = true;
            return;
        }

        for (int i = 0; i < _vipCallLadyWorkers.Length; i++)
        {
            Worker worker = _vipCallLadyWorkers[i];
            if (worker != null)
                worker.gameObject.SetActive(false);
        }

        _callLadiesActive = false;
        _callLadiesStationed = false;
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
        if (_vipStagePoint == null)
        {
            _vipStagePoint = FindTransformByName("Placeholder Stage");
            if (_vipStagePoint == null)
                _vipStagePoint = FindTransformByName("GeTai");
        }

        if (_vipPerformerWaypoint == null)
            _vipPerformerWaypoint = FindTransformByName("Performer Waypoint");

        if (_vipPerformer != null)
            return;

        Worker[] workers = FindObjectsByType<Worker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < workers.Length; i++)
        {
            Worker worker = workers[i];
            if (worker == null || !worker.ExcludeFromServicePool || !worker.ServesVipFloorOnly)
                continue;

            if (worker.name.IndexOf("Performer", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            _vipPerformer = worker;
            return;
        }
    }

    private void HideVipPerformerIfUnhired()
    {
        CacheVipPerformerReferences();
        if (_vipPerformer == null)
            return;

        // Always hide in Awake; HireSequence restore / walk-in reactivates after hire.
        _vipPerformer.gameObject.SetActive(false);
        _performerOnStage = false;
    }

    private void OnDestroy()
    {
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

    public void RemoveCustomerImmediately(Customer customer)
    {
        if (customer == null)
            return;

        if (_activeFlows.TryGetValue(customer, out Coroutine flow))
        {
            StopCoroutine(flow);
            _activeFlows.Remove(customer);
        }

        ClearVipIntroState(customer);
        ClearVipEventUiIfOwnedBy(customer);

        if (customer.IsVip)
        {
            BeginDismissVipCallLadies();
            BeginReturnVipPerformer();
        }

        UnregisterAwaitingPayment(customer);
        _completedPayments.Remove(customer);
        WorkerManager.Instance?.CancelOrdersForCustomer(customer);

        if (_customerQueue != null && customer.QueueSlotIndex >= 0)
            _customerQueue.Release(customer.QueueSlotIndex);

        if (customer.Seat != null)
            customer.Seat.Release();

        customer.Seat = null;
        customer.QueueSlotIndex = -1;

        if (_customerPool != null)
            ReleaseCustomerToPool(customer);
    }

    public void AcknowledgeVipIntro()
    {
        if (_vipAwaitingIntro == null)
            return;

        _vipIntroAcknowledged = true;
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
        UIManager.Instance?.HideAllVipEventButtons();
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
    }

    public void NotifyPranksterVisitEndedForAlternation()
    {
        if (!IsVipPranksterAlternationEnabled())
            return;

        _customersSincePranksterLeft = 0;
        _awaitingVipAfterPrankster = true;
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
        if (paymentUiAnchor == null || _customerPool == null)
            return;

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

            if (customer.IsVip)
                customer.PlayVipHappyAudio();

            totalPayment += ResolvePaymentForCustomer(customer);
            customersServed++;
        }

        if (customersServed == 0)
            return;

        AwardPayment(totalPayment);
        NotifyTableStatusForPaymentAnchor(paymentUiAnchor);
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

        _spawnCount = 0;

        if (IsVipPranksterAlternationEnabled())
        {
            _customersSincePranksterLeft = 0;
            _awaitingVipAfterPrankster = true;
        }

        _spawnRoutine = StartCoroutine(SpawnLoop());
    }

    private void StopSpawning()
    {
        if (_spawnRoutine == null)
            return;

        StopCoroutine(_spawnRoutine);
        _spawnRoutine = null;
    }

    private IEnumerator SpawnLoop()
    {
        WaitForSeconds wait = new(_spawnInterval);

        while (GameManager.Instance != null
            && GameManager.Instance.IsBusiness
            && GameManager.Instance.IsBusinessSessionActive)
        {
            yield return wait;

            if (!CanSpawnCustomer())
                continue;

            SpawnCustomer();
        }
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
        return firstTableCap + (builtTables - 1) * perAdditional;
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
            MarkFirstVipCustomerReceivedIfNeeded(customer);

        if (spawnVip)
            AudioManager.Play(SfxId.VipArrival);

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
        }

        if (_vipSpawnInterval <= 0)
            return false;

        return _spawnCount % _vipSpawnInterval == 0;
    }

    private bool MeetsVipSpawnRequirements()
    {
        if (WorkerManager.Instance == null || !WorkerManager.Instance.HasVipFloorWaiterHired())
            return false;

        return HasAvailableVipSeat();
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

    private int GetActiveVipCountForAlternation()
    {
        if (_customerPool == null)
            return 0;

        int vipCount = 0;
        IReadOnlyList<Customer> customers = _customerPool.ActiveCustomers;

        for (int i = 0; i < customers.Count; i++)
        {
            Customer customer = customers[i];

            if (customer == null)
                continue;

            // For alternation: any VIP that hasn't transitioned into Leaving blocks VIP spawns.
            if (customer.IsVip && customer.State != CustomerState.Leaving)
                vipCount++;
        }

        return vipCount;
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

    private void TrackFlow(Customer customer, Coroutine flow)
    {
        _activeFlows[customer] = flow;
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

        TableSeat seat = null;
        float seatWait = 0f;
        UIManager.Instance?.ShowVipWaitTimer(customer, _queueWaitTimeout);

        while (seat == null && seatWait < _queueWaitTimeout)
        {
            seat = FindFreeSeat(customer);

            if (seat == null)
            {
                seatWait += Time.deltaTime;
                UIManager.Instance?.UpdateVipWaitTimer(customer, Mathf.Max(0f, _queueWaitTimeout - seatWait), _queueWaitTimeout);
                yield return null;
            }
        }

        if (seat == null)
        {
            UIManager.Instance?.HideVipWaitTimer(customer);
            UIManager.Instance?.SetVipDialogue(VipDialogueState.UnhappyLeave);
            yield return Leave(customer);
            yield break;
        }

        UIManager.Instance?.HideVipWaitTimer(customer);

        TableSeat assignedSeat = null;
        yield return TryWalkCustomerToSeat(customer, seat, result => assignedSeat = result);

        if (assignedSeat == null)
        {
            yield return Leave(customer);
            yield break;
        }

        seat = assignedSeat;
        customer.SetState(CustomerState.Ordering);
        yield return RunVipSeatedEvents(customer, seat);
    }

    private IEnumerator RunVipSeatedEvents(Customer customer, TableSeat seat)
    {
        customer.VipEventBonus = 0;

        // Chef starts prepping VIP food immediately; waiter only collects after 上菜.
        DishOrder vipOrder = WorkerManager.Instance != null
            ? WorkerManager.Instance.SubmitVipHeldOrder(customer)
            : null;

        if (_vipSettleDelay > 0f)
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
                UIManager.Instance?.HideAllVipEventButtons();
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
        // Always include ServeDish; pick 1–2 more from the available pool.
        VipEventType[] optionalPool =
        {
            VipEventType.FeetMassage,
            VipEventType.ServeTea,
            VipEventType.CallLady,
            VipEventType.WatchStage
        };

        int optionalCount = Random.Range(0, 2) == 0 ? 1 : 2;
        optionalCount = Mathf.Min(optionalCount, optionalPool.Length);

        for (int i = optionalPool.Length - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            VipEventType temp = optionalPool[i];
            optionalPool[i] = optionalPool[swapIndex];
            optionalPool[swapIndex] = temp;
        }

        VipEventType[] sequence = new VipEventType[optionalCount + 1];
        for (int i = 0; i < optionalCount; i++)
            sequence[i] = optionalPool[i];

        sequence[optionalCount] = VipEventType.ServeDish;

        for (int i = sequence.Length - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            VipEventType temp = sequence[i];
            sequence[i] = sequence[swapIndex];
            sequence[swapIndex] = temp;
        }

        return sequence;
    }

    private IEnumerator RunVipServeDishEvent(Customer customer, DishOrder order, System.Action<bool> onComplete)
    {
        BeginPendingVipEvent(customer, VipEventType.ServeDish);

        UIManager.Instance?.ShowVipEventButton(VipEventType.ServeDish, customer);
        UIManager.Instance?.ShowVipWaitTimer(customer, _foodWaitTimeout);

        float wait = 0f;
        while (!_vipEventAcknowledged && wait < _foodWaitTimeout)
        {
            wait += Time.deltaTime;
            UIManager.Instance?.UpdateVipWaitTimer(customer, Mathf.Max(0f, _foodWaitTimeout - wait), _foodWaitTimeout);
            yield return null;
        }

        bool acknowledged = _vipEventAcknowledged;
        ClearPendingVipEvent();
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
        UIManager.Instance?.ShowVipWaitTimer(customer, _foodWaitTimeout);

        while (order != null
            && !order.IsDelivered
            && !order.IsCancelled
            && deliveryWait < _foodWaitTimeout)
        {
            deliveryWait += Time.deltaTime;
            UIManager.Instance?.UpdateVipWaitTimer(
                customer,
                Mathf.Max(0f, _foodWaitTimeout - deliveryWait),
                _foodWaitTimeout);
            yield return null;
        }

        UIManager.Instance?.HideVipWaitTimer(customer);

        bool delivered = order != null && order.IsDelivered && !order.IsCancelled;
        onComplete?.Invoke(delivered);
    }

    private IEnumerator RunSingleVipEvent(Customer customer, VipEventType eventType, System.Action<bool> onComplete)
    {
        BeginPendingVipEvent(customer, eventType);

        UIManager.Instance?.ShowVipEventButton(eventType, customer);
        UIManager.Instance?.ShowVipWaitTimer(customer, _foodWaitTimeout);

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
        else if (eventType == VipEventType.ServeTea || eventType == VipEventType.FeetMassage)
            yield return RunVipFloorWaiterSideService(customer);
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

        Transform stage = _vipStagePoint;
        performer.Locomotion?.ExitStationary();
        performer.SetState(WorkerState.Wait);

        if (stage != null)
            yield return MoveWorkerTo(performer, stage.position);

        FaceWorkerToward(performer, vip != null ? vip.transform : null);
        performer.Locomotion?.EnterStationary();
        _performerOnStage = true;
    }

    private IEnumerator RunVipFloorWaiterSideService(Customer vip)
    {
        CacheVipFloorWaiterReferences();

        Worker waiter = _vipFloorWaiter;
        if (waiter == null || !waiter.gameObject.activeInHierarchy)
            yield break;

        // Wait until she's free, then lock so dish runs don't interrupt 上茶 / 泡脚.
        if (WorkerManager.Instance != null)
        {
            float waitForFree = 0f;
            const float maxWaitForFree = 30f;
            while (!WorkerManager.Instance.TryBeginExternalTask(waiter) && waitForFree < maxWaitForFree)
            {
                waitForFree += Time.deltaTime;
                yield return null;
            }

            if (waitForFree >= maxWaitForFree && !WorkerManager.Instance.TryBeginExternalTask(waiter))
                yield break;
        }

        Transform servePoint = _vipWaiterServePoint;
        Transform returnPoint = waiter.WaitPoint;

        waiter.Locomotion?.ExitStationary();

        if (servePoint != null)
        {
            yield return MoveWorkerTo(waiter, servePoint.position);
            waiter.FaceDirection(servePoint.rotation);
        }
        else
        {
            FaceWorkerToward(waiter, vip != null ? vip.transform : null);
        }

        waiter.Locomotion?.EnterStationary();

        WorkerCharacterAnimator waiterAnimator = waiter.GetComponent<WorkerCharacterAnimator>();
        waiterAnimator?.SetCleaning(true);

        float hold = Mathf.Max(0f, _vipSideServiceHoldDuration);
        if (hold > 0f)
            yield return new WaitForSeconds(hold);

        waiterAnimator?.SetCleaning(false);
        waiter.Locomotion?.ExitStationary();

        bool needsRest = waiter.Energy != null && waiter.Energy.ApplyServeCost();

        if (needsRest && WorkerManager.Instance != null)
        {
            yield return WorkerManager.Instance.RunRestRoutine(waiter);
            WorkerManager.Instance.EndExternalTask(waiter);
            yield break;
        }

        if (returnPoint != null)
        {
            yield return MoveWorkerTo(waiter, returnPoint.position);
            waiter.FaceDirection(returnPoint.rotation);
        }

        waiter.Locomotion?.EnterStationary();
        waiter.SetState(WorkerState.Wait);
        WorkerManager.Instance?.EndExternalTask(waiter);
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
            yield break;

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
        _callLadiesStationed = true;
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
        }

        onArrived?.Invoke();
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
        _callLadiesStationed = true;
        _callLadyDismissRoutine = null;
    }

    private void BeginReturnVipPerformer()
    {
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
        TableSeat seat = null;
        float waitTimer = 0f;

        while (seat == null && waitTimer < _queueWaitTimeout)
        {
            seat = FindFreeSeat(customer);

            if (seat == null)
            {
                waitTimer += Time.deltaTime;
                yield return null;
            }
        }

        if (seat == null)
        {
            _customerQueue.Release(customer.QueueSlotIndex);
            customer.QueueSlotIndex = -1;
            yield return Leave(customer);
            yield break;
        }

        _customerQueue.Release(customer.QueueSlotIndex);
        customer.QueueSlotIndex = -1;

        TableSeat assignedSeat = null;
        yield return TryWalkCustomerToSeat(customer, seat, result => assignedSeat = result);

        if (assignedSeat == null)
        {
            yield return Leave(customer);
            yield break;
        }

        seat = assignedSeat;
        customer.SetState(CustomerState.Ordering);

        DishOrder order = WorkerManager.Instance != null
            ? WorkerManager.Instance.SubmitOrder(customer)
            : null;

        yield return RunCustomerFlowFromOrdering(customer, seat, order);
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

            _completedPayments.Remove(customer);
            ReleaseCustomerSeatAndNotify(customer, seat);
            yield return Leave(customer);
            yield break;
        }

        // Normal customers auto-collect; UIManager plays the coin FX on Paying.
        CompletePayment(customer);
        ReleaseCustomerSeatAndNotify(customer, seat);
        yield return Leave(customer);
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

        if (wasVip && GetActiveVipCount() <= 0)
            UIManager.Instance?.HideVipUi();
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

            seat.TryReserve(customer);
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
        StartCoroutine(Leave(customer));
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
    }
}
