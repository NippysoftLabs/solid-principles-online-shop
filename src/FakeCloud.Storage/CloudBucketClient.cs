namespace FakeCloud.Storage;

/// <summary>
/// Stands in for a cloud vendor's storage SDK. It keeps objects in memory so the examples
/// run without an account or a network.
/// </summary>
public sealed class CloudBucketClient(string bucket)
{
    private readonly Dictionary<string, byte[]> _objects = new(StringComparer.Ordinal);

    public string Bucket { get; } = bucket;

    public void UploadObject(string objectPath, byte[] content) => _objects[objectPath] = content.ToArray();

    public byte[]? DownloadObject(string objectPath) =>
        _objects.TryGetValue(objectPath, out var content) ? content.ToArray() : null;
}
