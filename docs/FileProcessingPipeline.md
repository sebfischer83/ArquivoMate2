# File Processing Pipeline

This guide explains how ArquivoMate processes PDF files and images today, and outlines the path to support Office documents in future releases.

## Current Workflow for PDFs

1. **Upload:** Users submit PDFs through the web client or API. Each upload includes metadata such as document owner, workspace, and retention policy.
2. **Validation:** The service verifies the MIME type and ensures that the file is free of malware using the antivirus scanning service.
3. **Normalization:** PDFs are normalized to PDF/A when possible to guarantee long-term readability. Non-conforming files are flagged for manual review.
4. **Text extraction:** The document passes through the text extraction service that uses OCR when embedded text is unavailable. Extracted content feeds the search index.
5. **Encryption and storage:** The normalized PDF and its metadata are encrypted with the workspace key and stored in the object repository.
6. **Indexing and notification:** Metadata and extracted text are indexed for search. Subscribers are notified about the new document via webhooks and in-app alerts.

## Current Workflow for Images

1. **Upload:** Supported formats (JPEG, PNG, TIFF) are uploaded with contextual metadata.
2. **Validation:** The antivirus service scans the image. The system also checks the file dimensions and size against workspace policies.
3. **Pre-processing:** Images are normalized to an archival format (lossless PNG) and orientation is corrected using EXIF data when available.
4. **OCR:** The OCR pipeline detects text regions and extracts machine-readable content. The output is attached as alternate text for accessibility and indexing.
5. **Encryption and storage:** The processed image and OCR results are encrypted with the workspace key and stored in the object repository.
6. **Indexing and notification:** Metadata and OCR text are indexed. Consumers subscribed to the workspace receive notifications about the new asset.

## Enabling Office Document Support

To extend the pipeline to Office documents (Word, Excel, PowerPoint), ArquivoMate can follow these steps:

1. **File type detection:** Enhance the upload validator to recognize Office MIME types and verify digital signatures when present.
2. **Conversion service:** Integrate a headless conversion engine (such as LibreOffice in server mode) to transform Office files into PDF/A or HTML. This ensures consistency with existing archival and OCR steps.
3. **Content extraction:** Use format-specific parsers to extract structured content (text, tables, slides) and convert it into a unified schema for indexing.
4. **Change tracking preservation:** Store revision history and comments where available to maintain auditability. Sensitive fields should be redacted according to workspace policies.
5. **Encryption and storage:** Encrypt the converted output and original Office file with the workspace key. Store both versions to support future reprocessing and downloads.
6. **Indexing and notification:** Index structured data such as document titles, spreadsheet cell values, and presentation slide notes. Notify subscribers using the existing webhook infrastructure.
7. **Governance updates:** Update compliance rules to account for new metadata fields, retention policies, and digital signature validation results.

Implementing these enhancements reuses the established pipeline while adding the conversion and extraction capabilities required for Office formats.
