using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Configuration;
using Xtensible.Time;
using Xunit;

namespace Xtensible.TusDotNet.Azure.Tests
{
    public class BlobStorageTests
    {
        public BlobStorageTests()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appSettings.json", true)
                .AddJsonFile("appSettings.local.json", true)
                .AddEnvironmentVariables()
                .Build();

            _connectionString = config.GetConnectionString("AzureBlobStorage");
            _azureBlobTusStore = new AzureBlobTusStore(_connectionString, ContainerName);
      
            EnsureTestFiles();
            Clock.Default = new MockClock(new DateTimeOffset(2022, 7, 1, 6, 44, 32, TimeSpan.Zero));
        }

        private const string SmallFileName = "small.txt";
        private const string LargeFileName = "large.txt";
        private const string ContainerName = "upload-tests";

        private readonly string _connectionString;
        private readonly AzureBlobTusStore _azureBlobTusStore;

        private void EnsureTestFiles()
        {
            const int smallFileSize = 4_096;
            const int largeFileSize = 4_299_182; // will span more than 1 block size

            if (!File.Exists(SmallFileName))
            {
                CreateFile(SmallFileName, smallFileSize);
                CreateFile(LargeFileName, largeFileSize);
            }
        }

        private void CreateFile(string filename, int filesize)
        {
            using var sw = new StreamWriter(filename);
            for (var i = 0; i < filesize; i++)
            {
                sw.Write('1');
            }
        }

        private AppendBlobClient GetAppendBlobClient(string blobName) => new AppendBlobClient(_connectionString, ContainerName, blobName);
        private string? GetMetadata(params (string Key, string Value)[] metadata) => metadata.Length == 0 ? null : string.Join(",", metadata
            .Select(md =>
                $"{md.Key.Replace(" ", "").Replace(",", "")} {Convert.ToBase64String(Encoding.UTF8.GetBytes(md.Value))}"));

        private string Base64ToString(string val)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(val));
        }

        [Fact]
        public async Task create_file()
        {
            var fileInfo = new FileInfo(SmallFileName);
            var id = await _azureBlobTusStore.CreateFileAsync(fileInfo.Length, GetMetadata(("test","1"), ("a","b"), ("test-id", nameof(delete_file))), CancellationToken.None);

            // assert that the file record is there and that the metadata is there
            var client = GetAppendBlobClient(id);
            var properties = await client.GetPropertiesAsync();
            Assert.NotNull(properties.Value);
            var metaData = properties.Value.Metadata["RawMetadata"].Split(',').Select(s =>
            {
                var pair = s.Split(' ');
                return (key:pair[0], value:Base64ToString(pair[1]));
            }).ToArray();

            Assert.Equal("test", metaData[0].key);
            Assert.Equal("1", metaData[0].value);

            Assert.Equal("a", metaData[1].key);
            Assert.Equal("b", metaData[1].value);

            await _azureBlobTusStore.DeleteFileAsync(id, CancellationToken.None);

        }


        [Fact]
        public async Task delete_file()
        {
            var fileInfo = new FileInfo(SmallFileName);
            var id = await _azureBlobTusStore.CreateFileAsync(fileInfo.Length, GetMetadata(("test", "1"), ("a", "b"), ("test-id", nameof(delete_file))), CancellationToken.None);
            await _azureBlobTusStore.DeleteFileAsync(id, CancellationToken.None);
            var blobClient = GetAppendBlobClient(id);
            var exists = await blobClient.ExistsAsync(CancellationToken.None);
            Assert.False(exists.Value);
        }

        [Theory]
        [InlineData(SmallFileName)]
        [InlineData(LargeFileName)]
        public async Task append_file(string filename)
        {
            var fileInfo = new FileInfo(filename);
            var id = await _azureBlobTusStore.CreateFileAsync(fileInfo.Length, GetMetadata(("filename", filename), ("test-id", nameof(append_file))), CancellationToken.None);
            await _azureBlobTusStore.AppendDataAsync(id, File.OpenRead(filename), CancellationToken.None);
            
            
            var blobClient = GetAppendBlobClient(id);
            var exists = await blobClient.ExistsAsync(CancellationToken.None);
            Assert.True(exists.Value);

            var blobStream = await blobClient.OpenReadAsync();

            Assert.Equal(blobStream.Length, fileInfo.Length);

            await _azureBlobTusStore.DeleteFileAsync(id, CancellationToken.None);
        }

        [Fact]
        public async Task expire_file()
        {
            var fileInfo = new FileInfo(SmallFileName);
            var id = await _azureBlobTusStore.CreateFileAsync(fileInfo.Length, GetMetadata(("filename", SmallFileName), ("test-id", nameof(append_file))), CancellationToken.None);
            await _azureBlobTusStore.SetExpirationAsync(id, Clock.Default.UtcNow.Subtract(TimeSpan.FromHours(1)), CancellationToken.None);

            var expiration = await _azureBlobTusStore.GetExpirationAsync(id, CancellationToken.None);
            Assert.Equal(Clock.Default.UtcNow.Subtract(TimeSpan.FromHours(1)), expiration);

            await Task.Delay(2000); // adding delay because blob api seems to be lagging a bit
            var expiringFiles = await _azureBlobTusStore.GetExpiredFilesAsync(CancellationToken.None);
            Assert.NotEmpty(expiringFiles);
            await _azureBlobTusStore.RemoveExpiredFilesAsync(CancellationToken.None);
            await Task.Delay(2000);
            expiringFiles = await _azureBlobTusStore.GetExpiredFilesAsync(CancellationToken.None);
            Assert.Empty(expiringFiles);

        }
    }
}