using ArquivoMate2.API.Filters;
using ArquivoMate2.API.Hubs;
using ArquivoMate2.API.HealthChecks;
using ArquivoMate2.API.Maintenance;
using ArquivoMate2.Application.Handlers;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Application.Services;
using ArquivoMate2.Infrastructure.Configuration;
using ArquivoMate2.Infrastructure.Configuration.IngestionProvider;
using ArquivoMate2.Infrastructure.Configuration.Auth;
using Hangfire;
using Hangfire.PostgreSql;
using JasperFx.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Configuration;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Threading;
using Microsoft.OpenApi.Models; // added for OpenApiInfo
using ArquivoMate2.Shared.Models.Sharing; // added for enum schema mapping
using Microsoft.OpenApi.Any; // for OpenApiString
using ArquivoMate2.API.Middleware;
using System.Collections.Generic; // for KeyValuePair in OTel attributes
using Microsoft.Extensions.Options; // added for options access
using ArquivoMate2.Infrastructure.Services; // for LanguageDetectionService & options
using ArquivoMate2.Application.Behaviors; // register pipeline behavior

namespace ArquivoMate2.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Configuration.AddEnvironmentVariables("AMate__");
            string connectionString = builder.Configuration.GetConnectionString("Default")!;
            string hangfireConnectionString = builder.Configuration.GetConnectionString("Hangfire")!;

            var seqUrl = builder.Configuration["Seq:ServerUrl"]; 
            var seqApiKey = builder.Configuration["Seq:ApiKey"]; 
            var cachingOtelSection = builder.Configuration.GetSection("Caching:Otel");
            var cacheServiceName = cachingOtelSection?["ServiceName"];
            var cacheOtlpEndpoint = cachingOtelSection?["Endpoint"];

            builder.Host.UseSerilog((context, config) =>
            {
                config.ReadFrom.Configuration(context.Configuration);
                if (!string.IsNullOrWhiteSpace(seqUrl))
                {
                    config.WriteTo.Seq(seqUrl, apiKey: seqApiKey, restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Verbose);
                }
                config.Enrich.FromLogContext();
                config.Enrich.WithProperty("Application", typeof(Program).Assembly.GetName().Name);
                // Added: Environment enrichment for distinguishing Dev/Staging/Production in Seq
                config.Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName);
            });

            builder.Services.AddOpenTelemetry()
              .ConfigureResource(r => r
                    // Extended: include service version, instance id & deployment environment
                    .AddService(
                        serviceName: cacheServiceName ?? "ArquivoMate2",
                        serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString(),
                        serviceInstanceId: Environment.MachineName)
                    .AddAttributes(new[]
                    {
                        new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName)
                    })
              )
              .WithTracing(tracing =>
              {
                  tracing.AddSource(typeof(Program).Assembly.GetName().Name!);
                  tracing.AddAspNetCoreInstrumentation();
                  tracing.AddHttpClientInstrumentation();
                  tracing.AddSource("Marten");
                  // Ensure custom controller activity source is captured
                  tracing.AddSource("ArquivoMate2.DocumentsController");
                  tracing.AddSource("App.Caching");
                  // Capture sub-operation activity sources from handlers and infrastructure
                  tracing.AddSource("ArquivoMate2.GetDocumentListHandler");
                  tracing.AddSource("ArquivoMate2.SearchClient");
                  // Capture MediatR pipeline activities
                  tracing.AddSource("ArquivoMate2.MediatRPipeline");
                  //tracing.AddNpgsql();

                  if (!string.IsNullOrWhiteSpace(seqUrl))
                  {
                      tracing.AddOtlpExporter(opt =>
                      {
                          opt.Endpoint = new Uri($"{seqUrl}ingest/otlp/v1/traces");
                          opt.Protocol = OtlpExportProtocol.HttpProtobuf;
                          opt.Headers = $"X-Seq-ApiKey={seqApiKey}";
                      });
                  }

                  if (!string.IsNullOrWhiteSpace(cacheOtlpEndpoint))
                  {
                      tracing.AddOtlpExporter(opt =>
                      {
                          opt.Endpoint = new Uri(cacheOtlpEndpoint);
                      });
                  }
              });

            // Register ApiResponse wrapper filter globally for all controllers
            builder.Services.AddControllers(options =>
            {
                options.Filters.Add<ApiResponseWrapperFilter>();
            });
            builder.Services.AddScoped<ApiKeyAuthorizationFilter>();
            builder.Services.AddScoped<ApiResponseWrapperFilter>();
            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSingleton<IDocumentEncryptionKeysExportStore, FileSystemDocumentEncryptionKeysExportStore>();
            builder.Services.AddScoped<IDocumentEncryptionKeysExportService, DocumentEncryptionKeysExportService>();
            builder.Services.AddTransient<DocumentEncryptionKeysExportJob>();
            builder.Services.AddTransient<MaintenanceExportCleanupJob>();

            builder.Services.AddSignalR().AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
            // register notifier implementation
            builder.Services.AddScoped<ArquivoMate2.API.Notifications.SignalRDocumentProcessingNotifier>();
            builder.Services.AddScoped<IDocumentProcessingNotifier>(sp => sp.GetRequiredService<ArquivoMate2.API.Notifications.SignalRDocumentProcessingNotifier>());

            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(UploadDocumentHandler).Assembly));
            // Register tracing pipeline for MediatR
            builder.Services.AddTransient(typeof(MediatR.IPipelineBehavior<,>), typeof(TracingBehavior<,>));
            builder.Services.AddHangfire(config =>
            {
                config.UseSerilogLogProvider();
                config.UseRecommendedSerializerSettings();
                // Increase distributed lock timeout to tolerate lingering DB sessions or transient contention on startup
                config.UsePostgreSqlStorage(hangfireConnectionString, new PostgreSqlStorageOptions
                {
                    DistributedLockTimeout = TimeSpan.FromSeconds(30)
                });
            });

            // Grouping service registration (Infrastructure implementation)
            builder.Services.AddScoped<ArquivoMate2.Application.Interfaces.Grouping.IDocumentGroupingService, ArquivoMate2.Infrastructure.Services.Grouping.DocumentGroupingService>();

            builder.Services.AddHangfireServer(options =>
            {
                options.Queues = new[] { "documents", "maintenance" };
                options.WorkerCount = 5;
            });
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

            // Language detection options & service registration
            // Expects configuration section "LanguageDetection" with e.g.:
            // "LanguageDetection": { "SupportedLanguages": ["de", "en", "fr"] }
            builder.Services.Configure<LanguageDetectionOptions>(builder.Configuration.GetSection("LanguageDetection"));
            builder.Services.AddSingleton<ILanguageDetectionService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<LanguageDetectionService>>();
                var opts = sp.GetRequiredService<IOptions<LanguageDetectionOptions>>().Value;
                return new LanguageDetectionService(logger, opts);
            });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "ArquivoMate2 API", Version = "v1" });
                var basePath = AppContext.BaseDirectory;
                foreach (var xml in Directory.GetFiles(basePath, "*.xml", SearchOption.TopDirectoryOnly))
                {
                    try { c.IncludeXmlComments(xml, includeControllerXmlComments: true); } catch { }
                }

                // Map ShareTargetType as string enum (explicit to override default numeric schema)
                c.MapType<ShareTargetType>(() => new OpenApiSchema
                {
                    Type = "string",
                    Enum = Enum.GetNames(typeof(ShareTargetType))
                        .Select(n => (IOpenApiAny)new OpenApiString(n))
                        .ToList()
                });

                // Map DocumentPermissions as array of strings (flags)
                c.MapType<DocumentPermissions>(() => new OpenApiSchema
                {
                    Type = "array",
                    Items = new OpenApiSchema
                    {
                        Type = "string",
                        Enum = new[] { "Read", "Edit", "Delete" }
                            .Select(n => (IOpenApiAny)new OpenApiString(n))
                            .ToList()
                    },
                    Description = "Flags: subset of [Read, Edit, Delete]. Empty array = None. Accepts legacy formats: comma string or numeric." 
                });

                // Use operation filter to show ApiResponse<T> wrapper for successful responses
                c.OperationFilter<ArquivoMate2.API.Swagger.ApiResponseOperationFilter>();
            });

            AddAuth(builder, builder.Configuration);

            builder.Services.AddPortableObjectLocalization(options =>
            {
                options.ResourcesPath = "Localization";
            });

            builder.Services.AddLocalization(options =>
            {
                options.ResourcesPath = "Localization";
            });

            builder.Services.AddHealthChecks()
                .AddNpgSql(connectionString, name: "pgsql")
                .AddCheck<MeilisearchHealthCheck>("meilisearch");

            var app = builder.Build();

            // Register middleware for producing ProblemDetails on unhandled exceptions
            app.UseMiddleware<ProblemDetailsMiddleware>();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger(c =>
                {
                    c.RouteTemplate = "openapi/{documentName}.json";
                });
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/openapi/v1.json", "ArquivoMate2 API v1");
                    c.RoutePrefix = "swagger";
                });
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
            app.UseSerilogRequestLogging((options) =>
            {
                options.IncludeQueryInRequestPath = true;
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
                options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
                {
                    diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? string.Empty);
                    diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                };
            });

            app.MapControllers();
            // controllers are already configured with the ApiResponse wrapper filter

            app.MapHealthChecks("/healthz");

            app.MapHangfireDashboard();

            app.MapHub<DocumentProcessingHub>("/hubs/documents", opt => { }).RequireCors("AllowAllOrigins");

            var supportedCultures = new[] { "en", "de" };
            var localizationOptions = new RequestLocalizationOptions()
                .SetDefaultCulture("de")
                .AddSupportedCultures(supportedCultures)
                .AddSupportedUICultures(supportedCultures);

            app.UseRequestLocalization(localizationOptions);

            // Use retry wrapper for recurring job registration to tolerate transient distributed lock issues
            TryAddOrUpdateRecurringJob(() => RecurringJob.AddOrUpdate<MaintenanceExportCleanupJob>(
                "maintenance-export-cleanup",
                job => job.ExecuteAsync(CancellationToken.None),
                Cron.Daily));

            using (var scope = app.Services.CreateScope())
            {
                var ingestionSettings = scope.ServiceProvider.GetService<IngestionProviderSettings>();

                switch (ingestionSettings)
                {
                    case FileSystemIngestionProviderSettings fileSystem when fileSystem.Type == IngestionProviderType.FileSystem:
                        ScheduleIngestionJob("filesystem-ingestion", fileSystem.PollingInterval);
                        break;
                    case S3IngestionProviderSettings s3 when s3.Type == IngestionProviderType.S3:
                        ScheduleIngestionJob("s3-ingestion", s3.PollingInterval);
                        break;
                }
            }

            app.Run();
        }

        private static void ScheduleIngestionJob(string jobId, TimeSpan pollingInterval)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling(pollingInterval.TotalMinutes));
            var cronExpression = Cron.MinuteInterval(minutes);

            TryAddOrUpdateRecurringJob(() => RecurringJob.AddOrUpdate<IngestionBackgroundJob>(
                jobId,
                job => job.ExecuteAsync(CancellationToken.None),
                cronExpression));
        }

        // Helper to retry recurring job registration when Postgres advisory lock is temporarily unavailable
        private static void TryAddOrUpdateRecurringJob(Action addOrUpdateAction)
        {
            const int maxAttempts = 3;
            var delay = TimeSpan.FromSeconds(5);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    addOrUpdateAction();
                    return;
                }
                catch (PostgreSqlDistributedLockException ex)
                {
                    // Last attempt -> rethrow so the application startup fails visibly
                    if (attempt == maxAttempts)
                    {
                        throw;
                    }

                    // brief backoff
                    Thread.Sleep(delay);
                }
            }
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
                                  options.Authority = oidcSettings!.Authority;
                                  options.Audience = oidcSettings.Audience;
                                  options.RequireHttpsMetadata = true;
                                  options.MapInboundClaims = false;

                                  options.TokenValidationParameters = new TokenValidationParameters
                                  {
                                      ValidateIssuer = false,
                                      ValidIssuer = oidcSettings.Issuer,
                                      ValidateAudience = false,
                                      ValidAudience = oidcSettings.Audience,
                                      ValidateLifetime = false,
                                      NameClaimType = "name",
                                      RoleClaimType = "roles"
                                  };

                                  options.Events = new JwtBearerEvents
                                  {
                                      OnMessageReceived = context =>
                                      {
                                          var accessToken = context.Request.Query["access_token"];
                                          var path = context.HttpContext.Request.Path;

                                          if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/documents"))
                                          {
                                              context.Token = accessToken!;
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
