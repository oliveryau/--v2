using UnityEngine;

public sealed class DishOrder
{
    public DishOrder(Customer customer)
    {
        Customer = customer;
    }

    public Customer Customer { get; }
    public bool IsReady { get; private set; }
    public bool IsDelivered { get; private set; }
    public bool IsCancelled { get; private set; }
    /// <summary>
    /// When true, chefs may cook this order but waiters will not deliver until released.
    /// Used so VIP food is prepped on seat-down and only collected after 上菜.
    /// </summary>
    public bool AwaitsManualServeRelease { get; private set; }

    public void MarkReady()
    {
        IsReady = true;
    }

    public void MarkDelivered()
    {
        IsDelivered = true;
    }

    public void Cancel()
    {
        IsCancelled = true;
    }

    public void MarkAwaitingManualServe()
    {
        AwaitsManualServeRelease = true;
    }

    public void ReleaseForServe()
    {
        AwaitsManualServeRelease = false;
    }
}
