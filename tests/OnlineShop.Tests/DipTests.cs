using System.Text;
using FakeCloud.Storage;
using Orders.Domain;
using Orders.Infrastructure;

namespace OnlineShop.Tests;

public class DipTests
{
    [Fact]
    public void Before_the_business_service_builds_the_vendor_client_and_cannot_take_another()
    {
        var service = typeof(Examples.Dip.Before.InvoiceService);

        Assert.Contains("FakeCloud.Storage", service.Assembly.GetReferencedAssemblies().Select(a => a.Name));
        // The only constructor takes nothing: there is no seam to pass another provider or a test double.
        Assert.Empty(service.GetConstructors().Single().GetParameters());
        Assert.Equal("invoices-prod/invoices/o-1.txt", new Examples.Dip.Before.InvoiceService().Archive("o-1", "c1", 10m));
    }

    [Fact]
    public void An_interface_and_constructor_injection_alone_still_depend_on_the_vendor()
    {
        var service = typeof(Examples.Dip.InterfaceOnly.InvoiceService);
        var dependency = service.GetConstructors().Single().GetParameters().Single().ParameterType;

        // It can take a test double...
        Assert.True(dependency.IsInterface);
        // ...but the abstraction belongs to the vendor and speaks its language.
        Assert.Equal("FakeCloud.Storage", dependency.Assembly.GetName().Name);
        Assert.Equal(["UploadObject", "get_Bucket"], dependency.GetMethods().Select(m => m.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void After_the_port_is_declared_by_the_domain_in_its_own_terms()
    {
        var port = typeof(IInvoiceArchive);
        var types = port.GetMethods().SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType));

        Assert.Equal(typeof(InvoiceService).Assembly, port.Assembly);
        Assert.All(types, t => Assert.True(t.Namespace == "System", $"{t} is not a framework type"));
        // The adapter is in another assembly and points at the domain.
        Assert.NotEqual(port.Assembly, typeof(CloudInvoiceArchive).Assembly);
        Assert.Contains("Orders.Domain", typeof(CloudInvoiceArchive).Assembly.GetReferencedAssemblies().Select(a => a.Name));
    }

    [Fact]
    public void After_the_domain_runs_with_any_archive_that_implements_its_port()
    {
        var memory = new MemoryArchive();
        var service = new InvoiceService(memory);

        Assert.Equal("memory:o-1", service.Archive(new Order("o-1", "c1", 10.5m)));
        Assert.Equal("o-1;c1;10.5", Encoding.UTF8.GetString(memory.Saved["o-1"]));
    }

    [Fact]
    public void After_the_cloud_adapter_keeps_an_id_with_slashes_in_one_path_segment()
    {
        var bucket = new CloudBucketClient("invoices-prod");
        var service = new InvoiceService(new CloudInvoiceArchive(bucket));

        Assert.Equal("invoices-prod/invoices/..%2Fo-1.txt", service.Archive(new Order("../o-1", "c1", 1m)));
        Assert.NotNull(bucket.DownloadObject("invoices/..%2Fo-1.txt"));
    }

    [Fact]
    public void After_the_cloud_adapter_rejects_ids_with_a_lone_surrogate()
    {
        var bucket = new CloudBucketClient("invoices-prod");
        var archive = new CloudInvoiceArchive(bucket);

        // Escaped, both ids would become "o%EF%BF%BD" and share one object.
        Assert.Equal("o%EF%BF%BD", Uri.EscapeDataString("o\uD800"));
        Assert.Equal("o%EF%BF%BD", Uri.EscapeDataString("o\uDBFF"));
        Assert.Throws<ArgumentException>(() => archive.Save("o\uD800", [1]));
        Assert.Throws<ArgumentException>(() => archive.Save("o\uDBFF", [2]));
        Assert.Equal("invoices-prod/invoices/o%F0%9F%93%A6.txt", archive.Save("o\uD83D\uDCE6", [3]));
    }

    private sealed class MemoryArchive : IInvoiceArchive
    {
        public Dictionary<string, byte[]> Saved { get; } = [];

        public string Save(string orderId, byte[] document)
        {
            Saved[orderId] = document;
            return $"memory:{orderId}";
        }
    }
}
