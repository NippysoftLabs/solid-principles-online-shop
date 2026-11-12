using System.Text.Json;
using Examples.Ocp.After;
using Examples.Ocp.Before;

namespace OnlineShop.Tests;

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

    // Extension 1, a new OPTION: one entry in config/shipping.json that uses a rule the catalog already has.
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

    // A silent fallback is how store pickup ended up costing 4.99: an unknown option has no right price,
    // so the only safe answer is an error the checkout can show and the logs can count.
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

    // Extension 2, a new BEHAVIOUR: a new IShippingRule. HalfPriceRule lives in this test assembly, so
    // ShippingCatalog and OrderTotals (in Examples.dll) are used exactly as compiled, without one edited line.
    [Fact]
    public void After_a_new_rule_from_another_assembly_works_without_changing_the_catalog_or_its_consumer()
    {
        var catalog = new ShippingCatalog([.. Rules, new HalfPriceRule()], [new ShippingOption("weekend", "half-price", Price: 9.99m)]);

        // 9.99 / 2 = 4.995, rounded to cents like every other price: 5.00.
        Assert.Equal(5.00m, catalog.Quote("weekend", 30m));
        Assert.Equal(35.00m, new OrderTotals(catalog).Total(30m, "weekend"));
        Assert.NotEqual(typeof(ShippingCatalog).Assembly, typeof(HalfPriceRule).Assembly);
        Assert.Equal(typeof(ShippingCatalog).Assembly, typeof(OrderTotals).Assembly);
    }

    [Fact]
    public void After_an_option_whose_rule_is_not_registered_stops_startup()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ShippingCatalog(Rules, [new ShippingOption("weekend", "half-price", Price: 9.99m)]));

        Assert.Equal("option 'weekend': no rule for kind 'half-price'", ex.Message);
    }

    // Written only in this test: no line of ShippingCatalog or OrderTotals was edited to accept it.
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
