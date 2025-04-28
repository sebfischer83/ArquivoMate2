using ArquivoMate2.Application.Configuration;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Document;
using ArquivoMate2.Domain.ValueObjects;
using ArquivoMate2.Infrastructure.Configuration.StorageProvider;
using ArquivoMate2.Infrastructure.Persistance;
using ArquivoMate2.Infrastructure.Services;
using ArquivoMate2.Infrastructure.Services.StorageProvider;
using FluentStorage;
using FluentStorage.AWS.Blobs;
using Marten;
using Marten.Events;
using Marten.Events.Projections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
                options.Connection(config.GetConnectionString("Default"));

                // Domain‑Events registrieren
                options.Events.AddEventTypes(new[]
                {
                    typeof(DocumentUploaded)
                    // hier weitere Event‑Typen hinzufügen…
                });

                // Stream‑Identity (GUIDs)
                options.Events.StreamIdentity = StreamIdentity.AsGuid;

                // Projektionen für Query‑Models
                options.Projections.Add<DocumentProjection>(ProjectionLifecycle.Inline);
            });

            // Für CQRS: Lightweight Sessions
            services.AddScoped<IDocumentSession>(sp => sp.GetRequiredService<IDocumentStore>().LightweightSession());
            services.AddScoped<IQuerySession>(sp => sp.GetRequiredService<IDocumentStore>().QuerySession());

            // Services
            services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IFileMetadataService, FileMetadataService>();
            services.AddScoped<IPathService, PathService>();


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
                    StorageFactory.Modules.UseAwsStorage();
                    services.AddScoped<IAwsS3BlobStorage>(sp =>
                    {
                        var options = sp.GetRequiredService<IOptions<S3StorageProviderSettings>>().Value;
                        return (IAwsS3BlobStorage) StorageFactory.Blobs.FromConnectionString($"aws.s3://keyId={options.AccessKey};key={options.SecretKey};bucket={options.BucketName};serviceUrl={options.Endpoint}");
                    });
                    break;
                // … weitere Fälle …
                default:
                    throw new InvalidOperationException("Unsupported FileProviderSettings");
            }

            return services;
        }
    }
}
