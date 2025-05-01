using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.ValueObjects;
using ArquivoMate2.Infrastructure.Configuration.StorageProvider;
using ArquivoMate2.Infrastructure.Mapping;
using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Infrastructure.Services;
using ArquivoMate2.Infrastructure.Services.StorageProvider;
using ArquivoMate2.Shared.Models;
using AutoMapper;
using EasyCaching.Core.Configurations;
using EasyCaching.Serialization.SystemTextJson.Configurations;
using FluentStorage;
using FluentStorage.AWS.Blobs;
using Marten;
using Marten.Events;
using Marten.Events.Projections;
using Marten.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MimeTypes;
using Minio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

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

                // Domain‑Events registrieren
                options.Events.AddEventTypes(new[]
                {
                    typeof(DocumentUploaded),
                    typeof(DocumentProcessed),
                    typeof(DocumentContentExtracted),
                    typeof(DocumentFilesPrepared)
                    // hier weitere Event‑Typen hinzufügen…
                });

                options.Events.StreamIdentity = StreamIdentity.AsGuid;

                options.Projections.Add<DocumentProjection>(ProjectionLifecycle.Inline);
            });

            services.AddScoped<IDocumentSession>(sp => sp.GetRequiredService<IDocumentStore>().LightweightSession());
            services.AddScoped<IQuerySession>(sp => sp.GetRequiredService<IDocumentStore>().QuerySession());

            services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IFileMetadataService, FileMetadataService>();
            services.AddScoped<IPathService, PathService>();
            services.AddScoped<IThumbnailService, ThumbnailService>();


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

            services.AddScoped<IValueResolver<DocumentView, DocumentDto, string>, FilePathResolver>();
            services.AddScoped<IValueResolver<DocumentView, DocumentDto, string>, ThumbnailPathResolver>();
            services.AddScoped<IValueResolver<DocumentView, DocumentDto, string>, MetadataPathResolver>();

            services.AddAutoMapper(typeof(Mapping.DocumentMapping).Assembly);

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
