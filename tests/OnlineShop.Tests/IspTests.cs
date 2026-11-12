using Examples.Isp;
using Examples.Isp.After;
using Examples.Isp.Before;

namespace OnlineShop.Tests;

/// <summary>
/// Interface segregation between behaviour contracts: which operations each consumer depends on.
/// The response-schema tests (ApiResponseTests) are a complementary example of API design.
/// </summary>
public class IspTests
{
    private static string[] OperationsOf(Type consumer) =>
        consumer.GetConstructors().Single().GetParameters().Single().ParameterType
            .GetMethods().Select(m => m.Name).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public void Before_the_tracking_page_depends_on_five_operations_and_calls_one()
    {
        Assert.Equal(
            ["ExportForAccounting", "GetStatus", "MarkShipped", "OrdersToPick", "Refund"],
            OperationsOf(typeof(Examples.Isp.Before.TrackingPage)));

        // Its test double must implement all five; four of them only throw.
        var page = new Examples.Isp.Before.TrackingPage(new FatDouble());
        Assert.Equal("Order o-1: Shipped", page.Show("o-1"));
        Assert.Throws<NotImplementedException>(() => new FatDouble().Refund("o-1", 1m));
    }

    [Fact]
    public void Before_what_the_tracking_page_receives_can_refund_and_ship()
    {
        var book = new OrderBook();
        book.Add("o-1");
        IOrderOperations seenByThePage = book;

        // Nothing in the page's dependency stops a bug in the page from refunding an order.
        seenByThePage.Refund("o-1", 25m);

        Assert.Equal("Order o-1: Refunded", new Examples.Isp.Before.TrackingPage(book).Show("o-1"));
    }

    [Fact]
    public void After_each_consumer_depends_only_on_the_operations_it_calls()
    {
        Assert.Equal(["GetStatus"], OperationsOf(typeof(Examples.Isp.After.TrackingPage)));
        Assert.Equal(["MarkShipped", "OrdersToPick"], OperationsOf(typeof(PickingScreen)));
        Assert.Equal(["ExportForAccounting", "Refund"], OperationsOf(typeof(RefundDesk)));
        Assert.False(typeof(IRefunds).IsAssignableFrom(typeof(IOrderStatusReader)));
    }

    [Fact]
    public void After_the_tracking_page_runs_with_a_one_method_double()
    {
        var page = new Examples.Isp.After.TrackingPage(new StatusDouble());

        Assert.Equal("Order o-1: Shipped", page.Show("o-1"));
    }

    [Fact]
    public void After_one_order_book_still_serves_every_consumer()
    {
        var book = new OrderBook();
        book.Add("o-1");
        book.Add("o-2");
        var tracking = new Examples.Isp.After.TrackingPage(book);

        Assert.Equal("o-1", new PickingScreen(book).ShipNext());
        Assert.Equal("o-2;25", new RefundDesk(book).RefundAndExport("o-2", 25m));
        Assert.Equal("Order o-1: Shipped", tracking.Show("o-1"));
        Assert.Equal("Order o-2: Refunded", tracking.Show("o-2"));
    }

    private sealed class FatDouble : IOrderOperations
    {
        public OrderStatus GetStatus(string orderId) => OrderStatus.Shipped;

        public IReadOnlyList<string> OrdersToPick() => throw new NotImplementedException();

        public void MarkShipped(string orderId) => throw new NotImplementedException();

        public void Refund(string orderId, decimal amount) => throw new NotImplementedException();

        public string ExportForAccounting() => throw new NotImplementedException();
    }

    private sealed class StatusDouble : IOrderStatusReader
    {
        public OrderStatus GetStatus(string orderId) => OrderStatus.Shipped;
    }
}
