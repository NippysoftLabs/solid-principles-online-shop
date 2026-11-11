namespace Examples.Lsp.Before;

/// <summary>
/// BEFORE: the key is used as the file name. It compiles against IBlobStore and passes a happy-path
/// test, but on a case-insensitive file system with reserved names it does not keep the contract.
/// </summary>
public sealed class NaiveFileBlobStore(SimulatedFileSystem files) : IBlobStore
{
    public void Put(string key, byte[] content)
    {
        BlobKey.Check(key);
        files.Write(key, content);
    }

    public byte[]? Get(string key)
    {
        BlobKey.Check(key);
        return files.Read(key);
    }
}
