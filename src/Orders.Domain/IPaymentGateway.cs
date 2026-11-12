namespace Orders.Domain;

/// <summary>
/// Port for any payment provider. Every adapter must keep this contract, which
/// Examples.Lsp.PaymentGatewayContract checks:
/// <list type="bullet">
/// <item>The amount is in euros: greater than 0, at most 999999.99 and with at most 2 decimals.
/// Any other amount, an empty idempotency key or an empty card token throws ArgumentException
/// and charges nothing (see <see cref="PaymentInput.Check"/>).</item>
/// <item>An approved charge reports the exact amount charged.</item>
/// <item>A declined card returns <see cref="PaymentStatus.Declined"/>; it does not throw.</item>
/// <item>A second call with the same idempotency key returns the first result and charges nothing new.</item>
/// </list>
/// </summary>
public interface IPaymentGateway
{
    PaymentResult Charge(string idempotencyKey, string cardToken, decimal amount);
}

public enum PaymentStatus { Approved, Declined }

/// <summary>ChargeId is the provider's id for the charge; it is empty and Amount is 0 when the card is declined.</summary>
public sealed record PaymentResult(PaymentStatus Status, string ChargeId, decimal Amount);

/// <summary>The input rules of the contract, in one place so every adapter applies the same ones.</summary>
public static class PaymentInput
{
    public const decimal MaxAmount = 999_999.99m;

    public static void Check(string idempotencyKey, string cardToken, decimal amount)
    {
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            throw new ArgumentException("idempotency key is empty", nameof(idempotencyKey));
        }
        if (string.IsNullOrEmpty(cardToken))
        {
            throw new ArgumentException("card token is empty", nameof(cardToken));
        }
        // Euros have 2 decimals (ISO 4217 minor unit 2). 19.990 has the same value as 19.99 and passes.
        if (amount <= 0 || amount > MaxAmount || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException($"amount must be more than 0, at most {MaxAmount} and have at most 2 decimals; got {amount}", nameof(amount));
        }
    }
}
