using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Models;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.Import;
using ArquivoMate2.Domain.Email;
using ArquivoMate2.Domain.ValueObjects;
using ArquivoMate2.Infrastructure.Configuration.DeliveryProvider;
using ArquivoMate2.Infrastructure.Configuration.Llm;
using ArquivoMate2.Infrastructure.Configuration.StorageProvider;
using ArquivoMate2.Infrastructure.Mapping;
using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Infrastructure.Repositories;
using ArquivoMate2.Infrastructure.Services;
using ArquivoMate2.Infrastructure.Services.DeliveryProvider;
using ArquivoMate2.Infrastructure.Services.EmailProvider;
using ArquivoMate2.Infrastructure.Services.Llm;
using ArquivoMate2.Infrastructure.Services.Search;
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

namespace ArquivoMate2.Infrastructure.Configuration
{
    public static class DependencyInjectionConfiguration
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            services.AddMarten(options =>
            {
                // Verbindungszeichenfolge aus appsettings.json
                options.Connection(config.GetConnectionString("Default")!);

                var env = config["ASPNETCORE_ENVIRONMENT"] ?? "Production";
                options.AutoCreateSchemaObjects = env.Equals("Development", StringComparison.OrdinalIgnoreCase)
                    ? JasperFx.AutoCreate.All
                    : JasperFx.AutoCreate.CreateOrUpdate;

                // Domain‑Events registrieren
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
                    typeof(HideDocumentImport)
                });

                options.Schema.For<PartyInfo>();
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

                // Indizes für DocumentView (Sortierung & Filter)
                var docView = options.Schema.For<DocumentView>();
                docView.Index(x => x.UserId);
                docView.Index(x => x.Date);
                docView.Index(x => x.OccurredOn);
                docView.Index(x => x.TotalPrice);
                docView.Index(x => x.Type);
                docView.Index(x => x.Accepted);
                docView.Index(x => x.CustomerNumber);
                docView.Index(x => x.InvoiceNumber);
                // Composite für häufige Sortierung/Filter (user + date)
                docView.Index(x => new { x.UserId, x.Date });
                // Erweiterte partielle / GIN Indizes bei Bedarf per separatem SQL-Migrationsskript hinzufügen (nicht im Code, um Build zu vereinfachen)

                options.Projections.Add<DocumentProjection>(ProjectionLifecycle.Inline);
                options.Projections.Add<ImportHistoryProjection>(ProjectionLifecycle.Inline);
            });

            services.AddScoped<IDocumentSession>(sp => sp.GetRequiredService<IDocumentStore>().LightweightSession());
            services.AddScoped<IQuerySession>(sp => sp.GetRequiredService<IDocumentStore>().QuerySession());

            services.AddScoped<IDocumentProcessor, DocumentProcessor>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IFileMetadataService, FileMetadataService>();
            services.AddScoped<IPathService, PathService>();
            services.AddScoped<IThumbnailService, ThumbnailService>();
            services.AddScoped<MeilisearchClient>(sp => new MeilisearchClient(config["Meilisearch:Url"], "supersecret"));
            services.AddScoped<ISearchClient, SearchClient>();
            services.AddHttpClient();

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

            services.AddScoped<IEmailSettingsRepository, EmailSettingsRepository>();
            services.AddScoped<IProcessedEmailRepository, ProcessedEmailRepository>();
            services.AddScoped<IEmailCriteriaRepository, EmailCriteriaRepository>();
            services.AddScoped<IEmailServiceFactory, EmailServiceFactory>();
            services.AddScoped<IEmailService, NullEmailService>();

            switch (settings)
            {
                case S3StorageProviderSettings local:
                    services.Configure<S3StorageProviderSettings>(config.GetSection("StorageProvider").GetSection("Args"));
                    services.AddScoped<IStorageProvider, S3StorageProvider>();
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

            switch (deliverySettings)
            {
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

            return services;
        }
    }
}
