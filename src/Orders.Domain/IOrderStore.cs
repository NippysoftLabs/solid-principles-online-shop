namespace Orders.Domain;

/// <summary>Port for wherever the shop keeps its orders.</summary>
public interface IOrderStore
{
    /// <summary>Saves the order; saving the same id again replaces it.</summary>
    void Save(Order order);
}
