namespace BookLoggerApp.Services;

/// <summary>
/// Interface for requesting native device permissions.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Request camera permission from the user.
    /// </summary>
    /// <returns>True if permission was granted, false otherwise.</returns>
    Task<bool> RequestCameraPermissionAsync();
}

/// <summary>
/// MAUI implementation for requesting native device permissions.
/// </summary>
public class PermissionService : IPermissionService
{
    /// <inheritdoc />
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
