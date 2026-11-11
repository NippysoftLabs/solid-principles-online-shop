using System.Text.Json;
using Examples.Ocp.After;

namespace SystemSolid.Tests;

public class OcpTests
{
    private static readonly SourceCatalog Catalog = new([new DelimitedReader(), new JsonLinesReader()]);

    [Fact]
    public void Before_a_new_source_kind_is_dropped_without_an_error()
    {
        var rows = Examples.Ocp.Before.StockImporter.Import("tsv", "A1\t5\nB2\t7\n");

        Assert.Empty(rows);
    }

    [Fact]
    public void After_a_tab_separated_partner_is_a_config_entry()
    {
        var tsv = new SourceDefinition("partner-tsv", "delimited", "\t");

        var rows = Catalog.Import(tsv, "A1\t5\nB2\t7\n");

        Assert.Equal([new StockRecord("A1", 5), new StockRecord("B2", 7)], rows);
    }

    [Fact]
    public void After_the_checked_in_sources_file_is_valid()
    {
        var sources = SourceCatalog.Load(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config", "sources.json")));

        Assert.Empty(Catalog.Validate(sources));
        Assert.Equal(3, sources.Count);
    }

    [Fact]
    public void After_every_config_error_is_reported_before_any_import()
    {
        SourceDefinition[] sources =
        [
            new("erp", "xml"),
            new("north", "delimited", ";;"),
            new("North", "jsonl", ","),
            new(" ", "jsonl"),
        ];

        Assert.Equal(
        [
            "source 'erp': no reader for format 'xml'",
            "source 'north': delimited format needs a delimiter of exactly one character",
            "source 'North': name used twice (names compare ignoring case)",
            "source 'North': jsonl format takes no delimiter",
            "a source has an empty name",
        ], Catalog.Validate(sources));
    }

    [Fact]
    public void After_a_misspelled_key_in_the_sources_file_fails_to_load()
    {
        var ex = Assert.Throws<JsonException>(() =>
            SourceCatalog.Load("""[{ "name": "p", "format": "delimited", "delimeter": "," }]"""));

        Assert.Contains("delimeter", ex.Message);
    }

    [Fact]
    public void After_a_bad_row_stops_the_import_with_its_line_number()
    {
        var csv = new SourceDefinition("north", "delimited", ",", HasHeader: true);

        var ex = Assert.Throws<FormatException>(() => Catalog.Import(csv, "sku,quantity\nA1,5\nB2,-7\n"));

        Assert.Equal("source 'north', line 3: expected sku,quantity", ex.Message);
    }
}
