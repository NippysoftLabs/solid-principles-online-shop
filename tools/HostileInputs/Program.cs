// Runs hostile inputs against the "after" code and prints each result as it comes out.
// "dotnet run --project tools/HostileInputs" prints the tables; with the argument "schemas" it prints
// the schemas of the released contract types instead. Not part of the test run.
using System.Text.Json.Nodes;
using Examples.Contracts.After;
using Examples.Lsp;
using Examples.Lsp.Before;
using Examples.Ocp.After;
using FakeCloud.Storage;
using Orders.Domain;
using Orders.Infrastructure;
using Orders.Notifications;
using V11 = Examples.Contracts.After.V1_1.OrderPlaced;

if (args is ["schemas"])
{
    Console.WriteLine(SchemaCompatibility.SchemaOf(typeof(Examples.Contracts.After.V1_0.OrderPlaced)).ToJsonString());
    Console.WriteLine(SchemaCompatibility.SchemaOf(typeof(V11)).ToJsonString());
    return;
}

var n = 0;
void Show(string input, Func<object?> run)
{
    string result;
    try
    {
        result = Format(run());
    }
    catch (Exception ex)
    {
        result = $"{ex.GetType().Name}: {ex.Message}";
    }
    // A NUL in a key would print as nothing visible; show it as \0.
    Console.WriteLine($"{++n,2}. {input}\n    -> {result.Replace("\0", "\\0")}");
}

static string Format(object? value) => value switch
{
    null => "null",
    string s => s.Length == 0 ? "(empty string)" : s,
    byte[] b => $"bytes [{string.Join(",", b)}]",
    IEnumerable<string> list => list.Any() ? string.Join(" | ", list) : "(no items)",
    System.Collections.IEnumerable items => "[" + string.Join(", ", items.Cast<object>()) + "]",
    _ => value.ToString() ?? "null",
};

Console.WriteLine("== A. ContractJson.Read<OrderPlaced 1.1>(message)");
foreach (var json in new[]
{
    """{"orderId":"o-1","total":100.40}""",
    """{"orderId":"o-1","amount":"100.40"}""",
    """{"orderId":"o-1","amount":null}""",
    """{"orderId":null,"amount":1}""",
    """{"orderId":"o-1","amount":1,"amount":999}""",
    """{"OrderId":"o-1","AMOUNT":5}""",
    """{"orderId":"o-1","amount":1,"Amount":999}""",
    """{"orderId":"o-1","amount":1e3}""",
    """{"orderId":"o-1","amount":1e400}""",
    """{"orderId":"o-1","amount":0.12345678901234567890123456789}""",
    """null""",
    """{"orderId":"o-1","amount":1,"discount":5}""",
    """{"orderId":"o-1","amount":1,}""",
    """{"orderId":"o-1","amount":1,"currency":5}""",
    """{"orderId":"","amount":-1}""",
})
{
    Show(json, () => ContractJson.Read<V11>(json));
}

Console.WriteLine();
Console.WriteLine("== B. SchemaCompatibility.BreakingChanges(old, new)");
var schemaPairs = new (string Label, string Old, string New)[]
{
    ("integer -> number", """{"type":"object","properties":{"qty":{"type":"integer"}},"required":["qty"]}""",
                          """{"type":"object","properties":{"qty":{"type":"number"}},"required":["qty"]}"""),
    ("number -> integer", """{"type":"object","properties":{"qty":{"type":"number"}},"required":["qty"]}""",
                          """{"type":"object","properties":{"qty":{"type":"integer"}},"required":["qty"]}"""),
    ("string -> string or null", """{"type":"object","properties":{"id":{"type":"string"}},"required":["id"]}""",
                                 """{"type":"object","properties":{"id":{"type":["string","null"]}},"required":["id"]}"""),
    ("required -> optional", """{"type":"object","properties":{"id":{"type":"string"}},"required":["id"]}""",
                             """{"type":"object","properties":{"id":{"type":"string"}}}"""),
    ("enum value added", """{"type":"object","properties":{"s":{"enum":["new","paid"]}}}""",
                         """{"type":"object","properties":{"s":{"enum":["new","paid","refunded"]}}}"""),
    ("array item type changed", """{"type":"object","properties":{"tags":{"type":"array","items":{"type":"string"}}}}""",
                                """{"type":"object","properties":{"tags":{"type":"array","items":{"type":"integer"}}}}"""),
    ("nested required property removed", """{"type":"object","properties":{"ship":{"type":"object","properties":{"zip":{"type":"string"}},"required":["zip"]}}}""",
                                         """{"type":"object","properties":{"ship":{"type":"object","properties":{}}}}"""),
    ("property renamed by case: amount -> Amount", """{"type":"object","properties":{"amount":{"type":"number"}},"required":["amount"]}""",
                                                   """{"type":"object","properties":{"Amount":{"type":"number"}},"required":["Amount"]}"""),
    ("same type, amount now in cents", """{"type":"object","properties":{"amount":{"type":"number"}},"required":["amount"]}""",
                                       """{"type":"object","properties":{"amount":{"type":"number"}},"required":["amount"]}"""),
    ("optional property removed", """{"type":"object","properties":{"note":{"type":["string","null"]}}}""",
                                  """{"type":"object","properties":{}}"""),
    ("maxLength 50 -> 10", """{"type":"object","properties":{"id":{"type":"string","maxLength":50}}}""",
                           """{"type":"object","properties":{"id":{"type":"string","maxLength":10}}}"""),
    ("boolean schema true -> false", "true", "false"),
};
foreach (var (label, oldSchema, newSchema) in schemaPairs)
{
    Show(label, () => SchemaCompatibility.BreakingChanges(JsonNode.Parse(oldSchema)!, JsonNode.Parse(newSchema)!));
}

Console.WriteLine();
Console.WriteLine("== C. SchemaCompatibility.CheckVersionBump(old, new, one breaking change)");
string[] oneBreaking = ["$.amount: required property removed"];
foreach (var (oldV, newV) in new[]
{
    ("1.1", "2.0"), ("1.1", "1.2"), ("1.9", "1.10"), ("2.0", "10.0"), ("1.1", "1.2\n"), ("1.1", "1.\u0662"),
    ("1.1", "01.2"), ("1.1", "2.0.0"), ("1.1", " 2.0"), ("1.1", "9999999999.0"), ("2.0", "1.9"),
})
{
    var shown = newV.Replace("\n", "\\n");
    Show($"'{oldV}' -> '{shown}'", () => SchemaCompatibility.CheckVersionBump(oldV, newV, oneBreaking) ?? "valid");
}

Console.WriteLine();
Console.WriteLine("== D. ShippingCatalog.Check(rules, ShippingCatalog.Parse(shipping file))");
IShippingRule[] rules = [new FlatRule(), new FreeOverRule(), new FreeRule()];
foreach (var json in new[]
{
    """[{"name":"express","kind":"flat","prize":9.99}]""",
    """[{"name":"express","kind":"flat","price":"9.99"}]""",
    """[{"name":"express","kind":"flat","price":null}]""",
    """[{"name":null,"kind":"free"}]""",
    """[{"name":"express"}]""",
    """[{"name":"express","kind":"flat","price":9.99,"price":0}]""",
    """[{"Name":"express","Kind":"flat","Price":9.99}]""",
    """[{"name":"express","kind":"flat","price":1e400}]""",
    """[{"name":"express","kind":"FLAT","price":9.99}]""",
    """[{"name":"standard","kind":"free-over","price":4.99,"threshold":-50}]""",
    """[{"name":"express","kind":"flat","price":0.001}]""",
    """[{"name":"express","kind":"flat","price":9.990}]""",
    """[{"name":"express","kind":"flat","price":9.99},]""",
    """{"name":"express","kind":"flat","price":9.99}""",
    """[]""",
})
{
    Show(json, () => ShippingCatalog.Check(rules, ShippingCatalog.Parse(json)));
}

Console.WriteLine();
Console.WriteLine("== E. ShippingCatalog.Quote(option, order total), with config/shipping.json");
var shipping = new ShippingCatalog(rules, ShippingCatalog.Parse(File.ReadAllText(Path.Combine("config", "shipping.json"))));
foreach (var (option, total) in new[]
{
    ("store-pickup", 30m), ("Store-Pickup", 30m), (" express", 30m), ("", 30m), ("standard", 49.995m),
    ("standard", 50m), ("standard", -1m), ("express", 0m), ("standard", decimal.MaxValue),
})
{
    Show($"'{option}', {total}", () => shipping.Quote(option, total));
}

Console.WriteLine();
Console.WriteLine("== F. IPaymentGateway.Charge(key, card, amount), per gateway");
var payments = new (string Label, string Key, string Card, decimal Amount)[]
{
    ("0.001", "k", "card-ok", 0.001m),
    ("19.990", "k", "card-ok", 19.990m),
    ("-0.01", "k", "card-ok", -0.01m),
    ("999999.995", "k", "card-ok", 999999.995m),
    ("decimal.MaxValue", "k", "card-ok", decimal.MaxValue),
    ("key ' ' (one space), 10", " ", "card-ok", 10m),
    ("card ' ' (one space), 10", "k", " ", 10m),
    ("card null, 10", "k", null!, 10m),
    ("card 'CARD-DECLINED', 10", "k", "CARD-DECLINED", 10m),
};
var gateways = new (string Name, Func<IPaymentGateway> Create)[]
{
    ("reference", () => new InMemoryPaymentGateway()),
    ("naive", () => new NaiveCentsGateway(new CentsPaymentApi())),
    ("fixed", () => new CentsGateway(new CentsPaymentApi())),
};
foreach (var (label, key, card, amount) in payments)
{
    foreach (var (name, create) in gateways)
    {
        Show($"{name}: {label}", () => create().Charge(key, card, amount));
    }
}
foreach (var (name, create) in gateways)
{
    Show($"{name}: key 'k' charged 10, then key 'k' again with 99", () =>
    {
        var gateway = create();
        return new[] { gateway.Charge("k", "card-ok", 10m), gateway.Charge("k", "card-ok", 99m) };
    });
}

Console.WriteLine();
Console.WriteLine("== G. ConfirmationEmail.Render(order) by customer name");
foreach (var name in new[] { "", "  Ana  ", "Ana\tRuiz", "José Ruiz", "<b>Ana</b> Ruiz", null! })
{
    var shown = name is null ? "null" : $"'{name.Replace("\t", "\\t")}'";
    Show($"name {shown}", () => ConfirmationEmail.Render(new Order("o-1", name!, 1m)));
}

Console.WriteLine();
Console.WriteLine("== H. CloudInvoiceArchive via InvoiceService.Archive(order)");
foreach (var id in new[] { "../o-1", "a/b", "%2F", "", "pedido-\u00F1", new string('9', 300), "o\uD800", "o\uDBFF" })
{
    var shown = id.Length > 40 ? $"<{id.Length} x '9'>" : string.Concat(id.Select(c => char.IsSurrogate(c) ? $"\\u{(int)c:X4}" : c.ToString()));
    Show($"order id '{shown}'", () =>
    {
        var location = new InvoiceService(new CloudInvoiceArchive(new CloudBucketClient("invoices-prod")))
            .Archive(new Order(id, "Ana", 1m));
        return location.Length > 80 ? $"{location[..60]}... ({location.Length} chars)" : location;
    });
}

Console.WriteLine();
Console.WriteLine("== I. IPaymentGateway.Charge when the provider charges but its answer is lost, per gateway");
foreach (var (name, create) in gateways)
{
    Show($"{name}: key 'k', card 'card-lost-response', 10; then the same call again", () =>
    {
        var gateway = create();
        var results = new List<object>();
        for (var i = 0; i < 2; i++)
        {
            try
            {
                results.Add(gateway.Charge("k", "card-lost-response", 10m));
            }
            catch (Exception ex)
            {
                results.Add($"{ex.GetType().Name}: {ex.Message}");
            }
        }
        return results;
    });
}

Console.WriteLine();
Console.WriteLine("== J. SchemaCompatibility.BreakingChanges(old, new): enum list changes");
foreach (var (label, oldSchema, newSchema) in new[]
{
    ("enum list removed (free text)", """{"type":"object","properties":{"s":{"enum":["new","paid"]}}}""",
                                      """{"type":"object","properties":{"s":{"type":"string"}}}"""),
    ("enum value removed", """{"type":"object","properties":{"s":{"enum":["new","paid","refunded"]}}}""",
                           """{"type":"object","properties":{"s":{"enum":["new","paid"]}}}"""),
    ("free text becomes an enum", """{"type":"object","properties":{"s":{"type":"string"}}}""",
                                  """{"type":"object","properties":{"s":{"enum":["new","paid"]}}}"""),
})
{
    Show(label, () => SchemaCompatibility.BreakingChanges(JsonNode.Parse(oldSchema)!, JsonNode.Parse(newSchema)!));
}
