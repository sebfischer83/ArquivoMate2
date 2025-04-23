
using ArquivoMate2.Application.Handlers;
using ArquivoMate2.Infrastructure.Configuration;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
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
                  tracing.AddOtlpExporter(opt =>
                  {
                      opt.Endpoint = new Uri("http://seq:5341/ingest/otlp/v1/traces");
                      opt.Protocol = OtlpExportProtocol.HttpProtobuf;
                  });
              });

            builder.Services.AddControllers();
            builder.Services.AddMarten(builder.Configuration);

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

            app.UseAuthorization();

            app.UseHangfireDashboard("/hangfire", new DashboardOptions { });
            app.UseSerilogRequestLogging();

            app.MapControllers();
            app.MapHangfireDashboard();
            app.Run();
        }
    }
}
