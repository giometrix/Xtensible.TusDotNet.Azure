using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using tusdotnet.Interfaces;
using tusdotnet.Models;
using tusdotnet.Parsers;

namespace Xtensible.TusDotNet.Azure
{
    public class TusAzureBlobFile : ITusFile
    {
        private static readonly BlobOpenReadOptions BlobOpenReadOptions = new(false);
        private static readonly Dictionary<string, Metadata> EmptyDictionary = new();
        private readonly AppendBlobClient _appendBlobClient;
        private readonly MetadataParsingStrategy _metadataParsingStrategy;
        

        public TusAzureBlobFile(string fileId, AppendBlobClient appendBlobClient, MetadataParsingStrategy metadataParsingStrategy)
        {
            Id = fileId;
            _appendBlobClient = appendBlobClient;
            _metadataParsingStrategy = metadataParsingStrategy;
        }

        public string Id { get; }


        public Task<Stream> GetContentAsync(CancellationToken cancellationToken)
        {
            return _appendBlobClient.OpenReadAsync(BlobOpenReadOptions, cancellationToken);
        }

        public async Task<Dictionary<string, Metadata>> GetMetadataAsync(CancellationToken cancellationToken)
        {
            var properties = await _appendBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            if (properties.Value.Metadata.TryGetValue("Metadata", out var blobMetadata))
            {
                var metadataParserResult = MetadataParser.ParseAndValidate(_metadataParsingStrategy, blobMetadata);
                if (metadataParserResult.Success)
                {
                    return metadataParserResult.Metadata;
                }
                throw new InvalidOperationException(metadataParserResult.ErrorMessage);
            }
            return EmptyDictionary;
          
        }
    }
}