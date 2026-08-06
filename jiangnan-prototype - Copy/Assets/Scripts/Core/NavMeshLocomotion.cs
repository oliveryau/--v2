using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(NavMeshAgent))]
public class NavMeshLocomotion : MonoBehaviour
{
    [SerializeField] private NavMeshAgent _agent;

    private bool _isStationary;

    public NavMeshAgent Agent => _agent;
    public bool IsStationary => _isStationary;

    private void Awake()
    {
        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();
    }

    public void Configure()
    {
        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();

        CharacterCollisions.PrepareCharacter(gameObject);

        if (_agent == null)
            return;

        _agent.acceleration = 12f;
        _agent.angularSpeed = 360f;
        _agent.autoBraking = true;
        _agent.stoppingDistance = 0.15f;
        CharacterCollisions.ConfigurePassThroughAgent(_agent);

        if (NavMeshMovement.CanControl(_agent))
            _agent.isStopped = true;
    }

    public void BeginMovement()
    {
        ExitStationary();

        if (_agent == null || !NavMeshMovement.CanControl(_agent))
            return;

        NavMeshMovement.TryWarp(_agent, transform.position);
        _agent.updatePosition = true;
        _agent.updateRotation = true;
        _agent.isStopped = false;
    }

    public void EnterStationary()
    {
        if (_isStationary || !isActiveAndEnabled)
            return;

        _isStationary = true;
        NavMeshMovement.Stop(_agent);

        if (_agent != null)
            _agent.updatePosition = false;
    }

    public void ExitStationary()
    {
        if (!_isStationary)
            return;

        _isStationary = false;

        if (_agent == null)
            return;

        _agent.updatePosition = true;
        _agent.updateRotation = true;
    }

    public void Release()
    {
        _isStationary = false;

        if (_agent != null)
            _agent.updatePosition = true;

        NavMeshMovement.Stop(_agent);
    }

    public void FaceDirection(Quaternion rotation)
    {
        if (_agent != null)
            _agent.updateRotation = false;

        transform.rotation = rotation;
    }
}
