using ArquivoMate2.API.Hubs;
using ArquivoMate2.API.Notifications;
using ArquivoMate2.Application.Handlers;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Services;
using ArquivoMate2.Infrastructure.Configuration;
using ArquivoMate2.Infrastructure.Configuration.Auth;
using Hangfire;
using Hangfire.PostgreSql;
using JasperFx.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
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
using System.Globalization;
using System.Text;
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
            string hangfireConnectionString = builder.Configuration.GetConnectionString("Hangfire");

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
              .ConfigureResource(r => r.AddService("ArquivoMate2"))
              .WithTracing(tracing =>
              {
                  tracing.AddSource(typeof(Program).Assembly.GetName().Name);
                  tracing.AddAspNetCoreInstrumentation();
                  tracing.AddHttpClientInstrumentation();
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

            builder.Services.AddControllers();
            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddHttpContextAccessor();

            builder.Services.AddSignalR().AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
            builder.Services.AddScoped<IDocumentProcessingNotifier, SignalRDocumentProcessingNotifier>();


            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(UploadDocumentHandler).Assembly));
            builder.Services.AddHangfire(config =>
            {
                config.UseSerilogLogProvider();
                config.UseRecommendedSerializerSettings();
                config.UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(hangfireConnectionString));
            });

            builder.Services.AddHangfireServer(options =>
            {
                options.Queues = new[] { "documents" };
                options.WorkerCount = 5;
            });
            builder.Services.AddMemoryCache();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins", policy =>
                {
                    policy
                        .WithOrigins("https://localhost:4200", "http://localhost:4200")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            builder.Services.AddHostedService<EmailDocumentBackgroundService>();

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            AddAuth(builder, builder.Configuration);

            builder.Services.AddPortableObjectLocalization(options =>
            {
                options.ResourcesPath = "Localization";
            });

            builder.Services.AddLocalization(options =>
            {
                options.ResourcesPath = "Localization";
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference(opt =>
                {
                    opt.AddServer("http://localhost:5000", "Local Development");
                });
            }

            app.UseCors("AllowAllOrigins");
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseRequestLocalization(opt =>
            {
                opt.AddSupportedCultures("en-US", "de-DE");
                opt.AddSupportedUICultures("en-US", "de-DE");
                opt.SetDefaultCulture("en-US");
            });

            app.UseHangfireDashboard("/hangfire", new DashboardOptions { });
            app.UseSerilogRequestLogging();

            app.MapControllers();
            app.MapHangfireDashboard();

            app.MapHub<DocumentProcessingHub>("/hubs/documents", opt =>
            {

            }).RequireCors("AllowAllOrigins");

            var supportedCultures = new[] { "en", "de" };
            var localizationOptions = new RequestLocalizationOptions()
                .SetDefaultCulture("de")
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);

            app.UseRequestLocalization(localizationOptions);

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
                                  options.Authority = oidcSettings.Authority;
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

                                  options.Events = new JwtBearerEvents
                                  {
                                      OnMessageReceived = context =>
                                      {
                                          var accessToken = context.Request.Query["access_token"];
                                          var path = context.HttpContext.Request.Path;

                                          if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/documents"))
                                          {
                                              context.Token = accessToken;
                                          }
                                          return Task.CompletedTask;
                                      }
                                  };
                              });
            }

            builder.Services.AddAuthorization();
        }
    }
}
