using UnityEngine;
using UnityEngine.AI;

public static class CharacterCollisions
{
    public static void PrepareCharacter(GameObject root)
    {
        if (root == null)
            return;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        CharacterController[] controllers = root.GetComponentsInChildren<CharacterController>(true);

        for (int i = 0; i < controllers.Length; i++)
            controllers[i].enabled = false;

        NavMeshObstacle[] obstacles = root.GetComponentsInChildren<NavMeshObstacle>(true);

        for (int i = 0; i < obstacles.Length; i++)
            Object.Destroy(obstacles[i]);
    }

    public static void ConfigurePassThroughAgent(NavMeshAgent agent)
    {
        if (agent == null)
            return;

        agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
    }
}
