using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Prankster))]
public class PranksterCharacterAnimator : MonoBehaviour
{
    private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");

    [SerializeField] private Animator _animator;
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private NavMeshLocomotion _locomotion;
    [SerializeField] private float _walkVelocityThreshold;

    private Prankster _prankster;
    private bool _lastIsWalking;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();

        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();

        if (_locomotion == null)
            _locomotion = GetComponent<NavMeshLocomotion>();

        _prankster = GetComponent<Prankster>();
    }

    private void LateUpdate()
    {
        if (_animator == null || _prankster == null)
            return;

        if (_locomotion != null && _locomotion.IsStationary && !_lastIsWalking)
            return;

        bool isMoving = _locomotion != null
            && !_locomotion.IsStationary
            && _agent != null
            && _agent.velocity.sqrMagnitude > _walkVelocityThreshold * _walkVelocityThreshold;

        if (_lastIsWalking == isMoving)
            return;

        _animator.SetBool(IsWalkingHash, isMoving);
        _lastIsWalking = isMoving;
    }
}
