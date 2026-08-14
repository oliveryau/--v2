using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(160)]
public class MissionUiController : MonoBehaviour
{
    private const string MissionRootName = "Mission";
    private const string ClosedRootName = "Closed";
    private const string OpenedRootName = "Opened";
    private const string ClosedBgName = "Bg";
    private const string OpenButtonName = "Open Button";
    private const string CloseButtonName = "Close Button";
    private const string TitleName = "Title";

    [SerializeField] private MissionCatalog _missionCatalog;
    [SerializeField] private GameObject _missionRoot;
    [SerializeField] private GameObject _closedUiRoot;
    [SerializeField] private GameObject _openedUiRoot;
    [SerializeField] private Button _openButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI[] _taskTexts = new TextMeshProUGUI[MissionCatalog.MaxTasksPerPart];
    [SerializeField] private Color _incompleteTaskColor = Color.white;
    [SerializeField] private Color _completeTaskColor = new Color(0.25f, 0.85f, 0.3f, 1f);
    [SerializeField] private float _completedPartHoldSeconds;

    private readonly int[] _placedCounts = new int[Enum.GetValues(typeof(PlaceableType)).Length];
    private readonly int[] _hiredCounts = new int[Enum.GetValues(typeof(WorkerType)).Length];
    private int _openBusinessCompletions;
    private int _currentPartIndex;
    private int _displayedPartIndex = -1;
    private Coroutine _advancePartRoutine;
    private bool _isAdvancingPart;
    private bool _initialized;
    private bool _isPanelOpen;
    private bool _buttonsWired;

    /// <summary>Linear watermark used by build/hire gating (missions 1-5 progress).</summary>
    public int CurrentPartIndex => _currentPartIndex;

    private void Awake()
    {
        EnsureInitialized();
    }

    public void EnsureInitialized(MissionCatalog catalog = null)
    {
        if (catalog != null)
            _missionCatalog = catalog;

        if (_missionCatalog == null)
            _missionCatalog = MissionCatalog.LoadOrCreateDefault();

        CacheReferences();
        WirePanelButtons();

        if (_initialized)
        {
            ApplySceneMissionUnlocks();
            return;
        }

        _currentPartIndex = PlayerProfileStorage.GetMainSceneMissionPartIndexForCurrentPlayer();
        ClampCurrentPartIndex();

        if (_currentPartIndex > MissionCatalog.OpenBusinessMissionPartIndex
            || PlayerProfileStorage.HasMainSceneBusinessStartedForCurrentPlayer())
        {
            _openBusinessCompletions = 1;
        }

        ApplySceneMissionUnlocks();

        // Default: mission panel starts closed.
        SetPanelOpen(false);
        _initialized = true;

        // Competitor/Future rely on this path (UIManager only used to skip Main-only init before).
        RefreshUi();
        SyncMissionRootVisibility(GameManager.Instance != null ? GameManager.Instance.State : GameState.Building);
    }

    public void Initialize(MissionCatalog catalog = null)
    {
        EnsureInitialized(catalog);
        RefreshFromPlacedCounts();
    }

    private void OnEnable()
    {
        GameEvents.StateChanged += HandleStateChanged;
        WirePanelButtons();
        ApplySceneMissionUnlocks();
        if (_initialized)
            RefreshUi();
        SyncMissionRootVisibility(GameManager.Instance != null ? GameManager.Instance.State : GameState.Building);
    }

    private void OnDisable()
    {
        GameEvents.StateChanged -= HandleStateChanged;
        UnwirePanelButtons();

        if (_advancePartRoutine != null)
        {
            StopCoroutine(_advancePartRoutine);
            _advancePartRoutine = null;

            while (IsDisplayedPartComplete() && TryAdvanceDisplayedPart())
            {
            }
        }
    }

    private void ApplySceneMissionUnlocks()
    {
        bool changed = false;

        if (RestaurantSceneMode.IsCompetitorScene
            && !PlayerProfileStorage.IsMissionStealUnlockedForCurrentPlayer())
        {
            PlayerProfileStorage.SetMissionStealUnlockedForCurrentPlayer();
            changed = true;
        }

        if (RestaurantSceneMode.IsFutureScene
            && !PlayerProfileStorage.IsMissionFutureUnlockedForCurrentPlayer())
        {
            PlayerProfileStorage.SetMissionFutureUnlockedForCurrentPlayer();
            changed = true;
        }

        // Older saves already cleared the steal gate via this flag.
        if (PlayerProfileStorage.HasCompetitorVipStealAttemptedForCurrentPlayer()
            && !PlayerProfileStorage.IsMissionStealCompletedForCurrentPlayer())
        {
            PlayerProfileStorage.SetMissionStealCompletedForCurrentPlayer();
            changed = true;
        }

        if (changed && _initialized)
            RefreshAfterExternalCompletion();
    }

    public static void NotifyServeVipFinished()
    {
        if (PlayerProfileStorage.IsMissionServeVipCompletedForCurrentPlayer())
            return;

        PlayerProfileStorage.SetMissionServeVipCompletedForCurrentPlayer();

        MissionUiController controller = FindActiveController();
        if (controller == null)
            return;

        controller.EnsureInitialized();
        if (controller._currentPartIndex < MissionCatalog.ServeVipMissionPartIndex + 1)
        {
            controller._currentPartIndex = MissionCatalog.ServeVipMissionPartIndex + 1;
            PlayerProfileStorage.SetMainSceneMissionPartIndexForCurrentPlayer(controller._currentPartIndex);
        }

        GameEvents.RaiseMissionPartCompleted(MissionCatalog.ServeVipMissionPartIndex);
        GameEvents.RaiseMissionPartChanged(controller._currentPartIndex);
        controller.RefreshAfterExternalCompletion();
    }

    public static void NotifyStealRequirementMet()
    {
        if (PlayerProfileStorage.IsMissionStealCompletedForCurrentPlayer())
            return;

        PlayerProfileStorage.SetMissionStealCompletedForCurrentPlayer();

        MissionUiController controller = FindActiveController();
        if (controller == null)
            return;

        controller.EnsureInitialized();
        GameEvents.RaiseMissionPartCompleted(MissionCatalog.StealCustomersMissionPartIndex);
        GameEvents.RaiseMissionPartChanged(controller._currentPartIndex);
        controller.RefreshAfterExternalCompletion();
    }

    public static void NotifyFutureItemPurchased()
    {
        bool alreadyComplete = PlayerProfileStorage.IsMissionFuturePurchaseCompletedForCurrentPlayer();
        PlayerProfileStorage.SetMissionFuturePurchaseCompletedForCurrentPlayer();

        if (alreadyComplete)
            return;

        MissionUiController controller = FindActiveController();
        if (controller == null)
            return;

        controller.EnsureInitialized();
        GameEvents.RaiseMissionPartCompleted(MissionCatalog.PurchaseModernDishMissionPartIndex);
        GameEvents.RaiseMissionPartChanged(controller._currentPartIndex);
        controller.RefreshAfterExternalCompletion();
    }

    public static void NotifyDishSoldToVip()
    {
        if (PlayerProfileStorage.IsMissionSellToVipCompletedForCurrentPlayer())
            return;

        PlayerProfileStorage.SetMissionSellToVipCompletedForCurrentPlayer();

        MissionUiController controller = FindActiveController();
        if (controller == null)
            return;

        controller.EnsureInitialized();
        GameEvents.RaiseMissionPartCompleted(MissionCatalog.SellDishToVipMissionPartIndex);
        GameEvents.RaiseMissionPartChanged(controller._currentPartIndex);
        controller.RefreshAfterExternalCompletion();
    }

    private void RefreshAfterExternalCompletion()
    {
        RefreshUi();
        SyncMissionRootVisibility(GameManager.Instance != null ? GameManager.Instance.State : GameState.Building);
    }

    private static MissionUiController FindActiveController()
    {
        return FindFirstObjectByType<MissionUiController>();
    }

    public void NotifyPlaceableBuilt(PlaceableType type)
    {
        int index = (int)type;

        if (index < 0 || index >= _placedCounts.Length)
            return;

        _placedCounts[index]++;
        RefreshUiAndAdvanceIfNeeded();
    }

    public void SyncPlacedCounts(int receptions, int stoves, int tables, int stairs, int vipTables, int vipStages = 0)
    {
        SetPlacedCount(PlaceableType.Reception, receptions);
        SetPlacedCount(PlaceableType.Stove, stoves);
        SetPlacedCount(PlaceableType.Table, tables);
        SetPlacedCount(PlaceableType.Stairs, stairs);
        SetPlacedCount(PlaceableType.VipTable, vipTables);
        SetPlacedCount(PlaceableType.VipStage, vipStages);
        RefreshUi();
    }

    public void SetHiredCounts(int chefs, int waiters)
    {
        SyncHiredCounts(chefs, waiters);
        RefreshUiAndAdvanceIfNeeded();
    }

    public void SyncHiredCounts(int chefs, int waiters)
    {
        SetHiredCount(WorkerType.Chef, chefs);
        SetHiredCount(WorkerType.Waiter, waiters);
        RefreshUi();
    }

    public void NotifyOpenBusinessOpened()
    {
        _openBusinessCompletions++;
        RefreshUiAndAdvanceIfNeeded();
    }

    private void HandleStateChanged(GameState state)
    {
        SyncMissionRootVisibility(state);
    }

    private void RefreshFromPlacedCounts()
    {
        RefreshUiAndAdvanceIfNeeded();
        SyncMissionRootVisibility(GameManager.Instance != null ? GameManager.Instance.State : GameState.Building);
    }

    private void RefreshUiAndAdvanceIfNeeded()
    {
        // Show ticked/green state first so the player can see task completion.
        RefreshUi();

        if (!IsDisplayedPartComplete())
        {
            if (_advancePartRoutine != null)
            {
                StopCoroutine(_advancePartRoutine);
                _advancePartRoutine = null;
            }

            SyncMissionRootVisibility(GameManager.Instance != null ? GameManager.Instance.State : GameState.Building);
            return;
        }

        if (_completedPartHoldSeconds <= 0f)
        {
            if (_advancePartRoutine != null)
            {
                StopCoroutine(_advancePartRoutine);
                _advancePartRoutine = null;
            }

            while (IsDisplayedPartComplete() && TryAdvanceDisplayedPart())
            {
            }

            RefreshUi();
            SyncMissionRootVisibility(GameManager.Instance != null ? GameManager.Instance.State : GameState.Building);
            return;
        }

        if (_advancePartRoutine == null)
            _advancePartRoutine = StartCoroutine(AdvanceCompletedPartAfterHold());
    }

    private IEnumerator AdvanceCompletedPartAfterHold()
    {
        float holdSeconds = Mathf.Max(0f, _completedPartHoldSeconds);

        if (holdSeconds > 0f)
            yield return new WaitForSeconds(holdSeconds);

        _advancePartRoutine = null;

        while (IsDisplayedPartComplete() && TryAdvanceDisplayedPart())
        {
        }

        RefreshUi();
        SyncMissionRootVisibility(GameManager.Instance != null ? GameManager.Instance.State : GameState.Building);
    }

    private void RefreshUi()
    {
        EnsureInitialized();

        if (_missionCatalog == null)
            return;

        BindOpenedTextRefs();

        if (!TryResolveDisplayedPart(out int partIndex, out MissionPartDefinition part))
        {
            _displayedPartIndex = -1;

            if (_missionRoot != null)
                _missionRoot.SetActive(false);

            return;
        }

        _displayedPartIndex = partIndex;

        if (_titleText != null)
            _titleText.text = string.IsNullOrEmpty(part.title) ? "任务" : part.title;

        MissionTaskDefinition[] tasks = part.tasks ?? Array.Empty<MissionTaskDefinition>();
        int taskCount = Mathf.Min(tasks.Length, MissionCatalog.MaxTasksPerPart);

        for (int i = 0; i < _taskTexts.Length; i++)
        {
            TextMeshProUGUI taskText = _taskTexts[i];

            if (taskText == null)
                continue;

            if (i >= taskCount)
            {
                taskText.gameObject.SetActive(false);
                continue;
            }

            MissionTaskDefinition task = tasks[i];
            bool complete = IsTaskComplete(task);
            string description = task != null && !string.IsNullOrEmpty(task.description)
                ? task.description
                : "任务";

            taskText.gameObject.SetActive(true);
            taskText.text = $"{i + 1}. {description}";
            taskText.color = complete ? _completeTaskColor : _incompleteTaskColor;
        }
    }

    private void SyncMissionRootVisibility(GameState state)
    {
        if (_missionRoot == null)
            return;

        bool hasDisplayedMission = TryResolveDisplayedPart(out _, out _);
        bool sceneAllowsMissionUi = RestaurantSceneMode.IsMainScene
            || RestaurantSceneMode.IsCompetitorScene
            || RestaurantSceneMode.IsFutureScene;

        bool stateAllowsMissionUi = RestaurantSceneMode.IsFutureScene
            || state == GameState.Building
            || state == GameState.Business;

        bool shouldShow = sceneAllowsMissionUi && stateAllowsMissionUi && hasDisplayedMission;

        bool wasActive = _missionRoot.activeSelf;
        _missionRoot.SetActive(shouldShow);

        if (!shouldShow)
            return;

        // Freshly shown mission UI starts closed.
        if (!wasActive)
            SetPanelOpen(false);
        else
            ApplyPanelVisibility();
    }

    /// <summary>
    /// Picks which mission card to show for the active scene.
    /// Competitor: only mission 7 (steal). Future: only mission 8 (purchase).
    /// Main: linear 1-6, then mission 9 (sell). Missions 7/8 never appear on Main.
    /// </summary>
    private bool TryResolveDisplayedPart(out int partIndex, out MissionPartDefinition part)
    {
        partIndex = -1;
        part = null;

        if (_missionCatalog == null)
            return false;

        // Mission 8 — Future scene only.
        if (RestaurantSceneMode.IsFutureScene)
        {
            if (PlayerProfileStorage.IsMissionFutureUnlockedForCurrentPlayer()
                && !PlayerProfileStorage.IsMissionFuturePurchaseCompletedForCurrentPlayer())
            {
                partIndex = MissionCatalog.PurchaseModernDishMissionPartIndex;
                return _missionCatalog.TryGetPart(partIndex, out part);
            }

            return false;
        }

        // Mission 7 — Competitor scene only (no other missions in this scene).
        if (RestaurantSceneMode.IsCompetitorScene)
        {
            if (PlayerProfileStorage.IsMissionStealUnlockedForCurrentPlayer()
                && !PlayerProfileStorage.IsMissionStealCompletedForCurrentPlayer())
            {
                partIndex = MissionCatalog.StealCustomersMissionPartIndex;
                return _missionCatalog.TryGetPart(partIndex, out part);
            }

            return false;
        }

        // Main scene (and any non-Future/Competitor): never show missions 7 or 8.
        if (!RestaurantSceneMode.IsMainScene)
            return false;

        // Early linear missions (1-5 / indices 0-4).
        if (_currentPartIndex < MissionCatalog.ServeVipMissionPartIndex)
        {
            partIndex = _currentPartIndex;
            return _missionCatalog.TryGetPart(partIndex, out part);
        }

        // Mission 6 — serve VIP (Main only).
        if (!PlayerProfileStorage.IsMissionServeVipCompletedForCurrentPlayer())
        {
            partIndex = MissionCatalog.ServeVipMissionPartIndex;
            return _missionCatalog.TryGetPart(partIndex, out part);
        }

        // Mission 9 — sell to VIP (Main only; after Future purchase).
        if (PlayerProfileStorage.IsMissionFuturePurchaseCompletedForCurrentPlayer()
            && !PlayerProfileStorage.IsMissionSellToVipCompletedForCurrentPlayer())
        {
            partIndex = MissionCatalog.SellDishToVipMissionPartIndex;
            return _missionCatalog.TryGetPart(partIndex, out part);
        }

        return false;
    }

    private void WirePanelButtons()
    {
        if (_buttonsWired)
            return;

        if (_openButton != null)
            _openButton.onClick.AddListener(HandleOpenButtonClicked);

        if (_closeButton != null)
            _closeButton.onClick.AddListener(HandleCloseButtonClicked);

        _buttonsWired = _openButton != null || _closeButton != null;
    }

    private void UnwirePanelButtons()
    {
        if (!_buttonsWired)
            return;

        if (_openButton != null)
            _openButton.onClick.RemoveListener(HandleOpenButtonClicked);

        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(HandleCloseButtonClicked);

        _buttonsWired = false;
    }

    private void HandleOpenButtonClicked()
    {
        BindOpenedTextRefs();
        RefreshUi();
        SetPanelOpen(true);
    }

    private void HandleCloseButtonClicked()
    {
        SetPanelOpen(false);
    }

    private void SetPanelOpen(bool open)
    {
        _isPanelOpen = open;
        ApplyPanelVisibility();
    }

    private void ApplyPanelVisibility()
    {
        if (_closedUiRoot != null)
        {
            _closedUiRoot.SetActive(!_isPanelOpen);
            _closedUiRoot.transform.localScale = Vector3.one;
        }

        if (_openedUiRoot != null)
            _openedUiRoot.SetActive(_isPanelOpen);

        if (_openButton != null)
            _openButton.transform.localScale = Vector3.one;
    }

    private void ClampCurrentPartIndex()
    {
        if (_missionCatalog == null || _missionCatalog.PartCount <= 0)
            return;

        // Cap at ServeVip + 1 so late conditional missions do not use linear watermark alone.
        int maxIndex = MissionCatalog.ServeVipMissionPartIndex + 1;
        int clamped = _currentPartIndex;

        if (clamped < 0)
            clamped = 0;
        else if (clamped > maxIndex)
            clamped = maxIndex;

        // If VIP prep already finished in an older save, keep watermark past prep.
        if (PlayerProfileStorage.IsMissionServeVipCompletedForCurrentPlayer()
            && clamped < MissionCatalog.ServeVipMissionPartIndex + 1)
        {
            clamped = MissionCatalog.ServeVipMissionPartIndex + 1;
        }

        if (clamped == _currentPartIndex)
            return;

        _currentPartIndex = clamped;
        PlayerProfileStorage.SetMainSceneMissionPartIndexForCurrentPlayer(_currentPartIndex);
    }

    private bool TryAdvanceDisplayedPart()
    {
        EnsureInitialized();

        if (_missionCatalog == null || _isAdvancingPart)
            return false;

        if (!TryResolveDisplayedPart(out int displayedIndex, out MissionPartDefinition completedPart))
            return false;

        if (!IsDisplayedPartComplete())
            return false;

        _isAdvancingPart = true;

        try
        {
            if (completedPart.revealSecondFloorWhenComplete)
                GameEvents.RaiseMainSceneSecondFloorRevealRequested();

            // Early linear missions still advance the watermark one-by-one.
            if (displayedIndex < MissionCatalog.ServeVipMissionPartIndex)
            {
                int nextIndex = displayedIndex + 1;
                bool hasNextPart = _missionCatalog.TryGetPart(nextIndex, out _);
                _currentPartIndex = hasNextPart ? nextIndex : MissionCatalog.ServeVipMissionPartIndex;
                PlayerProfileStorage.SetMainSceneMissionPartIndexForCurrentPlayer(_currentPartIndex);
                GameEvents.RaiseMissionPartChanged(_currentPartIndex);
                GameEvents.RaiseMissionPartCompleted(displayedIndex);
                return true;
            }

            // Late missions are completed via Notify* flags; advancing just refreshes display.
            if (displayedIndex == MissionCatalog.ServeVipMissionPartIndex)
            {
                PlayerProfileStorage.SetMissionServeVipCompletedForCurrentPlayer();
                _currentPartIndex = MissionCatalog.ServeVipMissionPartIndex + 1;
                PlayerProfileStorage.SetMainSceneMissionPartIndexForCurrentPlayer(_currentPartIndex);
            }
            else if (displayedIndex == MissionCatalog.StealCustomersMissionPartIndex)
            {
                PlayerProfileStorage.SetMissionStealCompletedForCurrentPlayer();
            }
            else if (displayedIndex == MissionCatalog.PurchaseModernDishMissionPartIndex)
            {
                PlayerProfileStorage.SetMissionFuturePurchaseCompletedForCurrentPlayer();
            }
            else if (displayedIndex == MissionCatalog.SellDishToVipMissionPartIndex)
            {
                PlayerProfileStorage.SetMissionSellToVipCompletedForCurrentPlayer();
            }

            GameEvents.RaiseMissionPartCompleted(displayedIndex);
            GameEvents.RaiseMissionPartChanged(_currentPartIndex);

            // Return true only if another mission is immediately available to show.
            return TryResolveDisplayedPart(out _, out _);
        }
        finally
        {
            _isAdvancingPart = false;
        }
    }

    private bool IsDisplayedPartComplete()
    {
        EnsureInitialized();

        if (!TryResolveDisplayedPart(out _, out MissionPartDefinition part))
            return false;

        MissionTaskDefinition[] tasks = part.tasks;

        if (tasks == null || tasks.Length == 0)
            return true;

        for (int i = 0; i < tasks.Length; i++)
        {
            if (!IsTaskComplete(tasks[i]))
                return false;
        }

        return true;
    }

    private bool IsTaskComplete(MissionTaskDefinition task)
    {
        if (task == null)
            return true;

        int required = Mathf.Max(1, task.requiredCount);

        switch (task.taskKind)
        {
            case MissionTaskKind.Hire:
                return GetHiredCount(task.requiredWorkerType) >= required;
            case MissionTaskKind.OpenBusiness:
                return _openBusinessCompletions >= required;
            case MissionTaskKind.ServeVip:
                return PlayerProfileStorage.IsMissionServeVipCompletedForCurrentPlayer();
            case MissionTaskKind.StealCustomers:
                return PlayerProfileStorage.IsMissionStealCompletedForCurrentPlayer();
            case MissionTaskKind.PurchaseModernDish:
                return PlayerProfileStorage.IsMissionFuturePurchaseCompletedForCurrentPlayer();
            case MissionTaskKind.SellDishToVip:
                return PlayerProfileStorage.IsMissionSellToVipCompletedForCurrentPlayer();
            default:
                return GetPlacedCount(task.requiredType) >= required;
        }
    }

    private void SetPlacedCount(PlaceableType type, int count)
    {
        int index = (int)type;

        if (index < 0 || index >= _placedCounts.Length)
            return;

        _placedCounts[index] = Mathf.Max(0, count);
    }

    private int GetPlacedCount(PlaceableType type)
    {
        int index = (int)type;

        if (index < 0 || index >= _placedCounts.Length)
            return 0;

        return _placedCounts[index];
    }

    private void SetHiredCount(WorkerType type, int count)
    {
        int index = (int)type;

        if (index < 0 || index >= _hiredCounts.Length)
            return;

        _hiredCounts[index] = Mathf.Max(0, count);
    }

    private int GetHiredCount(WorkerType type)
    {
        int index = (int)type;

        if (index < 0 || index >= _hiredCounts.Length)
            return 0;

        return _hiredCounts[index];
    }

    private void CacheReferences()
    {
        if (_missionRoot == null)
            _missionRoot = FindMissionRoot();

        if (_missionRoot == null)
            return;

        Transform missionTransform = _missionRoot.transform;

        if (_closedUiRoot == null)
        {
            Transform closed = FindChildRecursive(missionTransform, ClosedRootName);
            _closedUiRoot = closed != null ? closed.gameObject : null;
        }

        if (_openedUiRoot == null)
        {
            Transform opened = FindChildRecursive(missionTransform, OpenedRootName);
            _openedUiRoot = opened != null ? opened.gameObject : null;
        }

        // Always bind Title / Task texts from Opened so scene copies stay correct.
        BindOpenedTextRefs();

        if (_openButton == null)
            _openButton = ResolveOpenButton(missionTransform);

        if (_closeButton == null)
        {
            Transform closeButtonTransform = _openedUiRoot != null
                ? FindChildRecursive(_openedUiRoot.transform, CloseButtonName)
                : FindChildRecursive(missionTransform, CloseButtonName);
            _closeButton = closeButtonTransform != null
                ? closeButtonTransform.GetComponent<Button>()
                : null;
        }
    }

    private void BindOpenedTextRefs()
    {
        Transform contentRoot = _openedUiRoot != null
            ? _openedUiRoot.transform
            : (_missionRoot != null ? _missionRoot.transform : null);

        if (contentRoot == null)
            return;

        Transform title = FindChildRecursive(contentRoot, TitleName);
        if (title != null)
            _titleText = title.GetComponent<TextMeshProUGUI>();

        EnsureTaskTextArray();

        for (int i = 0; i < MissionCatalog.MaxTasksPerPart; i++)
        {
            Transform taskTransform = FindChildRecursive(contentRoot, $"Task ({i + 1})");
            if (taskTransform == null && i == 0)
                taskTransform = FindChildRecursive(contentRoot, "Task");

            if (taskTransform != null)
                _taskTexts[i] = taskTransform.GetComponent<TextMeshProUGUI>();
        }
    }

    private Button ResolveOpenButton(Transform missionTransform)
    {
        // Prefer the Closed Bg button (current setup), then legacy Open Button, then any Closed button.
        if (_closedUiRoot != null)
        {
            Transform closedBg = FindChildRecursive(_closedUiRoot.transform, ClosedBgName);
            Button bgButton = closedBg != null ? closedBg.GetComponent<Button>() : null;
            if (bgButton != null)
                return bgButton;

            Transform openButtonTransform = FindChildRecursive(_closedUiRoot.transform, OpenButtonName);
            Button namedButton = openButtonTransform != null
                ? openButtonTransform.GetComponent<Button>()
                : null;
            if (namedButton != null)
                return namedButton;

            Button closedButton = _closedUiRoot.GetComponentInChildren<Button>(true);
            if (closedButton != null)
                return closedButton;
        }

        Transform fallbackOpen = FindChildRecursive(missionTransform, OpenButtonName);
        return fallbackOpen != null ? fallbackOpen.GetComponent<Button>() : null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        Transform direct = root.Find(childName);
        if (direct != null)
            return direct;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void EnsureTaskTextArray()
    {
        if (_taskTexts != null && _taskTexts.Length == MissionCatalog.MaxTasksPerPart)
            return;

        TextMeshProUGUI[] resized = new TextMeshProUGUI[MissionCatalog.MaxTasksPerPart];

        if (_taskTexts != null)
        {
            int copyCount = Mathf.Min(_taskTexts.Length, resized.Length);

            for (int i = 0; i < copyCount; i++)
                resized[i] = _taskTexts[i];
        }

        _taskTexts = resized;
    }

    private static GameObject FindMissionRoot()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];

            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
                continue;

            Transform[] children = canvas.GetComponentsInChildren<Transform>(true);

            for (int childIndex = 0; childIndex < children.Length; childIndex++)
            {
                Transform child = children[childIndex];

                if (child != null && string.Equals(child.name, MissionRootName, StringComparison.OrdinalIgnoreCase))
                    return child.gameObject;
            }
        }

        return GameObject.Find(MissionRootName);
    }
}
