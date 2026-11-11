using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Examples.Lsp;

/// <summary>
/// The contract every store must keep, checked by <see cref="BlobStoreContract"/>:
/// <list type="bullet">
/// <item>A valid key is any string of 1 to 256 UTF-16 code units. Other keys throw ArgumentException.</item>
/// <item>Keys compare ordinal: case matters and no Unicode normalization is applied.</item>
/// <item>Get returns a copy of the bytes of the last Put for that key, or null if there was none.</item>
/// </list>
/// </summary>
public interface IBlobStore
{
    void Put(string key, byte[] content);

    byte[]? Get(string key);
}

public static class BlobKey
{
    public const int MaxLength = 256;

    public static void Check(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > MaxLength)
        {
            throw new ArgumentException($"key must have 1 to {MaxLength} UTF-16 code units", nameof(key));
        }
    }
}

/// <summary>Reference implementation, used to define what "same behaviour" means.</summary>
public sealed class MemoryBlobStore : IBlobStore
{
    private readonly Dictionary<string, byte[]> _items = new(StringComparer.Ordinal);

    public void Put(string key, byte[] content)
    {
        BlobKey.Check(key);
        _items[key] = content.ToArray();
    }

    public byte[]? Get(string key)
    {
        BlobKey.Check(key);
        return _items.TryGetValue(key, out var content) ? content.ToArray() : null;
    }
}

/// <summary>
/// A model, not a real disk: it applies the Win32 naming rules from Microsoft's
/// "Naming Files, Paths, and Namespaces" page (case-insensitive names, reserved characters,
/// reserved device names, trailing spaces and periods dropped) plus a 255-character name limit.
/// </summary>
public sealed class SimulatedFileSystem
{
    private const int MaxNameLength = 255;
    private static readonly string[] ReservedNames =
        ["CON", "PRN", "AUX", "NUL",
         "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "COM¹", "COM²", "COM³",
         "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9", "LPT¹", "LPT²", "LPT³"];

    private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

    public int FileCount => _files.Count;

    public void Write(string name, byte[] content) => _files[Normalize(name)] = content.ToArray();

    public byte[]? Read(string name) => _files.TryGetValue(Normalize(name), out var c) ? c.ToArray() : null;

    private static string Normalize(string name)
    {
        // Win32 drops trailing spaces and periods, so "report." and "report" name the same file.
        var trimmed = name.TrimEnd(' ', '.');
        if (trimmed.Length == 0)
        {
            throw new IOException($"invalid file name '{name}'");
        }
        if (trimmed.Length > MaxNameLength)
        {
            throw new PathTooLongException($"file name has {trimmed.Length} characters, the limit is {MaxNameLength}");
        }
        if (trimmed.Any(c => c < 32 || "<>:\"/\\|?*".Contains(c)))
        {
            throw new IOException($"invalid character in file name '{name}'");
        }
        var stem = trimmed.Split('.')[0];
        if (ReservedNames.Contains(stem, StringComparer.OrdinalIgnoreCase))
        {
            throw new IOException($"reserved device name '{name}'");
        }
        return trimmed;
    }
}

/// <summary>
/// AFTER: the file name is the SHA-256 of the key's UTF-16 code units, in lowercase hex.
/// 64 characters from [0-9a-f]: never reserved, never too long, and case-insensitivity cannot
/// merge two keys. Hashing the raw code units keeps lone surrogates distinct.
/// What it gives up: file names no longer tell you the key, and keys cannot be listed back.
/// </summary>
public sealed class HashedFileBlobStore(SimulatedFileSystem files) : IBlobStore
{
    public void Put(string key, byte[] content)
    {
        BlobKey.Check(key);
        files.Write(FileName(key), content);
    }

    public byte[]? Get(string key)
    {
        BlobKey.Check(key);
        return files.Read(FileName(key));
    }

    private static string FileName(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(MemoryMarshal.AsBytes(key.AsSpan())));
}
