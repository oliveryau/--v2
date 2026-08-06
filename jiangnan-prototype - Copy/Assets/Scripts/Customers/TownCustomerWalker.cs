using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(120)]
public class TownCustomerWalker : MonoBehaviour
{
    [SerializeField] private CustomerPool _customerPool;
    [SerializeField] private Transform _waypointsRoot;
    [SerializeField] private int _maxActiveCustomers;
    [SerializeField] private float _respawnDelay;

    private readonly List<Transform> _waypoints = new();
    private readonly List<Coroutine> _walkerLoops = new();

    private void Awake()
    {
        if (!RestaurantSceneMode.IsTownScene)
        {
            enabled = false;
            return;
        }

        if (_customerPool == null)
            _customerPool = GetComponent<CustomerPool>();
    }

    private void OnEnable()
    {
        if (!RestaurantSceneMode.IsTownScene)
            return;

        CacheWaypoints();
        StartWalkerLoops();
    }

    private void OnDisable()
    {
        for (int i = _walkerLoops.Count - 1; i >= 0; i--)
        {
            if (_walkerLoops[i] != null)
                StopCoroutine(_walkerLoops[i]);
        }

        _walkerLoops.Clear();

        if (_customerPool != null)
            _customerPool.ReleaseAll();
    }

    private void CacheWaypoints()
    {
        _waypoints.Clear();

        if (_waypointsRoot == null)
        {
            GameObject waypointsObject = GameObject.Find("Waypoints");

            if (waypointsObject != null)
                _waypointsRoot = waypointsObject.transform;
        }

        if (_waypointsRoot == null)
            return;

        for (int i = 0; i < _waypointsRoot.childCount; i++)
        {
            Transform waypoint = _waypointsRoot.GetChild(i);

            if (waypoint != null && waypoint.gameObject.activeInHierarchy)
                _waypoints.Add(waypoint);
        }
    }

    private void StartWalkerLoops()
    {
        if (_customerPool == null || _maxActiveCustomers <= 0 || _waypoints.Count < 2)
            return;

        for (int i = 0; i < _maxActiveCustomers; i++)
            _walkerLoops.Add(StartCoroutine(RunWalkerLoop()));
    }

    private IEnumerator RunWalkerLoop()
    {
        while (enabled)
        {
            if (_waypoints.Count < 2)
            {
                yield return null;
                continue;
            }

            if (!TryPickWaypointPair(out Transform startWaypoint, out Transform endWaypoint))
            {
                yield return null;
                continue;
            }

            Customer customer = _customerPool.Get(startWaypoint.position);

            if (customer == null)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            customer.SetState(CustomerState.WalkingToSeat);
            yield return NavMeshMovement.MoveTo(customer.Locomotion, endWaypoint.position);

            _customerPool.Release(customer);

            if (_respawnDelay > 0f)
                yield return new WaitForSeconds(_respawnDelay);
        }
    }

    private bool TryPickWaypointPair(out Transform startWaypoint, out Transform endWaypoint)
    {
        startWaypoint = null;
        endWaypoint = null;

        if (_waypoints.Count < 2)
            return false;

        int startIndex = Random.Range(0, _waypoints.Count);
        int endIndex = startIndex;

        while (endIndex == startIndex)
            endIndex = Random.Range(0, _waypoints.Count);

        startWaypoint = _waypoints[startIndex];
        endWaypoint = _waypoints[endIndex];
        return startWaypoint != null && endWaypoint != null;
    }
}
