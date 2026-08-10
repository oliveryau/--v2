using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class TableUpgradeDelivery : MonoBehaviour
{
    public static TableUpgradeDelivery Instance { get; private set; }

    [SerializeField] private GameObject _carrierWorkerTable1;
    [SerializeField] private GameObject _carrierWorkerTable2;
    [SerializeField] private GameObject _carrierWorkerTable3;
    [SerializeField] private Transform _deliverySpawn;
    [SerializeField] private Transform[] _deliveryCheckpoints;
    [SerializeField] private float _deliveryDuration;
    [Header("Completion VFX")]
    [SerializeField] private GameObject _completionVfxPrefab;
    [SerializeField] private float _completionVfxLifetime = 3f;

    private bool _isDelivering;
    private GameObject _equipmentTableLevel2Template;
    private GameObject _equipmentTableLevel3Template;

    public bool IsDelivering => _isDelivering;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_carrierWorkerTable1 != null)
            _carrierWorkerTable1.SetActive(false);

        if (_carrierWorkerTable2 != null)
            _carrierWorkerTable2.SetActive(false);

        if (_carrierWorkerTable3 != null)
            _carrierWorkerTable3.SetActive(false);

        CacheEquipmentTableTemplates();
    }

    public GameObject GetEquipmentTableTemplate(int level)
    {
        return level switch
        {
            2 => _equipmentTableLevel2Template,
            3 => _equipmentTableLevel3Template,
            _ => null
        };
    }

    private void CacheEquipmentTableTemplates()
    {
        _equipmentTableLevel2Template = FindEquipmentTableTemplate(_carrierWorkerTable2);
        _equipmentTableLevel3Template = FindEquipmentTableTemplate(_carrierWorkerTable3);
    }

    private static GameObject FindEquipmentTableTemplate(GameObject carrierGroup)
    {
        if (carrierGroup == null)
            return null;

        Transform carrierTransform = carrierGroup.transform;
        GameObject fallback = null;

        for (int i = 0; i < carrierTransform.childCount; i++)
        {
            Transform child = carrierTransform.GetChild(i);

            if (child == null || !child.name.StartsWith("P_Equipment_Table", System.StringComparison.Ordinal))
                continue;

            // Prefer the matching level prop when several equipment tables are nested under the carrier.
            if (carrierGroup.name.IndexOf("Table_2", System.StringComparison.OrdinalIgnoreCase) >= 0
                && child.name.IndexOf("Lv2", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child.gameObject;
            }

            if (carrierGroup.name.IndexOf("Table_3", System.StringComparison.OrdinalIgnoreCase) >= 0
                && child.name.IndexOf("Lv3", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return child.gameObject;
            }

            if (fallback == null)
                fallback = child.gameObject;
        }

        return fallback;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public IEnumerator DeliverRepair(Vector3 deliveryTarget)
    {
        yield return DeliverCarrier(_carrierWorkerTable1, deliveryTarget);
    }

    public IEnumerator DeliverUpgrade(int targetLevel, Vector3 deliveryTarget)
    {
        GameObject carrierGroup = targetLevel switch
        {
            2 => _carrierWorkerTable2,
            3 => _carrierWorkerTable3,
            _ => null
        };

        yield return DeliverCarrier(carrierGroup, deliveryTarget);
    }

    private IEnumerator DeliverCarrier(GameObject carrierGroup, Vector3 deliveryTarget)
    {
        if (carrierGroup == null)
            yield break;

        _isDelivering = true;

        Transform carrierTransform = carrierGroup.transform;
        Vector3 spawnPosition = _deliverySpawn != null
            ? _deliverySpawn.position
            : carrierTransform.position;
        Vector3[] checkpoints = PathMovement.BuildCheckpoints(_deliveryCheckpoints, spawnPosition.y);
        Vector3 targetPosition = PathMovement.FlattenToFloorY(deliveryTarget, spawnPosition.y);
        Vector3[] waypoints = PathMovement.BuildWaypoints(checkpoints, targetPosition);

        carrierTransform.position = spawnPosition;
        carrierGroup.SetActive(true);
        RuntimeMeshVisibility.PrepareHierarchyForRuntimeMove(carrierTransform);

        yield return PathMovement.Move(
            carrierTransform,
            spawnPosition,
            waypoints,
            Mathf.Max(0.01f, _deliveryDuration));

        carrierGroup.SetActive(false);
        _isDelivering = false;
    }

    public void PlayCompletionVfx(Transform vfxPoint)
    {
        BuildCompletionVfx.Play(_completionVfxPrefab, vfxPoint, _completionVfxLifetime);
    }
}
