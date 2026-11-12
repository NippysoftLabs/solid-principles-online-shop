namespace Examples.Isp;

// The tracking page's test double in the "before" design: it never refunds, and it stops compiling.
public sealed class FatTrackingDouble : Before.IOrderOperations
{
    public OrderStatus GetStatus(string orderId) => OrderStatus.Shipped;

    public IReadOnlyList<string> OrdersToPick() => throw new NotImplementedException();

    public void MarkShipped(string orderId) => throw new NotImplementedException();

    public void Refund(string orderId, decimal amount) => throw new NotImplementedException();

    public string ExportForAccounting() => throw new NotImplementedException();
}

// The tracking page's test double in the "after" design: the finance change does not reach it.
public sealed class StatusTrackingDouble : After.IOrderStatusReader
{
    public OrderStatus GetStatus(string orderId) => OrderStatus.Shipped;
}
