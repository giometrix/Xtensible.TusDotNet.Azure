using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Configuration;

namespace Xtensible.TusDotNet.Azure.Benchmarks
{
    [MemoryDiagnoser]
    public  class Benchmarks : IDisposable
    {
        private readonly string _filename;
        private readonly AzureBlobTusStore _blobTusStore;

        public Benchmarks()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appSettings.json", true)
                .AddJsonFile("appSettings.local.json", true)
                .AddEnvironmentVariables()
                .Build();

            var filename = "text.txt";


            var connectionString = config.GetConnectionString("AzureBlobStorage");

            _filename = filename;
            EnsureTestFile(filename, 104_857_600);
            _blobTusStore = new AzureBlobTusStore(connectionString, "perf-test");
        }

        [Benchmark]
        public async Task UploadFile()
        {
            var fi = new FileInfo(_filename);
            using var sr = new StreamReader(_filename).BaseStream;
            var id = await _blobTusStore.CreateFileAsync(fi.Length, null, CancellationToken.None);
            await _blobTusStore.AppendDataAsync(id, sr, CancellationToken.None);
        }

        private void EnsureTestFile(string filename, int size)
        {

            if (!File.Exists(filename))
            {
                CreateFile(filename, size);
            }
        }

        private void CreateFile(string filename, int size)
        {
            using var sw = new StreamWriter(filename);
            for (var i = 0; i < size; i++)
            {
                sw.Write('1');
            }
        }

        public void Dispose()
        {
            File.Delete(_filename);
        }
    }
}
