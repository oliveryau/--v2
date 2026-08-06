using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Customer))]
public class CustomerCharacterAnimator : MonoBehaviour
{
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    private static readonly int IsSittingHash = Animator.StringToHash("IsSitting");
    private static readonly int IsEatingHash = Animator.StringToHash("IsEating");

    [SerializeField] private Animator _animator;
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private NavMeshLocomotion _locomotion;
    [SerializeField] private float _walkVelocityThreshold;

    private Customer _customer;
    private bool _hasIsEatingParameter;
    private bool _lastIsWalking;
    private bool _lastIsSitting;
    private bool _lastIsEating;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();

        if (_locomotion == null)
            _locomotion = GetComponent<NavMeshLocomotion>();

        _customer = GetComponent<Customer>();
        _hasIsEatingParameter = HasAnimatorParameter(IsEatingHash);
    }

    private void OnEnable()
    {
        GameEvents.CustomerStateChanged += HandleCustomerStateChanged;
        RefreshAnimationState(force: true);
    }

    private void OnDisable()
    {
        GameEvents.CustomerStateChanged -= HandleCustomerStateChanged;
    }

    private void LateUpdate()
    {
        if (_animator == null || _customer == null)
            return;

        if (_locomotion != null
            && _locomotion.IsStationary
            && !_lastIsWalking
            && !IsMovementRelevantState(_customer.State))
        {
            return;
        }

        RefreshAnimationState(force: false);
    }

    private void HandleCustomerStateChanged(Customer customer, CustomerState state)
    {
        if (customer != _customer)
            return;

        RefreshAnimationState(force: true);
    }

    private void RefreshAnimationState(bool force)
    {
        if (_animator == null || _customer == null)
            return;

        bool isMoving = _locomotion != null
            && !_locomotion.IsStationary
            && _agent != null
            && _agent.velocity.sqrMagnitude > _walkVelocityThreshold * _walkVelocityThreshold;

        bool isEating = _customer.State == CustomerState.Eating && !isMoving;
        bool isSitting = IsSittingState(_customer.State) && !isMoving;
        bool isWalking = isMoving;

        if (force || _lastIsSitting != isSitting)
        {
            _animator.SetBool(IsSittingHash, isSitting);
            _lastIsSitting = isSitting;
        }

        if (force || _lastIsWalking != isWalking)
        {
            _animator.SetBool(IsWalkingHash, isWalking);
            _lastIsWalking = isWalking;
        }

        if (_hasIsEatingParameter && (force || _lastIsEating != isEating))
        {
            _animator.SetBool(IsEatingHash, isEating);
            _lastIsEating = isEating;
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

    private static bool IsMovementRelevantState(CustomerState state)
    {
        return state == CustomerState.Queue
            || state == CustomerState.WalkingToSeat
            || state == CustomerState.Leaving;
    }

    private static bool IsSittingState(CustomerState state)
    {
        return state == CustomerState.Ordering
            || state == CustomerState.Paying;
    }
}
