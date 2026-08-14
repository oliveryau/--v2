using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(125)]
public class PranksterManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Prankster _prankster;
    [SerializeField] private Transform _outerSpawnPoint;
    [SerializeField] private Transform _pranksterWaypoint;
    [SerializeField] private Transform _exitPoint;
    [Tooltip("Optional seed list. Break targets are rebuilt from built Table BuildSpots at runtime.")]
    [SerializeField] private DiningTable[] _tables;

    [Header("Scheduling")]
    [SerializeField] private int _customerSpawnInterval;

    private const int BrokenTableSpawnBlockThreshold = 2;

    [Header("Timings")]
    [SerializeField] private float _waitAtWaypointDuration;
    [SerializeField] private float _breakActionDuration;

    private int _customersSinceLastVisit;
    private int _customersSinceVipLeft;
    private bool _awaitingPranksterSpawn;
    private bool _visitActive;
    private bool _chaseDismissed;
    private Coroutine _visitRoutine;
    private readonly List<DiningTable> _builtTables = new();
    private readonly List<DiningTable> _tableBreakCandidates = new();

    private CustomerPool _customerPool;

    public bool ShouldShowChaseUi => _visitActive && !_chaseDismissed && _prankster != null && _prankster.gameObject.activeSelf;
    public bool IsVisitActive => _visitActive;
    public bool IsAwaitingPranksterSpawn => _awaitingPranksterSpawn;
    public Transform ChaseUiAnchor => _prankster != null ? _prankster.ChaseUiAnchor : null;
    public Transform NameUiAnchor => _prankster != null ? _prankster.NameUiAnchor : null;

    private void Awake()
    {
        if (!RestaurantSceneMode.IsMainScene)
        {
            enabled = false;
            return;
        }

        if (_prankster == null)
            _prankster = GetComponentInChildren<Prankster>(true);

        if (_outerSpawnPoint == null)
            _outerSpawnPoint = FindTransformByName("Outer Spawn Point");

        if (_pranksterWaypoint == null)
            _pranksterWaypoint = FindTransformByName("Prankster Waypoint");

        if (_exitPoint == null)
            _exitPoint = _outerSpawnPoint;
    }

    private void OnEnable()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return;

        GameEvents.CustomerSpawned += HandleCustomerSpawned;
        GameEvents.CustomerStateChanged += HandleCustomerStateChanged;
        GameEvents.StateChanged += HandleStateChanged;
        GameEvents.BuildSpotStateChanged += HandleBuildSpotStateChanged;
        RebuildBuiltTablesFromSpots();
    }

    private void Start()
    {
        if (_prankster != null)
            _prankster.gameObject.SetActive(false);

        // Build spots may finish restore after Awake/OnEnable.
        RebuildBuiltTablesFromSpots();
    }

    private void OnDisable()
    {
        GameEvents.CustomerSpawned -= HandleCustomerSpawned;
        GameEvents.CustomerStateChanged -= HandleCustomerStateChanged;
        GameEvents.StateChanged -= HandleStateChanged;
        GameEvents.BuildSpotStateChanged -= HandleBuildSpotStateChanged;
        CancelActiveVisit();
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Business)
        {
            RebuildBuiltTablesFromSpots();
            ResetVisitTracking();
            return;
        }

        CancelActiveVisit();
        ResetVisitTracking();
    }

    private void HandleBuildSpotStateChanged(BuildSpot spot, BuildSpotState state)
    {
        if (spot == null || spot.PlaceableType != PlaceableType.Table)
            return;

        if (state == BuildSpotState.Built)
            TryAddBuiltTable(ResolveDiningTable(spot));
        else
            TryRemoveBuiltTable(ResolveDiningTable(spot));
    }

    private void HandleCustomerSpawned()
    {
        if (!IsBusinessActive() || _visitActive || _prankster == null)
            return;

        // Early business (before VIP table / stage / 2F staff): normals only — no pranksters yet.
        if (!IsVipPhaseContentUnlocked())
            return;

        if (CountBrokenTables() >= BrokenTableSpawnBlockThreshold)
            return;

        if (IsVipPranksterAlternationEnabled())
        {
            if (!_awaitingPranksterSpawn)
                return;

            // Exclusive turn: never start a prankster while a VIP is present or being waited on.
            if (HasAnyNonLeavingVip())
                return;

            if (CustomerManager.Instance != null && CustomerManager.Instance.IsAwaitingVipForAlternation)
                return;

            _customersSinceVipLeft++;

            if (_customersSinceVipLeft < Mathf.Max(1, _customerSpawnInterval))
                return;

            _customersSinceVipLeft = 0;
            _awaitingPranksterSpawn = false;
            _visitRoutine = StartCoroutine(RunPranksterVisit());
            return;
        }

        _customersSinceLastVisit++;

        if (_customersSinceLastVisit < Mathf.Max(1, _customerSpawnInterval))
            return;

        _customersSinceLastVisit = 0;
        _visitRoutine = StartCoroutine(RunPranksterVisit());
    }

    private void HandleCustomerStateChanged(Customer customer, CustomerState state)
    {
        if (!IsVipPranksterAlternationEnabled())
            return;

        if (!IsVipPhaseContentUnlocked())
            return;

        if (_visitActive || _awaitingPranksterSpawn || !IsBusinessActive() || _prankster == null)
            return;

        if (state != CustomerState.Leaving || customer == null || !customer.IsVip)
            return;

        // Only schedule a prankster after the last VIP has left.
        if (HasAnyNonLeavingVip())
            return;

        CustomerManager.Instance?.NotifyVipLeftForAlternation();
        _customersSinceVipLeft = 0;
        _awaitingPranksterSpawn = true;
    }

    private static bool IsVipPhaseContentUnlocked()
    {
        return CustomerManager.Instance != null && CustomerManager.Instance.CanStartVipPhaseContent();
    }

    private bool IsVipPranksterAlternationEnabled()
    {
        return RestaurantSceneMode.IsMainScene;
    }

    private bool HasAnyNonLeavingVip()
    {
        if (_customerPool == null)
            _customerPool = FindFirstObjectByType<CustomerPool>();

        if (_customerPool == null)
            return false;

        IReadOnlyList<Customer> customers = _customerPool.ActiveCustomers;

        for (int i = 0; i < customers.Count; i++)
        {
            Customer customer = customers[i];

            if (customer == null)
                continue;

            if (!customer.IsVip)
                continue;

            if (customer.State != CustomerState.Leaving)
                return true;
        }

        return false;
    }

    public void RequestChaseAway()
    {
        if (!_visitActive || _chaseDismissed)
            return;

        _chaseDismissed = true;
        AudioManager.Play(SfxId.Unhappy);
        _prankster?.PlayKickAudio();
        UIManager.Instance?.PlayPranksterChasedAwayDialogue();

        if (_visitRoutine != null)
        {
            StopCoroutine(_visitRoutine);
            _visitRoutine = null;
        }

        _visitRoutine = StartCoroutine(LeaveImmediately());
    }

    private IEnumerator RunPranksterVisit()
    {
        _visitActive = true;
        _chaseDismissed = false;

        _prankster.PrepareForVisit();
        _prankster.WarpTo(_outerSpawnPoint.position);
        _prankster.gameObject.SetActive(true);

        yield return MovePrankster(_pranksterWaypoint.position);

        if (_chaseDismissed)
        {
            yield return LeaveImmediately();
            yield break;
        }

        _prankster.EnterStationary();
        float waitDuration = Mathf.Max(0f, _waitAtWaypointDuration);
        UIManager.Instance?.PlayPranksterArrivalDialogue(waitDuration);

        float elapsed = 0f;
        while (elapsed < waitDuration)
        {
            if (_chaseDismissed)
                break;

            UIManager.Instance?.UpdatePranksterWaitTimer(waitDuration - elapsed, waitDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        UIManager.Instance?.HidePranksterWaitTimer();

        if (_chaseDismissed)
        {
            yield return LeaveImmediately();
            yield break;
        }

        DiningTable targetTable = PickTableToBreak();

        if (targetTable != null)
        {
            yield return MovePrankster(targetTable.GetPranksterApproachPosition());

            if (_chaseDismissed)
            {
                yield return LeaveImmediately();
                yield break;
            }

            _prankster.EnterStationary();
            yield return new WaitForSeconds(_breakActionDuration);

            if (_chaseDismissed)
            {
                yield return LeaveImmediately();
                yield break;
            }

            targetTable.BreakByPrankster();
            AudioManager.Play(SfxId.PranksterLaugh);
            _chaseDismissed = true;
            UIManager.Instance?.PlayPranksterTableBrokenDialogue();

            if (CustomerManager.Instance != null)
                CustomerManager.Instance.ExpelNonVipCustomersFromTable(targetTable);
        }

        yield return LeaveImmediately();
    }

    private IEnumerator LeaveImmediately()
    {
        if (_prankster != null && _prankster.gameObject.activeSelf)
        {
            Vector3 leavePosition = _exitPoint != null ? _exitPoint.position : _outerSpawnPoint.position;
            yield return MovePrankster(leavePosition);
            _prankster.gameObject.SetActive(false);
        }

        UIManager.Instance?.HidePranksterDialogue();

        _visitActive = false;
        _visitRoutine = null;

        if (IsVipPranksterAlternationEnabled())
            CustomerManager.Instance?.NotifyPranksterVisitEndedForAlternation();
    }

    private IEnumerator MovePrankster(Vector3 destination)
    {
        _prankster.ExitStationary();
        yield return NavMeshMovement.MoveTo(_prankster.Locomotion, destination);
    }

    private DiningTable PickTableToBreak()
    {
        EnsureBuiltTablesReady();
        _tableBreakCandidates.Clear();

        for (int i = 0; i < _builtTables.Count; i++)
        {
            DiningTable table = _builtTables[i];

            if (!IsValidBreakTarget(table))
                continue;

            _tableBreakCandidates.Add(table);
        }

        if (_tableBreakCandidates.Count == 0)
            return null;

        return _tableBreakCandidates[Random.Range(0, _tableBreakCandidates.Count)];
    }

    private int CountBrokenTables()
    {
        EnsureBuiltTablesReady();
        int brokenCount = 0;

        for (int i = 0; i < _builtTables.Count; i++)
        {
            DiningTable table = _builtTables[i];

            if (table != null && table.IsBroken)
                brokenCount++;
        }

        return brokenCount;
    }

    private void EnsureBuiltTablesReady()
    {
        if (_builtTables.Count == 0)
            RebuildBuiltTablesFromSpots();
    }

    private void RebuildBuiltTablesFromSpots()
    {
        _builtTables.Clear();

        BuildSpot[] spots = FindObjectsByType<BuildSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < spots.Length; i++)
        {
            BuildSpot spot = spots[i];

            if (spot == null || !spot.IsBuilt || spot.PlaceableType != PlaceableType.Table)
                continue;

            TryAddBuiltTable(ResolveDiningTable(spot));
        }

        // Fallback: if spots are not ready yet, keep only active seeded tables.
        if (_builtTables.Count == 0 && _tables != null)
        {
            for (int i = 0; i < _tables.Length; i++)
            {
                DiningTable table = _tables[i];
                if (table != null && table.isActiveAndEnabled && table.gameObject.activeInHierarchy)
                    TryAddBuiltTable(table);
            }
        }
    }

    private void TryAddBuiltTable(DiningTable table)
    {
        if (table == null || table.IsVipTable || _builtTables.Contains(table))
            return;

        _builtTables.Add(table);
    }

    private void TryRemoveBuiltTable(DiningTable table)
    {
        if (table == null)
            return;

        _builtTables.Remove(table);
    }

    private static DiningTable ResolveDiningTable(BuildSpot spot)
    {
        if (spot == null || spot.BuiltObject == null)
            return null;

        DiningTable table = spot.BuiltObject.GetComponent<DiningTable>();
        if (table != null)
            return table;

        return spot.BuiltObject.GetComponentInChildren<DiningTable>(true);
    }

    private static bool IsValidBreakTarget(DiningTable table)
    {
        return table != null
            && table.isActiveAndEnabled
            && table.gameObject.activeInHierarchy
            && table.CanBeBrokenByPrankster
            && !table.HasVipOccupant();
    }

    private void CancelActiveVisit()
    {
        if (_visitRoutine != null)
        {
            StopCoroutine(_visitRoutine);
            _visitRoutine = null;
        }

        _visitActive = false;
        _chaseDismissed = true;

        if (_prankster != null)
            _prankster.gameObject.SetActive(false);
    }

    private void ResetVisitTracking()
    {
        _customersSinceLastVisit = 0;
        _customersSinceVipLeft = 0;
        _awaitingPranksterSpawn = false;
    }

    private static bool IsBusinessActive()
    {
        return GameManager.Instance != null && GameManager.Instance.IsBusiness;
    }

    private static Transform FindTransformByName(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }
}
