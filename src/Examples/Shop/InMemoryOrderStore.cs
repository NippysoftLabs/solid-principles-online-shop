using Orders.Domain;

namespace Examples.Shop;

/// <summary>Keeps orders in memory, by id, so the examples run without a database.</summary>
public sealed class InMemoryOrderStore : IOrderStore
{
    private readonly Dictionary<string, Order> _orders = new(StringComparer.Ordinal);

    public IReadOnlyCollection<Order> Orders => _orders.Values;

    public void Save(Order order) => _orders[order.Id] = order;
}
