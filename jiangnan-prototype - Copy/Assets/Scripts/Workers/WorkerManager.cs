using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(118)]
public class WorkerManager : MonoBehaviour
{
    public static WorkerManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private StoveStation _stoveStation;

    [Header("Timings")]
    [SerializeField] private float _cookDuration;

    private readonly List<Worker> _chefs = new();
    private readonly List<Worker> _waiters = new();
    private readonly Queue<DishOrder> _pendingCookOrders = new();
    private readonly Queue<DishOrder> _readyOrders = new();
    private readonly Dictionary<Worker, Coroutine> _activeTasks = new();
    private readonly Dictionary<Worker, DishOrder> _activeOrders = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        GameEvents.StateChanged += HandleStateChanged;
    }

    private void Start()
    {
        RefreshWorkerRoster();
    }

    private void OnDisable()
    {
        GameEvents.StateChanged -= HandleStateChanged;
        ClearBusinessOperations();
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Business)
        {
            RefreshWorkerRoster();
            ResetAllWorkerEnergy();
            ProcessOrders();
            return;
        }

        ClearBusinessOperations();
    }

    private void ClearBusinessOperations()
    {
        StopAllTasks();
        _pendingCookOrders.Clear();
        _readyOrders.Clear();
        _activeOrders.Clear();
        _stoveStation?.ReleaseAllPickups();
    }

    public void RegisterWorker(Worker worker)
    {
        if (worker == null)
            return;

        List<Worker> roster = GetRoster(worker.WorkerType);
        if (!roster.Contains(worker))
            roster.Add(worker);
    }

    public void UnregisterWorker(Worker worker)
    {
        if (worker == null)
            return;

        CancelTask(worker);
        _chefs.Remove(worker);
        _waiters.Remove(worker);
        _stoveStation?.ReleasePickup(worker);
        worker.StopMovement();
        worker.ResetToWait();
    }

    public DishOrder SubmitOrder(Customer customer)
    {
        if (customer == null)
            return null;

        DishOrder order = new DishOrder(customer);
        _pendingCookOrders.Enqueue(order);
        ProcessOrders();
        return order;
    }

    public void CancelOrder(DishOrder order)
    {
        if (order == null || order.IsDelivered)
            return;

        order.Cancel();
        RemoveFromQueue(_pendingCookOrders, order);
        RemoveFromQueue(_readyOrders, order);
        CancelActiveOrder(order);
        ProcessOrders();
    }

    public void CancelOrdersForCustomer(Customer customer)
    {
        if (customer == null)
            return;

        CancelOrdersInQueue(_pendingCookOrders, customer);
        CancelOrdersInQueue(_readyOrders, customer);
        CancelActiveOrdersForCustomer(customer);
        ProcessOrders();
    }

    private void CancelActiveOrder(DishOrder order)
    {
        if (order == null)
            return;

        List<Worker> workersToCancel = null;

        foreach (KeyValuePair<Worker, DishOrder> entry in _activeOrders)
        {
            if (entry.Value != order)
                continue;

            order.Cancel();

            if (workersToCancel == null)
                workersToCancel = new List<Worker>();

            workersToCancel.Add(entry.Key);
        }

        if (workersToCancel == null)
            return;

        for (int i = 0; i < workersToCancel.Count; i++)
            CancelTask(workersToCancel[i]);
    }

    private void CancelActiveOrdersForCustomer(Customer customer)
    {
        List<Worker> workersToCancel = null;

        foreach (KeyValuePair<Worker, DishOrder> entry in _activeOrders)
        {
            DishOrder order = entry.Value;

            if (order == null || order.Customer != customer || order.IsDelivered)
                continue;

            order.Cancel();

            if (workersToCancel == null)
                workersToCancel = new List<Worker>();

            workersToCancel.Add(entry.Key);
        }

        if (workersToCancel == null)
            return;

        for (int i = 0; i < workersToCancel.Count; i++)
            CancelTask(workersToCancel[i]);
    }

    private void CancelOrdersInQueue(Queue<DishOrder> queue, Customer customer)
    {
        if (queue.Count == 0)
            return;

        int count = queue.Count;

        for (int i = 0; i < count; i++)
        {
            DishOrder order = queue.Dequeue();

            if (order != null && order.Customer == customer && !order.IsDelivered)
                order.Cancel();
            else if (order != null)
                queue.Enqueue(order);
        }
    }

    private void ProcessOrders()
    {
        SendExhaustedWorkersToRest(_chefs);
        SendExhaustedWorkersToRest(_waiters);

        bool assigned;

        do
        {
            assigned = false;

            if (TryAssignChef())
                assigned = true;

            if (TryAssignWaiter())
                assigned = true;
        }
        while (assigned);
    }

    private void SendExhaustedWorkersToRest(List<Worker> roster)
    {
        for (int i = 0; i < roster.Count; i++)
        {
            Worker worker = roster[i];

            if (worker == null || !worker.isActiveAndEnabled || !worker.IsAvailable)
                continue;

            if (worker.Energy == null || !worker.Energy.ShouldRest)
                continue;

            if (_activeTasks.ContainsKey(worker))
                continue;

            _activeTasks[worker] = StartCoroutine(RestRoutine(worker));
        }
    }

    private bool TryAssignChef()
    {
        if (_stoveStation == null || _pendingCookOrders.Count == 0)
            return false;

        Worker chef = FindAvailableWorker(_chefs);

        if (chef == null)
            return false;

        DishOrder order = DequeuePrioritizedOrder(_pendingCookOrders);

        if (order == null)
            return false;

        Coroutine task = StartCoroutine(CookOrderRoutine(chef, order));
        _activeTasks[chef] = task;
        return true;
    }

    private bool TryAssignWaiter()
    {
        if (_stoveStation == null || _readyOrders.Count == 0 || !_stoveStation.HasAvailablePickup)
            return false;

        Worker waiter = FindAvailableWorker(_waiters);

        if (waiter == null || !_stoveStation.TryReservePickup(waiter, out int pickupIndex))
            return false;

        DishOrder order = DequeuePrioritizedOrder(_readyOrders);

        if (order == null)
        {
            _stoveStation.ReleasePickup(waiter);
            return false;
        }

        Coroutine task = StartCoroutine(DeliverOrderRoutine(waiter, order, pickupIndex));
        _activeTasks[waiter] = task;
        return true;
    }

    private IEnumerator CookOrderRoutine(Worker chef, DishOrder order)
    {
        _activeOrders[chef] = order;

        // Always cook from the chef wait waypoint, never from the rest point.
        chef.Locomotion.ExitStationary();
        yield return ReturnToWaitPoint(chef);

        if (order.IsCancelled)
        {
            AbortCook(chef);
            yield break;
        }

        chef.SetState(WorkerState.Cook);
        StartChefCookingAudio(chef);

        float elapsed = 0f;
        float cookDuration = Mathf.Max(0f, _cookDuration);

        while (elapsed < cookDuration)
        {
            if (order.IsCancelled)
            {
                StopChefCookingAudio(chef);
                AbortCook(chef);
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (order.IsCancelled)
        {
            StopChefCookingAudio(chef);
            AbortCook(chef);
            yield break;
        }

        order.MarkReady();
        _readyOrders.Enqueue(order);
        _activeOrders.Remove(chef);
        ProcessOrders();

        StopChefCookingAudio(chef);
        yield return FinishWorkerTask(chef);
        _activeTasks.Remove(chef);
    }

    private void AbortCook(Worker chef)
    {
        StopChefCookingAudio(chef);
        _activeTasks.Remove(chef);
        _activeOrders.Remove(chef);
        chef.Locomotion.ExitStationary();
        chef.SetState(WorkerState.Wait);
        ProcessOrders();
    }

    private IEnumerator DeliverOrderRoutine(Worker waiter, DishOrder order, int pickupIndex)
    {
        _activeOrders[waiter] = order;

        try
        {
            Customer customer = order.Customer;

            if (ShouldAbortDelivery(customer, order))
            {
                AbortDelivery(waiter, order, releasePickup: true);
                yield break;
            }

            waiter.SetState(WorkerState.GoToStove);

            yield return MoveWorkerTo(waiter, _stoveStation.GetPickupPosition(pickupIndex));

            if (ShouldAbortDelivery(customer, order))
            {
                AbortDelivery(waiter, order, releasePickup: true);
                yield break;
            }

            waiter.FaceDirection(_stoveStation.GetPickupRotation(pickupIndex));
            _stoveStation.ReleasePickup(waiter);

            waiter.SetState(WorkerState.BringDish);

            if (ShouldAbortDelivery(customer, order))
            {
                AbortDelivery(waiter, order, releasePickup: false);
                yield break;
            }

            yield return MoveWorkerTo(waiter, customer.Seat.Position);

            if (ShouldAbortDelivery(customer, order))
            {
                AbortDelivery(waiter, order, releasePickup: false);
                yield break;
            }

            waiter.FaceDirection(customer.Seat.Rotation);
            order.MarkDelivered();

            yield return FinishWorkerTask(waiter);
        }
        finally
        {
            _activeTasks.Remove(waiter);
            _activeOrders.Remove(waiter);
        }
    }

    private static bool ShouldAbortDelivery(Customer customer, DishOrder order)
    {
        return customer == null
            || customer.Seat == null
            || customer.Seat.Occupant != customer
            || order.IsCancelled;
    }

    private void AbortDelivery(Worker waiter, DishOrder order, bool releasePickup)
    {
        if (releasePickup)
            _stoveStation?.ReleasePickup(waiter);

        waiter.StopMovement();

        if (!order.IsDelivered)
            order.Cancel();

        _activeTasks.Remove(waiter);
        _activeOrders.Remove(waiter);
        waiter.Locomotion.ExitStationary();
        waiter.SetState(WorkerState.Wait);
        ProcessOrders();
    }

    private IEnumerator FinishWorkerTask(Worker worker)
    {
        if (!RestaurantSceneMode.IsCompetitorScene && worker.Energy.ApplyServeCost())
        {
            // Keep this worker tracked in _activeTasks while resting so CancelTask
            // can stop the rest flow cleanly if needed.
            yield return RestRoutine(worker);
            _activeTasks.Remove(worker);
            _activeOrders.Remove(worker);
            ProcessOrders();
            yield break;
        }

        if (worker.WorkerType == WorkerType.Waiter)
        {
            worker.Locomotion.ExitStationary();
            yield return ReturnToWaitPoint(worker);
        }

        worker.SetState(WorkerState.Wait);
        ProcessOrders();
    }

    private IEnumerator RestRoutine(Worker worker)
    {
        worker.SetState(WorkerState.Rest);
        worker.Locomotion.ExitStationary();

        Transform restPoint = worker.GetRestPoint();

        if (restPoint != null)
        {
            yield return MoveWorkerTo(worker, restPoint.position);
            worker.FaceDirection(restPoint.rotation);
        }

        worker.Locomotion.EnterStationary();
        worker.PlayRestingAudio();
        yield return worker.Energy.WaitUntilRecoveredEnoughRoutine();

        worker.StopRestingAudio();

        // Always return to the cook/wait waypoint after resting so chefs don't stay stuck at rest.
        worker.Locomotion.ExitStationary();
        yield return ReturnToWaitPoint(worker);
        worker.SetState(WorkerState.Wait);
    }

    public void KickFromRest(Worker worker)
    {
        // Kick-to-work removed: workers rest automatically until recovered.
    }

    private IEnumerator ReturnFromRestAndBecomeAvailable(Worker worker)
    {
        if (worker == null)
            yield break;

        worker.Locomotion.ExitStationary();
        yield return ReturnToWaitPoint(worker);
        worker.SetState(WorkerState.Wait);
        _activeTasks.Remove(worker);
        _activeOrders.Remove(worker);
        ProcessOrders();
    }

    private IEnumerator ReturnToWaitPoint(Worker worker)
    {
        if (worker.WaitPoint == null)
            yield break;

        yield return MoveWorkerTo(worker, worker.WaitPoint.position);
        worker.FaceDirection(worker.WaitPoint.rotation);
    }

    private IEnumerator MoveWorkerTo(Worker worker, Vector3 destination)
    {
        if (WorkerMovement.Instance == null)
        {
            Debug.LogWarning("WorkerMovement is missing. Add it to Build Manager.", this);
            yield break;
        }

        worker.Locomotion.ExitStationary();
        yield return WorkerMovement.Instance.MoveTo(worker, destination);
    }

    private Worker FindAvailableWorker(List<Worker> roster)
    {
        for (int i = 0; i < roster.Count; i++)
        {
            Worker worker = roster[i];

            if (worker == null || !worker.isActiveAndEnabled || !worker.IsAvailable || worker.IsResting)
                continue;

            if (worker.Energy != null && worker.Energy.ShouldRest)
                continue;

            if (_activeTasks.ContainsKey(worker))
                continue;

            return worker;
        }

        return null;
    }

    private void RefreshWorkerRoster()
    {
        Worker[] workers = FindObjectsOfType<Worker>(true);

        for (int i = 0; i < workers.Length; i++)
        {
            if (workers[i] != null)
                RegisterWorker(workers[i]);
        }
    }

    private void ResetAllWorkerEnergy()
    {
        ResetWorkerEnergy(_chefs);
        ResetWorkerEnergy(_waiters);
    }

    private static void ResetWorkerEnergy(List<Worker> workers)
    {
        for (int i = 0; i < workers.Count; i++)
            workers[i]?.Energy?.ResetEnergy();
    }

    private List<Worker> GetRoster(WorkerType workerType)
    {
        return workerType == WorkerType.Chef ? _chefs : _waiters;
    }

    private void CancelTask(Worker worker, bool stopRestingAudio = true)
    {
        if (!_activeTasks.TryGetValue(worker, out Coroutine task))
            return;

        bool wasResting = worker.State == WorkerState.Rest;

        if (task != null)
            StopCoroutine(task);

        worker.StopMovement();

        if (stopRestingAudio)
            worker.StopRestingAudio();
        _stoveStation?.ReleasePickup(worker);
        _activeTasks.Remove(worker);
        _activeOrders.Remove(worker);

        if (wasResting)
        {
            worker.Locomotion.ExitStationary();
            Coroutine returnTask = StartCoroutine(ReturnFromRestAndBecomeAvailable(worker));
            _activeTasks[worker] = returnTask;
            return;
        }

        worker.Locomotion.ExitStationary();
        worker.SetState(WorkerState.Wait);
        ProcessOrders();
    }

    private void StopAllTasks()
    {
        foreach (KeyValuePair<Worker, Coroutine> entry in _activeTasks)
        {
            if (entry.Value != null)
                StopCoroutine(entry.Value);
        }

        _activeTasks.Clear();
        _activeOrders.Clear();

        ResetRoster(_chefs);
        ResetRoster(_waiters);
    }

    private static void ResetRoster(List<Worker> roster)
    {
        for (int i = 0; i < roster.Count; i++)
        {
            if (roster[i] != null)
                roster[i].ResetToWait();
        }
    }

    private static void StartChefCookingAudio(Worker chef)
    {
        if (chef == null || chef.WorkerType != WorkerType.Chef)
            return;

        AudioManager.PlayBgmOn(chef.GetWorkerAudioSource(), BgmId.ChefCooking);
    }

    private static void StopChefCookingAudio(Worker chef)
    {
        if (chef == null)
            return;

        AudioManager.StopSource(chef.GetWorkerAudioSource());
    }

    private static DishOrder DequeuePrioritizedOrder(Queue<DishOrder> queue)
    {
        DishOrder vipOrder = DequeueFirstMatchingOrder(queue, IsVipOrder);

        if (vipOrder != null)
            return vipOrder;

        return DequeueFirstMatchingOrder(queue, IsAssignableOrder);
    }

    private static DishOrder DequeueFirstMatchingOrder(Queue<DishOrder> queue, System.Func<DishOrder, bool> predicate)
    {
        if (queue.Count == 0)
            return null;

        int count = queue.Count;
        DishOrder selected = null;

        for (int i = 0; i < count; i++)
        {
            DishOrder candidate = queue.Dequeue();

            if (selected == null && predicate(candidate))
            {
                selected = candidate;
                continue;
            }

            queue.Enqueue(candidate);
        }

        return selected;
    }

    private static bool IsAssignableOrder(DishOrder order)
    {
        return order != null && !order.IsCancelled;
    }

    private static bool IsVipOrder(DishOrder order)
    {
        return IsAssignableOrder(order) && order.Customer != null && order.Customer.IsVip;
    }

    private static void RemoveFromQueue(Queue<DishOrder> queue, DishOrder order)
    {
        if (queue.Count == 0)
            return;

        int count = queue.Count;

        for (int i = 0; i < count; i++)
        {
            DishOrder queuedOrder = queue.Dequeue();

            if (queuedOrder != order && !queuedOrder.IsCancelled)
                queue.Enqueue(queuedOrder);
        }
    }
}
