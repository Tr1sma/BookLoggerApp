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
            // Log only the file name, never the full path (which can carry user/account folders).
            _migrationService.Log($"[FilePicker] Picker returned: {(result == null ? "NULL" : result.FileName)}");

            if (result == null)
            {
                _migrationService.Log("[FilePicker] Result is null.");
                return null;
            }

            // Start Modification: Handle virtual files (e.g. Google Drive) where FullPath is null
            // Check if path is usable (local file)
            // If FullPath is null, OR it's a content URI, OR it doesn't exist on disk -> treat as virtual
            bool isVirtual = string.IsNullOrEmpty(result.FullPath)
                             || result.FullPath.StartsWith("content://")
                             || !File.Exists(result.FullPath);

            _migrationService.Log($"[FilePicker] IsVirtual: {isVirtual} (File: {result.FileName})");

            if (isVirtual)
            {
                _migrationService.Log("[FilePicker] Handling virtual file...");
                // Z.557: copy to a GUID-prefixed cache file so two picks of same-named files (or a
                // retry after a partial copy) can't collide on one path. The returned temp file is
                // owned by the caller, which consumes it; it lives in CacheDirectory, which the OS
                // reclaims, so we don't delete it here after the (synchronous) consumer reads it.
                var safeName = $"{Guid.NewGuid():N}_{Path.GetFileName(result.FileName)}";
                var cacheFile = Path.Combine(FileSystem.CacheDirectory, safeName);
                _migrationService.Log($"[FilePicker] Cache target: {safeName}");

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
                    // If the copy was interrupted partway through, remove the half-written file so
                    // nothing treats it as a valid backup. With the GUID-prefixed name each pick is
                    // unique, so no later run would ever overwrite this partial — explicit cleanup
                    // is the only thing that keeps the cache from accumulating dead fragments.
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
            // End Modification

            return result.FullPath;
        }
        catch (Exception ex)
        {
            _migrationService.Log($"[FilePicker] EXCEPTION: {ex}");
            throw;
        }
    }
}
