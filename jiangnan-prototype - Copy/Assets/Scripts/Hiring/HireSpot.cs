using System;
using UnityEngine;

[DisallowMultipleComponent]
public class HireSpot : MonoBehaviour
{
    [SerializeField] private WorkerType _workerType;
    [SerializeField] private int _cost;
    [SerializeField] private Transform _hireUiAnchor;
    [SerializeField] private RestaurantFloor _floor = RestaurantFloor.Ground;

    [Header("Worker Walk-In")]
    [SerializeField] private GameObject[] _workers;
    [SerializeField] private Transform _walkInSpawn;
    [SerializeField] private Transform[] _walkInEndpoints;
    [SerializeField] private float _walkInSpawnInterval;

    private HireSpotState _state = HireSpotState.Locked;
    private Vector3[] _fallbackTargetPositions;
    private Quaternion[] _fallbackTargetRotations;

    public WorkerType WorkerType => _workerType;
    public HireSpotState State => _state;
    public int Cost => _cost;
    /// <summary>
    /// Floor for gating/visibility. Prefer the world hire anchor — ground hire buttons may live on
    /// the Overlay canvas until activated, and Overlay screen-space Y must not count as upstairs.
    /// </summary>
    public RestaurantFloor Floor => _hireUiAnchor != null
        ? RestaurantFloorUtil.ResolveFloor(_hireUiAnchor, _floor)
        : _floor;
    public bool IsHired => _state == HireSpotState.Hired;
    public Transform HireUiAnchor => _hireUiAnchor != null ? _hireUiAnchor : transform;
    public GameObject[] Workers => _workers;
    public Transform WalkInSpawn => _walkInSpawn;
    public Transform[] WalkInEndpoints => _walkInEndpoints;
    public float WalkInSpawnInterval => _walkInSpawnInterval;

    public event Action<HireSpot> Clicked;
    public event Action<HireSpot> HireCompleted;

    private void Awake()
    {
        CacheFallbackTargets();
    }

    public void SetState(HireSpotState state)
    {
        if (_state == state)
            return;

        _state = state;
        GameEvents.RaiseHireSpotStateChanged(this, state);
    }

    public void ActivateForHiring()
    {
        if (_state == HireSpotState.Active)
        {
            GameEvents.RaiseHireSpotStateChanged(this, _state);
            return;
        }

        SetState(HireSpotState.Active);
    }

    public void NotifyClicked()
    {
        if (_state != HireSpotState.Active)
            return;

        Clicked?.Invoke(this);
    }

    public void BeginHire()
    {
        if (_state != HireSpotState.Active)
            return;

        if (WorkerMovement.Instance == null)
        {
            Debug.LogError("WorkerMovement is missing. Add it to Build Manager.", this);
            return;
        }

        SetState(HireSpotState.Hiring);
        WorkerMovement.Instance.BeginWalkIn(this, CompleteHire);
    }

    public void AssignWalkInTargets(float floorY, Vector3[] positions, Quaternion[] rotations)
    {
        bool[] endpointOccupied = new bool[_walkInEndpoints != null ? _walkInEndpoints.Length : 0];

        for (int i = 0; i < _workers.Length; i++)
        {
            if (_workers[i] == null)
                continue;

            int endpointIndex = FindFirstFreeEndpoint(endpointOccupied);

            if (endpointIndex >= 0)
            {
                endpointOccupied[endpointIndex] = true;
                Transform endpoint = _walkInEndpoints[endpointIndex];
                // Keep each endpoint's authored Y so upstairs wait points are not snapped to the ground spawn.
                float targetFloorY = ResolveWalkInFloorY(endpoint.position.y, floorY);
                positions[i] = PathMovement.FlattenToFloorY(endpoint.position, targetFloorY);
                rotations[i] = endpoint.rotation;
                continue;
            }

            Vector3 fallbackPosition = GetFallbackPosition(i);
            float fallbackFloorY = ResolveWalkInFloorY(fallbackPosition.y, floorY);
            positions[i] = PathMovement.FlattenToFloorY(fallbackPosition, fallbackFloorY);
            rotations[i] = GetFallbackRotation(i);
        }
    }

    private void CompleteHire()
    {
        SetState(HireSpotState.Hired);
        RegisterWorkers();
        HireCompleted?.Invoke(this);
    }

    public void RestoreHiredState()
    {
        if (IsHired)
        {
            EnsureWorkersVisibleAtWorkPositions();
            RegisterWorkers();
            return;
        }

        SetState(HireSpotState.Hired);
        EnsureWorkersVisibleAtWorkPositions();
        RegisterWorkers();
    }

    private void EnsureWorkersVisibleAtWorkPositions()
    {
        if (_workers == null || _workers.Length == 0)
            return;

        Vector3 spawnPosition = _walkInSpawn != null
            ? _walkInSpawn.position
            : transform.position;

        Vector3[] targetPositions = new Vector3[_workers.Length];
        Quaternion[] targetRotations = new Quaternion[_workers.Length];
        AssignWalkInTargets(spawnPosition.y, targetPositions, targetRotations);

        for (int i = 0; i < _workers.Length; i++)
        {
            GameObject workerObject = _workers[i];

            if (workerObject == null)
                continue;

            Worker worker = workerObject.GetComponent<Worker>();
            workerObject.SetActive(true);

            if (worker == null)
                continue;

            worker.WarpTo(targetPositions[i]);
            worker.FaceDirection(targetRotations[i]);
            worker.Locomotion?.EnterStationary();
        }
    }

    private void RegisterWorkers()
    {
        if (WorkerManager.Instance == null || _workers == null)
            return;

        for (int i = 0; i < _workers.Length; i++)
        {
            if (_workers[i] == null)
                continue;

            Worker worker = _workers[i].GetComponent<Worker>();

            if (worker != null)
                WorkerManager.Instance.RegisterWorker(worker);
        }
    }

    private int FindFirstFreeEndpoint(bool[] endpointOccupied)
    {
        if (_walkInEndpoints == null)
            return -1;

        for (int i = 0; i < _walkInEndpoints.Length; i++)
        {
            if (_walkInEndpoints[i] == null || endpointOccupied[i])
                continue;

            return i;
        }

        return -1;
    }

    private float ResolveWalkInFloorY(float endpointY, float spawnFloorY)
    {
        // Second-floor hires use the endpoint height (e.g. FemaleWaiter / Waiter3 Waypoint at y≈4).
        if (Floor == RestaurantFloor.Second)
            return endpointY;

        return spawnFloorY;
    }

    private Vector3 GetFallbackPosition(int workerIndex)
    {
        if (_fallbackTargetPositions != null && workerIndex < _fallbackTargetPositions.Length)
            return _fallbackTargetPositions[workerIndex];

        return transform.position;
    }

    private Quaternion GetFallbackRotation(int workerIndex)
    {
        if (_fallbackTargetRotations != null && workerIndex < _fallbackTargetRotations.Length)
            return _fallbackTargetRotations[workerIndex];

        return Quaternion.identity;
    }

    private void CacheFallbackTargets()
    {
        if (_workers == null || _workers.Length == 0)
        {
            _fallbackTargetPositions = Array.Empty<Vector3>();
            _fallbackTargetRotations = Array.Empty<Quaternion>();
            return;
        }

        _fallbackTargetPositions = new Vector3[_workers.Length];
        _fallbackTargetRotations = new Quaternion[_workers.Length];

        for (int i = 0; i < _workers.Length; i++)
        {
            if (_workers[i] == null)
                continue;

            _fallbackTargetPositions[i] = _workers[i].transform.position;
            _fallbackTargetRotations[i] = _workers[i].transform.rotation;
        }
    }
}
