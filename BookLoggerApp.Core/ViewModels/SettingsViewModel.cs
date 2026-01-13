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


    public SettingsViewModel(
        IImportExportService importExportService, 
        IAppSettingsProvider settingsProvider,
        IFileSaverService fileSaverService,
        IShareService shareService,
        IFilePickerService filePickerService,
        IMigrationService migrationService)

    {
        _importExportService = importExportService;
        _settingsProvider = settingsProvider;
        _fileSaverService = fileSaverService;
        _shareService = shareService;
        _filePickerService = filePickerService;
        _migrationService = migrationService;

        MigrationLog = _migrationService.GetMigrationLog();

    }

    [ObservableProperty]
    private AppSettings _settings = new();

    [ObservableProperty]
    private string _appVersion = "0.5.01";

    [ObservableProperty]
    private string _migrationLog;


    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            Settings = await _settingsProvider.GetSettingsAsync();
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
            var filePath = await _filePickerService.PickFileAsync("Select Backup File", ".zip");
            
            if (string.IsNullOrEmpty(filePath)) return; // User cancelled

            // 2. Restore
            await _importExportService.RestoreFromBackupAsync(filePath);

            // 3. Reload settings/data
             await LoadAsync();
            
        }, "Failed to restore backup");
    }
}

