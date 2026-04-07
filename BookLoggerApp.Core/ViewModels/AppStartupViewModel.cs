using CommunityToolkit.Mvvm.ComponentModel;
using BookLoggerApp.Core.Helpers;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class AppStartupViewModel : ViewModelBase, IDisposable
{
    private readonly IAppVersionService _appVersionService;
    private readonly IChangelogService _changelogService;
    private readonly IAppUpdateService _appUpdateService;
    private bool _initialized;
    private bool _dismissedUpdateAvailableThisSession;
    private bool _dismissedDownloadedUpdateThisSession;
    private bool _pendingUpdateAvailablePrompt;
    private bool _pendingDownloadedUpdatePrompt;

    public AppStartupViewModel(
        IAppVersionService appVersionService,
        IChangelogService changelogService,
        IAppUpdateService appUpdateService)
    {
        _appVersionService = appVersionService;
        _changelogService = changelogService;
        _appUpdateService = appUpdateService;
        _appUpdateService.StateChanged += OnAppUpdateStateChanged;
    }

    [ObservableProperty]
    private bool _isChangelogVisible;

    [ObservableProperty]
    private bool _isUpdateAvailableVisible;

    [ObservableProperty]
    private bool _isUpdateReadyVisible;

    [ObservableProperty]
    private bool _showFullHistory;

    [ObservableProperty]
    private bool _isStartingUpdate;

    [ObservableProperty]
    private string _currentVersion = string.Empty;

    [ObservableProperty]
    private ChangelogRelease? _currentRelease;

    [ObservableProperty]
    private IReadOnlyList<ChangelogRelease> _releaseHistory = Array.Empty<ChangelogRelease>();

    [ObservableProperty]
    private AppUpdateState _updateState = AppUpdateState.Unsupported;

    public bool HasVisibleOverlay => IsChangelogVisible || IsUpdateAvailableVisible || IsUpdateReadyVisible;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized)
        {
            return;
        }

        await ExecuteSafelyAsync(async () =>
        {
            _appVersionService.TrackCurrentVersion();
            CurrentVersion = _appVersionService.CurrentVersion;

            if (_appVersionService.IsFirstLaunchForCurrentVersion)
            {
                await LoadChangelogAsync(ct);
            }

            await RefreshUpdateStateAsync(ct);
            _initialized = true;
        }, "Failed to initialize startup experience");
    }

    public async Task HandleAppResumedAsync(CancellationToken ct = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(ct);
            return;
        }

        await ExecuteSafelyAsync(
            async () => await RefreshUpdateStateAsync(ct),
            "Failed to refresh app update state");
    }

    public Task ToggleHistoryAsync()
    {
        ShowFullHistory = !ShowFullHistory;
        return Task.CompletedTask;
    }

    public async Task CloseChangelogAsync()
    {
        IsChangelogVisible = false;
        ShowNextQueuedUpdatePrompt();
        await Task.CompletedTask;
    }

    public async Task DismissUpdateAvailableAsync()
    {
        _dismissedUpdateAvailableThisSession = true;
        _pendingUpdateAvailablePrompt = false;
        IsUpdateAvailableVisible = false;
        await Task.CompletedTask;
    }

    public async Task DismissUpdateReadyAsync()
    {
        _dismissedDownloadedUpdateThisSession = true;
        _pendingDownloadedUpdatePrompt = false;
        IsUpdateReadyVisible = false;
        await Task.CompletedTask;
    }

    public async Task StartFlexibleUpdateAsync(CancellationToken ct = default)
    {
        if (IsStartingUpdate)
        {
            return;
        }

        await ExecuteSafelyAsync(async () =>
        {
            IsStartingUpdate = true;

            if (await _appUpdateService.StartFlexibleUpdateAsync(ct))
            {
                IsUpdateAvailableVisible = false;
                _pendingUpdateAvailablePrompt = false;
                _dismissedUpdateAvailableThisSession = true;
            }

            await RefreshUpdateStateAsync(ct);
        }, "Failed to start app update");

        IsStartingUpdate = false;
    }

    public async Task CompleteUpdateAsync(CancellationToken ct = default)
    {
        await ExecuteSafelyAsync(async () =>
        {
            if (await _appUpdateService.CompleteUpdateAsync(ct))
            {
                IsUpdateReadyVisible = false;
                _pendingDownloadedUpdatePrompt = false;
            }
        }, "Failed to install app update");
    }

    public async Task<bool> HandleBackAsync()
    {
        if (IsUpdateReadyVisible)
        {
            await DismissUpdateReadyAsync();
            return true;
        }

        if (IsUpdateAvailableVisible)
        {
            await DismissUpdateAvailableAsync();
            return true;
        }

        if (IsChangelogVisible)
        {
            await CloseChangelogAsync();
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        _appUpdateService.StateChanged -= OnAppUpdateStateChanged;
    }

    private void OnAppUpdateStateChanged(object? sender, AppUpdateState state)
    {
        UpdateState = state;
        ApplyUpdatePromptState(state);
    }

    private async Task LoadChangelogAsync(CancellationToken ct)
    {
        ReleaseHistory = await _changelogService.GetReleaseHistoryAsync(ct);
        CurrentRelease = ReleaseHistory.FirstOrDefault(release =>
            string.Equals(release.Version, ChangelogParser.NormalizeVersion(CurrentVersion), StringComparison.OrdinalIgnoreCase));

        if (CurrentRelease != null)
        {
            IsChangelogVisible = true;
        }
    }

    private async Task RefreshUpdateStateAsync(CancellationToken ct)
    {
        UpdateState = await _appUpdateService.GetStateAsync(ct);
        ApplyUpdatePromptState(UpdateState);
    }

    private void ApplyUpdatePromptState(AppUpdateState state)
    {
        if (!state.IsSupported)
        {
            IsUpdateAvailableVisible = false;
            IsUpdateReadyVisible = false;
            _pendingUpdateAvailablePrompt = false;
            _pendingDownloadedUpdatePrompt = false;
            return;
        }

        if (state.IsUpdateDownloaded)
        {
            _pendingDownloadedUpdatePrompt = !_dismissedDownloadedUpdateThisSession;
            _pendingUpdateAvailablePrompt = false;

            if (IsChangelogVisible)
            {
                IsUpdateAvailableVisible = false;
                IsUpdateReadyVisible = false;
                OnPropertyChanged(nameof(HasVisibleOverlay));
                return;
            }

            IsUpdateAvailableVisible = false;
            IsUpdateReadyVisible = _pendingDownloadedUpdatePrompt;
            OnPropertyChanged(nameof(HasVisibleOverlay));
            return;
        }

        IsUpdateReadyVisible = false;
        _pendingDownloadedUpdatePrompt = false;

        if (state.IsUpdateInProgress)
        {
            IsUpdateAvailableVisible = false;
            IsUpdateReadyVisible = false;
            _pendingUpdateAvailablePrompt = false;
            _pendingDownloadedUpdatePrompt = false;
            OnPropertyChanged(nameof(HasVisibleOverlay));
            return;
        }

        if (state.IsUpdateAvailable && !_dismissedUpdateAvailableThisSession)
        {
            _pendingUpdateAvailablePrompt = true;

            if (IsChangelogVisible)
            {
                IsUpdateAvailableVisible = false;
                OnPropertyChanged(nameof(HasVisibleOverlay));
                return;
            }

            IsUpdateAvailableVisible = true;
            OnPropertyChanged(nameof(HasVisibleOverlay));
            return;
        }

        _pendingUpdateAvailablePrompt = false;
        IsUpdateAvailableVisible = false;
        OnPropertyChanged(nameof(HasVisibleOverlay));
    }

    private void ShowNextQueuedUpdatePrompt()
    {
        if (_pendingDownloadedUpdatePrompt && !_dismissedDownloadedUpdateThisSession)
        {
            IsUpdateReadyVisible = true;
            IsUpdateAvailableVisible = false;
        }
        else if (_pendingUpdateAvailablePrompt && !_dismissedUpdateAvailableThisSession)
        {
            IsUpdateAvailableVisible = true;
            IsUpdateReadyVisible = false;
        }

        OnPropertyChanged(nameof(HasVisibleOverlay));
    }

    partial void OnIsChangelogVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(HasVisibleOverlay));
    }

    partial void OnIsUpdateAvailableVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(HasVisibleOverlay));
    }

    partial void OnIsUpdateReadyVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(HasVisibleOverlay));
    }
}
