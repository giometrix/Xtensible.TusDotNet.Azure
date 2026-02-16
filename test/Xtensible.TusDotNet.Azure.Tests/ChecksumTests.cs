using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Configuration;
using tusdotnet.Interfaces;
using Xunit;

namespace Xtensible.TusDotNet.Azure.Tests
{
    public class ChecksumTests : IAsyncLifetime
    {
        private const string ContainerName = "checksum-tests";
        private const string SmallFileName = "checksum-small.txt";
        private const int SmallFileSize = 4_096;

        private readonly string _connectionString;
        private readonly AzureBlobChecksumTusStore _store;
        private string _fileId;

        public ChecksumTests()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appSettings.json", true)
                .AddJsonFile("appSettings.local.json", true)
                .AddEnvironmentVariables()
                .Build();

            _connectionString = config.GetConnectionString("AzureBlobStorage");
            _store = new AzureBlobChecksumTusStore(_connectionString, ContainerName);

            EnsureTestFile();
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            if (_fileId != null)
            {
                try
                {
                    await _store.DeleteFileAsync(_fileId, CancellationToken.None);
                }
                catch
                {
                    // best effort cleanup
                }
            }
            _store.Dispose();
        }

        private void EnsureTestFile()
        {
            if (!File.Exists(SmallFileName))
            {
                using var sw = new StreamWriter(SmallFileName);
                for (var i = 0; i < SmallFileSize; i++)
                {
                    sw.Write('1');
                }
            }
        }

        [Fact]
        public void implements_ITusChecksumStore()
        {
            Assert.IsAssignableFrom<ITusChecksumStore>(_store);
        }

        [Fact]
        public void base_store_does_not_implement_ITusChecksumStore()
        {
            using var baseStore = new AzureBlobTusStore(_connectionString, ContainerName);
            Assert.False(baseStore is ITusChecksumStore);
        }

        [Fact]
        public async Task get_supported_algorithms_returns_md5()
        {
            var algorithms = await _store.GetSupportedAlgorithmsAsync(CancellationToken.None);
            Assert.Contains("md5", algorithms);
        }

        [Fact]
        public async Task verify_checksum_succeeds_with_correct_md5()
        {
            var fileInfo = new FileInfo(SmallFileName);
            _fileId = await _store.CreateFileAsync(fileInfo.Length, null, CancellationToken.None);
            await _store.AppendDataAsync(_fileId, File.OpenRead(SmallFileName), CancellationToken.None);

            // compute the expected MD5 of the file
            byte[] expectedChecksum;
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(SmallFileName))
            {
                expectedChecksum = md5.ComputeHash(stream);
            }

            var result = await _store.VerifyChecksumAsync(_fileId, "md5", expectedChecksum, CancellationToken.None);
            Assert.True(result);
        }

        [Fact]
        public async Task verify_checksum_fails_with_wrong_md5()
        {
            var fileInfo = new FileInfo(SmallFileName);
            _fileId = await _store.CreateFileAsync(fileInfo.Length, null, CancellationToken.None);
            await _store.AppendDataAsync(_fileId, File.OpenRead(SmallFileName), CancellationToken.None);

            var wrongChecksum = new byte[16]; // all zeros — won't match
            var result = await _store.VerifyChecksumAsync(_fileId, "md5", wrongChecksum, CancellationToken.None);
            Assert.False(result);
        }

        [Fact]
        public async Task append_and_verify_round_trip()
        {
            var fileInfo = new FileInfo(SmallFileName);
            _fileId = await _store.CreateFileAsync(fileInfo.Length, null, CancellationToken.None);
            await _store.AppendDataAsync(_fileId, File.OpenRead(SmallFileName), CancellationToken.None);

            // verify the blob content matches the original file
            var blobClient = new AppendBlobClient(_connectionString, ContainerName, _fileId);
            var blobStream = await blobClient.OpenReadAsync();
            Assert.Equal(fileInfo.Length, blobStream.Length);

            // verify checksum validates
            byte[] checksum;
            using (var md5 = MD5.Create())
            using (var stream = File.OpenRead(SmallFileName))
            {
                checksum = md5.ComputeHash(stream);
            }

            Assert.True(await _store.VerifyChecksumAsync(_fileId, "md5", checksum, CancellationToken.None));
        }

        [Fact]
        public async Task checksum_also_inherits_all_base_store_operations()
        {
            var fileInfo = new FileInfo(SmallFileName);
            _fileId = await _store.CreateFileAsync(fileInfo.Length, null, CancellationToken.None);

            // verify base store operations work through the subclass
            Assert.True(await _store.FileExistAsync(_fileId, CancellationToken.None));
            Assert.Equal(fileInfo.Length, await _store.GetUploadLengthAsync(_fileId, CancellationToken.None));
            Assert.Equal(0, await _store.GetUploadOffsetAsync(_fileId, CancellationToken.None));

            await _store.AppendDataAsync(_fileId, File.OpenRead(SmallFileName), CancellationToken.None);
            Assert.Equal(fileInfo.Length, await _store.GetUploadOffsetAsync(_fileId, CancellationToken.None));
        }
    }
}
