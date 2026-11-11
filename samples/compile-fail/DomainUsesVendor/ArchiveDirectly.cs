using FakeCloud.Storage;

namespace Orders.Domain;

// Someone adds a shortcut inside the domain that talks to the vendor SDK.
public static class ArchiveDirectly
{
    public static void Save(Order order) => new CloudBucketClient("invoices-prod").UploadObject(order.Id, []);
}
