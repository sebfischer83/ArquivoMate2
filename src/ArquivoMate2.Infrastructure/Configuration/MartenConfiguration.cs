using ArquivoMate2.Domain.Document;
using ArquivoMate2.Infrastructure.Persistance;
using Marten;
using Marten.Events;
using Marten.Events.Projections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArquivoMate2.Infrastructure.Configuration
{
    public static class MartenConfiguration
    {
        public static IServiceCollection AddMarten(this IServiceCollection services, IConfiguration config)
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

            return services;
        }
    }
}
