using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Services;

public class FilePickerService : IFilePickerService
{
    public async Task<string?> PickFileAsync(string pickerTitle, params string[] allowedExtensions)
    {
        var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.iOS, new[] { "public.content" } }, // Fallback to content or specific UTType if known
                    { DevicePlatform.Android, new[] { "*/*" } }, // MIME types are hard to map generically without a larger map. Using all files for now to be safe.
                    { DevicePlatform.WinUI, allowedExtensions },
                    { DevicePlatform.Tizen, new[] { "*/*" } },
                    { DevicePlatform.macOS, allowedExtensions }
                });

        var options = new PickOptions
        {
            PickerTitle = pickerTitle,
            FileTypes = customFileType
        };

        var result = await FilePicker.Default.PickAsync(options);
        return result?.FullPath;
    }
}
