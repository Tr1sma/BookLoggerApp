using Android.Webkit;

namespace BookLoggerApp.Platforms.Android;

/// <summary>
/// Custom WebChromeClient that grants camera and microphone permissions to the WebView.
/// This is necessary because MAUI Blazor Hybrid WebViews don't automatically inherit
/// native app permissions for getUserMedia() JavaScript calls.
/// </summary>
public class CustomWebChromeClient : WebChromeClient
{
    public override void OnPermissionRequest(PermissionRequest? request)
    {
        if (request == null) return;

        // Grant all requested permissions (camera, microphone, etc.)
        // The native MAUI permission service has already verified the user granted permission
        request.Grant(request.GetResources());

        System.Diagnostics.Debug.WriteLine($"WebChromeClient: Granted permissions: {string.Join(", ", request.GetResources() ?? Array.Empty<string>())}");
    }
}
