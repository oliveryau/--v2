using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Worker))]
public class WorkerCharacterAnimator : MonoBehaviour
{
    private const int CookAnimationVariantCount = 3;

    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsCookingHash = Animator.StringToHash("IsCooking");
    private static readonly int CookAnimIndexHash = Animator.StringToHash("CookAnimIndex");

    [SerializeField] private Animator _animator;
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private NavMeshLocomotion _locomotion;
    [SerializeField] private float _walkVelocityThreshold;

    private Worker _worker;
    private WorkerState _lastWorkerState = WorkerState.Wait;
    private bool _hasIsCookingParameter;
    private bool _hasIsWalkingParameter;
    private bool _hasCookAnimIndexParameter;
    private bool _lastIsWalking;
    private bool _lastIsCooking;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();

        if (_locomotion == null)
            _locomotion = GetComponent<NavMeshLocomotion>();

        _worker = GetComponent<Worker>();
        _hasIsCookingParameter = HasAnimatorParameter(IsCookingHash);
        _hasIsWalkingParameter = HasAnimatorParameter(IsWalkingHash);
        _hasCookAnimIndexParameter = HasAnimatorParameter(CookAnimIndexHash);
    }

    private void OnEnable()
    {
        GameEvents.WorkerStateChanged += HandleWorkerStateChanged;
        RefreshAnimationState(force: true);
    }

    private void OnDisable()
    {
        GameEvents.WorkerStateChanged -= HandleWorkerStateChanged;
    }

    private void LateUpdate()
    {
        if (_animator == null || _worker == null || !_animator.isActiveAndEnabled)
            return;

        if (_worker.State == WorkerState.Cook && !_lastIsWalking)
            return;

        if (_locomotion != null && _locomotion.IsStationary && !_lastIsWalking)
            return;

        RefreshAnimationState(force: false);
    }

    private void HandleWorkerStateChanged(Worker worker, WorkerState state)
    {
        if (worker != _worker)
            return;

        if (_worker.WorkerType == WorkerType.Chef
            && state == WorkerState.Cook
            && _lastWorkerState != WorkerState.Cook
            && _hasCookAnimIndexParameter)
        {
            _animator.SetInteger(CookAnimIndexHash, UnityEngine.Random.Range(0, CookAnimationVariantCount));
        }

        _lastWorkerState = state;
        RefreshAnimationState(force: true);
    }

    private void RefreshAnimationState(bool force)
    {
        if (_animator == null || _worker == null || !_animator.isActiveAndEnabled)
            return;

        bool isCooking = _worker.WorkerType == WorkerType.Chef && _worker.State == WorkerState.Cook;

        if (_hasIsCookingParameter && (force || _lastIsCooking != isCooking))
        {
            _animator.SetBool(IsCookingHash, isCooking);
            _lastIsCooking = isCooking;
        }

        if (!_hasIsWalkingParameter)
            return;

        bool isWalking = !isCooking
            && _locomotion != null
            && !_locomotion.IsStationary
            && _agent != null
            && _agent.velocity.sqrMagnitude > _walkVelocityThreshold * _walkVelocityThreshold;

        if (force || _lastIsWalking != isWalking)
        {
            _animator.SetBool(IsWalkingHash, isWalking);
            _lastIsWalking = isWalking;
        }
    }

    private bool HasAnimatorParameter(int parameterHash)
    {
        for (int i = 0; i < _animator.parameterCount; i++)
        {
            if (_animator.GetParameter(i).nameHash == parameterHash)
                return true;
        }

        return false;
    }
}
