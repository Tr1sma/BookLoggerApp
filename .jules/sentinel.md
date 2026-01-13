## 2024-05-23 - Zip Slip Vulnerability in ImportExportService
**Vulnerability:** The `RestoreFromBackupAsync` method used `ZipFile.ExtractToDirectory` without validating that the extracted file paths were contained within the destination directory. This could allow an attacker to write files outside the intended directory via a crafted zip archive containing `../` traversal sequences.
**Learning:** Even if modern frameworks (like .NET 6+) offer some protection, explicit path validation ("Defense in Depth") is crucial for critical file operations. Always ensure the resolved full path starts with the intended target directory *and* includes a trailing separator to prevent partial path matching bypasses.
**Prevention:** Replace convenient one-liners like `ExtractToDirectory` with manual iteration and validation loops when handling untrusted archives. Verify `!destinationPath.StartsWith(targetDir + Path.DirectorySeparatorChar)` before writing.

## 2024-05-23 - Unrestricted Image Download (DoS Risk)
**Vulnerability:** `ImageService.DownloadImageFromUrlAsync` used `HttpClient.GetAsync` without `HttpCompletionOption.ResponseHeadersRead`, causing the entire response body to be buffered into memory before any checks could be performed. This exposed the application to Denial of Service (DoS) via "zip bomb" or massive file attacks.
**Learning:** `HttpClient.GetAsync` defaults to buffering the entire response. For file downloads, always use `HttpCompletionOption.ResponseHeadersRead` to inspect headers (Content-Length, Content-Type) *before* committing to download the body.
**Prevention:** Always validate `Content-Length` and `Content-Type` headers before reading the response stream for external resources. Enforce reasonable size limits (e.g., 10MB for images).
