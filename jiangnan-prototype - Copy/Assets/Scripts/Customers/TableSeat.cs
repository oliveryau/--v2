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
        if (customer == null)
            return false;

        // Already held by this customer (e.g. reserved during queue hold before walking).
        if (_occupant == customer)
            return true;

        if (_occupant != null)
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
    /// Lv2 stool meshes have an off-center pivot; when sit point is the root, recenter onto the collider.
    /// </summary>
    public void EnsureMissingAnchors(Transform fallbackSitPoint, Transform fallbackPaymentAnchor)
    {
        if (NeedsCenteredSitPoint(out Vector3 centeredLocal))
            _sitPoint = EnsureCenteredSitPoint(centeredLocal, fallbackSitPoint);
        else if (_sitPoint == null)
            _sitPoint = fallbackSitPoint != null ? fallbackSitPoint : transform;

        if (_paymentUiAnchor == null)
            _paymentUiAnchor = fallbackPaymentAnchor;

        if (_parentTable == null)
            _parentTable = GetComponentInParent<DiningTable>();
    }

    private bool NeedsCenteredSitPoint(out Vector3 centeredLocal)
    {
        centeredLocal = Vector3.zero;

        // Keep explicit authored sit points (e.g. VIP Seat Point children).
        if (_sitPoint != null && _sitPoint != transform)
            return false;

        if (!TryResolveOffsetSitLocal(out centeredLocal))
            return false;

        return true;
    }

    private bool TryResolveOffsetSitLocal(out Vector3 local)
    {
        local = Vector3.zero;

        if (GetComponent<Collider>() is BoxCollider boxCollider)
        {
            local = boxCollider.center;
            local.y += boxCollider.size.y * 0.35f;

            // Only recenter when the mesh pivot is meaningfully off the visual center.
            Vector3 horizontal = new Vector3(boxCollider.center.x, 0f, boxCollider.center.z);
            Vector3 worldHorizontal = transform.TransformVector(horizontal);
            return worldHorizontal.sqrMagnitude >= 0.0025f; // ~5cm
        }

        return false;
    }

    private Transform EnsureCenteredSitPoint(Vector3 localPosition, Transform fallbackSitPoint)
    {
        Transform existing = transform.Find("Seat Point");
        if (existing == null)
            existing = transform.Find("Sit Point");

        if (existing != null)
        {
            existing.localPosition = localPosition;
            return existing;
        }

        GameObject seatPointObject = new GameObject("Seat Point");
        Transform seatPoint = seatPointObject.transform;
        seatPoint.SetParent(transform, false);
        seatPoint.localRotation = Quaternion.identity;
        seatPoint.localScale = Vector3.one;
        seatPoint.localPosition = localPosition;

        if (localPosition == Vector3.zero
            && fallbackSitPoint != null
            && fallbackSitPoint != transform)
        {
            seatPoint.position = fallbackSitPoint.position;
        }

        return seatPoint;
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
