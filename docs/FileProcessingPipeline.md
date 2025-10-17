# File Processing Pipeline

## Summary
ArquivoMate processes uploaded files through a multi-stage pipeline that ensures validation, normalization, encryption, and indexing. This guide documents the current state for PDFs and images and outlines the work required to support Office documents.

## Current Status
PDF and image processing are production-ready. Office document support is planned and shares the same pipeline principles but still requires conversion and extraction services.

## Key Components
- **Upload services:** Accept files via the web client or API along with ownership, workspace, and retention metadata.
- **Validation:** Enforce MIME-type checks, antivirus scanning, and workspace-specific file policies.
- **Normalization/OCR:** Convert content to archival formats, extract text, and run OCR when needed.
- **Encryption and Storage:** Encrypt processed artifacts and metadata with the workspace key before writing to object storage.
- **Indexing and Notifications:** Publish metadata and extracted text to the search index and emit webhooks/in-app alerts.

## Process Flow
### PDFs
1. Upload with metadata.
2. Validate MIME type and scan for malware.
3. Normalize to PDF/A when possible; flag exceptions for review.
4. Extract text (using OCR when embedded text is missing).
5. Encrypt and store the normalized PDF and metadata.
6. Index the content and notify subscribers.

### Images
1. Upload supported formats (JPEG, PNG, TIFF) with metadata.
2. Run antivirus scanning and enforce size/dimension policies.
3. Normalize to archival PNG and correct orientation using EXIF data.
4. Execute OCR to extract machine-readable text for accessibility and indexing.
5. Encrypt and store the processed image plus OCR results.
6. Index metadata and OCR text, then notify subscribers.

## Future Enhancements
To extend support to Office documents (Word, Excel, PowerPoint):
1. Improve file-type detection and validate digital signatures when present.
2. Integrate a headless conversion service (e.g., LibreOffice) to render Office files into PDF/A or HTML for downstream processing.
3. Implement format-specific parsers that output a unified schema (text, tables, slides) for indexing.
4. Preserve change tracking, comments, and other revision metadata while enforcing redaction policies.
5. Encrypt both the converted output and the original file, storing each for reprocessing or download scenarios.
6. Index structured content (titles, spreadsheet cells, slide notes) and reuse existing webhook notifications.
7. Update governance policies to incorporate new metadata fields and digital-signature validation results.

## References
- `src/ArquivoMate2.Application/Documents/Commands/UploadDocumentCommand.cs`
- `src/ArquivoMate2.Infrastructure/DocumentProcessing`
- `tests/ArquivoMate2.Infrastructure.Tests` (processing pipeline coverage)
