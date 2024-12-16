# Xtensible.TusDotNet.Azure
[![Nuget](https://img.shields.io/nuget/v/Xtensible.TusDotNet.Azure)](https://www.nuget.org/packages/Xtensible.TusDotNet.Azure)

An Azure Blob Storage extension for [tusdotnet](https://github.com/tusdotnet/tusdotnet); .NET's most popular implementation of the [tus](https://tus.io/) protocol.

### What is tus?
[Tus](https://tus.io/) is a web based protocol for resumable uploads.  Implementations and client libraries exist for many platforms.

### What is tusdotnet?
[tusdotnet](https://github.com/tusdotnet/tusdotnet) is a popular implementation of the tus protocol for .net.

### Why do I need Xtensible.TusDotNet.Azure?
[Tusdotnet](https://github.com/tusdotnet/tusdotnet) only comes with a disk storage implementation.  This extension allows you to use blobstorage instead of local (or network attached) disk.

### Implemented Extensions
The tus protocol offers a few [extensions](https://tus.io/protocols/resumable-upload.html#protocol-extensions).  The following extensions are implemented:
* Termination - Allows for deletion of completed and incomplete uploads.
* Expiration - Server will consider uploads past a certain time expired and ready for deletion.

### Why aren't the following supported?
* Checksum - The code for checksum (using md5) is actually written and can be enabled by #defining ENABLE_CHECKSUM.  It is disabled because it only works for upload chunks <= 4MB, which is the buffer size
used to send data to Azure.  Anything beyond 4MB would be inefficient, because we'd have to download the bits from Azure to verify the checksum.  This implementation did not want to force upload limitations on consuming code,
so this extension was disabled.  If you care about this feature and are ok with limiting chunk size to 4MB you can compile with it turned on.

* Pipelines - tusdotnet supports pipelines for .net core 3.1+, which is more efficient than the streams implementation.  I haven't gotten to this yet, but will happily accept pull requests. 


### Azurite
[Azurite](https://github.com/Azure/Azurite) is the latest, and recommended Azure Storage emulator to use for testing and local development.
Unfortunately, it does not yet support tags (see [issue](https://github.com/Azure/Azurite/issues/647)).  This is problematic because tags is how this library queries for expired files.
To work around this, I have split out the expiration stuff into a separate interface `ITusExpirationDetailsStore` and implemented `NullExpirationDetailsStore`.

This allows you to use an external database (for just dev, or both prod and dev), or you can use `NullExpirationDetailsStore` in dev to forego using the expiration feature when using Azurite.

### Version notes

#### Why is the `UploadOffset` property on my upload blob missing or not updating anymore?

In version 3.0.0 the `UploadOffset` property was deprecated in favor of the actual `Content-Length` HTTP property of the blob.

This was done to fix possible upload blob data corruption when resuming uploads after a broken/cancelled connection and has the following (unproblematic) side effects:

* Existing upload blobs created with version < 3.0.0 contain an `UploadOffset` property which is not updated anymore after updating to version ≥ 3.0.0.
* New upload blobs created with version ≥ 3.0.0 do not contain an `UploadOffset` property at all.

#### Can I prevent blob data corruption with versions < 3.x?

Yes, by choosing a TUS chunk size ≤ 4 MiB (4 194 304 bytes). Please note that such small chunk sizes are not recommended because they reduce upload performance.

#### Can I downgrade from version 3.x to 2.x?

**Please don't, there is no reason why you should need or want to.**
Uploads will either fail or become corrupt if you do it anyway.

(It is possible, at your own risk, by making sure you set the `UploadOffset` metadata property on each upload blob to the exact same value as the `Content-Length` property of the blob, after stopping existing uploads and before deploying version 2.x.)