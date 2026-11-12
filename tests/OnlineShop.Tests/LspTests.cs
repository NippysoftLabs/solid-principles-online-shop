using Examples.Lsp;
using Examples.Lsp.Before;
using Examples.Shop;
using Orders.Domain;

namespace OnlineShop.Tests;

/// <summary>
/// The contract suite checks each adapter against the provider stand-in in this repository. It proves
/// that the adapter keeps the contract when the provider behaves like the stand-in; it does not prove
/// that a real provider behaves like it. That needs the same suite against the provider's test environment.
/// </summary>
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

        Assert.Equal(15, PaymentGatewayContract.CheckCount);
        Assert.Equal(
        [
            "approved card is charged the exact amount: wrong result",
            "amount under one euro: threw ArgumentOutOfRangeException: amount must be at least 1 cent (Parameter 'amountCents')",
            "largest allowed amount: wrong result",
            "declined card returns Declined: threw CardDeclinedException: card declined by the issuer",
            "after a decline the same key can be charged with another card: threw CardDeclinedException: card declined by the issuer",
            "no answer from the provider returns Unknown, not Declined: threw TimeoutException: the provider did not answer in time",
            "after an Unknown result, a retry with the same key returns the charge that happened: threw TimeoutException: the provider did not answer in time",
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

        Assert.Equal(CheckoutResult.Placed, checkout.Place(new Order("o-1", "Ana", 19.99m), "card-ok"));
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

            Assert.Equal(CheckoutResult.Declined, checkout.Place(order, "card-declined"));
            Assert.Equal(CheckoutResult.Placed, checkout.Place(order, "card-ok"));
        }
    }

    [Fact]
    public void After_a_lost_answer_is_unknown_not_declined_and_the_retry_charges_once()
    {
        var api = new CentsPaymentApi();
        var store = new InMemoryOrderStore();
        var checkout = new Checkout(new CentsGateway(api), store);
        var order = new Order("o-1", "Ana", 19.99m);

        // The provider charged the card, but its answer never arrived.
        Assert.Equal(CheckoutResult.PaymentUnknown, checkout.Place(order, "card-lost-response"));
        Assert.Single(api.Charges);
        Assert.Empty(store.Orders);

        // Retrying the same order reuses its key: the provider returns the charge it already made.
        Assert.Equal(CheckoutResult.Placed, checkout.Place(order, "card-lost-response"));
        Assert.Equal(1999, Assert.Single(api.Charges).AmountCents);
        Assert.Single(store.Orders);
    }

    [Fact]
    public void Before_a_lost_answer_escapes_as_an_exception_and_the_retry_charges_twice()
    {
        var api = new CentsPaymentApi();
        var checkout = new Checkout(new NaiveCentsGateway(api), new InMemoryOrderStore());
        var order = new Order("o-1", "Ana", 20m);

        Assert.Throws<TimeoutException>(() => checkout.Place(order, "card-lost-response"));
        Assert.Throws<TimeoutException>(() => checkout.Place(order, "card-lost-response"));
        Assert.Equal(2, api.Charges.Count);
    }

    [Fact]
    public void After_a_retried_order_is_charged_once()
    {
        var api = new CentsPaymentApi();
        var checkout = new Checkout(new CentsGateway(api), new InMemoryOrderStore());
        var order = new Order("o-1", "Ana", 19.99m);

        Assert.Equal(CheckoutResult.Placed, checkout.Place(order, "card-ok"));
        Assert.Equal(CheckoutResult.Placed, checkout.Place(order, "card-ok"));
        Assert.Equal(1999, Assert.Single(api.Charges).AmountCents);
    }
}
