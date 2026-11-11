using FakeCloud.Storage;
using Orders.Domain;

namespace Orders.Infrastructure;

/// <summary>Adapter: implements the domain's port with the vendor SDK.</summary>
public sealed class CloudInvoiceArchive(CloudBucketClient client) : IInvoiceArchive
{
    public string Save(string orderId, byte[] document)
    {
        // Uri.EscapeDataString turns a lone surrogate into U+FFFD (%EF%BF%BD), so two different ids
        // would name the same object and the second invoice would replace the first.
        if (!IsValidUtf16(orderId))
        {
            throw new ArgumentException("order id is not valid UTF-16 (it has a lone surrogate)", nameof(orderId));
        }
        // Escaped so an id such as "../x" stays one path segment.
        var path = $"invoices/{Uri.EscapeDataString(orderId)}.txt";
        client.UploadObject(path, document);
        return $"{client.Bucket}/{path}";
    }

    private static bool IsValidUtf16(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(value[i]))
            {
                return false;
            }
        }
        return true;
    }
}
