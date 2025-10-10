using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using ArquivoMate2.Domain.Email;
using ArquivoMate2.Domain.ValueObjects;
using ArquivoMate2.Domain.Users;
using ArquivoMate2.Domain.Sharing;
using ArquivoMate2.Infrastructure.Configuration.DeliveryProvider;
using ArquivoMate2.Infrastructure.Configuration.IngestionProvider;
using ArquivoMate2.Infrastructure.Configuration.Llm;
using ArquivoMate2.Infrastructure.Configuration.StorageProvider;
using ArquivoMate2.Infrastructure.Mapping;
using ArquivoMate2.Domain.ReadModels;
using ArquivoMate2.Infrastructure.Repositories;
using ArquivoMate2.Infrastructure.Services;
using ArquivoMate2.Infrastructure.Services.DeliveryProvider;
using ArquivoMate2.Infrastructure.Services.EmailProvider;
using ArquivoMate2.Infrastructure.Services.IngestionProvider;
using ArquivoMate2.Infrastructure.Services.Llm;
using ArquivoMate2.Infrastructure.Services.Search;
using ArquivoMate2.Infrastructure.Services.Sharing;
using ArquivoMate2.Infrastructure.Services.StorageProvider;
using ArquivoMate2.Shared.Models;
using AutoMapper;
using EasyCaching.Core.Configurations;
using EasyCaching.Serialization.SystemTextJson.Configurations;
using FluentStorage;
using FluentStorage.AWS.Blobs;
using JasperFx.Core;
using Marten;
using Marten.Events;
using Marten.Events.Projections;
using Marten.Schema;
using Meilisearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MimeTypes;
using Minio;
using OpenAI.Batch;
using OpenAI.Chat;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using Weasel.Core;
using Weasel.Core.Migrations;
using Weasel.Postgresql.Tables;
using EmailCriteria = ArquivoMate2.Domain.Email.EmailCriteria;
using JasperFx.Events.Projections;
using ArquivoMate2.Application.Interfaces.Sharing;
using ArquivoMate2.Infrastructure.Services.Encryption; // Encryption helpers
using ArquivoMate2.Infrastructure.Persistance; // ensure interface is visible

namespace ArquivoMate2.Infrastructure.Configuration
{
    /// <summary>
    /// Extension methods for wiring up infrastructure services and data access.
    /// </summary>
    public static class DependencyInjectionConfiguration
    {
        /// <summary>
        /// Registers infrastructure services including persistence, projections, search, and integrations.
        /// </summary>
        /// <param name="services">Service collection to configure.</param>
        /// <param name="config">Application configuration source.</param>
        /// <summary>
        /// Registers persistence, providers, integrations, and related infrastructure services into the provided service collection using the given application configuration.
        /// </summary>
        /// <param name="services">The service collection to configure.</param>
        /// <param name="config">The application configuration used to configure persistence, providers, and external integrations.</param>
        /// <returns>The updated service collection.</returns>
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            services.AddMarten(options =>
            {
                // Connection string sourced from appsettings.json
                options.Connection(config.GetConnectionString("Default")!);

                var env = config["ASPNETCORE_ENVIRONMENT"] ?? "Production";
                options.AutoCreateSchemaObjects = env.Equals("Development", StringComparison.OrdinalIgnoreCase)
                    ? JasperFx.AutoCreate.All
                    : JasperFx.AutoCreate.CreateOrUpdate;

                // Register domain events used by the aggregates
                options.Events.AddEventTypes(new[]
                {
                    typeof(DocumentUploaded),
                    typeof(DocumentDeleted),
                    typeof(DocumentContentExtracted),
                    typeof(DocumentFilesPrepared),
                    typeof(DocumentChatBotDataReceived),
                    typeof(InitDocumentImport),
                    typeof(MarkFailedDocumentImport),
                    typeof(MarkSucceededDocumentImport),
                    typeof(StartDocumentImport),
                    typeof(DocumentProcessed),
                    typeof(HideDocumentImport),
                    typeof(DocumentTitleInitialized),
                    typeof(DocumentTitleSuggested),
                    typeof(DocumentEncryptionEnabled),
                    typeof(DocumentEncryptionKeysAdded),
                    typeof(DocumentNoteAdded), // RESTORED
                    typeof(DocumentNoteDeleted), // RESTORED
                    typeof(DocumentLanguageDetected) // RESTORED
                });

                options.Schema.For<PartyInfo>()
                    .Index(x => x.UserId);
                options.Schema.For<EmailSettings>()
                    .Index(x => x.UserId)
                    .Index(x => x.IsActive);

                options.Schema.For<EmailCriteria>()
                    .Index(x => x.UserId);

                options.Schema.For<ProcessedEmail>()
                    .Index(x => x.UserId)
                    .Index(x => x.EmailUid)
                    .Index(x => x.Status);

                options.Events.StreamIdentity = JasperFx.Events.StreamIdentity.AsGuid;

                options.Schema.For<Document>()
                    .Index(d => d.UserId).Index(d => d.Hash);

                options.Schema.For<UserProfile>();

                options.Schema.For<DocumentShare>()
                    .Index(x => x.DocumentId)
                    .Index(x => x.OwnerUserId)
                    .Index(x => x.Target.Identifier);

                options.Schema.For<ShareGroup>()
                    .Index(x => x.OwnerUserId);

                options.Schema.For<ShareAutomationRule>()
                    .Index(x => x.OwnerUserId)
                    .Index(x => x.Target.Identifier);

                options.Schema.For<DocumentAccessView>()
                    .Index(x => x.OwnerUserId); // Base index (extend with custom GIN for EffectiveUserIds if required)

                options.Schema.For<ImportProcess>()
                    .Index(d => d.UserId)
                    .Index(x => x.IsHidden)
                    .Index(x => x.DocumentId)
                    .Index(x => x.Status)
                    .Index(x => x.Source);

                options.Schema.For<ImportHistoryView>()
                    .Index(x => x.UserId)
                    .Index(x => x.Status)
                    .Index(x => x.Source)
                    .Index(x => x.IsHidden);

                // Indexes to support DocumentView sorting and filtering
                var docView = options.Schema.For<DocumentView>();
                docView.Index(x => x.UserId);
                docView.Index(x => x.Date);
                docView.Index(x => x.OccurredOn);
                docView.Index(x => x.TotalPrice);
                docView.Index(x => x.Type);
                docView.Index(x => x.Accepted);
                docView.Index(x => x.CustomerNumber);
                docView.Index(x => x.InvoiceNumber);
                // Composite index for the common (user + date) combination
                docView.Index(x => new { x.UserId, x.Date });
                // Add advanced partial or GIN indexes via SQL migrations if required (kept out of code for simpler builds)

                options.Projections.Add<DocumentProjection>(ProjectionLifecycle.Inline);
                options.Projections.Add<ImportHistoryProjection>(ProjectionLifecycle.Inline);

                // Notes document schema
                options.Schema.For<ArquivoMate2.Domain.Notes.DocumentNote>()
                    .Index(x => x.DocumentId)
                    .Index(x => x.UserId);

                options.Schema.For<ExternalShare>()
                    .Index(x => x.DocumentId)
                    .Index(x => x.ExpiresAtUtc);
            });

            services.AddScoped<IDocumentSession>(sp => sp.GetRequiredService<IDocumentStore>().LightweightSession());
            services.AddScoped<IQuerySession>(sp => sp.GetRequiredService<IDocumentStore>().QuerySession());

            services.AddScoped<IDocumentProcessor, DocumentProcessor>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IFileMetadataService, FileMetadataService>();
            services.AddScoped<IPathService, PathService>();
            services.AddScoped<IThumbnailService, ThumbnailService>();
            var meilisearchUrl = config["Meilisearch:Url"]
                ?? throw new InvalidOperationException("Meilisearch URL is not configured.");
            var meilisearchApiKey = config["Meilisearch:ApiKey"]
                ?? config["Meilisearch:MasterKey"]
                ?? config["Meilisearch:Key"]
                ?? config["MEILI_MASTER_KEY"]
                ?? "supersecret";

            services.AddScoped<MeilisearchClient>(_ => new MeilisearchClient(meilisearchUrl, meilisearchApiKey));
            services.AddScoped<ISearchClient, SearchClient>();
            services.AddScoped<IDocumentAccessService, DocumentAccessService>();
            services.AddScoped<IAutoShareService, AutoShareService>();
            services.AddHttpClient();
            // Language detection service is configured in Program.cs where options are bound.
            // Avoid registering LanguageDetectionService here to prevent DI-validation issues when options are not yet configured.
            services.AddScoped<IDocumentOwnershipLookup, DocumentOwnershipLookup>(); // Provides ownership lookups for sharing
            services.AddScoped<IDocumentAccessUpdater, DocumentAccessUpdater>(); // Updates the read model for document access
            services.AddScoped<ArquivoMate2.Application.Interfaces.ImportHistory.IImportHistoryReadStore, ArquivoMate2.Infrastructure.Services.ImportHistory.ImportHistoryReadStore>();
            services.AddSingleton<ChatBotSettingsFactory>();
            var chatbotSettings = new ChatBotSettingsFactory(config).GetChatBotSettings();

            if (chatbotSettings is OpenAISettings openAISettings)
            {
                services.AddSingleton(openAISettings);
                if (openAISettings.UseBatch)
                {
                    services.AddScoped<IChatBot, OpenAIBatchChatBot>(_ =>
                    {
                        BatchClient client = new BatchClient(openAISettings.ApiKey);
                        return new OpenAIBatchChatBot(client);
                    });
                }
                else
                {
                    services.AddScoped<IChatBot, OpenAIChatBot>(_ =>
                        {
                            ChatClient client = new(model: openAISettings.Model, apiKey: openAISettings.ApiKey);
                            return new OpenAIChatBot(client);
                        });
                }
            }
            else
            {
                throw new InvalidOperationException("Unsupported ChatBotSettings");
            }

            services.Configure<OcrSettings>(config.GetSection("OcrSettings"));
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<OcrSettings>>().Value);

            services.Configure<Paths>(config.GetSection("Paths"));
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<Paths>>().Value);

            services.AddSingleton<StorageProviderSettingsFactory>();
            var settings = new StorageProviderSettingsFactory(config).GetsStorageProviderSettings();
            services.AddSingleton<DeliveryProviderSettingsFactory>();
            var deliverySettings = new DeliveryProviderSettingsFactory(config).GetDeliveryProviderSettings();

            services.AddSingleton<IngestionProviderSettingsFactory>();
            var ingestionSettings = new IngestionProviderSettingsFactory(config).GetIngestionProviderSettings();

            services.AddScoped<IEmailSettingsRepository, EmailSettingsRepository>();
            services.AddScoped<IProcessedEmailRepository, ProcessedEmailRepository>();
            services.AddScoped<IEmailCriteriaRepository, EmailCriteriaRepository>();
            services.AddScoped<IEmailServiceFactory, EmailServiceFactory>();
            services.AddScoped<IEmailService, NullEmailService>();

            switch (settings)
            {
                case S3StorageProviderSettings local:
                    // Register the resolved settings instance (which already merged parent-level RootPath)
                    services.AddSingleton<Microsoft.Extensions.Options.IOptions<S3StorageProviderSettings>>(Microsoft.Extensions.Options.Options.Create(local));
                    services.AddScoped<IStorageProvider, S3StorageProvider>();

                    // Configure Minio client with resolved S3 settings
                    var endpoint = local.Endpoint;
                    services.AddMinio(configureClient => configureClient
                        .WithEndpoint(endpoint)
                        .WithCredentials(local.AccessKey, local.SecretKey)
                        .WithSSL(true)
                        .Build());
                    break;
                default:
                    throw new InvalidOperationException("Unsupported FileProviderSettings");
            }

            switch (ingestionSettings)
            {
                case FileSystemIngestionProviderSettings fileSystem:
                    services.AddSingleton(fileSystem);
                    services.AddSingleton<IngestionProviderSettings>(fileSystem);
                    services.AddSingleton<Microsoft.Extensions.Options.IOptions<FileSystemIngestionProviderSettings>>(Microsoft.Extensions.Options.Options.Create(fileSystem));
                    services.AddSingleton<IIngestionProvider, FileSystemIngestionProvider>();
                    break;
                case S3IngestionProviderSettings s3:
                    services.AddSingleton(s3);
                    services.AddSingleton<IngestionProviderSettings>(s3);
                    services.AddSingleton<Microsoft.Extensions.Options.IOptions<S3IngestionProviderSettings>>(Microsoft.Extensions.Options.Options.Create(s3));
                    services.AddSingleton<IIngestionProvider, S3IngestionProvider>();
                    break;
                default:
                    services.AddSingleton(ingestionSettings);
                    services.AddSingleton<IngestionProviderSettings>(ingestionSettings);
                    services.AddSingleton<IIngestionProvider, NullIngestionProvider>();
                    break;
            }

            switch (deliverySettings)
            {
                case DeliveryProviderSettings noop when noop.Type == DeliveryProviderType.Noop:
                    // Default noop returns the raw fullPath. If you want server-side delivery, register ServerDeliveryProvider in DI and change config.
                    services.AddScoped<IDeliveryProvider, NoopDeliveryProvider>();
                    break;
                case DeliveryProviderSettings server when server.Type == DeliveryProviderType.Server:
                    // Route delivery through the API server. ServerDeliveryProvider builds a /api/delivery/... URL with a token.
                    services.AddScoped<IDeliveryProvider, ServerDeliveryProvider>();
                    break;
                case S3DeliveryProviderSettings s3:
                    services.Configure<S3DeliveryProviderSettings>(config.GetSection("DeliveryProvider").GetSection("Args"));
                    services.AddScoped<IDeliveryProvider, S3DeliveryProvider>();
                    break;
                case BunnyDeliveryProviderSettings bunny:
                    services.Configure<BunnyDeliveryProviderSettings>(config.GetSection("DeliveryProvider").GetSection("Args"));
                    services.AddScoped<IDeliveryProvider, BunnyCdnDeliveryProvider>();
                    break;
                default:
                    throw new InvalidOperationException("Unsupported DeliveryProviderSettings");
            }

            services.AddScoped<IMemberValueResolver<DocumentView, BaseDto, string, string>, PathResolver>();
            services.AddScoped<StatusTranslationResolver<ImportHistoryView, ImportHistoryListItemDto>>();
            services.AddScoped<ImportSourceTranslationResolver<ImportHistoryView, ImportHistoryListItemDto>>();
            services.AddAutoMapper(typeof(Mapping.DocumentMapping).Assembly);
            services.AddHostedService<DatabaseMigrationService>();
            services.AddHostedService<MeiliInitService>();

            services.AddEasyCaching(x => x.UseRedis(r =>
            {
                r.EnableLogging = false;
                r.DBConfig.KeyPrefix = "redis:";
                r.SerializerName = "A";
                r.DBConfig.Endpoints.Add(new EasyCaching.Core.Configurations.ServerEndPoint("cache", int.Parse("6379")));
                r.DBConfig.AbortOnConnectFail = false;
            }).WithSystemTextJson("A"));

            // Register a StackExchange.Redis ConnectionMultiplexer for direct Redis operations (admin tasks)
            var redisConfig = config["Redis:Configuration"] ?? "cache:6379";
            var redisOptions = ConfigurationOptions.Parse(redisConfig);
            redisOptions.AbortOnConnectFail = false;
            var mux = ConnectionMultiplexer.Connect(redisOptions);
            services.AddSingleton<IConnectionMultiplexer>(mux);

            services.Configure<EncryptionSettings>(config.GetSection("Encryption"));
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<EncryptionSettings>>().Value);
            services.AddTransient<IEncryptionService, EncryptionService>();
            services.AddTransient<IFileAccessTokenService, FileAccessTokenService>();
            services.Configure<AppSettings>(config.GetSection("App"));
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<AppSettings>>().Value);
            services.AddTransient<IExternalShareService, ExternalShareService>();
            services.AddTransient<IDocumentArtifactStreamer, DocumentArtifactStreamer>();
            // Register Marten-backed document encryption keys provider
            services.AddScoped<IDocumentEncryptionKeysProvider, MartenDocumentEncryptionKeysProvider>();

            return services;
        }
    }
}