using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class VipTreasureDelivery : MonoBehaviour
{
    public static VipTreasureDelivery Instance { get; private set; }

    private const string CarrierName = "CarrierWorker_Treasure";
    private const string TreasureBoxName = "Treasure Box";
    private const string WaypointName = "VIP TreasureChest Waypoint";
    private const string CoinPointName = "Coin Point";
    private const string CoinBurstVfxName = "Coin Burst VFX";
    private const string OpenStateName = "Open";
    private const string ClosedStateName = "Closed";

    [SerializeField] private GameObject _carrierWorkerTreasure;
    [SerializeField] private Transform _treasureChestWaypoint;
    [SerializeField] private Transform _coinPoint;
    [SerializeField] private ParticleSystem _coinBurstVfx;
    [SerializeField] private Animator _treasureAnimator;
    [Tooltip("Animator trigger played when the carrier reaches the waypoint.")]
    [SerializeField] private string _openTrigger = "open";
    [Tooltip("Animator trigger played when the treasure hides.")]
    [SerializeField] private string _closeTrigger = "close";
    [SerializeField] private float _deliveryDuration = 1.5f;
    [Tooltip("How long the opened treasure stays visible before despawning.")]
    [SerializeField] private float _openHoldDuration = 4f;
    [Tooltip("If true, coin trail waits for OnTreasureBoxOpened() (AnimationEvent). If false, trail starts when open is triggered.")]
    [SerializeField] private bool _waitForOpenAnimationEvent;

    private Vector3 _carrierSpawnPosition;
    private Quaternion _carrierSpawnRotation;
    private bool _hasCarrierSpawnPosition;
    private Coroutine _deliveryRoutine;
    private bool _openAnimEventReceived;
    private bool _coinTrailPlayed;
    private int _pendingGoldAward;

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
            _carrierSpawnRotation = _carrierWorkerTreasure.transform.rotation;
            _hasCarrierSpawnPosition = true;
            _carrierWorkerTreasure.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void PlayDelivery(int pendingGoldAward = 0)
    {
        CacheReferences();

        if (_deliveryRoutine != null)
        {
            StopCoroutine(_deliveryRoutine);
            _deliveryRoutine = null;
            // Don't lose gold if a previous delivery was interrupted mid-route.
            GrantPendingGoldAward();
        }

        _pendingGoldAward = Mathf.Max(0, pendingGoldAward);

        if (_carrierWorkerTreasure == null || _treasureChestWaypoint == null)
        {
            GrantPendingGoldAward();
            return;
        }

        _deliveryRoutine = StartCoroutine(DeliveryRoutine());
    }

    /// <summary>
    /// Optional AnimationEvent on the treasure open clip — fires the VIP coin trail.
    /// </summary>
    public void OnTreasureBoxOpened()
    {
        _openAnimEventReceived = true;
        PlayCoinTrailFromTreasure();
    }

    private IEnumerator DeliveryRoutine()
    {
        _openAnimEventReceived = false;
        _coinTrailPlayed = false;

        Transform carrierTransform = _carrierWorkerTreasure.transform;
        Vector3 spawnPosition = _hasCarrierSpawnPosition
            ? _carrierSpawnPosition
            : carrierTransform.position;
        Quaternion spawnRotation = _hasCarrierSpawnPosition
            ? _carrierSpawnRotation
            : carrierTransform.rotation;
        Vector3 targetPosition = PathMovement.FlattenToFloorY(
            _treasureChestWaypoint.position,
            spawnPosition.y);
        Vector3[] waypoints = PathMovement.BuildWaypoints(System.Array.Empty<Vector3>(), targetPosition);

        carrierTransform.SetPositionAndRotation(spawnPosition, spawnRotation);
        _carrierWorkerTreasure.SetActive(true);
        ResetTreasureTriggers();
        StopCoinBurstVfx();
        RuntimeMeshVisibility.PrepareHierarchyForRuntimeMove(carrierTransform);

        yield return PathMovement.Move(
            carrierTransform,
            spawnPosition,
            waypoints,
            Mathf.Max(0.01f, _deliveryDuration));

        carrierTransform.position = targetPosition;
        if (_treasureChestWaypoint != null)
            carrierTransform.rotation = _treasureChestWaypoint.rotation;

        TryPlayOpenAnimation();

        if (!_waitForOpenAnimationEvent)
            PlayCoinTrailFromTreasure();
        else
        {
            // Fallback if the open clip has no AnimationEvent yet.
            float waitForEvent = 0f;
            const float maxEventWait = 1.5f;
            while (!_openAnimEventReceived && waitForEvent < maxEventWait)
            {
                waitForEvent += Time.deltaTime;
                yield return null;
            }

            if (!_coinTrailPlayed)
                PlayCoinTrailFromTreasure();
        }

        float hold = Mathf.Max(0f, _openHoldDuration);
        if (hold > 0f)
            yield return new WaitForSeconds(hold);

        // Safety: never lose VIP payment if the open/trail path was skipped.
        GrantPendingGoldAward();

        PlayTreasureClosed();
        yield return null;

        carrierTransform.SetPositionAndRotation(spawnPosition, spawnRotation);
        _carrierWorkerTreasure.SetActive(false);
        _deliveryRoutine = null;
    }

    private void TryPlayOpenAnimation()
    {
        PlayTreasureState(_openTrigger, OpenStateName, resetTrigger: _closeTrigger);
    }

    private void PlayTreasureClosed()
    {
        PlayTreasureState(_closeTrigger, ClosedStateName, resetTrigger: _openTrigger);
    }

    private void ResetTreasureTriggers()
    {
        EnsureTreasureAnimator();
        if (_treasureAnimator == null)
            return;

        if (HasAnimatorTrigger(_treasureAnimator, _openTrigger))
            _treasureAnimator.ResetTrigger(_openTrigger);

        if (HasAnimatorTrigger(_treasureAnimator, _closeTrigger))
            _treasureAnimator.ResetTrigger(_closeTrigger);
    }

    private void PlayTreasureState(string triggerName, string stateName, string resetTrigger)
    {
        EnsureTreasureAnimator();
        if (_treasureAnimator == null)
            return;

        if (!string.IsNullOrEmpty(resetTrigger) && HasAnimatorTrigger(_treasureAnimator, resetTrigger))
            _treasureAnimator.ResetTrigger(resetTrigger);

        if (!string.IsNullOrEmpty(triggerName) && HasAnimatorTrigger(_treasureAnimator, triggerName))
        {
            _treasureAnimator.SetTrigger(triggerName);
            return;
        }

        if (!string.IsNullOrEmpty(stateName))
            _treasureAnimator.Play(stateName, 0, 0f);
    }

    private void EnsureTreasureAnimator()
    {
        if (_treasureAnimator != null)
            return;

        if (_carrierWorkerTreasure == null)
            return;

        Transform box = FindChildTransformByName(_carrierWorkerTreasure.transform, TreasureBoxName);
        if (box != null)
            _treasureAnimator = box.GetComponent<Animator>();

        if (_treasureAnimator == null)
            _treasureAnimator = _carrierWorkerTreasure.GetComponentInChildren<Animator>(true);
    }

    private static bool HasAnimatorTrigger(Animator animator, string triggerName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Trigger
                && parameter.name == triggerName)
            {
                return true;
            }
        }

        return false;
    }

    private void PlayCoinTrailFromTreasure()
    {
        if (_coinTrailPlayed)
            return;

        CacheReferences();
        Transform trailStart = _coinPoint != null
            ? _coinPoint
            : (_carrierWorkerTreasure != null ? _carrierWorkerTreasure.transform : null);

        if (trailStart == null)
            return;

        _coinTrailPlayed = true;
        // Award gold here so the plus-icon feedback lines up with the chest opening.
        GrantPendingGoldAward();
        PlayCoinBurstVfx();
        AudioManager.Play(SfxId.GoldCollect);
        AudioManager.Play(SfxId.BigCoins);
        UIManager.Instance?.PlayVipTreasureCoinTrail(trailStart);
    }

    private void PlayCoinBurstVfx()
    {
        if (_coinBurstVfx == null)
            return;

        if (!_coinBurstVfx.gameObject.activeSelf)
            _coinBurstVfx.gameObject.SetActive(true);

        _coinBurstVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _coinBurstVfx.Play(true);
    }

    private void StopCoinBurstVfx()
    {
        if (_coinBurstVfx == null)
            return;

        _coinBurstVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void GrantPendingGoldAward()
    {
        if (_pendingGoldAward <= 0)
            return;

        int amount = _pendingGoldAward;
        _pendingGoldAward = 0;

        if (GoldManager.Instance != null)
            GoldManager.Instance.AddGold(amount);
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

        if (_coinPoint == null && _carrierWorkerTreasure != null)
        {
            Transform nested = FindChildTransformByName(_carrierWorkerTreasure.transform, CoinPointName);
            if (nested != null)
                _coinPoint = nested;
            else
                _coinPoint = FindSceneTransformByName(CoinPointName);
        }

        if (_coinBurstVfx == null && _carrierWorkerTreasure != null)
        {
            Transform burst = FindChildTransformByName(_carrierWorkerTreasure.transform, CoinBurstVfxName);
            if (burst != null)
                _coinBurstVfx = burst.GetComponent<ParticleSystem>();
        }

        if (_coinBurstVfx != null)
        {
            ParticleSystem.MainModule main = _coinBurstVfx.main;
            main.playOnAwake = false;
        }

        if (_treasureAnimator == null && _carrierWorkerTreasure != null)
            EnsureTreasureAnimator();
    }

    private static Transform FindChildTransformByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == objectName)
                return transforms[i];
        }

        return null;
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
