using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Entitlements;
using BookLoggerApp.Core.Infrastructure;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IImportExportService _importExportService;
    private readonly IAppSettingsProvider _settingsProvider;
    private readonly IFileSaverService _fileSaverService;
    private readonly IShareService _shareService;
    private readonly IFilePickerService _filePickerService;
    private readonly IMigrationService _migrationService;
    private readonly INotificationService _notificationService;
    private readonly IReadingTimerNotificationService _timerNotification;
    private readonly IAppVersionService _appVersionService;
    private readonly ILanguageService _languageService;
    private readonly IAnalyticsService? _analytics;
    private readonly IFeatureGuard? _featureGuard;


    public SettingsViewModel(
        IImportExportService importExportService,
        IAppSettingsProvider settingsProvider,
        IFileSaverService fileSaverService,
        IShareService shareService,
        IFilePickerService filePickerService,
        IMigrationService migrationService,
        INotificationService notificationService,
        IReadingTimerNotificationService timerNotification,
        IAppVersionService appVersionService,
        ILanguageService languageService,
        IAnalyticsService? analytics = null,
        IFeatureGuard? featureGuard = null)
    {
        _importExportService = importExportService;
        _settingsProvider = settingsProvider;
        _fileSaverService = fileSaverService;
        _shareService = shareService;
        _filePickerService = filePickerService;
        _migrationService = migrationService;
        _notificationService = notificationService;
        _timerNotification = timerNotification;
        _appVersionService = appVersionService;
        _languageService = languageService;
        _analytics = analytics;
        _featureGuard = featureGuard;

        MigrationLog = _migrationService.GetMigrationLog();
        AppVersion = _appVersionService.CurrentVersion;
        SelectedLanguage = _languageService.CurrentLanguage;
        SupportedLanguages = _languageService.SupportedLanguages;
    }

    /// <summary>Re-reads the migration log from MigrationService for the latest contents while DB init is still running.</summary>
    public void RefreshMigrationLog()
    {
        MigrationLog = _migrationService.GetMigrationLog();
    }

    [ObservableProperty]
    private string _selectedLanguage = "en";

    [ObservableProperty]
    private IReadOnlyList<SupportedLanguage> _supportedLanguages = Array.Empty<SupportedLanguage>();

    [ObservableProperty]
    private bool _languageChangedPendingRestart;

    [ObservableProperty]
    private AppSettings _settings = new();

    [ObservableProperty]
    private string _appVersion = "1.0.0";

    [ObservableProperty]
    private string _migrationLog;

    [ObservableProperty]
    private int _reminderHour = 20;

    [ObservableProperty]
    private int _reminderMinute = 0;

    [ObservableProperty]
    private string _shelfLedgeColor = "#8B7355";

    [ObservableProperty]
    private string _shelfBaseColor = "#D4A574";

    [ObservableProperty]
    private bool _backupRestoreSucceeded;

    [RelayCommand]
    public async Task ToggleNotificationsAsync(bool enabled)
    {
        await ExecuteSafelyAsync(async () =>
        {
            if (enabled)
            {
                // OS notification permission required on Android 13+.
                bool granted = await _notificationService.RequestNotificationPermissionAsync();
                if (!granted)
                {
                    Settings.NotificationsEnabled = false;
                    OnPropertyChanged(nameof(Settings));
                    SetError(Tr("Error_NotificationPermissionDenied"));
                    return;
                }
            }

            Settings.NotificationsEnabled = enabled;
            if (!enabled)
            {
                Settings.ReadingRemindersEnabled = false;
                await _notificationService.CancelReadingReminderAsync();
                // Master switch off also removes any active live-timer notification.
                _timerNotification.Hide();
            }
            await SaveSettingsInternalAsync();
        }, Tr("Error_FailedTo_UpdateNotificationSettings"));
    }

    [RelayCommand]
    public async Task ToggleLiveTimerNotificationAsync(bool enabled)
    {
        await ExecuteSafelyAsync(async () =>
        {
            if (enabled)
            {
                // Re-verify OS permission; it may have been revoked since the master toggle was enabled.
                bool granted = await _notificationService.RequestNotificationPermissionAsync();
                if (!granted)
                {
                    Settings.LiveTimerNotificationEnabled = false;
                    OnPropertyChanged(nameof(Settings));
                    SetError(Tr("Error_NotificationPermissionDenied"));
                    await SaveSettingsInternalAsync();
                    return;
                }
            }

            Settings.LiveTimerNotificationEnabled = enabled;
            if (!enabled)
            {
                _timerNotification.Hide();
            }
            await SaveSettingsInternalAsync();
        }, Tr("Error_FailedTo_UpdateNotificationSettings"));
    }

    [RelayCommand]
    public async Task ToggleMoodTrackingAsync(bool enabled)
    {
        await ExecuteSafelyAsync(async () =>
        {
            Settings.MoodTrackingEnabled = enabled;
            await SaveSettingsInternalAsync();
        }, Tr("Error_FailedTo_UpdateMoodTrackingSettings"));
    }

    [RelayCommand]
    public async Task ToggleReadingRemindersAsync(bool enabled)
    {
        await ExecuteSafelyAsync(async () =>
        {
            if (enabled)
            {
                // Re-verify OS permission before scheduling; it may have been revoked since the toggle.
                bool granted = await _notificationService.RequestNotificationPermissionAsync();
                if (!granted)
                {
                    Settings.NotificationsEnabled = false;
                    Settings.ReadingRemindersEnabled = false;
                    OnPropertyChanged(nameof(Settings));
                    SetError(Tr("Error_NotificationPermissionDenied"));
                    await SaveSettingsInternalAsync();
                    return;
                }
            }

            Settings.ReadingRemindersEnabled = enabled;
            if (enabled)
            {
                var time = new TimeSpan(ReminderHour, ReminderMinute, 0);
                Settings.ReminderTime = time;
                await _notificationService.ScheduleReadingReminderAsync(time);
            }
            else
            {
                await _notificationService.CancelReadingReminderAsync();
            }
            await SaveSettingsInternalAsync();
        }, Tr("Error_FailedTo_UpdateReminderSettings"));
    }

    [RelayCommand]
    public async Task UpdateReminderTimeAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            var time = new TimeSpan(ReminderHour, ReminderMinute, 0);
            Settings.ReminderTime = time;
            if (Settings.ReadingRemindersEnabled)
            {
                await _notificationService.ScheduleReadingReminderAsync(time);
            }
            await SaveSettingsInternalAsync();
        }, Tr("Error_FailedTo_UpdateReminderTime"));
    }

    [RelayCommand]
    public async Task ToggleGettingStartedCtaAsync(bool hide)
    {
        await ExecuteSafelyAsync(async () =>
        {
            Settings.HideGettingStartedCta = hide;
            await SaveSettingsInternalAsync();
        }, Tr("Error_FailedTo_UpdateGettingStartedVisibility"));
    }

    [RelayCommand]
    public async Task UpdateShelfLedgeColorAsync(string color)
    {
        await ExecuteSafelyAsync(async () =>
        {
            // Custom shelf colors are Plus-only; enforce here so no non-overlay path persists one for free.
            _featureGuard?.RequireAccess(FeatureKey.CustomShelfColors, "Custom shelf colors require Plus.");
            ShelfLedgeColor = color;
            Settings.ShelfLedgeColor = color;
            await SaveSettingsInternalAsync();
        }, Tr("Error_FailedTo_UpdateShelfLedgeColor"));
    }

    [RelayCommand]
    public async Task UpdateShelfBaseColorAsync(string color)
    {
        await ExecuteSafelyAsync(async () =>
        {
            _featureGuard?.RequireAccess(FeatureKey.CustomShelfColors, "Custom shelf colors require Plus.");
            ShelfBaseColor = color;
            Settings.ShelfBaseColor = color;
            await SaveSettingsInternalAsync();
        }, Tr("Error_FailedTo_UpdateShelfBaseColor"));
    }

    private async Task SaveSettingsInternalAsync()
    {
        Settings.UpdatedAt = DateTime.UtcNow;
        await _settingsProvider.UpdateSettingsAsync(Settings);
    }

    [RelayCommand]
    public async Task ToggleAnalyticsAsync(bool enabled)
    {
        await ExecuteSafelyAsync(async () =>
        {
            Settings.AnalyticsEnabled = enabled;
            OnPropertyChanged(nameof(Settings));
            await SaveSettingsInternalAsync();
            LogSettingChanged("analytics");
        }, Tr("Error_FailedTo_UpdateAnalyticsSettings"));
    }

    [RelayCommand]
    public async Task ToggleCrashReportingAsync(bool enabled)
    {
        await ExecuteSafelyAsync(async () =>
        {
            Settings.CrashReportingEnabled = enabled;
            OnPropertyChanged(nameof(Settings));
            await SaveSettingsInternalAsync();
            LogSettingChanged("crash_reporting");
        }, Tr("Error_FailedTo_UpdateCrashReportingSettings"));
    }

    private void LogSettingChanged(string settingKey)
    {
        try
        {
            _analytics?.LogEvent(
                BookLoggerApp.Core.Services.Analytics.AnalyticsEventNames.AppSettingsChanged,
                BookLoggerApp.Core.Services.Analytics.AnalyticsParamBuilder.Create()
                    .Add(BookLoggerApp.Core.Services.Analytics.AnalyticsParamNames.SettingKey, settingKey)
                    .BuildMutable());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LogSettingChanged failed: {ex}");
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            Settings = await _settingsProvider.GetSettingsAsync();
            MigrationLog = _migrationService.GetMigrationLog();
            AppVersion = _appVersionService.CurrentVersion;

            ShelfLedgeColor = Settings.ShelfLedgeColor;
            ShelfBaseColor = Settings.ShelfBaseColor;
            SelectedLanguage = _languageService.CurrentLanguage;
            SupportedLanguages = _languageService.SupportedLanguages;

            if (Settings.ReminderTime.HasValue)
            {
                ReminderHour = Settings.ReminderTime.Value.Hours;
                ReminderMinute = Settings.ReminderTime.Value.Minutes;
            }
        }, Tr("Error_FailedTo_LoadSettings"));
    }

    [RelayCommand]
    public async Task ChangeLanguageAsync(string code)
    {
        await ExecuteSafelyAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(code) || string.Equals(code, _languageService.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await _languageService.SetLanguageAsync(code);
            SelectedLanguage = _languageService.CurrentLanguage;
            LanguageChangedPendingRestart = true;
            LogSettingChanged("language");
        }, Tr("Error_FailedTo_ChangeLanguage"));
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            Settings.UpdatedAt = DateTime.UtcNow;
            await _settingsProvider.UpdateSettingsAsync(Settings);
        }, Tr("Error_FailedTo_SaveSettings"));
    }

    [RelayCommand]
    public async Task ExportDataAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            var json = await _importExportService.ExportToJsonAsync();
            var fileName = $"BookLoggerExport_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            await _fileSaverService.SaveFileAsync(fileName, json, Tr("Common_ExportData"));
        }, Tr("Error_FailedTo_ExportData"));
    }

    [RelayCommand]
    public async Task ImportDataAsync(string json)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _importExportService.ImportFromJsonAsync(json);
        }, Tr("Error_FailedTo_ImportData"));
    }

    [RelayCommand]
    public async Task DeleteAllDataAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _importExportService.DeleteAllDataAsync();
        }, Tr("Error_FailedTo_DeleteData"));
    }

    [RelayCommand]
    public async Task BackupToCloudAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            var backupPath = await _importExportService.CreateBackupAsync();
            await _shareService.ShareFileAsync("BookLogger Backup", backupPath, "application/zip");
        }, Tr("Error_FailedTo_BackupData"));
    }

    [RelayCommand]
    public async Task RestoreFromBackupAsync()
    {
        BackupRestoreSucceeded = false;
        IsBusy = true;
        ClearError();

        try
        {
            AppendLog("=== Restore from Backup started ===");

            // Wait for the fire-and-forget DbInitializer to release its DbContext before touching the DB file; otherwise File.Copy corrupts the open connection ("database disk image malformed").
            AppendLog("Waiting for database initialization to complete...");
            try
            {
                await DatabaseInitializationHelper.EnsureInitializedAsync();
                AppendLog("Database initialization confirmed complete.");
            }
            catch (Exception initEx)
            {
                AppendLog($"DB initialization failed earlier: {initEx.GetType().Name}: {initEx.Message}");
                SetError(Tr("Error_RestoreDbInitFailed", initEx.GetType().Name, initEx.Message));
                return;
            }

            var filePath = await _filePickerService.PickFileAsync(Tr("Settings_Restore_PickerTitle", "Select Backup File"), ".zip");
            AppendLog($"Picker returned: {filePath ?? "NULL"}");

            if (string.IsNullOrEmpty(filePath))
            {
                AppendLog("Restore cancelled (no file selected).");
                SetError(Tr("Error_FilePickCancelled"));
                return;
            }

            AppendLog($"Calling ImportExportService.RestoreFromBackupAsync({filePath})");
            var progress = new Progress<string>(msg => AppendLog($"  [ImportExport] {msg}"));
            await _importExportService.RestoreFromBackupAsync(filePath, progress);
            AppendLog("ImportExportService returned successfully.");

            BackupRestoreSucceeded = true;
            AppendLog("Restore complete; awaiting app restart.");
        }
        catch (Exception ex)
        {
            AppendLog($"EXCEPTION: {ex.GetType().FullName}");
            AppendLog($"  Message: {ex.Message}");
            if (ex.InnerException is not null)
            {
                AppendLog($"  Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
            }
            AppendLog($"  Stack: {ex.StackTrace}");

            var detail = ex.InnerException is not null
                ? $" ({ex.InnerException.Message})"
                : string.Empty;
            SetError(Tr("Error_RestoreBackupFailed", ex.GetType().Name, ex.Message, detail));
            System.Diagnostics.Debug.WriteLine($"ERROR: {ex}");
        }
        finally
        {
            IsBusy = false;
        }
    }
    private void AppendLog(string message)
    {
        _migrationService.Log(message);
        MigrationLog = _migrationService.GetMigrationLog();
    }
}

