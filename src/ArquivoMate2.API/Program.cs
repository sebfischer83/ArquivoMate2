
using ArquivoMate2.Application.Handlers;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Infrastructure.Configuration;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Configuration;

namespace ArquivoMate2.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            string connectionString = builder.Configuration.GetConnectionString("Default");

            builder.Host.UseSerilog((context, config) =>
            {
                config.ReadFrom.Configuration(context.Configuration);
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
                  //tracing.AddNpgsql();
                  tracing.AddOtlpExporter(opt =>
                  {
                      opt.Endpoint = new Uri("http://seq:5341/ingest/otlp/v1/traces");
                      opt.Protocol = OtlpExportProtocol.HttpProtobuf;
                  });
              });

            builder.Services.AddControllers();
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

            builder.Services.AddHangfireServer();

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // URL deines Authentik-OIDC-Endpoints (.well-known/openid-configuration)
                    options.Authority = "https://auth2.modellfrickler.online/application/o/arquivomate2/";
                    // Audience genauso wie in deiner Authentik-Application definiert
                    options.Audience = "egrVGZZH9GkuULNmnpux9Yr9neRhHXyaVup0pEUh";
                    options.RequireHttpsMetadata = true;

                    // Optional: Feineinstellungen
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = "https://auth2.modellfrickler.online/application/o/arquivomate2/",
                        ValidateAudience = true,
                        ValidAudience = "egrVGZZH9GkuULNmnpux9Yr9neRhHXyaVup0pEUh",
                        ValidateLifetime = true
                    };
                });


            builder.Services.AddAuthorization();

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
    }
}
