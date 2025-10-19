# ArquivoMate2 Configuration Guide

## Overview

This guide provides a comprehensive reference for all configuration options in ArquivoMate2. The application uses `appsettings.json` for base configuration, with environment-specific overrides supported through `appsettings.{Environment}.json` files and environment variables.

## Table of Contents

1. [Configuration Hierarchy](#configuration-hierarchy)
2. [Connection Strings](#connection-strings)
3. [CORS Configuration](#cors-configuration)
4. [Authentication](#authentication)
5. [Storage Provider](#storage-provider)
6. [Delivery Provider](#delivery-provider)
7. [Ingestion Provider](#ingestion-provider)
8. [Caching](#caching)
9. [Search (Meilisearch)](#search-meilisearch)
10. [ChatBot (OpenAI)](#chatbot-openai)
11. [ChatBot (OpenRouter)](#chatbot-openrouter)
12. [Document Processing](#document-processing)
13. [Logging](#logging)
13. [Localization](#localization)
14. [Environment Variables](#environment-variables)
15. [Security Best Practices](#security-best-practices)

---

## Configuration Hierarchy

ArquivoMate2 loads configuration in the following order (later sources override earlier ones):

1. `appsettings.json` (base configuration)
2. `appsettings.{Environment}.json` (environment-specific)
3. User Secrets (Development only)
4. Environment Variables (prefix: `AMate__`)
5. Command-line arguments

### Example Override

```json
// appsettings.json (base)
{
  "Cors": {
    "AllowedOrigins": ["https://localhost:4200"]
  }
}

// appsettings.Production.json (override)
{
  "Cors": {
    "AllowedOrigins": ["https://app.yourdomain.com"]
  }
}
```

---

## Connection Strings

### PostgreSQL Database

```json
{
  "ConnectionString": {
    "Default": "Host=localhost;Database=arquivomate2;Username=postgres;Password=yourpassword",
    "Hangfire": "Host=localhost;Database=arquivomate2_hangfire;Username=postgres;Password=yourpassword",
    "VectorStore": "Host=localhost;Database=arquivomate2_vectors;Username=postgres;Password=yourpassword"
  }
}
```

**Parameters:**

- `Default`: Main application database (Marten event store + read models)
- `Hangfire`: Background job processing database
- `VectorStore`: Vector embeddings for semantic search (optional)

**Environment Variable Format:**
```bash
AMate__ConnectionString__Default="Host=db;Database=arquivomate2;Username=postgres;Password=secret"
AMate__ConnectionString__Hangfire="Host=db;Database=arquivomate2_hangfire;Username=postgres;Password=secret"
```

**Security:**
- ?? Never commit passwords to git
- ? Use environment variables in production
- ? Use Azure Key Vault / AWS Secrets Manager for production secrets

---

## CORS Configuration

Controls which frontend domains can access the API.

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://localhost:4200",
      "http://localhost:4200",
      "https://app.yourdomain.com"
    ]
  }
}
```

**Properties:**

- `AllowedOrigins`: Array of allowed frontend URLs
  - Must include protocol (`https://` or `http://`)
  - No trailing slashes
  - Port number required if non-standard

**Environment Variables:**
```bash
AMate__Cors__AllowedOrigins__0=https://app.yourdomain.com
AMate__Cors__AllowedOrigins__1=https://admin.yourdomain.com
```

**Security:**
- ? Use HTTPS in production
- ? Specify exact domains (no wildcards)
- ? Never use `*` in production
- ? Don't mix HTTP/HTTPS in production

**Default Behavior:**
If `Cors:AllowedOrigins` is not configured, defaults to:
```json
["https://localhost:4200", "http://localhost:4200"]
```

**See Also:** [CORS-Configuration.md](./CORS-Configuration.md)

---

## Authentication

ArquivoMate2 supports OpenID Connect (OIDC) / OAuth2 authentication.

```json
{
  "Auth": {
    "Type": "OIDC",
    "Args": {
      "ClientId": "arquivomate2-api",
      "Authority": "https://auth.yourdomain.com",
      "Audience": "arquivomate2-api",
      "Issuer": "https://auth.yourdomain.com",
      "CookieDomain": ".yourdomain.com"
    }
  }
}
```

**Properties:**

- `Type`: Authentication provider type
  - `OIDC`: OpenID Connect (Keycloak, Auth0, Azure AD, etc.)
  
- `ClientId`: OAuth2 client identifier registered with your provider
- `Authority`: OIDC provider base URL (used for discovery)
- `Audience`: Expected audience claim in JWT tokens
- `Issuer`: Expected issuer claim in JWT tokens
- `CookieDomain`: (Optional) Cookie domain for cross-subdomain auth

**Supported Authentication Flows:**

1. **JWT Bearer Token** (for API calls)
   - Add header: `Authorization: Bearer <token>`
   
2. **Cookie Authentication** (for browser sessions)
   - Automatically used when no Authorization header present

**Token Validation:**

The API validates:
- ? Token signature (using OIDC provider's signing keys)
- ? Issuer matches configured value
- ? Audience matches configured value
- ? Token expiration (with 2-minute clock skew tolerance)

**Example Providers:**

### Keycloak
```json
{
  "Authority": "https://keycloak.yourdomain.com/realms/arquivomate",
  "Issuer": "https://keycloak.yourdomain.com/realms/arquivomate",
  "Audience": "arquivomate2-api",
  "ClientId": "arquivomate2-api"
}
```

### Auth0
```json
{
  "Authority": "https://yourtenant.auth0.com",
  "Issuer": "https://yourtenant.auth0.com/",
  "Audience": "https://api.yourdomain.com",
  "ClientId": "your-auth0-client-id"
}
```

### Azure AD
```json
{
  "Authority": "https://login.microsoftonline.com/<tenant-id>/v2.0",
  "Issuer": "https://login.microsoftonline.com/<tenant-id>/v2.0",
  "Audience": "api://arquivomate2-api",
  "ClientId": "<application-id>"
}
```

**Security:**
- ?? Always use HTTPS for Authority/Issuer in production
- ? Verify Issuer/Audience match your OIDC provider exactly
- ? Test token validation with actual tokens from your provider

---

## Storage Provider

Manages primary document storage (original files + derivatives).

### S3-Compatible Storage (Recommended)

```json
{
  "StorageProvider": {
    "Type": "S3",
    "RootPath": "documents",
    "Args": {
      "BucketName": "arquivomate2-documents",
      "Region": "eu-central-1",
      "AccessKey": "your-access-key",
      "SecretKey": "your-secret-key",
      "Endpoint": "s3.eu-central-1.amazonaws.com",
      "IsPublic": false,
      "SseC": {
        "Enabled": true,
        "CustomerKeyBase64": "your-32-byte-key-base64-encoded"
      }
    }
  }
}
```

**Properties:**

- `Type`: Provider type (`S3`)
- `RootPath`: Root prefix for all documents in bucket
- `BucketName`: S3 bucket name
- `Region`: AWS region or MinIO region
- `AccessKey`: S3 access key ID
- `SecretKey`: S3 secret access key
- `Endpoint`: S3 endpoint URL
  - AWS: `s3.{region}.amazonaws.com`
  - MinIO: `minio.yourdomain.com:9000`
- `IsPublic`: Whether bucket allows public read access
  - `false`: Private bucket (recommended)
  - `true`: Public bucket (not recommended for production)

**SSE-C Encryption (Optional):**

Server-Side Encryption with Customer-Provided Keys provides transparent storage encryption.

- `Enabled`: Enable SSE-C encryption
- `CustomerKeyBase64`: Base64-encoded 256-bit (32-byte) encryption key

**Generating SSE-C Key:**
```bash
# Generate random 32-byte key and encode as base64
openssl rand -base64 32
# Example output: GOGv5lrShhpi7JBHkbJdIpH/g1elDym2+mPHpbc/YgA=
```

**Storage Provider Types:**

| Provider | Endpoint Example | Notes |
|----------|------------------|-------|
| **AWS S3** | `s3.eu-central-1.amazonaws.com` | Production-grade, global CDN |
| **MinIO** | `minio.local:9000` | Self-hosted S3-compatible |
| **Backblaze B2** | `s3.eu-central-003.backblazeb2.com` | Cost-effective S3-compatible |
| **DigitalOcean Spaces** | `fra1.digitaloceanspaces.com` | Simple S3-compatible |
| **Wasabi** | `s3.eu-central-1.wasabisys.com` | Fast S3-compatible |

**Environment Variables:**
```bash
AMate__StorageProvider__Args__AccessKey=your-access-key
AMate__StorageProvider__Args__SecretKey=your-secret-key
AMate__StorageProvider__Args__SseC__CustomerKeyBase64=your-encryption-key
```

**Security:**
- ?? Never commit access keys or encryption keys to git
- ? Use IAM roles (AWS) or service accounts where possible
- ? Rotate encryption keys regularly (requires migration)
- ? Set `IsPublic: false` in production

**See Also:** [SSE-C-Implementation.md](./SSE-C-Implementation.md)

---

## Delivery Provider

Controls how documents are delivered to end users (URLs for downloads).

### S3 Delivery (Recommended)

```json
{
  "DeliveryProvider": {
    "Type": "S3",
    "Args": {
      "BucketName": "arquivomate2-documents",
      "Region": "eu-central-1",
      "AccessKey": "your-access-key",
      "SecretKey": "your-secret-key",
      "Endpoint": "s3.eu-central-1.amazonaws.com",
      "IsPublic": false,
      "SseC": {
        "Enabled": true,
        "CustomerKeyBase64": "your-32-byte-key-base64-encoded"
      }
    }
  }
}
```

**Properties:**

Same as Storage Provider (usually same bucket).

**Delivery Modes:**

1. **Presigned URLs** (when `SseC.Enabled = false`)
   - Generates time-limited signed URLs
   - Valid for 1 hour
   - Cached for 55 minutes
   - Direct access from browser

2. **Server Delivery** (when `SseC.Enabled = true`)
   - SSE-C encrypted objects cannot use presigned URLs
   - Returns special marker: `sse-c://{path}`
   - Frontend must use `/api/delivery/...` endpoint
   - API decrypts and streams content

**Provider Types:**

| Type | Behavior | Use Case |
|------|----------|----------|
| `S3` | Presigned URLs or server delivery | Production (recommended) |
| `Server` | Always proxy through API | Development, SSE-C |
| `Noop` | Returns raw storage paths | Testing only |
| `Bunny` | BunnyCDN signed URLs | CDN with token auth |

**Environment Variables:**
```bash
AMate__DeliveryProvider__Type=S3
AMate__DeliveryProvider__Args__IsPublic=false
```

**Performance:**
- ? Presigned URLs: Best performance (direct S3 access)
- ?? Server Delivery: Higher server load, required for SSE-C
- ? Cache presigned URLs aggressively

---

## Ingestion Provider

Manages automatic document import from file system or S3 bucket.

### File System Ingestion

```json
{
  "IngestionProvider": {
    "Type": "FileSystem",
    "Args": {
      "RootPath": "/var/arquivomate2/ingestion",
      "ProcessingSubfolderName": "processing",
      "ProcessedSubfolderName": "processed",
      "FailedSubfolderName": "failed",
      "PollingInterval": "00:05:00"
    }
  }
}
```

**File Structure:**
```
/var/arquivomate2/ingestion/
??? user-id-1/
?   ??? document1.pdf       ? New files here
?   ??? processing/         ? Currently being processed
?   ??? processed/          ? Successfully imported
?   ??? failed/             ? Import failed
??? user-id-2/
    ??? ...
```

**Properties:**

- `Type`: Provider type (`FileSystem` or `S3`)
- `RootPath`: Base directory for ingestion
- `ProcessingSubfolderName`: Subfolder for in-progress files
- `ProcessedSubfolderName`: Subfolder for completed files
- `FailedSubfolderName`: Subfolder for failed imports
- `PollingInterval`: How often to check for new files (format: `hh:mm:ss`)

**Workflow:**

1. User places file in `/ingestion/{userId}/document.pdf`
2. Background job detects file every 5 minutes
3. Moves to `processing/` during import
4. On success: Moves to `processed/`
5. On failure: Moves to `failed/` with `.error.txt` file

### S3 Ingestion

```json
{
  "IngestionProvider": {
    "Type": "S3",
    "Args": {
      "BucketName": "arquivomate2-ingestion",
      "Region": "eu-central-1",
      "AccessKey": "your-access-key",
      "SecretKey": "your-secret-key",
      "Endpoint": "s3.eu-central-1.amazonaws.com",
      "UseSsl": true,
      "RootPrefix": "ingestion",
      "ProcessingSubfolderName": "processing",
      "ProcessedSubfolderName": "processed",
      "FailedSubfolderName": "failed",
      "PollingInterval": "00:05:00",
      "IngestionEmail": "ingestion@yourdomain.com",
      "SseC": {
        "Enabled": true,
        "CustomerKeyBase64": "your-32-byte-key-base64-encoded"
      }
    }
  }
}
```

**Additional S3 Properties:**

- `UseSsl`: Use HTTPS for S3 connections
- `RootPrefix`: Root prefix in bucket
- `IngestionEmail`: Default sender email for imported documents

**Disable Ingestion:**

```json
{
  "IngestionProvider": {
    "Type": "Noop"
  }
}
```

### SFTP Ingestion

ArquivoMate2 supports ingesting files from an SFTP server. The SFTP provider mirrors the FileSystem/S3 ingestion workflow: files are placed under a root prefix per user, the ingestion job moves a discovered file into a `processing` subfolder, and on success/failure the file is moved to `processed`/`failed` respectively.

### Example `appsettings.json` snippet (example section)

```json
"IngestionProviderExamples": {
  "Sftp": {
    "Type": "Sftp",
    "Args": {
      "Host": "sftp.example.com",
      "Port": 22,
      "Username": "ingest-user",
      "Password": "<optional-password-or-leave-empty-if-using-key>",
      "PrivateKeyFilePath": "/secrets/sftp/id_rsa",
      "PrivateKeyPassphrase": "<optional-passphrase>",
      "RootPrefix": "ingestion",
      "ProcessingSubfolderName": "processing",
      "ProcessedSubfolderName": "processed",
      "FailedSubfolderName": "failed",
      "PollingInterval": "00:05:00",
      "IngestionEmail": "ingestion@example.com"
    }
  }
}
```

### Properties

- `Host`: SFTP server hostname or IP
- `Port`: SFTP port (default 22)
- `Username`: Account used for ingestion operations
- `Password`: Optional - use for password authentication
- `PrivateKeyFilePath`: Optional - path on the API host to a private key file for key-based auth
- `PrivateKeyPassphrase`: Optional passphrase for the private key file
- `RootPrefix`: Root remote directory containing per-user folders (e.g. `/ingestion`)
- `ProcessingSubfolderName`, `ProcessedSubfolderName`, `FailedSubfolderName`: Subfolders under each user prefix to manage lifecycle
- `PollingInterval`: How often the background ingestion job polls the SFTP server (hh:mm:ss)
- `IngestionEmail`: Optional default sender address used when an ingested file should create an EmailDocument

### Authentication

The provider supports two authentication modes:
1. Password authentication (`Username` + `Password`).
2. Private-key authentication (`PrivateKeyFilePath`, optional `PrivateKeyPassphrase`).

Provide at least one authentication method. If both are present, password authentication will be used by default.

### Remote layout and behavior

Remote layout example for `RootPrefix = "ingestion"`:

```
/ingestion/
  user-1/
    mydoc.pdf             <- new file detected
    processing/
    processed/
    failed/
  user-2/
    ...
```

- When a file is detected in `/ingestion/{userId}/filename`, the ingestion job moves it to `/ingestion/{userId}/processing/filename` (reservation).
- After successful import the file is moved to `processed/`; on failure it is moved to `failed/` and an optional `.error.txt` file with the reason will be created.

### App deployment and secrets

- The SFTP private key file path is resolved on the API host. Place the key in a secure location and reference the absolute path with `PrivateKeyFilePath`.
- Avoid committing passwords or private keys to source control. Use environment variables or secret stores.

Example Docker Compose environment variables pattern:

```yaml
services:
  api:
    environment:
      - AMate__IngestionProvider__Args__Host=sftp.example.com
      - AMate__IngestionProvider__Args__Username=ingest-user
      - AMate__IngestionProvider__Args__Password=${SFTP_PASSWORD}
      - AMate__IngestionProvider__Args__PrivateKeyFilePath=/secrets/id_rsa
```

### Notes & Limitations

- The provider uses the SSH.NET (`Renci.SshNet`) library. Ensure the dependency is available in the `ArquivoMate2.Infrastructure` project.
- The ingestion job performs remote file operations (rename/upload/download). Provide an account with permissions to create directories, rename and remove files.
- Network reliability: if the SFTP server is transiently unreachable the ingestion job will retry on the normal ingestion schedule; monitor logs for repeated failures.

---

## Caching

ArquivoMate2 uses a hybrid caching strategy with memory + Redis.

```json
{
  "Caching": {
    "KeyPrefix": "redis:",
    "DefaultTtlSeconds": 300,
    "DefaultSliding": false,
    "PerKey": {
      "thumb:*": { "TtlSeconds": 1800, "Sliding": false },
      "preview:*": { "TtlSeconds": 1800, "Sliding": false },
      "meta:*": { "TtlSeconds": 1800, "Sliding": false },
      "s3delivery:*": { "TtlSeconds": 3300, "Sliding": false },
      "bunnyDelivery:*": { "TtlSeconds": 82800, "Sliding": false }
    },
    "Redis": {
      "Configuration": "cache:6379",
      "InstanceName": "redis:"
    },
    "Otel": {
      "ServiceName": "ArquivoMate2.Cache",
      "Endpoint": ""
    }
  }
}
```

**Properties:**

- `KeyPrefix`: Prefix for all cache keys
- `DefaultTtlSeconds`: Default cache lifetime (5 minutes)
- `DefaultSliding`: Whether to extend TTL on access
- `PerKey`: Key-specific TTL overrides (supports wildcards)
- `Redis.Configuration`: Redis connection string
- `Redis.InstanceName`: Redis key prefix (isolate multiple instances)

**Cache Key Patterns:**

| Pattern | TTL | Purpose |
|---------|-----|---------|
| `thumb:*` | 30 min | Document thumbnails |
| `preview:*` | 30 min | Preview PDFs |
| `meta:*` | 30 min | File metadata |
| `s3delivery:*` | 55 min | Presigned S3 URLs |
| `bunnyDelivery:*` | 23 hours | BunnyCDN URLs |

**Redis Connection Formats:**

```bash
# Single instance
cache:6379

# Password-protected
cache:6379,password=yourpassword

# Sentinel
sentinel:26379,sentinel:26380,serviceName=mymaster

# TLS
cache:6380,ssl=true,password=yourpassword
```

**Monitoring:**

OpenTelemetry traces are exported to configured endpoint for cache operations.

**Performance Tuning:**

- ? Increase TTL for static content
- ? Use sliding expiration for frequently accessed data
- ? Monitor cache hit ratio
- ?? Watch Redis memory usage

---

## Search (Meilisearch)

Full-text search engine for documents.

```json
{
  "Meilisearch": {
    "Url": "http://meili:7700",
    "ApiKey": "your-master-key"
  }
}
```

**Properties:**

- `Url`: Meilisearch server URL
- `ApiKey`: Master key or search key
  - Development: Can be omitted (uses default key)
  - Production: REQUIRED for security

**Index Configuration:**

ArquivoMate2 automatically creates the `documents` index with:
- **Searchable attributes**: `content`, `summary`, `title`
- **Filterable attributes**: `keywords`, `userId`, `allowedUserIds`
- **Sortable attributes**: `date`, `occuredOn`

**Environment Variables:**
```bash
AMate__Meilisearch__Url=https://search.yourdomain.com
AMate__Meilisearch__ApiKey=your-master-key
```

**Security:**
- ?? Never expose master key to frontend
- ? Use search-only keys for frontend
- ? Filter by `userId` to enforce document access

---

## ChatBot (OpenAI)

AI-powered document analysis using OpenAI GPT models.

```json
{
  "ChatBot": {
    "Type": "OpenAI",
    "Args": {
      "ApiKey": "sk-your-openai-api-key",
      "Model": "gpt-4o",
      "EmbeddingModel": "text-embedding-3-small",
      "UseBatch": false
    }
  }
}
```

**Properties:**

- `Type`: AI provider (`OpenAI`)
- `ApiKey`: OpenAI API key
- `Model`: Chat completion model
  - `gpt-4o`: Recommended (best quality)
  - `gpt-4o-mini`: Cost-effective alternative
  - `gpt-3.5-turbo`: Legacy, cheaper
- `EmbeddingModel`: Text embedding model for vector search
  - `text-embedding-3-small`: Recommended (cost-effective)
  - `text-embedding-3-large`: Higher quality, more expensive
- `UseBatch`: Use batch API for processing (slower, cheaper)

**Features:**

1. **Document Analysis**: Automatically extracts:
   - Document type (invoice, contract, letter, etc.)
   - Sender/Recipient information
   - Key metadata (invoice number, customer number, etc.)
   - Keywords and summary
   - Suggested title

2. **Semantic Search**: Vector embeddings for similarity search
   - Requires `VectorStore` connection string
   - Uses pgvector extension in PostgreSQL

**Costs (Approximate):**

| Model | Input | Output |
|-------|--------|--------|
| gpt-4o | $2.50/1M tokens | $10/1M tokens |
| gpt-4o-mini | $0.15/1M tokens | $0.60/1M tokens |
| text-embedding-3-small | $0.02/1M tokens | - |

**Disable AI Features:**

```json
{
  "ChatBot": {
    "Type": "Null"
  }
}
```

**Environment Variables:**
```bash
AMate__ChatBot__Args__ApiKey=sk-your-openai-api-key
AMate__ChatBot__Args__Model=gpt-4o
```
## ChatBot (OpenRouter)

OpenRouter nutzt dieselbe OpenAI SDK, ruft die Anfragen aber ueber die OpenRouter-Plattform ab.
Das Modell und der API-Schluessel muessen explizit gesetzt werden. Optional koennen `Referer` und `SiteName`
angegeben werden, damit OpenRouter die Herkunft der Anfragen verifizieren kann.

```json
{
  "ChatBot": {
    "Type": "OpenRouter",
    "Args": {
      "ApiKey": "sk-or-your-api-key",
      "Model": "openrouter/auto",
      "Endpoint": "https://openrouter.ai/api/v1",
      "Referer": "https://your-app.example",
      "SiteName": "ArquivoMate",
      "EmbeddingsApiKey": "sk-openai-embeddings",
      "EmbeddingModel": "text-embedding-3-small"
    }
  }
}
```

**Eigenschaften:**

- `Type`: AI Provider (`OpenRouter`)
- `ApiKey`: OpenRouter API-Schluessel (Pflicht)
- `Model`: Voll qualifizierter Modellname bei OpenRouter (Pflicht, z. B. `openrouter/auto` oder `anthropic/claude-3.5-sonnet`)
- `Endpoint`: API-Endpunkt (Standard: `https://openrouter.ai/api/v1`)
- `Referer`: Optionaler HTTP-Referer fuer OpenRouter
- `SiteName`: Optionaler Anzeigename der Anwendung
- `EmbeddingsApiKey`: Optionaler OpenAI API-Schluessel fuer Embeddings; ohne Wert wird Vektorisierung deaktiviert
- `EmbeddingModel`: Eingesetztes Embedding-Modell (z. B. `text-embedding-3-small`)

**Environment Variables:**
```bash
AMate__ChatBot__Type=OpenRouter
AMate__ChatBot__Args__ApiKey=sk-or-your-api-key
AMate__ChatBot__Args__Model=openrouter/auto
AMate__ChatBot__Args__Endpoint=https://openrouter.ai/api/v1
AMate__ChatBot__Args__Referer=https://your-app.example
AMate__ChatBot__Args__SiteName=ArquivoMate
AMate__ChatBot__Args__EmbeddingsApiKey=sk-openai-embeddings
AMate__ChatBot__Args__EmbeddingModel=text-embedding-3-small
```

---


## Document Processing

### OCR Settings

```json
{
  "OcrSettings": {
    "DefaultLanguages": ["eng", "deu"]
  }
}
```

**Tesseract Language Codes:**

- `eng`: English
- `deu`: German
- `fra`: French
- `spa`: Spanish
- `ita`: Italian
- `rus`: Russian

**Note:** Tesseract must have language data files installed.

### Language Detection

```json
{
  "LanguageDetection": {
    "SupportedLanguages": ["de", "en", "fr", "ru"]
  }
}
```

**ISO 639-1 Language Codes:**

- `de`: German
- `en`: English
- `fr`: French
- `ru`: Russian
- `es`: Spanish

### Path Configuration

```json
{
  "Paths": {
    "Working": "/var/arquivomate2/working",
    "PathBuilderSecret": "your-random-secret"
  }
}
```

**Properties:**

- `Working`: Temporary directory for document processing
- `PathBuilderSecret`: Secret for generating storage paths
  - Used to obfuscate storage paths
  - Should be random 32+ character string
  - ?? Changing this breaks existing document paths

**Generating Secret:**
```bash
openssl rand -hex 32
```

---

## Logging

ArquivoMate2 uses Serilog for structured logging.

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Information",
        "Hangfire": "Information",
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      },
      {
        "Name": "Console"
      }
    ]
  },
  "Seq": {
    "ServerUrl": "https://seq.yourdomain.com",
    "ApiKey": "your-seq-api-key"
  }
}
```

**Log Levels:**

- `Verbose`: Detailed diagnostic information
- `Debug`: Internal system events
- `Information`: General informational messages
- `Warning`: Warning messages
- `Error`: Error messages
- `Fatal`: Fatal error messages

**Outputs:**

1. **File**: Rolling daily log files
   - Retains last 7 days
   - Location: `logs/log-{date}.txt`

2. **Console**: Structured console output

3. **Seq** (Optional): Centralized log server
   - Requires `Seq:ServerUrl` and `Seq:ApiKey`
   - Provides powerful search and analysis

**Production Recommendations:**

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "Microsoft.AspNetCore": "Warning"
      }
    }
  }
}
```

---

## Localization

```json
{
  "JsonLocalizationOptions": {
    "ResourcesPath": "Resources",
    "CacheDuration": "00:00:15",
    "DefaultCulture": "en-US",
    "DefaultUICulture": "en-US",
    "SupportedCultureInfos": ["en-US", "de-DE", "ru-RU"]
  }
}
```

**Supported Languages:**

- `en-US`: English (United States)
- `de-DE`: German (Germany)
- `ru-RU`: Russian (Russia)

**Localization Files:**

- `src/ArquivoMate2.API/Localization/en.po`
- `src/ArquivoMate2.API/Localization/de.po`
- `src/ArquivoMate2.API/Localization/ru.po`

---

## Environment Variables

### Standard Format

```bash
# Hierarchical configuration using double underscores
AMate__Section__Subsection__Property=value
```

### Common Examples

```bash
# Database
AMate__ConnectionString__Default="Host=db;Database=arquivomate2;..."

# CORS
AMate__Cors__AllowedOrigins__0=https://app.yourdomain.com
AMate__Cors__AllowedOrigins__1=https://admin.yourdomain.com

# Storage
AMate__StorageProvider__Args__BucketName=my-bucket
AMate__StorageProvider__Args__AccessKey=your-key
AMate__StorageProvider__Args__SecretKey=your-secret
AMate__StorageProvider__Args__SseC__Enabled=true
AMate__StorageProvider__Args__SseC__CustomerKeyBase64=your-key

# Authentication
AMate__Auth__Args__ClientId=your-client-id
AMate__Auth__Args__Authority=https://auth.yourdomain.com

# Logging
AMate__Serilog__MinimumLevel__Default=Warning
AMate__Seq__ServerUrl=https://seq.yourdomain.com
AMate__Seq__ApiKey=your-seq-key

# AI
AMate__ChatBot__Args__ApiKey=sk-your-openai-key
AMate__ChatBot__Args__Model=gpt-4o

# Search
AMate__Meilisearch__Url=https://search.yourdomain.com
AMate__Meilisearch__ApiKey=your-meili-key
```

### Docker Compose Example

```yaml
services:
  api:
    image: arquivomate2:latest
    environment:
      - AMate__ConnectionString__Default=Host=db;Database=arquivomate2;Username=postgres;Password=${DB_PASSWORD}
      - AMate__Cors__AllowedOrigins__0=https://app.yourdomain.com
      - AMate__StorageProvider__Args__BucketName=documents
      - AMate__StorageProvider__Args__AccessKey=${S3_ACCESS_KEY}
      - AMate__StorageProvider__Args__SecretKey=${S3_SECRET_KEY}
      - AMate__Auth__Args__ClientId=${OIDC_CLIENT_ID}
      - AMate__ChatBot__Args__ApiKey=${OPENAI_API_KEY}
```

---

## Security Best Practices

### Secrets Management

? **Never Do:**
```json
{
  "ConnectionString": {
    "Default": "Host=db;Password=MyPassword123"
  }
}
```

? **Always Do:**
```bash
# Use environment variables
AMate__ConnectionString__Default="Host=db;Password=${DB_PASSWORD}"

# Or use secret management services
# - Azure Key Vault
# - AWS Secrets Manager
# - HashiCorp Vault
```

### Production Checklist

- [ ] Remove all hardcoded passwords/keys from appsettings.json
- [ ] Set `AllowedHosts` to specific domains (not `*`)
- [ ] Configure CORS with specific origins (no wildcards)
- [ ] Enable HTTPS for all external endpoints
- [ ] Use strong random secrets for `PathBuilderSecret`
- [ ] Enable SSE-C encryption for document storage
- [ ] Set appropriate log levels (not `Verbose` or `Debug`)
- [ ] Configure authentication with real OIDC provider
- [ ] Use separate databases for different environments
- [ ] Enable rate limiting (if exposed to internet)
- [ ] Configure firewall rules to restrict database access
- [ ] Regularly rotate access keys and encryption keys
- [ ] Monitor logs for suspicious activity
- [ ] Keep all dependencies up to date

### Encryption Keys

**Storage Encryption (SSE-C):**
```bash
# Generate key
openssl rand -base64 32

# Store securely (example with AWS Secrets Manager)
aws secretsmanager create-secret \
  --name arquivomate2/ssec-key \
  --secret-string "your-generated-key"
```

**Path Builder Secret:**
```bash
# Generate secret
openssl rand -hex 32

# Add to environment
AMate__Paths__PathBuilderSecret="your-generated-secret"
```

### Monitoring

**Health Checks:**
- Endpoint: `/healthz`
- Checks: PostgreSQL, Meilisearch

**Metrics:**
- OpenTelemetry traces to configured endpoint
- Serilog structured logs
- Seq centralized logging (optional)

---

## Troubleshooting

### Connection Issues

**Database Connection Failed:**
```
Could not connect to server: Connection refused
```

**Solution:**
1. Verify database is running: `docker ps` or `systemctl status postgresql`
2. Check connection string format
3. Verify network connectivity
4. Check firewall rules

**Redis Connection Failed:**
```
It was not possible to connect to the redis server(s)
```

**Solution:**
1. Verify Redis is running
2. Check `Caching:Redis:Configuration` format
3. Test connection: `redis-cli ping`

### Authentication Issues

**Token Validation Failed:**
```
IDX10205: Issuer validation failed
```

**Solution:**
1. Verify `Auth:Args:Issuer` matches your OIDC provider exactly
2. Check for trailing slashes (some providers require them)
3. Verify `Auth:Args:Authority` is accessible
4. Test discovery endpoint: `curl {Authority}/.well-known/openid-configuration`

### CORS Errors

**Access-Control-Allow-Origin missing:**

**Solution:**
1. Verify frontend origin is in `Cors:AllowedOrigins`
2. Check for exact match (protocol, domain, port)
3. Ensure no trailing slashes in configuration
4. Restart API after configuration changes

### Storage Issues

**Access Denied (S3):**

**Solution:**
1. Verify `AccessKey` and `SecretKey` are correct
2. Check IAM permissions for S3 bucket
3. Verify bucket exists and region is correct

**SSE-C Decryption Failed:**

**Solution:**
1. Verify `CustomerKeyBase64` is correct 32-byte key
2. Ensure same key used for encryption and decryption
3. Check key hasn't been rotated without migration

---

## Additional Resources

- [CORS Configuration Guide](./CORS-Configuration.md)
- [SSE-C Encryption Implementation](./SSE-C-Implementation.md)
- [Encryption Configuration Lock](./Encryption-Configuration-Lock.md)
- [GitHub Repository](https://github.com/sebfischer83/ArquivoMate2)

---

**Last Updated:** 2025-01-18  
**Version:** 1.0.0  
**Configuration File:** `src/ArquivoMate2.API/appsettings.json`
