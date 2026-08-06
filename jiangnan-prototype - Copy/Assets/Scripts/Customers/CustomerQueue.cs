using UnityEngine;

[DisallowMultipleComponent]
public class CustomerQueue : MonoBehaviour
{
    [SerializeField] private Transform[] _slots;

    private Customer[] _occupants;

    public bool IsFull => FindFirstFreeSlot() < 0;

    private void Awake()
    {
        int slotCount = _slots != null ? _slots.Length : 0;
        _occupants = new Customer[slotCount];
    }

    public bool TryAssign(Customer customer, out int slotIndex)
    {
        slotIndex = FindFirstFreeSlot();

        if (slotIndex < 0)
            return false;

        _occupants[slotIndex] = customer;
        customer.QueueSlotIndex = slotIndex;
        return true;
    }

    public void Release(int slotIndex)
    {
        if (_occupants == null || slotIndex < 0 || slotIndex >= _occupants.Length)
            return;

        _occupants[slotIndex] = null;
    }

    public Vector3 GetSlotPosition(int slotIndex)
    {
        if (_slots == null || slotIndex < 0 || slotIndex >= _slots.Length || _slots[slotIndex] == null)
            return transform.position;

        return _slots[slotIndex].position;
    }

    private int FindFirstFreeSlot()
    {
        if (_occupants == null)
            return -1;

        for (int i = 0; i < _occupants.Length; i++)
        {
            if (_occupants[i] == null)
                return i;
        }

        return -1;
    }
}
