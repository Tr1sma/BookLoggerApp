## 2024-05-23 - Zip Slip Vulnerability in Backup Restore
**Vulnerability:** Found usage of `ZipFile.ExtractToDirectory` in `ImportExportService.RestoreFromBackupAsync`. This method does not validate that extracted files lie within the destination directory, allowing malicious zip files (with entries like `../../evil.exe`) to write arbitrary files to the system.
**Learning:** Even in local-first apps, file import features can be vectors for attack if the file comes from an untrusted source (e.g., shared backups).
**Prevention:** Always manually iterate zip entries, resolve the full path, and verify it starts with the target directory path using `Path.GetFullPath` and `StartsWith` before extracting.
