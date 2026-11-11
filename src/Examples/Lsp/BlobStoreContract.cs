namespace Examples.Lsp;

/// <summary>
/// One contract suite for every IBlobStore. Each check gets a fresh store from the factory.
/// A store that passes behaves like MemoryBlobStore on these inputs; inputs not listed here are not checked.
/// </summary>
public static class BlobStoreContract
{
    private static readonly (string Name, Func<IBlobStore, bool> Holds)[] Checks =
    [
        ("round trip", s => Put(s, "a", 1, 2, 3) && Same(s.Get("a"), 1, 2, 3)),
        ("missing key returns null", s => s.Get("never-written") is null),
        ("empty content is not null", s => Put(s, "e") && Same(s.Get("e"))),
        ("last put wins", s => Put(s, "k", 1) && Put(s, "k", 2) && Same(s.Get("k"), 2)),
        ("stored bytes are a copy", s =>
        {
            byte[] data = [1];
            s.Put("c", data);
            data[0] = 9;
            return Same(s.Get("c"), 1);
        }),
        ("returned bytes are a copy", s =>
        {
            s.Put("r", [1]);
            s.Get("r")![0] = 9;
            return Same(s.Get("r"), 1);
        }),
        ("keys differing only in case stay apart", s =>
            Put(s, "Report.csv", 1) && Put(s, "report.csv", 2) && Same(s.Get("Report.csv"), 1)),
        ("trailing period is part of the key", s =>
            Put(s, "report.", 1) && Put(s, "report", 2) && Same(s.Get("report."), 1)),
        ("separators are allowed in keys", s => Put(s, "2026/11/report:v1", 1) && Same(s.Get("2026/11/report:v1"), 1)),
        ("device names are allowed in keys", s => Put(s, "CON", 1) && Same(s.Get("CON"), 1)),
        ("key of 256 non-ASCII characters", s =>
        {
            var key = new string('数', BlobKey.MaxLength);
            return Put(s, key, 1) && Same(s.Get(key), 1);
        }),
        ("no Unicode normalization (NFC vs NFD)", s =>
            Put(s, "café", 1) && Put(s, "café", 2) && Same(s.Get("café"), 1)),
        ("lone surrogates stay apart", s =>
            Put(s, "\uD800", 1) && Put(s, "\uDBFF", 2) && Same(s.Get("\uD800"), 1)),
        ("empty key throws ArgumentException", s => Throws<ArgumentException>(() => s.Put("", [1]))),
        ("empty key on Get throws ArgumentException", s => Throws<ArgumentException>(() => s.Get(""))),
        ("key of 257 throws ArgumentException", s =>
            Throws<ArgumentException>(() => s.Put(new string('x', BlobKey.MaxLength + 1), [1]))),
    ];

    public static IReadOnlyList<string> Verify(Func<IBlobStore> create)
    {
        var violations = new List<string>();
        foreach (var (name, holds) in Checks)
        {
            try
            {
                if (!holds(create()))
                {
                    violations.Add($"{name}: wrong result");
                }
            }
            catch (Exception ex)
            {
                violations.Add($"{name}: threw {ex.GetType().Name}: {ex.Message}");
            }
        }
        return violations;
    }

    private static bool Put(IBlobStore store, string key, params byte[] content)
    {
        store.Put(key, content);
        return true;
    }

    private static bool Same(byte[]? actual, params byte[] expected) =>
        actual is not null && actual.AsSpan().SequenceEqual(expected);

    // T or a type derived from it: that is what a caller's catch (T) handles. Any other type escapes it.
    private static bool Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (Exception ex)
        {
            return ex is T;
        }
    }
}
