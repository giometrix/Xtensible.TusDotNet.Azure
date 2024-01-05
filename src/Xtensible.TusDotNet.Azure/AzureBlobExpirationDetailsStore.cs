using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Xtensible.Time;

namespace Xtensible.TusDotNet.Azure
{
    public class AzureBlobExpirationDetailsStore : ITusExpirationDetailsStore
    {
        private const string ExpirationKey = "ExpiresAt";
        private readonly string _connectionString;
        private readonly string _containerName;
        private readonly string _blobPath;

        public AzureBlobExpirationDetailsStore(string connectionString, string containerName, string blobPath = "")
        {
            _connectionString = connectionString;
            _containerName = containerName;
            _blobPath = blobPath;
        }

        public async Task SetExpirationAsync(string fileId, DateTimeOffset expires, CancellationToken cancellationToken)
        {
            var blobClient = GetAppendBlobClient(fileId);
            await blobClient.SetTagsAsync(new Dictionary<string, string>
            {
                [ExpirationKey] = expires.UtcDateTime.ToString("s")
            }, cancellationToken: cancellationToken);
        }

        public async Task<DateTimeOffset?> GetExpirationAsync(string fileId, CancellationToken cancellationToken)
        {
            var blobClient = GetAppendBlobClient(fileId);
            var tags = await blobClient.GetTagsAsync(cancellationToken: cancellationToken);
            if (tags.Value.Tags.TryGetValue(ExpirationKey, out var expiration))
            {
                return DateTimeOffset.ParseExact(expiration, "s", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            }
            return null;
        }

        public async Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken cancellationToken)
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var blobItems = blobServiceClient.FindBlobsByTagsAsync($"@container='{_containerName}' AND {ExpirationKey} < '{Clock.Default.UtcNow:s}'", cancellationToken);
            var toDelete = new List<string>();
            var enumerator = blobItems.GetAsyncEnumerator(cancellationToken);
            while (await enumerator.MoveNextAsync())
            {
                toDelete.Add(enumerator.Current.BlobName);
            }
            return toDelete;
        }

        private AppendBlobClient GetAppendBlobClient(string fileId)
        {
            return new AppendBlobClient(_connectionString, _containerName, Path.Combine(_blobPath, fileId));
        }
    }
}