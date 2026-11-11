using Orders.Publishing;

namespace Samples;

// The risk team tries to reuse the finance team's rounding.
public static class CreditCheck
{
    public static bool Accepts(decimal amount, decimal limit) => InvoiceRounding.Round(amount, 0) <= limit;
}
