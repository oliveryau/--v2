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
    [SerializeField] private TableSeat[] _seats;
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

    private readonly Dictionary<Customer, Coroutine> _activeFlows = new();
    private readonly HashSet<Customer> _completedPayments = new();
    private readonly Dictionary<Transform, int> _awaitingPaymentCounts = new();
    private readonly Dictionary<Transform, int> _vipAwaitingPaymentCounts = new();
    private readonly List<TableSeat> _registeredSeats = new();
    private Coroutine _spawnRoutine;
    private int _spawnCount;
    private bool _awaitingVipAfterPrankster;
    private int _customersSincePranksterLeft;

    private PranksterManager _pranksterManager;

    public IReadOnlyList<Customer> ActiveCustomers => _customerPool != null
        ? _customerPool.ActiveCustomers
        : System.Array.Empty<Customer>();

    public bool HasActiveCustomers =>
        _customerPool != null && _customerPool.ActiveCustomers.Count > 0;

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

    private void Start()
    {
        RegisterConfiguredSeats();
    }

    private void RegisterConfiguredSeats()
    {
        if (_seats != null && _seats.Length > 0)
            RegisterSeats(_seats);
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
        if (customer == null || customer.State != CustomerState.Paying)
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

            if (table != null && table.isActiveAndEnabled && table.gameObject.activeInHierarchy)
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
        if (IsVipPranksterAlternationEnabled())
        {
            if (_customerPool == null || !_customerPool.HasVipPrefabs)
                return false;

            if (_pranksterManager == null)
                _pranksterManager = FindFirstObjectByType<PranksterManager>();

            if (_pranksterManager != null && _pranksterManager.IsVisitActive)
                return false;

            if (_pranksterManager != null && _pranksterManager.IsAwaitingPranksterSpawn)
                return false;

            if (GetActiveVipCountForAlternation() > 0)
                return false;

            if (!_awaitingVipAfterPrankster)
                return false;

            if (_customersSincePranksterLeft < Mathf.Max(1, _vipSpawnInterval))
                return false;

            _awaitingVipAfterPrankster = false;
            _customersSincePranksterLeft = 0;
            return true;
        }

        if (_vipSpawnInterval <= 0 || _customerPool == null || !_customerPool.HasVipPrefabs)
            return false;

        if (GetActiveVipCount() >= Mathf.Max(1, _maxActiveVips))
            return false;

        return _spawnCount % _vipSpawnInterval == 0;
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
        if (_customerQueue == null || !_customerQueue.TryAssign(customer, out int queueSlot))
        {
            yield return Leave(customer);
            yield break;
        }

        customer.SetState(CustomerState.Queue);

        if (customer.IsVip && _vipEntryWaypoint != null)
        {
            yield return CustomerMovement.Instance.MoveTo(customer, _vipEntryWaypoint.position);
            UIManager.Instance?.PlayVipAnnouncement();
        }

        yield return CustomerMovement.Instance.MoveTo(customer, _customerQueue.GetSlotPosition(queueSlot));
        customer.EnterStationary();
        yield return RunCustomerFlowAfterQueued(customer);
    }

    private IEnumerator RunCustomerFlowAfterQueued(Customer customer)
    {
        TableSeat seat = null;
        float waitTimer = 0f;

        while (seat == null && waitTimer < _queueWaitTimeout)
        {
            seat = FindFreeSeat();

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

        ReleaseCustomerSeatAndNotify(customer, seat);
        yield return LeaveWhileAwaitingPayment(customer);
    }

    private IEnumerator LeaveWhileAwaitingPayment(Customer customer)
    {
        if (_exitPoint != null && CustomerMovement.Instance != null)
            yield return CustomerMovement.Instance.MoveTo(customer, _exitPoint.position);

        _activeFlows.Remove(customer);

        while (!_completedPayments.Contains(customer))
            yield return null;

        _completedPayments.Remove(customer);
        customer.ClearPendingPayment();
        customer.SetState(CustomerState.Leaving);

        if (_customerQueue != null && customer.QueueSlotIndex >= 0)
            _customerQueue.Release(customer.QueueSlotIndex);

        ReleaseCustomerToPool(customer);
    }

    private IEnumerator Leave(Customer customer)
    {
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
    }

    private IEnumerator TryWalkCustomerToSeat(Customer customer, TableSeat preferredSeat, System.Action<TableSeat> onComplete)
    {
        const int maxAttempts = 4;
        TableSeat assignedSeat = null;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            TableSeat seat = attempt == 0 ? preferredSeat : FindFreeSeat();

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

    private TableSeat FindFreeSeat()
    {
        if (_registeredSeats.Count == 0)
            return null;

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
    }
}
