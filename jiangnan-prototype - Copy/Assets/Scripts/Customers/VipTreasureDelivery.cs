using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class VipTreasureDelivery : MonoBehaviour
{
    public static VipTreasureDelivery Instance { get; private set; }

    private const string CarrierName = "CarrierWorker_Treasure";
    private const string WaypointName = "VIP TreasureChest Waypoint";

    [SerializeField] private GameObject _carrierWorkerTreasure;
    [SerializeField] private Transform _treasureChestWaypoint;
    [SerializeField] private float _deliveryDuration = 2.5f;

    private Vector3 _carrierSpawnPosition;
    private bool _hasCarrierSpawnPosition;
    private Coroutine _deliveryRoutine;

    public bool IsDelivering => _deliveryRoutine != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        CacheReferences();

        if (_carrierWorkerTreasure != null)
        {
            _carrierSpawnPosition = _carrierWorkerTreasure.transform.position;
            _hasCarrierSpawnPosition = true;
            _carrierWorkerTreasure.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void PlayDelivery()
    {
        CacheReferences();

        if (_carrierWorkerTreasure == null || _treasureChestWaypoint == null)
            return;

        if (_deliveryRoutine != null)
            StopCoroutine(_deliveryRoutine);

        _deliveryRoutine = StartCoroutine(DeliveryRoutine());
    }

    private IEnumerator DeliveryRoutine()
    {
        Transform carrierTransform = _carrierWorkerTreasure.transform;
        Vector3 spawnPosition = _hasCarrierSpawnPosition
            ? _carrierSpawnPosition
            : carrierTransform.position;
        Vector3 targetPosition = PathMovement.FlattenToFloorY(
            _treasureChestWaypoint.position,
            spawnPosition.y);
        Vector3[] waypoints = PathMovement.BuildWaypoints(System.Array.Empty<Vector3>(), targetPosition);

        carrierTransform.position = spawnPosition;
        _carrierWorkerTreasure.SetActive(true);
        RuntimeMeshVisibility.PrepareHierarchyForRuntimeMove(carrierTransform);

        yield return PathMovement.Move(
            carrierTransform,
            spawnPosition,
            waypoints,
            Mathf.Max(0.01f, _deliveryDuration));

        carrierTransform.position = targetPosition;
        _deliveryRoutine = null;
    }

    private void CacheReferences()
    {
        if (_carrierWorkerTreasure == null)
        {
            Transform found = FindSceneTransformByName(CarrierName);
            if (found != null)
                _carrierWorkerTreasure = found.gameObject;
        }

        if (_treasureChestWaypoint == null)
            _treasureChestWaypoint = FindSceneTransformByName(WaypointName);
    }

    private static Transform FindSceneTransformByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == objectName)
                return transforms[i];
        }

        return null;
    }
}
