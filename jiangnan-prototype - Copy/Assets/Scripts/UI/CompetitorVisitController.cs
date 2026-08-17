using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Competitor-scene visit loop: steal UI on queuing customers, stay timer, chased banner, return to town.
/// </summary>
[DisallowMultipleComponent]
public class CompetitorVisitController : MonoBehaviour
{
    private const string StealCustomersUiName = "Steal Customers";
    private const string StayTimerUiName = "Stay Timer";
    private const string StayTimerFillName = "Timer";
    private const string StayTimerCountdownName = "Countdown";
    private const string ChasedUiName = "Chased UI";
    private const string CompetitorFaceUiName = "Competitor Face";
    private const string StealResultTextUiName = "Steal Result";
    private const string NameBgUiName = "Name Bg";
    private const string ProfilePicUiName = "Profile Pic";
    private const string StatusTextUiName = "Status Text";
    private const string StolenUiRootName = "Stolen UI";
    private const string StolenTextNamePrefix = "Stolen Text";
    private const string TownButtonUiName = "Town Button";
    private const string OnlineStatusLabel = "Online";
    private const string OfflineStatusLabel = "Offline";
    private const string StealResultFailLabel = "只抢几个客人！";
    private const string StealResultSuccessLabel = "成功抢顾客！";

    [Header("Stay Timer")]
    [SerializeField] private float _stayDurationSeconds = 15f;
    [SerializeField] private float _chaseThresholdMinSeconds = 1f;
    [SerializeField] private float _chaseThresholdMaxSeconds = 5f;
    [SerializeField] private float _chasedUiHoldSeconds = 3f;
    [SerializeField] private float _chasedUiPulseMinScale = 0.97f;
    [SerializeField] private float _chasedUiPulseMaxScale = 1.03f;
    [SerializeField] private float _chasedUiPulseSpeed = 2f;
    [SerializeField] private float _stayTimerPulseMinScale = 0.95f;
    [SerializeField] private float _stayTimerPulseMaxScale = 1.05f;
    [SerializeField] private float _stayTimerPulseSpeed = 2f;
    [Tooltip("Profile pic pulse while the competitor status reads Online.")]
    [SerializeField] private float _profilePicPulseMinScale = 0.95f;
    [SerializeField] private float _profilePicPulseMaxScale = 1.05f;
    [SerializeField] private float _profilePicPulseSpeed = 6f;
    [SerializeField] private Color _onlineStatusColor = new Color(0.2f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color _offlineStatusColor = new Color(0.94509804f, 0.3882353f, 0.3882353f, 1f);
    [SerializeField] private Color _stealResultSuccessColor = new Color(0.2f, 0.85f, 0.35f, 1f);
    [SerializeField] private Color _stealResultFailColor = new Color(0.94509804f, 0.3882353f, 0.3882353f, 1f);

    [Header("Steal Customers Pulse")]
    [SerializeField] private float _stealPulseMinScale = 0.9f;
    [SerializeField] private float _stealPulseMaxScale = 1.1f;
    [SerializeField] private float _stealPulseSpeed = 10f;

    [Header("Stolen Text Feedback")]
    [SerializeField] private float _stolenTextHoldSeconds = 1.25f;
    [SerializeField] private float _stolenTextFadeOutSeconds = 0.4f;
    [SerializeField] private float _stolenTextFloatDistance = 60f;

    private RectTransform _stealTemplateRoot;
    private Button _stealTemplateButton;
    private RectTransform _stayTimerRoot;
    private Image _stayTimerFillImage;
    private TextMeshProUGUI _stayTimerCountdownText;
    private RectTransform _chasedUiRoot;
    private Image _chasedCompetitorFaceImage;
    private TextMeshProUGUI _stealResultText;
    private Image _profilePicImage;
    private TextMeshProUGUI _statusText;
    private bool _competitorOnline;
    private bool _competitorOnlineLatched;
    private RectTransform _stolenUiRoot;
    private RectTransform _stolenTextTemplate;
    private GameObject _townButtonRoot;

    private Canvas _screenCanvas;
    private RectTransform _canvasRect;
    private Camera _worldCamera;

    private readonly Dictionary<Customer, StealUiEntry> _activeStealUis = new();
    private readonly List<Customer> _stealUiScratch = new();
    private readonly List<RectTransform> _stolenTextPool = new();
    private readonly List<StolenTextEntry> _activeStolenTexts = new();

    private float _stayDuration;
    private float _stayRemaining;
    private float _chaseThreshold;
    private bool _stayTimerRunning;
    private bool _chaseSequenceStarted;
    private Coroutine _chaseRoutine;

    private struct StealUiEntry
    {
        public RectTransform UiRoot;
        public Button Button;
        public Customer Customer;
        public UnityEngine.Events.UnityAction ClickListener;
    }

    private sealed class StolenTextEntry
    {
        public RectTransform UiRoot;
        public Graphic[] Graphics;
        public Color[] TargetColors;
        public Vector2 StartAnchoredPosition;
        public Coroutine Routine;
    }

    public void ConfigureStayDuration(float durationSeconds)
    {
        _stayDurationSeconds = Mathf.Max(0.01f, durationSeconds);

        // If the timer already started, retarget remaining time to the new duration.
        if (_stayTimerRunning && !_chaseSequenceStarted)
        {
            float elapsed = Mathf.Max(0f, _stayDuration - _stayRemaining);
            _stayDuration = _stayDurationSeconds;
            _stayRemaining = Mathf.Max(0f, _stayDuration - elapsed);
            ApplyStayTimerVisuals();
        }
    }

    public void ConfigureChaseThreshold(float minSeconds, float maxSeconds)
    {
        _chaseThresholdMinSeconds = Mathf.Max(0f, minSeconds);
        _chaseThresholdMaxSeconds = Mathf.Max(_chaseThresholdMinSeconds, maxSeconds);

        if (_stayTimerRunning && !_chaseSequenceStarted)
        {
            _chaseThreshold = Random.Range(_chaseThresholdMinSeconds, _chaseThresholdMaxSeconds);
            ApplyCompetitorStatusVisuals();
        }
    }

    private void OnEnable()
    {
        if (!RestaurantSceneMode.IsCompetitorScene)
            return;

        GameEvents.CustomerStateChanged += HandleCustomerStateChanged;
        GameEvents.BusinessSessionStarted += HandleBusinessSessionStarted;
    }

    private void OnDisable()
    {
        GameEvents.CustomerStateChanged -= HandleCustomerStateChanged;
        GameEvents.BusinessSessionStarted -= HandleBusinessSessionStarted;
    }

    private void Start()
    {
        if (!RestaurantSceneMode.IsCompetitorScene)
        {
            enabled = false;
            return;
        }

        CacheUi();
        HideVisitChrome();
        ApplyCompetitorProfileUi();
        ApplyCompetitorStatusVisuals(forceOffline: true);
        SyncStealUiForActiveQueuedCustomers();
    }

    private void SyncStealUiForActiveQueuedCustomers()
    {
        if (CustomerManager.Instance == null)
            return;

        IReadOnlyList<Customer> activeCustomers = CustomerManager.Instance.ActiveCustomers;
        for (int i = 0; i < activeCustomers.Count; i++)
        {
            Customer customer = activeCustomers[i];
            if (customer != null && customer.State == CustomerState.Queue)
                ShowStealUi(customer);
        }
    }

    private void OnDestroy()
    {
        if (_chaseRoutine != null)
        {
            StopCoroutine(_chaseRoutine);
            _chaseRoutine = null;
        }

        AudioManager.StopLooping(SfxId.Alert);
        ClearStealUis();
        ClearStolenTexts();
    }

    private void LateUpdate()
    {
        if (!RestaurantSceneMode.IsCompetitorScene)
            return;

        if (_chaseSequenceStarted)
        {
            UpdateChasedUiPulse();
            UpdateProfilePicPulse();
            return;
        }

        UpdateStayTimer();
        UpdateStayTimerPulse();
        UpdateProfilePicPulse();
        SyncStealUisFromActiveCustomers();
        UpdateStealUiPositions();
        UpdateStealUiPulses();
    }

    private void HandleBusinessSessionStarted()
    {
        if (!RestaurantSceneMode.IsCompetitorScene)
            return;

        // Stay timer arms only after the first steal tap.
        SyncStealUisFromActiveCustomers();
    }

    private void HandleCustomerStateChanged(Customer customer, CustomerState state)
    {
        if (!RestaurantSceneMode.IsCompetitorScene || customer == null || _chaseSequenceStarted)
            return;

        if (state == CustomerState.Queue)
            ShowStealUi(customer);
        else
            HideStealUi(customer);
    }

    /// <summary>
    /// Pool resets customers to Queue without raising CustomerStateChanged, so SetState(Queue)
    /// is often a no-op. Poll active customers so steal UI still appears.
    /// </summary>
    private void SyncStealUisFromActiveCustomers()
    {
        if (_stealTemplateRoot == null)
            CacheUi();

        if (_stealTemplateRoot == null || CustomerManager.Instance == null)
            return;

        IReadOnlyList<Customer> activeCustomers = CustomerManager.Instance.ActiveCustomers;
        for (int i = 0; i < activeCustomers.Count; i++)
        {
            Customer customer = activeCustomers[i];
            if (customer == null)
                continue;

            if (customer.State == CustomerState.Queue
                && !customer.IsImmuneToCompetitorSteal
                && !customer.WasStolenByCompetitor)
            {
                ShowStealUi(customer);
            }
            else if (_activeStealUis.ContainsKey(customer))
            {
                HideStealUi(customer);
            }
        }

        // Drop entries for customers that left the active list.
        _stealUiScratch.Clear();
        _stealUiScratch.AddRange(_activeStealUis.Keys);
        for (int i = 0; i < _stealUiScratch.Count; i++)
        {
            Customer customer = _stealUiScratch[i];
            if (customer == null || !ContainsCustomer(activeCustomers, customer))
                HideStealUi(customer);
        }
    }

    private static bool ContainsCustomer(IReadOnlyList<Customer> customers, Customer target)
    {
        for (int i = 0; i < customers.Count; i++)
        {
            if (customers[i] == target)
                return true;
        }

        return false;
    }

    private void CacheUi()
    {
        EnsureScreenUiCaches();

        if (_stealTemplateRoot == null)
        {
            GameObject stealObject = FindSceneUiObject(StealCustomersUiName);
            if (stealObject != null)
            {
                _stealTemplateRoot = stealObject.transform as RectTransform;
                _stealTemplateButton = stealObject.GetComponent<Button>()
                    ?? stealObject.GetComponentInChildren<Button>(true);
            }
        }

        if (_stayTimerRoot == null)
        {
            GameObject stayObject = FindSceneUiObject(StayTimerUiName);
            if (stayObject != null)
                _stayTimerRoot = stayObject.transform as RectTransform;
        }

        if (_stayTimerRoot != null)
        {
            if (_stayTimerFillImage == null)
            {
                Transform fillTransform = FindChildTransform(_stayTimerRoot, StayTimerFillName);
                if (fillTransform != null)
                    _stayTimerFillImage = fillTransform.GetComponent<Image>();

                if (_stayTimerFillImage == null)
                    _stayTimerFillImage = _stayTimerRoot.GetComponent<Image>();
            }

            if (_stayTimerCountdownText == null)
            {
                Transform countdownTransform = FindChildTransform(_stayTimerRoot, StayTimerCountdownName);
                if (countdownTransform != null)
                    _stayTimerCountdownText = countdownTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_chasedUiRoot == null)
        {
            GameObject chasedObject = FindSceneUiObject(ChasedUiName);
            if (chasedObject != null)
                _chasedUiRoot = chasedObject.transform as RectTransform;
        }

        if (_chasedUiRoot != null && _chasedCompetitorFaceImage == null)
        {
            Transform faceTransform = FindChildTransform(_chasedUiRoot, CompetitorFaceUiName);
            if (faceTransform != null)
                _chasedCompetitorFaceImage = faceTransform.GetComponent<Image>();
        }

        if (_chasedUiRoot != null && _stealResultText == null)
        {
            Transform stealResultTransform = FindChildTransform(_chasedUiRoot, StealResultTextUiName);
            if (stealResultTransform != null)
                _stealResultText = stealResultTransform.GetComponent<TextMeshProUGUI>();
        }

        if (_profilePicImage == null || _statusText == null)
        {
            GameObject nameBgObject = FindSceneUiObject(NameBgUiName);
            Transform nameBg = nameBgObject != null ? nameBgObject.transform : null;

            if (_profilePicImage == null && nameBg != null)
            {
                Transform profilePicTransform = FindChildTransform(nameBg, ProfilePicUiName);
                if (profilePicTransform != null)
                    _profilePicImage = profilePicTransform.GetComponent<Image>();
            }

            if (_statusText == null)
            {
                Transform statusRoot = _profilePicImage != null
                    ? _profilePicImage.transform
                    : nameBg;

                if (statusRoot != null)
                {
                    Transform statusTransform = FindChildTransform(statusRoot, StatusTextUiName);
                    if (statusTransform != null)
                        _statusText = statusTransform.GetComponent<TextMeshProUGUI>();
                }
            }
        }

        if (_townButtonRoot == null)
            _townButtonRoot = FindSceneUiObject(TownButtonUiName);

        CacheStolenTextPool();
    }

    private void CacheStolenTextPool()
    {
        if (_stolenUiRoot == null)
        {
            GameObject stolenRootObject = FindSceneUiObject(StolenUiRootName);
            if (stolenRootObject != null)
                _stolenUiRoot = stolenRootObject.transform as RectTransform;
        }

        if (_stolenUiRoot == null)
            return;

        _stolenUiRoot.gameObject.SetActive(true);

        for (int i = 0; i < _stolenUiRoot.childCount; i++)
        {
            Transform child = _stolenUiRoot.GetChild(i);
            if (child == null || !(child is RectTransform stolenText))
                continue;

            if (!IsStolenTextName(stolenText.name))
                continue;

            if (_stolenTextTemplate == null)
                _stolenTextTemplate = stolenText;

            if (IsStolenTextActive(stolenText) || _stolenTextPool.Contains(stolenText))
                continue;

            PrepareStolenTextForPool(stolenText);
            _stolenTextPool.Add(stolenText);
        }
    }

    private static bool IsStolenTextName(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.StartsWith(StolenTextNamePrefix, System.StringComparison.Ordinal);
    }

    private bool IsStolenTextActive(RectTransform uiRoot)
    {
        for (int i = 0; i < _activeStolenTexts.Count; i++)
        {
            if (_activeStolenTexts[i] != null && _activeStolenTexts[i].UiRoot == uiRoot)
                return true;
        }

        return false;
    }

    private static void PrepareStolenTextForPool(RectTransform uiRoot)
    {
        if (uiRoot == null)
            return;

        Graphic[] graphics = uiRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = false;
        }

        UiGraphicFade.RestoreAlpha(graphics);
        uiRoot.localScale = Vector3.one;
        uiRoot.gameObject.SetActive(false);
    }

    private void HideVisitChrome()
    {
        if (_stealTemplateRoot != null)
            _stealTemplateRoot.gameObject.SetActive(false);

        if (_stayTimerRoot != null)
            _stayTimerRoot.gameObject.SetActive(false);

        if (_chasedUiRoot != null)
            _chasedUiRoot.gameObject.SetActive(false);

        AudioManager.StopLooping(SfxId.Alert);

        // Keep Stolen UI root active as a pool parent; hide pooled children.
        CacheStolenTextPool();
    }

    private void BeginStayTimer()
    {
        CacheUi();

        // First steal engagement: show stay timer and remove the voluntary town exit.
        HideTownButtonUi();

        if (_stayTimerRoot == null || _chaseSequenceStarted || _stayTimerRunning)
            return;

        _stayDuration = Mathf.Max(0.01f, _stayDurationSeconds);
        _stayRemaining = _stayDuration;
        _chaseThreshold = Random.Range(
            Mathf.Min(_chaseThresholdMinSeconds, _chaseThresholdMaxSeconds),
            Mathf.Max(_chaseThresholdMinSeconds, _chaseThresholdMaxSeconds));

        _stayTimerRunning = true;
        _stayTimerRoot.gameObject.SetActive(true);
        ApplyStayTimerVisuals();
        ApplyCompetitorStatusVisuals(forceOffline: true);
    }

    private void HideTownButtonUi()
    {
        if (_townButtonRoot == null)
            _townButtonRoot = FindSceneUiObject(TownButtonUiName);

        if (_townButtonRoot != null)
            _townButtonRoot.SetActive(false);
    }

    private void UpdateStayTimer()
    {
        if (!_stayTimerRunning || _stayTimerRoot == null)
            return;

        _stayRemaining = Mathf.Max(0f, _stayRemaining - Time.deltaTime);
        ApplyStayTimerVisuals();
        ApplyCompetitorStatusVisuals();

        if (_stayRemaining <= _chaseThreshold)
            BeginChaseSequence();
    }

    private void UpdateStayTimerPulse()
    {
        if (_stayTimerRoot == null || !_stayTimerRoot.gameObject.activeSelf || _chaseSequenceStarted)
            return;

        _stayTimerRoot.localScale = Vector3.one * GetPulseScale(
            _stayTimerPulseMinScale,
            _stayTimerPulseMaxScale,
            _stayTimerPulseSpeed);
    }

    private void ApplyStayTimerVisuals()
    {
        float normalized = _stayDuration > 0f
            ? Mathf.Clamp01(_stayRemaining / _stayDuration)
            : 0f;

        if (_stayTimerFillImage != null)
            _stayTimerFillImage.fillAmount = normalized;

        if (_stayTimerCountdownText != null)
        {
            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, _stayRemaining));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            _stayTimerCountdownText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void BeginChaseSequence()
    {
        if (_chaseSequenceStarted)
            return;

        _chaseSequenceStarted = true;
        _stayTimerRunning = false;

        // He is here chasing the player — stays Online until the next visit starts.
        ApplyCompetitorStatusVisuals();

        if (_stayTimerRoot != null)
            _stayTimerRoot.localScale = Vector3.one;

        ClearStealUis();

        if (_chaseRoutine != null)
            StopCoroutine(_chaseRoutine);

        _chaseRoutine = StartCoroutine(RunChaseSequence());
    }

    private IEnumerator RunChaseSequence()
    {
        ApplyChasedCompetitorFace();
        ApplyStealResultVisuals();

        if (_chasedUiRoot != null)
        {
            _chasedUiRoot.localScale = Vector3.one;
            _chasedUiRoot.gameObject.SetActive(true);
            AudioManager.PlayLooping(SfxId.Alert);
        }

        yield return new WaitForSeconds(Mathf.Max(0.01f, _chasedUiHoldSeconds));

        AudioManager.StopLooping(SfxId.Alert);
        CompetitorSceneSelection.MarkChasedOutFromCurrentVisit();
        SceneManager.LoadScene(RestaurantSceneMode.TownSceneName);
    }

    private void UpdateChasedUiPulse()
    {
        if (_chasedUiRoot == null || !_chasedUiRoot.gameObject.activeSelf)
            return;

        _chasedUiRoot.localScale = Vector3.one * GetPulseScale(
            _chasedUiPulseMinScale,
            _chasedUiPulseMaxScale,
            _chasedUiPulseSpeed);
    }

    private void ApplyCompetitorProfileUi()
    {
        CacheUi();

        if (_profilePicImage == null)
            return;

        Sprite profilePic = CompetitorSceneSelection.GetProfilePic();
        if (profilePic != null)
            _profilePicImage.sprite = profilePic;
    }

    private void ApplyCompetitorStatusVisuals(bool forceOffline = false)
    {
        if (forceOffline)
            _competitorOnlineLatched = false;

        if (_statusText == null)
            CacheUi();

        if (_statusText == null)
            return;

        float onlineRemainingThreshold = 1f + _chaseThresholdMaxSeconds;

        if (!forceOffline
            && _stayTimerRunning
            && _stayRemaining <= onlineRemainingThreshold)
        {
            // Once he notices, he stays online through the chase until the next visit resets it.
            _competitorOnlineLatched = true;
        }

        bool isOnline = !forceOffline && _competitorOnlineLatched;

        _statusText.text = isOnline ? OnlineStatusLabel : OfflineStatusLabel;
        _statusText.color = isOnline ? _onlineStatusColor : _offlineStatusColor;
        _competitorOnline = isOnline;

        if (!isOnline)
            ResetProfilePicScale();
    }

    private void UpdateProfilePicPulse()
    {
        if (_profilePicImage == null)
            return;

        RectTransform profilePicRect = _profilePicImage.transform as RectTransform;
        if (profilePicRect == null)
            return;

        if (!_competitorOnline || !profilePicRect.gameObject.activeInHierarchy)
        {
            ResetProfilePicScale();
            return;
        }

        profilePicRect.localScale = Vector3.one * GetPulseScale(
            _profilePicPulseMinScale,
            _profilePicPulseMaxScale,
            _profilePicPulseSpeed);
    }

    private void ResetProfilePicScale()
    {
        if (_profilePicImage != null)
            _profilePicImage.transform.localScale = Vector3.one;
    }

    private void ApplyChasedCompetitorFace()
    {
        CacheUi();

        if (_chasedCompetitorFaceImage == null)
            return;

        Sprite angryFace = CompetitorSceneSelection.GetAngryFace();
        if (angryFace != null)
            _chasedCompetitorFaceImage.sprite = angryFace;
    }

    private void ApplyStealResultVisuals()
    {
        CacheUi();

        if (_stealResultText == null)
            return;

        bool success = CompetitorSceneSelection.HasMetBusinessResumeStealRequirement();
        _stealResultText.text = success ? StealResultSuccessLabel : StealResultFailLabel;
        _stealResultText.color = success ? _stealResultSuccessColor : _stealResultFailColor;
    }

    private void ShowStealUi(Customer customer)
    {
        if (customer == null || _stealTemplateRoot == null || _chaseSequenceStarted)
            return;

        if (customer.IsImmuneToCompetitorSteal || customer.WasStolenByCompetitor)
            return;

        if (_activeStealUis.ContainsKey(customer))
        {
            UpdateStealUiPosition(_activeStealUis[customer]);
            return;
        }

        RectTransform uiRoot = CreateStealUiRoot();
        if (uiRoot == null)
            return;

        Button button = uiRoot.GetComponent<Button>() ?? uiRoot.GetComponentInChildren<Button>(true);
        UnityEngine.Events.UnityAction listener = null;

        if (button != null)
        {
            listener = () => HandleStealClicked(customer);
            button.onClick.AddListener(listener);
            button.interactable = true;
        }

        uiRoot.gameObject.SetActive(true);
        // Keep steal prompts behind HUD / chased / timer UI.
        uiRoot.SetAsFirstSibling();
        DisableChildRaycastTargets(uiRoot);

        StealUiEntry entry = new StealUiEntry
        {
            UiRoot = uiRoot,
            Button = button,
            Customer = customer,
            ClickListener = listener
        };

        _activeStealUis[customer] = entry;
        UpdateStealUiPosition(entry);
    }

    private void HideStealUi(Customer customer)
    {
        if (customer == null || !_activeStealUis.TryGetValue(customer, out StealUiEntry entry))
            return;

        if (entry.Button != null && entry.ClickListener != null)
            entry.Button.onClick.RemoveListener(entry.ClickListener);

        if (entry.UiRoot != null)
        {
            entry.UiRoot.localScale = Vector3.one;

            if (entry.UiRoot == _stealTemplateRoot)
                entry.UiRoot.gameObject.SetActive(false);
            else
                Destroy(entry.UiRoot.gameObject);
        }

        _activeStealUis.Remove(customer);
    }

    private void ClearStealUis()
    {
        _stealUiScratch.Clear();
        _stealUiScratch.AddRange(_activeStealUis.Keys);

        for (int i = 0; i < _stealUiScratch.Count; i++)
            HideStealUi(_stealUiScratch[i]);

        _activeStealUis.Clear();

        if (_stealTemplateRoot != null)
        {
            _stealTemplateRoot.localScale = Vector3.one;
            _stealTemplateRoot.gameObject.SetActive(false);
        }
    }

    private RectTransform CreateStealUiRoot()
    {
        if (_stealTemplateRoot == null)
            return null;

        bool templateInUse = false;
        foreach (KeyValuePair<Customer, StealUiEntry> pair in _activeStealUis)
        {
            if (pair.Value.UiRoot == _stealTemplateRoot)
            {
                templateInUse = true;
                break;
            }
        }

        if (!templateInUse)
            return _stealTemplateRoot;

        RectTransform instance = Instantiate(_stealTemplateRoot, _stealTemplateRoot.parent);
        instance.name = StealCustomersUiName;
        return instance;
    }

    private void HandleStealClicked(Customer customer)
    {
        if (_chaseSequenceStarted || customer == null)
            return;

        if (CustomerManager.Instance == null)
            return;

        AudioManager.Play(SfxId.UiClick);

        // First steal tap reveals the stay timer and starts the visit countdown.
        BeginStayTimer();

        Transform reactPoint = customer.ReactPoint;
        Vector3 feedbackWorldPosition = reactPoint != null
            ? reactPoint.position
            : customer.transform.position;

        if (!CustomerManager.Instance.TryStealQueuedCustomer(customer))
            return;

        CompetitorSceneSelection.RegisterSuccessfulSteal(customer.IsVip);
        HideStealUi(customer);
        ShowStolenText(feedbackWorldPosition);
    }

    private void ShowStolenText(Vector3 worldAnchorPosition)
    {
        CacheStolenTextPool();

        RectTransform uiRoot = AcquireStolenText();
        if (uiRoot == null)
            return;

        Graphic[] graphics = uiRoot.GetComponentsInChildren<Graphic>(true);
        Color[] targetColors = UiGraphicFade.CaptureColors(graphics);
        UiGraphicFade.RestoreColors(graphics, targetColors);

        uiRoot.gameObject.SetActive(true);
        uiRoot.SetAsLastSibling();
        TryFollowWorldAnchor(uiRoot, worldAnchorPosition);

        StolenTextEntry entry = new StolenTextEntry
        {
            UiRoot = uiRoot,
            Graphics = graphics,
            TargetColors = targetColors,
            StartAnchoredPosition = uiRoot.anchoredPosition
        };

        entry.Routine = StartCoroutine(PlayStolenTextFade(entry));
        _activeStolenTexts.Add(entry);
    }

    private RectTransform AcquireStolenText()
    {
        for (int i = _stolenTextPool.Count - 1; i >= 0; i--)
        {
            RectTransform pooled = _stolenTextPool[i];
            _stolenTextPool.RemoveAt(i);

            if (pooled != null)
                return pooled;
        }

        if (_stolenTextTemplate == null || _stolenUiRoot == null)
            return null;

        RectTransform instance = Instantiate(_stolenTextTemplate, _stolenUiRoot);
        instance.name = $"{StolenTextNamePrefix} (Pooled)";
        PrepareStolenTextForPool(instance);
        return instance;
    }

    private IEnumerator PlayStolenTextFade(StolenTextEntry entry)
    {
        if (entry == null || entry.UiRoot == null)
            yield break;

        float holdSeconds = Mathf.Max(0f, _stolenTextHoldSeconds);
        float fadeSeconds = Mathf.Max(0.01f, _stolenTextFadeOutSeconds);
        float totalSeconds = holdSeconds + fadeSeconds;
        float floatDistance = _stolenTextFloatDistance;
        Color[] transparentColors = UiGraphicFade.BuildTransparentColors(entry.TargetColors);
        float elapsed = 0f;

        while (elapsed < totalSeconds)
        {
            elapsed += Time.deltaTime;
            float moveT = Mathf.Clamp01(elapsed / totalSeconds);
            entry.UiRoot.anchoredPosition = entry.StartAnchoredPosition + Vector2.up * (floatDistance * moveT);

            if (entry.Graphics != null && entry.TargetColors != null && transparentColors != null)
            {
                float fadeT = elapsed <= holdSeconds
                    ? 0f
                    : Mathf.Clamp01((elapsed - holdSeconds) / fadeSeconds);

                for (int i = 0; i < entry.Graphics.Length; i++)
                {
                    if (entry.Graphics[i] == null || i >= entry.TargetColors.Length || i >= transparentColors.Length)
                        continue;

                    entry.Graphics[i].color = Color.Lerp(entry.TargetColors[i], transparentColors[i], fadeT);
                }
            }

            yield return null;
        }

        entry.UiRoot.anchoredPosition = entry.StartAnchoredPosition + Vector2.up * floatDistance;
        ReleaseStolenText(entry);
    }

    private void ReleaseStolenText(StolenTextEntry entry)
    {
        if (entry == null)
            return;

        _activeStolenTexts.Remove(entry);

        if (entry.Routine != null)
        {
            // Only stop if this release isn't coming from the routine itself finishing.
            // Stopping a finished coroutine is harmless; stopping mid-fade is intentional for Clear.
        }

        if (entry.UiRoot == null)
            return;

        if (entry.Graphics != null && entry.TargetColors != null)
            UiGraphicFade.RestoreColors(entry.Graphics, entry.TargetColors);
        else
            UiGraphicFade.RestoreAlpha(entry.UiRoot.GetComponentsInChildren<Graphic>(true));

        entry.UiRoot.localScale = Vector3.one;
        entry.UiRoot.gameObject.SetActive(false);

        if (!_stolenTextPool.Contains(entry.UiRoot))
            _stolenTextPool.Add(entry.UiRoot);
    }

    private void ClearStolenTexts()
    {
        for (int i = _activeStolenTexts.Count - 1; i >= 0; i--)
        {
            StolenTextEntry entry = _activeStolenTexts[i];
            if (entry == null)
                continue;

            if (entry.Routine != null)
                StopCoroutine(entry.Routine);

            ReleaseStolenText(entry);
        }

        _activeStolenTexts.Clear();
    }

    private void UpdateStealUiPositions()
    {
        if (_activeStealUis.Count == 0)
            return;

        _stealUiScratch.Clear();
        _stealUiScratch.AddRange(_activeStealUis.Keys);

        for (int i = 0; i < _stealUiScratch.Count; i++)
        {
            Customer customer = _stealUiScratch[i];
            if (!_activeStealUis.TryGetValue(customer, out StealUiEntry entry))
                continue;

            if (customer == null
                || customer.State != CustomerState.Queue
                || customer.WasStolenByCompetitor)
            {
                HideStealUi(customer);
                continue;
            }

            UpdateStealUiPosition(entry);
        }
    }

    private void UpdateStealUiPulses()
    {
        if (_activeStealUis.Count == 0)
            return;

        float scale = GetPulseScale(_stealPulseMinScale, _stealPulseMaxScale, _stealPulseSpeed);
        Vector3 pulseScale = Vector3.one * scale;

        foreach (KeyValuePair<Customer, StealUiEntry> pair in _activeStealUis)
        {
            if (pair.Value.UiRoot != null && pair.Value.UiRoot.gameObject.activeSelf)
                pair.Value.UiRoot.localScale = pulseScale;
        }
    }

    private static float GetPulseScale(float minScale, float maxScale, float speed)
    {
        if (speed <= 0f)
            return 1f;

        float pulseT = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        return Mathf.Lerp(minScale, maxScale, pulseT);
    }

    private void UpdateStealUiPosition(StealUiEntry entry)
    {
        if (entry.UiRoot == null || entry.Customer == null)
            return;

        Transform anchor = entry.Customer.ReactPoint;
        if (anchor == null)
            return;

        if (!TryFollowWorldAnchor(entry.UiRoot, anchor.position))
            return;

        if (!entry.UiRoot.gameObject.activeSelf)
            entry.UiRoot.gameObject.SetActive(true);
    }

    private void EnsureScreenUiCaches()
    {
        if (_screenCanvas == null)
        {
            GameObject canvasObject = FindSceneUiObject("UI Canvas");
            if (canvasObject != null)
                _screenCanvas = canvasObject.GetComponent<Canvas>();

            if (_screenCanvas == null)
            {
                Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < canvases.Length; i++)
                {
                    Canvas canvas = canvases[i];
                    if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                    {
                        _screenCanvas = canvas;
                        break;
                    }
                }
            }
        }

        if (_screenCanvas != null && _canvasRect == null)
            _canvasRect = _screenCanvas.transform as RectTransform;

        Camera activeCamera = Camera.main;
        if (activeCamera != null)
            _worldCamera = activeCamera;
        else if (_worldCamera == null)
            _worldCamera = Camera.main;
    }

    /// <summary>
    /// Places a screen-space UI element on a world anchor. Uses world position so center-anchored
    /// children of a bottom-left canvas pivot stay on-screen (anchoredPosition from canvas-local
    /// coords would push them off the top-right).
    /// </summary>
    private bool TryFollowWorldAnchor(RectTransform uiRoot, Vector3 worldAnchorPosition)
    {
        if (uiRoot == null)
            return false;

        EnsureScreenUiCaches();

        if (_worldCamera == null || _screenCanvas == null)
            return false;

        RectTransform parentRect = uiRoot.parent as RectTransform;
        if (parentRect == null)
            parentRect = _canvasRect;

        if (parentRect == null)
            return false;

        Vector3 screenPoint = _worldCamera.WorldToScreenPoint(worldAnchorPosition);
        if (screenPoint.z <= 0f)
            return false;

        Camera canvasCamera = _screenCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _screenCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                parentRect,
                screenPoint,
                canvasCamera,
                out Vector3 worldPoint))
        {
            return false;
        }

        uiRoot.position = worldPoint;
        return true;
    }

    private static GameObject FindSceneUiObject(string objectName)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindChildTransform(roots[i].transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return GameObject.Find(objectName);
    }

    private static Transform FindChildTransform(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;

        if (root.name == name)
            return root;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == name)
                return children[i];
        }

        return null;
    }

    private static void DisableChildRaycastTargets(Transform root)
    {
        if (root == null)
            return;

        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null && graphics[i].transform != root)
                graphics[i].raycastTarget = false;
        }
    }
}
