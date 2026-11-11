using System.Globalization;
using Orders.Domain;

namespace Orders.Publishing;

/// <summary>Invoice text owned by the finance team.</summary>
public sealed class InvoicePublisher(int decimals)
{
    public string Format(Order order) =>
        $"INVOICE {order.Customer} {InvoiceRounding.Round(order.Amount, decimals).ToString(CultureInfo.InvariantCulture)}";
}

// internal: no other project can call it, so a finance change here cannot reach the credit rule.
internal static class InvoiceRounding
{
    internal static decimal Round(decimal value, int decimals) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);
}
