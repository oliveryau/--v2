using UnityEngine;

[DisallowMultipleComponent]
public class TableSeat : MonoBehaviour
{
    [SerializeField] private Transform _sitPoint;
    [SerializeField] private Transform _paymentUiAnchor;

    private Customer _occupant;
    private DiningTable _parentTable;

    public bool IsOccupied => _occupant != null;
    public DiningTable ParentTable => _parentTable;
    public Customer Occupant => _occupant;
    public Vector3 Position => _sitPoint != null ? _sitPoint.position : transform.position;
    public Vector3 WalkDestination => NavMeshMovement.ResolveReachablePosition(Position);
    public Quaternion Rotation => _sitPoint != null ? _sitPoint.rotation : transform.rotation;
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

    public void Configure(Transform sitPoint, Transform paymentAnchor)
    {
        _sitPoint = sitPoint;
        _paymentUiAnchor = paymentAnchor;

        if (_parentTable == null)
            _parentTable = GetComponentInParent<DiningTable>();
    }

    public void RestoreOccupant(Customer customer)
    {
        _occupant = customer;

        if (customer != null)
            customer.Seat = this;
    }
}
