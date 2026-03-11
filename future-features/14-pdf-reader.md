# PDF Reader

## Description

PDF reading lets the agent extract and reason over attached or referenced PDF documents. On Linux, the most practical parity path is a simple extraction pipeline backed by standard CLI utilities or a .NET PDF text-extraction library.

Current baseline:

- NetClaw has no PDF ingestion path.
- The runtime and channel model can already support a file-processing step before the agent sees the content.
- Linux is a good fit for `pdftotext` or equivalent utilities.

## High-Level Steps

1. Choose an extraction path: `pdftotext` on the host, or a .NET library if quality is sufficient.
2. Add a file-ingestion service that can accept channel attachments or referenced local files.
3. Store extracted text and file metadata in the group workspace.
4. Expose the content either as automatic context or as an explicit tool over stored files.
5. Add limits and error handling for huge PDFs, scanned PDFs, and extraction failures.

## Complexity

Low-Medium. This is one of the easier parity targets as long as OCR-heavy scanned documents are treated as a later enhancement.