using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Services;

public class ShareService : IShareService
{
    public async Task ShareFileAsync(string title, string filePath, string contentType = "application/octet-stream")
    {
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = title,
            File = new ShareFile(filePath, contentType)
        });
    }
}
