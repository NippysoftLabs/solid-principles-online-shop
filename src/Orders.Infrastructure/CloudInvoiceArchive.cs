using FakeCloud.Storage;
using Orders.Domain;

namespace Orders.Infrastructure;

/// <summary>Adapter: implements the domain's port with the vendor SDK.</summary>
public sealed class CloudInvoiceArchive(CloudBucketClient client) : IInvoiceArchive
{
    public string Save(string orderId, byte[] document)
    {
        // Escaped so an id such as "../x" stays one path segment.
        var path = $"invoices/{Uri.EscapeDataString(orderId)}.txt";
        client.UploadObject(path, document);
        return $"{client.Bucket}/{path}";
    }
}
