using System.Text.Json;
using System.Text.Json.Serialization;

namespace Examples.Ocp.After;

/// <summary>One entry of config/shipping.json. Price and Threshold are in euros.</summary>
public sealed record ShippingOption(string Name, string Kind, decimal? Price = null, decimal? Threshold = null);

/// <summary>A kind of shipping price. A new kind is a new class registered at startup; the catalog does not change.</summary>
public interface IShippingRule
{
    string Kind { get; }

    /// <summary>Configuration errors this rule can see in an option.</summary>
    IEnumerable<string> Validate(ShippingOption option);

    decimal Cost(ShippingOption option, decimal orderTotal);
}

public sealed class ShippingCatalog
{
    // Strict on purpose: a misspelled key, a missing value, a null or a price written as a string
    // fails when the file is loaded.
    private static readonly JsonSerializerOptions ConfigJson = new(JsonSerializerOptions.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        NumberHandling = JsonNumberHandling.Strict,
        AllowDuplicateProperties = false,
    };

    private readonly Dictionary<string, IShippingRule> _rules;
    private readonly Dictionary<string, ShippingOption> _options;

    /// <summary>Throws InvalidOperationException with every configuration error, so startup fails with all of them at once.</summary>
    public ShippingCatalog(IEnumerable<IShippingRule> rules, IReadOnlyList<ShippingOption> options)
    {
        // ToDictionary throws if two rules claim the same kind.
        _rules = rules.ToDictionary(r => r.Kind, StringComparer.Ordinal);
        var errors = Check(_rules, options);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("; ", errors));
        }
        _options = options.ToDictionary(o => o.Name, StringComparer.Ordinal);
    }

    public IEnumerable<string> OptionNames => _options.Keys;

    public static IReadOnlyList<ShippingOption> Parse(string json) =>
        JsonSerializer.Deserialize<List<ShippingOption>>(json, ConfigJson)
        ?? throw new JsonException("shipping file is null");

    /// <summary>The error list without building a catalog, for a CI step or a test.</summary>
    public static IReadOnlyList<string> Check(IEnumerable<IShippingRule> rules, IReadOnlyList<ShippingOption> options) =>
        Check(rules.ToDictionary(r => r.Kind, StringComparer.Ordinal), options);

    /// <summary>Cost of an option for an order total. An unknown option throws instead of falling back to another price.</summary>
    public decimal Quote(string optionName, decimal orderTotal)
    {
        if (!_options.TryGetValue(optionName, out var option))
        {
            throw new ArgumentException($"no shipping option '{optionName}'", nameof(optionName));
        }
        if (orderTotal < 0)
        {
            throw new ArgumentException($"order total must be 0 or more; got {orderTotal}", nameof(orderTotal));
        }
        return _rules[option.Kind].Cost(option, orderTotal);
    }

    private static List<string> Check(Dictionary<string, IShippingRule> rules, IReadOnlyList<ShippingOption> options)
    {
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option.Name))
            {
                errors.Add("an option has an empty name");
                continue;
            }
            if (!seen.Add(option.Name))
            {
                errors.Add($"option '{option.Name}': name used twice (names compare ignoring case)");
            }
            if (!rules.TryGetValue(option.Kind, out var rule))
            {
                errors.Add($"option '{option.Name}': no rule for kind '{option.Kind}'");
                continue;
            }
            errors.AddRange(rule.Validate(option).Select(e => $"option '{option.Name}': {e}"));
        }
        return errors;
    }

    internal static bool IsPrice(decimal? value) => value is >= 0 && decimal.Round(value.Value, 2) == value.Value;
}

/// <summary>The same price for every order.</summary>
public sealed class FlatRule : IShippingRule
{
    public string Kind => "flat";

    public IEnumerable<string> Validate(ShippingOption option)
    {
        if (!ShippingCatalog.IsPrice(option.Price))
        {
            yield return "flat needs a price of 0 or more with at most 2 decimals";
        }
        if (option.Threshold is not null)
        {
            yield return "flat takes no threshold";
        }
    }

    public decimal Cost(ShippingOption option, decimal orderTotal) => option.Price!.Value;
}

/// <summary>Free from the threshold on (inclusive), the price below it.</summary>
public sealed class FreeOverRule : IShippingRule
{
    public string Kind => "free-over";

    public IEnumerable<string> Validate(ShippingOption option)
    {
        if (!ShippingCatalog.IsPrice(option.Price))
        {
            yield return "free-over needs a price of 0 or more with at most 2 decimals";
        }
        if (option.Threshold is not > 0)
        {
            yield return "free-over needs a threshold greater than 0";
        }
    }

    public decimal Cost(ShippingOption option, decimal orderTotal) =>
        orderTotal >= option.Threshold!.Value ? 0m : option.Price!.Value;
}

/// <summary>Always free, such as picking the order up in a store.</summary>
public sealed class FreeRule : IShippingRule
{
    public string Kind => "free";

    public IEnumerable<string> Validate(ShippingOption option)
    {
        if (option.Price is not null || option.Threshold is not null)
        {
            yield return "free takes no price and no threshold";
        }
    }

    public decimal Cost(ShippingOption option, decimal orderTotal) => 0m;
}
