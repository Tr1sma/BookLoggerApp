namespace BookLoggerApp.Services;

public interface IPermissionService
{
    Task<bool> RequestCameraPermissionAsync();
}

public class PermissionService : IPermissionService
{
    public async Task<bool> RequestCameraPermissionAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();

            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            return status == PermissionStatus.Granted;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error requesting camera permission: {ex.Message}");
            return false;
        }
    }
}
