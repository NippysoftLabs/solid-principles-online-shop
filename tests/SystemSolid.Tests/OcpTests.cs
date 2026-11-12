using System.Text.Json;
using Examples.Ocp.After;
using Examples.Ocp.Before;

namespace SystemSolid.Tests;

public class OcpTests
{
    private static readonly IShippingRule[] Rules = [new FlatRule(), new FreeOverRule(), new FreeRule()];

    private static ShippingCatalog CheckedIn() =>
        new(Rules, ShippingCatalog.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config", "shipping.json"))));

    [Fact]
    public void Before_store_pickup_is_charged_as_standard_shipping()
    {
        Assert.Equal(4.99m, ShippingCost.For("standard", 30m));
        // Nobody added "store-pickup" to the switch: the customer pays 4.99 to collect the order.
        Assert.Equal(4.99m, ShippingCost.For("store-pickup", 30m));
    }

    [Fact]
    public void After_store_pickup_is_a_config_entry_and_costs_nothing()
    {
        var catalog = CheckedIn();

        Assert.Equal(["standard", "express", "store-pickup"], catalog.OptionNames);
        Assert.Equal(0m, catalog.Quote("store-pickup", 30m));
        Assert.Equal(4.99m, catalog.Quote("standard", 49.99m));
        Assert.Equal(0m, catalog.Quote("standard", 50m));
        Assert.Equal(9.99m, catalog.Quote("express", 200m));
    }

    [Fact]
    public void After_an_unknown_option_throws_instead_of_taking_another_price()
    {
        var ex = Assert.Throws<ArgumentException>(() => CheckedIn().Quote("drone", 30m));

        Assert.StartsWith("no shipping option 'drone'", ex.Message);
    }

    [Fact]
    public void After_every_config_error_is_reported_at_startup()
    {
        ShippingOption[] options =
        [
            new("drone", "by-weight"),
            new("express", "flat", Price: -1m),
            new("Express", "free", Price: 2m),
            new("standard", "free-over", Price: 4.999m),
            new(" ", "free"),
        ];

        Assert.Equal(
        [
            "option 'drone': no rule for kind 'by-weight'",
            "option 'express': flat needs a price of 0 or more with at most 2 decimals",
            "option 'Express': name used twice (names compare ignoring case)",
            "option 'Express': free takes no price and no threshold",
            "option 'standard': free-over needs a price of 0 or more with at most 2 decimals",
            "option 'standard': free-over needs a threshold greater than 0",
            "an option has an empty name",
        ], ShippingCatalog.Check(Rules, options));

        var ex = Assert.Throws<InvalidOperationException>(() => new ShippingCatalog(Rules, options));
        Assert.StartsWith("option 'drone': no rule for kind 'by-weight'; option 'express':", ex.Message);
    }

    [Fact]
    public void After_a_misspelled_key_in_the_shipping_file_fails_to_load()
    {
        var ex = Assert.Throws<JsonException>(() =>
            ShippingCatalog.Parse("""[{ "name": "express", "kind": "flat", "prize": 9.99 }]"""));

        Assert.Contains("prize", ex.Message);
    }

    [Fact]
    public void After_a_new_kind_of_price_is_a_new_rule_and_the_catalog_does_not_change()
    {
        var catalog = new ShippingCatalog([.. Rules, new HalfPriceRule()], [new ShippingOption("weekend", "half-price", Price: 9.99m)]);

        // 9.99 / 2 = 4.995, rounded to cents like every other price: 5.00.
        Assert.Equal(5.00m, catalog.Quote("weekend", 30m));
    }

    // Written only in this test: no line of ShippingCatalog was edited to accept it.
    private sealed class HalfPriceRule : IShippingRule
    {
        public string Kind => "half-price";

        public IEnumerable<string> Validate(ShippingOption option)
        {
            if (option.Price is not >= 0)
            {
                yield return "half-price needs a price of 0 or more";
            }
        }

        public decimal Cost(ShippingOption option, decimal orderTotal) =>
            Math.Round(option.Price!.Value / 2, 2, MidpointRounding.AwayFromZero);
    }
}
