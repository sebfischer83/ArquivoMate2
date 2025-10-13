# Encryption Options

This document explains the two encryption layers supported by ArquivoMate2:

- **S3 SSE-C (server-side encryption with customer-provided keys)** protects objects while they are stored in S3-compatible buckets using a key that you manage. The MinIO/S3 service performs the encryption/decryption when objects are uploaded, downloaded, copied, or accessed via presigned URLs.
- **Custom encryption** protects document artifacts at the application level. Files are encrypted before they are written to storage and decrypted again when they are streamed through the API. The application controls the encryption keys and wrapping logic.

Both features can be enabled independently. The sections below describe what each option does, when to use it, and how to configure the required settings.

## S3 SSE-C customer encryption

### What it does

When SSE-C is enabled the S3-compatible providers send a customer-provided AES-256 key with every storage operation. S3 encrypts the object with this key before persisting it and transparently decrypts it when the object is retrieved again. ArquivoMate2 applies the key automatically for all storage, ingestion, and delivery workflows:

- `S3StorageProvider` attaches the SSE-C parameters for uploads, downloads, and streaming operations so that every artifact stored via the provider is encrypted at rest inside the bucket.【F:src/ArquivoMate2.Infrastructure.Services/StorageProvider/S3StorageProvider.cs†L24-L87】
- `S3IngestionProvider` uses the same key for pending uploads, copy/move operations, and temporary processing files so that ingestion flows remain consistent with the storage layer.【F:src/ArquivoMate2.Infrastructure/Services/IngestionProvider/S3IngestionProvider.cs†L28-L129】
- `S3DeliveryProvider` includes the customer key when generating presigned URLs and forbids mixing SSE-C with public buckets to avoid leaking the key material.【F:src/ArquivoMate2.Infrastructure/Services/DeliveryProvider/S3DeliveryProvider.cs†L22-L68】

Because the S3 service performs the cryptography, SSE-C does not change the way clients interact with the API. However, it requires you to protect and distribute the 256-bit customer key that is sent with every S3 request.

### Configuration

All S3-related configuration objects expose a `CustomerEncryption` section that binds to `S3CustomerEncryptionSettings`.

```jsonc
"StorageProvider": {
  "Type": "S3",
  "Args": {
    "Endpoint": "minio.local",
    "BucketName": "arquivomate",
    "AccessKey": "app",
    "SecretKey": "app-secret",
    "CustomerEncryption": {
      "Enabled": true,
      "CustomerKeyBase64": "<base64 encoded 32 byte key>"
    }
  }
},
"IngestionProvider": {
  "Type": "S3",
  "Args": {
    "Endpoint": "minio.local",
    "BucketName": "arquivomate-ingestion",
    "AccessKey": "app",
    "SecretKey": "app-secret",
    "CustomerEncryption": {
      "Enabled": true,
      "CustomerKeyBase64": "<same base64 key>"
    }
  }
},
"DeliveryProvider": {
  "Type": "S3",
  "Args": {
    "Endpoint": "minio.local",
    "BucketName": "arquivomate",
    "AccessKey": "app",
    "SecretKey": "app-secret",
    "IsPublic": false,
    "CustomerEncryption": {
      "Enabled": true,
      "CustomerKeyBase64": "<same base64 key>"
    }
  }
}
```

Important notes:

- Set `Enabled` to `true` to activate SSE-C. When disabled the key is ignored and requests are sent without SSE-C headers.【F:src/ArquivoMate2.Infrastructure/Configuration/S3CustomerEncryptionSettings.cs†L13-L34】
- `CustomerKeyBase64` must be a base64 string that decodes to 32 bytes (AES-256). The configuration helper validates the length and throws if the value is missing or malformed.【F:src/ArquivoMate2.Infrastructure/Configuration/S3CustomerEncryptionSettings.cs†L18-L47】
- Use the same key across storage, ingestion, and delivery. The MinIO/S3 API rejects operations that do not provide the correct key for objects that were encrypted with SSE-C.

## Custom encryption

### What it does

Custom encryption is an application-level feature that encrypts document artifacts before they are uploaded to the storage provider. When enabled, the `CustomEncryptionService` wraps each artifact using AES-256-CBC with an HMAC-SHA256 integrity check, stores the ciphertext, and returns an envelope that contains the wrapped data encryption key (DEK). The DEK itself is wrapped with the configured master key using AES-GCM.【F:src/ArquivoMate2.Infrastructure/Services/Encryption/CustomEncryptionService.cs†L17-L106】

During downloads the API retrieves the encrypted artifact, unwraps the DEK using the same master key, and decrypts the payload on the fly before streaming it to clients. Access tokens for server-side delivery are signed with the master key so you can control how long download URLs remain valid.【F:src/ArquivoMate2.Infrastructure/Services/Encryption/FileAccessTokenService.cs†L11-L82】

### Configuration

The custom encryption settings are bound from the `CustomEncryption` section and shared across the API via dependency injection.【F:src/ArquivoMate2.Infrastructure/Configuration/DependencyInjectionConfiguration.cs†L370-L372】

```jsonc
"CustomEncryption": {
  "Enabled": true,
  "MasterKeyBase64": "<base64 encoded 32 byte key>",
  "TokenTtlMinutes": 60,
  "CacheTtlMinutes": 30
}
```

- `Enabled`: toggles the feature. When `false`, artifacts are stored in plaintext and the service bypasses encryption logic.【F:src/ArquivoMate2.Infrastructure/Services/Encryption/CustomEncryptionService.cs†L23-L52】
- `MasterKeyBase64`: base64 encoded AES-256 key that wraps the per-artifact DEKs and signs access tokens. A missing or incorrectly sized key causes startup to fail.【F:src/ArquivoMate2.Application/Configuration/CustomEncryptionSettings.cs†L5-L10】【F:src/ArquivoMate2.Infrastructure/Services/Encryption/CustomEncryptionService.cs†L25-L33】
- `TokenTtlMinutes`: controls the lifetime of delivery tokens created by `FileAccessTokenService`. Increase this value if clients need longer-lived download links.【F:src/ArquivoMate2.Application/Configuration/CustomEncryptionSettings.cs†L7-L9】【F:src/ArquivoMate2.Infrastructure/Services/Encryption/FileAccessTokenService.cs†L11-L52】
- `CacheTtlMinutes`: specifies how long decrypted keys can be cached by in-memory providers to avoid repeated unwrap operations.【F:src/ArquivoMate2.Application/Configuration/CustomEncryptionSettings.cs†L7-L10】

### Operational guidance

- Store the master key in a secure secret manager (Azure Key Vault, AWS Secrets Manager, etc.) and inject it into the configuration at runtime.
- Rotate the master key by decrypting existing `EncryptedArtifactKey` records with the current key, re-wrapping with the new key, and updating the stored metadata. Because artifacts are encrypted with unique DEKs, rotation does not require re-uploading files.
- Combine custom encryption with SSE-C when you need both defense-in-depth at the application layer and encrypted-at-rest storage managed by S3. In this scenario the ciphertext produced by the application is additionally protected by SSE-C inside the bucket.

## Key management checklist

- Generate 32-byte keys for both SSE-C and the custom encryption master key. Use a cryptographically secure random source.
- Back up the keys offline following the guidance in `docs/KeyBackup.md` so that encrypted documents remain recoverable.
- Limit access to the keys and rotate them periodically according to your organization's policies.
