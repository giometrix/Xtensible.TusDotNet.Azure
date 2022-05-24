using System;
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
    }
}