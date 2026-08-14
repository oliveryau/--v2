using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-150)]
public class CharacterPanelController : MonoBehaviour
{
    public static CharacterPanelController Instance { get; private set; }

    private const string FirstFloorButtonName = "1st Floor";
    private const string SecondFloorButtonName = "2nd Floor";
    private const string CameraRigName = "Camera Rig";
    private const string SecondFloorEnvironmentName = "Second Floor";
    private const string SecondFloorBuildSpotsName = "BuildSpots_SecondFloor";
    private const string SecondFloorLayerName = "SecondFloor";
    private const string StoveRootName = "Stove";
    private const string BagRootName = "Bag";
    private const string BagCountName = "Count";
    private const string BagInventoryListName = "Inventory List";

    [SerializeField] private TextMeshProUGUI _playerNameText;
    [SerializeField] private TextMeshProUGUI _goldAmountText;
    [SerializeField] private RectTransform _goldUiRoot;
    [SerializeField] private GameObject _floorsRoot;
    [SerializeField] private GameObject _bagRoot;
    [SerializeField] private Button _bagButton;
    [SerializeField] private TextMeshProUGUI _bagCountText;
    [SerializeField] private GameObject _bagInventoryListRoot;
    [SerializeField] private Button _firstFloorButton;
    [SerializeField] private Button _secondFloorButton;
    [SerializeField] private Vector3 _firstFloorCameraPosition = Vector3.zero;
    [SerializeField] private Vector3 _secondFloorCameraPosition = new Vector3(-5f, 5f, 0f);
    [SerializeField] private float _floorTransitionDuration = 0.75f;
    [SerializeField] private float _floorsUnlockPulseDuration = 3f;
    [SerializeField] private float _floorsUnlockPulseMinScale = 0.98f;
    [SerializeField] private float _floorsUnlockPulseMaxScale = 1.02f;
    [SerializeField] private float _floorsUnlockPulseSpeed = 8f;

    private Transform _cameraRig;
    private Camera _floorCamera;
    private Light _mainLight;
    private readonly List<GameObject> _secondFloorRoots = new();
    private readonly List<ParticleSystem> _groundFloorStoveParticles = new();
    private readonly List<TextMeshProUGUI> _bagInventoryItemTexts = new();
    private int _secondFloorLayer = -1;
    private int _currentFloor = 1;
    private bool _floorButtonsWired;
    private bool _bagButtonWired;
    private bool _bagInventoryVisible;
    private bool _secondFloorViewVisible = true;
    private Coroutine _floorTransitionRoutine;
    private Coroutine _floorsUnlockPulseRoutine;

    public TextMeshProUGUI GoldAmountText => _goldAmountText;
    public RectTransform GoldUiRoot => _goldUiRoot;
    public int CurrentFloor => _currentFloor;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent != null)
            transform.SetParent(null, false);

        DontDestroyOnLoad(gameObject);

        CacheUiReferences();
        RefreshPlayerName();
        ReparentToSceneCanvas();
        SyncFloorsVisibility();
        SyncBagVisibility();
        SyncFloorSwitching();
        WireBagButton();
        SetBagInventoryVisible(false);
        RefreshBagUi();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        GameEvents.SecondFloorUnlocked += HandleSecondFloorUnlocked;
        GameEvents.BagInventoryChanged += HandleBagInventoryChanged;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        GameEvents.SecondFloorUnlocked -= HandleSecondFloorUnlocked;
        GameEvents.BagInventoryChanged -= HandleBagInventoryChanged;
        UnsubscribeFloorButtons();
        UnsubscribeBagButton();

        if (_floorTransitionRoutine != null)
        {
            StopCoroutine(_floorTransitionRoutine);
            _floorTransitionRoutine = null;
        }

        StopFloorsUnlockPulse(resetScale: true);
    }

    private void HandleSecondFloorUnlocked()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return;

        SyncFloorButtonInteractable(_currentFloor == 2);
        StartFloorsUnlockPulse();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RefreshPlayerName()
    {
        if (_playerNameText == null)
            return;

        if (PlayerProfileStorage.HasCurrentPlayerName)
        {
            _playerNameText.text = PlayerProfileStorage.CurrentPlayerName;
            return;
        }

        if (PlayerProfileStorage.TryLoadLastPlayerName(out string savedName))
            _playerNameText.text = savedName;
    }

    /// <summary>
    /// Re-collect second-floor roots (e.g. after stairs unlock) and re-apply view visibility
    /// without deactivating gameplay objects.
    /// </summary>
    public void RefreshSecondFloorVisibilityRoots()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return;

        CacheFloorSceneReferences();
        SyncSecondFloorActorsToCullLayer();
        ApplySecondFloorViewVisible(_currentFloor == 2);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ReparentToSceneCanvas();
        RefreshPlayerName();
        SyncFloorsVisibility();
        SyncBagVisibility();
        SyncFloorSwitching();
        WireBagButton();
        SetBagInventoryVisible(false);
        RefreshBagUi();

        if (GoldManager.Instance != null)
            GameEvents.RaiseGoldChanged(GoldManager.Instance.CurrentGold);
    }

    private void HandleBagInventoryChanged()
    {
        RefreshBagUi();
    }

    private void ReparentToSceneCanvas()
    {
        Canvas screenCanvas = FindScreenCanvas();

        if (screenCanvas == null)
            return;

        RectTransform canvasRect = screenCanvas.transform as RectTransform;
        RectTransform panelRect = transform as RectTransform;

        if (canvasRect == null || panelRect == null)
            return;

        if (panelRect.parent == canvasRect)
            return;

        panelRect.SetParent(canvasRect, false);
    }

    private void SyncFloorsVisibility()
    {
        if (_floorsRoot == null)
            return;

        bool showFloors = RestaurantSceneMode.IsMainScene;
        if (!showFloors)
            StopFloorsUnlockPulse(resetScale: true);

        _floorsRoot.SetActive(showFloors);
    }

    private static bool ShouldShowBagUi()
    {
        return RestaurantSceneMode.IsMainScene || RestaurantSceneMode.IsFutureScene;
    }

    private void SyncBagVisibility()
    {
        CacheBagUiReferences();

        if (_bagRoot == null)
            return;

        bool showBag = ShouldShowBagUi();
        _bagRoot.SetActive(showBag);

        if (!showBag)
            SetBagInventoryVisible(false);
    }

    private void SyncFloorSwitching()
    {
        UnsubscribeFloorButtons();

        if (!RestaurantSceneMode.IsMainScene)
        {
            _cameraRig = null;
            _floorCamera = null;
            _mainLight = null;
            _secondFloorRoots.Clear();
            return;
        }

        CacheFloorUiReferences();
        CacheFloorSceneReferences();
        WireFloorButtons();
        SetFloor(1, force: true);
    }

    private void CacheFloorUiReferences()
    {
        if (_floorsRoot == null)
        {
            Transform floors = transform.Find("Floors");
            if (floors != null)
                _floorsRoot = floors.gameObject;
        }

        if (_floorsRoot == null)
            return;

        if (_firstFloorButton == null)
        {
            Transform first = _floorsRoot.transform.Find(FirstFloorButtonName);
            if (first != null)
                _firstFloorButton = first.GetComponent<Button>() ?? first.gameObject.AddComponent<Button>();
        }

        if (_secondFloorButton == null)
        {
            Transform second = _floorsRoot.transform.Find(SecondFloorButtonName);
            if (second != null)
                _secondFloorButton = second.GetComponent<Button>() ?? second.gameObject.AddComponent<Button>();
        }
    }

    private void CacheFloorSceneReferences()
    {
        GameObject cameraRigObject = GameObject.Find(CameraRigName);
        _cameraRig = cameraRigObject != null ? cameraRigObject.transform : null;

        _floorCamera = Camera.main;
        if (_floorCamera == null && _cameraRig != null)
            _floorCamera = _cameraRig.GetComponentInChildren<Camera>(true);

        _mainLight = FindMainDirectionalLight();
        _secondFloorLayer = LayerMask.NameToLayer(SecondFloorLayerName);

        _secondFloorRoots.Clear();
        TryAddSecondFloorRoot(FindSceneObjectByExactName(SecondFloorEnvironmentName));
        TryAddSecondFloorRoot(FindSceneObjectByExactName("Second Floor (1)"));
        TryAddSecondFloorRoot(FindSceneObjectByExactName(SecondFloorBuildSpotsName));
        CollectSecondFloorBuiltObjects();
        CacheGroundFloorStoveParticles();

        // Keep the floor slab active so it can be view-culled without killing simulation.
        // Build spots / built props stay owned by BuildSequenceController / BuildSpot.
        for (int i = 0; i < _secondFloorRoots.Count; i++)
        {
            GameObject root = _secondFloorRoots[i];
            if (root == null)
                continue;

            if (string.Equals(root.name, SecondFloorEnvironmentName, System.StringComparison.OrdinalIgnoreCase))
                root.SetActive(true);

            if (_secondFloorLayer >= 0)
                RestaurantFloorUtil.SetLayerRecursively(root, _secondFloorLayer);
        }
    }

    private void TryAddSecondFloorRoot(GameObject root)
    {
        if (root == null || _secondFloorRoots.Contains(root))
            return;

        _secondFloorRoots.Add(root);
    }

    private void CollectSecondFloorBuiltObjects()
    {
        BuildSpot[] spots = FindObjectsByType<BuildSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < spots.Length; i++)
        {
            BuildSpot spot = spots[i];
            if (spot == null)
                continue;

            if (!IsUnderSecondFloorBuildSpots(spot.transform))
                continue;

            // Built furniture lives outside the spot hierarchy — tag it for floor culling.
            if (spot.BuiltObject != null)
                TryAddSecondFloorRoot(spot.BuiltObject);
        }
    }

    private static bool IsUnderSecondFloorBuildSpots(Transform transform)
    {
        Transform current = transform;

        while (current != null)
        {
            if (string.Equals(current.name, SecondFloorBuildSpotsName, System.StringComparison.OrdinalIgnoreCase))
                return true;

            current = current.parent;
        }

        return false;
    }

    private void WireFloorButtons()
    {
        if (_floorButtonsWired)
            return;

        if (_firstFloorButton != null)
            _firstFloorButton.onClick.AddListener(HandleFirstFloorClicked);

        if (_secondFloorButton != null)
            _secondFloorButton.onClick.AddListener(HandleSecondFloorClicked);

        _floorButtonsWired = _firstFloorButton != null || _secondFloorButton != null;
    }

    private void UnsubscribeFloorButtons()
    {
        if (!_floorButtonsWired)
            return;

        if (_firstFloorButton != null)
            _firstFloorButton.onClick.RemoveListener(HandleFirstFloorClicked);

        if (_secondFloorButton != null)
            _secondFloorButton.onClick.RemoveListener(HandleSecondFloorClicked);

        _floorButtonsWired = false;
    }

    private void HandleFirstFloorClicked()
    {
        SetFloor(1);
    }

    private void HandleSecondFloorClicked()
    {
        if (!RestaurantFloorUtil.IsUnlockedForCurrentPlayer())
            return;

        SetFloor(2);
    }

    /// <summary>Switch the restaurant view to floor 1 (camera + floor UI visibility).</summary>
    public void GoToFirstFloor(bool force = false)
    {
        SetFloor(1, force);
    }

    /// <summary>Switch the restaurant view to floor 2 (camera + floor UI visibility).</summary>
    public void GoToSecondFloor(bool force = false)
    {
        SetFloor(2, force);
    }

    /// <summary>
    /// Jump to the authored camera for a floor. Recenters even when already viewing that floor.
    /// </summary>
    public void FocusDefaultFloorCamera(int floor)
    {
        if (floor == 2 && !RestaurantFloorUtil.IsUnlockedForCurrentPlayer())
            floor = 1;

        Vector3 cameraPosition = floor == 2 ? _secondFloorCameraPosition : _firstFloorCameraPosition;

        if (_currentFloor != floor)
        {
            SetFloor(floor);
            return;
        }

        FocusCameraAt(cameraPosition, ensureGroundFloor: floor == 1);
    }

    /// <summary>
    /// Ensure ground-floor view, then move the camera to an authored presentation position
    /// (used by portal lull framing — not the default first-floor camera).
    /// </summary>
    public void FocusCameraAt(Vector3 cameraPosition, bool ensureGroundFloor = true)
    {
        if (ensureGroundFloor && _currentFloor != 1)
        {
            if (_floorTransitionRoutine != null)
            {
                StopCoroutine(_floorTransitionRoutine);
                _floorTransitionRoutine = null;
            }

            _currentFloor = 1;
            ApplySecondFloorViewVisible(false);
            SyncFloorButtonInteractable(false);
            GameEvents.RaiseRestaurantFloorChanged(_currentFloor);
        }

        if (_floorTransitionRoutine != null)
        {
            StopCoroutine(_floorTransitionRoutine);
            _floorTransitionRoutine = null;
        }

        if (_floorTransitionDuration <= 0f)
        {
            ApplyCameraFloorPosition(cameraPosition, updateGroundPlane: true);
            return;
        }

        _floorTransitionRoutine = StartCoroutine(AnimateCameraToPosition(cameraPosition));
    }

    private IEnumerator AnimateCameraToPosition(Vector3 targetPosition)
    {
        Transform cameraTarget = ResolveCameraMoveTarget();
        Vector3 startPosition = cameraTarget != null ? cameraTarget.position : targetPosition;
        Vector3 endPosition = targetPosition;
        if (Mathf.Approximately(targetPosition.z, 0f))
            endPosition.z = startPosition.z;

        float duration = Mathf.Max(0.01f, _floorTransitionDuration);
        float elapsed = 0f;

        InputManager inputManager = FindFirstObjectByType<InputManager>();
        inputManager?.SetGroundHeight(endPosition.y);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothed = t * t * (3f - 2f * t);
            Vector3 position = Vector3.Lerp(startPosition, endPosition, smoothed);
            ApplyCameraFloorPosition(position, updateGroundPlane: false);
            yield return null;
        }

        ApplyCameraFloorPosition(endPosition, updateGroundPlane: true);
        _floorTransitionRoutine = null;
    }

    private void SetFloor(int floor, bool force = false)
    {
        if (floor == 2 && !RestaurantFloorUtil.IsUnlockedForCurrentPlayer())
            floor = 1;

        if (!force && _currentFloor == floor)
            return;

        if (_floorTransitionRoutine != null)
        {
            StopCoroutine(_floorTransitionRoutine);
            _floorTransitionRoutine = null;
        }

        _currentFloor = floor == 2 ? 2 : 1;
        bool onSecondFloor = _currentFloor == 2;
        Vector3 cameraPosition = onSecondFloor ? _secondFloorCameraPosition : _firstFloorCameraPosition;

        // Swap build/hire spot visibility immediately on click — don't wait for camera travel.
        ApplySecondFloorViewVisible(onSecondFloor);
        GameEvents.RaiseRestaurantFloorChanged(_currentFloor);

        if (force || _floorTransitionDuration <= 0f)
        {
            ApplyCameraFloorPosition(cameraPosition, updateGroundPlane: true);
            SyncFloorButtonInteractable(onSecondFloor);
            return;
        }

        _floorTransitionRoutine = StartCoroutine(AnimateFloorTransition(onSecondFloor, cameraPosition));
    }

    private IEnumerator AnimateFloorTransition(bool onSecondFloor, Vector3 targetPosition)
    {
        SetFloorButtonsInteractable(false);

        Transform cameraTarget = ResolveCameraMoveTarget();
        Vector3 startPosition = cameraTarget != null ? cameraTarget.position : targetPosition;
        // Floor targets set X/Y; keep the current pan Z unless a floor Z is authored.
        Vector3 endPosition = targetPosition;
        if (Mathf.Approximately(targetPosition.z, 0f))
            endPosition.z = startPosition.z;

        float duration = Mathf.Max(0.01f, _floorTransitionDuration);
        float elapsed = 0f;

        // Keep pan grounded at the destination while the camera travels.
        InputManager inputManager = FindFirstObjectByType<InputManager>();
        inputManager?.SetGroundHeight(endPosition.y);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothed = t * t * (3f - 2f * t); // smoothstep
            Vector3 position = Vector3.Lerp(startPosition, endPosition, smoothed);
            ApplyCameraFloorPosition(position, updateGroundPlane: false);
            yield return null;
        }

        ApplyCameraFloorPosition(endPosition, updateGroundPlane: true);
        SyncFloorButtonInteractable(onSecondFloor);
        _floorTransitionRoutine = null;
    }

    private void ApplySecondFloorViewVisible(bool visible)
    {
        _secondFloorViewVisible = visible;

        SyncSecondFloorActorsToCullLayer();

        if (_secondFloorLayer >= 0)
        {
            int mask = 1 << _secondFloorLayer;

            if (_floorCamera != null)
            {
                if (visible)
                    _floorCamera.cullingMask |= mask;
                else
                    _floorCamera.cullingMask &= ~mask;
            }

            // Prevent upstairs geometry from casting shadows onto floor 1 while hidden.
            if (_mainLight != null)
            {
                if (visible)
                    _mainLight.cullingMask |= mask;
                else
                    _mainLight.cullingMask &= ~mask;
            }
        }

        // Colliders still block raycasts even when culled — disable them while hidden.
        for (int i = 0; i < _secondFloorRoots.Count; i++)
        {
            GameObject root = _secondFloorRoots[i];
            if (root == null || !root.activeInHierarchy)
                continue;

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < colliders.Length; c++)
            {
                if (colliders[c] != null)
                    colliders[c].enabled = visible;
            }
        }

        // Stove smoke/fire stay on Default layer — hide them upstairs so they don't bleed through.
        SetGroundFloorStoveParticlesVisible(!visible);
    }

    private void CacheGroundFloorStoveParticles()
    {
        _groundFloorStoveParticles.Clear();

        if (!RestaurantSceneMode.IsMainScene)
            return;

        GameObject stoveRoot = FindSceneObjectByExactName(StoveRootName);
        if (stoveRoot == null)
            return;

        ParticleSystem[] particles = stoveRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] != null)
                _groundFloorStoveParticles.Add(particles[i]);
        }
    }

    private void SetGroundFloorStoveParticlesVisible(bool visible)
    {
        if (_groundFloorStoveParticles.Count == 0)
            CacheGroundFloorStoveParticles();

        for (int i = 0; i < _groundFloorStoveParticles.Count; i++)
        {
            ParticleSystem particle = _groundFloorStoveParticles[i];
            if (particle == null)
                continue;

            if (visible)
            {
                if (!particle.gameObject.activeSelf)
                    particle.gameObject.SetActive(true);

                if (!particle.isPlaying)
                    particle.Play(true);
            }
            else
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.gameObject.SetActive(false);
            }
        }
    }

    private void SyncSecondFloorActorsToCullLayer()
    {
        Worker[] workers = FindObjectsByType<Worker>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < workers.Length; i++)
        {
            Worker worker = workers[i];

            // VIP-floor waiters move between floors (stove pickup downstairs) — cull by elevation.
            if (worker == null || !worker.ServesVipFloorOnly)
                continue;

            RestaurantFloorUtil.SyncActorFloorViewLayerByElevation(worker.gameObject);
        }

        Customer[] customers = FindObjectsByType<Customer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < customers.Length; i++)
        {
            Customer customer = customers[i];

            // VIPs enter on the ground floor first — only hide them once they are upstairs.
            if (customer == null || !customer.IsVip)
                continue;

            RestaurantFloorUtil.SyncActorFloorViewLayerByElevation(customer.gameObject);
        }
    }

    private void LateUpdate()
    {
        if (!RestaurantSceneMode.IsMainScene || _floorCamera == null)
            return;

        // Keep VIP/waiter cull layers in sync as they walk between floors.
        SyncSecondFloorActorsToCullLayer();
    }

    private void SyncFloorButtonInteractable(bool onSecondFloor)
    {
        bool secondFloorUnlocked = RestaurantFloorUtil.IsUnlockedForCurrentPlayer();

        if (_firstFloorButton != null)
            _firstFloorButton.interactable = secondFloorUnlocked && onSecondFloor;

        if (_secondFloorButton != null)
            _secondFloorButton.interactable = secondFloorUnlocked && !onSecondFloor;
    }

    private void StartFloorsUnlockPulse()
    {
        CacheFloorUiReferences();

        if (_floorsRoot == null || !RestaurantSceneMode.IsMainScene)
            return;

        if (!_floorsRoot.activeSelf)
            _floorsRoot.SetActive(true);

        StopFloorsUnlockPulse(resetScale: false);
        _floorsUnlockPulseRoutine = StartCoroutine(PulseFloorsUnlockCoroutine());
    }

    private void StopFloorsUnlockPulse(bool resetScale)
    {
        if (_floorsUnlockPulseRoutine != null)
        {
            StopCoroutine(_floorsUnlockPulseRoutine);
            _floorsUnlockPulseRoutine = null;
        }

        if (resetScale && _floorsRoot != null)
            _floorsRoot.transform.localScale = Vector3.one;
    }

    private IEnumerator PulseFloorsUnlockCoroutine()
    {
        Transform floors = _floorsRoot != null ? _floorsRoot.transform : null;
        if (floors == null)
        {
            _floorsUnlockPulseRoutine = null;
            yield break;
        }

        float duration = Mathf.Max(0.01f, _floorsUnlockPulseDuration);
        float minScale = _floorsUnlockPulseMinScale;
        float maxScale = _floorsUnlockPulseMaxScale;
        float speed = Mathf.Max(0.01f, _floorsUnlockPulseSpeed);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float pulseT = (Mathf.Sin(elapsed * speed) + 1f) * 0.5f;
            float scale = Mathf.Lerp(minScale, maxScale, pulseT);
            floors.localScale = Vector3.one * scale;
            yield return null;
        }

        floors.localScale = Vector3.one;
        _floorsUnlockPulseRoutine = null;
    }

    private void SetFloorButtonsInteractable(bool interactable)
    {
        if (!interactable)
        {
            if (_firstFloorButton != null)
                _firstFloorButton.interactable = false;

            if (_secondFloorButton != null)
                _secondFloorButton.interactable = false;

            return;
        }

        SyncFloorButtonInteractable(_currentFloor == 2);
    }

    private Transform ResolveCameraMoveTarget()
    {
        InputManager inputManager = FindFirstObjectByType<InputManager>();

        if (inputManager != null && inputManager.transform != null)
            return inputManager.transform;

        return _cameraRig;
    }

    private void ApplyCameraFloorPosition(Vector3 position, bool updateGroundPlane)
    {
        InputManager inputManager = FindFirstObjectByType<InputManager>();

        if (inputManager != null)
        {
            // Floor switches are authored targets — don't clamp them to pan bounds.
            inputManager.SetPanTargetPositionOnly(position);
            if (updateGroundPlane)
                inputManager.SetGroundHeight(position.y);

            return;
        }

        if (_cameraRig == null)
            return;

        _cameraRig.position = position;
    }

    private void CacheUiReferences()
    {
        if (_playerNameText == null)
        {
            Transform profileBg = transform.Find("Profile Bg");

            if (profileBg != null)
            {
                Transform nameTransform = profileBg.Find("Name");

                if (nameTransform != null)
                    _playerNameText = nameTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_goldAmountText == null)
        {
            Transform goldUi = transform.Find("Gold UI");

            if (goldUi != null)
            {
                Transform goldAmt = goldUi.Find("Gold Amt");

                if (goldAmt != null)
                    _goldAmountText = goldAmt.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_goldUiRoot == null)
        {
            Transform goldUi = transform.Find("Gold UI");

            if (goldUi != null)
                _goldUiRoot = goldUi as RectTransform;
        }

        CacheFloorUiReferences();
        CacheBagUiReferences();
    }

    private void CacheBagUiReferences()
    {
        if (_bagRoot == null)
        {
            Transform bag = transform.Find(BagRootName);
            if (bag != null)
                _bagRoot = bag.gameObject;
        }

        if (_bagRoot == null)
            return;

        if (_bagButton == null)
            _bagButton = _bagRoot.GetComponent<Button>() ?? _bagRoot.AddComponent<Button>();

        if (_bagButton != null && _bagButton.targetGraphic == null)
        {
            Image bagImage = _bagRoot.GetComponent<Image>();
            if (bagImage != null)
                _bagButton.targetGraphic = bagImage;
        }

        if (_bagCountText == null)
        {
            Transform count = _bagRoot.transform.Find(BagCountName);
            if (count != null)
                _bagCountText = count.GetComponent<TextMeshProUGUI>();
        }

        if (_bagInventoryListRoot == null)
        {
            Transform inventoryList = _bagRoot.transform.Find(BagInventoryListName);
            if (inventoryList != null)
                _bagInventoryListRoot = inventoryList.gameObject;
        }

        CacheBagInventoryItemSlots();
    }

    private void CacheBagInventoryItemSlots()
    {
        _bagInventoryItemTexts.Clear();

        if (_bagInventoryListRoot == null)
            return;

        Transform listTransform = _bagInventoryListRoot.transform;
        for (int i = 0; i < listTransform.childCount; i++)
        {
            Transform child = listTransform.GetChild(i);
            if (child == null || !child.name.StartsWith("Item", StringComparison.OrdinalIgnoreCase))
                continue;

            TextMeshProUGUI itemText = child.GetComponent<TextMeshProUGUI>();
            if (itemText != null)
                _bagInventoryItemTexts.Add(itemText);
        }
    }

    private void WireBagButton()
    {
        CacheBagUiReferences();

        if (_bagButtonWired || _bagButton == null)
            return;

        _bagButton.onClick.AddListener(HandleBagClicked);
        _bagButtonWired = true;
    }

    private void UnsubscribeBagButton()
    {
        if (!_bagButtonWired || _bagButton == null)
            return;

        _bagButton.onClick.RemoveListener(HandleBagClicked);
        _bagButtonWired = false;
    }

    private void HandleBagClicked()
    {
        if (!ShouldShowBagUi() || _bagRoot == null || !_bagRoot.activeSelf)
            return;

        SetBagInventoryVisible(!_bagInventoryVisible);
    }

    private void SetBagInventoryVisible(bool visible)
    {
        _bagInventoryVisible = visible;

        if (_bagInventoryListRoot != null)
            _bagInventoryListRoot.SetActive(visible);
    }

    public void RefreshBagUi()
    {
        CacheBagUiReferences();

        BagInventoryEntry[] items = PlayerProfileStorage.GetBagItemsForCurrentPlayer();
        int totalCount = PlayerProfileStorage.GetBagTotalItemCountForCurrentPlayer();

        if (_bagCountText != null)
            _bagCountText.text = $"x{totalCount}";

        for (int i = 0; i < _bagInventoryItemTexts.Count; i++)
        {
            TextMeshProUGUI itemText = _bagInventoryItemTexts[i];
            if (itemText == null)
                continue;

            if (i < items.Length)
            {
                BagInventoryEntry entry = items[i];
                itemText.gameObject.SetActive(true);
                itemText.text = $"{entry.itemName} x{Mathf.Max(0, entry.count)}";
            }
            else
            {
                itemText.text = string.Empty;
                itemText.gameObject.SetActive(false);
            }
        }
    }

    private static Light FindMainDirectionalLight()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Light best = null;

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null || light.type != LightType.Directional || !light.enabled)
                continue;

            if (best == null || light.intensity > best.intensity)
                best = light;
        }

        return best;
    }

    private static GameObject FindSceneObjectByExactName(string objectName)
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (candidate != null
                && string.Equals(candidate.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private static Canvas FindScreenCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];

            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                continue;

            if (string.Equals(canvas.name, "UI Canvas", System.StringComparison.OrdinalIgnoreCase))
                return canvas;
        }

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];

            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                return canvas;
        }

        return null;
    }
}
