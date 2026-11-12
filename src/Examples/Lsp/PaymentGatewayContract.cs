using Orders.Domain;

namespace Examples.Lsp;

/// <summary>
/// One contract suite for every IPaymentGateway. Each check gets a fresh gateway from the factory.
/// A gateway that passes behaves like InMemoryPaymentGateway on these inputs; inputs not listed here are not checked.
/// </summary>
public static class PaymentGatewayContract
{
    private static readonly (string Name, Func<IPaymentGateway, bool> Holds)[] Checks =
    [
        ("approved card is charged the exact amount", g => g.Charge("k1", "card-ok", 19.99m) is { Status: PaymentStatus.Approved, Amount: 19.99m }),
        ("amount under one euro", g => g.Charge("k1", "card-ok", 0.05m) is { Status: PaymentStatus.Approved, Amount: 0.05m }),
        ("largest allowed amount", g => g.Charge("k1", "card-ok", PaymentInput.MaxAmount).Amount == PaymentInput.MaxAmount),
        ("declined card returns Declined", g => g.Charge("k1", "card-declined", 10m) is { Status: PaymentStatus.Declined, Amount: 0m }),
        ("after a decline the same key can be charged with another card", g =>
        {
            g.Charge("k1", "card-declined", 10m);
            return g.Charge("k1", "card-ok", 10m) is { Status: PaymentStatus.Approved, Amount: 10m };
        }),
        ("same key charges once", g => g.Charge("k1", "card-ok", 10m).ChargeId == g.Charge("k1", "card-ok", 10m).ChargeId),
        ("different keys are different charges", g => g.Charge("k1", "card-ok", 10m).ChargeId != g.Charge("k2", "card-ok", 10m).ChargeId),
        ("zero amount throws ArgumentException", g => Throws<ArgumentException>(() => g.Charge("k1", "card-ok", 0m))),
        ("negative amount throws ArgumentException", g => Throws<ArgumentException>(() => g.Charge("k1", "card-ok", -5m))),
        ("three decimals throws ArgumentException", g => Throws<ArgumentException>(() => g.Charge("k1", "card-ok", 19.999m))),
        ("amount over the limit throws ArgumentException", g => Throws<ArgumentException>(() => g.Charge("k1", "card-ok", 1_000_000m))),
        ("empty key throws ArgumentException", g => Throws<ArgumentException>(() => g.Charge("", "card-ok", 10m))),
        ("empty card token throws ArgumentException", g => Throws<ArgumentException>(() => g.Charge("k1", "", 10m))),
    ];

    public static int CheckCount => Checks.Length;

    public static IReadOnlyList<string> Verify(Func<IPaymentGateway> create)
    {
        var violations = new List<string>();
        foreach (var (name, holds) in Checks)
        {
            try
            {
                if (!holds(create()))
                {
                    violations.Add($"{name}: wrong result");
                }
            }
            catch (Exception ex)
            {
                violations.Add($"{name}: threw {ex.GetType().Name}: {ex.Message}");
            }
        }
        return violations;
    }

    // T or a type derived from it: that is what a caller's catch (T) handles. Any other type escapes it.
    private static bool Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (Exception ex)
        {
            return ex is T;
        }
    }
}
