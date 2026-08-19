using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Main-scene rival owner who drops in after the prankster, poaches every normal diner
/// and can be chased away before he does. Unrelated to CompetitorVisitController,
/// which drives the player's own steal run inside a competitor shop.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(126)]
public class CompetitorVisitorManager : MonoBehaviour
{
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

    [Header("References")]
    [Tooltip("Model used for 戴威 / 春华.")]
    [SerializeField] private Transform _maleCompetitor;
    [Tooltip("Model used for 红姐 / 韩熙.")]
    [SerializeField] private Transform _femaleCompetitor;
    [SerializeField] private Transform _outerSpawnPoint;
    [SerializeField] private Transform _competitorWaypoint;
    [SerializeField] private Transform _exitPoint;
    [SerializeField] private VipCompetitorCatalog _competitorCatalog;

    [Header("Scheduling")]
    [Tooltip("Customers that must spawn after the last prankster leaves before the rival walks in.")]
    [SerializeField] private int _customerSpawnInterval = 5;

    [Header("Timings")]
    [Tooltip("Seconds spent at the waypoint waiting to be chased away before the customers are poached.")]
    [SerializeField] private float _waitAtWaypointDuration = 10f;

    private CompetitorModel _maleModel;
    private CompetitorModel _femaleModel;
    private CompetitorModel _activeModel;
    private VipCompetitor _activeCompetitor;
    private bool _visitActive;
    private bool _awaitingSpawn;
    private int _customersSinceScheduled;
    private bool _chaseDismissed;
    private Coroutine _visitRoutine;

    public bool IsVisitActive => _visitActive;
    public bool IsAwaitingSpawn => _awaitingSpawn;
    public VipCompetitor ActiveCompetitor => _activeCompetitor;

    public bool ShouldShowChaseUi => _visitActive
        && !_chaseDismissed
        && _activeModel != null
        && _activeModel.Root != null
        && _activeModel.Root.gameObject.activeSelf;

    public Transform ChaseUiAnchor => _activeModel?.ChaseUiAnchor;
    public Transform NameUiAnchor => _activeModel?.NameUiAnchor;

    /// <summary>
    /// The rival only starts showing up once the player has poached customers from him
    /// (3 normals or 1 VIP in a single competitor run).
    /// </summary>
    public static bool IsUnlockedForCurrentPlayer()
    {
        return PlayerProfileStorage.HasCompetitorVipStealAttemptedForCurrentPlayer();
    }

    private void Awake()
    {
        if (!RestaurantSceneMode.IsMainScene)
        {
            enabled = false;
            return;
        }

        if (_maleCompetitor == null)
            _maleCompetitor = FindChildByName(transform, "Male Competitor");

        if (_femaleCompetitor == null)
            _femaleCompetitor = FindChildByName(transform, "Female Competitor");

        if (_outerSpawnPoint == null)
            _outerSpawnPoint = FindTransformByName("Outer Spawn Point");

        if (_competitorWaypoint == null)
            _competitorWaypoint = FindTransformByName("Prankster Waypoint");

        if (_exitPoint == null)
            _exitPoint = _outerSpawnPoint;

        _maleModel = CompetitorModel.Create(_maleCompetitor);
        _femaleModel = CompetitorModel.Create(_femaleCompetitor);

        if (_competitorCatalog == null)
            _competitorCatalog = VipCompetitorCatalog.LoadOrCreateDefault();

        _competitorCatalog.ConfigureSelection();
    }

    private void OnEnable()
    {
        if (!RestaurantSceneMode.IsMainScene)
            return;

        GameEvents.StateChanged += HandleStateChanged;
        GameEvents.CustomerSpawned += HandleCustomerSpawned;
    }

    private void Start()
    {
        HideModel(_maleModel);
        HideModel(_femaleModel);
    }

    private void OnDisable()
    {
        GameEvents.StateChanged -= HandleStateChanged;
        GameEvents.CustomerSpawned -= HandleCustomerSpawned;
        CancelActiveVisit();
    }

    private void LateUpdate()
    {
        if (_activeModel != null)
            _activeModel.SyncWalkAnimation();
    }

    private void HandleStateChanged(GameState state)
    {
        if (state != GameState.Business)
            CancelActiveVisit();
    }

    /// <summary>
    /// Claims the turn after the last prankster. He does not walk in right away: like the VIP
    /// and the prankster, he waits out a spawn interval first. Returns false when the
    /// alternation should carry on without him.
    /// </summary>
    public bool TryScheduleVisit()
    {
        if (!RestaurantSceneMode.IsMainScene || !enabled || !gameObject.activeInHierarchy)
            return false;

        if (_visitActive || _awaitingSpawn || !IsBusinessActive() || !IsUnlockedForCurrentPlayer())
            return false;

        if (CustomerManager.Instance == null || !CustomerManager.Instance.CanStartVipPhaseContent())
            return false;

        if (_outerSpawnPoint == null || _competitorWaypoint == null)
            return false;

        if (_maleModel == null && _femaleModel == null)
            return false;

        _awaitingSpawn = true;
        _customersSinceScheduled = 0;
        return true;
    }

    private void HandleCustomerSpawned()
    {
        if (!_awaitingSpawn || _visitActive || !IsBusinessActive())
            return;

        _customersSinceScheduled++;

        if (_customersSinceScheduled < Mathf.Max(1, _customerSpawnInterval))
            return;

        _customersSinceScheduled = 0;
        _awaitingSpawn = false;
        BeginVisit();
    }

    private void BeginVisit()
    {
        _activeCompetitor = CompetitorSceneSelection.PickRandomStealCompetitor();
        _activeModel = ResolveModelFor(_activeCompetitor);

        if (_activeModel == null)
        {
            // Nobody to walk in — hand the turn straight back so the lull still opens.
            CustomerManager.Instance?.NotifyCompetitorVisitorEndedForAlternation();
            return;
        }

        _visitRoutine = StartCoroutine(RunVisit());
    }

    public void RequestChaseAway()
    {
        if (!_visitActive || _chaseDismissed)
            return;

        _chaseDismissed = true;
        AudioManager.Play(SfxId.Unhappy);
        _activeModel?.PlayKickAudio();
        UIManager.Instance?.PlayCompetitorVisitorChasedAwayDialogue();

        if (_visitRoutine != null)
        {
            StopCoroutine(_visitRoutine);
            _visitRoutine = null;
        }

        _visitRoutine = StartCoroutine(LeaveImmediately());
    }

    private IEnumerator RunVisit()
    {
        _visitActive = true;
        _chaseDismissed = false;

        // Activate first: the agent can only be warped while its object is live.
        _activeModel.Root.gameObject.SetActive(true);
        _activeModel.PrepareForVisit();
        _activeModel.WarpTo(_outerSpawnPoint.position);
        UIManager.Instance?.SetCompetitorVisitorIdentity(_activeCompetitor);

        yield return MoveActiveModel(_competitorWaypoint.position);

        if (_chaseDismissed)
        {
            yield return LeaveImmediately();
            yield break;
        }

        _activeModel.EnterStationary();
        float waitDuration = Mathf.Max(0f, _waitAtWaypointDuration);
        AudioManager.Play(SfxId.PranksterLaugh);
        UIManager.Instance?.PlayCompetitorVisitorArrivalDialogue(_activeCompetitor, waitDuration);

        float elapsed = 0f;
        while (elapsed < waitDuration)
        {
            if (_chaseDismissed)
                break;

            UIManager.Instance?.UpdateCompetitorVisitorWaitTimer(waitDuration - elapsed, waitDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        UIManager.Instance?.HideCompetitorVisitorWaitTimer();

        if (_chaseDismissed)
        {
            yield return LeaveImmediately();
            yield break;
        }

        StealAllCustomers();

        yield return LeaveImmediately();
    }

    private void StealAllCustomers()
    {
        AudioManager.Play(SfxId.PranksterLaugh);
        _chaseDismissed = true;
        UIManager.Instance?.PlayCompetitorVisitorStoleCustomersDialogue();
        UIManager.Instance?.ShowCompetitorVisitorShout(_activeCompetitor);

        // Poached diners walk out without paying; VIPs stay put.
        CustomerManager.Instance?.ExpelAllNonVipCustomers();
    }

    private IEnumerator LeaveImmediately()
    {
        if (_activeModel != null && _activeModel.Root != null && _activeModel.Root.gameObject.activeSelf)
        {
            Vector3 leavePosition = _exitPoint != null ? _exitPoint.position : _outerSpawnPoint.position;
            yield return MoveActiveModel(leavePosition);
            HideModel(_activeModel);
        }

        UIManager.Instance?.HideCompetitorVisitorDialogue();

        _visitActive = false;
        _visitRoutine = null;
        _activeModel = null;

        CustomerManager.Instance?.NotifyCompetitorVisitorEndedForAlternation();
    }

    private IEnumerator MoveActiveModel(Vector3 destination)
    {
        _activeModel.ExitStationary();
        yield return NavMeshMovement.MoveTo(_activeModel.Locomotion, destination);
    }

    private CompetitorModel ResolveModelFor(VipCompetitor competitor)
    {
        CompetitorModel model = competitor switch
        {
            VipCompetitor.HongJie => _femaleModel,
            VipCompetitor.HanXi => _femaleModel,
            _ => _maleModel
        };

        return model ?? _maleModel ?? _femaleModel;
    }

    public void CancelActiveVisit()
    {
        if (_visitRoutine != null)
        {
            StopCoroutine(_visitRoutine);
            _visitRoutine = null;
        }

        _visitActive = false;
        _awaitingSpawn = false;
        _customersSinceScheduled = 0;
        _chaseDismissed = true;

        HideModel(_maleModel);
        HideModel(_femaleModel);
        _activeModel = null;

        UIManager.Instance?.HideCompetitorVisitorDialogue();
    }

    private static void HideModel(CompetitorModel model)
    {
        if (model != null && model.Root != null)
            model.Root.gameObject.SetActive(false);
    }

    private static bool IsBusinessActive()
    {
        return GameManager.Instance != null && GameManager.Instance.IsBusiness;
    }

    private static Transform FindTransformByName(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        Transform directChild = root.Find(childName);

        if (directChild != null)
            return directChild;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (child != null && string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }

    /// <summary>
    /// Runtime handle for one of the two competitor models. The models are plain characters
    /// in the scene, so movement, anchors and the walk animation are driven from here.
    /// </summary>
    private sealed class CompetitorModel
    {
        public Transform Root { get; private set; }
        public NavMeshAgent Agent { get; private set; }
        public NavMeshLocomotion Locomotion { get; private set; }
        public Transform ChaseUiAnchor { get; private set; }
        public Transform NameUiAnchor { get; private set; }

        private const float WalkVelocityThreshold = 0.05f;

        private Animator _animator;
        private AudioSource _audioSource;
        private bool _walkParameterResolved;
        private bool _hasWalkParameter;
        private bool _lastIsWalking;

        public static CompetitorModel Create(Transform root)
        {
            if (root == null)
                return null;

            NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
            NavMeshLocomotion locomotion = root.GetComponent<NavMeshLocomotion>();

            if (agent == null || locomotion == null)
            {
                Debug.LogWarning($"Competitor model {root.name} needs a NavMeshAgent and NavMeshLocomotion.", root);
                return null;
            }

            CompetitorModel model = new()
            {
                Root = root,
                Agent = agent,
                Locomotion = locomotion,
                _animator = root.GetComponent<Animator>(),
                _audioSource = root.GetComponent<AudioSource>()
            };

            model.ChaseUiAnchor = FindChildByName(root, "Point") ?? root;
            model.NameUiAnchor = FindChildByName(root, "Name Point") ?? root;

            return model;
        }

        public void PrepareForVisit()
        {
            Locomotion.Release();
            Locomotion.Configure();
        }

        public void WarpTo(Vector3 position)
        {
            Locomotion.ExitStationary();
            NavMeshMovement.TryWarp(Agent, position);
            NavMeshMovement.Stop(Agent);
        }

        public void EnterStationary()
        {
            Locomotion.EnterStationary();
        }

        public void ExitStationary()
        {
            Locomotion.ExitStationary();
        }

        public void PlayKickAudio()
        {
            if (_audioSource != null)
                AudioManager.PlayOn(_audioSource, SfxId.KickWorker);
        }

        public void SyncWalkAnimation()
        {
            if (_animator == null || !_animator.isActiveAndEnabled)
                return;

            // The animator only reports its parameters once its object has gone live.
            if (!_walkParameterResolved)
            {
                _hasWalkParameter = HasAnimatorParameter(_animator, IsWalkingHash);
                _walkParameterResolved = true;
            }

            if (!_hasWalkParameter)
                return;

            bool isWalking = !Locomotion.IsStationary
                && Agent != null
                && Agent.velocity.sqrMagnitude > WalkVelocityThreshold * WalkVelocityThreshold;

            if (_lastIsWalking == isWalking)
                return;

            _animator.SetBool(IsWalkingHash, isWalking);
            _lastIsWalking = isWalking;
        }

        private static bool HasAnimatorParameter(Animator animator, int parameterHash)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return false;

            for (int i = 0; i < animator.parameterCount; i++)
            {
                if (animator.GetParameter(i).nameHash == parameterHash)
                    return true;
            }

            return false;
        }
    }
}
