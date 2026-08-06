using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(115)]
public class CustomerMovement : MonoBehaviour
{
    public static CustomerMovement Instance { get; private set; }

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

    public IEnumerator MoveTo(Customer customer, Vector3 destination)
    {
        if (customer == null || customer.Locomotion == null)
            yield break;

        yield return NavMeshMovement.MoveTo(customer.Locomotion, destination);
    }
}
