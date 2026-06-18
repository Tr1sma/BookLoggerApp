using Android.Webkit;

namespace BookLoggerApp.Platforms.Android;

// Hybrid WebViews don't inherit native permissions for getUserMedia()
public class CustomWebChromeClient : WebChromeClient
{
    public override void OnPermissionRequest(PermissionRequest? request)
    {
        if (request == null) return;

        request.Grant(request.GetResources());

        System.Diagnostics.Debug.WriteLine($"WebChromeClient: Granted permissions: {string.Join(", ", request.GetResources() ?? Array.Empty<string>())}");
    }
}
