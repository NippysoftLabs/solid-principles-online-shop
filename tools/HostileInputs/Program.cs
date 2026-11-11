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
    Console.WriteLine($"{++n,2}. {input}\n    -> {result}");
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
Console.WriteLine("== D. SourceCatalog.Load(sources file)");
var catalog = new SourceCatalog([new DelimitedReader(), new JsonLinesReader()]);
foreach (var json in new[]
{
    """[{"name":"p","format":"delimited","delimeter":","}]""",
    """[{"name":null,"format":"jsonl"}]""",
    """[{"name":"p"}]""",
    """[{"name":"p","format":"jsonl"},]""",
    """{"name":"p","format":"jsonl"}""",
    """[{"name":"p","format":"delimited","delimiter":",","hasHeader":"true"}]""",
    """[{"Name":"p","Format":"jsonl"}]""",
    """[{"name":"p","format":"delimited","delimiter":""}]""",
})
{
    Show(json, () => catalog.Validate(SourceCatalog.Load(json)));
}

Console.WriteLine();
Console.WriteLine("== E. SourceCatalog.Import(source, content)");
var csv = new SourceDefinition("north", "delimited", ",");
var jsonl = new SourceDefinition("feed", "jsonl");
foreach (var (source, content) in new[]
{
    (csv, "A1,+5"), (csv, "A1, 5"), (csv, "A1,\u0665"), (csv, "A1,5\r\nB2,6\r\n"), (csv, "\uFEFFA1,5"),
    (csv, "A1,5,extra"), (csv, "A1,99999999999"), (jsonl, "null"), (jsonl, """{"sku":"A1","quantity":-1}"""),
    (jsonl, """{"sku":"A1","quantity":1.5}"""), (jsonl, """{"sku":"A1","quantity":"5"}"""),
})
{
    var shown = content.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\uFEFF", "<BOM>").Replace("\u0665", "<U+0665>");
    Show($"{source.Format}: {shown}", () => catalog.Import(source, content));
}

Console.WriteLine();
Console.WriteLine("== F. IBlobStore.Put(key, content) then Get(key), per store");
foreach (var (key, content) in new (string, byte[])[]
{
    ("a\0b", [1]), (".", [1]), ("NUL.txt", [1]), ("COM\u00B9", [1]), ("x", null!),
})
{
    var shownKey = key.Replace("\0", "\\0").Replace("\u00B9", "<U+00B9>");
    foreach (var (name, store) in new (string, IBlobStore)[]
    {
        ("memory", new MemoryBlobStore()),
        ("naive", new NaiveFileBlobStore(new SimulatedFileSystem())),
        ("hashed", new HashedFileBlobStore(new SimulatedFileSystem())),
    })
    {
        Show($"{name}: key '{shownKey}', content {(content is null ? "null" : "[1]")}", () =>
        {
            store.Put(key, content!);
            return store.Get(key);
        });
    }
}

Console.WriteLine();
Console.WriteLine("== G. CloudInvoiceArchive via InvoiceService.Archive(order)");
foreach (var id in new[] { "../o-1", "a/b", "%2F", "", "pedido-\u00F1", new string('9', 300) })
{
    var shown = id.Length > 40 ? $"<{id.Length} x '9'>" : id;
    Show($"order id '{shown}'", () =>
    {
        var location = new InvoiceService(new CloudInvoiceArchive(new CloudBucketClient("invoices-prod")))
            .Archive(new Order(id, "c1", 1m));
        return location.Length > 80 ? $"{location[..60]}... ({location.Length} chars)" : location;
    });
}
