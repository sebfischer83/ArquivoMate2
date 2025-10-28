using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using ArquivoMate2.Domain.Email;
using ArquivoMate2.Domain.ValueObjects;
using ArquivoMate2.Domain.Users;
using ArquivoMate2.Domain.Sharing;
using ArquivoMate2.Domain.DocumentTypes;
using ArquivoMate2.Infrastructure.Configuration.Caching;
using ArquivoMate2.Infrastructure.Configuration.DeliveryProvider;
using ArquivoMate2.Infrastructure.Configuration.IngestionProvider;
using ArquivoMate2.Infrastructure.Configuration.Llm;
using ArquivoMate2.Infrastructure.Configuration.StorageProvider;
using ArquivoMate2.Infrastructure.Configuration.DocumentTypes;
using ArquivoMate2.Infrastructure.Mapping;
using ArquivoMate2.Domain.ReadModels;
using ArquivoMate2.Infrastructure.Repositories;
using ArquivoMate2.Infrastructure.Services;
using ArquivoMate2.Infrastructure.Services.DeliveryProvider;
using ArquivoMate2.Infrastructure.Services.EmailProvider;
using ArquivoMate2.Infrastructure.Services.IngestionProvider;
using ArquivoMate2.Infrastructure.Services.Llm;
using ArquivoMate2.Infrastructure.Services.Search;
using ArquivoMate2.Infrastructure.Services.Vectorization;
using ArquivoMate2.Infrastructure.Services.Sharing;
using ArquivoMate2.Infrastructure.Services.StorageProvider;
using ArquivoMate2.Shared.Models;
using AutoMapper;
using ArquivoMate2.Infrastructure.Services.Caching;
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
using Microsoft.Extensions.Logging;
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
using System.Text.Json;
using System.Text.Json.Serialization;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;
using EmailCriteria = ArquivoMate2.Domain.Email.EmailCriteria;
using JasperFx.Events.Projections;
using ArquivoMate2.Application.Interfaces.Sharing;
using ArquivoMate2.Infrastructure.Services.Encryption;
using ArquivoMate2.Infrastructure.Persistance;
using Minio.Handlers;
using System.Net.Http;
using ArquivoMate2.Application.Features.Processors.LabResults;
using ArquivoMate2.Application.Features.Processors.LabResults.Models;
using ArquivoMate2.Application.Features.Processors.LabResults.Services;
using ArquivoMate2.Infrastructure.Services.LabResults;
using System.ClientModel;

namespace ArquivoMate2.Infrastructure.Configuration
{
    public static class DependencyInjectionConfiguration
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            services.AddMarten(options =>
            {
                options.Connection(config.GetConnectionString("Default")!);
                var env = config["ASPNETCORE_ENVIRONMENT"] ?? "Production";
                options.AutoCreateSchemaObjects = env.Equals("Development", StringComparison.OrdinalIgnoreCase)
                    ? JasperFx.AutoCreate.All
                    : JasperFx.AutoCreate.CreateOrUpdate;

                options.Events.AddEventTypes(new[]
                {
                    typeof(DocumentUploaded), typeof(DocumentDeleted), typeof(DocumentContentExtracted), typeof(DocumentFilesPrepared),
                    typeof(DocumentChatBotDataReceived), typeof(InitDocumentImport), typeof(MarkFailedDocumentImport), typeof(MarkSucceededDocumentImport),
                    typeof(StartDocumentImport), typeof(DocumentProcessed), typeof(HideDocumentImport), typeof(DocumentTitleInitialized),
                    typeof(DocumentTitleSuggested), typeof(DocumentEncryptionEnabled), typeof(DocumentEncryptionKeysAdded), typeof(DocumentNoteAdded),
                    typeof(DocumentNoteDeleted), typeof(DocumentLanguageDetected)
                });

                options.Schema.For<PartyInfo>().Index(x => x.UserId);
                options.Schema.For<EmailSettings>().Index(x => x.UserId).Index(x => x.IsActive);
                options.Schema.For<EmailCriteria>().Index(x => x.UserId);
                options.Schema.For<ProcessedEmail>().Index(x => x.UserId).Index(x => x.EmailUid).Index(x => x.Status);
                options.Events.StreamIdentity = JasperFx.Events.StreamIdentity.AsGuid;
                options.Schema.For<Document>().Index(d => d.UserId).Index(d => d.Hash);
                options.Schema.For<DocumentTypeDefinition>().UniqueIndex(x => x.NormalizedName);
                options.Schema.For<UserDocumentType>().Index(x => x.UserId).Index(x => x.DocumentTypeId);
                options.Schema.For<UserProfile>();
                options.Schema.For<DocumentShare>().Index(x => x.DocumentId).Index(x => x.OwnerUserId);
                options.Schema.For<ShareGroup>().Index(x => x.OwnerUserId);
                options.Schema.For<ShareAutomationRule>().Index(x => x.OwnerUserId);
                options.Schema.For<DocumentAccessView>().Index(x => x.OwnerUserId);

                options.Schema.For<ImportProcess>().Index(d => d.UserId).Index(x => x.IsHidden).Index(x => x.DocumentId).Index(x => x.Status).Index(x => x.Source);
                options.Schema.For<ImportHistoryView>().Index(x => x.UserId).Index(x => x.Status).Index(x => x.Source).Index(x => x.IsHidden);

                var docView = options.Schema.For<DocumentView>();
                docView.Index(x => x.UserId);
                docView.Index(x => x.Date);
                docView.Index(x => x.OccurredOn);
                docView.Index(x => x.TotalPrice);
                docView.Index(x => x.Type);
                docView.Index(x => x.Accepted);
                docView.Index(x => x.CustomerNumber);
                docView.Index(x => x.InvoiceNumber);
                docView.Index(x => new { x.UserId, x.Date });

                options.Projections.Add<DocumentProjection>(ProjectionLifecycle.Inline);
                options.Projections.Add<ImportHistoryProjection>(ProjectionLifecycle.Inline);

                options.Schema.For<ArquivoMate2.Domain.Notes.DocumentNote>().Index(x => x.DocumentId).Index(x => x.UserId);
                options.Schema.For<ExternalShare>().Index(x => x.DocumentId).Index(x => x.ExpiresAtUtc);
                options.Schema.For<LabPivotTable>().Index(x => x.OwnerId).UniqueIndex(x => x.Id);
            });

            services.AddScoped<IDocumentSession>(sp => sp.GetRequiredService<IDocumentStore>().LightweightSession());
            services.AddScoped<IQuerySession>(sp => sp.GetRequiredService<IDocumentStore>().QuerySession());

            services.AddScoped<IDocumentProcessor, DocumentProcessor>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IFileMetadataService, FileMetadataService>();
            services.AddScoped<IPathService, PathService>();
            services.AddScoped<IThumbnailService, ThumbnailService>();

            services.AddScoped<ArquivoMate2.Application.Features.ISystemFeatureProcessor, LabResultsFeatureProcessor>();
            services.AddScoped<ArquivoMate2.Application.Features.ISystemFeatureProcessorRegistry, ArquivoMate2.Application.Features.SystemFeatureProcessorRegistry>();
            services.Configure<DocumentTypeOptions>(config.GetSection("ServerConfig").GetSection("DocumentTypes"));
            services.AddHostedService<DocumentTypeInitializationService>();

            var meilisearchUrl = config["Meilisearch:Url"] ?? throw new InvalidOperationException("Meilisearch URL is not configured.");
            var meilisearchApiKey = config["Meilisearch:ApiKey"] ?? config["Meilisearch:MasterKey"] ?? config["Meilisearch:Key"] ?? config["MEILI_MASTER_KEY"] ?? "supersecret";
            services.AddScoped<MeilisearchClient>(_ => new MeilisearchClient(meilisearchUrl, meilisearchApiKey));
            services.AddScoped<ISearchClient, SearchClient>();
            services.AddScoped<IDocumentAccessService, DocumentAccessService>();
            services.AddScoped<IAutoShareService, AutoShareService>();
            services.AddHttpClient();

            services.AddScoped<IDocumentOwnershipLookup, DocumentOwnershipLookup>();
            services.AddScoped<IDocumentAccessUpdater, DocumentAccessUpdater>();
            services.AddScoped<ArquivoMate2.Application.Interfaces.ImportHistory.IImportHistoryReadStore, ArquivoMate2.Infrastructure.Services.ImportHistory.ImportHistoryReadStore>();
            services.AddSingleton<ChatBotSettingsFactory>();

            try
            {
                var chatbotSettings = new ChatBotSettingsFactory(config).GetChatBotSettings();
                if (chatbotSettings is OpenAISettings openAISettings)
                {
                    services.AddSingleton(openAISettings);
                    if (openAISettings.EnableEmbeddings)
                        services.AddSingleton<IEmbeddingsClient>(sp => new OpenAiEmbeddingsClient(openAISettings.EmbeddingModel, openAISettings.ApiKey));
                    else
                        services.AddSingleton<IEmbeddingsClient, NullEmbeddingsClient>();

                    var vectorStoreConnection = config.GetConnectionString("VectorStore");
                    if (openAISettings.EnableEmbeddings && !string.IsNullOrWhiteSpace(vectorStoreConnection))
                        services.AddSingleton<IDocumentVectorizationService>(sp => new DocumentVectorizationService(vectorStoreConnection!, openAISettings, sp.GetRequiredService<IEmbeddingsClient>(), sp.GetRequiredService<ILogger<DocumentVectorizationService>>()));
                    else
                        services.AddSingleton<IDocumentVectorizationService, NullDocumentVectorizationService>();

                    services.AddScoped<IChatBot, OpenAIChatBot>(sp =>
                    {

                        ChatClient client = new ChatClient(openAISettings.Model, new ApiKeyCredential(openAISettings.ApiKey), new OpenAI.OpenAIClientOptions()
                        {
                            EnableDistributedTracing = true,
                            NetworkTimeout = TimeSpan.FromSeconds(openAISettings.RequestTimeoutSeconds)
                        });
                        return new OpenAIChatBot(client, sp.GetRequiredService<IDocumentVectorizationService>(), sp.GetRequiredService<ILogger<OpenAIChatBot>>(), openAISettings);
                    });
                }
                else if (chatbotSettings is OpenRouterSettings openRouterSettings)
                {
                    services.AddSingleton(openRouterSettings);
                    services.Configure<OpenRouterSettings>(config.GetSection("ChatBot").GetSection("Args"));
                    services.AddHttpClient<OpenRouterChatBot>(client =>
                    {
                        client.BaseAddress = new Uri(openRouterSettings.BaseUrl);
                        if (!string.IsNullOrWhiteSpace(openRouterSettings.ApiKey))
                            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openRouterSettings.ApiKey);
                    });

                    services.AddScoped<IChatBot, OpenRouterChatBot>();
                    if (openRouterSettings.EnableEmbeddings)
                        services.AddSingleton<IEmbeddingsClient>(sp => new OpenAiEmbeddingsClient(openRouterSettings.EmbeddingModel, openRouterSettings.ApiKey));
                    else
                        services.AddSingleton<IEmbeddingsClient, NullEmbeddingsClient>();

                    var vectorStoreConnection = config.GetConnectionString("VectorStore");
                    if (openRouterSettings.EnableEmbeddings && !string.IsNullOrWhiteSpace(vectorStoreConnection))
                        services.AddSingleton<IDocumentVectorizationService>(sp => new DocumentVectorizationService(vectorStoreConnection!, new OpenAISettings { ApiKey = openRouterSettings.ApiKey, Model = openRouterSettings.Model, EmbeddingModel = openRouterSettings.EmbeddingModel, EmbeddingDimensions = 1536, UseBatch = openRouterSettings.UseBatch, EnableEmbeddings = openRouterSettings.EnableEmbeddings }, sp.GetRequiredService<IEmbeddingsClient>(), sp.GetRequiredService<ILogger<DocumentVectorizationService>>()));
                    else
                        services.AddSingleton<IDocumentVectorizationService, NullDocumentVectorizationService>();
                }
                else
                {
                    services.AddSingleton<IDocumentVectorizationService, NullDocumentVectorizationService>();
                    services.AddSingleton<IChatBot, NullChatBot>();
                }
            }
            catch (Exception)
            {
                services.AddSingleton<IDocumentVectorizationService, NullDocumentVectorizationService>();
                services.AddSingleton<IChatBot, NullChatBot>();
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
                    services.AddSingleton<Microsoft.Extensions.Options.IOptions<S3StorageProviderSettings>>(Microsoft.Extensions.Options.Options.Create(local));
                    services.AddScoped<IStorageProvider, S3StorageProvider>();
                    var endpoint = local.Endpoint;
                    services.AddMinio(configureClient =>
                    {
                        var builder = configureClient.WithEndpoint(endpoint).WithCredentials(local.AccessKey, local.SecretKey).WithSSL(true);
                        builder.Build();
                    });
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
                case SftpIngestionProviderSettings sftp:
                    services.AddSingleton(sftp);
                    services.AddSingleton<IngestionProviderSettings>(sftp);
                    services.AddSingleton<Microsoft.Extensions.Options.IOptions<SftpIngestionProviderSettings>>(Microsoft.Extensions.Options.Options.Create(sftp));
                    services.AddSingleton<IIngestionProvider, SftpIngestionProvider>();
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
                    services.AddScoped<IDeliveryProvider, NoopDeliveryProvider>();
                    break;
                case DeliveryProviderSettings server when server.Type == DeliveryProviderType.Server:
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

            var cachingSection = config.GetSection("Caching");
            services.Configure<CachingOptions>(cachingSection);

            services.AddMemoryCache();

            var redisConfiguration = cachingSection.GetValue<string>("Redis:Configuration") ?? config["Redis:Configuration"] ?? "cache:6379";
            var redisInstanceName = cachingSection.GetValue<string>("Redis:InstanceName") ?? "redis:";
            services.AddStackExchangeRedisCache(options => { options.Configuration = redisConfiguration; options.InstanceName = redisInstanceName; });

            var serializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
            var fusionBuilder = services.AddFusionCache();
            services.Configure<FusionCacheOptions>(options => { options.DefaultEntryOptions = new FusionCacheEntryOptions { Duration = TimeSpan.FromMinutes(5), IsFailSafeEnabled = true, FactorySoftTimeout = TimeSpan.FromSeconds(2), FactoryHardTimeout = TimeSpan.FromSeconds(10), AllowBackgroundDistributedCacheOperations = true }; });
            fusionBuilder.TryWithAutoSetup().WithSerializer(new FusionCacheSystemTextJsonSerializer(serializerOptions));

            services.AddSingleton<ITtlResolver, TtlResolver>();
            services.AddSingleton<IAppCache, EasyToFusionCacheAdapter>();

            var mux = ConnectionMultiplexer.Connect(redisConfiguration);
            services.AddSingleton<IConnectionMultiplexer>(mux);

            services.Configure<EncryptionSettings>(config.GetSection("Encryption"));
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<EncryptionSettings>>().Value);
            services.AddTransient<IEncryptionService, EncryptionService>();
            services.AddTransient<IFileAccessTokenService, FileAccessTokenService>();
            services.Configure<AppSettings>(config.GetSection("App"));
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<AppSettings>>().Value);
            services.AddTransient<IExternalShareService, ExternalShareService>();
            services.AddTransient<IDocumentArtifactStreamer, DocumentArtifactStreamer>();

            services.AddScoped<IDocumentEncryptionKeysProvider, MartenDocumentEncryptionKeysProvider>();

            // LabResults feature services
            services.AddScoped<ILabPivotUpdater, LabPivotUpdater>();
            services.AddScoped<IParameterNormalizer, DefaultParameterNormalizer>();
            services.AddScoped<IUnitNormalizer, DefaultUnitNormalizer>();
            services.AddScoped<IUnitConverter, DefaultUnitConverter>();

            // Document type enricher used to fill DocumentDto with type metadata
            services.AddScoped<ArquivoMate2.Application.Interfaces.IDocumentTypeEnricher, ArquivoMate2.Infrastructure.Services.DocumentTypeEnricher>();

            return services;
        }
    }
}
