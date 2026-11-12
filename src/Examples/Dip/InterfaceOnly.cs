using System.Globalization;
using System.Text;
using FakeCloud.Storage;

namespace Examples.Dip.InterfaceOnly;

/// <summary>
/// NOT dependency inversion, although it has an interface and takes it through the constructor:
/// the interface is the vendor's (it lives in FakeCloud.Storage) and the business code still decides
/// buckets and object paths. Changing to a provider shaped differently still means editing this class.
/// </summary>
public sealed class InvoiceService(ICloudBucket bucket)
{
    public string Archive(string orderId, string customer, decimal amount)
    {
        var path = $"invoices/{orderId}.txt";
        var document = $"{orderId};{customer};{amount.ToString(CultureInfo.InvariantCulture)}";
        bucket.UploadObject(path, Encoding.UTF8.GetBytes(document));
        return $"{bucket.Bucket}/{path}";
    }
}
