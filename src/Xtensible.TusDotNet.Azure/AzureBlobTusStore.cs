using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using Xtensible.Time;
namespace Xtensible.TusDotNet.Azure
{
    public class AzureBlobTusStore : ITusStore,
        ITusCreationStore
        , ITusReadableStore
        , ITusTerminationStore
        , ITusExpirationStore
#if ENABLE_CHECKSUM
        , ITusChecksumStore // we can only efficiently verify the checksum if we cap chunk sizes to <AppendBlobBlockSize> (4MB) or less
#endif

    {
        private const int AppendBlobBlockSize = 4_194_304; //4MB
        private const string UploadLengthKey = "UploadLength";
        private const string RawMetadataKey = "RawMetadata";
        private const string UploadOffsetKey = "UploadOffset";
        private const string MD5ChecksumKey = "MD5Checksum";

        private static bool _containerExists;
        private static readonly SemaphoreSlim ContainerSemaphore = new SemaphoreSlim(1);
        private static readonly IEnumerable<string> SupportedChecksumAlgorithms = new ReadOnlyCollection<string>(new[] { "md5" });
        private readonly string _connectionString;
        private readonly string _containerName;
        private readonly string _blobPath;
        private readonly bool _isContainerPublic;
        private readonly int _maxDegreeOfDeleteParallelism;
        private readonly MetadataParsingStrategy _metadataParsingStrategy;
        private readonly ArrayPool<byte> _writeBuffer = ArrayPool<byte>.Create();
        private readonly ITusExpirationDetailsStore _expirationDetailsStore;
        private readonly Func<string, Task<string>> _fileIdGeneratorAsync;

        public AzureBlobTusStore(string connectionString, string containerName, AzureBlobTusStoreOptions options = default)
        {
            options ??= new AzureBlobTusStoreOptions();
            _expirationDetailsStore = options.ExpirationDetailsStore ?? new AzureBlobExpirationDetailsStore(connectionString, containerName);
            _connectionString = connectionString;
            _containerName = containerName;
            _metadataParsingStrategy = options.MetadataParsingStrategy;
            _maxDegreeOfDeleteParallelism = options.MaxDegreeOfDeleteParallelism;
            _isContainerPublic = options.IsContainerPublic;
            _fileIdGeneratorAsync = options.FileIdGeneratorAsync ?? (metadata => Task.FromResult(Guid.NewGuid().ToString("N")));
            _blobPath = options.BlobPath;
        }

        public async Task<string> CreateFileAsync(long uploadLength, string metadata, CancellationToken cancellationToken)
        {
            await EnsureContainerExistsAsync(_connectionString, _containerName, _isContainerPublic, cancellationToken);

            var id = await _fileIdGeneratorAsync(metadata);
            var appendBlobClient = GetAppendBlobClient(id);
            var metadataDictionary = new Dictionary<string, string>
            {
                [UploadLengthKey] = uploadLength.ToString(),
                [RawMetadataKey] = metadata ?? string.Empty,
                [UploadOffsetKey] = "0"
            };

            await appendBlobClient.CreateIfNotExistsAsync(new AppendBlobCreateOptions { Metadata = metadataDictionary }, cancellationToken);
            return id;
        }

        public async Task<string> GetUploadMetadataAsync(string fileId, CancellationToken cancellationToken)
        {
            await EnsureContainerExistsAsync(_connectionString, _containerName, _isContainerPublic, cancellationToken);
            return await GetBlobMetadataAsync(fileId, RawMetadataKey, cancellationToken);
        }

        public Task SetExpirationAsync(string fileId, DateTimeOffset expires, CancellationToken cancellationToken)
        {
            return _expirationDetailsStore.SetExpirationAsync(fileId, expires, cancellationToken);
        }

        public Task<DateTimeOffset?> GetExpirationAsync(string fileId, CancellationToken cancellationToken)
        {
            return _expirationDetailsStore.GetExpirationAsync(fileId, cancellationToken);
        }

        public Task<IEnumerable<string>> GetExpiredFilesAsync(CancellationToken cancellationToken)
        {
            return _expirationDetailsStore.GetExpiredFilesAsync(cancellationToken);
        }

        public async Task<int> RemoveExpiredFilesAsync(CancellationToken cancellationToken)
        {
            var blobContainerClient = new BlobContainerClient(_connectionString, _containerName);
            var blobs = await GetExpiredFilesAsync(cancellationToken);

            var count = 0;
            Parallel.ForEach(blobs, new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfDeleteParallelism }, async blob =>
            {
                await blobContainerClient.DeleteBlobIfExistsAsync(blob, cancellationToken: cancellationToken);
                Interlocked.Increment(ref count);
            });
            return count;
        }


        public async Task<ITusFile> GetFileAsync(string fileId, CancellationToken cancellationToken)
        {
            await EnsureContainerExistsAsync(_connectionString, _containerName, _isContainerPublic, cancellationToken);
            return new TusAzureBlobFile(fileId, GetAppendBlobClient(fileId), _metadataParsingStrategy);
        }

        public async Task<long> AppendDataAsync(string fileId, Stream stream, CancellationToken cancellationToken)
        {
            await EnsureContainerExistsAsync(_connectionString, _containerName, _isContainerPublic, cancellationToken);
            var writeBuffer = _writeBuffer.Rent(AppendBlobBlockSize);
            long bytesWrittenThisRequest = 0;
            var md5Hash = Array.Empty<byte>();

            try
            {
                var appendBlobClient = GetAppendBlobClient(fileId);

                var properties = await GetBlobMetadataAsync(fileId, cancellationToken);
                var fileLength = long.Parse(properties[UploadLengthKey]);
                var offset = long.Parse(properties[UploadOffsetKey]);
                var total = offset;
                if (fileLength == offset)
                {
                    return 0;
                }

                var bytesReadFromClient = 0;
                var bytesWritten = 0;
                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    bytesReadFromClient = await stream.ReadAsync(writeBuffer, bytesWritten, AppendBlobBlockSize - bytesWritten, cancellationToken);
                    bytesWrittenThisRequest += bytesReadFromClient;
                    if (bytesReadFromClient == 0)
                    {
                        break;
                    }
                    total += bytesReadFromClient;

                    if (total > fileLength)
                    {
                        throw new TusStoreException(
                            $"Stream contains more data than the file's upload length. Stream data: {total}, upload length: {fileLength}.");
                    }

                    bytesWritten += bytesReadFromClient;
                    if (bytesWritten >= AppendBlobBlockSize)
                    {
                        using (var ms = new MemoryStream(writeBuffer, 0, bytesWritten))
                        {
                            var options = new AppendBlobAppendBlockOptions
                            {
                                TransferValidation = new UploadTransferValidationOptions
                                {
                                    ChecksumAlgorithm = StorageChecksumAlgorithm.MD5,
                                    PrecalculatedChecksum = ChecksumProvider.GetChecksum("md5", ms)
                                }
                            };
                            await appendBlobClient.AppendBlockAsync(ms, options, cancellationToken: cancellationToken);
                        }
                        bytesWritten = 0;
                    }
                }

                if (bytesWritten > 0)
                {
                    using (var ms = new MemoryStream(writeBuffer, 0, bytesWritten))
                    {
                        var options = new AppendBlobAppendBlockOptions
                        {
                            TransferValidation = new UploadTransferValidationOptions
                            {
                                ChecksumAlgorithm = StorageChecksumAlgorithm.MD5,
                                PrecalculatedChecksum = ChecksumProvider.GetChecksum("md5", ms)
                            }
                        };
                        await appendBlobClient.AppendBlockAsync(ms, options, cancellationToken: cancellationToken);
                    }
                }

                properties[UploadOffsetKey] = (long.Parse(properties[UploadOffsetKey]) + bytesWrittenThisRequest).ToString();
                properties[MD5ChecksumKey] = Convert.ToBase64String(md5Hash);
                await appendBlobClient.SetMetadataAsync(properties, cancellationToken: cancellationToken);
            }
            finally
            {
                _writeBuffer.Return(writeBuffer);
            }

            return bytesWrittenThisRequest;
        }

        public async Task<bool> FileExistAsync(string fileId, CancellationToken cancellationToken)
        {
            await EnsureContainerExistsAsync(_connectionString, _containerName, _isContainerPublic, cancellationToken);
            var blobClient = GetAppendBlobClient(fileId);
            return await blobClient.ExistsAsync(cancellationToken);
        }

        public async Task<long?> GetUploadLengthAsync(string fileId, CancellationToken cancellationToken)
        {
            await EnsureContainerExistsAsync(_connectionString, _containerName, _isContainerPublic, cancellationToken);
            return long.Parse(await GetBlobMetadataAsync(fileId, UploadLengthKey, cancellationToken));
        }

        public async Task<long> GetUploadOffsetAsync(string fileId, CancellationToken cancellationToken)
        {
            await EnsureContainerExistsAsync(_connectionString, _containerName, _isContainerPublic, cancellationToken);
            return long.Parse(await GetBlobMetadataAsync(fileId, UploadOffsetKey, cancellationToken));
        }

        public async Task DeleteFileAsync(string fileId, CancellationToken cancellationToken)
        {
            await EnsureContainerExistsAsync(_connectionString, _containerName, _isContainerPublic, cancellationToken);
            var blobClient = GetAppendBlobClient(fileId);
            await blobClient.DeleteAsync(cancellationToken: cancellationToken);
        }

        private async static Task EnsureContainerExistsAsync(string connectionString, string containerName, bool isContainerPublic,
            CancellationToken cancellationToken)
        {
            if (_containerExists)
            {
                return;
            }

            await ContainerSemaphore.WaitAsync(60000, cancellationToken).ConfigureAwait(false);

            if (_containerExists)
            {
                return;
            }
            var containerClient = new BlobContainerClient(connectionString, containerName);
            await containerClient.CreateIfNotExistsAsync(isContainerPublic ? PublicAccessType.BlobContainer : PublicAccessType.None,
                cancellationToken: cancellationToken);
            _containerExists = true;
            ContainerSemaphore.Release(1);
        }

        public Task<IEnumerable<string>> GetSupportedAlgorithmsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(SupportedChecksumAlgorithms);
        }

        public async Task<bool> VerifyChecksumAsync(string fileId, string algorithm, byte[] checksum, CancellationToken cancellationToken)
        {
            var metadata = await GetBlobMetadataAsync(fileId, cancellationToken);
            if (metadata.TryGetValue(MD5ChecksumKey, out var md5))
            {
                if (md5 == Convert.ToBase64String(checksum))
                {
                    return true;
                }
            }
            return false;
        }

        private AppendBlobClient GetAppendBlobClient(string fileId)
        {
            return new AppendBlobClient(_connectionString, _containerName, Path.Combine(_blobPath, fileId));
        }

        private async Task<string> GetBlobMetadataAsync(string fileId, string key, CancellationToken cancellationToken)
        {
            var blobClient = GetAppendBlobClient(fileId);
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            properties.Value.Metadata.TryGetValue(key, out var value);
            return value;
        }

        private async Task<Dictionary<string, string>> GetBlobMetadataAsync(string fileId, CancellationToken cancellationToken)
        {
            var blobClient = GetAppendBlobClient(fileId);
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            return properties.Value.Metadata.ToDictionary(k => k.Key, v => v.Value);
        }
    }
}