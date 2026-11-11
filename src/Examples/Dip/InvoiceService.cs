using System.Globalization;
using System.Text;
using FakeCloud.Storage;

namespace Examples.Dip.Before;

/// <summary>
/// BEFORE: the business service creates the vendor client itself and decides bucket and path.
/// Changing provider means editing this class, and a test cannot replace the vendor.
/// </summary>
public sealed class InvoiceService
{
    private readonly CloudBucketClient _bucket = new("invoices-prod");

    public string Archive(string orderId, string customer, decimal amount)
    {
        var path = $"invoices/{orderId}.txt";
        var document = $"{orderId};{customer};{amount.ToString(CultureInfo.InvariantCulture)}";
        _bucket.UploadObject(path, Encoding.UTF8.GetBytes(document));
        return $"{_bucket.Bucket}/{path}";
    }
}
