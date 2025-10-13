# SSE-C Encryption Support Implementation

This document describes the SSE-C (Server-Side Encryption with Customer-Provided Keys) implementation for ArquivoMate2.

## Overview

SSE-C is a server-side encryption method where the client provides the encryption key with each request. The S3-compatible storage server encrypts the object using the provided key but does not store the key itself. This means:

- The client must provide the same key for every GET, PUT, COPY, and HEAD operation
- The key is sent via HTTP headers with each request
- **Presigned URLs with SSE-C are technically possible but require special handling** (see below)

### Important: SSE-C and Presigned URLs

**Technical Feasibility:** AWS S3 does support presigned URLs with SSE-C, BUT:

1. **The client must send SSE-C headers with the request**, even when using a presigned URL
2. **Browsers cannot automatically include custom headers** with simple `<a>` or `<img>` tags
3. **Requires JavaScript** with `fetch()` API to add the required headers
4. **Security concerns:** The encryption key must be available in the frontend code

**Required Headers for GET with Presigned URL + SSE-C:**
```
X-Amz-Server-Side-Encryption-Customer-Algorithm: AES256
X-Amz-Server-Side-Encryption-Customer-Key: <base64-key>
X-Amz-Server-Side-Encryption-Customer-Key-MD5: <base64-md5>
```

**Why We Use Server-Proxy Instead:**

? **Better Security:** Encryption key stays on the server  
? **Simpler Implementation:** No complex CORS configuration needed  
? **Browser Compatibility:** Works in all browsers without JavaScript requirements  
? **Access Control:** Server can enforce permissions before streaming  
? **No Key Exposure:** Client never sees the encryption key

**For these reasons, this implementation uses a server-proxy pattern** where the S3DeliveryProvider returns a marker URL (`sse-c://{path}`) that the API server intercepts and streams directly using the StorageProvider (which has the encryption key).

## Implementation Details

### 1. Encryption Type Enum

**File:** `src\ArquivoMate2.Shared\Models\DocumentEncryptionType.cs`

```csharp
[Flags]
public enum DocumentEncryptionType
{
    None = 0,           // No encryption
    ClientSide = 1,     // Application-managed client-side encryption (existing)
    SseC = 2            // Server-Side Encryption with Customer-Provided Keys (new)
}
```

**Important:** This is a **Flags enum** that supports combining multiple encryption types:

```csharp
// Single encryption
var type1 = DocumentEncryptionType.ClientSide;

// Multiple encryption layers
var type2 = DocumentEncryptionType.ClientSide | DocumentEncryptionType.SseC;

// Check for specific encryption
if (type2.HasFlag(DocumentEncryptionType.SseC))
{
    // SSE-C is enabled
}
```

**Use Cases for Combined Encryption:**
- **ClientSide + SseC**: Double encryption - application encrypts before storage, then S3 encrypts again
  - Maximum security: Two independent encryption layers
  - Keys managed separately: Application master key + S3 customer key
  - Defense in depth: Compromise of one layer doesn't expose data

### 2. Domain Model Updates

#### Document Aggregate
**File:** `src\ArquivoMate2.Domain\Document\Document.cs`

- Added `EncryptionType` property of type `DocumentEncryptionType`
- Added `Apply(DocumentEncryptionTypeSet)` method to handle encryption type events

#### Document View (Read Model)
**File:** `src\ArquivoMate2.Domain\ReadModels\DocumentView.cs`

- Added `EncryptionType` property (stored as `int` for database compatibility)

#### DocumentDto
**File:** `src\ArquivoMate2.Shared\Models\DocumentDto.cs`

- Added `EncryptionType` property to expose encryption type via API

### 3. Domain Events

**File:** `src\ArquivoMate2.Domain\Document\DocumentEncryptionTypeSet.cs`

```csharp
public record DocumentEncryptionTypeSet(Guid AggregateId, int EncryptionType, DateTime OccurredOn) : IDomainEvent;
```

This event is raised when the encryption type is set for a document.

**Important:** The `EncryptionType` parameter is stored as `int` to support flags:

```csharp
// Single encryption
var singleEvent = new DocumentEncryptionTypeSet(
    documentId, 
    (int)DocumentEncryptionType.SseC, 
    DateTime.UtcNow
);

// Multiple encryption layers (ClientSide + SseC)
var combinedEvent = new DocumentEncryptionTypeSet(
    documentId, 
    (int)(DocumentEncryptionType.ClientSide | DocumentEncryptionType.SseC), 
    DateTime.UtcNow
);

// Check flags when reading
var encType = (DocumentEncryptionType)event.EncryptionType;
if (encType.HasFlag(DocumentEncryptionType.SseC))
{
    // Handle SSE-C
}
if (encType.HasFlag(DocumentEncryptionType.ClientSide))
{
    // Handle client-side encryption
}
```

### 4. Configuration Classes

#### SseCConfiguration
**File:** `src\ArquivoMate2.Infrastructure\Configuration\StorageProvider\SseCConfiguration.cs`

```csharp
public class SseCConfiguration
{
    public bool Enabled { get; set; } = false;
    public string CustomerKeyBase64 { get; set; } = string.Empty;
    
    public void Validate()
    {
        // Validates that the key is a valid 256-bit (32 byte) Base64 string
    }
}
```

#### Updated Settings Classes

- **S3StorageProviderSettings**: Added `SseC` property
- **S3DeliveryProviderSettings**: Added `SseC` property
- **S3IngestionProviderSettings**: Added `SseC` property

### 5. S3 Provider Implementations

#### S3StorageProvider
**Files:** 
- `src\ArquivoMate2.Infrastructure\Services\StorageProvider\S3StorageProvider.cs`
- `src\ArquivoMate2.Infrastructure.Services\StorageProvider\S3StorageProvider.cs`

**Changes:**
- Validates SSE-C configuration in constructor
- Creates MinIO `SSEC` object from customer key
- Applied SSE-C to all PUT and GET operations using `WithServerSideEncryption(_ssec)`

**Implementation:**
```csharp
using Minio.DataModel.Encryption;

private readonly SSEC? _ssec;

// In constructor:
if (_settings.SseC?.Enabled == true)
{
    var key = Convert.FromBase64String(_settings.SseC.CustomerKeyBase64);
    _ssec = new SSEC(key);
}

// In SaveFileAsync:
if (_ssec != null)
{
    putObjectArgs = putObjectArgs.WithServerSideEncryption(_ssec);
}

// In GetFileAsync:
if (_ssec != null)
{
    args = args.WithServerSideEncryption(_ssec);
}
```

#### S3IngestionProvider
**File:** `src\ArquivoMate2.Infrastructure\Services\IngestionProvider\S3IngestionProvider.cs`

**Changes:**
- Validates SSE-C configuration in constructor
- Creates MinIO `SSEC` and `SSECopy` objects from customer key
- Applied SSE-C to all S3 operations (PUT, GET, COPY)

**Implementation:**
```csharp
using Minio.DataModel.Encryption;

private readonly SSEC? _ssec;
private readonly SSECopy? _ssecCopy;

// In constructor:
if (_settings.SseC?.Enabled == true)
{
    var key = Convert.FromBase64String(_settings.SseC.CustomerKeyBase64);
    _ssec = new SSEC(key);
    _ssecCopy = new SSECopy(key);
}

// For COPY operations:
private async Task CopyObjectAsync(string sourceKey, string destinationKey, CancellationToken ct)
{
    var source = new CopySourceObjectArgs()
        .WithBucket(_settings.BucketName)
        .WithObject(sourceKey);

    // Apply SSE-C for source
    if (_ssecCopy != null)
    {
        source = source.WithServerSideEncryption(_ssecCopy);
    }

    var args = new CopyObjectArgs()
        .WithBucket(_settings.BucketName)
        .WithObject(destinationKey)
        .WithCopyObjectSource(source);

    // Apply SSE-C for destination
    if (_ssec != null)
    {
        args = args.WithServerSideEncryption(_ssec);
    }

    await _minioClient.CopyObjectAsync(args, ct);
}
```

#### S3DeliveryProvider
**File:** `src\ArquivoMate2.Infrastructure\Services\DeliveryProvider\S3DeliveryProvider.cs`

**Changes:**
- Validates SSE-C configuration in constructor
- Returns a special marker URL `sse-c://{fullPath}` when SSE-C is enabled

**Important:** SSE-C encrypted objects **cannot use presigned URLs** because the encryption key must be provided in request headers. When SSE-C is enabled, the delivery provider returns a marker URL that should be intercepted by the API server's delivery controller to stream the content directly (with SSE-C headers).

### 6. Projection Updates

**File:** `src\ArquivoMate2.Infrastructure\Persistance\DocumentProjection.cs`

Added handler for `DocumentEncryptionTypeSet` event:

```csharp
public void Apply(DocumentEncryptionTypeSet e, DocumentView view)
{
    view.EncryptionType = e.EncryptionType;
    if (e.EncryptionType != 0) // DocumentEncryptionType.None
    {
        view.Encrypted = true;
    }
    view.OccurredOn = e.OccurredOn;
}
```

### 7. Mapping Updates

**File:** `src\ArquivoMate2.Infrastructure\Mapping\DocumentMapping.cs`

Added mapping for `EncryptionType` property:

```csharp
.ForMember(d => d.EncryptionType, o => o.MapFrom(s => (DocumentEncryptionType)s.EncryptionType))
```

## Configuration Example

### appsettings.json

```json
{
  "StorageProvider": {
    "Type": "S3",
    "RootPath": "documents",
    "Args": {
      "AccessKey": "your-access-key",
      "SecretKey": "your-secret-key",
      "Endpoint": "s3.hetzner.cloud",
      "BucketName": "your-bucket",
      "Region": "eu-central",
      "SseC": {
        "Enabled": true,
        "CustomerKeyBase64": "your-base64-encoded-32-byte-key"
      }
    }
  },
  "IngestionProvider": {
    "Type": "S3",
    "Args": {
      "AccessKey": "your-access-key",
      "SecretKey": "your-secret-key",
      "Endpoint": "s3.hetzner.cloud",
      "BucketName": "your-bucket",
      "Region": "eu-central",
      "SseC": {
        "Enabled": true,
        "CustomerKeyBase64": "your-base64-encoded-32-byte-key"
      }
    }
  },
  "DeliveryProvider": {
    "Type": "S3",
    "Args": {
      "AccessKey": "your-access-key",
      "SecretKey": "your-secret-key",
      "Endpoint": "s3.hetzner.cloud",
      "BucketName": "your-bucket",
      "Region": "eu-central",
      "SseC": {
        "Enabled": true,
        "CustomerKeyBase64": "your-base64-encoded-32-byte-key"
      }
    }
  }
}
```

### Generating an SSE-C Key

You can generate a valid 256-bit key using PowerShell:

```powershell
$bytes = New-Object byte[] 32
[System.Security.Cryptography.RNGCryptoServiceProvider]::Create().GetBytes($bytes)
$base64Key = [Convert]::ToBase64String($bytes)
Write-Host "SSE-C Key (Base64): $base64Key"
```

Or using OpenSSL:

```bash
openssl rand -base64 32
```

## Usage Workflow

### When Uploading a Document

1. The application determines which encryption method to use based on configuration
2. If SSE-C is enabled, MinIO SDK automatically includes SSE-C headers with uploads
3. A `DocumentEncryptionTypeSet` event is raised with `EncryptionType = 2 (SseC)`
4. The document aggregate and read model are updated

### When Retrieving a Document

1. **Via Storage Provider**: MinIO SDK automatically includes SSE-C headers in GET requests
2. **Via Delivery Provider**: 
   - If SSE-C is enabled, returns `sse-c://{path}` marker URL
   - The API delivery controller should detect this and stream the file directly using the storage provider
   - Presigned URLs are not used for SSE-C encrypted objects

### When Processing Documents

All background processing (thumbnail generation, PDF conversion, etc.) automatically works with SSE-C because the storage provider transparently uses the MinIO SDK's SSE-C support for all operations.

## MinIO SDK SSE-C Support

The implementation uses MinIO SDK's built-in SSE-C classes instead of manual header management:

### Classes Used

- **`Minio.DataModel.Encryption.SSEC`**: For PUT and GET operations
- **`Minio.DataModel.Encryption.SSECopy`**: For COPY source operations

### Benefits

1. **Automatic Header Management**: The SDK handles all required headers (Algorithm, Key, Key-MD5)
2. **Type Safety**: Compile-time validation of encryption parameters
3. **Consistency**: Same encryption implementation across all S3 operations
4. **Maintainability**: SDK updates automatically include security improvements

### Example Usage

```csharp
// Create encryption objects once in constructor
var key = Convert.FromBase64String(customerKeyBase64);
var ssec = new SSEC(key);
var ssecCopy = new SSECopy(key);

// Use with PUT
var putArgs = new PutObjectArgs()
    .WithBucket(bucket)
    .WithObject(path)
    .WithStreamData(stream)
    .WithServerSideEncryption(ssec);

// Use with GET
var getArgs = new GetObjectArgs()
    .WithBucket(bucket)
    .WithObject(path)
    .WithServerSideEncryption(ssec);

// Use with COPY
var sourceArgs = new CopySourceObjectArgs()
    .WithBucket(bucket)
    .WithObject(sourcePath)
    .WithServerSideEncryption(ssecCopy);

var copyArgs = new CopyObjectArgs()
    .WithBucket(bucket)
    .WithObject(destPath)
    .WithCopyObjectSource(sourceArgs)
    .WithServerSideEncryption(ssec);
```

## Security Considerations

1. **Key Management**: The SSE-C key is stored in application configuration. Consider using:
   - Azure Key Vault
   - AWS Secrets Manager
   - Kubernetes Secrets
   - Environment variables with proper access controls

2. **Key Rotation**: To rotate SSE-C keys:
   - Upload a new copy of each object with the new key
   - Delete the old version
   - Update the configuration with the new key

3. **Transport Security**: Always use HTTPS/TLS when sending SSE-C keys to prevent key interception

4. **Backup**: The encryption key must be backed up securely. If lost, encrypted data cannot be recovered.

## Compatibility

- **Client-Side Encryption**: The existing client-side encryption (EncryptionType.ClientSide) continues to work independently
- **No Encryption**: Objects can still be stored without encryption (EncryptionType.None)
- **Coexistence**: Different documents can use different encryption methods within the same application
- **Combined Encryption**: Multiple encryption types can be enabled simultaneously using flags

### Encryption Scenarios

**Scenario 1: No Encryption**
```csharp
EncryptionType = DocumentEncryptionType.None
// Document stored in plain text
```

**Scenario 2: Client-Side Only**
```csharp
EncryptionType = DocumentEncryptionType.ClientSide
// Application encrypts with master key before storage
// S3 stores encrypted blob
```

**Scenario 3: SSE-C Only**
```csharp
EncryptionType = DocumentEncryptionType.SseC
// Application sends plain data with SSE-C headers
// S3 encrypts on server-side with customer key
```

**Scenario 4: Double Encryption (Defense in Depth)**
```csharp
EncryptionType = DocumentEncryptionType.ClientSide | DocumentEncryptionType.SseC
// 1. Application encrypts with master key (ClientSide)
// 2. S3 encrypts the already-encrypted blob with customer key (SSE-C)
// Result: Two independent encryption layers
```

### Checking Encryption Status

```csharp
// Check if any encryption is enabled
if (doc.EncryptionType != DocumentEncryptionType.None)
{
    // Document is encrypted
}

// Check for specific encryption type
if (doc.EncryptionType.HasFlag(DocumentEncryptionType.SseC))
{
    // SSE-C is enabled
}

// Check for multiple encryption
if (doc.EncryptionType.HasFlag(DocumentEncryptionType.ClientSide) && 
    doc.EncryptionType.HasFlag(DocumentEncryptionType.SseC))
{
    // Both encryption methods are active
}

// Using extension methods
using ArquivoMate2.Application.Extensions;

if (doc.EncryptionType.HasClientSideEncryption())
{
    // Client-side encryption is enabled
}

if (doc.EncryptionType.HasSseCEncryption())
{
    // SSE-C encryption is enabled
}
```

## Testing

To test SSE-C functionality:

1. Configure SSE-C in appsettings.json
2. Upload a test document
3. Verify the document is encrypted server-side by trying to download it without the key (should fail)
4. Verify the application can retrieve the document (using the configured key)
5. Test copy operations (ingestion workflow)
6. Test delivery (should return sse-c:// marker URL)

## References

- MinIO .NET SDK Documentation: https://github.com/minio/minio-dotnet
- Hetzner SSE-C Documentation: https://docs.hetzner.com/storage/object-storage/howto-protect-objects/encrypt-with-sse-c/
- AWS S3 SSE-C Documentation: https://docs.aws.amazon.com/AmazonS3/latest/userguide/ServerSideEncryptionCustomerKeys.html
