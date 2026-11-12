namespace Orders.Domain;

/// <summary>
/// Port declared by the business policy, in its own words: "keep this invoice for this order".
/// It says nothing about buckets, paths or vendors. Orders.Infrastructure implements it for one cloud;
/// the dependency points from the infrastructure to the domain, never back.
/// </summary>
public interface IInvoiceArchive
{
    /// <summary>Stores the document and returns where it was stored.</summary>
    string Save(string orderId, byte[] document);
}
