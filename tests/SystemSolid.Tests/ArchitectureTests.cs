using System.Reflection;
using FakeCloud.Storage;
using Orders.Domain;
using Orders.Infrastructure;
using Orders.Publishing;

namespace SystemSolid.Tests;

/// <summary>
/// The compiler stops code from using a project it does not reference. These tests stop the
/// reference itself: they read the assembly references the compiler kept, which are the ones the
/// code actually uses. A ProjectReference that nothing uses leaves no trace here.
/// </summary>
public class ArchitectureTests
{
    private static string[] ReferencesOf(Type type) =>
        type.Assembly.GetReferencedAssemblies().Select(a => a.Name!).Order().ToArray();

    [Fact]
    public void Domain_references_no_other_project_of_the_solution()
    {
        var references = ReferencesOf(typeof(CreditPolicy));

        Assert.DoesNotContain("Orders.Publishing", references);
        Assert.DoesNotContain("Orders.Infrastructure", references);
        Assert.DoesNotContain("FakeCloud.Storage", references);
    }

    [Fact]
    public void Only_infrastructure_references_the_vendor_sdk_among_the_after_projects()
    {
        Assert.Contains("FakeCloud.Storage", ReferencesOf(typeof(CloudInvoiceArchive)));
        Assert.DoesNotContain("FakeCloud.Storage", ReferencesOf(typeof(InvoicePublisher)));
    }

    [Fact]
    public void Invoice_rounding_is_not_visible_outside_publishing()
    {
        var rounding = typeof(InvoicePublisher).Assembly.GetType("Orders.Publishing.InvoiceRounding")!;

        Assert.False(rounding.IsPublic);
        Assert.Null(typeof(InvoicePublisher).Assembly.GetCustomAttribute<System.Runtime.CompilerServices.InternalsVisibleToAttribute>());
    }

    [Fact]
    public void Vendor_sdk_type_is_the_one_the_adapter_wraps()
    {
        var ctor = typeof(CloudInvoiceArchive).GetConstructors().Single();

        Assert.Equal(typeof(CloudBucketClient), ctor.GetParameters().Single().ParameterType);
    }
}
