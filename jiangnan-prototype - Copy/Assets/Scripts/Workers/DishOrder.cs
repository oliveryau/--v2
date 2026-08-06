using System;
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
}
