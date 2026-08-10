using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
public class UIManager : MonoBehaviour
{
    private const string TownSceneName = "2_Town Scene";

    public static UIManager Instance { get; private set; }

    [Header("Build UI")]
    [SerializeField] private Canvas _screenCanvas;
    [SerializeField] private Camera _worldCamera;

    [Header("Gold UI")]
    [SerializeField] private TextMeshProUGUI _goldAmountText;
    [SerializeField] private Button _addGoldButton;
    [SerializeField] private int _addGoldButtonAmount;
    [SerializeField] private RectTransform _goldPlusUi;
    [SerializeField] private RectTransform _goldMinusUi;
    [SerializeField] private float _goldChangeFloatDistance;
    [SerializeField] private float _goldChangeFloatDuration;

    [Header("Hire UI")]
    [SerializeField] private Button _chefHireButton;
    [SerializeField] private Button _waiterHireButton;
    [SerializeField] private float _chefHirePulseMinScale;
    [SerializeField] private float _chefHirePulseMaxScale;
    [SerializeField] private float _chefHirePulseSpeed;
    [SerializeField] private float _waiterHirePulseMinScale;
    [SerializeField] private float _waiterHirePulseMaxScale;
    [SerializeField] private float _waiterHirePulseSpeed;
    [Tooltip("Raises billboarded ground hire buttons so they don't clip into the ground.")]
    [SerializeField] private float _hireUiWorldHeightOffset = 0.85f;

    [Header("Seat Payment UI")]
    [SerializeField] private RectTransform _seatPaymentUiRoot;

    [Header("Coin Trail UI")]
    [SerializeField] private RectTransform _coinVfxRoot;
    [SerializeField] private RectTransform _coinTrailTargetUi;
    [SerializeField] private int _coinTrailCount;
    [Tooltip("How many coins to spawn for VIP treasure trails. Uses the shared pool and grows it if needed.")]
    [SerializeField] private int _vipCoinTrailCount;
    [SerializeField] private float _coinTrailDelay;
    [SerializeField] private float _coinTrailDuration;
    [Tooltip("Per-coin flight duration for VIP treasure-box coin trails.")]
    [SerializeField] private float _vipCoinTrailDuration = 1.5f;
    [SerializeField] private float _coinTrailArcHeight;

    [Header("VIP UI")]
    [SerializeField] private RectTransform _vipUiRoot;
    [SerializeField] private RectTransform _vipFireworksRoot;
    [SerializeField] private float _vipFireworksHoldDuration = 3f;
    [SerializeField] private float _vipFireworksFadeOutDuration = 0.4f;
    [SerializeField] private RectTransform _vipDoneEatingRoot;
    [SerializeField] private RectTransform _vipIntroButtonRoot;
    [SerializeField] private Button _vipIntroButton;
    [SerializeField] private RectTransform _vipWaitTimerRoot;
    [SerializeField] private Image _vipWaitTimerFillImage;
    [SerializeField] private TextMeshProUGUI _vipWaitTimerCountdownText;
    [SerializeField] private RectTransform _vipServeDishButtonRoot;
    [SerializeField] private Button _vipServeDishButton;
    [SerializeField] private RectTransform _vipFeetMassageButtonRoot;
    [SerializeField] private Button _vipFeetMassageButton;
    [SerializeField] private RectTransform _vipServeTeaButtonRoot;
    [SerializeField] private Button _vipServeTeaButton;
    [SerializeField] private RectTransform _vipCallLadyButtonRoot;
    [SerializeField] private Button _vipCallLadyButton;
    [SerializeField] private RectTransform _vipGeTaiButtonRoot;
    [SerializeField] private Button _vipGeTaiButton;
    [Header("VIP Main Icon")]
    [SerializeField] private RectTransform _vipMainIconRoot;
    [Tooltip("VIP icon pulses while the wait timer has this many seconds (or fewer) remaining.")]
    [SerializeField] private float _vipIconPulseRemainingThreshold = 15f;
    [SerializeField] private float _vipIconPulseMinScale = 0.92f;
    [SerializeField] private float _vipIconPulseMaxScale = 1.08f;
    [SerializeField] private float _vipIconPulseSpeed = 8f;
    [SerializeField] private TextMeshProUGUI _vipDialogueText;
    [Header("VIP Dialogue Lines")]
    [Tooltip("VIP arrives at the entry waypoint.")]
    [SerializeField] private string[] _vipDialogueArrived =
    {
        "哼，给本尊开门!"
    };
    [Tooltip("VIP leaves unhappy (invite ignored or 上菜 timed out). Hides after a few seconds.")]
    [SerializeField] private string[] _vipDialogueUnhappyLeave =
    {
        "岂有此理! 本尊走了!"
    };
    [Tooltip("VIP leaves happily after coin collection.")]
    [SerializeField] private string[] _vipDialogueSuccessLeave =
    {
        "太好吃了，下次再来!"
    };
    [Tooltip("VIP wants 上菜 (button appears).")]
    [SerializeField] private string[] _vipDialogueRequestServeDish =
    {
        "上菜! 本尊饿了!"
    };
    [Tooltip("VIP wants 泡脚 (button appears).")]
    [SerializeField] private string[] _vipDialogueRequestFeetMassage =
    {
        "给本尊好好泡脚!"
    };
    [Tooltip("VIP wants 上茶 (button appears).")]
    [SerializeField] private string[] _vipDialogueRequestServeTea =
    {
        "上茶! 要上等的!"
    };
    [Tooltip("VIP wants 叫美女 (button appears).")]
    [SerializeField] private string[] _vipDialogueRequestCallLady =
    {
        "叫几个美女来陪本尊!"
    };
    [Tooltip("VIP wants 歌台 / 表演 (button appears).")]
    [SerializeField] private string[] _vipDialogueRequestWatchStage =
    {
        "来段表演，助助兴!"
    };
    [Tooltip("Optional request timed out (except 上菜). Hides after a few seconds.")]
    [SerializeField] private string[] _vipDialogueDiscontent =
    {
        "哼，怠慢本尊?!"
    };
    [SerializeField] private float _vipNegativeDialogueHideDelay = 3f;

    [Header("Table Status UI")]
    [SerializeField] private RectTransform _tableStatusUiRoot;
    [SerializeField] private Color _tableStatusEmptySeatColor;
    [SerializeField] private Color _tableStatusFullColor;
    [SerializeField] private Color _tableStatusPaymentColor;

    [Header("Prankster UI")]
    [SerializeField] private RectTransform _chasePranksterUiRoot;
    [SerializeField] private Button _chasePranksterButton;
    [SerializeField] private RectTransform _pranksterNameUiRoot;
    [SerializeField] private float _chasePranksterPulseMinScale = 0.9f;
    [SerializeField] private float _chasePranksterPulseMaxScale = 1.1f;
    [SerializeField] private float _chasePranksterPulseSpeed = 10f;
    [SerializeField] private RectTransform _pranksterDialogueRoot;
    [SerializeField] private Sprite _pranksterDialogueSmirkIcon;
    [SerializeField] private Sprite _pranksterDialogueAngryIcon1;
    [SerializeField] private Sprite _pranksterDialogueAngryIcon2;
    [SerializeField] private float _pranksterDialogueFadeInDuration = 0.35f;
    [SerializeField] private float _pranksterDialogueHoldDuration = 1f;
    [SerializeField] private float _pranksterDialogueFadeOutDuration = 0.3f;
    [SerializeField] private string[] _pranksterChasedAwayMessages =
    {
        "算你狠! 走着瞧!",
        "切, 真没劲, 撤!"
    };
    [SerializeField] private string[] _pranksterTableBrokenMessages =
    {
        "哈哈, 让你开门做生意!",
        "哎呀，手滑了~"
    };

    [Header("Repair Table UI")]
    [SerializeField] private RectTransform _repairTableUiRoot;
    [SerializeField] private float _repairTablePulseMinScale = 0.88f;
    [SerializeField] private float _repairTablePulseMaxScale = 1.08f;
    [SerializeField] private float _repairTablePulseSpeed = 12f;
    [SerializeField] private RectTransform _dushRepairUiRoot;
    [SerializeField] private float _dushRepairDuration = 1f;

    [Header("Open Business UI")]
    [SerializeField] private GameObject _openBusinessRoot;
    [SerializeField] private Button _openBusinessButton;
    [SerializeField] private TextMeshProUGUI _businessTimerText;
    [SerializeField] private float _openBusinessFadeInDuration = 0.4f;
    [SerializeField] private float _openBusinessHoldDuration = 1.2f;
    [SerializeField] private float _openBusinessFadeOutDuration = 0.5f;

    [Header("Business Overview UI")]
    [SerializeField] private GameObject _businessOverviewRoot;
    [SerializeField] private Button _businessAcknowledgeButton;

    [Header("Table Build Info UI")]
    [SerializeField] private GameObject _tableBuildInfoRoot;
    [SerializeField] private Button _tableBuildInfoCloseButton;

    [Header("Table Upgrade UI")]
    [SerializeField] private GameObject _upgradeTableRoot;
    [SerializeField] private TextMeshProUGUI _upgradeLeftText;
    [SerializeField] private TextMeshProUGUI _upgradeRightText;
    [SerializeField] private Image _upgradeLeftImage;
    [SerializeField] private Image _upgradeRightImage;
    [SerializeField] private TextMeshProUGUI _upgradeCostText;
    [SerializeField] private Button _upgradeCostButton;
    [SerializeField] private Button _upgradeTableCloseButton;
    [SerializeField] private Sprite[] _tableLevelSprites;

    [Header("Worker Energy UI")]
    [SerializeField] private Canvas _worldCanvas;
    [SerializeField] private WorkerEnergyUiBinding[] _workerEnergyUiBindings;
    [SerializeField] private float _workerEnergyHeadHeightOffset;
    [SerializeField] private Color _workerEnergyRestingFillColor = Color.red;

    [Header("Not Enough Money UI")]
    [SerializeField] private RectTransform _notEnoughMoneyUiRoot;
    [SerializeField] private float _notEnoughMoneyDisplayDuration = 1.2f;
    [SerializeField] private float _notEnoughMoneyFloatDistance = 48f;
    [SerializeField] private float _notEnoughMoneyBelowGoldOffset = 36f;

    [Header("Creation Scene UI")]
    [SerializeField] private TMP_InputField _nameInputField;
    [SerializeField] private Button _entryButton;
    [SerializeField] private Button _randomiserButton;

    [Header("Town Loan UI")]
    [SerializeField] private GameObject _loanRoot;
    [SerializeField] private Button _getMoneyButton;
    [SerializeField] private TextMeshProUGUI _loanAmountText;
    [SerializeField] private int _loanAmount;

    [Header("Town Build UI")]
    [SerializeField] private BuildSpot _townBuildSpot;
    [SerializeField] private GameObject _newBuildingUiRoot;
    [SerializeField] private GameObject _firstBuildingOption;
    [SerializeField] private GameObject _closeBuildingUi;
    [SerializeField] private int _townBuildCost;
    [SerializeField] private GameObject _townBuildingEffectRoot;
    [SerializeField] private RectTransform _townBuildingEffectRect;
    [SerializeField] private TextMeshProUGUI _townBuildTimeLeftText;
    [SerializeField] private float _townBuildDurationSeconds;

    [Header("Town Enter Shop UI")]
    [SerializeField] private GameObject _enterShopUiRoot;
    [SerializeField] private RectTransform _enterShopUiRect;
    [SerializeField] private GameObject _enterShopIcon;
    [SerializeField] private Transform _enterShopAnchor;

    [Header("Town Enter Competitor Shop UI")]
    [SerializeField] private GameObject _enterCompetitorShopUiRoot;
    [SerializeField] private EnterCompetitorShopUiBinding[] _enterCompetitorShopUiBindings;
    [SerializeField] private float _enterCompetitorShopPulseMinScale = 0.9f;
    [SerializeField] private float _enterCompetitorShopPulseMaxScale = 1.1f;
    [SerializeField] private float _enterCompetitorShopPulseSpeed = 10f;

    [Header("Competitor Scene Name UI")]
    [SerializeField] private TextMeshProUGUI _competitorRestaurantNameText;

    [Header("Town Competitor Shop Name UI")]
    [SerializeField] private CompetitorShopNameUiBinding[] _competitorShopNameUiBindings;

    [Header("Competitor Catalog")]
    [SerializeField] private VipCompetitorCatalog _competitorCatalog;

    [Header("Town Owner Shop Name UI")]
    [SerializeField] private RectTransform _ownShopNameUiRoot;
    [SerializeField] private TextMeshProUGUI _ownShopNameText;

    [Header("Town Owner Rating UI")]
    [SerializeField] private RectTransform _ownRatingUiRoot;
    [SerializeField] private TextMeshProUGUI _ownRatingText;

    [Header("Scene Navigation")]
    [SerializeField] private Button _townButton;

    [Header("Main Buttons UI")]
    [SerializeField] private GameObject _mainButtonsRoot;

    [Header("Mission UI")]
    [SerializeField] private MissionCatalog _missionCatalog;

    private RectTransform _canvasRect;
    private bool _buildSpotCostUiSuppressed;
    private HireSpot _activeHireSpot;
    private DiningTable _selectedUpgradeTable;
    private readonly Dictionary<Transform, SeatPaymentUiEntry> _activeSeatPaymentUis = new();
    private readonly Dictionary<DiningTable, TableStatusUiEntry> _activeTableStatusUis = new();
    private readonly Dictionary<DiningTable, RepairTableUiEntry> _activeRepairTableUis = new();
    private readonly Dictionary<Worker, WorkerEnergyUiEntry> _activeWorkerEnergyUis = new();
    private readonly List<RectTransform> _notEnoughMoneyUiPool = new();
    private readonly List<NotEnoughMoneyUiEntry> _activeNotEnoughMoneyUis = new();
    private PranksterManager _pranksterManager;
    private readonly List<DiningTable> _repairTableSyncScratch = new();
    private readonly List<Worker> _workerEnergyUiScratch = new();

    private int _lastGoldAmount = -1;
    private int _pendingGoldDelta;
    private bool _hasPendingGoldDelta;
    private Vector2 _goldPlusRestPosition;
    private Vector2 _goldMinusRestPosition;
    private Coroutine _goldPlusAnimation;
    private Coroutine _goldMinusAnimation;
    private readonly List<RectTransform> _coinTrailPool = new();
    private readonly Dictionary<RectTransform, Coroutine> _activeCoinTrailAnimations = new();
    private readonly Dictionary<int, Coroutine> _coinTrailSequences = new();
    private int _nextCoinTrailSequenceId;
    private RectTransform _coinTrailTemplate;
    private Coroutine _vipDialogueHideRoutine;
    private Graphic[] _vipFireworksGraphics;
    private Color[] _vipFireworksTargetColors;
    private Animator _vipFireworksAnimator;
    private Coroutine _vipFireworksRoutine;
    private Customer _vipIntroCustomer;
    private Customer _vipWaitTimerCustomer;
    private Customer _vipEventCustomer;
    private VipEventType? _activeVipEventButton;
    private float _vipWaitTimerRemaining;
    private float _vipWaitTimerDuration;
    private bool _vipIntroButtonWired;
    private bool _vipEventButtonsWired;
    private RectTransform _pranksterDialogueRootRuntime;
    private TextMeshProUGUI _pranksterDialogueText;
    private Image _pranksterDialogueIconImage;
    private Graphic[] _pranksterDialogueGraphics;
    private Color[] _pranksterDialogueTargetColors;
    private Coroutine _pranksterDialogueRoutine;
    private Coroutine _dushRepairEffectRoutine;
    private Coroutine _openBusinessSequenceRoutine;
    private bool _openBusinessSequencePlayed;
    private readonly HashSet<Button> _wiredHireSpotButtons = new();
    private readonly Dictionary<HireSpot, Vector3> _hireSpotHomeWorldPositions = new();

    private Button _townFirstOptionButton;
    private Button _townCloseBuildingUiButton;
    private Button _enterShopIconButton;
    private bool _enterShopUiVisible;
    private readonly List<EnterCompetitorShopButtonHandler> _enterCompetitorShopButtonHandlers = new();
    private Coroutine _townBuildRoutine;
    private bool _townBuildingEffectVisible;
    private GameObject[] _townOpponentShops;
    private Transform _ownerShopNameAnchor;
    private Transform _ownerShopRatingAnchor;

    public float WorkerEnergyHeadHeightOffset => _workerEnergyHeadHeightOffset;

    private struct SeatPaymentUiEntry
    {
        public RectTransform UiRoot;
        public Transform PaymentAnchor;
        public RectTransform ExtraGlowRoot;
    }

    private struct TableStatusUiEntry
    {
        public RectTransform UiRoot;
        public TextMeshProUGUI StatusText;
        public DiningTable Table;
    }

    private struct RepairTableUiEntry
    {
        public RectTransform UiRoot;
        public Button Button;
        public TextMeshProUGUI CostText;
    }

    private sealed class NotEnoughMoneyUiEntry
    {
        public RectTransform UiRoot;
        public Coroutine Routine;
        public Vector3 GoldAnchorWorld;
        public float Elapsed;
        public float Duration;
        public float FloatDistance;
        public float BelowGoldOffset;
        public Graphic[] Graphics;
        public Color[] TargetColors;
    }

    private struct WorkerEnergyUiEntry
    {
        public Worker Worker;
        public RectTransform UiRoot;
        public Image FillImage;
        public Color NormalFillColor;
    }

    [System.Serializable]
    private class WorkerEnergyUiBinding
    {
        public Worker Worker;
        public RectTransform Root;
        public Image FillImage;
        [System.NonSerialized] public Color NormalFillColor;
        [System.NonSerialized] public bool HasNormalFillColor;
    }

    private const string WorkerEnergyFillImageName = "Image";
    private const string CoinCollectionExtraGlowName = "Extra Glow";
    private const string VipUiRootName = "VIP Main UI";
    private const string VipFireworksName = "Fireworks";
    private const string VipDoneEatingRootName = "VIP Done Eating";
    private const string VipMainIconName = "VIP Icon";
    private const string VipIntroButtonName = "VIP Intro Button";
    private const string VipWaitTimerName = "VIP Wait Timer";
    private const string VipWaitTimerFillName = "Energy Bar";
    private const string VipWaitTimerCountdownName = "Countdown";
    private const string VipServeDishButtonName = "VIP ServeDish Button";
    private const string VipFeetMassageButtonName = "VIP Feetmassage Button";
    private const string VipServeTeaButtonName = "VIP ServeTea Button";
    private const string VipCallLadyButtonName = "VIP CallLady Button";
    private const string VipGeTaiButtonName = "VIP GeTai Button";
    private const string VipDialogueTextName = "VIP Text";
    private const string ChasePranksterUiName = "Chase Prankster";
    private const string PranksterNameUiName = "Prankster Name";
    private const string PranksterDialogueRootName = "Prankster Dialogue";
    private const string PranksterDialogueTextName = "Prankster Text";
    private const string PranksterDialogueIconName = "Prankster Icon";
    private const string RepairTableUiName = "Repair Table";
    private const string DushRepairUiName = "Dush_Repair";
    private const string NotEnoughMoneyUiRootName = "Not enough money";
    private const string NotEnoughMoneyNomUiNamePrefix = "Nom";

    private enum PranksterDialogueType
    {
        ChasedAway,
        TableBroken
    }

    private readonly Dictionary<Worker, WorkerEnergyUiBinding> _runtimeWorkerEnergyBindings = new();
    private readonly HashSet<WorkerEnergyUiBinding> _initializedWorkerEnergyBindings = new();

    [System.Serializable]
    private class CompetitorShopNameUiBinding
    {
        public RectTransform UiRoot;
        public Transform Anchor;
    }

    [System.Serializable]
    private class EnterCompetitorShopUiBinding
    {
        public RectTransform UiRoot;
        public Transform Anchor;
        public int ShopIndex;
    }

    private sealed class EnterCompetitorShopButtonHandler
    {
        public Button Button;
        public UnityEngine.Events.UnityAction Listener;
    }

    private const string OpponentShopNamePrefix = "Opponent Shop (";
    private const string CompetitorShopNamePrefix = "CompetitorShopName (";
    private const string OwnerShopObjectName = "Owner Shop";
    private const string OwnShopNameUiName = "OwnShopName";
    private const string OwnRatingUiName = "OwnRating";
    private const string ShopNamePointName = "Name Point";
    private const string ShopRatingPointName = "Rating Point";
    private const string EnterCompetitorShopTemplateName = "EnterCompetitorShop";
    private const string EnterCompetitorShopPrefix = "EnterCompetitorShop (";
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_screenCanvas == null)
            _screenCanvas = FindScreenCanvas();

        if (_screenCanvas != null)
            _canvasRect = _screenCanvas.transform as RectTransform;

        if (_worldCamera == null)
            _worldCamera = Camera.main;

        if (_worldCanvas == null)
            _worldCanvas = FindWorldCanvas();

        InitializeWorkerEnergyUiBindings();

        if (_addGoldButton == null)
            _addGoldButton = FindGoldUiButton("Plus Bg");
        else
            DisableChildRaycastTargets(_addGoldButton.transform);

        if (_addGoldButton != null)
            _addGoldButton.onClick.AddListener(HandleAddGoldClicked);

        CacheGoldChangeUi();
        CacheCoinTrailUi();
        CacheVipUi();
        CacheVipDoneEatingUi();
        CacheVipIntroButtonUi();
        CacheVipWaitTimerUi();
        CacheVipEventButtonsUi();

        InitializeOpenBusinessUi();
        InitializeBusinessOverviewUi();

        CacheTableBuildInfoUiReferences();

        if (_tableBuildInfoCloseButton != null)
            _tableBuildInfoCloseButton.onClick.AddListener(HandleTableBuildInfoCloseClicked);

        CacheUpgradeTableUiReferences();

        if (_upgradeTableCloseButton != null)
            _upgradeTableCloseButton.onClick.AddListener(HandleUpgradeTableCloseClicked);

        if (_upgradeCostButton != null)
            _upgradeCostButton.onClick.AddListener(HandleUpgradeCostClicked);

        // Hire buttons are wired per-spot in EnsureHireSpotButtonWired so chef/waiter
        // clicks never share a single _activeHireSpot.

        if (_seatPaymentUiRoot != null)
            _seatPaymentUiRoot.gameObject.SetActive(false);

        CacheNotEnoughMoneyUi();

        if (_tableStatusUiRoot != null)
            _tableStatusUiRoot.gameObject.SetActive(false);

        InitializePranksterUi();
        InitializeRepairTableUi();

        HideVipUi();
        HideVipDoneEating();
        HideVipIntroButton();
        HideVipWaitTimer();
        HideAllVipEventButtons();
        HideBusinessTimer();
        HideBusinessOverview();
        HideOpenBusiness();
        HideTableBuildInfoUi();
        HideUpgradeTableUi();
        InitializeCreationSceneUi();
        InitializeTownLoanUi();
        InitializeTownBuildUi();
        InitializeEnterShopUi();
        InitializeEnterCompetitorShopUi();
        InitializeCompetitorSceneNameUi();
        InitializeCompetitorShopNameUi();
        InitializeOwnShopNameUi();
        InitializeTownRatingUi();
        InitializeSceneNavigationUi();
        InitializeMainButtonsUi();
        InitializeMissionUi();
        SyncOpenBusinessUiVisibility();
        if (RestaurantSceneMode.IsCompetitorScene)
            HideSceneMenuUi();
        BindPersistentCharacterPanelUi();
    }

    private void Start()
    {
        InitializeCreationSceneStart();
        InitializeTownLoanStart();
        InitializeTownBuildStart();
        InitializeEnterShopStart();

        // Town restaurant uses its own New Building UI flow — never show BuildSpotCostUI there.
        if (IsActiveTownScene())
            SetBuildSpotCostUiSuppressed(true);
        else
            SetBuildSpotCostUiSuppressed(false);

        SyncActiveUi();
        SyncOpenBusinessUiVisibility();

        if (IsActiveTownScene())
        {
            SyncTownRatingTexts();
            SyncTownCompetitorShopVisibility();
        }
    }

    private void OnValidate()
    {
        SyncLoanAmountText();
    }

    private void OnEnable()
    {
        GameEvents.StateChanged += HandleStateChanged;
        GameEvents.BuildSpotStateChanged += HandleBuildSpotStateChanged;
        GameEvents.HireSpotStateChanged += HandleHireSpotStateChanged;
        GameEvents.CustomerStateChanged += HandleCustomerStateChanged;
        GameEvents.WorkerEnergyChanged += OnWorkerUiRefreshRequested;
        GameEvents.WorkerStateChanged += OnWorkerUiRefreshRequested;
        GameEvents.GoldChanged += HandleGoldChanged;
        GameEvents.HiringCompleted += HandleHiringCompleted;
        GameEvents.TableBuildInfoRequested += HandleTableBuildInfoRequested;
        GameEvents.MissionPartChanged += HandleMissionPartChanged;
        GameEvents.MissionPartCompleted += HandleMissionPartCompletedForOpenBusiness;
        GameEvents.BusinessSessionStarted += HandleBusinessSessionStarted;
        GameEvents.BusinessDowntimeStarted += HandleBusinessDowntimeStarted;
        GameEvents.RestaurantFloorChanged += HandleRestaurantFloorChanged;
        GameEvents.SecondFloorUnlocked += HandleSecondFloorUnlockedForUi;
        GameEvents.TableClicked += HandleTableClicked;
        GameEvents.TableUpgraded += HandleTableUpgraded;
        GameEvents.TableStatusChanged += HandleTableStatusChanged;
        Canvas.willRenderCanvases += HandleCanvasWillRenderCanvases;
        BindPersistentCharacterPanelUi();
        SyncGoldUi();
        if (IsActiveTownScene())
            SyncTownRatingTexts();
        SyncActiveUi();
        SyncAllNestedBuildSpotCostUi();

        if (RestaurantSceneMode.IsMainScene)
            SyncRepairTableUis();
    }

    private void OnDisable()
    {
        GameEvents.StateChanged -= HandleStateChanged;
        GameEvents.BuildSpotStateChanged -= HandleBuildSpotStateChanged;
        GameEvents.HireSpotStateChanged -= HandleHireSpotStateChanged;
        GameEvents.CustomerStateChanged -= HandleCustomerStateChanged;
        GameEvents.WorkerEnergyChanged -= OnWorkerUiRefreshRequested;
        GameEvents.WorkerStateChanged -= OnWorkerUiRefreshRequested;
        GameEvents.GoldChanged -= HandleGoldChanged;
        GameEvents.HiringCompleted -= HandleHiringCompleted;
        GameEvents.TableBuildInfoRequested -= HandleTableBuildInfoRequested;
        GameEvents.MissionPartChanged -= HandleMissionPartChanged;
        GameEvents.MissionPartCompleted -= HandleMissionPartCompletedForOpenBusiness;
        GameEvents.BusinessSessionStarted -= HandleBusinessSessionStarted;
        GameEvents.BusinessDowntimeStarted -= HandleBusinessDowntimeStarted;
        GameEvents.RestaurantFloorChanged -= HandleRestaurantFloorChanged;
        GameEvents.SecondFloorUnlocked -= HandleSecondFloorUnlockedForUi;
        GameEvents.TableClicked -= HandleTableClicked;
        GameEvents.TableUpgraded -= HandleTableUpgraded;
        GameEvents.TableStatusChanged -= HandleTableStatusChanged;
        Canvas.willRenderCanvases -= HandleCanvasWillRenderCanvases;
    }

    private void OnDestroy()
    {
        if (_openBusinessButton != null)
            _openBusinessButton.onClick.RemoveListener(HandleOpenBusinessClicked);

        if (_businessAcknowledgeButton != null)
            _businessAcknowledgeButton.onClick.RemoveListener(HandleBusinessAcknowledgeClicked);

        if (_tableBuildInfoCloseButton != null)
            _tableBuildInfoCloseButton.onClick.RemoveListener(HandleTableBuildInfoCloseClicked);

        if (_upgradeTableCloseButton != null)
            _upgradeTableCloseButton.onClick.RemoveListener(HandleUpgradeTableCloseClicked);

        if (_upgradeCostButton != null)
            _upgradeCostButton.onClick.RemoveListener(HandleUpgradeCostClicked);

        if (_chasePranksterButton != null)
            _chasePranksterButton.onClick.RemoveListener(HandleChasePranksterClicked);

        if (_vipIntroButton != null && _vipIntroButtonWired)
            _vipIntroButton.onClick.RemoveListener(HandleVipIntroButtonClicked);

        if (_addGoldButton != null)
            _addGoldButton.onClick.RemoveListener(HandleAddGoldClicked);

        UnsubscribeCreationSceneUi();
        UnsubscribeTownLoanUi();
        UnsubscribeTownBuildUi();
        UnsubscribeEnterShopUi();
        UnsubscribeEnterCompetitorShopUi();
        UnsubscribeSceneNavigationUi();

        if (_goldPlusAnimation != null)
            StopCoroutine(_goldPlusAnimation);

        if (_goldMinusAnimation != null)
            StopCoroutine(_goldMinusAnimation);

        HideVipUi();
        HideVipDoneEating();
        StopAllCoinTrailAnimations();
        ClearSeatPaymentUis();
        ClearTableStatusUis();
        ClearRepairTableUis();
        ClearPranksterUi();

        if (_dushRepairEffectRoutine != null)
        {
            StopCoroutine(_dushRepairEffectRoutine);
            _dushRepairEffectRoutine = null;
        }

        StopOpenBusinessSequence();
        ClearNotEnoughMoneyUis();

        SafeSetUiActive(_dushRepairUiRoot, false);
        ClearWorkerEnergyUis();

        if (Instance == this)
            Instance = null;
    }

    private void LateUpdate()
    {
        SyncAllHireSpotUi();
        UpdateSeatPaymentUiPositions();
        UpdateTableStatusPositions();
        UpdateNotEnoughMoneyUiPositions();
        UpdatePranksterChaseUi();
        UpdateRepairTableUis();
        UpdateEnterShopUiPosition();
        UpdateEnterCompetitorShopUiPositions();
        UpdateTownBuildingEffectPosition();
        UpdateCompetitorShopNameUiPositions();
        UpdateOwnShopNameUiPosition();
        UpdateOwnRatingUiPosition();
        FlushPendingGoldDelta();
    }

    public void ShowVipUi()
    {
        if (_vipUiRoot == null)
            return;

        _vipUiRoot.gameObject.SetActive(true);
        PlayVipFireworksIntro();
    }

    public void HideVipUi()
    {
        StopVipFireworksRoutine();
        StopVipDialogueHideRoutine();

        if (_vipFireworksRoot != null)
            _vipFireworksRoot.gameObject.SetActive(false);

        if (_vipUiRoot != null)
            _vipUiRoot.gameObject.SetActive(false);
    }

    public void SetVipDialogue(VipDialogueState state)
    {
        CacheVipDialogueText();

        string[] lines = ResolveVipDialogueLines(state);
        if (_vipDialogueText == null || lines == null || lines.Length == 0)
            return;

        string chosen = lines[UnityEngine.Random.Range(0, lines.Length)];
        if (string.IsNullOrWhiteSpace(chosen))
            return;

        StopVipDialogueHideRoutine();

        // Ensure nested Text Bg / VIP Text is visible under VIP Main UI.
        if (_vipDialogueText.transform.parent != null)
            _vipDialogueText.transform.parent.gameObject.SetActive(true);

        _vipDialogueText.gameObject.SetActive(true);
        _vipDialogueText.text = chosen;

        if (_vipUiRoot != null && !_vipUiRoot.gameObject.activeSelf)
            _vipUiRoot.gameObject.SetActive(true);

        if (state == VipDialogueState.UnhappyLeave || state == VipDialogueState.Discontent)
            _vipDialogueHideRoutine = StartCoroutine(HideVipDialogueAfterDelay(_vipNegativeDialogueHideDelay));
    }

    public void SetVipDialogueForEvent(VipEventType eventType)
    {
        SetVipDialogue(eventType switch
        {
            VipEventType.ServeDish => VipDialogueState.RequestServeDish,
            VipEventType.FeetMassage => VipDialogueState.RequestFeetMassage,
            VipEventType.ServeTea => VipDialogueState.RequestServeTea,
            VipEventType.CallLady => VipDialogueState.RequestCallLady,
            VipEventType.WatchStage => VipDialogueState.RequestWatchStage,
            _ => VipDialogueState.Discontent
        });
    }

    public void HideVipDialogue()
    {
        StopVipDialogueHideRoutine();
        CacheVipDialogueText();

        if (_vipDialogueText == null)
            return;

        if (_vipDialogueText.transform.parent != null)
            _vipDialogueText.transform.parent.gameObject.SetActive(false);

        _vipDialogueText.gameObject.SetActive(false);
    }

    private IEnumerator HideVipDialogueAfterDelay(float delay)
    {
        float wait = Mathf.Max(0f, delay);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        _vipDialogueHideRoutine = null;
        HideVipDialogue();
    }

    private void StopVipDialogueHideRoutine()
    {
        if (_vipDialogueHideRoutine == null)
            return;

        StopCoroutine(_vipDialogueHideRoutine);
        _vipDialogueHideRoutine = null;
    }

    private string[] ResolveVipDialogueLines(VipDialogueState state)
    {
        return state switch
        {
            VipDialogueState.Arrived => _vipDialogueArrived,
            VipDialogueState.UnhappyLeave => _vipDialogueUnhappyLeave,
            VipDialogueState.SuccessLeave => _vipDialogueSuccessLeave,
            VipDialogueState.RequestServeDish => _vipDialogueRequestServeDish,
            VipDialogueState.RequestFeetMassage => _vipDialogueRequestFeetMassage,
            VipDialogueState.RequestServeTea => _vipDialogueRequestServeTea,
            VipDialogueState.RequestCallLady => _vipDialogueRequestCallLady,
            VipDialogueState.RequestWatchStage => _vipDialogueRequestWatchStage,
            VipDialogueState.Discontent => _vipDialogueDiscontent,
            _ => null
        };
    }

    private void CacheVipDialogueText()
    {
        if (_vipDialogueText != null)
            return;

        EnsureScreenUiCaches();
        CacheVipUi();

        if (_vipUiRoot == null)
            return;

        Transform textRoot = FindChildTransform(_vipUiRoot, VipDialogueTextName);
        if (textRoot != null)
            _vipDialogueText = textRoot.GetComponent<TextMeshProUGUI>();
    }

    /// <summary>Legacy entry point used when the VIP reaches the entry waypoint.</summary>
    public void PlayVipAnnouncement()
    {
        ShowVipUi();
        SetVipDialogue(VipDialogueState.Arrived);
    }

    public void ShowVipIntroButton(Customer vip)
    {
        CacheVipIntroButtonUi();

        if (_vipIntroButtonRoot == null || vip == null)
            return;

        _vipIntroCustomer = vip;
        _vipIntroButtonRoot.gameObject.SetActive(true);
        UpdateVipIntroButtonPosition();
    }

    public void HideVipIntroButton()
    {
        _vipIntroCustomer = null;

        if (_vipIntroButtonRoot != null)
            _vipIntroButtonRoot.gameObject.SetActive(false);
    }

    public void ShowVipWaitTimer(Customer vip, float duration)
    {
        CacheVipWaitTimerUi();

        if (_vipWaitTimerRoot == null || vip == null)
            return;

        // Timer lives under VIP Main UI / VIP Icon — keep the banner visible.
        if (_vipUiRoot != null && !_vipUiRoot.gameObject.activeSelf)
            _vipUiRoot.gameObject.SetActive(true);

        _vipWaitTimerCustomer = vip;
        _vipWaitTimerDuration = Mathf.Max(0.01f, duration);
        _vipWaitTimerRemaining = _vipWaitTimerDuration;
        _vipWaitTimerRoot.gameObject.SetActive(true);
        ApplyVipWaitTimerVisuals();
    }

    public void UpdateVipWaitTimer(Customer vip, float remaining, float duration)
    {
        if (vip == null || _vipWaitTimerCustomer != vip)
            return;

        _vipWaitTimerDuration = Mathf.Max(0.01f, duration);
        _vipWaitTimerRemaining = Mathf.Clamp(remaining, 0f, _vipWaitTimerDuration);
        ApplyVipWaitTimerVisuals();
    }

    public void HideVipWaitTimer(Customer vip = null)
    {
        if (vip != null && _vipWaitTimerCustomer != null && _vipWaitTimerCustomer != vip)
            return;

        _vipWaitTimerCustomer = null;
        _vipWaitTimerRemaining = 0f;
        _vipWaitTimerDuration = 0f;

        if (_vipWaitTimerRoot != null)
            _vipWaitTimerRoot.gameObject.SetActive(false);

        ResetVipMainIconScale();
    }

    public void ShowVipEventButton(VipEventType eventType, Customer vip)
    {
        CacheVipEventButtonsUi();

        if (vip == null)
            return;

        RectTransform root = GetVipEventButtonRoot(eventType);
        if (root == null)
            return;

        SetVipEventButtonActive(_vipServeDishButtonRoot, eventType == VipEventType.ServeDish);
        SetVipEventButtonActive(_vipFeetMassageButtonRoot, eventType == VipEventType.FeetMassage);
        SetVipEventButtonActive(_vipServeTeaButtonRoot, eventType == VipEventType.ServeTea);
        SetVipEventButtonActive(_vipCallLadyButtonRoot, eventType == VipEventType.CallLady);
        SetVipEventButtonActive(_vipGeTaiButtonRoot, eventType == VipEventType.WatchStage);

        _vipEventCustomer = vip;
        _activeVipEventButton = eventType;
        root.SetAsLastSibling();
        SetVipDialogueForEvent(eventType);
        UpdateVipEventButtonPosition();
    }

    public void HideVipEventButton(VipEventType eventType)
    {
        RectTransform root = GetVipEventButtonRoot(eventType);
        if (root != null)
            root.gameObject.SetActive(false);

        if (_activeVipEventButton.HasValue && _activeVipEventButton.Value == eventType)
        {
            _activeVipEventButton = null;
            _vipEventCustomer = null;
        }
    }

    public void HideAllVipEventButtons()
    {
        SetVipEventButtonActive(_vipServeDishButtonRoot, false);
        SetVipEventButtonActive(_vipFeetMassageButtonRoot, false);
        SetVipEventButtonActive(_vipServeTeaButtonRoot, false);
        SetVipEventButtonActive(_vipCallLadyButtonRoot, false);
        SetVipEventButtonActive(_vipGeTaiButtonRoot, false);

        _activeVipEventButton = null;
        _vipEventCustomer = null;
    }

    private void HideVipDoneEating()
    {
        if (_vipDoneEatingRoot != null)
            _vipDoneEatingRoot.gameObject.SetActive(false);
    }

    private void CacheVipDoneEatingUi()
    {
        if (_screenCanvas == null)
            return;

        if (_vipDoneEatingRoot == null)
            _vipDoneEatingRoot = FindRectTransformByName(_screenCanvas.transform, VipDoneEatingRootName);

        if (_vipDoneEatingRoot != null)
            _vipDoneEatingRoot.gameObject.SetActive(false);
    }

    public void PlayPranksterChasedAwayDialogue()
    {
        PlayPranksterDialogue(PranksterDialogueType.ChasedAway);
    }

    public void PlayPranksterTableBrokenDialogue()
    {
        PlayPranksterDialogue(PranksterDialogueType.TableBroken);
    }

    private void PlayPranksterDialogue(PranksterDialogueType dialogueType)
    {
        if (!RestaurantSceneMode.IsMainScene)
            return;

        CachePranksterDialogueUi();

        if (_pranksterDialogueRootRuntime == null)
            return;

        if (_pranksterDialogueRoutine != null)
            StopCoroutine(_pranksterDialogueRoutine);

        _pranksterDialogueRoutine = StartCoroutine(PlayPranksterDialogueCoroutine(dialogueType));
    }

    private void HidePranksterDialogue()
    {
        if (_pranksterDialogueRoutine != null)
        {
            StopCoroutine(_pranksterDialogueRoutine);
            _pranksterDialogueRoutine = null;
        }

        if (_pranksterDialogueRootRuntime == null)
            return;

        if (_pranksterDialogueGraphics != null && _pranksterDialogueTargetColors != null)
            UiGraphicFade.RestoreColors(_pranksterDialogueGraphics, _pranksterDialogueTargetColors);

        _pranksterDialogueRootRuntime.gameObject.SetActive(false);
    }

    private void CachePranksterDialogueUi()
    {
        if (_screenCanvas == null)
            return;

        if (_pranksterDialogueRootRuntime == null)
        {
            if (_pranksterDialogueRoot != null)
                _pranksterDialogueRootRuntime = _pranksterDialogueRoot;
            else
                _pranksterDialogueRootRuntime = FindRectTransformByName(_screenCanvas.transform, PranksterDialogueRootName);
        }

        if (_pranksterDialogueRootRuntime == null)
            return;

        if (_pranksterDialogueText == null)
        {
            Transform textRoot = FindChildTransform(_pranksterDialogueRootRuntime, PranksterDialogueTextName);
            _pranksterDialogueText = textRoot != null ? textRoot.GetComponent<TextMeshProUGUI>() : null;
        }

        if (_pranksterDialogueIconImage == null)
        {
            Transform iconRoot = FindChildTransform(_pranksterDialogueRootRuntime, PranksterDialogueIconName);
            _pranksterDialogueIconImage = iconRoot != null ? iconRoot.GetComponent<Image>() : null;
        }

        _pranksterDialogueGraphics = _pranksterDialogueRootRuntime.GetComponentsInChildren<Graphic>(true);
        _pranksterDialogueTargetColors = UiGraphicFade.CaptureColors(_pranksterDialogueGraphics);

        for (int i = 0; i < _pranksterDialogueGraphics.Length; i++)
        {
            if (_pranksterDialogueGraphics[i] != null)
                _pranksterDialogueGraphics[i].raycastTarget = false;
        }

        _pranksterDialogueRootRuntime.gameObject.SetActive(false);
    }

    private void PreparePranksterDialogueContent(PranksterDialogueType dialogueType)
    {
        PreparePranksterDialogueIcon(dialogueType);

        if (_pranksterDialogueText != null)
        {
            _pranksterDialogueText.gameObject.SetActive(true);

            string[] messages = dialogueType == PranksterDialogueType.TableBroken
                ? _pranksterTableBrokenMessages
                : _pranksterChasedAwayMessages;

            if (messages != null && messages.Length > 0)
                _pranksterDialogueText.text = messages[UnityEngine.Random.Range(0, messages.Length)];
        }

        if (_pranksterDialogueIconImage != null)
            _pranksterDialogueIconImage.gameObject.SetActive(true);
    }

    private void PreparePranksterDialogueIcon(PranksterDialogueType dialogueType)
    {
        if (_pranksterDialogueIconImage == null)
            return;

        _pranksterDialogueIconImage.gameObject.SetActive(true);

        if (dialogueType == PranksterDialogueType.TableBroken)
        {
            if (_pranksterDialogueSmirkIcon != null)
                _pranksterDialogueIconImage.sprite = _pranksterDialogueSmirkIcon;

            return;
        }

        Sprite angryIcon = PickRandomPranksterAngryIcon();

        if (angryIcon != null)
            _pranksterDialogueIconImage.sprite = angryIcon;
    }

    private Sprite PickRandomPranksterAngryIcon()
    {
        bool hasAngry1 = _pranksterDialogueAngryIcon1 != null;
        bool hasAngry2 = _pranksterDialogueAngryIcon2 != null;

        if (hasAngry1 && hasAngry2)
            return UnityEngine.Random.value < 0.5f ? _pranksterDialogueAngryIcon1 : _pranksterDialogueAngryIcon2;

        if (hasAngry1)
            return _pranksterDialogueAngryIcon1;

        return hasAngry2 ? _pranksterDialogueAngryIcon2 : null;
    }

    private IEnumerator PlayPranksterDialogueCoroutine(PranksterDialogueType dialogueType)
    {
        PreparePranksterDialogueContent(dialogueType);

        _pranksterDialogueGraphics = _pranksterDialogueRootRuntime.GetComponentsInChildren<Graphic>(true);
        _pranksterDialogueTargetColors = UiGraphicFade.CaptureColors(_pranksterDialogueGraphics);
        Color[] transparentColors = UiGraphicFade.BuildTransparentColors(_pranksterDialogueTargetColors);

        _pranksterDialogueRootRuntime.gameObject.SetActive(true);
        UiGraphicFade.RestoreColors(_pranksterDialogueGraphics, transparentColors);

        yield return UiGraphicFade.FadeColors(
            _pranksterDialogueGraphics,
            transparentColors,
            _pranksterDialogueTargetColors,
            _pranksterDialogueFadeInDuration);

        if (_pranksterDialogueHoldDuration > 0f)
            yield return new WaitForSeconds(_pranksterDialogueHoldDuration);

        yield return UiGraphicFade.FadeColors(
            _pranksterDialogueGraphics,
            _pranksterDialogueTargetColors,
            transparentColors,
            _pranksterDialogueFadeOutDuration);

        _pranksterDialogueRootRuntime.gameObject.SetActive(false);
        UiGraphicFade.RestoreColors(_pranksterDialogueGraphics, _pranksterDialogueTargetColors);
        _pranksterDialogueRoutine = null;
    }

    private void CacheVipUi()
    {
        if (_screenCanvas == null)
            return;

        if (_vipUiRoot == null)
            _vipUiRoot = FindRectTransformByName(_screenCanvas.transform, VipUiRootName);

        if (_vipUiRoot == null)
            return;

        if (_vipFireworksRoot == null)
        {
            Transform fireworks = FindChildTransform(_vipUiRoot, VipFireworksName);
            if (fireworks != null)
                _vipFireworksRoot = fireworks as RectTransform;
        }

        if (_vipFireworksRoot != null)
        {
            _vipFireworksAnimator = _vipFireworksRoot.GetComponent<Animator>();
            _vipFireworksGraphics = _vipFireworksRoot.GetComponentsInChildren<Graphic>(true);
            _vipFireworksTargetColors = UiGraphicFade.CaptureColors(_vipFireworksGraphics);

            for (int i = 0; i < _vipFireworksGraphics.Length; i++)
            {
                if (_vipFireworksGraphics[i] != null)
                    _vipFireworksGraphics[i].raycastTarget = false;
            }

            _vipFireworksRoot.gameObject.SetActive(false);
        }

        DisableChildRaycastTargets(_vipUiRoot);
        CacheVipDialogueText();
        CacheVipMainIconUi();
        _vipUiRoot.gameObject.SetActive(false);
    }

    private void CacheVipMainIconUi()
    {
        if (_vipUiRoot == null)
            CacheVipUi();

        if (_vipUiRoot == null)
            return;

        if (_vipMainIconRoot != null)
            return;

        Transform icon = FindChildTransform(_vipUiRoot, VipMainIconName);
        if (icon != null)
            _vipMainIconRoot = icon as RectTransform;
    }

    private void CacheVipIntroButtonUi()
    {
        EnsureScreenUiCaches();

        if (_vipIntroButtonRoot == null && _screenCanvas != null)
            _vipIntroButtonRoot = FindRectTransformByName(_screenCanvas.transform, VipIntroButtonName);

        if (_vipIntroButtonRoot == null)
            return;

        if (_vipIntroButton == null)
            _vipIntroButton = _vipIntroButtonRoot.GetComponent<Button>()
                ?? _vipIntroButtonRoot.GetComponentInChildren<Button>(true);

        if (_vipIntroButton != null && !_vipIntroButtonWired)
        {
            _vipIntroButton.onClick.AddListener(HandleVipIntroButtonClicked);
            _vipIntroButtonWired = true;
        }

        _vipIntroButtonRoot.gameObject.SetActive(false);
    }

    private void CacheVipWaitTimerUi()
    {
        EnsureScreenUiCaches();

        if (_vipWaitTimerRoot == null)
        {
            // Prefer the timer nested under VIP Main UI → VIP Icon.
            if (_vipUiRoot != null)
                _vipWaitTimerRoot = FindRectTransformByName(_vipUiRoot, VipWaitTimerName);

            if (_vipWaitTimerRoot == null && _screenCanvas != null)
                _vipWaitTimerRoot = FindRectTransformByName(_screenCanvas.transform, VipWaitTimerName);
        }

        if (_vipWaitTimerRoot == null)
            return;

        if (_vipWaitTimerFillImage == null)
        {
            Transform fillTransform = FindChildTransform(_vipWaitTimerRoot, VipWaitTimerFillName);
            if (fillTransform != null)
                _vipWaitTimerFillImage = fillTransform.GetComponent<Image>();
            else
                _vipWaitTimerFillImage = _vipWaitTimerRoot.GetComponent<Image>();
        }

        if (_vipWaitTimerCountdownText == null)
        {
            Transform countdownTransform = FindChildTransform(_vipWaitTimerRoot, VipWaitTimerCountdownName);
            if (countdownTransform != null)
                _vipWaitTimerCountdownText = countdownTransform.GetComponent<TextMeshProUGUI>();
        }

        // Timer is display-only — never block taps on the VIP request buttons.
        Graphic[] timerGraphics = _vipWaitTimerRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < timerGraphics.Length; i++)
        {
            if (timerGraphics[i] != null)
                timerGraphics[i].raycastTarget = false;
        }

        _vipWaitTimerRoot.gameObject.SetActive(false);
    }

    private void CacheVipEventButtonsUi()
    {
        EnsureScreenUiCaches();

        CacheVipEventButton(
            ref _vipServeDishButtonRoot,
            ref _vipServeDishButton,
            VipServeDishButtonName,
            VipEventType.ServeDish);

        CacheVipEventButton(
            ref _vipFeetMassageButtonRoot,
            ref _vipFeetMassageButton,
            VipFeetMassageButtonName,
            VipEventType.FeetMassage);

        CacheVipEventButton(
            ref _vipServeTeaButtonRoot,
            ref _vipServeTeaButton,
            VipServeTeaButtonName,
            VipEventType.ServeTea);

        CacheVipEventButton(
            ref _vipCallLadyButtonRoot,
            ref _vipCallLadyButton,
            VipCallLadyButtonName,
            VipEventType.CallLady);

        CacheVipEventButton(
            ref _vipGeTaiButtonRoot,
            ref _vipGeTaiButton,
            VipGeTaiButtonName,
            VipEventType.WatchStage);

        if (_vipGeTaiButton == null && _vipGeTaiButtonRoot != null)
            _vipGeTaiButton = EnsureButtonOnObject(_vipGeTaiButtonRoot.gameObject);

        if (!_vipEventButtonsWired)
        {
            WireVipEventButton(_vipServeDishButton, VipEventType.ServeDish);
            WireVipEventButton(_vipFeetMassageButton, VipEventType.FeetMassage);
            WireVipEventButton(_vipServeTeaButton, VipEventType.ServeTea);
            WireVipEventButton(_vipCallLadyButton, VipEventType.CallLady);
            WireVipEventButton(_vipGeTaiButton, VipEventType.WatchStage);
            _vipEventButtonsWired = true;

            SetVipEventButtonActive(_vipServeDishButtonRoot, false);
            SetVipEventButtonActive(_vipFeetMassageButtonRoot, false);
            SetVipEventButtonActive(_vipServeTeaButtonRoot, false);
            SetVipEventButtonActive(_vipCallLadyButtonRoot, false);
            SetVipEventButtonActive(_vipGeTaiButtonRoot, false);
            _activeVipEventButton = null;
            _vipEventCustomer = null;
        }
    }

    private void CacheVipEventButton(
        ref RectTransform root,
        ref Button button,
        string objectName,
        VipEventType eventType)
    {
        if (root == null && _screenCanvas != null)
            root = FindRectTransformByName(_screenCanvas.transform, objectName);

        if (root == null)
            return;

        if (button == null)
            button = root.GetComponent<Button>() ?? root.GetComponentInChildren<Button>(true);
    }

    private void WireVipEventButton(Button button, VipEventType eventType)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        VipEventType captured = eventType;
        button.onClick.AddListener(() => HandleVipEventButtonClicked(captured));
    }

    private void HandleVipEventButtonClicked(VipEventType eventType)
    {
        CustomerManager.Instance?.AcknowledgeVipEvent(eventType);
        HideVipDialogue();
        HideVipEventButton(eventType);
    }

    private RectTransform GetVipEventButtonRoot(VipEventType eventType)
    {
        return eventType switch
        {
            VipEventType.ServeDish => _vipServeDishButtonRoot,
            VipEventType.FeetMassage => _vipFeetMassageButtonRoot,
            VipEventType.ServeTea => _vipServeTeaButtonRoot,
            VipEventType.CallLady => _vipCallLadyButtonRoot,
            VipEventType.WatchStage => _vipGeTaiButtonRoot,
            _ => null
        };
    }

    private static void SetVipEventButtonActive(RectTransform root, bool active)
    {
        if (root != null)
            root.gameObject.SetActive(active);
    }

    private void HandleVipIntroButtonClicked()
    {
        CustomerManager.Instance?.AcknowledgeVipIntro();
        HideVipDialogue();
        HideVipIntroButton();
    }

    private void UpdateVipIntroButtonPosition()
    {
        if (_vipIntroButtonRoot == null || _vipIntroCustomer == null)
            return;

        if (!_vipIntroButtonRoot.gameObject.activeSelf)
            return;

        Transform anchor = _vipIntroCustomer.ReactPoint;
        if (anchor == null)
            return;

        // Keep the button active while intro is pending — only refresh position when projection works.
        TryUpdateScreenUiPosition(_vipIntroButtonRoot, anchor.position);
    }

    private void UpdateVipEventButtonPosition()
    {
        if (!_activeVipEventButton.HasValue || _vipEventCustomer == null)
            return;

        if (_activeVipEventButton.Value == VipEventType.WatchStage)
        {
            UpdateVipGeTaiButtonPosition();
            return;
        }

        RectTransform root = GetVipEventButtonRoot(_activeVipEventButton.Value);
        if (root == null)
            return;

        // VIP event buttons are second-floor only (same as GeTai).
        bool onSecondFloor = CharacterPanelController.Instance == null
            || CharacterPanelController.Instance.CurrentFloor == 2;

        if (!onSecondFloor)
        {
            if (root.gameObject.activeSelf)
                root.gameObject.SetActive(false);
            return;
        }

        EnsureScreenUiCaches();

        Transform anchor = _vipEventCustomer.ReactPoint != null
            ? _vipEventCustomer.ReactPoint
            : _vipEventCustomer.LakePoint;

        if (anchor == null)
            return;

        if (!TryGetEdgeClampedVipButtonLocalPoint(anchor.position, root, out Vector2 localPoint))
            return;

        if (!root.gameObject.activeSelf)
            root.gameObject.SetActive(true);

        root.anchoredPosition = localPoint;
    }

    private void UpdateVipGeTaiButtonPosition()
    {
        RectTransform root = _vipGeTaiButtonRoot;
        if (root == null)
            return;

        // GeTai / stage UI is second-floor only.
        bool onSecondFloor = CharacterPanelController.Instance == null
            || CharacterPanelController.Instance.CurrentFloor == 2;

        if (!onSecondFloor)
        {
            if (root.gameObject.activeSelf)
                root.gameObject.SetActive(false);
            return;
        }

        EnsureScreenUiCaches();

        Transform stage = CustomerManager.Instance != null
            ? CustomerManager.Instance.VipStagePoint
            : null;

        if (stage == null)
            stage = FindSceneTransformByName("Placeholder Stage")
                ?? FindSceneTransformByName("GeTai");

        if (stage == null)
            return;

        if (!TryGetEdgeClampedVipButtonLocalPoint(stage.position, root, out Vector2 localPoint))
            return;

        if (!root.gameObject.activeSelf)
            root.gameObject.SetActive(true);

        root.anchoredPosition = localPoint;
    }

    private bool TryGetEdgeClampedVipButtonLocalPoint(
        Vector3 worldAnchorPosition,
        RectTransform root,
        out Vector2 localPoint)
    {
        localPoint = default;

        EnsureScreenUiCaches();

        if (_worldCamera == null || _canvasRect == null || _screenCanvas == null)
            return false;

        Vector3 screenPoint = _worldCamera.WorldToScreenPoint(worldAnchorPosition);
        if (screenPoint.z <= 0f)
        {
            // Behind camera — pin to nearest horizontal edge using viewport direction.
            Vector3 viewport = _worldCamera.WorldToViewportPoint(worldAnchorPosition);
            screenPoint.x = viewport.x < 0.5f ? 0f : Screen.width;
            screenPoint.y = Mathf.Clamp(Screen.height * 0.5f, 0f, Screen.height);
            screenPoint.z = 1f;
        }

        float halfW = Mathf.Max(40f, root != null ? root.rect.width * 0.5f : 40f);
        float halfH = Mathf.Max(40f, root != null ? root.rect.height * 0.5f : 40f);
        float pad = 24f;

        // Persistent on floor 2: clamp to screen edges when the anchor is off-screen.
        screenPoint.x = Mathf.Clamp(screenPoint.x, pad + halfW, Screen.width - pad - halfW);
        screenPoint.y = Mathf.Clamp(screenPoint.y, pad + halfH, Screen.height - pad - halfH);

        Camera canvasCamera = _screenCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _screenCanvas.worldCamera;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect,
            screenPoint,
            canvasCamera,
            out localPoint);
    }

    private static Transform FindSceneTransformByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform t = transforms[i];
            if (t != null && t.name == objectName)
                return t;
        }

        return null;
    }

    private void UpdateVipReceptionUi()
    {
        UpdateVipIntroButtonPosition();
        UpdateVipEventButtonPosition();
        UpdateVipMainIconPulse();
    }

    private void UpdateVipMainIconPulse()
    {
        CacheVipMainIconUi();

        if (_vipMainIconRoot == null)
            return;

        bool shouldPulse = _vipWaitTimerCustomer != null
            && _vipWaitTimerRoot != null
            && _vipWaitTimerRoot.gameObject.activeInHierarchy
            && _vipWaitTimerRemaining > 0f
            && _vipWaitTimerRemaining <= _vipIconPulseRemainingThreshold;

        if (shouldPulse)
        {
            _vipMainIconRoot.localScale = Vector3.one * GetPulseScale(
                _vipIconPulseMinScale,
                _vipIconPulseMaxScale,
                _vipIconPulseSpeed);
            return;
        }

        ResetVipMainIconScale();
    }

    private void ResetVipMainIconScale()
    {
        if (_vipMainIconRoot != null)
            _vipMainIconRoot.localScale = Vector3.one;
    }

    private void ApplyVipWaitTimerVisuals()
    {
        float normalized = _vipWaitTimerDuration > 0f
            ? Mathf.Clamp01(_vipWaitTimerRemaining / _vipWaitTimerDuration)
            : 0f;

        if (_vipWaitTimerFillImage != null)
            _vipWaitTimerFillImage.fillAmount = normalized;

        if (_vipWaitTimerCountdownText != null)
        {
            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, _vipWaitTimerRemaining));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            _vipWaitTimerCountdownText.text = $"{minutes:00}:{seconds:00}";
        }
    }

    private void PlayVipFireworksIntro()
    {
        if (_vipFireworksRoot == null)
            return;

        StopVipFireworksRoutine();
        _vipFireworksRoutine = StartCoroutine(PlayVipFireworksIntroCoroutine());
    }

    private void StopVipFireworksRoutine()
    {
        if (_vipFireworksRoutine != null)
        {
            StopCoroutine(_vipFireworksRoutine);
            _vipFireworksRoutine = null;
        }

        if (_vipFireworksGraphics != null && _vipFireworksTargetColors != null)
            UiGraphicFade.RestoreColors(_vipFireworksGraphics, _vipFireworksTargetColors);
    }

    private IEnumerator PlayVipFireworksIntroCoroutine()
    {
        _vipFireworksGraphics = _vipFireworksRoot.GetComponentsInChildren<Graphic>(true);
        _vipFireworksTargetColors = UiGraphicFade.CaptureColors(_vipFireworksGraphics);

        _vipFireworksRoot.gameObject.SetActive(true);
        UiGraphicFade.RestoreColors(_vipFireworksGraphics, _vipFireworksTargetColors);

        if (_vipFireworksAnimator != null)
        {
            _vipFireworksAnimator.enabled = true;
            _vipFireworksAnimator.Rebind();
            _vipFireworksAnimator.Update(0f);
            _vipFireworksAnimator.Play(0, 0, 0f);
        }

        float holdDuration = Mathf.Max(0f, _vipFireworksHoldDuration);
        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        Color[] transparentColors = UiGraphicFade.BuildTransparentColors(_vipFireworksTargetColors);
        yield return UiGraphicFade.FadeColors(
            _vipFireworksGraphics,
            _vipFireworksTargetColors,
            transparentColors,
            _vipFireworksFadeOutDuration);

        _vipFireworksRoot.gameObject.SetActive(false);
        UiGraphicFade.RestoreColors(_vipFireworksGraphics, _vipFireworksTargetColors);
        _vipFireworksRoutine = null;
    }

    public void RegisterWorkerEnergyUi(Worker worker)
    {
        if (!RestaurantSceneMode.UsesWorkerEnergyUi || worker == null)
            return;

        if (_activeWorkerEnergyUis.ContainsKey(worker))
        {
            if (IsWorkerEnergyEntryValid(_activeWorkerEnergyUis[worker]))
                return;

            UnregisterWorkerEnergyUi(worker);
        }

        WorkerEnergyUiBinding binding = ResolveWorkerEnergyUiBinding(worker);

        if (binding == null)
        {
            Debug.LogWarning($"UIManager could not find worker energy UI for {worker.name}.", this);
            return;
        }

        EnsureWorkerEnergyUiBinding(binding);

        if (binding.Root == null || binding.FillImage == null)
        {
            Debug.LogWarning($"Worker energy UI for {worker.name} is missing fill image.", this);
            return;
        }

        WorkerEnergyUiEntry entry = CreateWorkerEnergyUiEntry(worker, binding);
        _activeWorkerEnergyUis[worker] = entry;
        SetWorkerEnergyUiVisible(binding.Root, ShouldShowWorkerEnergyUi(worker));
        RefreshWorkerEnergyUi(worker);
    }

    public void UnregisterWorkerEnergyUi(Worker worker)
    {
        if (worker == null || !_activeWorkerEnergyUis.TryGetValue(worker, out WorkerEnergyUiEntry entry))
            return;

        if (entry.FillImage != null)
            entry.FillImage.color = entry.NormalFillColor;

        if (entry.UiRoot != null)
            entry.UiRoot.gameObject.SetActive(false);

        _activeWorkerEnergyUis.Remove(worker);
    }

    private void HandleStateChanged(GameState state)
    {
        SyncActiveUi();
        SyncOpenBusinessUiVisibility();

        if (state != GameState.Business)
        {
            HideVipUi();
            HideVipDoneEating();
            HideUpgradeTableUi();
        }
    }

    private void HandleTableClicked(DiningTable table)
    {
        if (table == null || GameManager.Instance == null || !GameManager.Instance.IsBusiness)
            return;

        if (table.IsUpgrading)
            return;

        if (table.IsBroken)
            return;

        if (!table.CanUpgrade)
            return;

        ShowUpgradeTableUi(table);
    }

    private void HandleTableUpgraded(DiningTable table)
    {
        if (table == null)
            return;

        RefreshTableStatusUi(table);

        if (_selectedUpgradeTable == table)
            HideUpgradeTableUi();
    }

    private void HandleUpgradeCostClicked()
    {
        if (_selectedUpgradeTable == null)
            return;

        if (_selectedUpgradeTable.TryBeginUpgrade())
            HideUpgradeTableUi();
    }

    private void HandleUpgradeTableCloseClicked()
    {
        HideUpgradeTableUi();
    }

    private void ShowUpgradeTableUi(DiningTable table)
    {
        if (table == null || _upgradeTableRoot == null)
            return;

        _selectedUpgradeTable = table;
        RefreshUpgradeTableUi();
        _upgradeTableRoot.SetActive(true);
    }

    private void HideUpgradeTableUi()
    {
        _selectedUpgradeTable = null;

        if (_upgradeTableRoot != null)
            _upgradeTableRoot.SetActive(false);
    }

    private void RefreshUpgradeTableUi()
    {
        if (_selectedUpgradeTable == null)
            return;

        int currentLevel = _selectedUpgradeTable.Level;
        int nextLevel = Mathf.Min(currentLevel + 1, _selectedUpgradeTable.MaxTableLevel);

        if (_upgradeLeftText != null)
            _upgradeLeftText.text = $"本卓{currentLevel}级";

        if (_upgradeRightText != null)
            _upgradeRightText.text = $"本卓{nextLevel}级";

        if (_upgradeLeftImage != null)
            _upgradeLeftImage.sprite = GetTableLevelSprite(currentLevel);

        if (_upgradeRightImage != null)
            _upgradeRightImage.sprite = GetTableLevelSprite(nextLevel);

        if (_upgradeCostText != null)
        {
            _upgradeCostText.text = _selectedUpgradeTable.CanUpgrade
                ? _selectedUpgradeTable.GetUpgradeCost().ToString()
                : string.Empty;
        }

        RefreshUpgradeCostAffordability();
    }

    private void RefreshUpgradeCostAffordability()
    {
        if (_upgradeCostButton == null || _selectedUpgradeTable == null)
            return;

        _upgradeCostButton.interactable = _selectedUpgradeTable.CanUpgrade;
    }

    private Sprite GetTableLevelSprite(int level)
    {
        if (_tableLevelSprites == null || _tableLevelSprites.Length == 0)
            return null;

        int index = Mathf.Clamp(level - 1, 0, _tableLevelSprites.Length - 1);
        return _tableLevelSprites[index];
    }

    private void CacheUpgradeTableUiReferences()
    {
        if (_upgradeTableRoot == null)
            return;

        Transform root = _upgradeTableRoot.transform;

        if (_upgradeLeftText == null)
            _upgradeLeftText = FindChildText(root, "Left Text");

        if (_upgradeRightText == null)
            _upgradeRightText = FindChildText(root, "Right Text");

        if (_upgradeLeftImage == null)
            _upgradeLeftImage = FindChildImage(root, "Left");

        if (_upgradeRightImage == null)
            _upgradeRightImage = FindChildImage(root, "Right");

        if (_upgradeCostText == null)
            _upgradeCostText = FindChildText(root, "Cost");

        if (_upgradeCostButton == null)
        {
            Transform upgradeCostUi = FindChildTransform(root, "Upgrade Cost UI");

            if (upgradeCostUi != null)
                _upgradeCostButton = upgradeCostUi.GetComponent<Button>();
        }

        if (_upgradeTableCloseButton == null)
            _upgradeTableCloseButton = FindUpgradeTableCloseButton(root);
    }

    private static Button FindUpgradeTableCloseButton(Transform root)
    {
        string[] closeButtonNames = { "Close UI", "CloseButton", "Close Button" };

        for (int i = 0; i < closeButtonNames.Length; i++)
        {
            Button button = FindChildButton(root, closeButtonNames[i]);

            if (button != null)
                return button;
        }

        Transform closeUi = FindChildTransform(root, "Close UI");

        if (closeUi == null)
            return null;

        Button existingButton = closeUi.GetComponent<Button>();

        if (existingButton != null)
            return existingButton;

        Image image = closeUi.GetComponent<Image>();

        if (image == null)
            return null;

        Button closeButton = closeUi.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = image;
        return closeButton;
    }

    private static RectTransform FindChildRectTransform(Transform root, string objectName)
    {
        Transform found = FindChildTransform(root, objectName);
        return found as RectTransform;
    }

    private static Transform FindChildTransform(Transform root, string objectName)
    {
        if (string.Equals(root.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildTransform(root.GetChild(i), objectName);

            if (found != null)
                return found;
        }

        return null;
    }

    private static TextMeshProUGUI FindChildText(Transform root, string objectName) =>
        FindChildComponent<TextMeshProUGUI>(root, objectName);

    private static Image FindChildImage(Transform root, string objectName) =>
        FindChildComponent<Image>(root, objectName);

    private static Button FindChildButton(Transform root, string objectName) =>
        FindChildComponent<Button>(root, objectName);

    private static T FindChildComponent<T>(Transform root, string objectName) where T : Component
    {
        T[] components = root.GetComponentsInChildren<T>(true);

        for (int i = 0; i < components.Length; i++)
        {
            if (string.Equals(components[i].gameObject.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                return components[i];
        }

        return null;
    }

    public void SetBuildSpotCostUiSuppressed(bool suppressed)
    {
        _buildSpotCostUiSuppressed = suppressed;
        SyncAllNestedBuildSpotCostUi();
    }

    private void HandleBuildSpotStateChanged(BuildSpot spot, BuildSpotState state)
    {
        if (spot == null)
            return;

        spot.SetCostUiSuppressed(_buildSpotCostUiSuppressed || IsActiveTownScene());
        spot.RefreshCostUi();
    }

    private void SyncAllNestedBuildSpotCostUi()
    {
        bool suppressed = _buildSpotCostUiSuppressed || IsActiveTownScene();
        BuildSpot[] spots = FindObjectsByType<BuildSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < spots.Length; i++)
        {
            BuildSpot spot = spots[i];
            if (spot == null)
                continue;

            spot.SetCostUiSuppressed(suppressed);
            spot.RefreshCostUi();
        }
    }

    private void HandleHireSpotStateChanged(HireSpot spot, HireSpotState state)
    {
        SyncAllHireSpotUi();
    }

    private void HandleRestaurantFloorChanged(int floor)
    {
        SyncAllNestedBuildSpotCostUi();
        SyncAllHireSpotUi();
        SyncAllTableStatusUiForCurrentFloor();
        SyncWorkerEnergyUiForCurrentFloor();
        SyncSeatPaymentUiForCurrentFloor();
        UpdateVipEventButtonPosition();
    }

    private void HandleSecondFloorUnlockedForUi()
    {
        SyncAllNestedBuildSpotCostUi();
        SyncAllHireSpotUi();
        SyncAllTableStatusUiForCurrentFloor();
        SyncWorkerEnergyUiForCurrentFloor();
        SyncSeatPaymentUiForCurrentFloor();
    }

    private void HandleHiringCompleted()
    {
        SyncOpenBusinessUiVisibility();
    }

    private void HandleMissionPartChanged(int partIndex)
    {
        SyncOpenBusinessUiVisibility();
        SyncActiveUi();
    }

    private void HandleMissionPartCompletedForOpenBusiness(int partIndex)
    {
        SyncOpenBusinessUiVisibility();

        if (partIndex == MissionCatalog.StarterBuildMissionPartIndex)
            SyncActiveUi();
    }

    private void HandleBusinessSessionStarted()
    {
        _openBusinessSequencePlayed = true;
        StopOpenBusinessSequence();
        HideOpenBusiness();
        HideBusinessOverview();
        HideBusinessTimer();
        SyncActiveUi();
        SyncAllNestedBuildSpotCostUi();
        SyncAllHireSpotUi();
    }

    private void HandleBusinessDowntimeStarted()
    {
        HideBusinessOverview();
        SyncOpenBusinessUiVisibility();
        SyncActiveUi();
        SyncAllNestedBuildSpotCostUi();
    }

    private void HandleBusinessAcknowledgeClicked()
    {
        if (GameManager.Instance == null)
            return;

        GameManager.Instance.AcknowledgeBusinessCloseSummary();
        SyncOpenBusinessUiVisibility();
        SyncActiveUi();
        SyncAllNestedBuildSpotCostUi();
    }

    private void HandleTableBuildInfoRequested()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsBuilding)
            return;

        ShowTableBuildInfoUi();
    }

    private void HandleTableBuildInfoCloseClicked()
    {
        HideTableBuildInfoUi();
        PlayerProfileStorage.SetTableBuildInfoDismissedForCurrentPlayer();
    }

    private void InitializeOpenBusinessUi()
    {
        if (!RestaurantSceneMode.IsMainScene)
        {
            HideOpenBusiness();
            HideBusinessTimer();
            return;
        }

        CacheOpenBusinessUiReferences();
        HideBusinessTimer();
        DisableOpenBusinessInteraction();
        // Visibility is synced after mission UI initializes.
    }

    private void InitializeBusinessOverviewUi()
    {
        CacheBusinessOverviewUiReferences();
        HideBusinessOverview();
    }

    private void HandleOpenBusinessClicked()
    {
        // Open Business is announcement-only now; kept for any leftover button wiring.
        TryBeginOpenBusinessAnnouncement();
    }

    private void SyncOpenBusinessUiVisibility()
    {
        if (!RestaurantSceneMode.IsMainScene)
        {
            HideOpenBusiness();
            return;
        }

        CacheOpenBusinessUiReferences();

        if (ShouldPlayOpenBusinessAnnouncement())
            TryBeginOpenBusinessAnnouncement();
        else if (_openBusinessSequenceRoutine == null)
            HideOpenBusiness();
    }

    private void CacheOpenBusinessUiReferences()
    {
        if (_openBusinessRoot == null)
            _openBusinessRoot = FindSceneUiObject("Open Business");

        if (_openBusinessButton == null && _openBusinessRoot != null)
            _openBusinessButton = _openBusinessRoot.GetComponent<Button>()
                ?? _openBusinessRoot.GetComponentInChildren<Button>(true);

        DisableOpenBusinessInteraction();

        if (_businessTimerText == null)
        {
            RectTransform timerRect = FindScreenUiRect("Timer");
            _businessTimerText = timerRect != null ? timerRect.GetComponent<TextMeshProUGUI>() : null;
        }
    }

    private void DisableOpenBusinessInteraction()
    {
        if (_openBusinessRoot == null)
            return;

        Button[] buttons = _openBusinessRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
                continue;

            buttons[i].onClick.RemoveListener(HandleOpenBusinessClicked);
            buttons[i].interactable = false;
            buttons[i].enabled = false;
        }

        Graphic[] graphics = _openBusinessRoot.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = false;
        }
    }

    private static bool ShouldPlayOpenBusinessAnnouncement()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsBusinessOpen)
            return false;

        if (PlayerProfileStorage.HasMainSceneBusinessStartedForCurrentPlayer())
            return false;

        MissionUiController missionUi = FindFirstObjectByType<MissionUiController>();
        int missionPart = missionUi != null
            ? missionUi.CurrentPartIndex
            : PlayerProfileStorage.GetMainSceneMissionPartIndexForCurrentPlayer();

        return missionPart >= MissionCatalog.OpenBusinessMissionPartIndex;
    }

    private void TryBeginOpenBusinessAnnouncement()
    {
        if (_openBusinessSequencePlayed || _openBusinessSequenceRoutine != null)
            return;

        if (!ShouldPlayOpenBusinessAnnouncement())
            return;

        _openBusinessSequenceRoutine = StartCoroutine(PlayOpenBusinessAnnouncementRoutine());
    }

    private IEnumerator PlayOpenBusinessAnnouncementRoutine()
    {
        _openBusinessSequencePlayed = true;
        CacheOpenBusinessUiReferences();

        if (_openBusinessRoot == null)
        {
            OpenBusinessAutomatically();
            _openBusinessSequenceRoutine = null;
            yield break;
        }

        Graphic[] graphics = _openBusinessRoot.GetComponentsInChildren<Graphic>(true);
        Color[] targetColors = UiGraphicFade.CaptureColors(graphics);
        Color[] transparentColors = UiGraphicFade.BuildTransparentColors(targetColors);

        _openBusinessRoot.SetActive(true);
        UiGraphicFade.RestoreColors(graphics, transparentColors);

        yield return UiGraphicFade.FadeColors(
            graphics,
            transparentColors,
            targetColors,
            _openBusinessFadeInDuration);

        if (_openBusinessHoldDuration > 0f)
            yield return new WaitForSeconds(_openBusinessHoldDuration);

        yield return UiGraphicFade.FadeColors(
            graphics,
            targetColors,
            transparentColors,
            _openBusinessFadeOutDuration);

        HideOpenBusiness();
        UiGraphicFade.RestoreColors(graphics, targetColors);

        OpenBusinessAutomatically();
        _openBusinessSequenceRoutine = null;
    }

    private void OpenBusinessAutomatically()
    {
        if (GameManager.Instance == null || GameManager.Instance.IsBusinessSessionActive)
            return;

        if (!GameManager.Instance.TryOpenBusinessSession())
            return;

        AudioManager.Play(SfxId.OpenBusiness);
        HideOpenBusiness();
        HideBusinessOverview();

        MissionUiController missionUi = FindFirstObjectByType<MissionUiController>();
        missionUi?.NotifyOpenBusinessOpened();
        SyncActiveUi();
        SyncAllNestedBuildSpotCostUi();
        SyncAllHireSpotUi();
    }

    private void StopOpenBusinessSequence()
    {
        if (_openBusinessSequenceRoutine == null)
            return;

        StopCoroutine(_openBusinessSequenceRoutine);
        _openBusinessSequenceRoutine = null;
    }

    private void HideOpenBusiness()
    {
        if (_openBusinessRoot != null)
            _openBusinessRoot.SetActive(false);
    }

    private void CacheBusinessOverviewUiReferences()
    {
        if (_businessOverviewRoot == null)
            _businessOverviewRoot = FindSceneUiObject("Business Overview");

        if (_businessOverviewRoot == null)
            return;

        if (_businessAcknowledgeButton == null)
        {
            Transform acknowledge = _businessOverviewRoot.transform.Find("Acknowledge UI");
            if (acknowledge != null)
            {
                _businessAcknowledgeButton = acknowledge.GetComponent<Button>()
                    ?? acknowledge.gameObject.AddComponent<Button>();
            }
        }

        if (_businessAcknowledgeButton != null)
        {
            // Scene button often has no TargetGraphic — wire Background image so clicks work.
            if (_businessAcknowledgeButton.targetGraphic == null)
            {
                Transform background = _businessAcknowledgeButton.transform.Find("Background");
                Graphic graphic = background != null
                    ? background.GetComponent<Graphic>()
                    : _businessAcknowledgeButton.GetComponent<Graphic>();

                if (graphic == null)
                {
                    Image image = _businessAcknowledgeButton.gameObject.GetComponent<Image>()
                        ?? _businessAcknowledgeButton.gameObject.AddComponent<Image>();
                    image.color = new Color(1f, 1f, 1f, 0.01f);
                    image.raycastTarget = true;
                    graphic = image;
                }

                _businessAcknowledgeButton.targetGraphic = graphic;
            }

            _businessAcknowledgeButton.interactable = true;
            _businessAcknowledgeButton.onClick.RemoveListener(HandleBusinessAcknowledgeClicked);
            _businessAcknowledgeButton.onClick.AddListener(HandleBusinessAcknowledgeClicked);
        }
    }

    private void HideBusinessOverview()
    {
        if (_businessOverviewRoot != null)
            _businessOverviewRoot.SetActive(false);
    }

    private void ShowBusinessTimer()
    {
        if (_businessTimerText != null)
            _businessTimerText.gameObject.SetActive(true);
    }

    private void HideBusinessTimer()
    {
        if (_businessTimerText != null)
            _businessTimerText.gameObject.SetActive(false);
    }

    private void UpdateBusinessTimerUi()
    {
        if (_businessTimerText == null)
            return;

        GameManager gameManager = GameManager.Instance;

        if (gameManager == null || !gameManager.IsBusinessSessionActive)
        {
            if (_businessTimerText.gameObject.activeSelf)
                HideBusinessTimer();

            return;
        }

        if (!_businessTimerText.gameObject.activeSelf)
            ShowBusinessTimer();

        int totalSeconds = Mathf.CeilToInt(gameManager.BusinessSessionRemainingSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        _businessTimerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void CacheTableBuildInfoUiReferences()
    {
        if (_tableBuildInfoRoot == null)
            _tableBuildInfoRoot = FindSceneUiObject("Table Info");

        if (_tableBuildInfoCloseButton == null)
            _tableBuildInfoCloseButton = FindTableBuildInfoButton("Close UI");

        if (_tableBuildInfoCloseButton == null)
            _tableBuildInfoCloseButton = FindTableBuildInfoButton("close ui");
    }

    private void ShowTableBuildInfoUi()
    {
        if (_tableBuildInfoRoot != null)
            _tableBuildInfoRoot.SetActive(true);
    }

    private void HideTableBuildInfoUi()
    {
        if (_tableBuildInfoRoot != null)
            _tableBuildInfoRoot.SetActive(false);
    }

    private Button FindTableBuildInfoButton(string objectName)
    {
        if (_tableBuildInfoRoot == null)
            return null;

        Button[] buttons = _tableBuildInfoRoot.GetComponentsInChildren<Button>(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && string.Equals(buttons[i].gameObject.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                return buttons[i];
        }

        Transform closeTransform = _tableBuildInfoRoot.transform.Find(objectName);

        return closeTransform != null ? EnsureButtonOnObject(closeTransform.gameObject) : null;
    }

    private Button FindGoldUiButton(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        Button button = FindGoldUiButton(_screenCanvas != null ? _screenCanvas.transform : null, objectName);

        if (button != null)
            return button;

        CharacterPanelController panel = CharacterPanelController.Instance;

        return panel != null ? FindGoldUiButton(panel.transform, objectName) : null;
    }

    private static Button FindGoldUiButton(Transform root, string objectName)
    {
        if (root == null)
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (!string.Equals(candidate.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                continue;

            Button button = candidate.GetComponent<Button>();

            if (button == null)
                button = candidate.gameObject.AddComponent<Button>();

            DisableChildRaycastTargets(candidate);
            return button;
        }

        return null;
    }

    private static void DisableChildRaycastTargets(Transform root)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null && graphics[i].transform != root)
                graphics[i].raycastTarget = false;
        }
    }

    private void HandleCustomerStateChanged(Customer customer, CustomerState state)
    {
        if (GameManager.Instance != null && !GameManager.Instance.IsBusiness)
            return;

        if (state == CustomerState.Paying)
        {
            if (customer != null && customer.IsVip)
                ShowSeatPaymentUi(customer);
            else
                PlayNormalCustomerAutoCollectFx(customer);
        }
        else if (customer.Seat != null)
            TryHideSeatPaymentUi(customer.Seat.PaymentUiAnchor);
        else if (customer.PendingPaymentAnchor != null)
            TryHideSeatPaymentUi(customer.PendingPaymentAnchor);

        RefreshTableStatusForCustomer(customer);
    }

    private void RefreshTableStatusForCustomer(Customer customer)
    {
        if (customer?.Seat != null)
        {
            foreach (KeyValuePair<DiningTable, TableStatusUiEntry> entry in _activeTableStatusUis)
            {
                if (entry.Key != null && entry.Key.ContainsSeat(customer.Seat))
                    RefreshTableStatusUi(entry.Key);
            }

            return;
        }

        if (customer?.PendingPaymentAnchor == null)
            return;

        DiningTable table = customer.PendingPaymentAnchor.GetComponentInParent<DiningTable>();

        if (table != null)
            RefreshTableStatusUi(table);
    }

    private void HandleTableStatusChanged(DiningTable table)
    {
        RefreshTableStatusUi(table);

        if (RestaurantSceneMode.IsMainScene && table != null && (table.IsBroken || table.IsRepairing))
            SyncRepairTableUis();
    }

    private void OnWorkerUiRefreshRequested(Worker worker, int currentEnergy, int maxEnergy) =>
        RefreshWorkerEnergyUi(worker);

    private void OnWorkerUiRefreshRequested(Worker worker, WorkerState state) =>
        RefreshWorkerEnergyUi(worker);

    private void SyncActiveUi()
    {
        if (GameManager.Instance == null)
            return;

        // Build-spot cost UI is now per-spot (cloned instances), so we don't use
        // the old single-active-spot UI tracking here.

        SyncAllHireSpotUi();

        if (GameManager.Instance.IsBusiness)
        {
            if (RestaurantSceneMode.UsesWorkerEnergyUi)
                RegisterActiveWorkerEnergyUis();
            else
                ClearWorkerEnergyUis();

            RegisterTableStatusUis();
        }
        else
        {
            ClearSeatPaymentUis();
            ClearTableStatusUis();
            ClearWorkerEnergyUis();
        }

        SyncMainButtonsVisibility();
    }

    private void SyncAllHireSpotUi()
    {
        EnsureScreenUiCaches();

        HireSpot[] spots = FindObjectsByType<HireSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        HireSpot activeSpotOnFloor = null;
        bool chefButtonInUse = false;
        bool waiterButtonInUse = false;

        for (int i = 0; i < spots.Length; i++)
        {
            HireSpot spot = spots[i];

            if (spot == null)
                continue;

            Button hireButton = GetHireButtonForSpot(spot);
            EnsureHireSpotButtonWired(spot, hireButton);
            RectTransform hireUiRoot = hireButton != null ? hireButton.transform as RectTransform : null;

            if (!ShouldDisplayHireSpotUi(spot))
            {
                if (hireUiRoot != null)
                    hireUiRoot.gameObject.SetActive(false);

                continue;
            }

            if (hireUiRoot == null)
                continue;

            hireUiRoot.gameObject.SetActive(true);
            TryPositionHireSpotUi(hireUiRoot, spot);
            hireUiRoot.localScale = Vector3.one * GetHirePulseScale(spot.WorkerType);

            if (hireButton == _chefHireButton)
                chefButtonInUse = true;
            else if (hireButton == _waiterHireButton)
                waiterButtonInUse = true;

            if (spot.State == HireSpotState.Active && IsSpotOnCurrentFloor(spot))
                activeSpotOnFloor = spot;
        }

        if (!chefButtonInUse && _chefHireButton != null)
            _chefHireButton.gameObject.SetActive(false);

        if (!waiterButtonInUse && _waiterHireButton != null)
            _waiterHireButton.gameObject.SetActive(false);

        _activeHireSpot = activeSpotOnFloor;

        if (activeSpotOnFloor != null)
            RefreshHireAffordability(activeSpotOnFloor);
    }

    private bool ShouldDisplayHireSpotUi(HireSpot spot)
    {
        if (spot == null || !IsSpotOnCurrentFloor(spot))
            return false;

        // Hide immediately when the hire starts (spot enters HireSpotState.Hiring).
        // This prevents the button from lingering while the worker is walking.
        if (spot.State != HireSpotState.Active)
            return false;

        if (!ShouldKeepHireSpotsAvailableForUi())
            return false;

        return IsHireMissionActiveForUi();
    }

    private static bool ShouldKeepHireSpotsAvailableForUi()
    {
        if (GameManager.Instance == null)
            return false;

        // Once business is open (or was started), hire UI stays available.
        if (GameManager.Instance.IsBusinessSessionActive
            || PlayerProfileStorage.HasMainSceneBusinessStartedForCurrentPlayer())
        {
            return true;
        }

        return GameManager.Instance.IsBuilding || GameManager.Instance.IsHiring;
    }

    private Button GetHireButtonForSpot(HireSpot spot)
    {
        if (spot == null)
            return null;

        // Each hire spot owns its button (supports separate ground / second-floor waiter UIs).
        Button ownButton = spot.GetComponent<Button>();

        if (ownButton != null)
            return ownButton;

        return spot.WorkerType == WorkerType.Chef ? _chefHireButton : _waiterHireButton;
    }

    private void EnsureHireSpotButtonWired(HireSpot spot, Button hireButton)
    {
        if (spot == null || hireButton == null || !_wiredHireSpotButtons.Add(hireButton))
            return;

        // Each visible hire button must notify its own spot (not a shared _activeHireSpot).
        HireSpot capturedSpot = spot;
        hireButton.onClick.AddListener(() =>
        {
            if (capturedSpot != null && capturedSpot.State == HireSpotState.Active)
                capturedSpot.NotifyClicked();
        });
    }

    private Vector3 ResolveHireHomeWorldPosition(HireSpot spot, RectTransform hireUiRoot)
    {
        if (spot == null)
            return Vector3.zero;

        if (_hireSpotHomeWorldPositions.TryGetValue(spot, out Vector3 cached))
            return cached;

        // Prefer the hire point's authored world position (stable across reparents).
        Vector3 home = spot.HireUiAnchor != null
            ? spot.HireUiAnchor.position
            : (hireUiRoot != null ? hireUiRoot.position : spot.transform.position);

        _hireSpotHomeWorldPositions[spot] = home;
        return home;
    }

    private static Transform ResolveHireUiParent(HireSpot spot, RectTransform hireUiRoot)
    {
        // Always prefer the authored HireUiAnchor (ChefPoint / WaiterPoint / second-floor point),
        // even if it starts inactive — activate so the button can render at that offset.
        Transform preferred = spot != null ? spot.HireUiAnchor : null;
        if (preferred != null)
        {
            if (!preferred.gameObject.activeSelf)
                preferred.gameObject.SetActive(true);

            return preferred;
        }

        if (hireUiRoot != null
            && hireUiRoot.parent != null
            && hireUiRoot.parent.gameObject.activeInHierarchy)
        {
            return hireUiRoot.parent;
        }

        return spot != null ? spot.transform : null;
    }

    private void TryPositionHireSpotUi(RectTransform hireUiRoot, HireSpot spot)
    {
        if (hireUiRoot == null || spot == null)
            return;

        Vector3 worldAnchor = ResolveHireHomeWorldPosition(spot, hireUiRoot);

        // Screen-space hire UI: clamp to screen edges when dragged away.
        if (!IsWorldSpaceHireUi(hireUiRoot))
        {
            if (TryGetEdgeClampedScreenUiLocalPoint(worldAnchor, hireUiRoot, out Vector2 localPoint))
                hireUiRoot.anchoredPosition = localPoint;
            return;
        }

        EnsureScreenUiCaches();
        if (_worldCamera == null)
            _worldCamera = Camera.main;

        // Second floor: stay on the hire point (no edge-clamp).
        if (spot.Floor == RestaurantFloor.Second)
        {
            Transform secondFloorAnchor = ResolveHireUiParent(spot, hireUiRoot);
            if (secondFloorAnchor != null && hireUiRoot.parent != secondFloorAnchor)
                hireUiRoot.SetParent(secondFloorAnchor, false);

            hireUiRoot.anchorMin = new Vector2(0.5f, 0.5f);
            hireUiRoot.anchorMax = new Vector2(0.5f, 0.5f);
            hireUiRoot.pivot = new Vector2(0.5f, 0.5f);
            hireUiRoot.anchoredPosition3D = Vector3.zero;

            if (_worldCamera != null)
                hireUiRoot.rotation = _worldCamera.transform.rotation;

            return;
        }

        // First floor: edge-clamp from each HireUiAnchor home (ChefPoint / WaiterPoint).
        Canvas worldCanvas = _worldCanvas != null ? _worldCanvas : FindWorldCanvas();
        if (worldCanvas == null || _worldCamera == null)
        {
            Transform fallbackAnchor = ResolveHireUiParent(spot, hireUiRoot);
            EnsureHireUiAnchoredToWorldPoint(hireUiRoot, fallbackAnchor);
            return;
        }

        if (!TryGetEdgeClampedScreenPoint(worldAnchor, hireUiRoot, out Vector3 screenPoint))
            return;

        Camera canvasCamera = worldCanvas.worldCamera != null ? worldCanvas.worldCamera : _worldCamera;
        RectTransform canvasRect = worldCanvas.transform as RectTransform;
        if (canvasRect == null)
            return;

        if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(
                canvasRect,
                screenPoint,
                canvasCamera,
                out Vector3 worldPoint))
        {
            return;
        }

        if (hireUiRoot.parent != canvasRect)
            hireUiRoot.SetParent(canvasRect, true);

        hireUiRoot.position = worldPoint + Vector3.up * Mathf.Max(0f, _hireUiWorldHeightOffset);
        hireUiRoot.rotation = canvasCamera.transform.rotation;
    }

    private bool TryGetEdgeClampedScreenPoint(
        Vector3 worldAnchorPosition,
        RectTransform root,
        out Vector3 screenPoint)
    {
        screenPoint = default;

        EnsureScreenUiCaches();

        if (_worldCamera == null)
            return false;

        screenPoint = _worldCamera.WorldToScreenPoint(worldAnchorPosition);
        if (screenPoint.z <= 0f)
        {
            Vector3 viewport = _worldCamera.WorldToViewportPoint(worldAnchorPosition);
            screenPoint.x = viewport.x < 0.5f ? 0f : Screen.width;
            screenPoint.y = Mathf.Clamp(Screen.height * 0.5f, 0f, Screen.height);
            screenPoint.z = 1f;
        }

        float halfW = 48f;
        float halfH = 48f;

        if (root != null && !IsWorldSpaceHireUi(root))
        {
            halfW = Mathf.Max(40f, root.rect.width * 0.5f);
            halfH = Mathf.Max(40f, root.rect.height * 0.5f);
        }

        float pad = 24f;
        screenPoint.x = Mathf.Clamp(screenPoint.x, pad + halfW, Screen.width - pad - halfW);
        screenPoint.y = Mathf.Clamp(screenPoint.y, pad + halfH, Screen.height - pad - halfH);
        return true;
    }

    private bool TryGetEdgeClampedScreenUiLocalPoint(
        Vector3 worldAnchorPosition,
        RectTransform root,
        out Vector2 localPoint)
    {
        localPoint = default;

        EnsureScreenUiCaches();

        if (_canvasRect == null || _screenCanvas == null)
            return false;

        if (!TryGetEdgeClampedScreenPoint(worldAnchorPosition, root, out Vector3 screenPoint))
            return false;

        Camera canvasCamera = _screenCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _screenCanvas.worldCamera;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect,
            screenPoint,
            canvasCamera,
            out localPoint);
    }

    private void EnsureHireUiAnchoredToWorldPoint(RectTransform hireUiRoot, Transform anchor)
    {
        if (hireUiRoot == null || anchor == null)
            return;

        if (hireUiRoot.parent != anchor)
            hireUiRoot.SetParent(anchor, false);

        hireUiRoot.anchorMin = new Vector2(0.5f, 0.5f);
        hireUiRoot.anchorMax = new Vector2(0.5f, 0.5f);
        hireUiRoot.pivot = new Vector2(0.5f, 0.5f);
        hireUiRoot.anchoredPosition3D = Vector3.zero;

        // Face the camera so the tilted isometric view doesn't skew world-space hire buttons.
        EnsureScreenUiCaches();
        if (_worldCamera == null)
            _worldCamera = Camera.main;

        if (_worldCamera != null)
            hireUiRoot.rotation = _worldCamera.transform.rotation;
        else
            hireUiRoot.localRotation = Quaternion.identity;

        hireUiRoot.position = anchor.position + Vector3.up * Mathf.Max(0f, _hireUiWorldHeightOffset);
    }

    private bool IsWorldSpaceHireUi(RectTransform hireUiRoot)
    {
        return IsUnderWorldSpaceCanvas(hireUiRoot);
    }

    private bool IsUnderWorldSpaceCanvas(Transform transform)
    {
        if (transform == null)
            return false;

        EnsureScreenUiCaches();

        if (_worldCanvas == null)
            _worldCanvas = FindWorldCanvas();

        if (_worldCanvas == null)
            return false;

        return transform == _worldCanvas.transform
            || transform.IsChildOf(_worldCanvas.transform);
    }

    private static bool IsHireMissionActiveForUi()
    {
        // After business opens, any remaining active hire spots stay visible.
        if (GameManager.Instance != null
            && (GameManager.Instance.IsBusinessSessionActive
                || PlayerProfileStorage.HasMainSceneBusinessStartedForCurrentPlayer()))
        {
            return true;
        }

        int missionPart = GetCurrentMissionPartIndex();

        if (missionPart == MissionCatalog.HireMissionPartIndex)
            return true;

        if (missionPart > MissionCatalog.HireMissionPartIndex)
            return false;

        BuildSequenceController buildController = FindFirstObjectByType<BuildSequenceController>();
        return buildController != null && buildController.AreStarterBuildSpotsComplete();
    }

    private static int GetCurrentMissionPartIndex()
    {
        int savedPart = PlayerProfileStorage.GetMainSceneMissionPartIndexForCurrentPlayer();
        MissionUiController missionUi = FindFirstObjectByType<MissionUiController>();

        if (missionUi == null)
            return savedPart;

        missionUi.EnsureInitialized();
        return Mathf.Max(missionUi.CurrentPartIndex, savedPart);
    }

    private static bool IsSpotOnCurrentFloor(HireSpot spot)
    {
        if (spot == null)
            return false;

        int currentFloor = CharacterPanelController.Instance != null
            ? CharacterPanelController.Instance.CurrentFloor
            : (int)RestaurantFloor.Ground;

        return (int)spot.Floor == currentFloor;
    }

    private void HandleGoldChanged(int currentGold)
    {
        if (_lastGoldAmount >= 0)
        {
            _pendingGoldDelta += currentGold - _lastGoldAmount;
            _hasPendingGoldDelta = true;
        }

        _lastGoldAmount = currentGold;
        SyncGoldUi(currentGold);

        if (_activeHireSpot != null)
            RefreshHireAffordability(_activeHireSpot);

        RefreshUpgradeCostAffordability();
        RefreshRepairTableUiAffordability();
    }

    private void FlushPendingGoldDelta()
    {
        if (!_hasPendingGoldDelta)
            return;

        _hasPendingGoldDelta = false;
        int delta = _pendingGoldDelta;
        _pendingGoldDelta = 0;

        if (delta > 0)
            ShowGoldChangeFeedback(_goldPlusUi, delta, true);
        else if (delta < 0)
            ShowGoldChangeFeedback(_goldMinusUi, -delta, false);
    }

    private void BindPersistentCharacterPanelUi()
    {
        CharacterPanelController panel = CharacterPanelController.Instance;

        if (panel == null)
            return;

        if (panel.GoldAmountText != null)
            _goldAmountText = panel.GoldAmountText;

        if (panel.GoldUiRoot != null)
            _coinTrailTargetUi = panel.GoldUiRoot;

        if (_addGoldButton == null)
            _addGoldButton = FindGoldUiButton("Plus Bg");

        CacheGoldChangeUi();
    }

    private void CacheGoldChangeUi()
    {
        CacheGoldChangeUi(_screenCanvas != null ? _screenCanvas.transform : null);

        CharacterPanelController panel = CharacterPanelController.Instance;

        if (panel != null)
            CacheGoldChangeUi(panel.transform);
    }

    private void CacheGoldChangeUi(Transform root)
    {
        if (root == null)
            return;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (candidate == null)
                continue;

            if (_goldPlusUi == null && string.Equals(candidate.name, "Gold Plus", System.StringComparison.OrdinalIgnoreCase))
                _goldPlusUi = candidate as RectTransform;

            if (_goldMinusUi == null && string.Equals(candidate.name, "Gold Minus", System.StringComparison.OrdinalIgnoreCase))
                _goldMinusUi = candidate as RectTransform;
        }

        PrepareGoldChangeUi(_goldPlusUi, ref _goldPlusRestPosition);
        PrepareGoldChangeUi(_goldMinusUi, ref _goldMinusRestPosition);
    }

    private static void PrepareGoldChangeUi(RectTransform ui, ref Vector2 restPosition)
    {
        if (ui == null)
            return;

        restPosition = ui.anchoredPosition;

        TextMeshProUGUI label = ui.GetComponent<TextMeshProUGUI>();

        if (label != null)
            label.raycastTarget = false;

        ui.gameObject.SetActive(false);
    }

    private void ShowGoldChangeFeedback(RectTransform ui, int amount, bool isGain)
    {
        if (ui == null || amount <= 0)
            return;

        TextMeshProUGUI label = ui.GetComponent<TextMeshProUGUI>();

        if (label == null)
            label = ui.GetComponentInChildren<TextMeshProUGUI>(true);

        if (label != null)
            label.text = isGain ? $"+{amount}" : $"-{amount}";

        if (isGain)
        {
            if (_goldPlusAnimation != null)
                StopCoroutine(_goldPlusAnimation);

            _goldPlusAnimation = StartCoroutine(PlayGoldChangeFloatCoroutine(ui, _goldPlusRestPosition, true));
            return;
        }

        if (_goldMinusAnimation != null)
            StopCoroutine(_goldMinusAnimation);

        _goldMinusAnimation = StartCoroutine(PlayGoldChangeFloatCoroutine(ui, _goldMinusRestPosition, false));
    }

    private IEnumerator PlayGoldChangeFloatCoroutine(RectTransform ui, Vector2 restPosition, bool isGain)
    {
        yield return PlayUiFloatCoroutine(ui, restPosition);

        if (isGain)
            _goldPlusAnimation = null;
        else
            _goldMinusAnimation = null;
    }

    private IEnumerator PlayUiFloatCoroutine(RectTransform ui, Vector2 restPosition)
    {
        if (ui == null)
            yield break;

        ui.gameObject.SetActive(true);
        ui.anchoredPosition = restPosition;

        float elapsed = 0f;
        Vector2 endPosition = restPosition + Vector2.up * _goldChangeFloatDistance;

        while (elapsed < _goldChangeFloatDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _goldChangeFloatDuration);
            ui.anchoredPosition = Vector2.Lerp(restPosition, endPosition, t);
            yield return null;
        }

        ui.gameObject.SetActive(false);
        ui.anchoredPosition = restPosition;
    }

    private void HandleAddGoldClicked()
    {
        if (GoldManager.Instance == null)
            return;

        GoldManager.Instance.AddGold(_addGoldButtonAmount);
    }

    private void SyncGoldUi()
    {
        int currentGold = GoldManager.Instance != null ? GoldManager.Instance.CurrentGold : 0;

        if (_lastGoldAmount < 0)
            _lastGoldAmount = currentGold;

        SyncGoldUi(currentGold);
    }

    private void SyncGoldUi(int currentGold)
    {
        if (_goldAmountText != null)
            _goldAmountText.text = currentGold.ToString();
    }

    private void RefreshHireAffordability(HireSpot spot)
    {
        if (spot == null)
            return;

        Button hireButton = GetHireButtonForSpot(spot);

        if (hireButton != null)
            hireButton.interactable = true;
    }

    private void ShowSeatPaymentUi(Customer customer)
    {
        if (_seatPaymentUiRoot == null || customer == null || customer.Seat == null)
            return;

        Transform paymentAnchor = customer.Seat.PaymentUiAnchor;

        if (paymentAnchor == null || _activeSeatPaymentUis.ContainsKey(paymentAnchor))
            return;

        RectTransform uiRoot = CreateSeatPaymentUiRoot();
        Button paymentButton = uiRoot.GetComponentInChildren<Button>(true);

        if (paymentButton != null)
            paymentButton.onClick.AddListener(() => HandleSeatPaymentClicked(paymentAnchor));

        RectTransform extraGlowRoot = FindChildRectTransform(uiRoot, CoinCollectionExtraGlowName);

        _activeSeatPaymentUis[paymentAnchor] = new SeatPaymentUiEntry
        {
            UiRoot = uiRoot,
            PaymentAnchor = paymentAnchor,
            ExtraGlowRoot = extraGlowRoot
        };

        uiRoot.gameObject.SetActive(IsPaymentAnchorOnCurrentFloor(paymentAnchor));
        uiRoot.localScale = Vector3.one;
        UpdateSeatPaymentExtraGlow(_activeSeatPaymentUis[paymentAnchor]);
        if (uiRoot.gameObject.activeSelf)
            UpdateScreenUiPosition(uiRoot, paymentAnchor.position);
        ApplyWorldAnchoredUiSiblingOrder();
    }

    private void PlayNormalCustomerAutoCollectFx(Customer customer)
    {
        if (customer == null)
            return;

        Transform paymentAnchor = customer.Seat != null
            ? customer.Seat.PaymentUiAnchor
            : customer.PendingPaymentAnchor;
        Transform collectPoint = ResolveTableCollectPoint(paymentAnchor);

        AudioManager.Play(SfxId.GoldCollect);
        PlayCoinTrail(collectPoint, useVipCount: false);
    }

    public void TryHideSeatPaymentUi(Transform paymentAnchor)
    {
        if (paymentAnchor == null)
            return;

        if (CustomerManager.Instance != null && CustomerManager.Instance.HasAwaitingPaymentsAt(paymentAnchor))
            return;

        HideSeatPaymentUi(paymentAnchor);
    }

    private static void SafeDestroyUiClone(RectTransform uiRoot, RectTransform templateRoot)
    {
        if (uiRoot == null || uiRoot == templateRoot)
            return;

        Destroy(uiRoot.gameObject);
    }

    private static void SafeSetUiActive(RectTransform uiRoot, bool active)
    {
        if (uiRoot == null)
            return;

        uiRoot.gameObject.SetActive(active);
    }

    private void HideSeatPaymentUi(Transform paymentAnchor)
    {
        if (paymentAnchor == null || !_activeSeatPaymentUis.TryGetValue(paymentAnchor, out SeatPaymentUiEntry entry))
            return;

        if (entry.UiRoot == null)
        {
            _activeSeatPaymentUis.Remove(paymentAnchor);
            return;
        }

        Button paymentButton = entry.UiRoot.GetComponentInChildren<Button>(true);

        if (paymentButton != null)
            paymentButton.onClick.RemoveAllListeners();

        if (entry.ExtraGlowRoot != null)
        {
            entry.ExtraGlowRoot.gameObject.SetActive(false);
            entry.ExtraGlowRoot.localScale = Vector3.one;
        }

        if (entry.UiRoot == _seatPaymentUiRoot)
        {
            entry.UiRoot.gameObject.SetActive(false);
            entry.UiRoot.localScale = Vector3.one;
        }
        else
            SafeDestroyUiClone(entry.UiRoot, _seatPaymentUiRoot);

        _activeSeatPaymentUis.Remove(paymentAnchor);
        ApplyWorldAnchoredUiSiblingOrder();
    }

    private RectTransform CreateSeatPaymentUiRoot()
    {
        if (_activeSeatPaymentUis.Count == 0)
            return _seatPaymentUiRoot;

        RectTransform instance = Instantiate(_seatPaymentUiRoot, _seatPaymentUiRoot.parent);
        instance.gameObject.SetActive(false);
        return instance;
    }

    private void ClearSeatPaymentUis()
    {
        foreach (KeyValuePair<Transform, SeatPaymentUiEntry> entry in _activeSeatPaymentUis)
            SafeDestroyUiClone(entry.Value.UiRoot, _seatPaymentUiRoot);

        _activeSeatPaymentUis.Clear();

        if (_seatPaymentUiRoot != null)
        {
            Transform extraGlow = FindChildTransform(_seatPaymentUiRoot, CoinCollectionExtraGlowName);

            if (extraGlow != null)
            {
                extraGlow.gameObject.SetActive(false);
                extraGlow.localScale = Vector3.one;
            }

            _seatPaymentUiRoot.gameObject.SetActive(false);
            _seatPaymentUiRoot.localScale = Vector3.one;
        }
    }

    private void RegisterTableStatusUis()
    {
        if (_tableStatusUiRoot == null)
            return;

        DiningTable[] tables = FindObjectsByType<DiningTable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < tables.Length; i++)
        {
            DiningTable table = tables[i];

            if (table == null || !IsTableAvailableForStatusUi(table) || _activeTableStatusUis.ContainsKey(table))
                continue;

            RegisterTableStatusUi(table);
        }

        // Drop any status UIs still pointing at unbuilt / inactive tables.
        List<DiningTable> staleTables = null;

        foreach (KeyValuePair<DiningTable, TableStatusUiEntry> entry in _activeTableStatusUis)
        {
            if (IsTableAvailableForStatusUi(entry.Key))
                continue;

            staleTables ??= new List<DiningTable>();
            staleTables.Add(entry.Key);
        }

        if (staleTables == null)
            return;

        for (int i = 0; i < staleTables.Count; i++)
            UnregisterTableStatusUi(staleTables[i]);
    }

    private void RegisterTableStatusUi(DiningTable table)
    {
        if (!IsTableAvailableForStatusUi(table) || _tableStatusUiRoot == null)
            return;

        RectTransform uiRoot = CreateTableStatusUiRoot();
        TextMeshProUGUI statusText = uiRoot.GetComponent<TextMeshProUGUI>();

        if (statusText != null)
            statusText.raycastTarget = false;

        _activeTableStatusUis[table] = new TableStatusUiEntry
        {
            UiRoot = uiRoot,
            StatusText = statusText,
            Table = table
        };

        uiRoot.gameObject.SetActive(true);
        RefreshTableStatusUi(table);
        UpdateTableStatusPosition(table);
        ApplyWorldAnchoredUiSiblingOrder();
    }

    private void UnregisterTableStatusUi(DiningTable table)
    {
        if (table == null || !_activeTableStatusUis.TryGetValue(table, out TableStatusUiEntry entry))
            return;

        SafeDestroyUiClone(entry.UiRoot, _tableStatusUiRoot);
        _activeTableStatusUis.Remove(table);
    }

    private static bool IsTableAvailableForStatusUi(DiningTable table)
    {
        // VIP tables don't use status labels.
        if (table == null || table.IsVipTable)
            return false;

        // Unbuilt furniture stays inactive under its BuildSpot, so skip those tables.
        return table.isActiveAndEnabled && table.gameObject.activeInHierarchy;
    }

    private RectTransform CreateTableStatusUiRoot()
    {
        if (_activeTableStatusUis.Count == 0)
            return _tableStatusUiRoot;

        RectTransform instance = Instantiate(_tableStatusUiRoot, _tableStatusUiRoot.parent);
        instance.gameObject.SetActive(false);
        return instance;
    }

    private void ClearTableStatusUis()
    {
        foreach (KeyValuePair<DiningTable, TableStatusUiEntry> entry in _activeTableStatusUis)
            SafeDestroyUiClone(entry.Value.UiRoot, _tableStatusUiRoot);

        _activeTableStatusUis.Clear();
        SafeSetUiActive(_tableStatusUiRoot, false);
    }

    private void RefreshTableStatusUi(DiningTable table)
    {
        if (table == null || !_activeTableStatusUis.TryGetValue(table, out TableStatusUiEntry entry) || entry.StatusText == null)
            return;

        if (!IsTableAvailableForStatusUi(table) || !IsTableOnCurrentFloor(table))
        {
            if (entry.UiRoot != null)
                entry.UiRoot.gameObject.SetActive(false);

            return;
        }

        if (entry.UiRoot != null && !entry.UiRoot.gameObject.activeSelf)
            entry.UiRoot.gameObject.SetActive(true);

        switch (table.GetCurrentStatus())
        {
            case TableStatusType.CollectPayment:
                entry.StatusText.text = "结账";
                entry.StatusText.color = _tableStatusPaymentColor;
                break;

            case TableStatusType.Broken:
                entry.StatusText.text = "损坏";
                entry.StatusText.color = _tableStatusFullColor;
                break;

            case TableStatusType.Full:
                entry.StatusText.text = "桌子满";
                entry.StatusText.color = _tableStatusFullColor;
                break;

            default:
                entry.StatusText.text = "有空位";
                entry.StatusText.color = _tableStatusEmptySeatColor;
                break;
        }
    }

    private void SyncAllTableStatusUiForCurrentFloor()
    {
        foreach (KeyValuePair<DiningTable, TableStatusUiEntry> entry in _activeTableStatusUis)
            RefreshTableStatusUi(entry.Key);

        UpdateTableStatusPositions();
        ApplyWorldAnchoredUiSiblingOrder();
    }

    private static bool IsTableOnCurrentFloor(DiningTable table)
    {
        if (table == null)
            return false;

        int currentFloor = CharacterPanelController.Instance != null
            ? CharacterPanelController.Instance.CurrentFloor
            : (int)RestaurantFloor.Ground;

        return (int)table.Floor == currentFloor;
    }

    private void UpdateTableStatusPosition(DiningTable table)
    {
        if (table == null || !_activeTableStatusUis.TryGetValue(table, out TableStatusUiEntry entry))
            return;

        if (entry.UiRoot == null || !entry.UiRoot.gameObject.activeInHierarchy)
            return;

        UpdateScreenUiPosition(entry.UiRoot, table.StatusPoint.position);
    }

    private void UpdateTableStatusPositions()
    {
        foreach (KeyValuePair<DiningTable, TableStatusUiEntry> entry in _activeTableStatusUis)
        {
            if (entry.Value.Table == null || entry.Value.UiRoot == null || !entry.Value.UiRoot.gameObject.activeInHierarchy)
                continue;

            UpdateTableStatusPosition(entry.Value.Table);
        }
    }

    private void ApplyWorldAnchoredUiSiblingOrder()
    {
        int siblingIndex = 0;

        foreach (KeyValuePair<DiningTable, TableStatusUiEntry> entry in _activeTableStatusUis)
        {
            if (entry.Value.UiRoot == null || !entry.Value.UiRoot.gameObject.activeInHierarchy)
                continue;

            entry.Value.UiRoot.SetSiblingIndex(siblingIndex);
            siblingIndex++;
        }

        foreach (KeyValuePair<Transform, SeatPaymentUiEntry> entry in _activeSeatPaymentUis)
        {
            if (entry.Value.UiRoot == null || !entry.Value.UiRoot.gameObject.activeInHierarchy)
                continue;

            entry.Value.UiRoot.SetSiblingIndex(siblingIndex);
            siblingIndex++;
        }
    }

    private void RegisterActiveWorkerEnergyUis()
    {
        Worker[] workers = FindObjectsOfType<Worker>(true);

        for (int i = 0; i < workers.Length; i++)
        {
            if (workers[i] != null && workers[i].isActiveAndEnabled)
                RegisterWorkerEnergyUi(workers[i]);
        }
    }

    private void ClearWorkerEnergyUis()
    {
        foreach (KeyValuePair<Worker, WorkerEnergyUiEntry> entry in _activeWorkerEnergyUis)
        {
            if (entry.Value.FillImage != null)
                entry.Value.FillImage.color = entry.Value.NormalFillColor;

            SafeSetUiActive(entry.Value.UiRoot, false);
        }

        _activeWorkerEnergyUis.Clear();
        _initializedWorkerEnergyBindings.Clear();
    }

    private void RefreshWorkerEnergyUi(Worker worker)
    {
        if (!RestaurantSceneMode.UsesWorkerEnergyUi || worker == null)
            return;

        if (!_activeWorkerEnergyUis.ContainsKey(worker) && worker.isActiveAndEnabled)
            RegisterWorkerEnergyUi(worker);

        if (!_activeWorkerEnergyUis.TryGetValue(worker, out WorkerEnergyUiEntry entry))
            return;

        if (!IsWorkerEnergyEntryValid(entry))
        {
            UnregisterWorkerEnergyUi(worker);

            if (worker.isActiveAndEnabled)
                RegisterWorkerEnergyUi(worker);

            return;
        }

        if (entry.FillImage != null && entry.Worker.Energy != null)
        {
            entry.FillImage.fillAmount = Mathf.Clamp01(entry.Worker.Energy.Normalized);
            ApplyWorkerEnergyFillColor(entry);
        }
    }

    private WorkerEnergyUiEntry CreateWorkerEnergyUiEntry(Worker worker, WorkerEnergyUiBinding binding)
    {
        CacheWorkerEnergyNormalFillColor(binding);

        return new WorkerEnergyUiEntry
        {
            Worker = worker,
            UiRoot = binding.Root,
            FillImage = binding.FillImage,
            NormalFillColor = binding.HasNormalFillColor ? binding.NormalFillColor : Color.white
        };
    }

    private void InitializeWorkerEnergyUiBindings()
    {
        if (!RestaurantSceneMode.UsesWorkerEnergyUi)
            return;

        if (_workerEnergyUiBindings != null)
        {
            for (int i = 0; i < _workerEnergyUiBindings.Length; i++)
            {
                WorkerEnergyUiBinding binding = _workerEnergyUiBindings[i];

                if (binding == null)
                    continue;

                EnsureWorkerEnergyUiBinding(binding);
                SetWorkerEnergyUiVisible(binding.Root, false);
            }
        }

        // Bindings only cover ground workers; hide every worker EnergyUiRoot (e.g. Waiter3 / FemaleWaiter)
        // so authored-active bars don't show before that worker is hired.
        HideAllWorkerEnergyUiRoots();
    }

    private static void HideAllWorkerEnergyUiRoots()
    {
        Worker[] workers = FindObjectsByType<Worker>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < workers.Length; i++)
        {
            Worker worker = workers[i];

            if (worker == null || worker.EnergyUiRoot == null)
                continue;

            SetWorkerEnergyUiVisible(worker.EnergyUiRoot, false);
        }
    }

    private void EnsureWorkerEnergyUiBinding(WorkerEnergyUiBinding binding)
    {
        if (binding == null)
            return;

        if (_initializedWorkerEnergyBindings.Contains(binding) && IsWorkerEnergyBindingValid(binding))
            return;

        _initializedWorkerEnergyBindings.Remove(binding);
        CacheWorkerEnergyFillImage(binding);

        if (IsWorkerEnergyBindingValid(binding))
            _initializedWorkerEnergyBindings.Add(binding);
    }

    private static void SetWorkerEnergyUiVisible(RectTransform root, bool visible)
    {
        if (root != null)
            root.gameObject.SetActive(visible);
    }

    private void CacheWorkerEnergyFillImage(WorkerEnergyUiBinding binding)
    {
        if (binding.FillImage == null)
        {
            if (binding.Root == null)
                return;

            Transform fillTransform = FindChildTransform(binding.Root, WorkerEnergyFillImageName);

            if (fillTransform != null)
                binding.FillImage = fillTransform.GetComponent<Image>();

            if (binding.FillImage == null)
            {
                Image[] images = binding.Root.GetComponentsInChildren<Image>(true);

                for (int i = 0; i < images.Length; i++)
                {
                    Image image = images[i];

                    if (image == null || image.transform == binding.Root)
                        continue;

                    if (image.type == Image.Type.Filled)
                    {
                        binding.FillImage = image;
                        break;
                    }
                }

                if (binding.FillImage == null)
                {
                    for (int i = 0; i < images.Length; i++)
                    {
                        Image image = images[i];

                        if (image != null && image.transform != binding.Root)
                        {
                            binding.FillImage = image;
                            break;
                        }
                    }
                }
            }
        }

        CacheWorkerEnergyNormalFillColor(binding);
    }

    private static void CacheWorkerEnergyNormalFillColor(WorkerEnergyUiBinding binding)
    {
        if (binding == null || binding.HasNormalFillColor || binding.FillImage == null)
            return;

        binding.NormalFillColor = binding.FillImage.color;
        binding.HasNormalFillColor = true;
    }

    private static bool IsWorkerEnergyEntryValid(WorkerEnergyUiEntry entry)
    {
        return entry.Worker != null
            && entry.UiRoot != null
            && entry.FillImage != null;
    }

    private static bool IsWorkerEnergyBindingValid(WorkerEnergyUiBinding binding)
    {
        return binding != null
            && binding.Root != null
            && binding.FillImage != null;
    }

    private WorkerEnergyUiBinding ResolveWorkerEnergyUiBinding(Worker worker)
    {
        if (worker == null)
            return null;

        if (_workerEnergyUiBindings != null)
        {
            for (int i = 0; i < _workerEnergyUiBindings.Length; i++)
            {
                WorkerEnergyUiBinding binding = _workerEnergyUiBindings[i];

                if (binding == null)
                    continue;

                if (binding.Worker == worker)
                    return binding;

                if (worker.EnergyUiRoot != null && binding.Root == worker.EnergyUiRoot)
                    return binding;
            }
        }

        if (worker.EnergyUiRoot != null)
            return GetOrCreateRuntimeWorkerEnergyBinding(worker);

        return null;
    }

    private WorkerEnergyUiBinding GetOrCreateRuntimeWorkerEnergyBinding(Worker worker)
    {
        if (_runtimeWorkerEnergyBindings.TryGetValue(worker, out WorkerEnergyUiBinding existing))
        {
            if (existing.Root != worker.EnergyUiRoot)
            {
                _initializedWorkerEnergyBindings.Remove(existing);
                _runtimeWorkerEnergyBindings.Remove(worker);
            }
            else if (IsWorkerEnergyBindingValid(existing))
            {
                return existing;
            }
            else
            {
                _initializedWorkerEnergyBindings.Remove(existing);
                _runtimeWorkerEnergyBindings.Remove(worker);
            }
        }

        if (worker.EnergyUiRoot == null)
            return null;

        RectTransform root = worker.EnergyUiRoot;

        if (root == null)
            return null;

        WorkerEnergyUiBinding binding = new WorkerEnergyUiBinding
        {
            Worker = worker,
            Root = root
        };

        EnsureWorkerEnergyUiBinding(binding);
        _runtimeWorkerEnergyBindings[worker] = binding;
        SetWorkerEnergyUiVisible(binding.Root, false);
        return binding;
    }

    private void HandleCanvasWillRenderCanvases()
    {
        UpdateWorkerEnergyUis();
        SyncAllHireSpotUi();
        UpdateVipReceptionUi();
    }

    private void UpdateWorkerEnergyUis()
    {
        if (!RestaurantSceneMode.UsesWorkerEnergyUi)
            return;

        if (_worldCanvas == null)
            _worldCanvas = FindWorldCanvas();

        if (_worldCamera == null)
            _worldCamera = Camera.main;

        if (_worldCamera == null)
            return;

        Quaternion cameraRotation = _worldCamera.transform.rotation;

        _workerEnergyUiScratch.Clear();
        _workerEnergyUiScratch.AddRange(_activeWorkerEnergyUis.Keys);

        for (int i = 0; i < _workerEnergyUiScratch.Count; i++)
        {
            Worker worker = _workerEnergyUiScratch[i];

            if (!_activeWorkerEnergyUis.TryGetValue(worker, out WorkerEnergyUiEntry entry))
                continue;

            if (entry.Worker == null || entry.UiRoot == null)
                continue;

            if (entry.FillImage != null && entry.Worker.Energy != null)
            {
                entry.FillImage.fillAmount = Mathf.Clamp01(entry.Worker.Energy.Normalized);
                ApplyWorkerEnergyFillColor(entry);
            }

            bool showEnergy = ShouldShowWorkerEnergyUi(worker);
            if (entry.UiRoot.gameObject.activeSelf != showEnergy)
                SetWorkerEnergyUiVisible(entry.UiRoot, showEnergy);

            if (!showEnergy)
                continue;

            entry.UiRoot.position = entry.Worker.EnergyUiWorldPosition;
            entry.UiRoot.rotation = cameraRotation;
        }
    }

    private void SyncWorkerEnergyUiForCurrentFloor()
    {
        foreach (KeyValuePair<Worker, WorkerEnergyUiEntry> entry in _activeWorkerEnergyUis)
        {
            if (entry.Value.UiRoot == null)
                continue;

            SetWorkerEnergyUiVisible(entry.Value.UiRoot, ShouldShowWorkerEnergyUi(entry.Key));
        }
    }

    private static bool ShouldShowWorkerEnergyUi(Worker worker)
    {
        if (worker == null || !worker.isActiveAndEnabled)
            return false;

        // Hide upstairs energy bars while the player is viewing floor 1.
        if (RestaurantFloorUtil.IsAtSecondFloorElevation(worker.transform))
        {
            int currentFloor = CharacterPanelController.Instance != null
                ? CharacterPanelController.Instance.CurrentFloor
                : (int)RestaurantFloor.Ground;

            if (currentFloor != (int)RestaurantFloor.Second)
                return false;
        }

        return true;
    }

    private void ApplyWorkerEnergyFillColor(WorkerEnergyUiEntry entry)
    {
        if (entry.FillImage == null || entry.Worker == null)
            return;

        entry.FillImage.color = entry.Worker.IsResting
            ? _workerEnergyRestingFillColor
            : entry.NormalFillColor;
    }

    private static float GetPulseScale(float minScale, float maxScale, float speed)
    {
        if (speed <= 0f)
            return 1f;

        float pulseT = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;
        return Mathf.Lerp(minScale, maxScale, pulseT);
    }

    private float GetHirePulseScale(WorkerType workerType)
    {
        return workerType == WorkerType.Chef
            ? GetPulseScale(_chefHirePulseMinScale, _chefHirePulseMaxScale, _chefHirePulseSpeed)
            : GetPulseScale(_waiterHirePulseMinScale, _waiterHirePulseMaxScale, _waiterHirePulseSpeed);
    }

    private void HandleSeatPaymentClicked(Transform paymentAnchor)
    {
        Transform collectPoint = ResolveTableCollectPoint(paymentAnchor);
        bool isVipCollection = CustomerManager.Instance != null
            && CustomerManager.Instance.HasVipAwaitingPaymentsAt(paymentAnchor);
        CustomerManager.Instance?.CompletePaymentsAtPaymentAnchor(paymentAnchor);
        AudioManager.Play(SfxId.GoldCollect);

        // VIP coins fly from the treasure box after the carrier arrives — not on tap.
        if (!isVipCollection)
            PlayCoinTrail(collectPoint, useVipCount: false);

        TryHideSeatPaymentUi(paymentAnchor);

        if (isVipCollection)
        {
            SetVipDialogue(VipDialogueState.SuccessLeave);
            CharacterPanelController.Instance?.GoToFirstFloor();
            VipTreasureDelivery.Instance?.PlayDelivery();
        }
    }

    public void ShowNotEnoughMoneyFeedback()
    {
        CacheNotEnoughMoneyUi();

        RectTransform uiRoot = AcquireNotEnoughMoneyNomUi();

        if (uiRoot == null || !TryGetGoldUiWorldAnchor(out Vector3 goldAnchor))
            return;

        uiRoot.gameObject.SetActive(true);
        uiRoot.localScale = Vector3.one;

        Graphic[] graphics = uiRoot.GetComponentsInChildren<Graphic>(true);
        UiGraphicFade.RestoreAlpha(graphics);
        Color[] targetColors = UiGraphicFade.CaptureColors(graphics);

        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i] != null)
                graphics[i].raycastTarget = false;
        }

        NotEnoughMoneyUiEntry entry = new NotEnoughMoneyUiEntry
        {
            UiRoot = uiRoot,
            GoldAnchorWorld = goldAnchor,
            Duration = _notEnoughMoneyDisplayDuration,
            FloatDistance = _notEnoughMoneyFloatDistance,
            BelowGoldOffset = _notEnoughMoneyBelowGoldOffset,
            Graphics = graphics,
            TargetColors = targetColors
        };

        UpdateNotEnoughMoneyUiPosition(entry);
        entry.Routine = StartCoroutine(PlayNotEnoughMoneyFloatFade(entry));
        _activeNotEnoughMoneyUis.Add(entry);
    }

    private void CacheNotEnoughMoneyUi()
    {
        if (_notEnoughMoneyUiRoot == null)
        {
            if (_screenCanvas == null)
                _screenCanvas = FindScreenCanvas();

            if (_screenCanvas != null)
                _notEnoughMoneyUiRoot = FindRectTransformByName(_screenCanvas.transform, NotEnoughMoneyUiRootName);
        }

        if (_notEnoughMoneyUiRoot == null)
            return;

        for (int i = 0; i < _notEnoughMoneyUiRoot.childCount; i++)
        {
            Transform child = _notEnoughMoneyUiRoot.GetChild(i);

            if (child == null || !(child is RectTransform nomUi))
                continue;

            if (!IsNotEnoughMoneyNomUiName(nomUi.name))
                continue;

            if (IsNotEnoughMoneyUiActive(nomUi))
                continue;

            if (_notEnoughMoneyUiPool.Contains(nomUi))
                continue;

            nomUi.gameObject.SetActive(false);
            nomUi.localScale = Vector3.one;
            UiGraphicFade.RestoreAlpha(nomUi.GetComponentsInChildren<Graphic>(true));
            _notEnoughMoneyUiPool.Add(nomUi);
        }
    }

    private bool IsNotEnoughMoneyUiActive(RectTransform uiRoot)
    {
        for (int i = 0; i < _activeNotEnoughMoneyUis.Count; i++)
        {
            if (_activeNotEnoughMoneyUis[i].UiRoot == uiRoot)
                return true;
        }

        return false;
    }

    private static bool IsNotEnoughMoneyNomUiName(string objectName)
    {
        return !string.IsNullOrEmpty(objectName)
            && objectName.StartsWith(NotEnoughMoneyNomUiNamePrefix, StringComparison.OrdinalIgnoreCase);
    }

    private RectTransform AcquireNotEnoughMoneyNomUi()
    {
        for (int i = _notEnoughMoneyUiPool.Count - 1; i >= 0; i--)
        {
            RectTransform pooled = _notEnoughMoneyUiPool[i];
            _notEnoughMoneyUiPool.RemoveAt(i);

            if (pooled != null)
                return pooled;
        }

        for (int i = 0; i < _activeNotEnoughMoneyUis.Count; i++)
        {
            NotEnoughMoneyUiEntry entry = _activeNotEnoughMoneyUis[i];

            if (entry.UiRoot == null)
                continue;

            RectTransform recycled = entry.UiRoot;
            ReleaseNotEnoughMoneyUi(recycled);
            _notEnoughMoneyUiPool.Remove(recycled);
            return recycled;
        }

        return null;
    }

    private void UpdateNotEnoughMoneyUiPositions()
    {
        for (int i = 0; i < _activeNotEnoughMoneyUis.Count; i++)
        {
            NotEnoughMoneyUiEntry entry = _activeNotEnoughMoneyUis[i];

            if (entry?.UiRoot == null || !entry.UiRoot.gameObject.activeInHierarchy)
                continue;

            UpdateNotEnoughMoneyUiPosition(entry);
        }
    }

    private void UpdateNotEnoughMoneyUiPosition(NotEnoughMoneyUiEntry entry)
    {
        if (entry?.UiRoot == null)
            return;

        float duration = Mathf.Max(0.01f, entry.Duration);
        float progress = Mathf.Clamp01(entry.Elapsed / duration);
        Vector2 screenOffset = Vector2.down * entry.BelowGoldOffset
            + Vector2.up * (entry.FloatDistance * progress);
        UpdateScreenUiPositionWithScreenOffset(entry.UiRoot, entry.GoldAnchorWorld, screenOffset);
    }

    private IEnumerator PlayNotEnoughMoneyFloatFade(NotEnoughMoneyUiEntry entry)
    {
        if (entry?.UiRoot == null)
            yield break;

        float duration = Mathf.Max(0.01f, entry.Duration);
        Color[] transparentColors = UiGraphicFade.BuildTransparentColors(entry.TargetColors);
        entry.Elapsed = 0f;

        while (entry.Elapsed < duration)
        {
            entry.Elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(entry.Elapsed / duration);
            UpdateNotEnoughMoneyUiPosition(entry);

            if (entry.Graphics != null && entry.TargetColors != null && transparentColors != null)
            {
                for (int i = 0; i < entry.Graphics.Length; i++)
                {
                    if (entry.Graphics[i] == null || i >= entry.TargetColors.Length || i >= transparentColors.Length)
                        continue;

                    entry.Graphics[i].color = Color.Lerp(entry.TargetColors[i], transparentColors[i], t);
                }
            }

            yield return null;
        }

        UpdateNotEnoughMoneyUiPosition(entry);

        if (entry.Graphics != null && entry.TargetColors != null)
            UiGraphicFade.RestoreColors(entry.Graphics, entry.TargetColors);

        ReleaseNotEnoughMoneyUi(entry.UiRoot);
    }

    private void ReleaseNotEnoughMoneyUi(RectTransform uiRoot)
    {
        if (uiRoot == null)
            return;

        for (int i = _activeNotEnoughMoneyUis.Count - 1; i >= 0; i--)
        {
            NotEnoughMoneyUiEntry entry = _activeNotEnoughMoneyUis[i];

            if (entry.UiRoot != uiRoot)
                continue;

            if (entry.Routine != null)
                StopCoroutine(entry.Routine);

            _activeNotEnoughMoneyUis.RemoveAt(i);
            break;
        }

        Graphic[] graphics = uiRoot.GetComponentsInChildren<Graphic>(true);
        UiGraphicFade.RestoreAlpha(graphics);
        uiRoot.gameObject.SetActive(false);
        uiRoot.localScale = Vector3.one;

        if (!_notEnoughMoneyUiPool.Contains(uiRoot))
            _notEnoughMoneyUiPool.Add(uiRoot);
    }

    private void ClearNotEnoughMoneyUis()
    {
        for (int i = _activeNotEnoughMoneyUis.Count - 1; i >= 0; i--)
        {
            NotEnoughMoneyUiEntry entry = _activeNotEnoughMoneyUis[i];

            if (entry.Routine != null)
                StopCoroutine(entry.Routine);

            if (entry.UiRoot != null)
            {
                Graphic[] graphics = entry.UiRoot.GetComponentsInChildren<Graphic>(true);
                UiGraphicFade.RestoreAlpha(graphics);
                entry.UiRoot.gameObject.SetActive(false);
                entry.UiRoot.localScale = Vector3.one;

                if (!_notEnoughMoneyUiPool.Contains(entry.UiRoot))
                    _notEnoughMoneyUiPool.Add(entry.UiRoot);
            }
        }

        _activeNotEnoughMoneyUis.Clear();
    }

    private RectTransform ResolveGoldUiRoot()
    {
        if (_coinTrailTargetUi != null)
            return _coinTrailTargetUi;

        CharacterPanelController panel = CharacterPanelController.Instance;
        return panel != null ? panel.GoldUiRoot : null;
    }

    private bool TryGetGoldUiWorldAnchor(out Vector3 worldAnchor)
    {
        worldAnchor = default;
        RectTransform goldUi = ResolveGoldUiRoot();

        if (goldUi == null)
            return false;

        Vector3[] corners = new Vector3[4];
        goldUi.GetWorldCorners(corners);
        worldAnchor = (corners[0] + corners[3]) * 0.5f;
        return true;
    }

    private void UpdateSeatPaymentUiPositions()
    {
        foreach (KeyValuePair<Transform, SeatPaymentUiEntry> entry in _activeSeatPaymentUis)
        {
            if (entry.Value.UiRoot == null || entry.Value.PaymentAnchor == null)
                continue;

            bool show = IsPaymentAnchorOnCurrentFloor(entry.Value.PaymentAnchor);
            if (entry.Value.UiRoot.gameObject.activeSelf != show)
                entry.Value.UiRoot.gameObject.SetActive(show);

            if (!show)
                continue;

            UpdateScreenUiPosition(entry.Value.UiRoot, entry.Value.PaymentAnchor.position);
            entry.Value.UiRoot.localScale = Vector3.one;
            UpdateSeatPaymentExtraGlow(entry.Value);
        }
    }

    private void SyncSeatPaymentUiForCurrentFloor()
    {
        foreach (KeyValuePair<Transform, SeatPaymentUiEntry> entry in _activeSeatPaymentUis)
        {
            if (entry.Value.UiRoot == null)
                continue;

            entry.Value.UiRoot.gameObject.SetActive(IsPaymentAnchorOnCurrentFloor(entry.Value.PaymentAnchor));
        }
    }

    private static bool IsPaymentAnchorOnCurrentFloor(Transform paymentAnchor)
    {
        if (paymentAnchor == null)
            return false;

        DiningTable table = paymentAnchor.GetComponentInParent<DiningTable>();

        if (table != null)
            return IsTableOnCurrentFloor(table);

        int currentFloor = CharacterPanelController.Instance != null
            ? CharacterPanelController.Instance.CurrentFloor
            : (int)RestaurantFloor.Ground;

        return (int)RestaurantFloorUtil.ResolveFloor(paymentAnchor) == currentFloor;
    }

    private void UpdateSeatPaymentExtraGlow(SeatPaymentUiEntry entry)
    {
        if (entry.ExtraGlowRoot == null)
            return;

        bool showVipGlow = CustomerManager.Instance != null
            && CustomerManager.Instance.HasVipAwaitingPaymentsAt(entry.PaymentAnchor);

        if (!showVipGlow)
        {
            if (entry.ExtraGlowRoot.gameObject.activeSelf)
                entry.ExtraGlowRoot.gameObject.SetActive(false);

            entry.ExtraGlowRoot.localScale = Vector3.one;
            return;
        }

        if (!entry.ExtraGlowRoot.gameObject.activeSelf)
            entry.ExtraGlowRoot.gameObject.SetActive(true);

        entry.ExtraGlowRoot.localScale = Vector3.one;
    }

    private void EnsureScreenUiCaches()
    {
        if (_screenCanvas == null)
            _screenCanvas = FindScreenCanvas();

        if (_screenCanvas != null && _canvasRect == null)
            _canvasRect = _screenCanvas.transform as RectTransform;

        Camera activeCamera = Camera.main;

        if (activeCamera != null)
            _worldCamera = activeCamera;
        else if (_worldCamera == null)
            _worldCamera = Camera.main;
    }

    private bool TryUpdateScreenUiPosition(RectTransform uiRoot, Vector3 worldAnchorPosition)
    {
        if (uiRoot == null)
            return false;

        EnsureScreenUiCaches();

        if (_worldCamera == null || _canvasRect == null)
            return false;

        Vector3 screenPoint = _worldCamera.WorldToScreenPoint(worldAnchorPosition);

        if (screenPoint.z <= 0f)
            return false;

        Camera canvasCamera = _screenCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _screenCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screenPoint,
                canvasCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        uiRoot.anchoredPosition = localPoint;
        return true;
    }

    private void UpdateScreenUiPosition(RectTransform uiRoot, Vector3 worldAnchorPosition)
    {
        TryUpdateScreenUiPosition(uiRoot, worldAnchorPosition);
    }

    private void UpdateScreenUiPositionWithScreenOffset(
        RectTransform uiRoot,
        Vector3 worldAnchorPosition,
        Vector2 screenOffset)
    {
        if (uiRoot == null)
            return;

        if (screenOffset == Vector2.zero)
        {
            if (!TryGetCanvasLocalPoint(worldAnchorPosition, out Vector2 localPoint))
                return;

            uiRoot.anchoredPosition = localPoint;
            return;
        }

        if (_worldCamera == null || _canvasRect == null)
            return;

        Vector3 screenPoint = _worldCamera.WorldToScreenPoint(worldAnchorPosition);

        if (screenPoint.z < 0f)
            return;

        screenPoint.x += screenOffset.x;
        screenPoint.y += screenOffset.y;

        Camera canvasCamera = _screenCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _screenCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect,
                screenPoint,
                canvasCamera,
                out Vector2 offsetLocalPoint))
        {
            return;
        }

        uiRoot.anchoredPosition = offsetLocalPoint;
    }

    private bool TryGetCanvasLocalPoint(Vector3 worldPosition, out Vector2 localPoint)
    {
        localPoint = default;

        if (_canvasRect == null || _worldCamera == null)
            return false;

        Vector3 screenPoint = _worldCamera.WorldToScreenPoint(worldPosition);

        if (screenPoint.z < 0f)
            return false;

        Camera canvasCamera = _screenCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _screenCanvas.worldCamera;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect,
            screenPoint,
            canvasCamera,
            out localPoint);
    }

    private bool TryGetLocalPointInRect(RectTransform rect, Vector3 worldPosition, out Vector2 localPoint)
    {
        localPoint = default;

        if (rect == null || _worldCamera == null)
            return false;

        Vector3 screenPoint = _worldCamera.WorldToScreenPoint(worldPosition);

        if (screenPoint.z < 0f)
            return false;

        Camera canvasCamera = _screenCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _screenCanvas.worldCamera;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect,
            screenPoint,
            canvasCamera,
            out localPoint);
    }

    private bool TryGetUiLocalPointInRect(RectTransform rect, RectTransform uiTarget, out Vector2 localPoint)
    {
        localPoint = default;

        if (rect == null || uiTarget == null)
            return false;

        Vector3[] corners = new Vector3[4];
        uiTarget.GetWorldCorners(corners);
        Vector3 center = (corners[0] + corners[2]) * 0.5f;

        Camera canvasCamera = _screenCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : _screenCanvas.worldCamera;

        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect,
            center,
            canvasCamera,
            out localPoint);
    }

    private void CacheCoinTrailUi()
    {
        if (_screenCanvas == null)
            return;

        if (_coinVfxRoot == null)
            _coinVfxRoot = FindRectTransformByName(_screenCanvas.transform, "coinvfx");

        if (_coinTrailTargetUi == null && _goldAmountText != null)
            _coinTrailTargetUi = _goldAmountText.transform.parent as RectTransform;

        _coinTrailPool.Clear();
        _coinTrailTemplate = null;

        if (_coinVfxRoot == null)
            return;

        _coinVfxRoot.gameObject.SetActive(true);

        for (int i = 0; i < _coinVfxRoot.childCount; i++)
        {
            RectTransform coin = _coinVfxRoot.GetChild(i) as RectTransform;

            if (coin == null)
                continue;

            PrepareCoinTrailCoin(coin);
            coin.gameObject.SetActive(false);
            _coinTrailPool.Add(coin);

            if (_coinTrailTemplate == null)
                _coinTrailTemplate = coin;
        }

        // Pre-grow so normal + VIP trails can run without stealing each other's coins.
        EnsureCoinTrailPoolCapacity(Mathf.Max(_coinTrailCount, _vipCoinTrailCount));
    }

    private static void PrepareCoinTrailCoin(RectTransform coin)
    {
        if (coin == null)
            return;

        Image coinImage = coin.GetComponent<Image>();
        if (coinImage != null)
            coinImage.raycastTarget = false;

        coin.localScale = Vector3.one;
    }

    private void EnsureCoinTrailPoolCapacity(int requiredCount)
    {
        requiredCount = Mathf.Max(0, requiredCount);

        while (_coinTrailPool.Count < requiredCount)
        {
            if (CreatePooledCoinTrailCoin() == null)
                break;
        }
    }

    private RectTransform CreatePooledCoinTrailCoin()
    {
        if (_coinVfxRoot == null || _coinTrailTemplate == null)
            return null;

        RectTransform coin = Instantiate(_coinTrailTemplate, _coinVfxRoot);
        coin.name = $"{_coinTrailTemplate.name}_Pooled_{_coinTrailPool.Count}";
        PrepareCoinTrailCoin(coin);
        coin.gameObject.SetActive(false);
        _coinTrailPool.Add(coin);
        return coin;
    }

    private RectTransform AcquireCoinTrailCoin()
    {
        for (int i = 0; i < _coinTrailPool.Count; i++)
        {
            RectTransform coin = _coinTrailPool[i];

            if (coin == null || _activeCoinTrailAnimations.ContainsKey(coin))
                continue;

            if (coin.gameObject.activeSelf)
                continue;

            return coin;
        }

        return CreatePooledCoinTrailCoin();
    }

    private RectTransform ResolveCoinTrailTarget()
    {
        if (_coinTrailTargetUi != null)
            return _coinTrailTargetUi;

        if (_goldAmountText != null)
            return _goldAmountText.rectTransform;

        return null;
    }

    private static Transform ResolveTableCollectPoint(Transform paymentAnchor)
    {
        if (paymentAnchor == null)
            return null;

        DiningTable table = paymentAnchor.GetComponentInParent<DiningTable>();

        if (table != null && table.PaymentAnchor != null)
            return table.PaymentAnchor;

        return paymentAnchor;
    }

    /// <summary>
    /// VIP treasure delivery coin trail: starts at the treasure Coin Point, uses VIP count + duration.
    /// </summary>
    public void PlayVipTreasureCoinTrail(Transform worldStart)
    {
        PlayCoinTrail(worldStart, useVipCount: true, durationOverride: _vipCoinTrailDuration);
    }

    private void PlayCoinTrail(Transform worldStart, bool useVipCount = false, float? durationOverride = null)
    {
        if (_coinVfxRoot == null || worldStart == null)
            return;

        if (_coinTrailPool.Count == 0 && _coinTrailTemplate == null)
            CacheCoinTrailUi();

        if (_coinTrailPool.Count == 0 && _coinTrailTemplate == null)
            return;

        if (!_coinVfxRoot.gameObject.activeSelf)
            _coinVfxRoot.gameObject.SetActive(true);

        if (!TryGetLocalPointInRect(_coinVfxRoot, worldStart.position, out Vector2 startLocal))
            return;

        RectTransform target = ResolveCoinTrailTarget();

        if (target == null || !TryGetUiLocalPointInRect(_coinVfxRoot, target, out Vector2 endLocal))
            return;

        int coinCount = Mathf.Max(1, useVipCount ? _vipCoinTrailCount : _coinTrailCount);
        EnsureCoinTrailPoolCapacity(GetBusyCoinTrailCount() + coinCount);

        float duration = Mathf.Max(0.01f, durationOverride ?? _coinTrailDuration);
        Vector2 controlPoint = (startLocal + endLocal) * 0.5f + Vector2.up * _coinTrailArcHeight;

        int sequenceId = ++_nextCoinTrailSequenceId;
        Coroutine sequence = StartCoroutine(
            PlayCoinTrailSequence(sequenceId, startLocal, endLocal, controlPoint, coinCount, duration));
        _coinTrailSequences[sequenceId] = sequence;
    }

    private int GetBusyCoinTrailCount() => _activeCoinTrailAnimations.Count;

    private IEnumerator PlayCoinTrailSequence(
        int sequenceId,
        Vector2 startLocal,
        Vector2 endLocal,
        Vector2 controlPoint,
        int coinCount,
        float duration)
    {
        coinCount = Mathf.Max(1, coinCount);

        try
        {
            for (int i = 0; i < coinCount; i++)
            {
                RectTransform coin = AcquireCoinTrailCoin();
                if (coin == null)
                    yield break;

                BeginCoinTrailAnimation(coin, startLocal, endLocal, controlPoint, duration);

                if (_coinTrailDelay > 0f && i < coinCount - 1)
                    yield return new WaitForSeconds(_coinTrailDelay);
            }
        }
        finally
        {
            _coinTrailSequences.Remove(sequenceId);
        }
    }

    private void BeginCoinTrailAnimation(
        RectTransform coin,
        Vector2 startLocal,
        Vector2 endLocal,
        Vector2 controlPoint,
        float duration)
    {
        coin.gameObject.SetActive(true);
        coin.localScale = Vector3.one;
        coin.SetAsLastSibling();

        Coroutine routine = StartCoroutine(
            AnimateCoinTrailCoin(coin, startLocal, endLocal, controlPoint, duration));
        _activeCoinTrailAnimations[coin] = routine;
    }

    private void StopAllCoinTrailAnimations()
    {
        if (_coinTrailSequences.Count > 0)
        {
            List<Coroutine> sequences = new(_coinTrailSequences.Values);
            _coinTrailSequences.Clear();

            for (int i = 0; i < sequences.Count; i++)
            {
                if (sequences[i] != null)
                    StopCoroutine(sequences[i]);
            }
        }

        foreach (KeyValuePair<RectTransform, Coroutine> entry in _activeCoinTrailAnimations)
        {
            if (entry.Value != null)
                StopCoroutine(entry.Value);

            ReleaseCoinTrailCoin(entry.Key);
        }

        _activeCoinTrailAnimations.Clear();
    }

    private void ReleaseCoinTrailCoin(RectTransform coin)
    {
        if (coin == null)
            return;

        coin.gameObject.SetActive(false);
        coin.localScale = Vector3.one;
    }

    private IEnumerator AnimateCoinTrailCoin(
        RectTransform coin,
        Vector2 startLocal,
        Vector2 endLocal,
        Vector2 controlPoint,
        float duration)
    {
        coin.anchoredPosition = startLocal;

        float flightDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < flightDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightDuration);
            float smoothT = t * t * (3f - 2f * t);
            coin.anchoredPosition = QuadraticBezier(startLocal, controlPoint, endLocal, smoothT);
            coin.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, smoothT);
            yield return null;
        }

        ReleaseCoinTrailCoin(coin);
        _activeCoinTrailAnimations.Remove(coin);
    }

    private static RectTransform FindRectTransformByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null
                && string.Equals(transforms[i].name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return transforms[i] as RectTransform;
            }
        }

        return null;
    }

    private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        float inverseT = 1f - t;
        return inverseT * inverseT * start + 2f * inverseT * t * control + t * t * end;
    }

    private static Canvas FindScreenCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);

        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].renderMode != RenderMode.WorldSpace)
                return canvases[i];
        }

        return null;
    }

    private static Canvas FindWorldCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);

        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].renderMode == RenderMode.WorldSpace)
                return canvases[i];
        }

        return null;
    }

    private void InitializeCreationSceneUi()
    {
        if (_nameInputField == null && _entryButton == null && _randomiserButton == null)
            return;

        if (_nameInputField == null)
            _nameInputField = FindNameInputField();

        if (_entryButton == null)
            _entryButton = FindEntryButton();

        if (_entryButton == null)
            _entryButton = EnsureButtonOnObject(FindGameObjectByName("Entry Button"));

        if (_randomiserButton == null)
            _randomiserButton = FindRandomiserButton();

        if (_entryButton != null)
            _entryButton.onClick.AddListener(HandleEntryClicked);

        if (_randomiserButton != null)
            _randomiserButton.onClick.AddListener(HandleRandomiserClicked);

        if (_nameInputField != null)
            _nameInputField.onSubmit.AddListener(HandleNameSubmitted);
    }

    private void InitializeCreationSceneStart()
    {
        if (_nameInputField == null)
            return;

        if (PlayerProfileStorage.TryLoadLastPlayerName(out string savedName))
            _nameInputField.text = savedName;
    }

    private void UnsubscribeCreationSceneUi()
    {
        if (_entryButton != null)
            _entryButton.onClick.RemoveListener(HandleEntryClicked);

        if (_randomiserButton != null)
            _randomiserButton.onClick.RemoveListener(HandleRandomiserClicked);

        if (_nameInputField != null)
            _nameInputField.onSubmit.RemoveListener(HandleNameSubmitted);
    }

    private void HandleRandomiserClicked()
    {
        if (_nameInputField == null)
            return;

        _nameInputField.text = ChinesePlayerNameRandomizer.Generate();
    }

    private void HandleEntryClicked()
    {
        if (!TrySaveEnteredName())
            return;

        SceneManager.LoadScene(TownSceneName);
    }

    private void HandleNameSubmitted(string _)
    {
        HandleEntryClicked();
    }

    private bool TrySaveEnteredName()
    {
        if (_nameInputField == null)
            return false;

        string enteredName = _nameInputField.text;

        if (!PlayerProfileStorage.SavePlayerName(enteredName))
        {
            Debug.LogWarning("UIManager: player name is empty.");
            return false;
        }

        PlayerProfileStorage.MarkLoanPresentationPending();

        if (GoldManager.Instance != null)
            GoldManager.Instance.ReloadGoldForCurrentPlayer();

        CharacterPanelController panel = CharacterPanelController.Instance;

        if (panel != null)
            panel.RefreshPlayerName();

        return true;
    }

    private void InitializeTownLoanUi()
    {
        if (_loanRoot == null && _getMoneyButton == null)
            return;

        if (_loanRoot == null)
            _loanRoot = FindGameObjectByName("Loan");

        if (_getMoneyButton == null)
            _getMoneyButton = FindGetMoneyButton();

        if (_loanAmountText == null)
            _loanAmountText = FindLoanAmountText();

        if (_getMoneyButton != null)
            _getMoneyButton.onClick.AddListener(HandleGetMoneyClicked);

        SyncLoanAmountText();
        HideLoan();
    }

    private void InitializeTownLoanStart()
    {
        if (_loanRoot == null)
            return;

        if (ShouldPresentLoan())
            ShowLoan();
    }

    private void UnsubscribeTownLoanUi()
    {
        if (_getMoneyButton != null)
            _getMoneyButton.onClick.RemoveListener(HandleGetMoneyClicked);
    }

    private bool ShouldPresentLoan()
    {
        if (_loanRoot == null)
            return false;

        if (!PlayerProfileStorage.IsLoanPresentationPending())
            return false;

        if (PlayerProfileStorage.HasClaimedStarterLoanForCurrentPlayer())
            return false;

        if (GoldManager.Instance != null && GoldManager.Instance.CurrentGold > 0)
            return false;

        PlayerProfileStorage.ConsumeLoanPresentationPending();
        return true;
    }

    private void HandleGetMoneyClicked()
    {
        if (_loanAmount <= 0)
            return;

        if (GoldManager.Instance != null)
            GoldManager.Instance.AddGold(_loanAmount);

        AudioManager.Play(SfxId.GoldCollect);
        PlayerProfileStorage.SetStarterLoanClaimedForCurrentPlayer();
        HideLoan();
    }

    private void ShowLoan()
    {
        SyncLoanAmountText();

        if (_loanRoot != null)
            _loanRoot.SetActive(true);
    }

    private void HideLoan()
    {
        if (_loanRoot != null)
            _loanRoot.SetActive(false);
    }

    private void SyncLoanAmountText()
    {
        if (_loanAmountText != null)
            _loanAmountText.text = _loanAmount.ToString();
    }

    private void InitializeTownBuildUi()
    {
        if (_townBuildSpot == null && _newBuildingUiRoot == null)
            return;

        if (_townBuildSpot == null)
            _townBuildSpot = FindObjectOfType<BuildSpot>();

        if (_newBuildingUiRoot == null)
            _newBuildingUiRoot = FindGameObjectByName("New Building UI");

        if (_firstBuildingOption == null && _newBuildingUiRoot != null)
        {
            Transform optionsRoot = _newBuildingUiRoot.transform.Find("BuildOptions")
                ?? _newBuildingUiRoot.transform.Find("Options");

            if (optionsRoot != null)
                _firstBuildingOption = optionsRoot.Find("1")?.gameObject;
        }

        if (_closeBuildingUi == null && _newBuildingUiRoot != null)
            _closeBuildingUi = _newBuildingUiRoot.transform.Find("Close UI")?.gameObject;

        HideNewBuildingUi();
        InitializeTownBuildingEffectUi();
        RestoreSavedTownBuildState();

        if (_townBuildSpot != null)
        {
            _townBuildSpot.Clicked += HandleTownBuildSpotClicked;
            _townBuildSpot.BuildCompleted += HandleTownRestaurantBuilt;
        }

        _townFirstOptionButton = EnsureButtonOnObject(_firstBuildingOption);
        _townCloseBuildingUiButton = EnsureButtonOnObject(_closeBuildingUi);

        if (_townFirstOptionButton != null)
            _townFirstOptionButton.onClick.AddListener(HandleTownFirstOptionClicked);

        if (_townCloseBuildingUiButton != null)
            _townCloseBuildingUiButton.onClick.AddListener(HideNewBuildingUi);
    }

    private void InitializeTownBuildStart()
    {
        if (_townBuildSpot == null)
            return;

        SetBuildSpotCostUiSuppressed(true);

        if (!_townBuildSpot.IsBuilt)
            _townBuildSpot.SetState(BuildSpotState.Active);
    }

    private void UnsubscribeTownBuildUi()
    {
        if (_townBuildRoutine != null)
        {
            StopCoroutine(_townBuildRoutine);
            _townBuildRoutine = null;
        }

        HideTownBuildingEffect();

        if (_townBuildSpot != null)
        {
            _townBuildSpot.Clicked -= HandleTownBuildSpotClicked;
            _townBuildSpot.BuildCompleted -= HandleTownRestaurantBuilt;
        }

        if (_townFirstOptionButton != null)
            _townFirstOptionButton.onClick.RemoveListener(HandleTownFirstOptionClicked);

        if (_townCloseBuildingUiButton != null)
            _townCloseBuildingUiButton.onClick.RemoveListener(HideNewBuildingUi);
    }

    private void HandleTownBuildSpotClicked(BuildSpot spot)
    {
        if (spot == null || spot.IsBuilt)
            return;

        ShowNewBuildingUi();
    }

    private void HandleTownFirstOptionClicked()
    {
        if (_townBuildSpot == null || _townBuildSpot.IsBuilt || _townBuildRoutine != null)
            return;

        if (GoldManager.Instance == null || !GoldManager.Instance.TrySpend(_townBuildCost))
            return;

        HideNewBuildingUi();
        _townBuildRoutine = StartCoroutine(PlayTownBuildSequence());
    }

    private void InitializeTownBuildingEffectUi()
    {
        if (_townBuildingEffectRoot == null)
            _townBuildingEffectRoot = FindSceneUiObject("BuildingEffect");

        if (_townBuildingEffectRoot == null)
            return;

        if (_townBuildingEffectRect == null)
            _townBuildingEffectRect = _townBuildingEffectRoot.GetComponent<RectTransform>();

        if (_townBuildTimeLeftText == null)
        {
            Transform timeText = _townBuildingEffectRoot.transform.Find("Time Bg/Time Left Text");

            if (timeText != null)
                _townBuildTimeLeftText = timeText.GetComponent<TextMeshProUGUI>();
        }

        HideTownBuildingEffect();
    }

    private IEnumerator PlayTownBuildSequence()
    {
        _townBuildSpot.EnterBuildingPhase();
        ShowTownBuildingEffect();
        AudioManager.Play(SfxId.Building);

        int totalSeconds = Mathf.Max(1, Mathf.RoundToInt(_townBuildDurationSeconds));

        for (int second = 0; second <= totalSeconds; second++)
        {
            SetTownBuildTimeLeftText(totalSeconds - second);

            if (second < totalSeconds)
                yield return new WaitForSeconds(1f);
        }

        HideTownBuildingEffect();
        _townBuildSpot.FinishBuild(playCompletionVfx: false);
        _townBuildRoutine = null;
    }

    private void ShowTownBuildingEffect()
    {
        _townBuildingEffectVisible = true;

        if (_townBuildingEffectRoot != null)
            _townBuildingEffectRoot.SetActive(true);

        UpdateTownBuildingEffectPosition();
    }

    private void HideTownBuildingEffect()
    {
        _townBuildingEffectVisible = false;

        if (_townBuildingEffectRoot != null)
            _townBuildingEffectRoot.SetActive(false);
    }

    private void SetTownBuildTimeLeftText(int secondsRemaining)
    {
        if (_townBuildTimeLeftText == null)
            return;

        secondsRemaining = Mathf.Max(0, secondsRemaining);
        _townBuildTimeLeftText.text = $"00:{secondsRemaining:D2}";
    }

    private void UpdateTownBuildingEffectPosition()
    {
        if (!_townBuildingEffectVisible || _townBuildingEffectRect == null || _townBuildSpot == null)
            return;

        UpdateScreenUiPosition(_townBuildingEffectRect, _townBuildSpot.BuildEffectAnchor.position);
    }

    private void ShowNewBuildingUi()
    {
        if (_newBuildingUiRoot != null)
            _newBuildingUiRoot.SetActive(true);
    }

    private void HideNewBuildingUi()
    {
        if (_newBuildingUiRoot != null)
            _newBuildingUiRoot.SetActive(false);
    }

    private void RestoreSavedTownBuildState()
    {
        if (_townBuildSpot == null || !PlayerProfileStorage.HasBuiltTownRestaurantForCurrentPlayer())
            return;

        if (!_townBuildSpot.IsBuilt)
            _townBuildSpot.SetState(BuildSpotState.Built);
    }

    private void HandleTownRestaurantBuilt(BuildSpot spot)
    {
        if (spot == null || spot != _townBuildSpot)
            return;

        PlayerProfileStorage.SetTownRestaurantBuiltForCurrentPlayer();
        ShowEnterShopUi();
    }

    private void InitializeEnterShopUi()
    {
        if (_enterShopUiRoot == null)
            _enterShopUiRoot = FindGameObjectByName("EnterShop");

        if (_enterShopUiRoot == null)
            return;

        if (_enterShopUiRect == null)
            _enterShopUiRect = _enterShopUiRoot.GetComponent<RectTransform>();

        if (_enterShopIcon == null && _enterShopUiRoot != null)
            _enterShopIcon = _enterShopUiRoot.transform.Find("Icon")?.gameObject;

        if (_enterShopAnchor == null)
        {
            GameObject ownerShop = FindGameObjectByName("Owner Shop");

            if (ownerShop != null)
                _enterShopAnchor = ownerShop.transform.Find("Point");
        }

        HideEnterShopUi();

        _enterShopIconButton = EnsureButtonOnObject(_enterShopIcon);

        if (_enterShopIconButton != null)
            _enterShopIconButton.onClick.AddListener(HandleEnterShopClicked);
    }

    private void InitializeEnterShopStart()
    {
        if (_enterShopUiRoot == null)
            return;

        if (PlayerProfileStorage.HasBuiltTownRestaurantForCurrentPlayer())
            ShowEnterShopUi();
    }

    private void UnsubscribeEnterShopUi()
    {
        if (_enterShopIconButton != null)
            _enterShopIconButton.onClick.RemoveListener(HandleEnterShopClicked);
    }

    private void HandleEnterShopClicked()
    {
        SceneManager.LoadScene(RestaurantSceneMode.MainSceneName);
    }

    private void ShowEnterShopUi()
    {
        _enterShopUiVisible = true;

        if (_enterShopUiRoot != null)
            _enterShopUiRoot.SetActive(true);
    }

    private void HideEnterShopUi()
    {
        _enterShopUiVisible = false;

        if (_enterShopUiRoot != null)
            _enterShopUiRoot.SetActive(false);
    }

    private void UpdateEnterShopUiPosition()
    {
        if (!_enterShopUiVisible || _enterShopUiRect == null || _enterShopAnchor == null)
            return;

        UpdateScreenUiPosition(_enterShopUiRect, _enterShopAnchor.position);
    }

    private void InitializeEnterCompetitorShopUi()
    {
        if (!IsActiveTownScene())
            return;

        EnsureCompetitorCatalogConfigured();

        CacheTownOpponentShopsIfNeeded();
        ShowTownCompetitorShops();

        if (_enterCompetitorShopUiRoot == null)
            _enterCompetitorShopUiRoot = FindGameObjectByName(EnterCompetitorShopTemplateName);

        if (!HasValidEnterCompetitorShopBindings())
            _enterCompetitorShopUiBindings = BuildEnterCompetitorShopBindingsFromScene();

        UnsubscribeEnterCompetitorShopUi();
        WireEnterCompetitorShopButtons();
        SyncEnterCompetitorShopUiVisibility(true);
    }

    private void UnsubscribeEnterCompetitorShopUi()
    {
        for (int i = 0; i < _enterCompetitorShopButtonHandlers.Count; i++)
        {
            EnterCompetitorShopButtonHandler handler = _enterCompetitorShopButtonHandlers[i];

            if (handler.Button != null && handler.Listener != null)
                handler.Button.onClick.RemoveListener(handler.Listener);
        }

        _enterCompetitorShopButtonHandlers.Clear();
    }

    private void WireEnterCompetitorShopButtons()
    {
        if (_enterCompetitorShopUiBindings == null)
            return;

        for (int i = 0; i < _enterCompetitorShopUiBindings.Length; i++)
        {
            EnterCompetitorShopUiBinding binding = _enterCompetitorShopUiBindings[i];

            if (binding?.UiRoot == null)
                continue;

            int shopIndex = ResolveEnterCompetitorShopIndex(binding, i + 1);
            Transform iconTransform = binding.UiRoot.Find("Icon");
            GameObject clickTarget = iconTransform != null ? iconTransform.gameObject : binding.UiRoot.gameObject;
            Button button = EnsureButtonOnObject(clickTarget);

            if (button == null)
                continue;

            UnityEngine.Events.UnityAction listener = () => HandleEnterCompetitorShopClicked(shopIndex);
            button.onClick.AddListener(listener);
            _enterCompetitorShopButtonHandlers.Add(new EnterCompetitorShopButtonHandler
            {
                Button = button,
                Listener = listener
            });
        }
    }

    private void HandleEnterCompetitorShopClicked(int shopIndex)
    {
        CompetitorSceneSelection.SelectFromTownShopIndex(shopIndex);
        SceneManager.LoadScene(RestaurantSceneMode.CompetitorSceneName);
    }

    private void SyncEnterCompetitorShopUiVisibility(bool visible)
    {
        if (_enterCompetitorShopUiBindings == null)
            return;

        for (int i = 0; i < _enterCompetitorShopUiBindings.Length; i++)
        {
            EnterCompetitorShopUiBinding binding = _enterCompetitorShopUiBindings[i];

            if (binding?.UiRoot == null)
                continue;

            binding.UiRoot.gameObject.SetActive(visible);

            if (!visible)
                binding.UiRoot.localScale = Vector3.one;
        }
    }

    private void UpdateEnterCompetitorShopUiPositions()
    {
        if (!IsActiveTownScene() || _enterCompetitorShopUiBindings == null)
            return;

        float pulseScale = GetEnterCompetitorShopPulseScale();

        for (int i = 0; i < _enterCompetitorShopUiBindings.Length; i++)
        {
            EnterCompetitorShopUiBinding binding = _enterCompetitorShopUiBindings[i];

            if (binding?.UiRoot == null || binding.Anchor == null || !binding.UiRoot.gameObject.activeInHierarchy)
                continue;

            UpdateScreenUiPosition(binding.UiRoot, binding.Anchor.position);
            binding.UiRoot.localScale = Vector3.one * pulseScale;
        }
    }

    private bool HasValidEnterCompetitorShopBindings()
    {
        if (_enterCompetitorShopUiBindings == null || _enterCompetitorShopUiBindings.Length == 0)
            return false;

        for (int i = 0; i < _enterCompetitorShopUiBindings.Length; i++)
        {
            EnterCompetitorShopUiBinding binding = _enterCompetitorShopUiBindings[i];

            if (binding != null && binding.UiRoot != null && binding.Anchor != null)
                return true;
        }

        return false;
    }

    private EnterCompetitorShopUiBinding[] BuildEnterCompetitorShopBindingsFromScene()
    {
        if (_enterCompetitorShopUiRoot == null)
            return null;

        Transform uiParent = _screenCanvas != null
            ? _screenCanvas.transform
            : _enterCompetitorShopUiRoot.transform.parent;

        List<EnterCompetitorShopUiBinding> bindings = new List<EnterCompetitorShopUiBinding>();

        for (int shopIndex = 1; shopIndex <= 16; shopIndex++)
        {
            GameObject shopObject = FindSceneObjectByNameIncludingInactive($"{OpponentShopNamePrefix}{shopIndex})");

            if (shopObject == null)
            {
                if (shopIndex > 1)
                    break;

                continue;
            }

            Transform anchor = shopObject.transform.Find("Point");

            if (anchor == null)
                continue;

            GameObject uiObject = FindSceneUiObject($"{EnterCompetitorShopPrefix}{shopIndex})");

            if (uiObject == null)
            {
                if (shopIndex == 1)
                    uiObject = _enterCompetitorShopUiRoot;
                else
                {
                    uiObject = Instantiate(_enterCompetitorShopUiRoot, uiParent);
                    uiObject.name = $"{EnterCompetitorShopPrefix}{shopIndex})";
                }
            }

            RectTransform uiRoot = uiObject.GetComponent<RectTransform>();

            if (uiRoot == null)
                continue;

            bindings.Add(new EnterCompetitorShopUiBinding
            {
                UiRoot = uiRoot,
                Anchor = anchor,
                ShopIndex = shopIndex
            });
        }

        return bindings.Count > 0 ? bindings.ToArray() : null;
    }

    private static int ResolveEnterCompetitorShopIndex(EnterCompetitorShopUiBinding binding, int fallbackIndex)
    {
        if (binding.ShopIndex > 0)
            return binding.ShopIndex;

        if (binding.UiRoot != null
            && TryParseIndexedSceneObjectName(binding.UiRoot.name, EnterCompetitorShopPrefix, out int parsedIndex))
        {
            return parsedIndex;
        }

        if (binding.Anchor != null)
        {
            Transform shopTransform = binding.Anchor.parent;

            if (shopTransform != null
                && TryParseIndexedSceneObjectName(shopTransform.name, OpponentShopNamePrefix, out int shopIndex))
            {
                return shopIndex;
            }
        }

        return fallbackIndex;
    }

    private static bool TryParseIndexedSceneObjectName(string objectName, string prefix, out int index)
    {
        index = 0;

        if (string.IsNullOrEmpty(objectName)
            || string.IsNullOrEmpty(prefix)
            || !objectName.StartsWith(prefix, System.StringComparison.Ordinal))
        {
            return false;
        }

        string suffix = objectName.Substring(prefix.Length);

        if (!suffix.EndsWith(")", System.StringComparison.Ordinal))
            return false;

        return int.TryParse(suffix.Substring(0, suffix.Length - 1), out index) && index > 0;
    }

    private void InitializeCompetitorSceneNameUi()
    {
        if (!RestaurantSceneMode.IsCompetitorScene)
            return;

        if (_competitorRestaurantNameText == null)
        {
            GameObject nameBg = FindGameObjectByName("Name Bg");

            if (nameBg != null)
            {
                Transform nameTextTransform = nameBg.transform.Find("Name Text");

                if (nameTextTransform != null)
                    _competitorRestaurantNameText = nameTextTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_competitorRestaurantNameText == null)
            return;

        _competitorRestaurantNameText.text = CompetitorSceneSelection.GetRestaurantName();
    }

    private void InitializeOwnShopNameUi()
    {
        if (!IsActiveTownScene())
            return;

        if (_ownShopNameUiRoot == null)
        {
            GameObject uiObject = FindSceneUiObject(OwnShopNameUiName);
            _ownShopNameUiRoot = uiObject != null ? uiObject.GetComponent<RectTransform>() : null;
        }

        if (_ownShopNameText == null && _ownShopNameUiRoot != null)
            _ownShopNameText = _ownShopNameUiRoot.GetComponent<TextMeshProUGUI>();

        GameObject ownerShop = FindGameObjectByName(OwnerShopObjectName);
        _ownerShopNameAnchor = ownerShop != null ? FindShopNamePoint(ownerShop.transform, 0) : null;

        SyncOwnShopNameText();

        if (_ownShopNameUiRoot != null)
            _ownShopNameUiRoot.gameObject.SetActive(_ownerShopNameAnchor != null);
    }

    private void SyncOwnShopNameText()
    {
        if (_ownShopNameText == null)
            return;

        _ownShopNameText.text = "我的饭店";
    }

    private void UpdateOwnShopNameUiPosition()
    {
        if (!IsActiveTownScene()
            || _ownShopNameUiRoot == null
            || _ownerShopNameAnchor == null
            || !_ownShopNameUiRoot.gameObject.activeInHierarchy)
        {
            return;
        }

        UpdateScreenUiPosition(_ownShopNameUiRoot, _ownerShopNameAnchor.position);
    }

    private void InitializeCompetitorShopNameUi()
    {
        if (!IsActiveTownScene())
            return;

        EnsureCompetitorCatalogConfigured();

        if (!HasValidCompetitorShopNameBindings())
            _competitorShopNameUiBindings = BuildCompetitorShopNameBindingsFromScene();

        CacheTownOpponentShopsIfNeeded();
        SyncCompetitorShopNameLabels();
        SyncTownCompetitorShopVisibility();
    }

    private void EnsureCompetitorCatalogConfigured()
    {
        if (_competitorCatalog == null)
            _competitorCatalog = VipCompetitorCatalog.LoadOrCreateDefault();

        _competitorCatalog.ConfigureSelection();
    }

    private void SyncCompetitorShopNameLabels()
    {
        if (_competitorShopNameUiBindings == null)
            return;

        for (int i = 0; i < _competitorShopNameUiBindings.Length; i++)
        {
            CompetitorShopNameUiBinding binding = _competitorShopNameUiBindings[i];

            if (binding?.UiRoot == null)
                continue;

            int shopIndex = ResolveCompetitorShopNameIndex(binding, i + 1);

            if (!CompetitorSceneSelection.TryGetProfileByTownShopIndex(shopIndex, out VipCompetitorProfile profile))
                continue;

            TextMeshProUGUI label = binding.UiRoot.GetComponent<TextMeshProUGUI>();

            if (label != null)
                label.text = profile.RestaurantName;
        }
    }

    private int ResolveCompetitorShopNameIndex(CompetitorShopNameUiBinding binding, int fallbackIndex)
    {
        if (binding.UiRoot != null
            && TryParseIndexedSceneObjectName(binding.UiRoot.name, CompetitorShopNamePrefix, out int parsedIndex))
        {
            return parsedIndex;
        }

        if (binding.Anchor != null)
        {
            Transform shopTransform = binding.Anchor.parent;

            if (shopTransform != null
                && TryParseIndexedSceneObjectName(shopTransform.name, OpponentShopNamePrefix, out int shopIndex))
            {
                return shopIndex;
            }
        }

        return fallbackIndex;
    }

    private void CacheTownOpponentShopsIfNeeded()
    {
        if (_townOpponentShops != null)
            return;

        List<GameObject> shops = new List<GameObject>();

        for (int shopIndex = 1; shopIndex <= 16; shopIndex++)
        {
            GameObject shopObject = FindSceneObjectByNameIncludingInactive($"{OpponentShopNamePrefix}{shopIndex})");

            if (shopObject == null)
            {
                if (shopIndex > 1)
                    break;

                continue;
            }

            shops.Add(shopObject);
        }

        _townOpponentShops = shops.ToArray();
    }

    private void ShowTownCompetitorShops()
    {
        if (_townOpponentShops == null)
            return;

        for (int i = 0; i < _townOpponentShops.Length; i++)
        {
            if (_townOpponentShops[i] != null)
                _townOpponentShops[i].SetActive(true);
        }
    }

    private void SyncTownCompetitorShopVisibility()
    {
        if (!IsActiveTownScene())
            return;

        CacheTownOpponentShopsIfNeeded();
        ShowTownCompetitorShops();

        if (_competitorShopNameUiBindings != null)
        {
            for (int i = 0; i < _competitorShopNameUiBindings.Length; i++)
            {
                CompetitorShopNameUiBinding binding = _competitorShopNameUiBindings[i];

                if (binding?.UiRoot == null)
                    continue;

                binding.UiRoot.gameObject.SetActive(true);
            }
        }

        SyncEnterCompetitorShopUiVisibility(true);
        SyncTownRatingVisibility();
    }

    private void InitializeTownRatingUi()
    {
        if (!IsActiveTownScene())
            return;

        InitializeOwnRatingUi();
        HideTownCompetitorRatingUi();
        SyncTownRatingTexts();
        SyncTownRatingVisibility();
    }

    private void InitializeOwnRatingUi()
    {
        if (_ownRatingUiRoot == null)
        {
            GameObject uiObject = FindSceneUiObject(OwnRatingUiName);
            _ownRatingUiRoot = uiObject != null ? uiObject.GetComponent<RectTransform>() : null;
        }

        if (_ownRatingText == null && _ownRatingUiRoot != null)
            _ownRatingText = _ownRatingUiRoot.GetComponent<TextMeshProUGUI>();

        GameObject ownerShop = FindGameObjectByName(OwnerShopObjectName);
        _ownerShopRatingAnchor = ownerShop != null ? FindShopRatingPoint(ownerShop.transform, 0) : null;
    }

    private void HideTownCompetitorRatingUi()
    {
        if (_screenCanvas == null)
            _screenCanvas = FindScreenCanvas();

        if (_screenCanvas == null)
            return;

        Transform[] transforms = _screenCanvas.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];

            if (candidate == null)
                continue;

            if (candidate.name.StartsWith("CompetitorRating", StringComparison.OrdinalIgnoreCase))
                candidate.gameObject.SetActive(false);
        }
    }

    private void SyncTownRatingTexts()
    {
        if (!IsActiveTownScene())
            return;

        SyncOwnRatingText();
    }

    private void SyncTownRatingVisibility()
    {
        if (!IsActiveTownScene())
            return;

        if (_ownRatingUiRoot != null)
            _ownRatingUiRoot.gameObject.SetActive(_ownerShopRatingAnchor != null);
    }

    private void SyncOwnRatingText()
    {
        if (_ownRatingText == null)
            return;

        float rating = GetOwnRestaurantRatingForTownUi();
        _ownRatingText.text = rating.ToString("0.0");
    }

    private static float GetOwnRestaurantRatingForTownUi()
    {
        PlayerProfileStorage.TryGetRestaurantRatingStateForCurrentPlayer(
            out float rating,
            out _,
            out _,
            out _);

        return rating;
    }

    private void UpdateOwnRatingUiPosition()
    {
        if (!IsActiveTownScene()
            || _ownRatingUiRoot == null
            || _ownerShopRatingAnchor == null
            || !_ownRatingUiRoot.gameObject.activeInHierarchy)
        {
            return;
        }

        UpdateScreenUiPosition(_ownRatingUiRoot, _ownerShopRatingAnchor.position);
    }

    private static Transform FindShopRatingPoint(Transform shopRoot, int shopIndex)
    {
        if (shopRoot == null)
            return null;

        if (shopIndex > 0)
        {
            Transform indexedAnchor = shopRoot.Find($"Opp{shopIndex} Rating Point");

            if (indexedAnchor != null)
                return indexedAnchor;
        }

        Transform directRatingPoint = shopRoot.Find(ShopRatingPointName);

        if (directRatingPoint != null)
            return directRatingPoint;

        Transform[] children = shopRoot.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (child != null && string.Equals(child.name, ShopRatingPointName, StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    private void UpdateCompetitorShopNameUiPositions()
    {
        if (!IsActiveTownScene() || _competitorShopNameUiBindings == null)
            return;

        for (int i = 0; i < _competitorShopNameUiBindings.Length; i++)
        {
            CompetitorShopNameUiBinding binding = _competitorShopNameUiBindings[i];

            if (binding?.UiRoot == null || binding.Anchor == null || !binding.UiRoot.gameObject.activeInHierarchy)
                continue;

            UpdateScreenUiPosition(binding.UiRoot, binding.Anchor.position);
        }
    }

    private bool HasValidCompetitorShopNameBindings()
    {
        if (_competitorShopNameUiBindings == null || _competitorShopNameUiBindings.Length == 0)
            return false;

        for (int i = 0; i < _competitorShopNameUiBindings.Length; i++)
        {
            CompetitorShopNameUiBinding binding = _competitorShopNameUiBindings[i];

            if (binding != null && binding.UiRoot != null && binding.Anchor != null)
                return true;
        }

        return false;
    }

    private CompetitorShopNameUiBinding[] BuildCompetitorShopNameBindingsFromScene()
    {
        List<CompetitorShopNameUiBinding> bindings = new List<CompetitorShopNameUiBinding>();

        for (int shopIndex = 1; shopIndex <= 16; shopIndex++)
        {
            GameObject shopObject = FindSceneObjectByNameIncludingInactive($"{OpponentShopNamePrefix}{shopIndex})");

            if (shopObject == null)
            {
                if (shopIndex > 1)
                    break;

                continue;
            }

            Transform anchor = FindShopNamePoint(shopObject.transform, shopIndex);
            GameObject nameObject = FindSceneUiObject($"{CompetitorShopNamePrefix}{shopIndex})");

            if (anchor == null || nameObject == null)
                continue;

            RectTransform uiRoot = nameObject.GetComponent<RectTransform>();

            if (uiRoot == null)
                continue;

            bindings.Add(new CompetitorShopNameUiBinding
            {
                UiRoot = uiRoot,
                Anchor = anchor
            });
        }

        return bindings.Count > 0 ? bindings.ToArray() : null;
    }

    private static Transform FindShopNamePoint(Transform shopRoot, int shopIndex)
    {
        if (shopRoot == null)
            return null;

        if (shopIndex > 0)
        {
            Transform indexedAnchor = shopRoot.Find($"Opp{shopIndex} Name Point");

            if (indexedAnchor != null)
                return indexedAnchor;
        }

        Transform directNamePoint = shopRoot.Find(ShopNamePointName);

        if (directNamePoint != null)
            return directNamePoint;

        Transform[] children = shopRoot.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (child != null && string.Equals(child.name, ShopNamePointName, StringComparison.Ordinal))
                return child;
        }

        return null;
    }

    private static bool IsActiveTownScene()
    {
        return string.Equals(SceneManager.GetActiveScene().name, TownSceneName, System.StringComparison.Ordinal);
    }

    private void InitializeSceneNavigationUi()
    {
        if (_townButton == null)
            _townButton = FindTownButton();

        if (_townButton != null)
            _townButton.onClick.AddListener(HandleTownButtonClicked);
    }

    private void UnsubscribeSceneNavigationUi()
    {
        if (_townButton != null)
            _townButton.onClick.RemoveListener(HandleTownButtonClicked);
    }

    private void HandleTownButtonClicked()
    {
        SceneManager.LoadScene(TownSceneName);
    }

    private void InitializeMainButtonsUi()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return;

        if (_mainButtonsRoot == null)
            _mainButtonsRoot = FindSceneUiObject("Main Buttons");

        if (_townButton == null)
            _townButton = FindTownButton();

        HideSceneMenuUi();
        HideTownPopupIfPresent();
        SyncMainButtonsVisibility();
    }

    private void InitializeMissionUi()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return;

        GameObject missionRoot = FindSceneUiObject("Mission");

        if (missionRoot == null)
            return;

        MissionUiController controller = missionRoot.GetComponent<MissionUiController>();

        if (controller == null)
            controller = missionRoot.AddComponent<MissionUiController>();

        controller.Initialize(_missionCatalog);
    }

    private void HideSceneMenuUi()
    {
        GameObject menuRoot = FindSceneUiObject("Menu");

        if (menuRoot != null)
            menuRoot.SetActive(false);

        GameObject menuButtonObject = FindSceneUiObject("Menu Button");

        if (menuButtonObject != null)
            menuButtonObject.SetActive(false);

        RectTransform menuPopup = FindScreenUiRect("Menu Popup");

        if (menuPopup != null)
            menuPopup.gameObject.SetActive(false);

        GameObject ratingBgObject = FindSceneUiObject("Rating Bg");

        if (ratingBgObject != null)
            ratingBgObject.SetActive(false);
    }

    private void HideTownPopupIfPresent()
    {
        RectTransform townPopup = FindScreenUiRect("Town Popup");

        if (townPopup != null)
            townPopup.gameObject.SetActive(false);
    }

    private void InitializePranksterUi()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return;

        if (_chasePranksterUiRoot == null)
            _chasePranksterUiRoot = FindScreenUiRect(ChasePranksterUiName);

        if (_pranksterNameUiRoot == null)
            _pranksterNameUiRoot = FindScreenUiRect(PranksterNameUiName);

        if (_chasePranksterButton == null && _chasePranksterUiRoot != null)
            _chasePranksterButton = EnsureButtonOnObject(_chasePranksterUiRoot.gameObject);

        if (_chasePranksterButton != null)
            _chasePranksterButton.onClick.AddListener(HandleChasePranksterClicked);

        SafeSetUiActive(_chasePranksterUiRoot, false);

        if (_chasePranksterUiRoot != null)
            _chasePranksterUiRoot.localScale = Vector3.one;

        SafeSetUiActive(_pranksterNameUiRoot, false);
        CachePranksterDialogueUi();
        HidePranksterDialogue();
    }

    private void InitializeRepairTableUi()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return;

        if (_repairTableUiRoot == null)
            _repairTableUiRoot = FindScreenUiRect(RepairTableUiName);

        if (_dushRepairUiRoot == null)
            _dushRepairUiRoot = FindScreenUiRect(DushRepairUiName);

        SafeSetUiActive(_repairTableUiRoot, false);
        SafeSetUiActive(_dushRepairUiRoot, false);
    }

    public void PlayTableBreakDustEffect(DiningTable table)
    {
        if (!RestaurantSceneMode.IsMainScene || table == null)
            return;

        if (_dushRepairUiRoot == null)
            _dushRepairUiRoot = FindScreenUiRect(DushRepairUiName);

        if (_dushRepairUiRoot == null)
            return;

        if (_dushRepairEffectRoutine != null)
            StopCoroutine(_dushRepairEffectRoutine);

        _dushRepairEffectRoutine = StartCoroutine(PlayDushRepairEffectRoutine(table));
    }

    private IEnumerator PlayDushRepairEffectRoutine(DiningTable table)
    {
        RectTransform effectRoot = _dushRepairUiRoot;
        Transform anchor = table.PaymentAnchor;

        if (anchor == null)
            anchor = table.StatusPoint;

        if (anchor != null)
            UpdateScreenUiPosition(effectRoot, anchor.position);

        effectRoot.gameObject.SetActive(true);

        Animator animator = effectRoot.GetComponent<Animator>();

        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0f, _dushRepairDuration);

        while (elapsed < duration)
        {
            if (anchor != null)
                UpdateScreenUiPosition(effectRoot, anchor.position);

            elapsed += Time.deltaTime;
            yield return null;
        }

        effectRoot.gameObject.SetActive(false);
        _dushRepairEffectRoutine = null;
    }

    private void UpdatePranksterChaseUi()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return;

        if (_pranksterManager == null)
            _pranksterManager = FindFirstObjectByType<PranksterManager>();

        if (_pranksterManager == null)
            return;

        bool shouldShow = _pranksterManager.ShouldShowChaseUi;
        float chasePulseScale = GetChasePranksterPulseScale();
        Transform chaseAnchor = _pranksterManager.ChaseUiAnchor;
        Transform nameAnchor = _pranksterManager.NameUiAnchor;

        if (_chasePranksterUiRoot != null)
        {
            if (shouldShow && chaseAnchor != null)
            {
                _chasePranksterUiRoot.gameObject.SetActive(true);
                UpdateScreenUiPosition(_chasePranksterUiRoot, chaseAnchor.position);
                _chasePranksterUiRoot.localScale = Vector3.one * chasePulseScale;

                if (_chasePranksterButton != null)
                    _chasePranksterButton.interactable = true;
            }
            else
            {
                _chasePranksterUiRoot.gameObject.SetActive(false);
                _chasePranksterUiRoot.localScale = Vector3.one;

                if (_chasePranksterButton != null)
                    _chasePranksterButton.interactable = false;
            }
        }

        if (_pranksterNameUiRoot != null)
        {
            if (shouldShow && nameAnchor != null)
            {
                _pranksterNameUiRoot.gameObject.SetActive(true);
                UpdateScreenUiPosition(_pranksterNameUiRoot, nameAnchor.position);
            }
            else
                _pranksterNameUiRoot.gameObject.SetActive(false);
        }
    }

    private void UpdateRepairTableUis()
    {
        if (!RestaurantSceneMode.IsMainScene || _repairTableUiRoot == null)
            return;

        float pulseScale = GetRepairTablePulseScale();

        foreach (KeyValuePair<DiningTable, RepairTableUiEntry> entry in _activeRepairTableUis)
        {
            DiningTable table = entry.Key;
            RepairTableUiEntry repairUi = entry.Value;

            if (table == null || repairUi.UiRoot == null)
                continue;

            Transform anchor = table.PaymentAnchor;

            if (table.IsBroken && !table.IsRepairing && anchor != null)
            {
                repairUi.UiRoot.gameObject.SetActive(true);
                UpdateScreenUiPosition(repairUi.UiRoot, anchor.position);
                repairUi.UiRoot.localScale = Vector3.one * pulseScale;
            }
            else
            {
                repairUi.UiRoot.gameObject.SetActive(false);
                repairUi.UiRoot.localScale = Vector3.one;
            }
        }
    }

    private void SyncRepairTableUis()
    {
        if (!RestaurantSceneMode.IsMainScene || _repairTableUiRoot == null)
            return;

        DiningTable[] tables = FindObjectsByType<DiningTable>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < tables.Length; i++)
        {
            DiningTable table = tables[i];

            if (table != null && table.IsBroken && !table.IsRepairing)
                ShowRepairTableUi(table);
        }

        _repairTableSyncScratch.Clear();
        _repairTableSyncScratch.AddRange(_activeRepairTableUis.Keys);

        for (int i = 0; i < _repairTableSyncScratch.Count; i++)
        {
            DiningTable table = _repairTableSyncScratch[i];

            if (table == null || !table.IsBroken || table.IsRepairing)
                HideRepairTableUi(table);
        }
    }

    private void ShowRepairTableUi(DiningTable table)
    {
        if (table == null || _repairTableUiRoot == null || _activeRepairTableUis.ContainsKey(table))
            return;

        RectTransform uiRoot = CreateRepairTableUiRoot();
        Button repairButton = EnsureButtonOnObject(uiRoot.gameObject);

        if (repairButton != null)
        {
            repairButton.onClick.RemoveAllListeners();
            repairButton.onClick.AddListener(() => HandleRepairTableClicked(table));
        }

        RepairTableUiEntry entry = new RepairTableUiEntry
        {
            UiRoot = uiRoot,
            Button = repairButton
        };
        RefreshRepairTableUi(table, ref entry);
        _activeRepairTableUis[table] = entry;
    }

    private void RefreshRepairTableUi(DiningTable table, ref RepairTableUiEntry entry)
    {
        if (table == null || entry.UiRoot == null)
            return;

        if (entry.CostText == null)
            entry.CostText = FindChildText(entry.UiRoot, "Cost");

        if (entry.CostText != null)
            entry.CostText.text = table.RepairCost.ToString();

        if (entry.Button != null)
            entry.Button.interactable = table.CanRepair();
    }

    private void RefreshRepairTableUiAffordability()
    {
        List<DiningTable> trackedTables = new(_activeRepairTableUis.Keys);

        for (int i = 0; i < trackedTables.Count; i++)
        {
            DiningTable table = trackedTables[i];

            if (table == null || !_activeRepairTableUis.TryGetValue(table, out RepairTableUiEntry entry))
                continue;

            if (entry.Button != null)
                entry.Button.interactable = table.CanRepair();
        }
    }

    private void HideRepairTableUi(DiningTable table)
    {
        if (table == null || !_activeRepairTableUis.TryGetValue(table, out RepairTableUiEntry entry))
            return;

        if (entry.Button != null)
            entry.Button.onClick.RemoveAllListeners();

        if (entry.UiRoot == _repairTableUiRoot)
        {
            entry.UiRoot.gameObject.SetActive(false);
            entry.UiRoot.localScale = Vector3.one;
        }
        else
            SafeDestroyUiClone(entry.UiRoot, _repairTableUiRoot);

        _activeRepairTableUis.Remove(table);
    }

    private void HandleChasePranksterClicked()
    {
        if (_pranksterManager == null)
            _pranksterManager = FindFirstObjectByType<PranksterManager>();

        _pranksterManager?.RequestChaseAway();
    }

    private void HandleRepairTableClicked(DiningTable table)
    {
        if (table == null || !table.IsBroken)
            return;

        if (!table.TryRepair())
            return;

        HideRepairTableUi(table);
    }

    private void ClearPranksterUi()
    {
        if (_chasePranksterUiRoot != null)
        {
            _chasePranksterUiRoot.gameObject.SetActive(false);
            _chasePranksterUiRoot.localScale = Vector3.one;
        }

        SafeSetUiActive(_pranksterNameUiRoot, false);
        HidePranksterDialogue();
    }

    private void ClearRepairTableUis()
    {
        List<DiningTable> trackedTables = new(_activeRepairTableUis.Keys);

        for (int i = 0; i < trackedTables.Count; i++)
            HideRepairTableUi(trackedTables[i]);

        SafeSetUiActive(_repairTableUiRoot, false);
    }

    private RectTransform CreateRepairTableUiRoot()
    {
        if (_activeRepairTableUis.Count == 0)
            return _repairTableUiRoot;

        RectTransform instance = Instantiate(_repairTableUiRoot, _repairTableUiRoot.parent);
        instance.gameObject.SetActive(false);
        return instance;
    }

    private float GetChasePranksterPulseScale()
    {
        return GetPulseScale(_chasePranksterPulseMinScale, _chasePranksterPulseMaxScale, _chasePranksterPulseSpeed);
    }

    private float GetRepairTablePulseScale()
    {
        return GetPulseScale(_repairTablePulseMinScale, _repairTablePulseMaxScale, _repairTablePulseSpeed);
    }

    private float GetEnterCompetitorShopPulseScale()
    {
        return GetPulseScale(
            _enterCompetitorShopPulseMinScale,
            _enterCompetitorShopPulseMaxScale,
            _enterCompetitorShopPulseSpeed);
    }

    private RectTransform FindScreenUiRect(string objectName)
    {
        GameObject uiObject = FindSceneUiObject(objectName);
        return uiObject != null ? uiObject.transform as RectTransform : null;
    }

    private void SyncMainButtonsVisibility()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return;

        if (_mainButtonsRoot == null)
            _mainButtonsRoot = FindSceneUiObject("Main Buttons");

        if (_mainButtonsRoot == null)
            return;

        bool shouldShow = GameManager.Instance != null && GameManager.Instance.IsBusiness;
        _mainButtonsRoot.SetActive(shouldShow);

        if (shouldShow)
            ClearUiSelection();
    }

    private static void ClearUiSelection()
    {
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem != null)
            eventSystem.SetSelectedGameObject(null);
    }

    private GameObject FindSceneUiObject(string objectName)
    {
        if (_screenCanvas == null)
            _screenCanvas = FindScreenCanvas();

        if (_screenCanvas != null)
        {
            Transform[] transforms = _screenCanvas.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];

                if (candidate != null && string.Equals(candidate.name, objectName, System.StringComparison.OrdinalIgnoreCase))
                    return candidate.gameObject;
            }
        }

        return FindGameObjectByName(objectName);
    }

    private static TMP_InputField FindNameInputField()
    {
        TMP_InputField[] fields = FindObjectsByType<TMP_InputField>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < fields.Length; i++)
        {
            TMP_InputField field = fields[i];

            if (field != null && string.Equals(field.name, "Input Field", System.StringComparison.OrdinalIgnoreCase))
                return field;
        }

        return fields.Length > 0 ? fields[0] : null;
    }

    private static Button FindRandomiserButton()
    {
        GameObject randomiserObject = GameObject.Find("Randomiser");
        return randomiserObject != null ? randomiserObject.GetComponent<Button>() : null;
    }

    private static Button FindEntryButton()
    {
        GameObject entryObject = GameObject.Find("Entry Button");
        return entryObject != null ? entryObject.GetComponent<Button>() : null;
    }

    private static Button FindGetMoneyButton()
    {
        GameObject buttonObject = GameObject.Find("Get money button");
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    private static TextMeshProUGUI FindLoanAmountText()
    {
        GameObject buttonObject = GameObject.Find("Get money button");

        if (buttonObject == null)
            return null;

        TextMeshProUGUI[] labels = buttonObject.GetComponentsInChildren<TextMeshProUGUI>(true);

        for (int i = 0; i < labels.Length; i++)
        {
            TextMeshProUGUI label = labels[i];

            if (label != null && int.TryParse(label.text, out _))
                return label;
        }

        return labels.Length > 0 ? labels[0] : null;
    }

    private static Button FindTownButton()
    {
        GameObject townButtonObject = GameObject.Find("Town Button");
        return townButtonObject != null ? townButtonObject.GetComponent<Button>() : null;
    }

    private static GameObject FindGameObjectByName(string objectName)
    {
        return GameObject.Find(objectName);
    }

    private static GameObject FindSceneObjectByNameIncludingInactive(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        GameObject activeMatch = GameObject.Find(objectName);

        if (activeMatch != null)
            return activeMatch;

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid())
            return null;

        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            GameObject root = roots[rootIndex];

            if (root == null)
                continue;

            if (string.Equals(root.name, objectName, StringComparison.Ordinal))
                return root;

            Transform[] children = root.GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];

                if (child != null && string.Equals(child.name, objectName, StringComparison.Ordinal))
                    return child.gameObject;
            }
        }

        return null;
    }

    private static Button EnsureButtonOnObject(GameObject target)
    {
        if (target == null)
            return null;

        Button button = target.GetComponent<Button>();

        if (button != null)
            return button;

        button = target.AddComponent<Button>();
        Image image = target.GetComponent<Image>();

        if (image != null)
            button.targetGraphic = image;

        return button;
    }
}
