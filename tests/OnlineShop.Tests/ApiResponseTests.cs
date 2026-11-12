using System.Text.Json.Nodes;
using Examples.Contracts.After;
using Examples.Api;
using Examples.Api.After;

namespace OnlineShop.Tests;

/// <summary>
/// Complementary to ISP: one response per consumer, which is data minimization and API design. The tests check
/// response schemas; the segregation of behaviour contracts (operations) is in IspTests.
/// </summary>
public class ApiResponseTests
{
    private static readonly Product Sample = new("A1", "Desk lamp", 39.90m, 21.15m, "SUP-7", "B-12-3", 48);

    private static string[] PropertyNames(string json) =>
        JsonNode.Parse(json)!.AsObject().Select(p => p.Key).ToArray();

    private static string[] SchemaProperties(Type type) =>
        SchemaCompatibility.SchemaOf(type)["properties"]!.AsObject().Select(p => p.Key).ToArray();

    [Fact]
    public void Before_the_storefront_receives_cost_supplier_and_bin()
    {
        var json = Examples.Api.Before.ProductApi.Get(Sample);

        Assert.Equal(["sku", "name", "price", "unitCost", "supplierId", "bin", "stockOnHand"], PropertyNames(json));
    }

    [Fact]
    public void After_each_consumer_gets_only_its_fields()
    {
        Assert.Equal(["sku", "name", "price"], PropertyNames(ProductApi.GetForStorefront(Sample)));
        Assert.Equal(["sku", "bin", "stockOnHand"], PropertyNames(ProductApi.GetForWarehouse(Sample)));
    }

    // An allowlist, not a search for "cost": a new sensitive field with any name fails here.
    [Fact]
    public void After_the_storefront_contract_is_exactly_its_allowlist()
    {
        Assert.Equal(["sku", "name", "price"], SchemaProperties(typeof(StorefrontProduct)));
    }
}
