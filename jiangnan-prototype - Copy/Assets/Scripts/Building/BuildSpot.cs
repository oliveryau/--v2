using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BuildSpot : MonoBehaviour
{
    private const string CostUiName = "BuildSpotCostUI";
    private const string CostLabelName = "Cost";

    [SerializeField] private PlaceableType _placeableType;
    [SerializeField] private GameObject _builtObject;
    [SerializeField] private Transform _costUiAnchor;
    [SerializeField] private GameObject _costUiRoot;
    [SerializeField] private TextMeshProUGUI _costText;
    [SerializeField] private int _cost;
    [SerializeField] private RestaurantFloor _floor = RestaurantFloor.Ground;
    private Button _worldBuildButton;

    [Header("Delivery")]
    [SerializeField] private GameObject _carrierGroup;
    [SerializeField] private Transform _deliverySpawn;
    [SerializeField] private Transform[] _deliveryCheckpoints;
    [SerializeField] private GameObject _buildVfxPrefab;
    [SerializeField] private Transform _builtVfxPoint;
    [SerializeField] private float _buildVfxLifetime;
    [SerializeField] private float _deliveryDuration;

    private BuildSpotState _state = BuildSpotState.Locked;
    private Coroutine _deliveryRoutine;
    private bool _costUiSuppressed;

    public PlaceableType PlaceableType => _placeableType;
    public BuildSpotState State => _state;
    public int Cost => _cost;
    public RestaurantFloor Floor => RestaurantFloorUtil.ResolveFloor(transform, _floor);
    public Transform CostUiAnchor => _costUiAnchor != null ? _costUiAnchor : transform;
    public Transform BuildEffectAnchor => _builtObject != null ? _builtObject.transform : transform;
    public GameObject BuiltObject => _builtObject;
    public bool IsBuilt => _state == BuildSpotState.Built;

    public event Action<BuildSpot> Clicked;
    public event Action<BuildSpot> BuildCompleted;

    private void Awake()
    {
        if (_worldBuildButton == null)
            _worldBuildButton = GetComponent<Button>();

        if (_worldBuildButton != null)
            _worldBuildButton.onClick.AddListener(NotifyClicked);

        if (_carrierGroup != null)
            _carrierGroup.SetActive(false);

        CacheCostUi();
        ApplyCostText();
        ApplyState(_state);
    }

    private void OnDestroy()
    {
        if (_worldBuildButton != null)
            _worldBuildButton.onClick.RemoveListener(NotifyClicked);
    }

    public void SetState(BuildSpotState state)
    {
        if (_state == state)
            return;

        _state = state;
        ApplyState(state);
        GameEvents.RaiseBuildSpotStateChanged(this, state);
    }

    public void NotifyClicked()
    {
        if (_state != BuildSpotState.Active)
            return;

        Clicked?.Invoke(this);
    }

    public void BeginDelivery()
    {
        if (_state != BuildSpotState.Active)
            return;

        StopDeliveryRoutine();

        if (_carrierGroup == null)
        {
            CompleteBuild();
            return;
        }

        _deliveryRoutine = StartCoroutine(DeliveryRoutine());
    }

    public void EnterBuildingPhase()
    {
        if (_state != BuildSpotState.Active)
            return;

        StopDeliveryRoutine();
        SetState(BuildSpotState.Delivering);
    }

    public void FinishBuild(bool playCompletionVfx = true)
    {
        if (_state != BuildSpotState.Delivering)
            return;

        CompleteBuild(playCompletionVfx);
    }

    public void SetCostUiSuppressed(bool suppressed)
    {
        _costUiSuppressed = suppressed;
        SyncCostUiVisibility();
    }

    public void RefreshCostUi()
    {
        CacheCostUi();
        ApplyCostText();
        SyncCostUiVisibility();
    }

    private IEnumerator DeliveryRoutine()
    {
        SetState(BuildSpotState.Delivering);

        Transform carrierTransform = _carrierGroup.transform;
        Vector3 spawnPosition = _deliverySpawn != null
            ? _deliverySpawn.position
            : carrierTransform.position;
        Vector3[] checkpoints = PathMovement.BuildCheckpoints(_deliveryCheckpoints, spawnPosition.y);
        Vector3 targetPosition = PathMovement.FlattenToFloorY(transform.position, spawnPosition.y);
        Vector3[] waypoints = PathMovement.BuildWaypoints(checkpoints, targetPosition);

        carrierTransform.position = spawnPosition;
        _carrierGroup.SetActive(true);
        RuntimeMeshVisibility.PrepareHierarchyForRuntimeMove(carrierTransform);

        yield return PathMovement.Move(
            carrierTransform,
            spawnPosition,
            waypoints,
            Mathf.Max(0.01f, _deliveryDuration));

        _carrierGroup.SetActive(false);
        CompleteBuild();
        _deliveryRoutine = null;
    }

    private void CompleteBuild(bool playCompletionVfx = true)
    {
        if (playCompletionVfx)
            PlayBuildVfx();

        AudioManager.Play(SfxId.BuildComplete);
        SetState(BuildSpotState.Built);
        BuildCompleted?.Invoke(this);
    }

    private void StopDeliveryRoutine()
    {
        if (_deliveryRoutine == null)
            return;

        StopCoroutine(_deliveryRoutine);
        _deliveryRoutine = null;
    }

    private void PlayBuildVfx()
    {
        BuildCompletionVfx.Play(_buildVfxPrefab, ResolveBuiltVfxPoint(), _buildVfxLifetime);
    }

    private Transform ResolveBuiltVfxPoint()
    {
        if (_builtVfxPoint != null)
            return _builtVfxPoint;

        return BuildCompletionVfx.ResolveVfxPoint(null, _builtObject);
    }

    private void ApplyState(BuildSpotState state)
    {
        SyncBuiltObjectVisibility(state == BuildSpotState.Built);

        switch (state)
        {
            case BuildSpotState.Active:
            case BuildSpotState.Delivering:
                gameObject.SetActive(ShouldShowOnCurrentViewFloor(state));
                SetBuildButtonVisible(state == BuildSpotState.Active && IsOnCurrentViewFloor());
                SyncCostUiVisibility();
                break;

            default:
                gameObject.SetActive(false);
                SyncCostUiVisibility();
                break;
        }
    }

    private void CacheCostUi()
    {
        if (_costUiRoot == null)
        {
            Transform found = FindChildTransform(transform, CostUiName);
            if (found != null)
                _costUiRoot = found.gameObject;
        }

        if (_costText == null && _costUiRoot != null)
        {
            Transform label = FindChildTransform(_costUiRoot.transform, CostLabelName);
            if (label != null)
                _costText = label.GetComponent<TextMeshProUGUI>();

            if (_costText == null)
                _costText = _costUiRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (_costUiRoot == null)
            return;

        Graphic[] graphics = _costUiRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = false;
        }
    }

    private void ApplyCostText()
    {
        if (_costText != null)
            _costText.text = _cost.ToString();
    }

    private void SyncCostUiVisibility()
    {
        if (_costUiRoot == null)
            return;

        bool show = !_costUiSuppressed
            && _state == BuildSpotState.Active
            && IsOnCurrentViewFloor();

        if (_costUiRoot.activeSelf != show)
            _costUiRoot.SetActive(show);
    }

    private static Transform FindChildTransform(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == objectName)
                return children[i];
        }

        return null;
    }

    private void SyncBuiltObjectVisibility(bool shouldShow)
    {
        if (_builtObject == null)
            return;

        if (shouldShow)
        {
            _builtObject.SetActive(true);

            if (_builtObject.TryGetComponent(out DiningTable diningTable))
                diningTable.RefreshVisualsForBuild();

            return;
        }

        // Never hide an object claimed by a different Built spot (bad shared refs used to
        // wipe Table 3/5 whenever VIP spots applied Locked/Active).
        if (IsBuiltObjectClaimedByAnotherBuiltSpot())
            return;

        _builtObject.SetActive(false);
    }

    private bool IsBuiltObjectClaimedByAnotherBuiltSpot()
    {
        BuildSpot[] spots = FindObjectsByType<BuildSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < spots.Length; i++)
        {
            BuildSpot spot = spots[i];

            if (spot == null || spot == this || !spot.IsBuilt)
                continue;

            if (spot.BuiltObject == _builtObject)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Re-apply world visibility for the currently viewed restaurant floor
    /// without changing build state.
    /// </summary>
    public void RefreshViewFloorVisibility()
    {
        if (_state == BuildSpotState.Active || _state == BuildSpotState.Delivering)
        {
            gameObject.SetActive(ShouldShowOnCurrentViewFloor(_state));
            SetBuildButtonVisible(_state == BuildSpotState.Active && IsOnCurrentViewFloor());
        }

        SyncCostUiVisibility();
    }

    private bool ShouldShowOnCurrentViewFloor(BuildSpotState state)
    {
        // Keep delivering spots alive even if the player switches floors mid-delivery.
        if (state == BuildSpotState.Delivering)
            return true;

        return IsOnCurrentViewFloor();
    }

    private bool IsOnCurrentViewFloor()
    {
        int viewedFloor = CharacterPanelController.Instance != null
            ? CharacterPanelController.Instance.CurrentFloor
            : (int)RestaurantFloor.Ground;

        return (int)Floor == viewedFloor;
    }

    private void SetBuildButtonVisible(bool visible)
    {
        if (_worldBuildButton == null)
            return;

        _worldBuildButton.interactable = visible;

        Graphic graphic = _worldBuildButton.targetGraphic;

        if (graphic == null)
            return;

        graphic.enabled = visible;
        graphic.raycastTarget = visible;
    }
}
