
using ArquivoMate2.Application.Handlers;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration;
using ArquivoMate2.Infrastructure.Configuration.Auth;
using Hangfire;
using Hangfire.PostgreSql;
using JasperFx.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Configuration;
using System.Text.Json.Serialization;

namespace ArquivoMate2.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            //builder.Configuration.AddUserSecrets<Program>();
            builder.Configuration.AddEnvironmentVariables("AMate__");
            string connectionString = builder.Configuration.GetConnectionString("Default");

            var seqUrl = builder.Configuration["Seq:ServerUrl"];
            var seqApiKey = builder.Configuration["Seq:ApiKey"];

            builder.Host.UseSerilog((context, config) =>
            {
                config.ReadFrom.Configuration(context.Configuration);
                if (!string.IsNullOrWhiteSpace(seqUrl))
                {
                    config.WriteTo.Seq(seqUrl, apiKey: seqApiKey);
                }
                config.Enrich.FromLogContext();
                config.Enrich.WithProperty("Application", typeof(Program).Assembly.GetName().Name);
            });

            builder.Services.AddOpenTelemetry()
              .ConfigureResource(r => r.AddService("My Service"))
              .WithTracing(tracing =>
              {
                  tracing.AddSource(typeof(Program).Assembly.GetName().Name);
                  tracing.AddAspNetCoreInstrumentation();
                  tracing.AddHttpClientInstrumentation();
                  tracing.AddConsoleExporter(); 
                  tracing.AddSource("Marten");

                  if (!string.IsNullOrWhiteSpace(seqUrl))
                  {
                      tracing.AddOtlpExporter(opt =>
                      {
                          opt.Endpoint = new Uri("http://seq:5341/ingest/otlp/v1/traces");
                          opt.Protocol = OtlpExportProtocol.HttpProtobuf;
                          opt.Headers = $"X-Seq-ApiKey={seqApiKey}";
                      });
                  }
                 
              });

            builder.Services.AddControllers().AddNewtonsoftJson();
            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddHttpContextAccessor();

            // Replace the problematic line with the following:
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(UploadDocumentHandler).Assembly));
            builder.Services.AddHangfire(config =>
            {
                config.UseSerilogLogProvider();
                config.UseRecommendedSerializerSettings();
                config.UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(connectionString));
            });

            builder.Services.AddHangfireServer(options =>
            {
                options.Queues = new[] { "documents" };
                options.WorkerCount = 5;
            });

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            AddAuth(builder, builder.Configuration);

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference(opt =>
                {
                    opt.AddServer("https://localhost:5000", "Local Development");
                });
            }

            app.UseHttpsRedirection();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseHangfireDashboard("/hangfire", new DashboardOptions { });
            app.UseSerilogRequestLogging();

            app.MapControllers();
            app.MapHangfireDashboard();
            app.Run();
        }

        private static void AddAuth(WebApplicationBuilder builder, ConfigurationManager configuration)
        {
            var config = new AuthSettingsFactory(configuration).GetAuthSettings();

            if (config.Type == AuthType.OIDC)
            {
                var oidcSettings = config as OIDCSettings;

                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                              .AddJwtBearer(options =>
                              {
                                  // URL deines Authentik-OIDC-Endpoints (.well-known/openid-configuration)
                                  options.Authority = oidcSettings.Authority;
                                  // Audience genauso wie in deiner Authentik-Application definiert
                                  options.Audience = oidcSettings.Audience;
                                  options.RequireHttpsMetadata = true;

                                  // Optional: Feineinstellungen
                                  options.TokenValidationParameters = new TokenValidationParameters
                                  {
                                      ValidateIssuer = false,
                                      ValidIssuer = oidcSettings.Issuer,
                                      ValidateAudience = false,
                                      ValidAudience = oidcSettings.Audience,
                                      ValidateLifetime = false
                                  };
                              });
            }



            builder.Services.AddAuthorization();
        }
    }
}
