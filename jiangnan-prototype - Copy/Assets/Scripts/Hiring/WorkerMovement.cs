using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(105)]
public class WorkerMovement : MonoBehaviour
{
    public static WorkerMovement Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void BeginWalkIn(HireSpot spot, Action onComplete)
    {
        StartCoroutine(WalkInRoutine(spot, onComplete));
    }

    public IEnumerator MoveTo(Worker worker, Vector3 destination)
    {
        if (worker == null || worker.Locomotion == null)
            yield break;

        yield return NavMeshMovement.MoveTo(worker.Locomotion, destination);
    }

    private IEnumerator WalkInRoutine(HireSpot spot, Action onComplete)
    {
        GameObject[] workers = spot.Workers;

        if (workers == null || workers.Length == 0)
        {
            onComplete?.Invoke();
            yield break;
        }

        Vector3 spawnPosition = ResolveWalkInSpawnPosition(spot);
        float spawnInterval = Mathf.Max(0f, spot.WalkInSpawnInterval);

        Vector3[] targetPositions = new Vector3[workers.Length];
        Quaternion[] targetRotations = new Quaternion[workers.Length];
        spot.AssignWalkInTargets(spawnPosition.y, targetPositions, targetRotations);

        int pendingWorkerMoves = 0;
        int spawnedCount = 0;

        for (int i = 0; i < workers.Length; i++)
        {
            if (workers[i] == null)
                continue;

            Worker worker = workers[i].GetComponent<Worker>();

            if (worker == null || worker.Locomotion == null)
            {
                Debug.LogWarning($"WorkerMovement requires a Worker with NavMeshLocomotion on {workers[i].name}.", workers[i]);
                continue;
            }

            if (spawnedCount > 0)
                yield return new WaitForSeconds(spawnInterval);

            PrepareWorkerForWalkIn(worker, spawnPosition);

            Vector3[] waypoints = PathMovement.BuildWaypoints(System.Array.Empty<Vector3>(), targetPositions[i]);
            Quaternion endpointRotation = targetRotations[i];

            pendingWorkerMoves++;
            spawnedCount++;
            StartCoroutine(MoveWorker(worker, waypoints, endpointRotation, () => pendingWorkerMoves--));
        }

        while (pendingWorkerMoves > 0)
            yield return null;

        onComplete?.Invoke();
    }

    private IEnumerator MoveWorker(Worker worker, Vector3[] waypoints, Quaternion endpointRotation, Action onComplete)
    {
        yield return NavMeshMovement.MoveAlong(worker.Locomotion, waypoints);
        worker.FaceDirection(endpointRotation);
        worker.Locomotion.EnterStationary();
        onComplete?.Invoke();
    }

    private static void PrepareWorkerForWalkIn(Worker worker, Vector3 spawnPosition)
    {
        worker.gameObject.SetActive(false);
        worker.WarpTo(spawnPosition);
        worker.gameObject.SetActive(true);
    }

    private static Vector3 ResolveWalkInSpawnPosition(HireSpot spot)
    {
        if (spot == null)
            return Vector3.zero;

        return spot.WalkInSpawn != null
            ? spot.WalkInSpawn.position
            : spot.transform.position;
    }
}
