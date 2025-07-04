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
                options.AutoCreateSchemaObjects = AutoCreate.All;

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
                    typeof(MarkSuccededDocumentImport),
                    typeof(StartDocumentImport),
                    typeof(DocumentProcessed),
                    typeof(HideDocumentImport)
                    // hier weitere Event‑Typen hinzufügen…
                });

                options.Schema.For<PartyInfo>();
                options.Schema.For<EmailSettings>()
                    .Index(x => x.UserId)
                    .Index(x => x.IsActive);

                options.Schema.For<ProcessedEmail>()
                    .Index(x => x.UserId)
                    .Index(x => x.EmailUid)
                    .Index(x => x.Status);

                options.Events.StreamIdentity = StreamIdentity.AsGuid;

                options.Schema.For<Document>()
                    .Index(d => d.UserId).Index(d => d.Hash);

                options.Schema.For<ImportProcess>()
                    .Index(d => d.UserId)
                    .Index(x => x.IsHidden)
                    .Index(x => x.DocumentId)
                    .Index(x => x.Status)
                    .Index(x => x.Source); // Add index for ImportSource for efficient queries

                options.Schema.For<ImportHistoryView>()
                    .Index(x => x.UserId)
                    .Index(x => x.Status)
                    .Index(x => x.Source)  // Add index for ImportSource filtering
                    .Index(x => x.IsHidden);

                //options.Advanced.UseNGramSearchWithUnaccent = true;

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
            services.AddScoped<MeilisearchClient>(sp =>
            {
                return new MeilisearchClient(config["Meilisearch:Url"], "supersecret");
            });

            services.AddScoped<ISearchClient, SearchClient>();

            services.AddHttpClient();

            services.AddSingleton<ChatBotSettingsFactory>();
            var chatbotSettings = new ChatBotSettingsFactory(config).GetChatBotSettings();

            if (chatbotSettings is OpenAISettings openAISettings)
            {
                services.AddSingleton(openAISettings);
                if (openAISettings.UseBatch)
                {
                    services.AddScoped<IChatBot, OpenAIBatchChatBot>(service =>
                    {
                        var opt = service.GetRequiredService<OpenAISettings>();
                        BatchClient client = new BatchClient(openAISettings.ApiKey);
                        var bot = new OpenAIBatchChatBot(client);
                        return bot;
                    });
                }
                else
                {
                    services.AddScoped<IChatBot, OpenAIChatBot>(service =>
                    {
                        var opt = service.GetRequiredService<OpenAISettings>();
                        ChatClient client = new(model: opt.Model, apiKey: opt.ApiKey);
                        var bot = new OpenAIChatBot(client);
                        return bot;
                    });
                }
            }
            else
            {
                throw new InvalidOperationException("Unsupported ChatBotSettings");
            }

            // config
            services.Configure<OcrSettings>(
                config.GetSection("OcrSettings"));

            services.AddSingleton(sp =>
                sp.GetRequiredService<IOptions<OcrSettings>>().Value);

            services.Configure<Paths>(
                config.GetSection("Paths"));

            services.AddSingleton(sp =>
                sp.GetRequiredService<IOptions<Paths>>().Value);

            services.AddSingleton<StorageProviderSettingsFactory>();
            var settings = new StorageProviderSettingsFactory(config).GetsStorageProviderSettings();

            services.AddSingleton<DeliveryProviderSettingsFactory>();
            var deliverySettings = new DeliveryProviderSettingsFactory(config).GetDeliveryProviderSettings();

            // Email Configuration - Only database-based, no JSON fallback
            services.AddScoped<IEmailSettingsRepository, EmailSettingsRepository>();
            services.AddScoped<IProcessedEmailRepository, ProcessedEmailRepository>();
            services.AddScoped<IEmailServiceFactory, EmailServiceFactory>();
            
            // Register NullEmailService as default - no functionality until user configures settings
            services.AddScoped<IEmailService, NullEmailService>();

            switch (settings)
            {
                case S3StorageProviderSettings local:
                    services.Configure<S3StorageProviderSettings>(
                         config.GetSection("StorageProvider").GetSection("Args"));
                    services.AddScoped<IStorageProvider, S3StorageProvider>();

                    var endpoint = local.Endpoint;

                    services.AddMinio(configureClient => configureClient
                        .WithEndpoint(endpoint)
                                        .WithCredentials(local.AccessKey, local.SecretKey)
                                        .WithSSL(true)
                                    .Build());
                    break;
                // … weitere Fälle …
                default:
                    throw new InvalidOperationException("Unsupported FileProviderSettings");
            }

            switch (deliverySettings)
            {
                case S3DeliveryProviderSettings s3:
                    services.Configure<S3DeliveryProviderSettings>(
                        config.GetSection("DeliveryProvider").GetSection("Args"));
                    services.AddScoped<IDeliveryProvider, S3DeliveryProvider>();
                    // currently s3 delivery must be the same settings than storage
                    break;

                case BunnyDeliveryProviderSettings bunny:
                    services.Configure<BunnyDeliveryProviderSettings>(
                        config.GetSection("DeliveryProvider").GetSection("Args"));
                    //services.AddScoped<IDeliveryProvider, BunnyDeliveryProvider>();
                    // Hier ggf. BunnyNet-Client registrieren
                    break;

                default:
                    throw new InvalidOperationException("Unsupported DeliveryProviderSettings");
            }

            services.AddScoped<IMemberValueResolver<DocumentView, BaseDto, string, string>, PathResolver>();

            services.AddAutoMapper(typeof(Mapping.DocumentMapping).Assembly);

            services.AddHostedService<DatabaseMigrationService>();
            services.AddHostedService<MeiliInitService>();

            services.AddEasyCaching(x =>
                            x.UseRedis(r =>
                            {
                                r.EnableLogging = false;
                                r.DBConfig.KeyPrefix = "redis" + ":";
                                r.SerializerName = "A";
                                r.DBConfig.Endpoints.Add(new EasyCaching.Core.Configurations.ServerEndPoint("cache", int.Parse("6379")));
                                r.DBConfig.AbortOnConnectFail = false;
                            }).WithSystemTextJson("A")
            );

            return services;
        }
    }
}
