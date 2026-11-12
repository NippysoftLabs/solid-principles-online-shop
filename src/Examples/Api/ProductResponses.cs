using System.Text.Json;

namespace Examples.Api
{
    /// <summary>The catalog's internal model. Cost and supplier are for purchasing, bin and stock for the warehouse.</summary>
    public sealed record Product(
        string Sku, string Name, decimal Price, decimal UnitCost, string SupplierId, string Bin, int StockOnHand);

    public static class ApiJson
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerOptions.Web)
        {
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
        };
    }
}

namespace Examples.Api.Before
{
    /// <summary>BEFORE: one endpoint for every consumer. The storefront receives cost, supplier and bin.</summary>
    public static class ProductApi
    {
        public static string Get(Product product) => JsonSerializer.Serialize(product, ApiJson.Options);
    }
}

namespace Examples.Api.After
{
    /// <summary>What the storefront needs, and nothing else.</summary>
    public sealed record StorefrontProduct(string Sku, string Name, decimal Price)
    {
        public static StorefrontProduct From(Product p) => new(p.Sku, p.Name, p.Price);
    }

    /// <summary>What the warehouse needs, and nothing else.</summary>
    public sealed record WarehouseProduct(string Sku, string Bin, int StockOnHand)
    {
        public static WarehouseProduct From(Product p) => new(p.Sku, p.Bin, p.StockOnHand);
    }

    public static class ProductApi
    {
        public static string GetForStorefront(Product product) =>
            JsonSerializer.Serialize(StorefrontProduct.From(product), ApiJson.Options);

        public static string GetForWarehouse(Product product) =>
            JsonSerializer.Serialize(WarehouseProduct.From(product), ApiJson.Options);
    }
}
