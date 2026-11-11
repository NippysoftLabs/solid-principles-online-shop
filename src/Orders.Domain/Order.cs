namespace Orders.Domain;

public sealed record Order(string Id, string Customer, decimal Amount);
