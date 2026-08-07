using UnityEngine;

[DisallowMultipleComponent]
public class TableSeat : MonoBehaviour
{
    [SerializeField] private Transform _sitPoint;
    [SerializeField] private Transform _paymentUiAnchor;

    private Customer _occupant;
    private DiningTable _parentTable;

    public bool IsOccupied => _occupant != null;
    public DiningTable ParentTable
    {
        get
        {
            if (_parentTable == null)
                _parentTable = GetComponentInParent<DiningTable>();

            return _parentTable;
        }
    }
    public Customer Occupant => _occupant;
    public Vector3 Position => _sitPoint != null ? _sitPoint.position : transform.position;
    public Vector3 WalkDestination => NavMeshMovement.ResolveReachablePosition(Position);
    public Quaternion Rotation => ResolveFacingRotation();
    public Transform PaymentUiAnchor => _paymentUiAnchor != null ? _paymentUiAnchor : transform;

    public bool TryReserve(Customer customer)
    {
        if (_occupant != null || customer == null)
            return false;

        _occupant = customer;
        customer.Seat = this;
        return true;
    }

    public void Release()
    {
        if (_occupant == null)
            return;

        _occupant.Seat = null;
        _occupant = null;
    }

    public Customer DetachOccupantForReplacement()
    {
        Customer customer = _occupant;

        if (customer != null)
            customer.Seat = null;

        _occupant = null;
        return customer;
    }

    private void Awake()
    {
        _parentTable = GetComponentInParent<DiningTable>();
    }

    /// <summary>
    /// Fills missing sit/payment refs only. Inspector-authored VIP anchors are left alone.
    /// </summary>
    public void EnsureMissingAnchors(Transform fallbackSitPoint, Transform fallbackPaymentAnchor)
    {
        if (_sitPoint == null)
            _sitPoint = fallbackSitPoint;

        if (_paymentUiAnchor == null)
            _paymentUiAnchor = fallbackPaymentAnchor;

        if (_parentTable == null)
            _parentTable = GetComponentInParent<DiningTable>();
    }

    public void RestoreOccupant(Customer customer)
    {
        _occupant = customer;

        if (customer != null)
            customer.Seat = this;
    }

    private Quaternion ResolveFacingRotation()
    {
        Vector3 sitPosition = Position;
        DiningTable table = ParentTable;

        if (table != null)
        {
            Vector3 toTable = table.transform.position - sitPosition;
            toTable.y = 0f;

            if (toTable.sqrMagnitude > 0.0001f)
                return Quaternion.LookRotation(toTable.normalized, Vector3.up);
        }

        Transform point = _sitPoint != null ? _sitPoint : transform;
        Vector3 forward = point.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude > 0.0001f)
            return Quaternion.LookRotation(forward.normalized, Vector3.up);

        return Quaternion.identity;
    }
}
