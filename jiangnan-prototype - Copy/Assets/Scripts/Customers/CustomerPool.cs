using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CustomerPool : MonoBehaviour
{
    [SerializeField] private Customer _prefab;
    [SerializeField] private Customer[] _vipPrefabs;
    [SerializeField] private int _prewarmCount;
    [SerializeField] private int _vipPrewarmCount;

    private readonly Dictionary<Customer, Stack<Customer>> _availableByPrefab = new();
    private readonly Dictionary<Customer, Customer> _prefabByInstance = new();
    private readonly List<Customer> _active = new();

    public IReadOnlyList<Customer> ActiveCustomers => _active;
    public bool HasVipPrefabs => _vipPrefabs != null && _vipPrefabs.Length > 0;

    private void Awake()
    {
        HideSceneTemplateCustomers();
        Prewarm(_prefab, _prewarmCount);

        if (_vipPrefabs == null)
            return;

        for (int i = 0; i < _vipPrefabs.Length; i++)
            Prewarm(_vipPrefabs[i], _vipPrewarmCount);
    }

    private void HideSceneTemplateCustomers()
    {
        // Scene templates under this pool are Instantiation sources only — keep them inactive.
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
                child.gameObject.SetActive(false);
        }

        // Normal customer prefab may be a scene object (not a project asset).
        if (_prefab != null && _prefab.gameObject.scene.IsValid())
            _prefab.gameObject.SetActive(false);
    }

    public Customer Get(Vector3 position)
    {
        return GetFromPrefab(_prefab, position, false);
    }

    public Customer GetVip(Vector3 position)
    {
        if (!HasVipPrefabs)
            return Get(position);

        Customer vipPrefab = _vipPrefabs[Random.Range(0, _vipPrefabs.Length)];

        if (vipPrefab == null)
            return Get(position);

        return GetFromPrefab(vipPrefab, position, true);
    }

    public void Release(Customer customer)
    {
        if (customer == null || !_prefabByInstance.TryGetValue(customer, out Customer prefab))
            return;

        _active.Remove(customer);
        customer.ResetForPool();
        customer.transform.SetParent(transform);
        GetOrCreateStack(prefab).Push(customer);
    }

    public void ReleaseAll()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
            Release(_active[i]);
    }

    private Customer GetFromPrefab(Customer prefab, Vector3 position, bool isVip)
    {
        if (prefab == null)
            return null;

        Stack<Customer> available = GetOrCreateStack(prefab);
        Customer customer = available.Count > 0 ? available.Pop() : CreateInstance(prefab);

        customer.transform.SetParent(transform);
        customer.gameObject.SetActive(true);
        customer.ResetForSpawn();
        customer.ConfigureForSpawn(isVip);
        customer.WarpTo(position);
        _active.Add(customer);
        return customer;
    }

    private void Prewarm(Customer prefab, int count)
    {
        if (prefab == null || count <= 0)
            return;

        Stack<Customer> available = GetOrCreateStack(prefab);

        for (int i = 0; i < count; i++)
            available.Push(CreateInstance(prefab));
    }

    private Customer CreateInstance(Customer prefab)
    {
        Customer customer = Instantiate(prefab, transform);
        customer.gameObject.SetActive(false);
        _prefabByInstance[customer] = prefab;
        return customer;
    }

    private Stack<Customer> GetOrCreateStack(Customer prefab)
    {
        if (!_availableByPrefab.TryGetValue(prefab, out Stack<Customer> available))
        {
            available = new Stack<Customer>();
            _availableByPrefab[prefab] = available;
        }

        return available;
    }
}
