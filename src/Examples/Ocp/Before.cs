using System.Globalization;
using System.Text.Json;

namespace Examples.Ocp.Before;

/// <summary>
/// BEFORE: every source kind is an arm of a switch. A new kind means editing and redeploying
/// this class; until someone does, its rows fall into the default arm and are dropped.
/// </summary>
public static class StockImporter
{
    public static IReadOnlyList<(string Sku, int Quantity)> Import(string kind, string content) => kind switch
    {
        "csv" => ReadCsv(content),
        "jsonl" => ReadJsonLines(content),
        _ => [],
    };

    private static List<(string, int)> ReadCsv(string content) =>
        content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r').Split(','))
            .Select(f => (f[0], int.Parse(f[1], CultureInfo.InvariantCulture)))
            .ToList();

    private static List<(string, int)> ReadJsonLines(string content) =>
        content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonDocument.Parse(line).RootElement)
            .Select(e => (e.GetProperty("sku").GetString()!, e.GetProperty("quantity").GetInt32()))
            .ToList();
}
