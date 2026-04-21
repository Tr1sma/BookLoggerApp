using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private readonly IAppVersionService _appVersionService;


    public SettingsViewModel(
        IImportExportService importExportService,
        IAppSettingsProvider settingsProvider,
        IFileSaverService fileSaverService,
        IShareService shareService,
        IFilePickerService filePickerService,
        IMigrationService migrationService,
        INotificationService notificationService,
        IAppVersionService appVersionService)
    {
        _importExportService = importExportService;
        _settingsProvider = settingsProvider;
        _fileSaverService = fileSaverService;
        _shareService = shareService;
        _filePickerService = filePickerService;
        _migrationService = migrationService;
        _notificationService = notificationService;
        _appVersionService = appVersionService;

        MigrationLog = _migrationService.GetMigrationLog();
        AppVersion = _appVersionService.CurrentVersion;
    }

    [ObservableProperty]
    private AppSettings _settings = new();

    [ObservableProperty]
    private string _appVersion = "0.9.5";

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
                // Request OS-level notification permission (required on Android 13+)
                bool granted = await _notificationService.RequestNotificationPermissionAsync();
                if (!granted)
                {
                    Settings.NotificationsEnabled = false;
                    OnPropertyChanged(nameof(Settings));
                    SetError("Notification permission was denied. Please enable it in your device settings.");
                    return;
                }
            }

            Settings.NotificationsEnabled = enabled;
            if (!enabled)
            {
                Settings.ReadingRemindersEnabled = false;
                await _notificationService.CancelReadingReminderAsync();
            }
            await SaveSettingsInternalAsync();
        }, "Failed to update notification settings");
    }

    [RelayCommand]
    public async Task ToggleReadingRemindersAsync(bool enabled)
    {
        await ExecuteSafelyAsync(async () =>
        {
            if (enabled)
            {
                // Re-verify OS permission before scheduling (may have been revoked since toggle)
                bool granted = await _notificationService.RequestNotificationPermissionAsync();
                if (!granted)
                {
                    Settings.NotificationsEnabled = false;
                    Settings.ReadingRemindersEnabled = false;
                    OnPropertyChanged(nameof(Settings));
                    SetError("Notification permission was denied. Please enable it in your device settings.");
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
        }, "Failed to update reminder settings");
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
        }, "Failed to update reminder time");
    }

    [RelayCommand]
    public async Task ToggleGettingStartedCtaAsync(bool hide)
    {
        await ExecuteSafelyAsync(async () =>
        {
            Settings.HideGettingStartedCta = hide;
            await SaveSettingsInternalAsync();
        }, "Failed to update Getting Started visibility");
    }

    [RelayCommand]
    public async Task UpdateShelfLedgeColorAsync(string color)
    {
        await ExecuteSafelyAsync(async () =>
        {
            ShelfLedgeColor = color;
            Settings.ShelfLedgeColor = color;
            await SaveSettingsInternalAsync();
        }, "Failed to update shelf ledge color");
    }

    [RelayCommand]
    public async Task UpdateShelfBaseColorAsync(string color)
    {
        await ExecuteSafelyAsync(async () =>
        {
            ShelfBaseColor = color;
            Settings.ShelfBaseColor = color;
            await SaveSettingsInternalAsync();
        }, "Failed to update shelf base color");
    }

    private async Task SaveSettingsInternalAsync()
    {
        Settings.UpdatedAt = DateTime.UtcNow;
        await _settingsProvider.UpdateSettingsAsync(Settings);
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

            if (Settings.ReminderTime.HasValue)
            {
                ReminderHour = Settings.ReminderTime.Value.Hours;
                ReminderMinute = Settings.ReminderTime.Value.Minutes;
            }
        }, "Failed to load settings");
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            Settings.UpdatedAt = DateTime.UtcNow;
            await _settingsProvider.UpdateSettingsAsync(Settings);
        }, "Failed to save settings");
    }

    [RelayCommand]
    public async Task ExportDataAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            var json = await _importExportService.ExportToJsonAsync();
            var fileName = $"BookLoggerExport_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            await _fileSaverService.SaveFileAsync(fileName, json);
        }, "Failed to export data");
    }

    [RelayCommand]
    public async Task ImportDataAsync(string json)
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _importExportService.ImportFromJsonAsync(json);
        }, "Failed to import data");
    }

    [RelayCommand]
    public async Task DeleteAllDataAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            await _importExportService.DeleteAllDataAsync();
        }, "Failed to delete data");
    }

    [RelayCommand]
    public async Task BackupToCloudAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            // 1. Create ZIP Backup
            var backupPath = await _importExportService.CreateBackupAsync();

            // 2. Share File
            await _shareService.ShareFileAsync("BookLogger Backup", backupPath, "application/zip");
        }, "Failed to backup data");
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

            // Wait for the fire-and-forget DbInitializer to fully release its
            // scoped DbContext before we touch the DB file. On a fresh install
            // the user can race to Settings → Restore while DbInitializer is
            // still running its seed sync; File.Copy then corrupts the open
            // connection and every page blows up with "database disk image
            // malformed" until a manual restart.
            AppendLog("Waiting for database initialization to complete...");
            try
            {
                await DatabaseInitializationHelper.EnsureInitializedAsync();
                AppendLog("Database initialization confirmed complete.");
            }
            catch (Exception initEx)
            {
                AppendLog($"DB initialization failed earlier: {initEx.GetType().Name}: {initEx.Message}");
                SetError($"Cannot restore: database initialization failed earlier. Please restart BookHeart and try again. ({initEx.GetType().Name}: {initEx.Message})");
                return;
            }

            var filePath = await _filePickerService.PickFileAsync("Select Backup File", ".zip");
            AppendLog($"Picker returned: {filePath ?? "NULL"}");

            if (string.IsNullOrEmpty(filePath))
            {
                AppendLog("Restore cancelled (no file selected).");
                SetError("File selection failed or was cancelled. If you selected a file from Google Drive, try saving it to your device storage first.");
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
            SetError($"Failed to restore backup: {ex.GetType().Name}: {ex.Message}{detail}");
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
        // Refresh local property from source of truth
        MigrationLog = _migrationService.GetMigrationLog();
    }
}

