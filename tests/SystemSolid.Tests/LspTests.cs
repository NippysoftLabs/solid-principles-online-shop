using Examples.Lsp;
using Examples.Lsp.Before;

namespace SystemSolid.Tests;

public class LspTests
{
    [Fact]
    public void Reference_store_keeps_the_whole_contract()
    {
        Assert.Empty(BlobStoreContract.Verify(() => new MemoryBlobStore()));
    }

    [Fact]
    public void Before_the_naive_file_store_passes_the_happy_path_but_breaks_the_contract()
    {
        var store = new NaiveFileBlobStore(new SimulatedFileSystem());
        store.Put("invoice-1.pdf", [1, 2, 3]);
        Assert.Equal([1, 2, 3], store.Get("invoice-1.pdf"));

        var violations = BlobStoreContract.Verify(() => new NaiveFileBlobStore(new SimulatedFileSystem()));

        Assert.Equal(
        [
            "keys differing only in case stay apart: wrong result",
            "trailing period is part of the key: wrong result",
            "separators are allowed in keys: threw IOException: invalid character in file name '2026/11/report:v1'",
            "device names are allowed in keys: threw IOException: reserved device name 'CON'",
            "key of 256 non-ASCII characters: threw PathTooLongException: file name has 256 characters, the limit is 255",
        ], violations);
    }

    [Fact]
    public void After_the_hashed_file_store_keeps_the_whole_contract()
    {
        Assert.Empty(BlobStoreContract.Verify(() => new HashedFileBlobStore(new SimulatedFileSystem())));
    }
}
