# Server Configuration

Detaillierte Dokumentation aller konfigurierbaren Einstellungen fr den Server (appsettings / Umgebungsvariablen).

Hinweis: Program.cs ldt environment-variablen mit dem Prefix `AMate__`.

---

## Kurz: Prinzipien
- Primre Quelle: `appsettings.json` (Produktion: env vars / Secret Store).
- Environment-Variable-Prefix: `AMate__Section__Key` (z. B. `AMate__StorageProvider__Args__Endpoint`).
- Geheimnisse (API keys, Access/Secret, MasterKey) => Umgebungsvariablen oder Secrets Vault.
- Viele Provider nutzen `Args`-Unterknoten (z. B. `StorageProvider:Args`).

---

## 1) ConnectionStrings
- `ConnectionStrings:Default` (string)  PostgreSQL fr Marten (erforderlich).
- `ConnectionStrings:Hangfire` (string)  PostgreSQL fr Hangfire.
- `ConnectionStrings:VectorStore` (string)  optional, Vector DB (Postgres + pgvector).

---

## 2) App (global)
- `App:PublicBaseUrl` (string?)  Basis-URL fr Delivery-/Share-Links. Wenn leer, wird Request.Scheme+Host verwendet.
- `App:PublicShareDefaultTtlMinutes` (int, default 60)
- `App:PublicShareMaxTtlMinutes` (int, default 1440)

---

## 3) Auth (OIDC)
- `Auth:Type` = `OIDC` (enum)
- `Auth:Args:Authority` (string)  OIDC issuer URL
- `Auth:Args:Audience` (string)
- `Auth:Args:Issuer` (string)
- `Auth:Args:ClientId` (string)
- `Auth:Args:CookieDomain` (string?, optional)

Hinweis: JwtBearer handler akzeptiert `access_token` Query param fr SignalR Hubs (`/hubs/documents`) (siehe `Program.cs`).

---

## 4) StorageProvider
Top-Level:
- `StorageProvider:Type` = `S3` (derzeit untersttzt)
- `StorageProvider:RootPath` (string)  Root-prefix fr Objekte

S3-Args (`StorageProvider:Args`):
- `AccessKey` (string)
- `SecretKey` (string)
- `Endpoint` (string)
- `BucketName` (string)
- `Region` (string)
- `IsPublic` (bool)
- `SseC:Enabled` (bool)  SSE?C aktivieren
- `SseC:CustomerKeyBase64` (string)  Base64 (32 bytes) wenn SSE?C aktiv

Hinweis: Wenn SSE?C aktiviert ist, knnen keine presigned URLs verwendet werden; die API liefert in diesem Fall `sse-c://`-Marker fr serverseitige Auslieferung.

---

## 5) DeliveryProvider
- `DeliveryProvider:Type` = `Noop` | `S3` | `Bunny` | `Server`
- `DeliveryProvider:Args` abhngig vom Typ:
  - S3: `Endpoint`, `BucketName`, `IsPublic`, `SseC`
  - Bunny: `TokenAuthenticationKey`, `Host`, `UseTokenAuthentication`, `UseTokenIpValidation`, `UseTokenPath`, `TokenCountries`, `TokenCountriesBlocked`
  - Server: serverseitige Auslieferung (keine Args erforderlich)

---

## 6) IngestionProvider
- `IngestionProvider:Type` = `FileSystem` | `S3` | `None`
- `IngestionProvider:Args`:
  - FileSystem: `RootPath`, `PollingInterval` (TimeSpan)
  - S3: `AccessKey`, `SecretKey`, `Endpoint`, `BucketName`, `UseSsl`, `RootPrefix`, `ProcessingSubfolderName`, `ProcessedSubfolderName`, `FailedSubfolderName`, `PollingInterval`, optional `SseC`

---

## 7) Meilisearch
- `Meilisearch:Url` (string)  erforderlich
- `Meilisearch:ApiKey` / `Meilisearch:MasterKey` / `Meilisearch:Key`  API key

---

## 8) ChatBot / LLM (OpenAI)
Einstellungen ber `ChatBotSettingsFactory` / `OpenAISettings`:
- `ApiKey` (string)  erforderlich
- `Model` (string)  default `gpt-4`
- `UseBatch` (bool)
- `EmbeddingModel` (string)  z. B. `text-embedding-3-small`
- `EmbeddingDimensions` (int, default 1536)

Wenn `ConnectionStrings:VectorStore` gesetzt ist, wird `DocumentVectorizationService` aktiviert.

---

## 9) Encryption (Application-wide)
- `Encryption:Enabled` (bool)
- `Encryption:MasterKeyBase64` (string)  muss 32 Bytes Base64 sein (AES?256)
- `Encryption:TokenTtlMinutes` (int, default 5)
- `Encryption:CacheTtlMinutes` (int, default 30)

Wirkung: `EncryptionService` verschlsselt Artefakte (client-side) und erstellt `EncryptedArtifactKey` Events; `FileAccessTokenService` erzeugt signierte Tokens.

---

## 10) Paths
- `Paths:Working` (string)  base working directory
- `Paths:PathBuilderSecret` (string)  secret for path hashing

Derived: `Paths.Upload` = Path.Combine(Working, "upload")

---

## 11) OCR / LanguageDetection
- `OcrSettings:DefaultLanguages` (string[])
- `LanguageDetection:SupportedLanguages` (string[])

---

## 12) Caching / FusionCache / Redis
- `Caching:KeyPrefix` (string)
- `Caching:DefaultTtlSeconds` (int)
- `Caching:DefaultSliding` (bool)
- `Caching:PerKey`  mapping `pattern` -> `{ TtlSeconds, Sliding }`
- `Caching:Redis:Configuration` (string)  e.g. `redis:6379`
- `Caching:Redis:InstanceName` (string)
- `Caching:Otel:ServiceName`, `Caching:Otel:Endpoint`  optional OTLP exporter

---

## 13) Hangfire
- Hangfire verwendet `ConnectionStrings:Hangfire` fr Postgres storage
- Optionen (z. B. DistributedLockTimeout) werden in `Program.cs` gesetzt

---

## 14) Vector Store
- `ConnectionStrings:VectorStore` (Postgres)  wenn gesetzt, `DocumentVectorizationService` verwendet pgvector; ansonsten `NullDocumentVectorizationService`

---

## 15) SignalR / Auth Hinweise (bei 401)
- JwtBearer handler in `Program.cs` nutzt `OnMessageReceived` um `access_token` Query param fr hubs zu akzeptieren.
- Wenn UI SignalR 401 empfngt: prfen ob Client `accessTokenFactory` verwendet oder Cookie + CORS (`AllowCredentials`) korrekt ist.
- Fr Debugging: Logging-Level erhhen fr `Microsoft.AspNetCore.Authentication` und `Microsoft.AspNetCore.SignalR` sowie `JwtBearerEvents` loggen.

---

## 16) Environment variable examples
Beispiele (prefix `AMate__`):
```
AMate__StorageProvider__Type=S3
AMate__StorageProvider__RootPath=documents
AMate__StorageProvider__Args__Endpoint=s3.example.com
AMate__Encryption__MasterKeyBase64=<base64-32>
AMate__ChatBot__Args__ApiKey=<openai-key>
```

---

## 17) Beispiel `appsettings.json` (skeleton)
```json
{
  "ConnectionStrings": {
    "Default": "Host=pg;Database=arquivo;Username=...;Password=...",
    "Hangfire": "Host=pg;Database=hangfire;Username=...;Password=...",
    "VectorStore": "Host=pg;Database=vectors;Username=...;Password=..."
  },
  "Seq": { "ServerUrl": "", "ApiKey": "" },
  "App": { "PublicBaseUrl": "https://app.example.com", "PublicShareDefaultTtlMinutes": 60, "PublicShareMaxTtlMinutes": 1440 },
  "Auth": {
    "Type": "OIDC",
    "Args": {
      "Authority": "https://auth.example.com",
      "Audience": "arquivo-api",
      "Issuer": "https://auth.example.com",
      "ClientId": "arquivo-client",
      "CookieDomain": ".example.com"
    }
  },
  "StorageProvider": {
    "Type": "S3",
    "RootPath": "documents",
    "Args": {
      "AccessKey": "<access>",
      "SecretKey": "<secret>",
      "Endpoint": "s3.example.com",
      "BucketName": "arquivo-docs",
      "Region": "eu-central-1",
      "IsPublic": false,
      "SseC": { "Enabled": true, "CustomerKeyBase64": "<base64-32-bytes>" }
    }
  },
  "DeliveryProvider": { "Type": "S3", "Args": { "Endpoint": "s3.example.com", "BucketName": "arquivo-docs", "IsPublic": false } },
  "IngestionProvider": { "Type": "S3", "Args": { "AccessKey": "<access>", "SecretKey": "<secret>", "Endpoint": "s3.example.com", "BucketName": "ingest", "RootPrefix": "ingestion", "PollingInterval": "00:05:00" } },
  "Meilisearch": { "Url": "http://meili:7700", "ApiKey": "masterKey" },
  "ChatBot": {
    "Type": "OpenRouter",
    "Args": {
      "ApiKey": "<openrouter-api-key>",
      "Model": "openrouter/auto",
      "Endpoint": "https://openrouter.ai/api/v1",
      "Referer": "https://your-app.example",
      "SiteName": "ArquivoMate",
      "EmbeddingsApiKey": "<optional-openai-key>"
    }
  },
  "Encryption": { "Enabled": true, "MasterKeyBase64": "<base64-32>", "TokenTtlMinutes": 5, "CacheTtlMinutes": 30 },
  "Paths": { "Working": "/var/opt/arquivomate", "PathBuilderSecret": "<secret>" },
  "OcrSettings": { "DefaultLanguages": [ "de", "en" ] },
  "Caching": { "KeyPrefix": "am:", "DefaultTtlSeconds": 300, "Redis": { "Configuration": "redis:6379", "InstanceName": "redis:" } }
}
```

---

Wenn du mchtest, lege ich diese Datei direkt in `docs/Configuration.md` ab (erledigt) oder erweitere spezifische Abschnitte (z. B. SSE?C Betriebsanleitung, SignalR Debugging Snippets, Beispiel env?vars).
