using System;
using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(160)]
public class MissionUiController : MonoBehaviour
{
    private const string MissionRootName = "Mission";
    private const string TitleName = "Title";

    [SerializeField] private MissionCatalog _missionCatalog;
    [SerializeField] private GameObject _missionRoot;
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

        if (_initialized)
            return;

        _currentPartIndex = PlayerProfileStorage.GetMainSceneMissionPartIndexForCurrentPlayer();
        ClampCurrentPartIndex();

        if (_currentPartIndex > MissionCatalog.OpenBusinessMissionPartIndex
            || PlayerProfileStorage.HasMainSceneBusinessStartedForCurrentPlayer())
        {
            _openBusinessCompletions = 1;
        }

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
    }

    private void OnDisable()
    {
        GameEvents.StateChanged -= HandleStateChanged;

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

    public void NotifyWorkerHired(WorkerType type)
    {
        int index = (int)type;

        if (index < 0 || index >= _hiredCounts.Length)
            return;

        _hiredCounts[index]++;
        RefreshUiAndAdvanceIfNeeded();
    }

    public void SetPlacedCounts(int receptions, int stoves, int tables)
    {
        SyncPlacedCounts(receptions, stoves, tables);
        RefreshUiAndAdvanceIfNeeded();
    }

    public void SyncPlacedCounts(int receptions, int stoves, int tables)
    {
        SetPlacedCount(PlaceableType.Reception, receptions);
        SetPlacedCount(PlaceableType.Stove, stoves);
        SetPlacedCount(PlaceableType.Table, tables);
        RefreshUi();
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

    public void SetOpenBusinessCompletions(int count)
    {
        _openBusinessCompletions = Mathf.Max(0, count);
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

        _missionRoot.SetActive(shouldShow);
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

        if (_titleText == null)
        {
            Transform title = _missionRoot.transform.Find(TitleName);
            _titleText = title != null ? title.GetComponent<TextMeshProUGUI>() : null;
        }

        EnsureTaskTextArray();

        for (int i = 0; i < MissionCatalog.MaxTasksPerPart; i++)
        {
            if (_taskTexts[i] != null)
                continue;

            Transform taskTransform = _missionRoot.transform.Find($"Task ({i + 1})");

            if (taskTransform != null)
                _taskTexts[i] = taskTransform.GetComponent<TextMeshProUGUI>();
        }
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
