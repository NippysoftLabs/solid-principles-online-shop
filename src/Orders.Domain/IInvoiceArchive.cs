namespace Orders.Domain;

/// <summary>
/// Port owned by the domain. Adapters for a given cloud live in Orders.Infrastructure.
/// </summary>
public interface IInvoiceArchive
{
    /// <summary>Stores the document and returns where it was stored.</summary>
    string Save(string orderId, byte[] document);
}
