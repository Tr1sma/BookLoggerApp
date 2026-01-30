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

            // Start Modification: Handle virtual files (e.g. Google Drive) where FullPath is null
            // Check if path is usable (local file)
            // If FullPath is null, OR it's a content URI, OR it doesn't exist on disk -> treat as virtual
            bool isVirtual = string.IsNullOrEmpty(result.FullPath) 
                             || result.FullPath.StartsWith("content://") 
                             || !File.Exists(result.FullPath);
            
            _migrationService.Log($"[FilePicker] IsVirtual: {isVirtual} (Path: {result.FullPath})");

            if (isVirtual)
            {
                _migrationService.Log("[FilePicker] Handling virtual file...");
                // Copy to temp file in cache directory
                var cacheFile = Path.Combine(FileSystem.CacheDirectory, result.FileName);
                _migrationService.Log($"[FilePicker] Cache target: {cacheFile}");
            
                // Delete if exists to ensure fresh copy
                if (File.Exists(cacheFile)) 
                    File.Delete(cacheFile);

                using var stream = await result.OpenReadAsync();
                using var fileStream = File.Create(cacheFile);
                await stream.CopyToAsync(fileStream);
                
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
