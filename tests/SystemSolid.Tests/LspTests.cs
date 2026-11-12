using Examples.Lsp;
using Examples.Lsp.Before;
using Examples.Shop;
using Orders.Domain;

namespace SystemSolid.Tests;

public class LspTests
{
    [Fact]
    public void Reference_gateway_keeps_the_whole_contract()
    {
        Assert.Empty(PaymentGatewayContract.Verify(() => new InMemoryPaymentGateway()));
    }

    [Fact]
    public void Before_the_naive_adapter_passes_the_happy_path_but_breaks_the_contract()
    {
        var naive = new NaiveCentsGateway(new CentsPaymentApi());
        Assert.Equal(new PaymentResult(PaymentStatus.Approved, "charge-1", 20m), naive.Charge("o-1", "card-ok", 20m));

        var violations = PaymentGatewayContract.Verify(() => new NaiveCentsGateway(new CentsPaymentApi()));

        Assert.Equal(13, PaymentGatewayContract.CheckCount);
        Assert.Equal(
        [
            "approved card is charged the exact amount: wrong result",
            "amount under one euro: threw ArgumentOutOfRangeException: amount must be at least 1 cent (Parameter 'amountCents')",
            "largest allowed amount: wrong result",
            "declined card returns Declined: threw CardDeclinedException: card declined by the issuer",
            "after a decline the same key can be charged with another card: threw CardDeclinedException: card declined by the issuer",
            "same key charges once: wrong result",
            "three decimals throws ArgumentException: wrong result",
            "amount over the limit throws ArgumentException: wrong result",
            "empty key throws ArgumentException: wrong result",
            "empty card token throws ArgumentException: wrong result",
        ], violations);
    }

    [Fact]
    public void Before_the_same_checkout_with_the_naive_adapter_charges_19_for_19_99_and_crashes_on_a_decline()
    {
        var api = new CentsPaymentApi();
        var checkout = new Checkout(new NaiveCentsGateway(api), new InMemoryOrderStore());

        Assert.True(checkout.Place(new Order("o-1", "Ana", 19.99m), "card-ok"));
        Assert.Equal(1900, Assert.Single(api.Charges).AmountCents);
        Assert.Throws<CardDeclinedException>(() => checkout.Place(new Order("o-2", "Ana", 10m), "card-declined"));
    }

    [Fact]
    public void After_the_fixed_adapter_keeps_the_whole_contract()
    {
        Assert.Empty(PaymentGatewayContract.Verify(() => new CentsGateway(new CentsPaymentApi())));
    }

    [Fact]
    public void After_a_declined_order_can_be_paid_with_another_card()
    {
        foreach (var gateway in new IPaymentGateway[] { new InMemoryPaymentGateway(), new CentsGateway(new CentsPaymentApi()) })
        {
            var checkout = new Checkout(gateway, new InMemoryOrderStore());
            var order = new Order("o-1", "Ana", 19.99m);

            Assert.False(checkout.Place(order, "card-declined"));
            Assert.True(checkout.Place(order, "card-ok"));
        }
    }

    [Fact]
    public void After_a_retried_order_is_charged_once()
    {
        var api = new CentsPaymentApi();
        var checkout = new Checkout(new CentsGateway(api), new InMemoryOrderStore());
        var order = new Order("o-1", "Ana", 19.99m);

        Assert.True(checkout.Place(order, "card-ok"));
        Assert.True(checkout.Place(order, "card-ok"));
        Assert.Equal(1999, Assert.Single(api.Charges).AmountCents);
    }
}
