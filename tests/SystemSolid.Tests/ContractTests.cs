using System.Text.Json;
using System.Text.Json.Nodes;
using Examples.Contracts.After;
using Examples.Contracts.Before;

namespace SystemSolid.Tests;

public class ContractTests
{
    private const string V11Message = """{"orderId":"o-1","amount":100.40,"currency":"EUR"}""";
    private const string RenamedMessage = """{"orderId":"o-1","total":100.40}""";

    // Released versions, oldest first. A new release adds a line here and a frozen schema file.
    private static readonly (string Version, Type Type)[] Releases =
    [
        ("1.0", typeof(Examples.Contracts.After.V1_0.OrderPlaced)),
        ("1.1", typeof(Examples.Contracts.After.V1_1.OrderPlaced)),
    ];

    private static JsonNode Frozen(string version) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Snapshots", "order-placed", $"{version}.schema.json")))!;

    [Fact]
    public void Before_a_renamed_field_is_read_as_zero_without_an_error()
    {
        var order = OrderPlacedReader.Read(RenamedMessage);

        Assert.Equal("o-1", order.OrderId);
        Assert.Equal(0m, order.Amount);
    }

    [Fact]
    public void After_a_renamed_field_is_rejected_when_the_message_is_read()
    {
        var ex = Assert.Throws<JsonException>(() => ContractJson.Read<Examples.Contracts.After.V1_1.OrderPlaced>(RenamedMessage));

        Assert.Contains("amount", ex.Message);
    }

    [Fact]
    public void After_an_old_reader_tolerates_a_field_added_in_a_minor_version()
    {
        var order = ContractJson.Read<Examples.Contracts.After.V1_0.OrderPlaced>(V11Message);

        Assert.Equal(new Examples.Contracts.After.V1_0.OrderPlaced("o-1", 100.40m), order);
    }

    [Fact]
    public void Released_types_still_produce_their_frozen_schema()
    {
        foreach (var (version, type) in Releases)
        {
            var current = SchemaCompatibility.SchemaOf(type);
            Assert.True(JsonNode.DeepEquals(Frozen(version), current),
                $"{type.FullName} no longer matches Snapshots/order-placed/{version}.schema.json; a released version is frozen.\n{current.ToJsonString()}");
        }
    }

    [Fact]
    public void Each_release_is_compatible_with_the_previous_one_or_bumps_the_major()
    {
        for (var i = 1; i < Releases.Length; i++)
        {
            var breaking = SchemaCompatibility.BreakingChanges(Frozen(Releases[i - 1].Version), Frozen(Releases[i].Version));
            Assert.Null(SchemaCompatibility.CheckVersionBump(Releases[i - 1].Version, Releases[i].Version, breaking));
        }
    }

    public enum StatusV1 { New, Paid }

    public enum StatusV2 { New, Paid, Refunded }

    public sealed record OrderStatusV1(string OrderId, StatusV1 Status);

    public sealed record OrderStatusV2(string OrderId, StatusV2 Status);

    [Fact]
    public void A_new_enum_member_is_reported_and_an_old_reader_rejects_it()
    {
        var breaking = SchemaCompatibility.BreakingChanges(
            SchemaCompatibility.SchemaOf(typeof(OrderStatusV1)), SchemaCompatibility.SchemaOf(typeof(OrderStatusV2)));

        Assert.Equal(["$.status: new enum value \"Refunded\""], breaking);
        Assert.Throws<JsonException>(() => ContractJson.Read<OrderStatusV1>("""{"orderId":"o-1","status":"Refunded"}"""));
        // Integers are not accepted either: 2 would otherwise be read as an undefined StatusV1.
        Assert.Throws<JsonException>(() => ContractJson.Read<OrderStatusV1>("""{"orderId":"o-1","status":2}"""));
    }

    [Fact]
    public void Renaming_amount_cannot_ship_as_a_minor_version()
    {
        var breaking = SchemaCompatibility.BreakingChanges(
            Frozen("1.1"), SchemaCompatibility.SchemaOf(typeof(Examples.Contracts.After.Proposals.OrderPlaced)));

        Assert.Equal(["$.amount: required property removed"], breaking);
        Assert.Equal("1 breaking change(s) need version 2.0, not 1.2", SchemaCompatibility.CheckVersionBump("1.1", "1.2", breaking));
        Assert.Null(SchemaCompatibility.CheckVersionBump("1.1", "2.0", breaking));
    }
}
