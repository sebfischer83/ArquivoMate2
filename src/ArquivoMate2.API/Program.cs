
using ArquivoMate2.Application.Handlers;
using ArquivoMate2.Infrastructure.Configuration;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;

namespace ArquivoMate2.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            string connectionString = builder.Configuration.GetConnectionString("Default");
            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddMarten(builder.Configuration);

            // Replace the problematic line with the following:
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(UploadDocumentHandler).Assembly));
            builder.Services.AddHangfire(config =>
                    config.UsePostgreSqlStorage(opt => opt.UseNpgsqlConnection(connectionString)));
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
                    opt.BaseServerUrl = "https://localhost:5000";
                    opt.AddServer("https://localhost:5000", "Local Development");
                });
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.UseHangfireDashboard("/hangfire", new DashboardOptions { });

            app.MapControllers();
            app.MapHangfireDashboard();
            app.Run();
        }
    }
}
