using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public static class NavMeshMovement
{
    private const float DefaultTimeoutSeconds = 30f;

    public static IEnumerator MoveTo(NavMeshLocomotion locomotion, Vector3 destination, float stoppingDistance = 0.15f)
    {
        if (locomotion == null)
            yield break;

        locomotion.BeginMovement();
        yield return MoveTo(locomotion.Agent, destination, stoppingDistance);
    }

    public static IEnumerator MoveAlong(NavMeshLocomotion locomotion, Vector3[] destinations, float stoppingDistance = 0.15f)
    {
        if (locomotion == null || destinations == null || destinations.Length == 0)
            yield break;

        locomotion.BeginMovement();

        for (int i = 0; i < destinations.Length; i++)
            yield return MoveTo(locomotion.Agent, destinations[i], stoppingDistance);
    }

    public static IEnumerator MoveTo(NavMeshAgent agent, Vector3 destination, float stoppingDistance = 0.15f)
    {
        if (agent == null)
            yield break;

        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"NavMeshAgent on {agent.name} is not on a NavMesh.", agent);
            yield break;
        }

        Vector3 reachableDestination = ResolveReachablePosition(destination);

        agent.isStopped = false;
        agent.stoppingDistance = stoppingDistance;
        agent.updateRotation = true;
        agent.updatePosition = true;
        CharacterCollisions.ConfigurePassThroughAgent(agent);
        agent.SetDestination(reachableDestination);

        float elapsed = 0f;

        while (!HasReachedDestination(agent, reachableDestination, stoppingDistance))
        {
            elapsed += Time.deltaTime;

            if (elapsed >= DefaultTimeoutSeconds)
            {
                Debug.LogWarning($"NavMesh movement timed out for {agent.name}.", agent);
                break;
            }

            yield return null;
        }

        Stop(agent);
    }

    public static Vector3 ResolveReachablePosition(Vector3 position, float sampleRadius = 2f)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
            return hit.position;

        return position;
    }

    public static bool TryWarp(NavMeshAgent agent, Vector3 position, float sampleRadius = 2f)
    {
        if (agent == null)
            return false;

        if (NavMesh.SamplePosition(position, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            return true;
        }

        agent.transform.position = position;
        return agent.isOnNavMesh;
    }

    public static bool CanControl(NavMeshAgent agent)
    {
        return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    }

    public static void Stop(NavMeshAgent agent)
    {
        if (!CanControl(agent))
            return;

        agent.isStopped = true;
        agent.ResetPath();
        agent.velocity = Vector3.zero;
    }

    private static bool HasReachedDestination(
        NavMeshAgent agent,
        Vector3 destination,
        float stoppingDistance)
    {
        if (agent.pathPending)
            return false;

        if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
            return false;

        float arrivalDistance = Mathf.Max(stoppingDistance, 0.35f);

        if (Vector3.Distance(agent.transform.position, destination) <= arrivalDistance)
            return true;

        if (!agent.hasPath)
            return Vector3.Distance(agent.transform.position, destination) <= arrivalDistance;

        if (agent.pathStatus == NavMeshPathStatus.PathPartial && agent.velocity.sqrMagnitude <= 0.01f)
            return agent.remainingDistance <= arrivalDistance;

        if (agent.remainingDistance > stoppingDistance)
            return false;

        return agent.velocity.sqrMagnitude <= 0.01f;
    }
}
