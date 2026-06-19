using Android.Content.PM;
using Android.Webkit;

namespace BookLoggerApp.Platforms.Android;

/// <summary>
/// Custom WebChromeClient that grants the WebView the camera permission required by the
/// in-app barcode scanner (getUserMedia video capture). It grants ONLY video capture, and
/// ONLY while the native CAMERA runtime permission is actually held. Every other resource
/// (microphone, protected media id, …) is denied so embedded or future remote web content
/// cannot silently obtain device sensors. See code review SEC-18.
/// </summary>
public class CustomWebChromeClient : WebChromeClient
{
    public override void OnPermissionRequest(PermissionRequest? request)
    {
        if (request == null) return;

        string[] requested = request.GetResources() ?? Array.Empty<string>();

        bool cameraGranted =
            global::Android.App.Application.Context.CheckSelfPermission(
                global::Android.Manifest.Permission.Camera) == Permission.Granted;

        // Grant nothing but VIDEO_CAPTURE, and only when the OS-level camera permission is held.
        string[] toGrant = cameraGranted
            ? Array.FindAll(requested, static r => r == PermissionRequest.ResourceVideoCapture)
            : Array.Empty<string>();

        if (toGrant.Length > 0)
        {
            request.Grant(toGrant);
            System.Diagnostics.Debug.WriteLine(
                $"WebChromeClient: granted {string.Join(", ", toGrant)}");
        }
        else
        {
            request.Deny();
            System.Diagnostics.Debug.WriteLine(
                $"WebChromeClient: denied {string.Join(", ", requested)} (cameraGranted={cameraGranted})");
        }
    }
}
