# Sentinel's Journal

## 2024-05-23 - [Zip Slip Vulnerability Pattern]
**Vulnerability:** Zip Slip allows attackers to overwrite arbitrary files by including `../../` in zip entry names.
**Learning:** Even if modern runtimes (like .NET 6+) patch this internally in `ExtractToDirectory`, reliance on implicit behavior is less secure than explicit validation.
**Prevention:** Always validate that the canonical destination path of a zip entry starts with the canonical target directory path.
