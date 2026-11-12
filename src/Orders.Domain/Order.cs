namespace Orders.Domain;

/// <summary>An order in the online shop. Total is in euros.</summary>
public sealed record Order(string Id, string CustomerName, decimal Total);
