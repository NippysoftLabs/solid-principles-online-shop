using Examples.Lsp;
using Examples.Shop;
using Examples.Srp.Before;
using Orders.Domain;
using Orders.Notifications;

namespace OnlineShop.Tests;

public class SrpTests
{
    private static readonly Order Ana = new("o-1", "Ana", 25m);

    [Fact]
    public void Before_a_marketing_change_to_the_email_shows_an_error_after_the_card_was_charged()
    {
        var store = new InMemoryOrderStore();
        var api = new CentsPaymentApi();
        var processor = new OrderProcessor(new CentsGateway(api), store);

        // The customer sees an error page...
        Assert.Throws<ArgumentOutOfRangeException>(() => processor.Place(Ana, "card-ok"));

        // ...but was charged and the order exists. No email went out.
        Assert.Equal(2500, Assert.Single(api.Charges).AmountCents);
        Assert.Single(store.Orders);
        Assert.Empty(processor.SentEmails);
    }

    [Fact]
    public void After_checkout_charges_and_saves_without_running_any_email_code()
    {
        var store = new InMemoryOrderStore();
        var api = new CentsPaymentApi();

        Assert.Equal(CheckoutResult.Placed, new Checkout(new CentsGateway(api), store).Place(Ana, "card-ok"));
        Assert.Equal(2500, Assert.Single(api.Charges).AmountCents);
        Assert.Single(store.Orders);
    }

    [Fact]
    public void After_the_email_is_marketing_code_that_reads_a_saved_order()
    {
        Assert.Equal("Hi Ana, your order o-1 is confirmed.", ConfirmationEmail.Render(Ana));
        Assert.Equal("Hi Ana, your order o-2 is confirmed.", ConfirmationEmail.Render(new Order("o-2", "Ana Ruiz", 10m)));
    }

    [Fact]
    public void After_a_declined_card_saves_nothing()
    {
        var store = new InMemoryOrderStore();

        Assert.Equal(CheckoutResult.Declined, new Checkout(new InMemoryPaymentGateway(), store).Place(Ana, "card-declined"));
        Assert.Empty(store.Orders);
    }
}
