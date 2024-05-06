using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Xtensible.TusDotNet.Azure
{
    internal static class AzureBlobClientFactory
    {
        public static BlobServiceClient CreateBlobServiceClient(AzureBlobTusStoreAuthenticationMode authenticationMode, string connectionString)
        {

            if (authenticationMode == AzureBlobTusStoreAuthenticationMode.SystemAssignedManagedIdentity)
            {
                var connectionStringIsUri = Uri.TryCreate(connectionString, UriKind.Absolute, out var blobStorageUri);
                if (!connectionStringIsUri)
                {
                    throw new ArgumentException("connectionString must be a Azure Blob Storage URI when authentication mode is SystemAssignedManagedIdentity");
                }

                return new BlobServiceClient(blobStorageUri, new DefaultAzureCredential());
            }
            else
            {
                return new BlobServiceClient(connectionString);
            }
        }

        public static AppendBlobClient CreateAppendBlobClient(AzureBlobTusStoreAuthenticationMode authenticationMode, string connectionString, string containerName, string blobPath) 
        {
            if (authenticationMode == AzureBlobTusStoreAuthenticationMode.SystemAssignedManagedIdentity)
            {
                var connectionStringIsUri = Uri.TryCreate(connectionString, UriKind.Absolute, out var blobStorageUri);
                if (!connectionStringIsUri)
                {
                    throw new ArgumentException("connectionString must be a Azure Blob Storage URI when authentication mode is SystemAssignedManagedIdentity");
                }
                return new AppendBlobClient(new Uri(blobStorageUri, $"{containerName}/{blobPath}"), new DefaultAzureCredential());
            }
            else
            {
                return new AppendBlobClient(connectionString, containerName, blobPath);
            }
        }

        public static BlobContainerClient CreateBlobContainerClient(AzureBlobTusStoreAuthenticationMode authenticationMode, string connectionString, string containerName) 
        {
            if (authenticationMode == AzureBlobTusStoreAuthenticationMode.SystemAssignedManagedIdentity)
            {
                var connectionStringIsUri = Uri.TryCreate(connectionString, UriKind.Absolute, out var blobStorageUri);
                if (!connectionStringIsUri)
                {
                    throw new ArgumentException("connectionString must be a Azure Blob Storage URI when authentication mode is SystemAssignedManagedIdentity");
                }
                return new BlobContainerClient(new Uri(blobStorageUri, containerName), new DefaultAzureCredential());
            }
            else
            {
                return new BlobContainerClient(connectionString, containerName);
            }
        }
    }
}
