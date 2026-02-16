using tusdotnet.Interfaces;

namespace Xtensible.TusDotNet.Azure
{
    /// <summary>
    /// An Azure Blob Storage backed tus store that extends <see cref="AzureBlobTusStore"/>
    /// with checksum validation (MD5) support via <see cref="ITusChecksumStore"/>.
    /// </summary>
    /// <remarks>
    /// Checksum verification is performed per 4 MiB block (4,194,304 bytes). The TUS client
    /// must use a chunk size &lt;= 4 MiB for checksum validation to work reliably.
    /// Only the MD5 algorithm is supported, as Azure Blob Storage natively supports MD5
    /// checksums on block appends.
    /// </remarks>
    public class AzureBlobChecksumTusStore(string connectionString, string containerName, AzureBlobTusStoreOptions options = default)
        : AzureBlobTusStore(connectionString, containerName, options), ITusChecksumStore;
}
