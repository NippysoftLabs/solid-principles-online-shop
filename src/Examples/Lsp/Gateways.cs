using Orders.Domain;

namespace Examples.Lsp
{
    /// <summary>Reference implementation, used to define what "same behaviour" means. "card-declined" is declined.</summary>
    public sealed class InMemoryPaymentGateway : IPaymentGateway
    {
        private readonly Dictionary<string, PaymentResult> _byKey = new(StringComparer.Ordinal);
        private int _next;

        public PaymentResult Charge(string idempotencyKey, string cardToken, decimal amount)
        {
            PaymentInput.Check(idempotencyKey, cardToken, amount);
            if (_byKey.TryGetValue(idempotencyKey, out var first))
            {
                return first;
            }
            if (cardToken == "card-declined")
            {
                // Not stored: a declined charge does not use up the key.
                return new PaymentResult(PaymentStatus.Declined, "", 0m);
            }
            var result = new PaymentResult(PaymentStatus.Approved, $"charge-{++_next}", amount);
            _byKey[idempotencyKey] = result;
            return result;
        }
    }

    /// <summary>
    /// Stands in for a provider's SDK: amounts in whole cents, CardDeclinedException for a declined card,
    /// and an optional idempotency key. It keeps charges in memory so the examples run without a network.
    /// </summary>
    public sealed class CentsPaymentApi
    {
        private readonly Dictionary<string, CentsCharge> _byKey = new(StringComparer.Ordinal);
        private readonly List<CentsCharge> _charges = [];

        public IReadOnlyList<CentsCharge> Charges => _charges;

        public CentsCharge CreateCharge(long amountCents, string cardToken, string? idempotencyKey = null)
        {
            if (idempotencyKey is not null && _byKey.TryGetValue(idempotencyKey, out var first))
            {
                return first;
            }
            if (amountCents < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(amountCents), "amount must be at least 1 cent");
            }
            if (cardToken == "card-declined")
            {
                throw new CardDeclinedException("card declined by the issuer");
            }
            var charge = new CentsCharge($"charge-{_charges.Count + 1}", amountCents);
            _charges.Add(charge);
            if (idempotencyKey is not null)
            {
                _byKey[idempotencyKey] = charge;
            }
            return charge;
        }
    }

    public sealed record CentsCharge(string Id, long AmountCents);

    public sealed class CardDeclinedException(string message) : Exception(message);

    /// <summary>
    /// AFTER: an adapter for the cents API that keeps the contract. It checks the input with the shared
    /// rules, converts after multiplying, turns a decline into a result and passes the key on.
    /// </summary>
    public sealed class CentsGateway(CentsPaymentApi api) : IPaymentGateway
    {
        public PaymentResult Charge(string idempotencyKey, string cardToken, decimal amount)
        {
            PaymentInput.Check(idempotencyKey, cardToken, amount);
            try
            {
                // amount has at most 2 decimals and is at most 999999.99, so amount * 100 is a whole number that fits a long.
                var charge = api.CreateCharge(decimal.ToInt64(amount * 100), cardToken, idempotencyKey);
                return new PaymentResult(PaymentStatus.Approved, charge.Id, charge.AmountCents / 100m);
            }
            catch (CardDeclinedException)
            {
                return new PaymentResult(PaymentStatus.Declined, "", 0m);
            }
        }
    }
}

namespace Examples.Lsp.Before
{
    /// <summary>
    /// BEFORE: an adapter for the cents API that compiles against IPaymentGateway and passes a
    /// happy-path test with 20.00, but does not keep the contract the checkout relies on.
    /// </summary>
    public sealed class NaiveCentsGateway(CentsPaymentApi api) : IPaymentGateway
    {
        public PaymentResult Charge(string idempotencyKey, string cardToken, decimal amount)
        {
            // The cast applies to amount before the multiplication: 19.99 becomes 19, then 1900 cents.
            // The key is not passed on, and a declined card throws CardDeclinedException out of here.
            var charge = api.CreateCharge((long)amount * 100, cardToken);
            return new PaymentResult(PaymentStatus.Approved, charge.Id, charge.AmountCents / 100m);
        }
    }
}
