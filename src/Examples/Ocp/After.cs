using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Examples.Ocp.After;

/// <summary>One entry of config/sources.json.</summary>
public sealed record SourceDefinition(string Name, string Format, string? Delimiter = null, bool HasHeader = false);

public sealed record StockRecord(string Sku, int Quantity);

/// <summary>A new format is a new class registered at startup; the catalog does not change.</summary>
public interface IRecordReader
{
    string Format { get; }

    /// <summary>Configuration errors this reader can see in a source definition.</summary>
    IEnumerable<string> Validate(SourceDefinition source);

    IReadOnlyList<StockRecord> Read(SourceDefinition source, string content);
}

public sealed class SourceCatalog
{
    /// <summary>Drops a leading byte order mark (U+FEFF), splits on "\n" and drops a trailing "\r".</summary>
    internal static List<string> Lines(string content) =>
        content.TrimStart('\uFEFF').Split('\n').Select(l => l.TrimEnd('\r')).ToList();

    // Strict on purpose: a misspelled key, a missing value or a null fails when the file is loaded.
    private static readonly JsonSerializerOptions ConfigJson = new(JsonSerializerOptions.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false,
    };

    private readonly Dictionary<string, IRecordReader> _readers;

    public SourceCatalog(IEnumerable<IRecordReader> readers)
    {
        // ToDictionary throws if two readers claim the same format.
        _readers = readers.ToDictionary(r => r.Format, StringComparer.Ordinal);
    }

    public static IReadOnlyList<SourceDefinition> Load(string json) =>
        JsonSerializer.Deserialize<List<SourceDefinition>>(json, ConfigJson)
        ?? throw new JsonException("sources file is null");

    /// <summary>Every error in the list, so CI or startup can fail with all of them at once.</summary>
    public IReadOnlyList<string> Validate(IReadOnlyList<SourceDefinition> sources)
    {
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.Name))
            {
                errors.Add("a source has an empty name");
                continue;
            }
            if (!seen.Add(source.Name))
            {
                errors.Add($"source '{source.Name}': name used twice (names compare ignoring case)");
            }
            if (!_readers.TryGetValue(source.Format, out var reader))
            {
                errors.Add($"source '{source.Name}': no reader for format '{source.Format}'");
                continue;
            }
            errors.AddRange(reader.Validate(source).Select(e => $"source '{source.Name}': {e}"));
        }
        return errors;
    }

    public IReadOnlyList<StockRecord> Import(SourceDefinition source, string content)
    {
        // Validate already ran at startup; this repeats it for a source built in code that skipped it.
        var errors = Validate([source]);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("; ", errors));
        }
        return _readers[source.Format].Read(source, content);
    }
}

/// <summary>CSV, TSV or any one-character delimiter: a new partner with tabs is a config entry.</summary>
public sealed class DelimitedReader : IRecordReader
{
    public string Format => "delimited";

    public IEnumerable<string> Validate(SourceDefinition source)
    {
        if (source.Delimiter is not { Length: 1 })
        {
            yield return "delimited format needs a delimiter of exactly one character";
        }
    }

    public IReadOnlyList<StockRecord> Read(SourceDefinition source, string content)
    {
        var delimiter = source.Delimiter![0];
        var lines = SourceCatalog.Lines(content);
        var records = new List<StockRecord>();
        for (var i = source.HasHeader ? 1 : 0; i < lines.Count; i++)
        {
            if (lines[i].Length == 0)
            {
                continue;
            }
            var fields = lines[i].Split(delimiter);
            // A bad row stops the import with its line number instead of being skipped.
            if (fields.Length != 2 || fields[0].Length == 0
                || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var quantity))
            {
                throw new FormatException($"source '{source.Name}', line {i + 1}: expected sku{delimiter}quantity");
            }
            records.Add(new StockRecord(fields[0], quantity));
        }
        return records;
    }
}

/// <summary>One JSON object per line: {"sku":"A1","quantity":5}.</summary>
public sealed class JsonLinesReader : IRecordReader
{
    private static readonly JsonSerializerOptions RowJson = new(JsonSerializerOptions.Web)
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        NumberHandling = JsonNumberHandling.Strict,
        AllowDuplicateProperties = false,
    };

    public string Format => "jsonl";

    public IEnumerable<string> Validate(SourceDefinition source)
    {
        if (source.Delimiter is not null)
        {
            yield return "jsonl format takes no delimiter";
        }
        if (source.HasHeader)
        {
            yield return "jsonl format has no header line";
        }
    }

    public IReadOnlyList<StockRecord> Read(SourceDefinition source, string content)
    {
        var lines = SourceCatalog.Lines(content);
        var records = new List<StockRecord>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].Length == 0)
            {
                continue;
            }
            var record = JsonSerializer.Deserialize<StockRecord>(lines[i], RowJson);
            // Same rule as the delimited reader: a non-empty sku and a quantity of 0 or more.
            if (record is null || record.Sku.Length == 0 || record.Quantity < 0)
            {
                throw new FormatException($"source '{source.Name}', line {i + 1}: expected a non-empty sku and a quantity of 0 or more");
            }
            records.Add(record);
        }
        return records;
    }
}
