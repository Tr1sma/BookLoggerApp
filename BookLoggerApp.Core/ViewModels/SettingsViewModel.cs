using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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


    public SettingsViewModel(
        IImportExportService importExportService,
        IAppSettingsProvider settingsProvider,
        IFileSaverService fileSaverService,
        IShareService shareService,
        IFilePickerService filePickerService,
        IMigrationService migrationService,
        INotificationService notificationService)
    {
        _importExportService = importExportService;
        _settingsProvider = settingsProvider;
        _fileSaverService = fileSaverService;
        _shareService = shareService;
        _filePickerService = filePickerService;
        _migrationService = migrationService;
        _notificationService = notificationService;

        MigrationLog = _migrationService.GetMigrationLog();
    }

    [ObservableProperty]
    private AppSettings _settings = new();

    [ObservableProperty]
    private string _appVersion = "0.6.2";

    [ObservableProperty]
    private string _migrationLog;

    [ObservableProperty]
    private int _reminderHour = 20;

    [ObservableProperty]
    private int _reminderMinute = 0;

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
        await ExecuteSafelyAsync(async () =>
        {
            // 1. Pick File
            AppendLog("Opening file picker...");
            var filePath = await _filePickerService.PickFileAsync("Select Backup File", ".zip");
            AppendLog($"Picker returned path: {filePath ?? "NULL"}");
            
            if (string.IsNullOrEmpty(filePath))
            {
                AppendLog("Restore cancelled or path is empty.");
                // User might have thought they selected a file but the picker failed.
                SetError("File selection failed or was cancelled. If you selected a file from Google Drive, try saving it to your device storage first.");
                return; 
            }

            // 2. Restore
            AppendLog($"Calling RestoreFromBackupAsync with {filePath}");
            await _importExportService.RestoreFromBackupAsync(filePath);

            // 3. Reload settings/data
             await LoadAsync();
             AppendLog("Settings reloaded.");
            
        }, "Failed to restore backup");
    }
    private void AppendLog(string message)
    {
        _migrationService.Log(message);
        // Refresh local property from source of truth
        MigrationLog = _migrationService.GetMigrationLog();
    }
}

