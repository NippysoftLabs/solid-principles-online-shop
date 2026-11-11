using Examples.Srp.Before;
using Orders.Domain;
using Orders.Publishing;

namespace SystemSolid.Tests;

public class SrpTests
{
    [Fact]
    public void Before_a_finance_rounding_change_lets_an_order_over_the_credit_limit_through()
    {
        var beforeChange = new OrderPipeline(amountDecimals: 2).Process("c1,100.40", creditLimit: 100m);
        var afterChange = new OrderPipeline(amountDecimals: 0).Process("c1,100.40", creditLimit: 100m);

        Assert.False(beforeChange.Accepted);
        // Finance only wanted whole units on the invoice; the risk check changed with it.
        Assert.True(afterChange.Accepted);
        Assert.Equal("INVOICE c1 100", afterChange.Invoice);
    }

    [Fact]
    public void After_the_same_finance_change_only_changes_the_invoice()
    {
        var order = new Order("o-1", "c1", 100.40m);

        Assert.False(CreditPolicy.Accepts(order, creditLimit: 100m));
        Assert.Equal("INVOICE c1 100.40", new InvoicePublisher(decimals: 2).Format(order));
        Assert.Equal("INVOICE c1 100", new InvoicePublisher(decimals: 0).Format(order));
    }
}
