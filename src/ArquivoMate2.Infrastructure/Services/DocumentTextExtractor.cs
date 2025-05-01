using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.ValueObjects;
using ImageMagick;
using JasperFx.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace ArquivoMate2.Infrastructure.Services
{
    /// <summary>
    /// Service for extracting text from PDF documents.
    /// Supports both direct text extraction and OCR (Optical Character Recognition) for image-based PDFs.
    /// </summary>
    public class DocumentTextExtractor : IDocumentTextExtractor
    {
        private readonly string _tesseractPath = "tesseract"; // Path to the Tesseract CLI tool.
        private readonly ILogger<DocumentTextExtractor> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentTextExtractor"/> class.
        /// </summary>
        /// <param name="logger">Logger for logging information and warnings.</param>
        public DocumentTextExtractor(ILogger<DocumentTextExtractor> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Extracts text from a PDF document.
        /// If the document contains selectable text, it uses PdfPig for extraction.
        /// If no text is found or OCR is forced, it uses Tesseract OCR to extract text from images.
        /// </summary>
        /// <param name="documentStream">The PDF document stream.</param>
        /// <param name="documentMetadata">Metadata about the document, including language settings.</param>
        /// <param name="forceOcr">Forces OCR even if text is found in the document.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>The extracted text as a string.</returns>
        /// <exception cref="Exception">Thrown if Tesseract fails during OCR.</exception>
        public async Task<string> ExtractPdfTextAsync(Stream documentStream, DocumentMetadata documentMetadata, bool forceOcr, CancellationToken cancellationToken = default)
        {
            documentStream.Position = 0;
            string text;

            if (!forceOcr)
            {
                // Attempt to extract text using PdfPig
                using (var pdf = PdfDocument.Open(documentStream))
                {
                    text = string.Concat(pdf.GetPages().Select(p => p.Text));
                }
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogInformation("Extracted {Length} characters from PDF document with PdfPig for document {DocumentId}", text.Length, documentMetadata.DocumentId);
                    return text;
                }
                else
                {
                    _logger.LogWarning("No text found in PDF document with PdfPig");
                }
            }

            // Reset stream position for OCR processing
            documentStream.Position = 0;
            using var images = new MagickImageCollection();
            images.Read(documentStream, new MagickReadSettings { Density = new Density(150) });
            _logger.LogInformation("Extracted {Count} images from PDF document", images.Count);

            // Join the languages for Tesseract OCR
            var languages = documentMetadata.Languages.Join("+");
            var result = new StringBuilder();
            foreach (var img in images)
            {
                // Temporary image file
                var tmpImage = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
                img.Format = MagickFormat.Png;
                img.Write(tmpImage);

                _logger.LogInformation("Extracting text from image with Tesseract OCR: {ImagePath}", tmpImage);
                // Call Tesseract CLI: Output to STDOUT
                var psi = new ProcessStartInfo(_tesseractPath,
                    $"-l {languages} {tmpImage} stdout")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi)!;
                var ocrText = await proc.StandardOutput.ReadToEndAsync(cancellationToken);
                var err = await proc.StandardError.ReadToEndAsync(cancellationToken);
                proc.WaitForExit();

                if (proc.ExitCode != 0)
                    throw new Exception($"Tesseract failed: {err}");

                result.AppendLine(ocrText);
                File.Delete(tmpImage);
            }
            return result.ToString();
        }
    }
}
