using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using tusdotnet.Models;

namespace Xtensible.TusDotNet.Azure
{
    public class AzureBlobTusStoreOptions
    {
        public ITusExpirationDetailsStore ExpirationDetailsStore { get; set; } = default;
        public MetadataParsingStrategy MetadataParsingStrategy { get; set; } = MetadataParsingStrategy.Original;
        public int MaxDegreeOfDeleteParallelism { get; set; } = 4;
        public bool IsContainerPublic { get; set; } = false;
        public Func<string, Task<string>> FileIdGeneratorAsync { get; set; } = default;
        public string BlobPath { get; set; } = "";
        public Action<Dictionary<string, string>> UpdateAzureMeta { get; set; } = default;
        public AzureBlobTusStoreAuthenticationMode AuthenticationMode { get; set; } = AzureBlobTusStoreAuthenticationMode.ConnectionString;
    }
}