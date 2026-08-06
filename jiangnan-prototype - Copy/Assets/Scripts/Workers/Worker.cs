using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NavMeshLocomotion))]
[RequireComponent(typeof(WorkerEnergy))]
public class Worker : MonoBehaviour
{
    [SerializeField] private WorkerType _workerType;
    [SerializeField] private Transform _waitPoint;
    [SerializeField] private Transform _restPoint;
    [SerializeField] private Transform _energyUiAnchor;
    [SerializeField] private RectTransform _energyUiRoot;
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private NavMeshLocomotion _locomotion;
    [SerializeField] private WorkerEnergy _energy;

    private WorkerState _state = WorkerState.Wait;

    public WorkerType WorkerType => _workerType;
    public NavMeshAgent Agent => _agent;
    public NavMeshLocomotion Locomotion => _locomotion;
    public WorkerEnergy Energy => _energy;
    public WorkerState State => _state;
    public RectTransform EnergyUiRoot => _energyUiRoot;
    public Transform WaitPoint => _waitPoint;
    public bool IsAvailable => _state == WorkerState.Wait;
    public bool IsResting => _state == WorkerState.Rest;

    public Vector3 EnergyUiWorldPosition
    {
        get
        {
            if (_energyUiAnchor != null)
                return _energyUiAnchor.position;

            float offset = UIManager.Instance != null
                ? UIManager.Instance.WorkerEnergyHeadHeightOffset
                : 2.2f;

            return transform.position + Vector3.up * offset;
        }
    }

    public Transform GetRestPoint()
    {
        return _restPoint != null ? _restPoint : _waitPoint;
    }

    public AudioSource GetRestPointAudioSource()
    {
        Transform restPoint = GetRestPoint();
        return restPoint != null ? restPoint.GetComponent<AudioSource>() : null;
    }

    public AudioSource GetWorkerAudioSource()
    {
        return GetComponent<AudioSource>();
    }

    public void PlayRestingAudio()
    {
        AudioManager.PlayBgmOn(GetRestPointAudioSource(), BgmId.Sleeping);
    }

    public void StopRestingAudio()
    {
        AudioManager.StopSource(GetRestPointAudioSource());
    }

    public void PlayKickAudio()
    {
        AudioManager.PlayOn(GetRestPointAudioSource(), SfxId.KickWorker);
    }

    private void Awake()
    {
        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();

        if (_locomotion == null)
            _locomotion = GetComponent<NavMeshLocomotion>();

        if (_energy == null)
            _energy = GetComponent<WorkerEnergy>();

        _locomotion.Configure();
        EnsureWorkerAnimator();
    }

    private void EnsureWorkerAnimator()
    {
        RuntimeAnimatorController controller = _workerType switch
        {
            WorkerType.Waiter => ResolveWorkerController(
                "Waiter",
                "Assets/Animations/Characters/WaiterControllerDependencies/Waiter.controller"),
            WorkerType.Chef => ResolveWorkerController(
                "Chef",
                "Assets/Animations/Characters/ChefControllerDependencies/Chef.controller"),
            _ => null,
        };

        if (controller == null)
            return;

        Animator animator = GetComponent<Animator>();

        if (animator == null)
            animator = gameObject.AddComponent<Animator>();

        if (animator.runtimeAnimatorController == null)
            animator.runtimeAnimatorController = controller;

        if (animator.avatar == null)
            animator.avatar = LoadAvatarFromSourcePrefab();

        animator.applyRootMotion = false;

        if (GetComponent<WorkerCharacterAnimator>() == null)
            gameObject.AddComponent<WorkerCharacterAnimator>();
    }

    private static RuntimeAnimatorController ResolveWorkerController(string resourceName, string editorPath)
    {
        RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>(resourceName);

        if (controller != null)
            return controller;

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(editorPath);
#else
        return null;
#endif
    }

    private Avatar LoadAvatarFromSourcePrefab()
    {
#if UNITY_EDITOR
        string assetPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);

        if (!string.IsNullOrEmpty(assetPath))
        {
            Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);

            for (int i = 0; i < assets.Length; i++)
            {
                if (assets[i] is Avatar avatar)
                    return avatar;
            }
        }
#endif

        return null;
    }

    private void Start()
    {
        TryRegister();

        if (ShouldStayHiddenUntilHired())
            gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        TryRegister();

        if (RestaurantSceneMode.UsesWorkerEnergyUi)
            UIManager.Instance?.RegisterWorkerEnergyUi(this);
    }

    private void OnDisable()
    {
        UIManager.Instance?.UnregisterWorkerEnergyUi(this);
    }

    private void OnDestroy()
    {
        if (WorkerManager.Instance != null)
            WorkerManager.Instance.UnregisterWorker(this);
    }

    private void TryRegister()
    {
        if (WorkerManager.Instance != null)
            WorkerManager.Instance.RegisterWorker(this);
    }

    private bool ShouldStayHiddenUntilHired()
    {
        HireSpot[] spots = FindObjectsOfType<HireSpot>(true);

        for (int i = 0; i < spots.Length; i++)
        {
            HireSpot spot = spots[i];

            if (spot == null || spot.IsHired || spot.Workers == null)
                continue;

            for (int j = 0; j < spot.Workers.Length; j++)
            {
                if (spot.Workers[j] == gameObject)
                    return true;
            }
        }

        return false;
    }

    public void SetState(WorkerState state)
    {
        if (_state == state)
            return;

        _state = state;
        GameEvents.RaiseWorkerStateChanged(this, state);
        ApplyLocomotionForState(state);
    }

    private void ApplyLocomotionForState(WorkerState state)
    {
        if (state == WorkerState.Wait || state == WorkerState.Cook || state == WorkerState.Rest)
            _locomotion.EnterStationary();
        else if (state == WorkerState.GoToStove || state == WorkerState.BringDish)
            _locomotion.ExitStationary();
    }

    public void WarpTo(Vector3 position)
    {
        _locomotion.ExitStationary();
        NavMeshMovement.TryWarp(_agent, position);
        NavMeshMovement.Stop(_agent);
    }

    public void StopMovement()
    {
        NavMeshMovement.Stop(_agent);
    }

    public void ResetToWait()
    {
        SetState(WorkerState.Wait);
    }

    public void FaceDirection(Quaternion rotation)
    {
        _locomotion.FaceDirection(rotation);
    }
}
