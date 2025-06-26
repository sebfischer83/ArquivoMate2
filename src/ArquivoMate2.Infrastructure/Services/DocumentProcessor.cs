using ArquivoMate2.Application.Interfaces;
using ArquivoMate2.Domain.ValueObjects;
using ImageMagick;
using JasperFx.Core;
using Microsoft.AspNetCore.Components.Forms;
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
    public class DocumentProcessor : IDocumentProcessor
    {
        private readonly string _tesseractPath = "tesseract"; // Path to the Tesseract CLI tool.
        private readonly ILogger<DocumentProcessor> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocumentProcessor"/> class.
        /// </summary>
        /// <param name="logger">Logger for logging information and warnings.</param>
        public DocumentProcessor(ILogger<DocumentProcessor> logger)
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

        public async Task<byte[]> GeneratePreviewPdf(Stream documentStream, DocumentMetadata documentMetadata, CancellationToken cancellationToken = default)
        {
            var tempInput = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
            var tempOutputBase = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var tempOutput = tempOutputBase + ".pdf";

            try
            {
                // Input-Stream in Datei schreiben
                documentStream.Position = 0;
                using (var fs = new FileStream(tempInput, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await documentStream.CopyToAsync(fs, 81920, cancellationToken).ConfigureAwait(false);
                }

                var args = $"--output-type pdfa --optimize 3 \"{tempInput}\" \"{tempOutput}\" ";

                // Process starten
                var psi = new ProcessStartInfo
                {
                    FileName = "ocrmypdf",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = new Process { StartInfo = psi })
                {
                    proc.Start();

                    string stdOut = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                    string stdErr = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);

                    using (cancellationToken.Register(() => proc.Kill()))
                    {
                        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                if (!File.Exists(tempOutput))
                {
                    args = $"--output-type pdfa --skip-text --optimize 3 \"{tempInput}\" \"{tempOutput}\" ";
                    psi = new ProcessStartInfo
                    {
                        FileName = "ocrmypdf",
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using (var proc = new Process { StartInfo = psi })
                    {
                        proc.Start();

                        string stdOut = await proc.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                        string stdErr = await proc.StandardError.ReadToEndAsync().ConfigureAwait(false);

                        using (cancellationToken.Register(() => proc.Kill()))
                        {
                            await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }
                }

                // Ausgabe lesen
                byte[] result = await File.ReadAllBytesAsync(tempOutput, cancellationToken).ConfigureAwait(false);
                return result;
            }
            finally
            {
                // Aufräumen
                TryDelete(tempInput);
                TryDelete(tempOutput);
            }
        }
        private void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* Ignorieren */ }
        }
    }
}
