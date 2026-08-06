using System;
using System.Collections;
using UnityEngine;

public static class PathMovement
{
    public static Vector3[] BuildCheckpoints(Transform[] checkpoints, float floorY)
    {
        if (checkpoints == null || checkpoints.Length == 0)
            return Array.Empty<Vector3>();

        int count = 0;

        for (int i = 0; i < checkpoints.Length; i++)
        {
            if (checkpoints[i] != null)
                count++;
        }

        if (count == 0)
            return Array.Empty<Vector3>();

        Vector3[] result = new Vector3[count];
        int index = 0;

        for (int i = 0; i < checkpoints.Length; i++)
        {
            if (checkpoints[i] == null)
                continue;

            result[index++] = FlattenToFloorY(checkpoints[i].position, floorY);
        }

        return result;
    }

    public static Vector3[] BuildWaypoints(Vector3[] checkpoints, Vector3 finalPosition)
    {
        Vector3[] waypoints = new Vector3[checkpoints.Length + 1];

        for (int i = 0; i < checkpoints.Length; i++)
            waypoints[i] = checkpoints[i];

        waypoints[checkpoints.Length] = finalPosition;
        return waypoints;
    }

    public static float GetDistance(Vector3 startPosition, Vector3[] path)
    {
        if (path.Length == 0)
            return 0f;

        float distance = Vector3.Distance(startPosition, path[0]);

        for (int i = 1; i < path.Length; i++)
            distance += Vector3.Distance(path[i - 1], path[i]);

        return distance;
    }

    public static IEnumerator Move(
        Transform target,
        Vector3 startPosition,
        Vector3[] waypoints,
        float totalDuration,
        Action<Vector3> onFaceDirection = null)
    {
        float totalDistance = GetDistance(startPosition, waypoints);
        Vector3 previousPosition = startPosition;

        for (int i = 0; i < waypoints.Length; i++)
        {
            Vector3 nextPosition = waypoints[i];
            float segmentDistance = Vector3.Distance(previousPosition, nextPosition);
            float segmentDuration = totalDistance > 0f
                ? totalDuration * (segmentDistance / totalDistance)
                : totalDuration / waypoints.Length;
            segmentDuration = Mathf.Max(0.01f, segmentDuration);

            Vector3 segmentDirection = nextPosition - previousPosition;
            float elapsed = 0f;

            while (elapsed < segmentDuration)
            {
                elapsed += Time.deltaTime;
                target.position = Vector3.Lerp(previousPosition, nextPosition, elapsed / segmentDuration);
                onFaceDirection?.Invoke(segmentDirection);
                yield return null;
            }

            target.position = nextPosition;
            onFaceDirection?.Invoke(segmentDirection);
            previousPosition = nextPosition;
        }
    }

    public static Vector3 FlattenToFloorY(Vector3 position, float floorY)
    {
        position.y = floorY;
        return position;
    }

    public static void ClearStaticForRuntimeMove(Transform root)
    {
        RuntimeMeshVisibility.Prepare(root);
    }
}
