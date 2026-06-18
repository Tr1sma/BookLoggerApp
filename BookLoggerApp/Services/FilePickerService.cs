using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Services;

public class FilePickerService : IFilePickerService
{
    private readonly IMigrationService _migrationService;

    public FilePickerService(IMigrationService migrationService)
    {
        _migrationService = migrationService;
    }

    public async Task<string?> PickFileAsync(string pickerTitle, params string[] allowedExtensions)
    {
        _migrationService.Log($"[FilePicker] Starting PickFileAsync. Title: {pickerTitle}");
        try
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.content" } },
                    { DevicePlatform.Android, new[] { "*/*" } },
                    { DevicePlatform.WinUI, allowedExtensions },
                    { DevicePlatform.Tizen, new[] { "*/*" } },
                    { DevicePlatform.macOS, allowedExtensions }
                });

            var options = new PickOptions
            {
                PickerTitle = pickerTitle,
                FileTypes = customFileType
            };

            _migrationService.Log("[FilePicker] Requesting system picker...");
            var result = await FilePicker.Default.PickAsync(options);
            _migrationService.Log($"[FilePicker] Picker returned: {(result == null ? "NULL" : result.FullPath)}");

            if (result == null)
            {
                _migrationService.Log("[FilePicker] Result is null.");
                return null;
            }

            // Google Drive and other providers return a content:// URI with no local path;
            // copy to cache so callers always get a real filesystem path.
            bool isVirtual = string.IsNullOrEmpty(result.FullPath)
                             || result.FullPath.StartsWith("content://")
                             || !File.Exists(result.FullPath);

            _migrationService.Log($"[FilePicker] IsVirtual: {isVirtual} (Path: {result.FullPath})");

            if (isVirtual)
            {
                _migrationService.Log("[FilePicker] Handling virtual file...");
                var cacheFile = Path.Combine(FileSystem.CacheDirectory, result.FileName);
                _migrationService.Log($"[FilePicker] Cache target: {cacheFile}");

                if (File.Exists(cacheFile))
                    File.Delete(cacheFile);

                using var stream = await result.OpenReadAsync();
                try
                {
                    using (var fileStream = File.Create(cacheFile))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                }
                catch
                {
                    // Remove partial file so a future picker run can't treat it as valid.
                    try
                    {
                        if (File.Exists(cacheFile))
                            File.Delete(cacheFile);
                    }
                    catch (Exception cleanupEx)
                    {
                        _migrationService.Log($"[FilePicker] Cleanup of partial cache file failed: {cleanupEx.Message}");
                    }
                    throw;
                }

                _migrationService.Log("[FilePicker] Copied virtual file to cache.");
                return cacheFile;
            }

            return result.FullPath;
        }
        catch (Exception ex)
        {
            _migrationService.Log($"[FilePicker] EXCEPTION: {ex}");
            throw;
        }
    }
}
