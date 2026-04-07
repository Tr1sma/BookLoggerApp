namespace BookLoggerApp.Core.Helpers;

public static class ScannerTaskCompletionHelper
{
    public static void TrySetCancelledResult(TaskCompletionSource<string?>? taskCompletionSource)
    {
        if (taskCompletionSource == null)
        {
            return;
        }

        if (taskCompletionSource.Task.IsCompleted)
        {
            return;
        }

        taskCompletionSource.TrySetResult(null);
    }
}
