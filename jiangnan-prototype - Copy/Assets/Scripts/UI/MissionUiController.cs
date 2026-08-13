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
    private Coroutine _advancePartRoutine;
    private bool _isAdvancingPart;
    private bool _initialized;
    private bool _isPanelOpen;
    private bool _buttonsWired;

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
            return;

        _currentPartIndex = PlayerProfileStorage.GetMainSceneMissionPartIndexForCurrentPlayer();
        ClampCurrentPartIndex();

        if (_currentPartIndex > MissionCatalog.OpenBusinessMissionPartIndex
            || PlayerProfileStorage.HasMainSceneBusinessStartedForCurrentPlayer())
        {
            _openBusinessCompletions = 1;
        }

        // Default: mission panel starts closed.
        SetPanelOpen(false);
        _initialized = true;
    }

    public void Initialize(MissionCatalog catalog = null)
    {
        EnsureInitialized(catalog);
        RefreshFromPlacedCounts();
    }

    private void OnEnable()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return;

        GameEvents.StateChanged += HandleStateChanged;
        WirePanelButtons();
    }

    private void OnDisable()
    {
        GameEvents.StateChanged -= HandleStateChanged;
        UnwirePanelButtons();

        if (_advancePartRoutine != null)
        {
            StopCoroutine(_advancePartRoutine);
            _advancePartRoutine = null;

            while (IsCurrentPartComplete() && TryAdvancePart())
            {
            }
        }
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

        if (!IsCurrentPartComplete())
        {
            if (_advancePartRoutine != null)
            {
                StopCoroutine(_advancePartRoutine);
                _advancePartRoutine = null;
            }

            return;
        }

        if (_completedPartHoldSeconds <= 0f)
        {
            if (_advancePartRoutine != null)
            {
                StopCoroutine(_advancePartRoutine);
                _advancePartRoutine = null;
            }

            while (IsCurrentPartComplete() && TryAdvancePart())
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

        while (IsCurrentPartComplete() && TryAdvancePart())
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

        if (!_missionCatalog.TryGetPart(_currentPartIndex, out MissionPartDefinition part))
        {
            if (_missionRoot != null)
                _missionRoot.SetActive(false);

            return;
        }

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

        bool hasActivePart = _missionCatalog != null
            && _missionCatalog.TryGetPart(_currentPartIndex, out _);
        bool shouldShow = RestaurantSceneMode.IsMainScene
            && hasActivePart
            && !AreAllMissionsComplete()
            && (state == GameState.Building || state == GameState.Business);

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

    private bool AreAllMissionsComplete()
    {
        return _missionCatalog != null
            && _missionCatalog.PartCount > 0
            && _currentPartIndex >= _missionCatalog.PartCount;
    }

    private void ClampCurrentPartIndex()
    {
        if (_missionCatalog == null || _missionCatalog.PartCount <= 0)
            return;

        // PartCount is the sentinel for "all missions finished".
        int maxIndex = _missionCatalog.PartCount;
        int clamped = _currentPartIndex;

        if (clamped < 0)
            clamped = 0;
        else if (clamped > maxIndex)
            clamped = maxIndex;

        if (clamped == _currentPartIndex)
            return;

        _currentPartIndex = clamped;
        PlayerProfileStorage.SetMainSceneMissionPartIndexForCurrentPlayer(_currentPartIndex);
    }

    private bool TryAdvancePart()
    {
        EnsureInitialized();

        if (_missionCatalog == null || _isAdvancingPart)
            return false;

        if (AreAllMissionsComplete())
            return false;

        int completedIndex = _currentPartIndex;
        int nextIndex = completedIndex + 1;

        if (!_missionCatalog.TryGetPart(completedIndex, out MissionPartDefinition completedPart))
            return false;

        _isAdvancingPart = true;

        try
        {
            if (completedPart.revealSecondFloorWhenComplete)
                GameEvents.RaiseMainSceneSecondFloorRevealRequested();

            bool hasNextPart = _missionCatalog.TryGetPart(nextIndex, out _);

            if (hasNextPart)
            {
                _currentPartIndex = nextIndex;
            }
            else
            {
                // Move past the last part so the mission UI can hide.
                _currentPartIndex = _missionCatalog.PartCount;
            }

            PlayerProfileStorage.SetMainSceneMissionPartIndexForCurrentPlayer(_currentPartIndex);
            GameEvents.RaiseMissionPartChanged(_currentPartIndex);
            GameEvents.RaiseMissionPartCompleted(completedIndex);

            return hasNextPart;
        }
        finally
        {
            _isAdvancingPart = false;
        }
    }

    private bool IsCurrentPartComplete()
    {
        EnsureInitialized();

        if (AreAllMissionsComplete())
            return false;

        if (_missionCatalog == null || !_missionCatalog.TryGetPart(_currentPartIndex, out MissionPartDefinition part))
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

        Transform contentRoot = _openedUiRoot != null ? _openedUiRoot.transform : missionTransform;

        if (_titleText == null)
        {
            Transform title = FindChildRecursive(contentRoot, TitleName);
            _titleText = title != null ? title.GetComponent<TextMeshProUGUI>() : null;
        }

        EnsureTaskTextArray();

        for (int i = 0; i < MissionCatalog.MaxTasksPerPart; i++)
        {
            if (_taskTexts[i] != null)
                continue;

            Transform taskTransform = FindChildRecursive(contentRoot, $"Task ({i + 1})");

            if (taskTransform != null)
                _taskTexts[i] = taskTransform.GetComponent<TextMeshProUGUI>();
        }

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
