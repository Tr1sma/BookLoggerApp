using ZXing.Net.Maui;
using BookLoggerApp.Core.Helpers;

namespace BookLoggerApp;

public partial class ScannerPage : ContentPage
{
    private TaskCompletionSource<string?>? _tcs;
    private bool _isProcessing = false;

    public ScannerPage()
    {
        InitializeComponent();
        
        cameraBarcodeReaderView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.OneDimensional, 
            AutoRotate = true,
            Multiple = false,
            TryHarder = true
        };
    }

    public void AssignTaskCompletionSource(TaskCompletionSource<string?> tcs)
    {
        _tcs = tcs;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        cameraBarcodeReaderView.IsDetecting = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        cameraBarcodeReaderView.IsDetecting = false;
        ScannerTaskCompletionHelper.TrySetCancelledResult(_tcs);
    }

    private void CameraBarcodeReaderView_BarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (_isProcessing) return;
        
        if (e.Results != null && e.Results.Any())
        {
            var result = e.Results.FirstOrDefault();
            var code = result?.Value;
            
            if (!string.IsNullOrEmpty(code))
            {
                _isProcessing = true;
                Dispatcher.Dispatch(async () =>
                {
                    cameraBarcodeReaderView.IsDetecting = false;
                    // Publish the scanned code BEFORE popping the modal. PopModalAsync triggers
                    // OnDisappearing, which calls TrySetCancelledResult(null); since the TCS is
                    // first-writer-wins, completing it here first makes that cancel a no-op.
                    // Doing it the other way round loses the scan (null returned, ISBN never filled).
                    _tcs?.TrySetResult(code);
                    await Navigation.PopModalAsync();
                });
            }
        }
    }

    private async void CancelButton_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
        ScannerTaskCompletionHelper.TrySetCancelledResult(_tcs);
    }

    private void FlashlightButton_Clicked(object sender, EventArgs e)
    {
        cameraBarcodeReaderView.IsTorchOn = !cameraBarcodeReaderView.IsTorchOn;
    }
}
