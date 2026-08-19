using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NavMeshLocomotion))]
public class Customer : MonoBehaviour
{
    private const string LakePointName = "LaKePoint";

    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private NavMeshLocomotion _locomotion;
    [SerializeField] private Transform _reactPoint;
    [SerializeField] private Transform _lakePoint;

    private CustomerState _state = CustomerState.Queue;

    public NavMeshAgent Agent => _agent;
    public NavMeshLocomotion Locomotion => _locomotion;
    public CustomerState State => _state;
    public bool IsVip { get; private set; }
    public bool IgnoresVipCap { get; private set; }
    public bool IsImmuneToCompetitorSteal { get; private set; }
    public bool WasStolenByCompetitor { get; private set; }
    public int QueueSlotIndex { get; set; } = -1;
    public TableSeat Seat { get; set; }
    public Transform PendingPaymentAnchor { get; set; }
    public int PendingPaymentTableLevel { get; set; } = 1;
    public int VipEventBonus { get; set; }
    /// <summary>Shop catalog this VIP is craving this visit (null for normal customers).</summary>
    public ItemCatalog VipTastePreferredShop { get; set; }

    public Transform ReactPoint => _reactPoint != null ? _reactPoint : transform;

    public Transform LakePoint
    {
        get
        {
            if (_lakePoint != null)
                return _lakePoint;

            Transform found = FindChildTransform(transform, LakePointName);
            if (found != null)
                _lakePoint = found;

            return _lakePoint != null ? _lakePoint : transform;
        }
    }

    private void Awake()
    {
        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();

        if (_locomotion == null)
            _locomotion = GetComponent<NavMeshLocomotion>();

        if (_lakePoint == null)
            _lakePoint = FindChildTransform(transform, LakePointName);

        _locomotion.Configure();
    }

    private static Transform FindChildTransform(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == name)
                return children[i];
        }

        return null;
    }

    public void SetState(CustomerState state)
    {
        if (_state == state)
            return;

        _state = state;
        GameEvents.RaiseCustomerStateChanged(this, state);

        if (state == CustomerState.Ordering || state == CustomerState.Eating || state == CustomerState.Paying)
            _locomotion.EnterStationary();
        else if (state == CustomerState.Leaving || state == CustomerState.WalkingToSeat)
            _locomotion.ExitStationary();
    }

    public void ConfigureForSpawn(bool isVip)
    {
        IsVip = isVip;
        IgnoresVipCap = false;
        IsImmuneToCompetitorSteal = false;
        WasStolenByCompetitor = false;
        RestaurantFloorUtil.SyncActorFloorViewLayerByElevation(gameObject);
    }

    public void ConfigureVipProtection(bool ignoresVipCap, bool immuneToCompetitorSteal)
    {
        IgnoresVipCap = ignoresVipCap;
        IsImmuneToCompetitorSteal = immuneToCompetitorSteal;
    }

    public void MarkStolenByCompetitor()
    {
        WasStolenByCompetitor = true;
    }

    public void ResetForSpawn()
    {
        IsVip = false;
        IgnoresVipCap = false;
        IsImmuneToCompetitorSteal = false;
        WasStolenByCompetitor = false;
        QueueSlotIndex = -1;
        Seat = null;
        VipEventBonus = 0;
        VipTastePreferredShop = null;
        ClearPendingPayment();
        _state = CustomerState.Queue;
        _locomotion.Release();
        _locomotion.Configure();
        RestaurantFloorUtil.SetBelongsToSecondFloorView(gameObject, false);
    }

    public void WarpTo(Vector3 position)
    {
        _locomotion.ExitStationary();
        NavMeshMovement.TryWarp(_agent, position);
        NavMeshMovement.Stop(_agent);
    }

    public void StopMovement()
    {
        NavMeshMovement.Stop(_agent);
    }

    public void EnterStationary()
    {
        _locomotion.EnterStationary();
    }

    public void ResetForPool()
    {
        IsVip = false;
        IgnoresVipCap = false;
        IsImmuneToCompetitorSteal = false;
        WasStolenByCompetitor = false;
        QueueSlotIndex = -1;
        Seat = null;
        VipEventBonus = 0;
        VipTastePreferredShop = null;
        ClearPendingPayment();
        _locomotion.Release();
        RestaurantFloorUtil.SetBelongsToSecondFloorView(gameObject, false);
        gameObject.SetActive(false);
    }

    public void ClearPendingPayment()
    {
        PendingPaymentAnchor = null;
        PendingPaymentTableLevel = 1;
    }
}
