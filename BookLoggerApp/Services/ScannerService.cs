namespace BookLoggerApp.Services;

public class ScannerService : IScannerService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPermissionService _permissionService;

    public ScannerService(IServiceProvider serviceProvider, IPermissionService permissionService)
    {
        _serviceProvider = serviceProvider;
        _permissionService = permissionService;
    }

    public async Task<string?> ScanBarcodeAsync()
    {
        // Check camera permission each time (user may have granted "only this time")
        bool hasPermission = await _permissionService.RequestCameraPermissionAsync();
        if (!hasPermission)
        {
            return null;
        }

        var tcs = new TaskCompletionSource<string?>();

        try
        {
            var scannerPage = new ScannerPage();
            scannerPage.AssignTaskCompletionSource(tcs);
            
            // Get the current navigation context
            // In MAUI Blazor, we can usually access MainPge via Application.Current
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.Navigation.PushModalAsync(scannerPage);
            }
            else
            {
                return null;
            }

            // Wait for the result
            return await tcs.Task;
        }
        catch (Exception ex)
        {
            // Log error or handle gracefully
            System.Diagnostics.Debug.WriteLine($"Scanner error: {ex.Message}");
            return null;
        }
    }
}
