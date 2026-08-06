using UnityEngine;

[DisallowMultipleComponent]
public class StoveStation : MonoBehaviour
{
    [SerializeField] private Transform[] _pickupPoints;

    private Worker[] _occupants;

    public int PickupPointCount => _pickupPoints != null ? _pickupPoints.Length : 0;
    public bool HasAvailablePickup => FindFirstFreePickupIndex() >= 0;

    private void Awake()
    {
        int count = PickupPointCount;
        _occupants = count > 0 ? new Worker[count] : System.Array.Empty<Worker>();
    }

    public bool TryReservePickup(Worker worker, out int pickupIndex)
    {
        pickupIndex = -1;

        if (worker == null || _pickupPoints == null || _pickupPoints.Length == 0)
            return false;

        pickupIndex = FindFirstFreePickupIndex();

        if (pickupIndex < 0)
            return false;

        _occupants[pickupIndex] = worker;
        return true;
    }

    public void ReleasePickup(Worker worker)
    {
        if (worker == null || _occupants == null)
            return;

        for (int i = 0; i < _occupants.Length; i++)
        {
            if (_occupants[i] == worker)
                _occupants[i] = null;
        }
    }

    public void ReleaseAllPickups()
    {
        if (_occupants == null)
            return;

        for (int i = 0; i < _occupants.Length; i++)
            _occupants[i] = null;
    }

    public Vector3 GetPickupPosition(int pickupIndex)
    {
        Transform point = GetPickupPoint(pickupIndex);
        return point != null ? point.position : transform.position;
    }

    public Quaternion GetPickupRotation(int pickupIndex)
    {
        Transform point = GetPickupPoint(pickupIndex);
        return point != null ? point.rotation : transform.rotation;
    }

    private int FindFirstFreePickupIndex()
    {
        if (_pickupPoints == null)
            return -1;

        for (int i = 0; i < _pickupPoints.Length; i++)
        {
            if (_pickupPoints[i] == null || _occupants[i] != null)
                continue;

            return i;
        }

        return -1;
    }

    private Transform GetPickupPoint(int pickupIndex)
    {
        if (_pickupPoints == null || pickupIndex < 0 || pickupIndex >= _pickupPoints.Length)
            return null;

        return _pickupPoints[pickupIndex];
    }
}
