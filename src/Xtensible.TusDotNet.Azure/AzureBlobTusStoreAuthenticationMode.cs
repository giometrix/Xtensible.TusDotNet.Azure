using System;
using System.Collections.Generic;
using System.Text;

namespace Xtensible.TusDotNet.Azure
{
    /// <summary>
    /// Modes for how the library authenticates with the Azure storage account.
    /// ConnectionString: Use a connection string with either a Shared Access Signature (SAS) or access key. See https://learn.microsoft.com/en-us/azure/storage/common/storage-configure-connection-string.
    /// SystemAssignedManagedIdentity: Authenticate using Azure system-assigned managed identity. See https://learn.microsoft.com/en-us/entra/identity/managed-identities-azure-resources/overview.
    /// </summary>
    public enum AzureBlobTusStoreAuthenticationMode
    {
        ConnectionString = 0,
        SystemAssignedManagedIdentity = 1
    }
}
