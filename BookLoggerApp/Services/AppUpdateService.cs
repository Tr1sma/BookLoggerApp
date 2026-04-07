using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

#if ANDROID
using Android.App;
using Microsoft.Maui.ApplicationModel;
using Xamarin.Google.Android.Play.Core.AppUpdate;
using Xamarin.Google.Android.Play.Core.AppUpdate.Install.Model;
#endif

namespace BookLoggerApp.Services;

public sealed class AppUpdateService : IAppUpdateService, IDisposable
{
#if ANDROID
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(4);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IAppUpdateManager? _appUpdateManager;
    private CancellationTokenSource? _pollingCts;
    private AppUpdateState _lastState = AppUpdateState.Unsupported;
#endif

    public event EventHandler<AppUpdateState>? StateChanged;

    public async Task<AppUpdateState> GetStateAsync(CancellationToken ct = default)
    {
#if ANDROID
        await _gate.WaitAsync(ct);

        try
        {
            if (!TryEnsureManager())
            {
                _lastState = AppUpdateState.Unsupported;
                return _lastState;
            }

            var info = await GetAppUpdateInfoAsync(ct);
            _lastState = MapState(info);

            if (_lastState.IsUpdateInProgress && !_lastState.IsUpdateDownloaded)
            {
                EnsurePolling();
            }
            else if (!_lastState.IsUpdateInProgress || _lastState.IsUpdateDownloaded)
            {
                StopPolling();
            }

            return _lastState;
        }
        finally
        {
            _gate.Release();
        }
#else
        await Task.CompletedTask;
        return AppUpdateState.Unsupported;
#endif
    }

    public async Task<bool> StartFlexibleUpdateAsync(CancellationToken ct = default)
    {
#if ANDROID
        await _gate.WaitAsync(ct);

        try
        {
            if (!TryEnsureManager())
            {
                return false;
            }

            var info = await GetAppUpdateInfoAsync(ct);
            var state = MapState(info);
            _lastState = state;

            if (!state.CanStartFlexibleUpdate)
            {
                return false;
            }

            var activity = Platform.CurrentActivity;
            if (activity == null)
            {
                return false;
            }

            var options = AppUpdateOptions.NewBuilder(AppUpdateType.Flexible).Build();
            var startTask = _appUpdateManager!.StartUpdateFlow(info, activity, options);
            var started = startTask != null;

            if (started)
            {
                EnsurePolling();
                await RaiseStateChangedAsync(await GetAppUpdateInfoAsync(ct));
            }

            return started;
        }
        finally
        {
            _gate.Release();
        }
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    public async Task<bool> CompleteUpdateAsync(CancellationToken ct = default)
    {
#if ANDROID
        await _gate.WaitAsync(ct);

        try
        {
            if (!TryEnsureManager())
            {
                return false;
            }

            _appUpdateManager!.CompleteUpdate();
            StopPolling();
            return true;
        }
        finally
        {
            _gate.Release();
        }
#else
        await Task.CompletedTask;
        return false;
#endif
    }

    public void Dispose()
    {
#if ANDROID
        StopPolling();
        _pollingCts?.Dispose();
        _gate.Dispose();
#endif
    }

#if ANDROID
    private bool TryEnsureManager()
    {
        if (_appUpdateManager != null)
        {
            return true;
        }

        var activity = Platform.CurrentActivity;
        if (activity == null)
        {
            return false;
        }

        _appUpdateManager = AppUpdateManagerFactory.Create(activity);
        return true;
    }

    private async Task<AppUpdateInfo> GetAppUpdateInfoAsync(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<AppUpdateInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = ct.Register(() => tcs.TrySetCanceled(ct));

        var task = _appUpdateManager!.GetAppUpdateInfo();
        task.AddOnSuccessListener(new AppUpdateSuccessListener(result =>
        {
            if (result is AppUpdateInfo info)
            {
                tcs.TrySetResult(info);
                return;
            }

            tcs.TrySetException(new InvalidOperationException("Play Store returned no app update info."));
        }));
        task.AddOnFailureListener(new AppUpdateFailureListener(ex =>
        {
            tcs.TrySetException(new InvalidOperationException(
                $"Failed to query app update state: {ex?.Message ?? "Unknown Play Store error"}",
                ex));
        }));

        return await tcs.Task;
    }

    private AppUpdateState MapState(AppUpdateInfo info)
    {
        var availability = info.UpdateAvailability();
        var installStatus = info.InstallStatus();
        var isUpdateAvailable = availability == UpdateAvailability.UpdateAvailable
            || availability == UpdateAvailability.DeveloperTriggeredUpdateInProgress;
        var isUpdateDownloaded = installStatus == InstallStatus.Downloaded;
        var isUpdateInProgress = installStatus == InstallStatus.Downloading
            || installStatus == InstallStatus.Pending
            || installStatus == InstallStatus.Installing
            || isUpdateDownloaded;

        return new AppUpdateState
        {
            IsSupported = true,
            IsUpdateAvailable = isUpdateAvailable,
            IsUpdateDownloaded = isUpdateDownloaded,
            IsUpdateInProgress = isUpdateInProgress,
            CanStartFlexibleUpdate = isUpdateAvailable && info.IsUpdateTypeAllowed(AppUpdateType.Flexible)
        };
    }

    private void EnsurePolling()
    {
        if (_pollingCts != null && !_pollingCts.IsCancellationRequested)
        {
            return;
        }

        _pollingCts = new CancellationTokenSource();
        _ = PollUntilStableAsync(_pollingCts.Token);
    }

    private void StopPolling()
    {
        if (_pollingCts == null)
        {
            return;
        }

        _pollingCts.Cancel();
        _pollingCts.Dispose();
        _pollingCts = null;
    }

    private async Task PollUntilStableAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(PollInterval, ct);

                var state = await GetStateAsync(ct);
                StateChanged?.Invoke(this, state);

                if (state.IsUpdateDownloaded || !state.IsUpdateInProgress)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!ct.IsCancellationRequested)
            {
                StopPolling();
            }
        }
    }

    private async Task RaiseStateChangedAsync(AppUpdateInfo info)
    {
        _lastState = MapState(info);
        StateChanged?.Invoke(this, _lastState);
        await Task.CompletedTask;
    }

    private sealed class AppUpdateSuccessListener : Java.Lang.Object, Android.Gms.Tasks.IOnSuccessListener
    {
        private readonly Action<Java.Lang.Object?> _onSuccess;

        public AppUpdateSuccessListener(Action<Java.Lang.Object?> onSuccess)
        {
            _onSuccess = onSuccess;
        }

        public void OnSuccess(Java.Lang.Object? result)
        {
            _onSuccess(result);
        }
    }

    private sealed class AppUpdateFailureListener : Java.Lang.Object, Android.Gms.Tasks.IOnFailureListener
    {
        private readonly Action<Exception?> _onFailure;

        public AppUpdateFailureListener(Action<Exception?> onFailure)
        {
            _onFailure = onFailure;
        }

        public void OnFailure(Java.Lang.Exception? ex)
        {
            _onFailure(ex);
        }
    }
#endif
}
