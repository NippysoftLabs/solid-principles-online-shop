using FakeCloud.Storage;
using Orders.Domain;
using Orders.Infrastructure;
using Orders.Notifications;

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

    // An allowlist: any assembly outside the framework fails, whatever its name.
    [Fact]
    public void Domain_references_only_framework_assemblies()
    {
        Assert.All(ReferencesOf(typeof(Checkout)), name =>
            Assert.True(name.StartsWith("System.", StringComparison.Ordinal), $"Orders.Domain references {name}"));
    }

    [Fact]
    public void Notifications_depend_on_checkout_and_never_the_other_way()
    {
        Assert.Contains("Orders.Domain", ReferencesOf(typeof(ConfirmationEmail)));
        Assert.DoesNotContain("Orders.Notifications", ReferencesOf(typeof(Checkout)));
    }

    [Fact]
    public void Only_infrastructure_references_the_vendor_sdk_among_the_after_projects()
    {
        Assert.Contains("FakeCloud.Storage", ReferencesOf(typeof(CloudInvoiceArchive)));
        Assert.DoesNotContain("FakeCloud.Storage", ReferencesOf(typeof(ConfirmationEmail)));
    }

    [Fact]
    public void Vendor_sdk_type_is_the_one_the_adapter_wraps()
    {
        var ctor = typeof(CloudInvoiceArchive).GetConstructors().Single();

        Assert.Equal(typeof(CloudBucketClient), ctor.GetParameters().Single().ParameterType);
    }
}
