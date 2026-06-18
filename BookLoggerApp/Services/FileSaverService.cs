using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Services;

public class FileSaverService : IFileSaverService
{
    public async Task<string?> SaveFileAsync(string defaultFileName, string content, CancellationToken ct = default)
    {
        try
        {
            var tempPath = Path.Combine(FileSystem.CacheDirectory, defaultFileName);
            await File.WriteAllTextAsync(tempPath, content, ct);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export Data",
                File = new ShareFile(tempPath)
            });

            return tempPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FileSaverService error: {ex.Message}");
            return null;
        }
    }
}
