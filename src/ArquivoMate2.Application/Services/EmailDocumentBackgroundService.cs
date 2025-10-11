using ArquivoMate2.Application.Commands;
using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.Email;
using ArquivoMate2.Domain.Import;
using ArquivoMate2.Shared.Models;
using Hangfire;
using Marten;
using Marten.Internal.Sessions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ArquivoMate2.Application.Services
{
    public class EmailDocumentBackgroundService : BackgroundService
    {
        private readonly ILogger<EmailDocumentBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

        public EmailDocumentBackgroundService(ILogger<EmailDocumentBackgroundService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("EmailDocumentBackgroundService started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await DoWorkAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception in EmailDocumentBackgroundService: {Message}", ex.Message);
                }

                try
                {
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
            }

            _logger.LogInformation("EmailDocumentBackgroundService stopped.");
        }

        public async Task DoWorkAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IEmailServiceFactory>();
            var processedEmailRepository = scope.ServiceProvider.GetRequiredService<IProcessedEmailRepository>();
            var mediatr = scope.ServiceProvider.GetRequiredService<IMediator>();
            var querySession = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
            var emailCriteriaRepository = scope.ServiceProvider.GetRequiredService<IEmailCriteriaRepository>();

            var services = await factory.CreateEmailServicesAsync(cancellationToken);

            foreach (var service in services)
            {
                var userId = service.UserId;

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("No current user available for email processing.");
                    continue;
                }

                var provider = service;

                if (provider == null)
                {
                    _logger.LogWarning("No email service provider available.");
                    continue;
                }
                if (provider.ProviderType == Shared.Models.EmailProviderType.Null)
                {
                    continue;
                }

                try
                {
                    _logger.LogInformation("Starting email document processing for user {UserId}...", userId);

                    var domainCriteria = await emailCriteriaRepository.GetEmailCriteriaAsync(userId, cancellationToken);
                    
                    // Convert from Domain EmailCriteria to Shared EmailCriteria for the email service
                    ArquivoMate2.Shared.Models.EmailCriteria criteria;
                    if (domainCriteria != null)
                    {
                        // Use the user's saved criteria with built-in conversion method
                        criteria = domainCriteria.ToSharedEmailCriteria();
                    }
                    else
                    {
                        // Use default criteria for document processing
                        criteria = ArquivoMate2.Domain.Email.EmailCriteria.CreateDefaultForDocumentProcessing();
                    }
                    
                    var emails = await provider.GetEmailsAsync(criteria, cancellationToken);
                    if (emails == null || !emails.Any())
                    {
                        _logger.LogInformation("No new emails found for processing.");
                        continue;
                    }

                    foreach (var email in emails)
                    {
                        var alreadyProcessed = await processedEmailRepository.IsEmailProcessedAsync(email.Uid, userId, cancellationToken);
                        if (alreadyProcessed)
                        {
                            continue;
                        }

                        await ProcessSingleEmailAsync(mediatr, querySession, email, provider, processedEmailRepository, userId, cancellationToken);
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing email documents for user {UserId}.", userId);
                }
            }
        }

        private async Task ProcessSingleEmailAsync(
            IMediator mediatr,
            IDocumentSession querySession,
            EmailMessage email,
            IEmailService provider,
            IProcessedEmailRepository processedEmailRepository,
            string userId,
            CancellationToken cancellationToken)
        {
            var processedEmail = new ProcessedEmail
            {
                EmailUid = email.Uid,
                UserId = userId,
                EmailMessageId = email.MessageId,
                Subject = email.Subject,
                From = email.From,
                SourceFolder = email.FolderName
            };

            try
            {
                _logger.LogInformation("Processing email UID {EmailUid} from {From} with subject '{Subject}'",
                    email.Uid, email.From, email.Subject);

                if (!email.HasAttachments)
                {
                    _logger.LogInformation("Moving email UID {EmailUid} without attachments to processed folder", email.Uid);

                    // Move email to processed folder and set flag
                    await provider.MoveEmailWithFlagAsync("INBOX", "INBOX/Processed", email.Uid, "Processed", cancellationToken);

                    processedEmail.Status = EmailProcessingStatus.NoAttachments;
                    processedEmail.DestinationFolder = "INBOX/Processed";

                    _logger.LogDebug("Successfully moved email UID {EmailUid} to processed folder", email.Uid);
                }
                else
                {
                    _logger.LogInformation("Email UID {EmailUid} has {AttachmentCount} attachments - processing for document creation",
                        email.Uid, email.Attachments.Count);

                    foreach (var attachment in email.Attachments)
                    {
                        var guid = await mediatr.Send(new UploadDocumentByMailCommand(provider.UserId, new Models.EmailDocument() { Email = email.From, Subject = email.Subject, File = attachment.Content, FileName = attachment.FileName }), 
                            cancellationToken);

                        var historyEvent = new Domain.Import.InitDocumentImport(
                           Guid.NewGuid(),
                           provider.UserId,
                           attachment.FileName,
                           DateTime.UtcNow,
                           ImportSource.Email);

                        querySession.Events.StartStream<ImportProcess>(historyEvent.AggregateId, historyEvent);
                        await querySession.SaveChangesAsync();

                        BackgroundJob.Enqueue<DocumentProcessingService>("documents", svc => svc.ProcessAsync(guid, historyEvent.AggregateId, provider.UserId));
                    }

                    processedEmail.Status = EmailProcessingStatus.Success;
                }

                await processedEmailRepository.SaveProcessedEmailAsync(processedEmail, cancellationToken);
                _logger.LogDebug("Stored processed email record for UID {EmailUid}", email.Uid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing email UID {EmailUid}: {ErrorMessage}", email.Uid, ex.Message);

                // Store the failed processing record
                processedEmail.Status = EmailProcessingStatus.Failed;
                processedEmail.ErrorMessage = ex.Message;

                try
                {
                    await processedEmailRepository.SaveProcessedEmailAsync(processedEmail, cancellationToken);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Failed to save error record for email UID {EmailUid}", email.Uid);
                }
            }
        }
    }
}
