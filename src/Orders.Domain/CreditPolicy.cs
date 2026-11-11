namespace Orders.Domain;

/// <summary>Credit rule owned by the risk team.</summary>
public static class CreditPolicy
{
    // Compares the exact amount. How an invoice rounds it is decided elsewhere.
    public static bool Accepts(Order order, decimal creditLimit) => order.Amount <= creditLimit;
}
