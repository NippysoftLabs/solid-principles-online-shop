using System.Text.Json;
using System.Text.Json.Serialization;

namespace Examples.Contracts.Before
{
    /// <summary>
    /// BEFORE: a consumer's copy of the event with settable properties and defaults.
    /// A field the producer renames or drops is read as its default, with no error.
    /// </summary>
    public sealed class OrderPlacedReader
    {
        public string OrderId { get; set; } = "";

        public decimal Amount { get; set; }

        public static OrderPlacedReader Read(string json) =>
            JsonSerializer.Deserialize<OrderPlacedReader>(json, JsonSerializerOptions.Web)!;
    }
}

namespace Examples.Contracts.After
{
    /// <summary>
    /// Options for every contract: constructor parameters without a default are required, a null in a
    /// non-nullable member throws, a number written as a string ("100.40") is rejected (the Web defaults
    /// accept it) and so is a property that appears twice. Unknown members are ignored, so a reader
    /// tolerates added fields. Names still match ignoring case, as in the Web defaults.
    /// </summary>
    public static class ContractJson
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerOptions.Web)
        {
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            NumberHandling = JsonNumberHandling.Strict,
            AllowDuplicateProperties = false,
        };

        public static T Read<T>(string json) =>
            JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException("message is the JSON literal null");
    }
}

namespace Examples.Contracts.After.V1_0
{
    /// <summary>Released as 1.0. Frozen: tests compare its schema with Snapshots/order-placed/1.0.schema.json.</summary>
    public sealed record OrderPlaced(string OrderId, decimal Amount);
}

namespace Examples.Contracts.After.V1_1
{
    /// <summary>Released as 1.1: adds an optional currency, which 1.0 readers ignore.</summary>
    public sealed record OrderPlaced(string OrderId, decimal Amount, string? Currency = null);
}

namespace Examples.Contracts.After.Proposals
{
    /// <summary>Proposed as "1.2": renames amount to total. The compatibility check says it is breaking.</summary>
    public sealed record OrderPlaced(string OrderId, decimal Total, string? Currency = null);
}
