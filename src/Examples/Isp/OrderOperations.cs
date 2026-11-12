namespace Examples.Isp
{
    public enum OrderStatus { Paid, Shipped, Refunded }

    /// <summary>
    /// One in-memory order book. It implements the "before" interface and the three "after" interfaces:
    /// interface segregation changes what each consumer sees, not the class behind it.
    /// </summary>
    public sealed class OrderBook : Before.IOrderOperations, After.IOrderStatusReader, After.IPickingQueue, After.IRefunds
    {
        private readonly Dictionary<string, OrderStatus> _status = new(StringComparer.Ordinal);
        private readonly List<string> _refunds = [];

        public void Add(string orderId) => _status[orderId] = OrderStatus.Paid;

        /// <summary>Throws KeyNotFoundException for an order the book does not have.</summary>
        public OrderStatus GetStatus(string orderId) => _status[orderId];

        public IReadOnlyList<string> OrdersToPick() =>
            _status.Where(p => p.Value == OrderStatus.Paid).Select(p => p.Key).Order(StringComparer.Ordinal).ToList();

        public void MarkShipped(string orderId)
        {
            if (GetStatus(orderId) != OrderStatus.Paid)
            {
                throw new InvalidOperationException($"order {orderId} is {GetStatus(orderId)}, not Paid");
            }
            _status[orderId] = OrderStatus.Shipped;
        }

        public void Refund(string orderId, decimal amount)
        {
            GetStatus(orderId);
            _status[orderId] = OrderStatus.Refunded;
            _refunds.Add($"{orderId};{amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        /// <summary>One "order;amount" line per refund, in the order they were made.</summary>
        public string ExportForAccounting() => string.Join("\n", _refunds);
    }
}

namespace Examples.Isp.Before
{
    /// <summary>
    /// BEFORE: one interface for everyone who works with orders. The tracking page calls one of its five
    /// operations, but depends on all of them: its test double has to implement all five, and a new
    /// finance operation breaks that double and makes the page rebuild. "partial" lets
    /// samples/compile-fail add a member without editing this file.
    /// </summary>
    public partial interface IOrderOperations
    {
        OrderStatus GetStatus(string orderId);          // tracking page
        IReadOnlyList<string> OrdersToPick();           // warehouse
        void MarkShipped(string orderId);               // warehouse
        void Refund(string orderId, decimal amount);    // finance
        string ExportForAccounting();                   // finance
    }

    /// <summary>The customer's "where is my order?" page.</summary>
    public sealed class TrackingPage(IOrderOperations orders)
    {
        public string Show(string orderId) => $"Order {orderId}: {orders.GetStatus(orderId)}";
    }
}

namespace Examples.Isp.After
{
    /// <summary>What the tracking page calls, and nothing else. See the Before note about "partial".</summary>
    public partial interface IOrderStatusReader
    {
        OrderStatus GetStatus(string orderId);
    }

    /// <summary>What the warehouse calls.</summary>
    public partial interface IPickingQueue
    {
        IReadOnlyList<string> OrdersToPick();

        void MarkShipped(string orderId);
    }

    /// <summary>What finance calls.</summary>
    public partial interface IRefunds
    {
        void Refund(string orderId, decimal amount);

        string ExportForAccounting();
    }

    /// <summary>The customer's "where is my order?" page. It can read a status; it has no way to refund or ship.</summary>
    public sealed class TrackingPage(IOrderStatusReader orders)
    {
        public string Show(string orderId) => $"Order {orderId}: {orders.GetStatus(orderId)}";
    }

    /// <summary>The warehouse screen: ships the first paid order.</summary>
    public sealed class PickingScreen(IPickingQueue queue)
    {
        public string? ShipNext()
        {
            var next = queue.OrdersToPick().FirstOrDefault();
            if (next is not null)
            {
                queue.MarkShipped(next);
            }
            return next;
        }
    }

    /// <summary>The finance desk: refunds and the accounting export.</summary>
    public sealed class RefundDesk(IRefunds refunds)
    {
        public string RefundAndExport(string orderId, decimal amount)
        {
            refunds.Refund(orderId, amount);
            return refunds.ExportForAccounting();
        }
    }
}
