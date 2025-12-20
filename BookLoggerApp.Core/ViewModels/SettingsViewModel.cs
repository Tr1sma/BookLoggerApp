using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IImportExportService _importExportService;
    private readonly IAppSettingsProvider _appSettingsProvider;

    public SettingsViewModel(
        IImportExportService importExportService,
        IAppSettingsProvider appSettingsProvider)
    {
        _importExportService = importExportService;
        _appSettingsProvider = appSettingsProvider;
    }

    [ObservableProperty]
    private AppSettings _settings = new();

    [ObservableProperty]
    private string _appVersion = "0.3.19";

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            Settings = await _appSettingsProvider.GetSettingsAsync();
        }, "Failed to load settings");
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            Settings.UpdatedAt = DateTime.UtcNow;
            await _appSettingsProvider.UpdateSettingsAsync(Settings);
        }, "Failed to save settings");
    }

    [RelayCommand]
    public async Task ExportDataAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            var json = await _importExportService.ExportToJsonAsync();
            // TODO: Save to file using platform-specific file picker
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
}

