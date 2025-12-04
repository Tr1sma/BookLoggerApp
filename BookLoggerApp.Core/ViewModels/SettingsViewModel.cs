using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BookLoggerApp.Core.Models;
using BookLoggerApp.Core.Services.Abstractions;

namespace BookLoggerApp.Core.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IImportExportService _importExportService;

    public SettingsViewModel(IImportExportService importExportService)
    {
        _importExportService = importExportService;
    }

    [ObservableProperty]
    private AppSettings _settings = new();

    [ObservableProperty]
    private readonly string _appVersion = "0.3.19";

    [RelayCommand]
    public async Task LoadAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            // TODO: Load settings from service when AppSettingsService is implemented
            // For now, use defaults
            Settings = new AppSettings();
        }, "Failed to load settings");
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        await ExecuteSafelyAsync(async () =>
        {
            Settings.UpdatedAt = DateTime.UtcNow;
            // TODO: Save settings when AppSettingsService is implemented
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

