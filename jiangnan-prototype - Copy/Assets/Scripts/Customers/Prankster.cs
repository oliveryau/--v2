using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NavMeshLocomotion))]
public class Prankster : MonoBehaviour
{
    [SerializeField] private NavMeshAgent _agent;
    [SerializeField] private NavMeshLocomotion _locomotion;

    public NavMeshLocomotion Locomotion => _locomotion;
    public Transform ChaseUiAnchor => ResolveChildAnchor("Point");
    public Transform NameUiAnchor => ResolveChildAnchor("Name Point");

    private Transform ResolveChildAnchor(string childName)
    {
        if (string.IsNullOrEmpty(childName))
            return transform;

        Transform directChild = transform.Find(childName);

        if (directChild != null)
            return directChild;

        Transform[] children = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];

            if (child != null && string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return transform;
    }

    private void Awake()
    {
        if (_agent == null)
            _agent = GetComponent<NavMeshAgent>();

        if (_locomotion == null)
            _locomotion = GetComponent<NavMeshLocomotion>();

        _locomotion.Configure();
    }

    public void PrepareForVisit()
    {
        _locomotion.Release();
        _locomotion.Configure();
    }

    public void PlayKickAudio()
    {
        AudioManager.PlayOn(GetComponent<AudioSource>(), SfxId.KickWorker);
    }

    public void WarpTo(Vector3 position)
    {
        _locomotion.ExitStationary();
        NavMeshMovement.TryWarp(_agent, position);
        NavMeshMovement.Stop(_agent);
    }

    public void EnterStationary()
    {
        _locomotion.EnterStationary();
    }

    public void ExitStationary()
    {
        _locomotion.ExitStationary();
    }
}
