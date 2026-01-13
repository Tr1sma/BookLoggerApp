## 2024-05-23 - Zip Slip Vulnerability in ImportExportService
**Vulnerability:** The `RestoreFromBackupAsync` method used `ZipFile.ExtractToDirectory` without validating that the extracted file paths were contained within the destination directory. This could allow an attacker to write files outside the intended directory via a crafted zip archive containing `../` traversal sequences.
**Learning:** Even if modern frameworks (like .NET 6+) offer some protection, explicit path validation ("Defense in Depth") is crucial for critical file operations. Always ensure the resolved full path starts with the intended target directory *and* includes a trailing separator to prevent partial path matching bypasses.
**Prevention:** Replace convenient one-liners like `ExtractToDirectory` with manual iteration and validation loops when handling untrusted archives. Verify `!destinationPath.StartsWith(targetDir + Path.DirectorySeparatorChar)` before writing.


## 2024-05-23 - Unrestricted Image Download (DoS Risk)
**Vulnerability:** `ImageService.DownloadImageFromUrlAsync` used `HttpClient.GetAsync` without `HttpCompletionOption.ResponseHeadersRead`, causing the entire response body to be buffered into memory before any checks could be performed. This exposed the application to Denial of Service (DoS) via "zip bomb" or massive file attacks.
**Learning:** `HttpClient.GetAsync` defaults to buffering the entire response. For file downloads, always use `HttpCompletionOption.ResponseHeadersRead` to inspect headers (Content-Length, Content-Type) *before* committing to download the body.
**Prevention:** Always validate `Content-Length` and `Content-Type` headers before reading the response stream for external resources. Enforce reasonable size limits (e.g., 10MB for images).

## 2024-05-24 - Zip Bomb Vulnerability in ImportExportService
**Vulnerability:** The `RestoreFromBackupAsync` method extracted files without checking the total size or number of entries. A malicious "Zip Bomb" (e.g., highly compressed file) could cause Denial of Service (DoS) by exhausting disk space or memory during extraction.
**Learning:** Validating individual file paths (Zip Slip) is not enough; you must also validate resource consumption. Compressed archives can expand to orders of magnitude larger than their compressed size.
**Prevention:** Implement resource limits during extraction:
1. Limit total number of entries (e.g., 10,000).
2. Limit total uncompressed size (e.g., 1GB).
3. Validate these limits incrementally inside the extraction loop.

## 2024-05-24 - Missing Input Validation in Data Import
**Vulnerability:** The `ImportExportService` allowed importing JSON and CSV data directly into the database without validation. This could lead to data corruption, application instability (e.g., crashing on invalid enum values), or logical errors (e.g., future dates, negative page counts) if a malicious or malformed file was imported.
**Learning:** Deserialization libraries (like `System.Text.Json` or `CsvHelper`) do not automatically enforce business rules or data annotations. Explicit validation is required at the boundary where data enters the system.
**Prevention:** Inject `IValidator<T>` (e.g., FluentValidation) into import services and validate each entity before persistence. Log and skip invalid entries instead of failing the entire operation to improve resilience.
